using Rengex.Translator;
using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace Rengex {
  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class Program : Application {
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
          var basis = new SelfTranslator(delay);
          new ChildForkTranslator(basis).Serve().Wait();
        }
      }
      catch (Exception e) {
        File.AppendAllText("rengexerr.log", $"{DateTime.Now}, Either: {e}\r\n");
      }
    }

    private static void LaunchWPF() {
      var application = new Program();
      application.InitializeComponent();
      application.DispatcherUnhandledException += OnUnhandledException;
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
      application.Run();
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
      File.AppendAllText("rengexerr.log", $"{DateTime.Now}, Parent: {e}\r\n");
    }
  }
}
