using System.IO.Pipes;
using System.Threading.Tasks;

namespace Rengex.Translator {
  public class ChildForkTranslator {
    readonly ITranslator Translator;
    readonly NamedPipeClientStream PipeClient;

    public ChildForkTranslator(ITranslator basis, string pipeName = ParentForkTranslator.DefaultPipeName) {
      PipeClient = new NamedPipeClientStream(".", pipeName);
      Translator = basis;
    }

    public async Task Serve() {
      await PipeClient.ConnectAsync(5000).ConfigureAwait(false);
      try {
        if (!PipeClient.IsConnected) {
          return;
        }
        while (true) {
          object src = await PipeClient.ReadObjAsync<object>().ConfigureAwait(false);
          if (src is bool) {
            break;
          }
          string done = await Translator.Translate(src as string).ConfigureAwait(false);
          await PipeClient.WriteObjAsync(done).ConfigureAwait(false);
        }
      }
      finally {
        PipeClient.Dispose();
      }
    }
  }
}
