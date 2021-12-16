using Nanikit.Ehnd;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Rengex.Translator {
  public sealed class EhndTranslator : ITranslator {
    private static IEhnd? Instance;

    public EhndTranslator(string? eztransDirectory) {
      if (Instance != null) {
        return;
      }
      var path = eztransDirectory != null ? Path.Combine(eztransDirectory, Ehnd.DllName) : null;
      Instance = new BatchEhnd(new Ehnd(path));
    }

    public Task<string> Translate(string source) {
      if (Instance == null) {
        throw new Exception("Assertion failed. Instance is null.");
      }

      return Task.Run(() => Instance.TranslateAsync(source));
    }

    public void Dispose() {
    }
  }
}
