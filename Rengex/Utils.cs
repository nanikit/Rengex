using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Rengex {
  public static class Utils {
    public static string PrecreateDirectory(string path) {
      Directory.CreateDirectory(Path.GetDirectoryName(path));
      return path;
    }

    public static Task ForEachAsync<T>(this IEnumerable<T> source, int dop, Func<T, Task> body) {
      IList<IEnumerator<T>> parts = Partitioner.Create(source).GetPartitions(dop);
      IEnumerable<Task> tasks = parts.Select(p => Task.Run(async () => {
        using (p) {
          while (p.MoveNext()) {
            await body(p.Current);
          }
        }
      }));
      return Task.WhenAll(tasks);
    }
  }

  public class MyBufferBlock<T> {

    private class Client {
      public TaskCompletionSource<T> Waiting;
      public CancellationTokenRegistration Cancelling;
    }

    private ConcurrentQueue<T> DataQueue = new ConcurrentQueue<T>();
    private ConcurrentQueue<Client> Clients = new ConcurrentQueue<Client>();

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
