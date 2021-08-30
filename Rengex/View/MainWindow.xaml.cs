namespace Rengex.View {
  using System;
  using System.Linq;
  using System.Text;
  using System.Windows;
  using System.Windows.Documents;
  using System.Windows.Input;

  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window {

    private readonly MainWindowVM vm;

    public MainWindow() {
      string build = Properties.Resources.BuildDate;
      string date = $"{build.Substring(2, 2)}{build.Substring(5, 2)}{build.Substring(8, 2)}";
      AppendText($"Rengex v{date} by nanikit\n");

      vm = new MainWindowVM(logAdded: LogAdded);
      DataContext = vm;

      InitializeComponent();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e) {
      if (e.Key == Key.OemTilde) {
        ShowRegexDebugWindow();
        e.Handled = true;
      }
      else {
        base.OnPreviewKeyDown(e);
      }
    }

    private static DebugWindow? DebugWindow =>
      Application.Current.Windows.OfType<DebugWindow>().FirstOrDefault();

    private void OnFlaskClick(object sender, RoutedEventArgs e) {
      ShowRegexDebugWindow();
    }

    private void ShowRegexDebugWindow() {
      Window? window = DebugWindow;
      if (window == null) {
        new DebugWindow(vm.dotConfig).Show();
      }
      else {
        _ = window.Activate();
      }
    }

    private void OnDrop(object sender, DragEventArgs e) {
      if (!e.Data.GetDataPresent(DataFormats.FileDrop)) {
        return;
      }
      string[]? paths = e.Data.GetData(DataFormats.FileDrop) as string[];
      vm.RunDefaultOperation(paths);
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
          _ = buffer.Append(blockText, 0, croppedLen);
        }
        else if (
          context == TextPointerContext.ElementEnd &&
          navigator.Parent is Paragraph
        ) {
          _ = buffer.AppendLine();
        }
        else if (
          navigator.Parent is BlockUIContainer block &&
          block.Child is WorkProgress progress &&
          progress.DataContext is Jp2KrTranslationVM work
        ) {
          foreach (Exception e in work.Exceptions) {
            _ = buffer.AppendLine(e.ToString());
            _ = buffer.AppendLine();
          }
        }
        navigator = navigator.GetNextContextPosition(forward);
      }
      while (offsetToEnd > 0);

      string txt = buffer.ToString();
      Clipboard.SetText(txt);
      ea.Handled = true;
    }

    private void Post(Action action) {
      _ = Dispatcher.BeginInvoke(action);
    }

    private void WithAutoScroll(Action action) {
      Post(() => {
        double bottom = TbLog.VerticalOffset + TbLog.ViewportHeight;
        bool isBottommost = bottom >= TbLog.ExtentHeight - 10;
        action();
        if (isBottommost) {
          TbLog.ScrollToEnd();
        }
      });
    }

    private void LogAdded(object obj) {
      switch (obj) {
        case string s:
          AppendText(s);
          break;
        case Exception e:
          AppendException(e);
          break;
        case Jp2KrTranslationVM tvm:
          AppendProgress(tvm);
          break;
        default:
          throw new Exception("unexpected argument");
      }
    }

    private void AppendText(string res) {
      WithAutoScroll(() => {
        TbLog.AppendText(res);
      });
    }

    private void AppendProgress(Jp2KrTranslationVM tvm) {
      var control = new WorkProgress(tvm);
      var container = new BlockUIContainer(control);
      WithAutoScroll(() => TbLog.Document.Blocks.Add(container));
    }

    private void AppendException(Exception e, string info = null) {
      WithAutoScroll(() => {
        TbLog.AppendText($"오류: {info ?? e.Message}");
        var r = new Run($"\r\n{e}") {
          FontSize = 1
        };
        var lastPara = TbLog.Document.Blocks.LastBlock as Paragraph;
        lastPara.Inlines.Add(new Span(r));
        lastPara.Inlines.Add(new Run("\r\n"));
      });
    }

    private void TbLogOnPreviewDragOver(object sender, DragEventArgs e) {
      e.Handled = true;
    }

    private void AskAndImport(object sender, RoutedEventArgs e) {
      vm.AskAndImport();
    }
  }
}
