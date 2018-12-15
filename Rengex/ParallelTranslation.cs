using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Rengex {
  public class ParallelTranslation {

    public event Action<TranslationUnit> OnImport = delegate { };
    public event Action<TranslationUnit> OnTranslation = delegate { };
    public event Action<TranslationUnit> OnExport = delegate { };
    public event Action<TranslationUnit> OnComplete = delegate { };
    public event Action<TranslationUnit, Exception> OnError = delegate { };

    private List<TranslationUnit> Translations;
    private RegexDotConfiguration DotConfig;
    private int WorkerCount;

    public ParallelTranslation(RegexDotConfiguration dot, string[] paths = null) {
      DotConfig = dot;
      Translations = paths?.SelectMany(p => WalkForSources(p))?.ToList();
      WorkerCount = 4;
    }

    private IEnumerable<TranslationUnit> WalkForSources(string path) {
      return CwdDesignator
        .WalkForSources(path)
        .Select(x => new TranslationUnit(DotConfig, x));
    }

    private IEnumerable<TranslationUnit> FindTranslations() {
      return WalkForSources(CwdDesignator.MetadataDirectory);
    }

    private void ParallelForEach(Action<TranslationUnit> action) {
      var po = new ParallelOptions() { MaxDegreeOfParallelism = WorkerCount };
      Parallel.ForEach(Translations ?? FindTranslations(), po, t => {
        try {
          action(t);
        }
        catch (Exception e) {
          OnError(t, e);
        }
      });
    }

    public void ImportTranslation() {
      ParallelForEach(translation => {
        OnImport(translation);
        translation.ExtractSourceText();
        OnComplete(translation);
      });
    }

    public void MachineTranslation() {
      var translator = new ConcurrentTranslator(WorkerCount);
      ParallelForEach(translation => {
        OnTranslation(translation);
        translation.MachineTranslate(translator).Wait();
        OnComplete(translation);
      });
      translator.Dispose();
    }

    public void ExportTranslation() {
      ParallelForEach(translation => {
        OnExport(translation);
        translation.BuildTranslation();
        OnComplete(translation);
      });
    }

    public void OnestopTranslation() {
      var translator = new ConcurrentTranslator(WorkerCount);
      ParallelForEach(translation => {
        OnImport(translation);
        translation.ExtractSourceText();
        OnTranslation(translation);
        translation.MachineTranslate(translator).Wait();
        OnExport(translation);
        translation.BuildTranslation();
        OnComplete(translation);
      });
      translator.Dispose();
    }
  }
}
