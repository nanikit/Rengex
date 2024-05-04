using Nanikit.Ehnd;
using Rengex.Helper;
using Rengex.Model;
using Rengex.Translator;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Rengex {

  public class Jp2KrDesignVM : Jp2KrTranslationVM {

    public Jp2KrDesignVM() : base(null, null) {
      Ongoings.Add(new TestLabelProgressVM());
      Ongoings.Add(new TestLabelProgressVM());
      Faults.Add(new TestLabelProgressVM());
      Progress = new TestLabelProgressVM();
    }

    public new TestLabelProgressVM Progress { get; set; }

    public class TestLabelProgressVM : ILabelProgressVM {
      public Brush Foreground => new SolidColorBrush(Colors.PaleGreen);
      public string Label => "테스트";

      public double Value => 50;

      public void Cancel() {
      }
    }
  }

  public class Jp2KrTranslationVM : ViewModelBase {
    private static readonly int coreCount;

    private readonly RegexDotConfiguration? dotConfig;
    private readonly List<TranslationUnit>? translations;
    private EhndTranslator? _selfTranslator;
    private string? workKind;

    static Jp2KrTranslationVM() {
      coreCount = GetDesiredSubprocessCount();
    }

    /// <summary>
    /// if paths is null, search from metadata folder.
    /// </summary>
    public Jp2KrTranslationVM(RegexDotConfiguration? dot, string[]? paths = null) {
      dotConfig = dot;
      translations = paths?.SelectMany(p => WalkForSources(p))?.ToList();
      Progress = new LabelProgressVM();
      Faults = new ObservableCollection<ILabelProgressVM>();
      Ongoings = new ObservableCollection<ILabelProgressVM>();
    }

    public List<Exception> Exceptions { get; private set; } = new List<Exception>();
    public ObservableCollection<ILabelProgressVM> Faults { get; private set; }
    public ObservableCollection<ILabelProgressVM> Ongoings { get; private set; }
    public LabelProgressVM Progress { get; private set; }

    public Task ExportTranslation() {
      workKind = "병합: ";
      return ParallelForEach(x => new MergeJp2Kr(x));
    }

    public Task ImportTranslation() {
      workKind = "추출: ";
      return ParallelForEach(x => new ImportJp2Kr(x));
    }

    public async Task MachineTranslation() {
      workKind = "번역: ";
      _selfTranslator ??= new EhndTranslator(Properties.Settings.Default.EzTransDir);

      using var engine = new ForkTranslator(coreCount, _selfTranslator);

      Jp2KrWork genVm(TranslationUnit x) {
        return new TranslateJp2Kr(x, engine);
      }

      await ParallelForEach(genVm).ConfigureAwait(false);
    }

    public async Task OneStopTranslation() {
      workKind = "원터치: ";
      _selfTranslator ??= new EhndTranslator(Properties.Settings.Default.EzTransDir);

      using var engine = new ForkTranslator(coreCount, _selfTranslator);

      Jp2KrWork genVm(TranslationUnit x) {
        return new OneStopJp2Kr(x, engine);
      }

      await ParallelForEach(genVm).ConfigureAwait(false);
    }

    private static int GetDesiredSubprocessCount() {
      int coreCount = 0;
      foreach (var item in new ManagementObjectSearcher("Select * from Win32_Processor").Get()) {
        string row = $"{item["NumberOfCores"]}";
        if (int.TryParse(row, out int count)) {
          coreCount += count;
        }
      }

      // Main process doesn't run ehnd for preventing crash.
      int countExceptCurrentProcess = coreCount + 1;
      return countExceptCurrentProcess;
    }

    private IEnumerable<TranslationUnit> FindTranslations() {
      return WalkForSources(ManagedPath.MetadataDirectory);
    }

    private Task ParallelForEach(Func<TranslationUnit, Jp2KrWork> genViewModel) {
      var translationUnits = translations ?? FindTranslations().ToList();
      int complete = 0;
      Progress.Value = 0;
      Progress.Label = $"{workKind}{complete} / {translationUnits.Count}";

      return translationUnits.ForEachPinnedAsync(coreCount, async t => {
        var item = genViewModel(t);
        Ongoings.Add(item.Progress);
        try {
          await Task.Run(() => item.Process());
        }
        catch (EhndNotFoundException) {
          Progress.Foreground = LabelProgressVM.FgError;
          throw;
        }
        catch (Exception e) {
          Progress.Foreground = LabelProgressVM.FgError;
          item.SetProgress(TranslationPhase.Complete, 100, e.Message);
          Faults.Add(item.Progress);
          Exceptions.Add(e);
        }
        finally {
          _ = Ongoings.Remove(item.Progress);
          complete++;
          Progress.Value = (double)complete / translationUnits.Count * 100;
          Progress.Label = $"{workKind}{complete} / {translationUnits.Count}";
        }
      });
    }

    private IEnumerable<TranslationUnit> WalkForSources(string path) {
      if (dotConfig == null) {
        return Enumerable.Empty<TranslationUnit>();
      }

      return ManagedPath
        .WalkForSources(path)
        .Select(x => new TranslationUnit(dotConfig, x));
    }
  }
}
