using Rengex.Translator;
using System;
using System.Threading.Tasks;
using System.Windows.Media;
using static Rengex.Translator.SplitTranslater;

namespace Rengex {

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
    Init, Import, Translation, Export, Complete, Error
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
        case TranslationPhase.Init:
          return "대기 중…";
        default:
          return "";
      }
    }

    private static string GetEllipsisPath(TranslationUnit unit) {
      return Util.GetEllipsisPath(unit.Workspace.RelativePath, 30);
    }

    public string EllipsisPath { get; private set; }

    public LabelProgressVM Progress { get; private set; }

    public TranslationPhase Phase { get; set; }

    protected TranslationUnit translation;

    public Jp2KrWork(TranslationUnit translation) {
      this.translation = translation;
      EllipsisPath = GetEllipsisPath(this.translation);
      Progress = new LabelProgressVM();
      SetProgress(TranslationPhase.Init, 0);
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
      translation.ExtractSourceText();
      SetProgress(TranslationPhase.Complete, 100);
      return Task.CompletedTask;
    }
  }

  class MergeJp2Kr : Jp2KrWork {
    public MergeJp2Kr(TranslationUnit tu) : base(tu) { }

    public override Task Process() {
      SetProgress(TranslationPhase.Export, 0);
      translation.BuildTranslation();
      SetProgress(TranslationPhase.Complete, 100);
      return Task.CompletedTask;
    }
  }

  class TranslateJp2Kr : Jp2KrWork, IJp2KrLogger {
    private readonly IJp2KrTranslator translator;
    private int translationSize;

    public TranslateJp2Kr(TranslationUnit tu, IJp2KrTranslator engine) : base(tu) {
      translator = engine;
    }

    public async override Task Process() {
      Phase = TranslationPhase.Translation;

      using (var splitter = new SplitTranslater(translator, this)) {
        await translation.MachineTranslate(splitter).ConfigureAwait(false);
      }

      SetProgress(TranslationPhase.Complete, 100);
    }

    public void OnStart(int total) {
      translationSize = total;
      OnProgress(0);
    }

    public void OnProgress(int current) {
      double ratio = (double)current / translationSize;
      double val = (double.IsNaN(ratio) ? 1 : ratio) * 100;
      string desc = $"({current}/{translationSize})";
      SetProgress(TranslationPhase.Translation, val, desc);
    }
  }

  class OnestopJp2Kr : Jp2KrWork, IJp2KrLogger {
    private readonly IJp2KrTranslator translator;
    private int translationSize;

    public OnestopJp2Kr(TranslationUnit tu, IJp2KrTranslator engine) : base(tu) {
      translator = engine;
    }

    public async override Task Process() {
      SetProgress(TranslationPhase.Import, 0);
      translation.ExtractSourceText();

      SetProgress(TranslationPhase.Translation, 10);
      using (var splitter = new SplitTranslater(translator, this)) {
        await translation.MachineTranslate(splitter).ConfigureAwait(false);
      }

      SetProgress(TranslationPhase.Export, 90);
      translation.BuildTranslation();

      SetProgress(TranslationPhase.Complete, 100);
    }

    public void OnStart(int total) {
      translationSize = total;
      OnProgress(0);
    }

    public void OnProgress(int current) {
      double ratio = (double)current / translationSize;
      double val = (double.IsNaN(ratio) ? 1 : ratio) * 80 + 10;
      string desc = $"({current}/{translationSize})";
      SetProgress(TranslationPhase.Translation, val, desc);
    }
  }
}
