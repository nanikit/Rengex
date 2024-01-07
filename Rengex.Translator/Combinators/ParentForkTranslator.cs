using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Rengex.Translator {

  public class ParentForkTranslator : ITranslator {
    private static readonly SemaphoreSlim _creationLock = new(1);
    private static DateTime _lastSpawnTime = DateTime.Now;
    private readonly CancellationToken _cancellation;
    private readonly string _pipeName;
    private readonly NamedPipeServerStream _pipeServer;
    private Process? _child;

    public ParentForkTranslator(CancellationToken cancellation) {
      _cancellation = cancellation;

      _pipeName = $"{Guid.NewGuid()}";
      _pipeServer = new NamedPipeServerStream(_pipeName);
    }

    public void Dispose() {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    public async Task<string> Translate(string source) {
      await SendTranslationWork(source).ConfigureAwait(false);
      return await ReceiveTranslation().ConfigureAwait(false);
    }

    protected virtual void Dispose(bool disposing) {
      if (disposing) {
        DisposePipe();
        DisposeChild();
      }
    }

    // Ehnd delete all and recreate temporary dictionary file,
    // so parallel initialization is dangerous.
    private static async Task DelayForEhndInitialization() {
      var delay = _lastSpawnTime.AddSeconds(5) - DateTime.Now;
      int milliseconds = (int)Math.Max(0, delay.TotalMilliseconds);
      await Task.Delay(milliseconds).ConfigureAwait(false);
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

    private void DisposePipe() {
      if (_pipeServer.IsConnected) {
        _pipeServer.WriteObjAsync(false).Wait(5000);
      }
      _pipeServer.Dispose();
    }

    private async Task EnsureChild() {
      if (_pipeServer.IsConnected) {
        return;
      }
      if (!await RecreateAndConnect().ConfigureAwait(false)) {
        throw new ApplicationException("번역 프로세스 초기화에 실패했습니다");
      }
    }

    private async Task<string> ReceiveTranslation() {
      try {
        return await _pipeServer.ReadObjAsync<string>().ConfigureAwait(false);
      }
      catch (EndOfStreamException) {
        throw new SubprocessTerminatedException();
      }
    }

    private async Task<bool> RecreateAndConnect() {
      DisposeChild();

      await _creationLock.WaitAsync(_cancellation).ConfigureAwait(false);
      try {
        await DelayForEhndInitialization().ConfigureAwait(false);

        var connection = _pipeServer.WaitForConnectionAsync(_cancellation);
        string path = Environment.ProcessPath!;
        _child = Process.Start(path, new string[] { "--connect", _pipeName });
        var kill = _child.WaitForExitAsync(_cancellation);

        var finished = await Task.WhenAny(connection, kill).ConfigureAwait(false);
        return finished == connection;
      }
      finally {
        _lastSpawnTime = DateTime.Now;
        _creationLock.Release();
      }
    }

    private async Task SendTranslationWork(string script) {
      await EnsureChild().ConfigureAwait(false);
      await _pipeServer.WriteObjAsync(script).ConfigureAwait(false);
    }
  }

  internal class SubprocessTerminatedException : Exception {
    public SubprocessTerminatedException() : base("Subprocess has been terminated during translation") { }
  }
}
