using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Rengex {
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window {

    private Task Ongoing;
    private RoutedEventHandler DropAction;
    private Jp2KrTranslationVM Translator;
    private RegexDotConfiguration DotConfig;

    public MainWindow() {
      InitializeComponent();
      OnRightClick(BtnOnestop, null);
      string build = Properties.Resources.BuildDate;
      string date = $"{build.Substring(2, 2)}{build.Substring(5, 2)}{build.Substring(8, 2)}";
      LogText($"Rengex v{date} by nanikit\n");
      try {
        EnsureConfiguration();
      }
      catch (Exception e) {
        AppendException(e);
      }
    }

    private RegexDotConfiguration EnsureConfiguration() {
      if (DotConfig != null) {
        return DotConfig;
      }
      string cwd = CwdDesignator.ProjectDirectory;
      DotConfig = new RegexDotConfiguration(cwd, ConfigReloaded, ConfigFaulted);
      return DotConfig;
    }

    private DebugWindow DebugWindow =>
      App.Current.Windows.OfType<DebugWindow>().FirstOrDefault();

    protected override void OnPreviewKeyDown(KeyEventArgs e) {
      if (e.Key == Key.OemTilde) {
        ShowRegexDebugWindow();
        e.Handled = true;
      }
      else {
        base.OnPreviewKeyDown(e);
      }
    }

    private void ShowRegexDebugWindow() {
      Window dw = DebugWindow;
      if (dw == null) {
        new DebugWindow(DotConfig).Show();
      }
      else {
        dw.Activate();
      }
    }

    private void ConfigReloaded(FileSystemEventArgs fse) {
      if (fse != null) {
        LogText($"설정 반영 성공: {fse.Name}\r\n");
      }
    }

    private void ConfigFaulted(FileSystemEventArgs fse, Exception e) {
      string msg = $"설정 반영 실패: {fse.Name}, {e.Message}\r\n";
      if (e is ApplicationException) {
        LogText(msg);
      }
      else {
        AppendException(e, msg);
      }
    }

    private void OnDrop(object sender, DragEventArgs e) {
      if (!e.Data.GetDataPresent(DataFormats.FileDrop)) {
        return;
      }
      var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
      Translator = GetTranslator(paths);
      DropAction?.Invoke(null, e);
    }

    private void CopyTextCommand(object sender, ExecutedRoutedEventArgs ea) {
      const LogicalDirection forward = LogicalDirection.Forward;
      TextSelection selection = TbLog.Selection;
      TextPointer navigator = selection.Start.GetPositionAtOffset(0, forward);
      TextPointer end = selection.End;
      var buffer = new StringBuilder();

      int offsetToEnd;
      do {
        offsetToEnd = navigator.GetOffsetToPosition(end);
        TextPointerContext context = navigator.GetPointerContext(forward);
        if (context == TextPointerContext.Text) {
          string blockText = navigator.GetTextInRun(forward);
          int croppedLen = Math.Min(offsetToEnd, navigator.GetTextRunLength(forward));
          buffer.Append(blockText, 0, croppedLen);
        }
        else if (
          context == TextPointerContext.ElementEnd &&
          navigator.Parent is Paragraph
        ) {
          buffer.AppendLine();
        }
        else if (
          navigator.Parent is BlockUIContainer block &&
          block.Child is WorkProgress progress &&
          progress.DataContext is Jp2KrTranslationVM work
        ) {
          foreach (Exception e in work.Exceptions) {
            buffer.AppendLine(e.ToString());
            buffer.AppendLine();
          }
        }
        navigator = navigator.GetNextContextPosition(forward);
      }
      while (offsetToEnd > 0);

      string txt = buffer.ToString();
      Clipboard.SetText(txt);
      ea.Handled = true;
    }

    /// <summary>
    /// Get a new translator if new paths are designated. Otherwise
    /// use a clone of previous translator for efficiency if possible.
    /// </summary>
    private Jp2KrTranslationVM GetTranslator(string[] paths = null) {
      if (paths == null && Translator != null) {
        Translator = Translator.Clone();
        return Translator;
      }
      else {
        return new Jp2KrTranslationVM(EnsureConfiguration(), paths);
      }
    }

    private async void OnImportClick(object sender, RoutedEventArgs e) {
      // If not jumped from OnDrop
      if (sender != null) {
        var ofd = new OpenFileDialog();
        ofd.Multiselect = true;
        ofd.CheckPathExists = true;
        if (ofd.ShowDialog() != true) {
          return;
        }
        Translator = GetTranslator(ofd.FileNames);
      }
      if (Translator == null) {
        return;
      }
      await Operate(() => Task.Run(Translator.ImportTranslation));
    }

    private async void OnTranslateClick(object sender, RoutedEventArgs e) {
      await Operate(GetTranslator().MachineTranslation);
    }

    private async void OnExportClick(object sender, RoutedEventArgs e) {
      await Operate(() => Task.Run(GetTranslator().ExportTranslation));
    }

    private async void OnOnestopClick(object sender, RoutedEventArgs e) {
      await Operate(GetTranslator().OnestopTranslation);
    }

    private void OnRightClick(object sender, MouseButtonEventArgs e) {
      if (!(sender is Button button)) {
        return;
      }
      RoutedEventHandler nul = null;
      if (button == BtnExport) {
        DropAction = DropAction == OnExportClick ? nul : OnExportClick;
      }
      else if (button == BtnImport) {
        DropAction = DropAction == OnImportClick ? nul : OnImportClick;
      }
      else if (button == BtnOnestop) {
        DropAction = DropAction == OnOnestopClick ? nul : OnOnestopClick;
      }
      else if (button == BtnTranslate) {
        DropAction = DropAction == OnTranslateClick ? nul : OnTranslateClick;
      }
      BtnImport.ClearValue(Control.BackgroundProperty);
      BtnExport.ClearValue(Control.BackgroundProperty);
      BtnOnestop.ClearValue(Control.BackgroundProperty);
      BtnTranslate.ClearValue(Control.BackgroundProperty);
      if (DropAction != null) {
        button.Background = new SolidColorBrush(Colors.Azure);
      }
    }

    private void Post(Action action) => Dispatcher.BeginInvoke(action);

    private void WithAutoScroll(Action action) => Post(() => {
      double bottom = TbLog.VerticalOffset + TbLog.ViewportHeight;
      bool isBottommost = bottom >= TbLog.ExtentHeight - 10;
      action();
      if (isBottommost) {
        TbLog.ScrollToEnd();
      }
    });

    private void LogText(string res) => WithAutoScroll(() => {
      TbLog.AppendText(res);
    });

    private void AppendException(Exception e, string info = null) {
      WithAutoScroll(() => {
        TbLog.AppendText($"오류: {info ?? e.Message}");
        var r = new Run($"\r\n{e}");
        r.FontSize = 1;
        var lastPara = TbLog.Document.Blocks.LastBlock as Paragraph;
        lastPara.Inlines.Add(new Span(r));
        lastPara.Inlines.Add(new Run("\r\n"));
      });
    }

    private async Task Operate(Func<Task> task) {
      if (!Ongoing?.IsCompleted ?? false) {
        LogText("이미 작업 중입니다. 나중에 시도해주세요.\r\n");
        return;
      }

      var control = new WorkProgress(Translator);
      var container = new BlockUIContainer(control);
      TbLog.Document.Blocks.Add(container);

      Ongoing = task();
      try {
        await Ongoing;
        LogText("작업이 끝났습니다.\r\n");
      }
      catch (UnauthorizedAccessException) {
        LogText("파일이 사용 중이거나 읽기 전용인지 확인해보세요.");
      }
      catch (EzTransNotFoundException) {
        var ofd = new OpenFileDialog();
        ofd.CheckPathExists = true;
        ofd.Multiselect = false;
        ofd.Title = "Ehnd를 설치한 이지트랜스 폴더의 파일을 아무거나 찾아주세요";
        if (ofd.ShowDialog() != true) {
          return;
        }
        Properties.Settings.Default.EzTransDir = Path.GetDirectoryName(ofd.FileName);
        Properties.Settings.Default.Save();
      }
      catch (Exception) {
        LogText($"오류가 발생했습니다. 진행 표시줄을 복사하면 오류 내용이 복사됩니다.\r\n");
      }
    }

    private void TbLogOnPreviewDragOver(object sender, DragEventArgs e) {
      e.Handled = true;
    }
  }
}
