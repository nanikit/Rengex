using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;

namespace Rengex {
  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class App : Application {
    [STAThread]
    [HandleProcessCorruptedStateExceptions]
    public static void Main(string[] args) {
      try {
        if (args.Length == 0) {
          LaunchWPF();
        }
        else {
          int delay;
          if (!int.TryParse(args[0], out delay)) {
            delay = 200;
          }
          new ChildForkTranslator(delay).Serve().Wait();
        }
      }
      catch (Exception e) {
        File.AppendAllText("rengexerr.log", $"{DateTime.Now}, Either: {e}\r\n");
      }
    }

    private static void LaunchWPF() {
      var application = new App();
      application.InitializeComponent();
      application.ShutdownMode = ShutdownMode.OnMainWindowClose;
      application.DispatcherUnhandledException += OnUnhandledException;
      application.Run();
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
      File.AppendAllText("rengexerr.log", $"{DateTime.Now}, Parent: {e}\r\n");
    }
  }
}
