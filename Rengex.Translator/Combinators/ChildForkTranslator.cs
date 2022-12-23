using System;
using System.IO.Pipes;
using System.Text.Json;
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
          if (message is not JsonElement element || element.GetString() is not string text) {
            break;
          }
          string done = await Translator.Translate(text).ConfigureAwait(false);
          await PipeClient.WriteObjAsync(done).ConfigureAwait(false);
        }
        await PipeClient.FlushAsync().ConfigureAwait(false);
      }
      catch (Exception error) {
        System.Diagnostics.Debug.WriteLine(error);
      }
      finally {
        await PipeClient.DisposeAsync().ConfigureAwait(false);
      }
    }
  }
}
