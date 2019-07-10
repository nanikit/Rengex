using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Rengex {
  public static class Util {
    public static string PrecreateDirectory(string path) {
      Directory.CreateDirectory(Path.GetDirectoryName(path));
      return path;
    }

    public static string GetEllipsisPath(string path, int len) {
      string ellipsisPath = path.Length > len
        ? $"...{path.Substring(path.Length - len)}"
        : path;
      return ellipsisPath;
    }

    public static Task ForEachAsync<T>(this IEnumerable<T> source, int dop, Func<T, Task> body) {
      IList<IEnumerator<T>> parts = Partitioner.Create(source).GetPartitions(dop);
      IEnumerable<Task> tasks = parts.Select(p => Task.Run(async () => {
        using (p) {
          while (p.MoveNext()) {
            await body(p.Current).ConfigureAwait(false);
          }
        }
      }));
      return Task.WhenAll(tasks);
    }

    public static Task ForEachPinnedAsync<T>(this IEnumerable<T> source, int dop, Func<T, Task> body) {
      IList<IEnumerator<T>> parts = Partitioner.Create(source).GetPartitions(dop);
      IEnumerable<Task> tasks = parts.Select(async p => {
        using (p) {
          while (p.MoveNext()) {
            await body(p.Current);
          }
        }
      });
      return Task.WhenAll(tasks);
    }
  }

  public class ViewModelBase : INotifyPropertyChanged {
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// It should be called only in the property setter.
    /// </summary>
    protected void Set<T>(ref T member, T value, [CallerMemberName] string name = null) {
      if (Equals(member, value)) {
        return;
      }
      member = value;
      NotifyChange(name);
    }

    protected void NotifyChange(string name) {
      var ev = new PropertyChangedEventArgs(name);
      PropertyChanged?.Invoke(this, ev);
    }
  }

  public class MyBufferBlock<T> {

    private class Client {
      public TaskCompletionSource<T> Waiting;
      public CancellationTokenRegistration Cancelling;
    }

    private readonly ConcurrentQueue<T> DataQueue = new ConcurrentQueue<T>();
    private readonly ConcurrentQueue<Client> Clients = new ConcurrentQueue<Client>();

    public int PendingSize => DataQueue.Count;

    public int HungerSize => Clients.Count;

    public Task<T> ReceiveAsync() {
      return ReceiveAsync(CancellationToken.None);
    }

    public Task<T> ReceiveAsync(CancellationToken token) {
      var ret = new TaskCompletionSource<T>();
      if (DataQueue.TryDequeue(out T res)) {
        ret.SetResult(res);
      }
      else {
        CancellationTokenRegistration c;
        c = token.Register(() => ret.TrySetCanceled(token));
        Clients.Enqueue(new Client { Waiting = ret, Cancelling = c });
      }
      return ret.Task;
    }

    public void Enqueue(T value) {
      while (Clients.TryDequeue(out Client client)) {
        client.Cancelling.Dispose();
        if (client.Waiting.TrySetResult(value)) {
          return;
        }
      }
      DataQueue.Enqueue(value);
    }

    public void Abort() {
      while (Clients.TryDequeue(out Client client)) {
        client.Cancelling.Dispose();
        client.Waiting.TrySetCanceled();
      }
    }
  }
}
