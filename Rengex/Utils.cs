using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Rengex {
  public static class Utils {
    public static string PrecreateDirectory(string path) {
      Directory.CreateDirectory(Path.GetDirectoryName(path));
      return path;
    }
  }

  public class MyBufferBlock<T> {
    private ConcurrentQueue<T> DataQueue = new ConcurrentQueue<T>();
    private ConcurrentQueue<TaskCompletionSource<T>> Workers =
      new ConcurrentQueue<TaskCompletionSource<T>>();

    public Task<T> ReceiveAsync() {
      return ReceiveAsync(CancellationToken.None);
    }

    public Task<T> ReceiveAsync(CancellationToken token) {
      var ret = new TaskCompletionSource<T>();
      if (DataQueue.TryDequeue(out T res)) {
        ret.SetResult(res);
      }
      else {
        Workers.Enqueue(ret);
        token.Register(new Action(() => ret.TrySetCanceled(token)));
      }
      return ret.Task;
    }

    public void Enqueue(T value) {
      while (Workers.TryDequeue(out TaskCompletionSource<T> worker)) {
        if (worker.TrySetResult(value)) return;
      }
      DataQueue.Enqueue(value);
    }

    public void Abort() {
      while (Workers.TryDequeue(out TaskCompletionSource<T> worker)) {
        worker.SetException(new TaskCanceledException());
      }
    }
  }
}
