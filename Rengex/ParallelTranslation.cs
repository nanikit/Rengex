namespace Rengex {
  using Rengex.Translator;
  using System;
  using System.Threading.Tasks;
  using System.Windows.Media;
  using static Rengex.Translator.SplitTranslator;

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
      return phase switch
      {
        TranslationPhase.Complete => "완료",
        TranslationPhase.Error => "에러 - ",
        TranslationPhase.Export => "병합 중…",
        TranslationPhase.Import => "추출 중…",
        TranslationPhase.Translation => "번역 중…",
        TranslationPhase.Init => "대기 중…",
        _ => "",
      };
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

    public override async Task Process() {
      await translation.ExtractSourceText().ConfigureAwait(false);
      SetProgress(TranslationPhase.Complete, 100);
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
    private readonly ITranslator translator;
    private int translationSize;

    public TranslateJp2Kr(TranslationUnit tu, ITranslator engine) : base(tu) {
      translator = engine;
    }

    public async override Task Process() {
      Phase = TranslationPhase.Translation;

      using (var splitter = new SplitTranslator(translator, this)) {
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

  class OneStopJp2Kr : Jp2KrWork, IJp2KrLogger {
    private readonly ITranslator translator;
    private int translationSize;

    public OneStopJp2Kr(TranslationUnit tu, ITranslator engine) : base(tu) {
      translator = engine;
    }

    public async override Task Process() {
      SetProgress(TranslationPhase.Import, 0);
      await translation.ExtractSourceText();

      SetProgress(TranslationPhase.Translation, 10);
      using (var splitter = new SplitTranslator(translator, this)) {
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
