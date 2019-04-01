using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using static Rengex.SplitTranslater;

namespace Rengex {

  public class Jp2KrDesignVM : Jp2KrTranslationVM {
    public class TestLabelProgressVM : ILabelProgressVM {
      public string Label => "테스트";

      public double Value => 50;

      public Brush Foreground => new SolidColorBrush(Colors.PaleGreen);

      public void Cancel() {
      }
    }
    public new TestLabelProgressVM Progress { get; set; }

    public Jp2KrDesignVM() : base(null, null) {
      Ongoings.Add(new TestLabelProgressVM());
      Ongoings.Add(new TestLabelProgressVM());
      Faults.Add(new TestLabelProgressVM());
      Progress = new TestLabelProgressVM();
    }
  }

  public class Jp2KrTranslationVM : ViewModelBase {

    public readonly List<Exception> Exceptions = new List<Exception>();
    public ObservableCollection<ILabelProgressVM> Ongoings { get; private set; }
    public ObservableCollection<ILabelProgressVM> Faults { get; private set; }

    public LabelProgressVM Progress { get; private set; }

    private int WorkerCount;
    private RegexDotConfiguration DotConfig;
    private List<TranslationUnit> Translations;
    private string WorkKind;

    /// <summary>
    /// if paths is null, search from metadata folder.
    /// </summary>
    public Jp2KrTranslationVM(RegexDotConfiguration dot, string[] paths = null) : this(dot) {
      Translations = paths?.SelectMany(p => WalkForSources(p))?.ToList();
    }

    private Jp2KrTranslationVM(RegexDotConfiguration dot) {
      DotConfig = dot;
      WorkerCount = Environment.ProcessorCount;
      Ongoings = new ObservableCollection<ILabelProgressVM>();
      Faults = new ObservableCollection<ILabelProgressVM>();
      Progress = new LabelProgressVM();
    }

    public Jp2KrTranslationVM Clone() {
      return new Jp2KrTranslationVM(DotConfig) {
        Translations = Translations
      };
    }

    public Task ImportTranslation() {
      WorkKind = "추출: ";
      return ParallelForEach(x => new ImportJp2Kr(x));
    }

    public async Task MachineTranslation() {
      WorkKind = "번역: ";
      using (var engine = new ForkTranslator(WorkerCount)) {
        Jp2KrWork genVm(TranslationUnit x) => new TranslateJp2Kr(x, engine);
        await ParallelForEach(genVm).ConfigureAwait(false);
      }
    }

    public Task ExportTranslation() {
      WorkKind = "병합: ";
      return ParallelForEach(x => new MergeJp2Kr(x));
    }

    public async Task OnestopTranslation() {
      WorkKind = "원터치: ";
      using (var engine = new ForkTranslator(WorkerCount)) {
        Jp2KrWork genVm(TranslationUnit x) => new OnestopJp2Kr(x, engine);
        await ParallelForEach(genVm).ConfigureAwait(false);
      }
    }

    private IEnumerable<TranslationUnit> WalkForSources(string path) {
      return CwdDesignator
        .WalkForSources(path)
        .Select(x => new TranslationUnit(DotConfig, x));
    }

    private IEnumerable<TranslationUnit> FindTranslations() {
      return WalkForSources(CwdDesignator.MetadataDirectory);
    }

    private Task ParallelForEach(Func<TranslationUnit, Jp2KrWork> genViewModel) {
      List<TranslationUnit> translations = Translations ?? FindTranslations().ToList();
      int complete = 0;
      Progress.Value = 0;
      Progress.Label = $"{WorkKind}{complete} / {translations.Count}";

      return translations.ForEachPinnedAsync(WorkerCount, async t => {
        Jp2KrWork item = genViewModel(t);
        Ongoings.Add(item.Progress);
        try {
          await Task.Run(() => item.Process());
        }
        catch (EzTransNotFoundException e) {
          Progress.Foreground = LabelProgressVM.FgError;
          throw e;
        }
        catch (Exception e) {
          Progress.Foreground = LabelProgressVM.FgError;
          string msg;
          if (e is RegexMatchTimeoutException) {
            msg = "정규식 검색이 너무 오래 걸립니다. 정규식을 점검해주세요.";
          }
          else {
            msg = e.Message;
          }
          item.SetProgress(TranslationPhase.Error, 100, msg);
          Faults.Add(item.Progress);
          Exceptions.Add(e);
        }
        finally {
          Ongoings.Remove(item.Progress);
          complete++;
          Progress.Value = (double)complete / translations.Count * 100;
          Progress.Label = $"{WorkKind}{complete} / {translations.Count}";
        }
      });
    }

    public void Cancel() {
    }
  }

  public interface ILabelProgressVM {
    string Label { get; }
    double Value { get; }
    Brush Foreground { get; }
    void Cancel();
  }

