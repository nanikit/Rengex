using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Rengex.Translator {
  public class ParentForkTranslator : ITranslator {
    private readonly string _pipeName;
    private readonly NamedPipeServerStream _pipeServer;
    private readonly CancellationToken _cancellation;
    private Process? _child;

    public ParentForkTranslator(CancellationToken cancellation) {
      _cancellation = cancellation;

      var guid = Guid.NewGuid();
      _pipeName = $"{guid}";
      _pipeServer = new NamedPipeServerStream(_pipeName);
    }

    public async Task<string> Translate(string source) {
      await SendTranslationWork(source).ConfigureAwait(false);
      return await ReceiveTranslation().ConfigureAwait(false);
    }

    private async Task SendTranslationWork(string script) {
      await EnsureChild().ConfigureAwait(false);
      await _pipeServer.WriteObjAsync(script).ConfigureAwait(false);
    }

    private async Task EnsureChild() {
      if (_pipeServer.IsConnected) {
        return;
      }
      if (!await RecreateAndConnect().ConfigureAwait(false)) {
        throw new ApplicationException("번역 프로세스 초기화에 실패했습니다");
      }
    }

    private async Task<bool> RecreateAndConnect() {
      DisposeChild();

      Task connection = _pipeServer.WaitForConnectionAsync(_cancellation);

      string path = Process.GetCurrentProcess().MainModule!.FileName!;
      _child = Process.Start(path, new string[] { "--connect", _pipeName });
      Task kill = _child.WaitForExitAsync(_cancellation);

      Task finished = await Task.WhenAny(connection, kill).ConfigureAwait(false);
      return finished == connection;
    }

    private async Task<string> ReceiveTranslation() {
      return await _pipeServer.ReadObjAsync<string>().ConfigureAwait(false);
    }

    public void Dispose() {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) {
      if (disposing) {
        DisposePipe();
        DisposeChild();
      }
    }

    private void DisposePipe() {
      if (_pipeServer.IsConnected) {
        _pipeServer.WriteObjAsync(false).Wait(5000);
      }
      _pipeServer.Dispose();
    }

    private void DisposeChild() {
      if (_child != null) {
        if (!_child.HasExited) {
          _child.Kill();
        }
        _child.Dispose();
        _child = null;
      }
    }
  }
}
