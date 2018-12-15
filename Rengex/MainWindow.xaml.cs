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
    private ParallelTranslation Translator;
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
        e.Handled = true;
        Window dw = DebugWindow;
        if (dw == null) {
          new DebugWindow(DotConfig).Show();
        }
        else {
          dw.Activate();
        }
      }
      else {
        base.OnPreviewKeyDown(e);
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

    private void CopyTextCommand(object sender, ExecutedRoutedEventArgs e) {
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
          context == TextPointerContext.EmbeddedElement &&
          navigator.Parent is InlineUIContainer container &&
          container.Child is LabelProgress LpProg) {
          buffer.Append(LpProg.TbLabel.Text);
        }
        else if (
          context == TextPointerContext.ElementEnd &&
          navigator.Parent is Paragraph) {
          buffer.AppendLine();
        }
        navigator = navigator.GetNextContextPosition(forward);
      }
      while (offsetToEnd > 0);

      string txt = buffer.ToString();
      Clipboard.SetText(txt);
      e.Handled = true;
    }

    Dictionary<TranslationUnit, LabelProgress> JobProgresses = new Dictionary<TranslationUnit, LabelProgress>();
    private void MakingProgress(TranslationUnit unit, string msg, double pg) => Post(() => {
      if (!JobProgresses.TryGetValue(unit, out LabelProgress pb)) {
        pb = new LabelProgress();
        JobProgresses[unit] = pb;

        WithAutoScroll(() => {
          var container = new InlineUIContainer(pb, TbLog.Document.ContentEnd);
          TbLog.AppendText("\r\n");
        });
      }
      string ellipsisPath = GetEllipsisPath(unit);
      pb.SetProgressAndLabel(pg, $"{ellipsisPath}: {msg}");
      if (pg == 100) {
        JobProgresses.Remove(unit);
      }
    });

    private static string GetEllipsisPath(TranslationUnit unit) {
      string path = unit.Workspace.RelativePath;
      string ellipsisPath = path.Length > 30
        ? $"...{path.Substring(path.Length - 30)}"
        : path;
      return ellipsisPath;
    }

    private ParallelTranslation GetTranslator(string[] paths = null) {
      ParallelTranslation translator = new ParallelTranslation(EnsureConfiguration(), paths);
      translator.OnImport += t => {
        MakingProgress(t, "추출 중..", 25);
      };
      translator.OnTranslation += t => {
        MakingProgress(t, "번역 중..", 50);
      };
      translator.OnExport += t => {
        MakingProgress(t, "병합 중..", 80);
      };
      translator.OnComplete += t => {
        MakingProgress(t, "완료", 100);
      };
      translator.OnError += (t, e) => {
        string desc = e.Message;
        if (e is RegexMatchTimeoutException) {
          desc = "정규식 검색이 너무 오래 걸립니다. 정규식을 점검해주세요.";
        }
        AppendException(e, $"{Path.GetFileName(t.Workspace.RelativePath)}: {desc}");
        LabelProgress pb = JobProgresses[t];
        Post(() => pb.SetError(GetEllipsisPath(t)));
        JobProgresses.Remove(t);
      };
      return translator;
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
      await Operate(Translator.ImportTranslation);
    }

    private void UseAllImportedIfNotSelected() {
      if (Translator == null) {
        Translator = GetTranslator();
      }
    }

    private async void OnTranslateClick(object sender, RoutedEventArgs e) {
      UseAllImportedIfNotSelected();
      await Operate(Translator.MachineTranslation);
    }

    private async void OnExportClick(object sender, RoutedEventArgs e) {
      UseAllImportedIfNotSelected();
      await Operate(Translator.ExportTranslation);
    }

    private async void OnOnestopClick(object sender, RoutedEventArgs e) {
      UseAllImportedIfNotSelected();
      await Operate(Translator.OnestopTranslation);
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
      BtnImport.Background = null;
      BtnExport.Background = null;
      BtnOnestop.Background = null;
      BtnTranslate.Background = null;
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

    private async Task Operate(Action action) {
      if (!Ongoing?.IsCompleted ?? false) {
        LogText("이미 작업 중입니다. 나중에 시도해주세요.\r\n");
        return;
      }

      Ongoing = Task.Run(action);
      try {
        await Ongoing;
        LogText("작업이 끝났습니다.\r\n");
      }
      catch (UnauthorizedAccessException e) {
        AppendException(e, "파일이 사용 중이거나 읽기 전용인지 확인해보세요.");
      }
      catch (EzTransNotFoundException e) {
        AppendException(e);
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
      catch (Exception e) {
        AppendException(e);
      }
    }

    private void TbLogOnPreviewDragOver(object sender, DragEventArgs e) {
      e.Handled = true;
    }
  }
}
