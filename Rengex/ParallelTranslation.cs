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

    private Task ParallelForEach(Func<TranslationUnit, Task> action) {
      IEnumerable<TranslationUnit> translations = Translations ?? FindTranslations();
      return translations.ForEachAsync(WorkerCount, async t => {
        try {
          await action(t).ConfigureAwait(false);
        }
        catch (Exception e) {
          OnError(t, e);
        }
      });
    }


    public Task ImportTranslation() {
      return ParallelForEach(translation => {
        OnImport(translation);
        translation.ExtractSourceText();
        OnComplete(translation);
        return Task.CompletedTask;
      });
    }

    public async Task MachineTranslation() {
      var translator = new ForkTranslator(WorkerCount);
      await ParallelForEach(async translation => {
        OnTranslation(translation);
        await translation.MachineTranslate(translator).ConfigureAwait(false);
        OnComplete(translation);
      }).ConfigureAwait(false);
      translator.Dispose();
    }

    public Task ExportTranslation() {
      return ParallelForEach(translation => {
        OnExport(translation);
        translation.BuildTranslation();
        OnComplete(translation);
        return Task.CompletedTask;
      });
    }

    public async Task OnestopTranslation() {
      var translator = new ForkTranslator(WorkerCount);
      await ParallelForEach(async translation => {
        OnImport(translation);
        translation.ExtractSourceText();
        OnTranslation(translation);
        await translation.MachineTranslate(translator).ConfigureAwait(false);
        OnExport(translation);
        translation.BuildTranslation();
        OnComplete(translation);
      }).ConfigureAwait(false);
      translator.Dispose();
    }
  }
}
