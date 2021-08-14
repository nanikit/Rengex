using Nanikit.Ehnd;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Rengex.Translator {
  public sealed class EhndTranslator : ITranslator {
    private static Ehnd? Instance;
    private static Task<string> _task = Task.FromResult("");

    public EhndTranslator(string? eztransDirectory) {
      if (Instance != null) {
        return;
      }
      var path = eztransDirectory != null ? Path.Combine(eztransDirectory, Ehnd.DllName) : null;
      Instance = new Ehnd(path);
    }

    public Task<string> Translate(string source) {
      if (Instance == null) {
        throw new Exception("Assertion failed. Instance is null.");
      }

      var previousTask = _task;
      _task = Task.Run(async () => {
        await previousTask.ConfigureAwait(false);
        return Instance.Translate(source);
      });
      return _task;
    }

    public void Dispose() {
      GC.SuppressFinalize(this);
    }
  }
}
