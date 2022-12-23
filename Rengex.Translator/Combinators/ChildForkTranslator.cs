using System;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace Rengex.Translator {
  public class ChildForkTranslator {
    readonly ITranslator Translator;
    readonly NamedPipeClientStream PipeClient;

    public ChildForkTranslator(ITranslator basis, string pipeName) {
      PipeClient = new NamedPipeClientStream(pipeName);
      Translator = basis;
    }

    public async Task Serve() {
      await PipeClient.ConnectAsync(10000).ConfigureAwait(false);
      try {
        if (!PipeClient.IsConnected) {
          System.Diagnostics.Debug.WriteLine("Child translation process connect timeout");
          return;
        }
        while (true) {
          object message = await PipeClient.ReadObjAsync<object>().ConfigureAwait(false);
          if (!(message is string start)) {
            break;
          }
          string done = await Translator.Translate(start).ConfigureAwait(false);
          await PipeClient.WriteObjAsync(done).ConfigureAwait(false);
        }
      }
      catch (Exception error) {
        System.Diagnostics.Debug.WriteLine(error);
      }
      finally {
        PipeClient.Dispose();
      }
    }
  }
}
