using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Rengex.Translator {
  /// <summary>
  /// It provides split translation with progress information.
  /// </summary>
  /// <remarks>
  /// It's Dispose() doesn't dispose base translator.
  /// </remarks>
  public class SplitTranslator : ITranslator {

    public interface IJp2KrLogger {
      void OnStart(int total);
      void OnProgress(int current);
    }

    private readonly ITranslator Backend;
    private readonly IJp2KrLogger? Logger;

    public SplitTranslator(ITranslator translator, IJp2KrLogger? progress = null) {
      Backend = translator;
      Logger = progress;
    }

    public async Task<string> Translate(string source) {
      Logger?.OnStart(source.Length);

      List<string> splits = ChunkByLines(source).ToList();
      List<Task<string>> splitTasks = splits.Select(Backend.Translate).ToList();
      var runnings = new List<Task<string>>(splitTasks);
      int translatedLength = 0;

      while (runnings.Count != 0) {
        Task<string> done = await Task.WhenAny(runnings).ConfigureAwait(false);
        runnings.Remove(done);

        int doneIdx = splitTasks.IndexOf(done);
        translatedLength += splits[doneIdx].Length;
        Logger?.OnProgress(translatedLength);
      }

      string[] results = await Task.WhenAll(splitTasks).ConfigureAwait(false);
      return string.Join("", results);
    }

    /// <summary>
    /// It does nothing.
    /// </summary>
    public void Dispose() {
    }

    // TODO: improve readability
    private static IEnumerable<string> ChunkByLines(string source) {
      int startIdx = 0;
      int endIdx = 0;
      while (true) {
        if (endIdx >= source.Length) {
          yield return source.Substring(startIdx, endIdx - startIdx);
          break;
        }
        if (source[endIdx] == '\n') {
          int len = endIdx - startIdx + 1;
          if (len > 2000) {
            yield return source.Substring(startIdx, len);
            startIdx = endIdx + 1;
          }
        }
        endIdx++;
      }
    }
  }
}
