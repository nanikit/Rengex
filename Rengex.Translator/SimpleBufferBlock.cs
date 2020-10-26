using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Rengex.Translator {
  public class SimpleBufferBlock<T> {

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