  public class LabelProgressVM : ViewModelBase, ILabelProgressVM {

    public static Brush FgNormal = new SolidColorBrush(Colors.PaleGreen);
    public static Brush FgError = new SolidColorBrush(Colors.LightPink);

    public event Action CancelEvent = delegate { };

    private string _Label;
    public string Label {
      get => _Label;
      set => Set(ref _Label, value);
    }

    private double _Value;
    public double Value {
      get => _Value;
      set => Set(ref _Value, value);
    }

    private Brush _Foreground = FgNormal;
    public Brush Foreground {
      get => _Foreground;
      set => Set(ref _Foreground, value);
    }

    public void Cancel() {
      CancelEvent.Invoke();
    }
  }

  public enum TranslationPhase {
    Import, Translation, Export, Complete, Error
  }

  abstract class Jp2KrWork {

    private static string PhaseToString(TranslationPhase phase) {
      switch (phase) {
        case TranslationPhase.Complete:
          return "완료";
        case TranslationPhase.Error:
          return "에러 - ";
        case TranslationPhase.Export:
          return "병합 중…";
        case TranslationPhase.Import:
          return "추출 중…";
        case TranslationPhase.Translation:
          return "번역 중…";
        default:
          return "";
      }
    }

    private static string GetEllipsisPath(TranslationUnit unit) {
      return Util.GetEllipsisPath(unit.Workspace.RelativePath, 30);
    }

    protected TranslationUnit Translation;

    public string EllipsisPath { get; private set; }

    public TranslationPhase Phase;

    public LabelProgressVM Progress { get; private set; }

    public Jp2KrWork(TranslationUnit translation) {
      Translation = translation;
      EllipsisPath = GetEllipsisPath(Translation);
      Progress = new LabelProgressVM();
    }

    public abstract Task Process();

    public void SetProgress(TranslationPhase phase, double progress, string desc = null) {
      Phase = phase;
      Progress.Value = progress;

      string status = PhaseToString(Phase);
      Progress.Label = $"{EllipsisPath}: {status}{desc}";

      if (Phase == TranslationPhase.Error) {
        Progress.Foreground = LabelProgressVM.FgError;
      }
    }

    public void Cancel() {
    }
  }

  class ImportJp2Kr : Jp2KrWork {
    public ImportJp2Kr(TranslationUnit tu) : base(tu) { }

    public override Task Process() {
      Translation.ExtractSourceText();
      SetProgress(TranslationPhase.Complete, 100);
      return Task.CompletedTask;
    }
  }

  class MergeJp2Kr : Jp2KrWork {
    public MergeJp2Kr(TranslationUnit tu) : base(tu) { }

    public override Task Process() {
      SetProgress(TranslationPhase.Export, 0);
      Translation.BuildTranslation();
      SetProgress(TranslationPhase.Complete, 100);
      return Task.CompletedTask;
    }
  }

  class TranslateJp2Kr : Jp2KrWork, IJp2KrLogger {
    private IJp2KrTranslator Translator;
    private int TranslationSize;

    public TranslateJp2Kr(TranslationUnit tu, IJp2KrTranslator engine) : base(tu) {
      Translator = engine;
    }

    public async override Task Process() {
      Phase = TranslationPhase.Translation;

      var translator = new SplitTranslater(Translator, this);
      await Translation.MachineTranslate(translator).ConfigureAwait(false);

      SetProgress(TranslationPhase.Complete, 100);
    }

    public void OnStart(int total) {
      TranslationSize = total;
    }

    public void OnProgress(int current) {
      double ratio = (double)current / TranslationSize;
      double val = (double.IsNaN(ratio) ? 1 : ratio) * 100;
      string desc = $"({current}/{TranslationSize})";
      SetProgress(TranslationPhase.Translation, val, desc);
    }
  }

  class OnestopJp2Kr : Jp2KrWork, IJp2KrLogger {
    private IJp2KrTranslator Translator;
    private int TranslationSize;

    public OnestopJp2Kr(TranslationUnit tu, IJp2KrTranslator engine) : base(tu) {
      Translator = engine;
    }

    public async override Task Process() {
      Translation.ExtractSourceText();
      SetProgress(TranslationPhase.Translation, 10);

      var translator = new SplitTranslater(Translator, this);
      await Translation.MachineTranslate(translator).ConfigureAwait(false);

      SetProgress(TranslationPhase.Export, 90);
      Translation.BuildTranslation();

      SetProgress(TranslationPhase.Complete, 100);
    }

    public void OnStart(int total) {
      TranslationSize = total;
    }

    public void OnProgress(int current) {
      double ratio = (double)current / TranslationSize;
      double val = (double.IsNaN(ratio) ? 1 : ratio) * 80 + 10;
      string desc = $"({current}/{TranslationSize})";
      SetProgress(TranslationPhase.Translation, val, desc);
    }
  }
}
