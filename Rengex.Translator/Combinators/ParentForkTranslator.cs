using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace Rengex.Translator {
  public class ParentForkTranslator : ITranslator {
    public const string DefaultPipeName = "rengex_subtrans";

    /// <summary>
    /// Ehnd에서 크래시가 나는 경우가 있는데 견고한 방법을 찾지 못함.
    /// </summary>
    int MsInitDelay = 200;
    Process? Child;
    readonly string PipeName;
    NamedPipeServerStream PipeServer;

    public ParentForkTranslator(string pipeName = DefaultPipeName) {
      PipeName = pipeName;
      PipeServer = new NamedPipeServerStream(PipeName, PipeDirection.InOut, Environment.ProcessorCount + 2);
    }

    public async Task<string> Translate(string source) {
      try {
        await SendTranslationWork(source).ConfigureAwait(false);
        return await ReceiveTranslation().ConfigureAwait(false);
      }
      catch (Exception e) {
        MsInitDelay += 500;
        throw e;
      }
    }

    private async Task SendTranslationWork(string script) {
      if (!PipeServer.IsConnected) {
        await InitializeChild().ConfigureAwait(false);
      }
      await PipeServer.WriteObjAsync(script).ConfigureAwait(false);
    }

    private async Task InitializeChild() {
      DisposeChild();
      Task connection = PipeServer.WaitForConnectionAsync();
      string path = Process.GetCurrentProcess().MainModule.FileName;
      Child = Process.Start(path, MsInitDelay.ToString());
      Task finished = await Task.WhenAny(connection, Task.Delay(2000)).ConfigureAwait(false);
      if (finished != connection) {
        throw new ApplicationException("서브 프로세스가 응답하지 않습니다.");
      }
    }

    private async Task<string> ReceiveTranslation() {
      return await PipeServer.ReadObjAsync<string>().ConfigureAwait(false);
    }

    public void Dispose() {
      Dispose(true);
    }

    protected virtual void Dispose(bool disposing) {
      if (disposing) {
        DisposePipe();
        DisposeChild();
      }
    }

    private void DisposePipe() {
      if (PipeServer != null) {
        if (PipeServer.IsConnected) {
          PipeServer.WriteObjAsync(false).Wait(5000);
        }
        PipeServer.Dispose();
      }
    }

    private void DisposeChild() {
      if (Child != null) {
        if (!Child.HasExited) {
          Child.Kill();
        }
        Child.Dispose();
      }
    }
  }
}
