using Microsoft.Win32;
using Rengex.Translator;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rengex.View {

  public enum Operation {
    None, Import, Translate, Export, Onestop
  }

  public class AzureIfEqualConverter : MarkupExtension, IValueConverter {
    private static readonly Brush azureBrush = new SolidColorBrush(Colors.Azure);
    private static readonly Brush transparentBrush = new SolidColorBrush(Colors.Transparent);

    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
      if (value.Equals(parameter)) {
        return azureBrush;
      }
      return transparentBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
      throw new NotImplementedException();
    }

    public override object ProvideValue(IServiceProvider serviceProvider) {
      return this;
    }
  }

  class MainWindowVM : ViewModelBase {

    public Operation DefaultOperation {
      get => defaultButton;
      set => Set(ref defaultButton, value);
    }

    public ICommand OperateCommand { get; private set; }
    public ICommand PinCommand { get; private set; }

    public event Action<object> LogAdded;

    // TODO: hide this
    public RegexDotConfiguration dotConfig;

    private string[] paths;
    private Operation defaultButton;
    private Jp2KrTranslationVM translator;

    public MainWindowVM() {
      DefaultOperation = Operation.Onestop;
      OperateCommand = new RelayCommand<Operation>(RunOperation);
      PinCommand = new RelayCommand<Operation>(PinAction);
      dotConfig = EnsureConfiguration();
    }

    public void RunDefaultOperation(string[] paths) {
      this.paths = paths;
      RunOperation(DefaultOperation);
    }

    private void RunOperation(Operation kind) {
      switch (kind) {
        case Operation.Import:
          Import();
          break;
        case Operation.Translate:
          Translate();
          break;
        case Operation.Export:
          Export();
          break;
        case Operation.Onestop:
          Onestop();
          break;
      }
    }

    private void PinAction(Operation kind) {
      if (DefaultOperation == kind) {
        DefaultOperation = Operation.None;
      }
      else {
        DefaultOperation = kind;
      }
    }

    private bool IsIdle() => translator == null;

    private void Import() {
      paths = AskImportPaths();
      if (paths != null) {
        _ = Operate(tvm => tvm.ImportTranslation());
      }
    }
    private void Translate() {
      _ = Operate(tvm => tvm.MachineTranslation());
    }
    private void Export() {
      _ = Operate(tvm => tvm.ExportTranslation());
    }
    private void Onestop() {
      _ = Operate(tvm => tvm.OnestopTranslation());
    }

    private async Task Operate(Func<Jp2KrTranslationVM, Task> tasker) {
      if (!IsIdle()) {
        Log("이미 작업 중입니다. 나중에 시도해주세요.\r\n");
        return;
      }

      translator = new Jp2KrTranslationVM(dotConfig, paths);
      Log(translator);

      try {
        await LoopIfEztransNotFound(tasker).ConfigureAwait(false);
      }
      catch (UnauthorizedAccessException) {
        Log("파일이 사용 중이거나 읽기 전용인지 확인해보세요.");
      }
      catch (Exception e) {
        if (translator.Exceptions.Count == 0) {
          translator.Exceptions.Add(e);
        }
        Log($"오류가 발생했습니다. 진행 표시줄을 복사하면 오류 내용이 복사됩니다.\r\n");
      }
      finally {
        translator = null;
      }
    }

    private async Task LoopIfEztransNotFound(Func<Jp2KrTranslationVM, Task> tasker) {
      while (true) {
        Task ongoing = tasker(translator);
        try {
          await ongoing.ConfigureAwait(false);
          Log("작업이 끝났습니다.\r\n");
          return;
        }
        catch (EztransNotFoundException) {
          string ezDir = AskEztransDir();
          if (ezDir == null) {
            return;
          }

          var props = Properties.Settings.Default;
          props.EztransDir = ezDir;
          props.Save();

          Task retry = Operate(tasker);
          await retry.ConfigureAwait(false);
        }
      }
    }

    // TODO: Separate as VM
    private string AskEztransDir() {
      var ofd = new OpenFileDialog {
        CheckPathExists = true,
        Multiselect = false,
        Title = "Ehnd를 설치한 이지트랜스 폴더의 파일을 아무거나 찾아주세요"
      };
      if (ofd.ShowDialog() == true) {
        return Path.GetDirectoryName(ofd.FileName);
      }
      return null;
    }

    // TODO: Separate as VM
    private string[] AskImportPaths() {
      var ofd = new OpenFileDialog {
        CheckPathExists = false,
        Multiselect = true,
        Title = "Ehnd를 설치한 이지트랜스 폴더의 파일을 아무거나 찾아주세요"
      };
      if (ofd.ShowDialog() == true) {
        return ofd.FileNames;
      }
      return null;
    }

    private RegexDotConfiguration EnsureConfiguration() {
      if (dotConfig != null) {
        return dotConfig;
      }
      string cwd = CwdDesignator.ProjectDirectory;
      dotConfig = new RegexDotConfiguration(cwd, ConfigReloaded, ConfigFaulted);
      return dotConfig;
    }

    private void ConfigReloaded(FileSystemEventArgs fse) {
      if (fse != null) {
        Log($"설정 반영 성공: {fse.Name}\r\n");
      }
    }

    private void ConfigFaulted(FileSystemEventArgs fse, Exception e) {
      string msg = $"설정 반영 실패: {fse.Name}, {e.Message}\r\n";
      Log(e is ApplicationException ? msg as object : e);
    }

    private void Log(object item) {
      LogAdded(item);
    }
  }
}
