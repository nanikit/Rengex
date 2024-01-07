using Microsoft.Win32;
using Nanikit.Ehnd;
using Rengex.Helper;
using Rengex.Model;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rengex.View {

  public enum Operation {
    None, Import, Translate, Export, OneStop
  }

  public class AzureIfEqualConverter : MarkupExtension, IValueConverter {
    private static readonly Brush azureBrush = new SolidColorBrush(Colors.Azure);
    private static readonly Brush transparentBrush = new SolidColorBrush(Colors.Transparent);

    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
      return value.Equals(parameter) ? azureBrush : transparentBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
      throw new NotImplementedException();
    }

    public override object ProvideValue(IServiceProvider serviceProvider) {
      return this;
    }
  }

  internal class MainWindowVM : ViewModelBase {

    // TODO: hide this
    public RegexDotConfiguration dotConfig;

    private Operation defaultButton;

    private string[]? paths;

    private Jp2KrTranslationVM? translator;

    public MainWindowVM(Action<object>? logAdded = null) {
      LogAdded += logAdded;
      DefaultOperation = Operation.OneStop;
      OperateCommand = new RelayCommand<Operation>(RunOperation);
      PinCommand = new RelayCommand<Operation>(PinAction);
      dotConfig = EnsureConfiguration();
    }

    public event Action<object> LogAdded = delegate { };

    public Operation DefaultOperation {
      get => defaultButton;
      set => Set(ref defaultButton, value);
    }

    public ICommand OperateCommand { get; private set; }
    public ICommand PinCommand { get; private set; }

    public void AskAndImport() {
      paths = AskImportPaths();
      if (paths != null) {
        Import();
      }
    }

    public void RunDefaultOperation(string[]? paths) {
      this.paths = paths;
      RunOperation(DefaultOperation);
    }

    // TODO: Separate as VM
    private static string? AskEztransDir() {
      var ofd = new OpenFileDialog {
        CheckPathExists = true,
        Multiselect = false,
        Title = "Ehnd를 설치한 이지트랜스 폴더의 파일을 아무거나 찾아주세요"
      };
      return ofd.ShowDialog() == true ? Path.GetDirectoryName(ofd.FileName) : null;
    }

    // TODO: Separate as VM
    private static string[]? AskImportPaths() {
      var ofd = new OpenFileDialog {
        CheckPathExists = false,
        Multiselect = true,
        Title = "가져올 파일을 선택해주세요"
      };
      return ofd.ShowDialog() == true ? ofd.FileNames : null;
    }

    private void ConfigFaulted(FileSystemEventArgs fse, Exception e) {
      string msg = $"설정 반영 실패: {fse.Name}, {e.Message}\r\n";
      Log(e is ApplicationException ? msg as object : e);
    }

    private void ConfigReloaded(FileSystemEventArgs fse) {
      if (fse != null) {
        Log($"설정 반영 성공: {fse.Name}\r\n");
      }
    }

    private RegexDotConfiguration EnsureConfiguration() {
      if (dotConfig != null) {
        return dotConfig;
      }

      string cwd = ManagedPath.ProjectDirectory;
      _ = Directory.CreateDirectory(cwd);
      dotConfig = new RegexDotConfiguration(cwd, ConfigReloaded, ConfigFaulted);
      return dotConfig;
    }

    private void Export() {
      _ = Operate(tvm => tvm.ExportTranslation());
    }

    private void Import() {
      _ = Operate(tvm => tvm.ImportTranslation());
    }

    private bool IsIdle() {
      return translator == null;
    }

    private void Log(object item) {
      LogAdded(item);
    }

    private async Task LoopIfEztransNotFound(Func<Jp2KrTranslationVM, Task> tasker) {
      while (true) {
        var ongoing = tasker(translator);
        try {
          await ongoing.ConfigureAwait(false);
          Log("작업이 끝났습니다.\r\n");
          return;
        }
        catch (EhndNotFoundException) {
          string? ezDir = AskEztransDir();
          if (ezDir == null) {
            return;
          }

          var settings = Properties.Settings.Default;
          settings.EzTransDir = ezDir;
          settings.Save();

          var retry = Operate(tasker);
          await retry.ConfigureAwait(false);
        }
      }
    }

    private void OneStop() {
      _ = Operate(tvm => tvm.OneStopTranslation());
    }

    private async Task Operate(Func<Jp2KrTranslationVM, Task> tasker) {
      if (!IsIdle()) {
        Log("이미 작업 중입니다. 나중에 시도해주세요.\r\n");
        return;
      }

      try {
        translator = new Jp2KrTranslationVM(dotConfig, paths);
        Log(translator);

        await LoopIfEztransNotFound(tasker).ConfigureAwait(false);
      }
      catch (UnauthorizedAccessException) {
        Log("파일이 사용 중이거나 읽기 전용인지 확인해보세요.");
      }
      catch (Exception e) {
        if (translator?.Exceptions.Count == 0) {
          translator.Exceptions.Add(e);
        }
        Log($"오류가 발생했습니다. 진행 표시줄을 복사하면 오류 내용이 복사됩니다.\r\n");
      }
      finally {
        translator = null;
      }
    }

    private void PinAction(Operation kind) {
      DefaultOperation = DefaultOperation == kind ? Operation.None : kind;
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

      case Operation.OneStop:
        OneStop();
        break;

      case Operation.None:
        break;
      }
    }

    private void Translate() {
      _ = Operate(tvm => tvm.MachineTranslation());
    }
  }
}
