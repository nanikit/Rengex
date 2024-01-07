namespace Rengex {

  using Rengex.Helper;
  using Rengex.Model;
  using Rengex.Translator;
  using System;
  using System.Threading.Tasks;
  using System.Windows.Media;
  using static Rengex.Translator.SplitTranslator;

  public enum TranslationPhase {
    Init, Import, Translation, Export, Complete, Error
  }

  public interface ILabelProgressVM {
    Brush Foreground { get; }
    string Label { get; }
    double Value { get; }

    void Cancel();
  }

  public class LabelProgressVM : ViewModelBase, ILabelProgressVM {
    public static Brush FgError = new SolidColorBrush(Colors.LightPink);
    public static Brush FgNormal = new SolidColorBrush(Colors.PaleGreen);
    private Brush _Foreground = FgNormal;

    private string _Label;

    private double _Value;

    public event Action CancelEvent = delegate { };

    public Brush Foreground {
      get => _Foreground;
      set => Set(ref _Foreground, value);
    }

    public string Label {
      get => _Label;
      set => Set(ref _Label, value);
    }

    public double Value {
      get => _Value;
      set => Set(ref _Value, value);
    }

    public void Cancel() {
      CancelEvent.Invoke();
    }
  }

  internal class ImportJp2Kr : Jp2KrWork {

    public ImportJp2Kr(TranslationUnit tu) : base(tu) {
    }

    public override async Task Process() {
      await translation.ExtractSourceText().ConfigureAwait(false);
      SetProgress(TranslationPhase.Complete, 100);
    }
  }

  internal abstract class Jp2KrWork {
    protected TranslationUnit translation;

    public Jp2KrWork(TranslationUnit translation) {
      this.translation = translation;
      EllipsisPath = GetEllipsisPath(this.translation);
      Progress = new LabelProgressVM();
      SetProgress(TranslationPhase.Init, 0);
    }

    public string EllipsisPath { get; private set; }

    public TranslationPhase Phase { get; set; }

    public LabelProgressVM Progress { get; private set; }

    public void Cancel() {
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

    private static string GetEllipsisPath(TranslationUnit unit) {
      return Util.GetEllipsisPath(unit.ManagedPath.RelativePath, 30);
    }

    private static string PhaseToString(TranslationPhase phase) {
      return phase switch {
        TranslationPhase.Complete => "완료",
        TranslationPhase.Error => "에러 - ",
        TranslationPhase.Export => "병합 중…",
        TranslationPhase.Import => "추출 중…",
        TranslationPhase.Translation => "번역 중…",
        TranslationPhase.Init => "대기 중…",
        _ => "",
      };
    }
  }

  internal class MergeJp2Kr : Jp2KrWork {

    public MergeJp2Kr(TranslationUnit tu) : base(tu) {
    }

    public override async Task Process() {
      SetProgress(TranslationPhase.Export, 0);
      await translation.BuildTranslation().ConfigureAwait(false);
      SetProgress(TranslationPhase.Complete, 100);
    }
  }

  internal class OneStopJp2Kr : Jp2KrWork, IJp2KrLogger {
    private readonly ITranslator translator;
    private int translationSize;

    public OneStopJp2Kr(TranslationUnit tu, ITranslator engine) : base(tu) {
      translator = engine;
    }

    public void OnProgress(int current) {
      double ratio = (double)current / translationSize;
      double val = (double.IsNaN(ratio) ? 1 : ratio) * 80 + 10;
      string desc = $"({current}/{translationSize})";
      SetProgress(TranslationPhase.Translation, val, desc);
    }

    public void OnStart(int total) {
      translationSize = total;
      OnProgress(0);
    }

    public override async Task Process() {
      SetProgress(TranslationPhase.Import, 0);
      await translation.ExtractSourceText();

      SetProgress(TranslationPhase.Translation, 10);
      using (var splitter = new SplitTranslator(translator, this)) {
        await translation.MachineTranslate(splitter).ConfigureAwait(false);
      }

      SetProgress(TranslationPhase.Export, 90);
      await translation.BuildTranslation().ConfigureAwait(false);

      SetProgress(TranslationPhase.Complete, 100);
    }
  }

  internal class TranslateJp2Kr : Jp2KrWork, IJp2KrLogger {
    private readonly ITranslator translator;
    private int translationSize;

    public TranslateJp2Kr(TranslationUnit tu, ITranslator engine) : base(tu) {
      translator = engine;
    }

    public void OnProgress(int current) {
      double ratio = (double)current / translationSize;
      double val = (double.IsNaN(ratio) ? 1 : ratio) * 100;
      string desc = $"({current}/{translationSize})";
      SetProgress(TranslationPhase.Translation, val, desc);
    }

    public void OnStart(int total) {
      translationSize = total;
      OnProgress(0);
    }

    public override async Task Process() {
      Phase = TranslationPhase.Translation;

      using (var splitter = new SplitTranslator(translator, this)) {
        await translation.MachineTranslate(splitter).ConfigureAwait(false);
      }

      SetProgress(TranslationPhase.Complete, 100);
    }
  }
}
