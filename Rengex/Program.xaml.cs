namespace Rengex {
  using Rengex.Translator;
  using System;
  using System.IO;
  using System.Runtime.ExceptionServices;
  using System.Text;
  using System.Windows;
  using System.Windows.Threading;

  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class Program : Application {
    [STAThread]
    [HandleProcessCorruptedStateExceptions]
    public static void Main(string[] args) {
      try {
        if (args.Length == 2 && Guid.TryParse(args[1], out Guid guid)) {
          var eztransDirectory = Rengex.Properties.Settings.Default.EzTransDir;
          var basis = new EhndTranslator(eztransDirectory);
          new ChildForkTranslator(basis, $"{guid}").Serve().Wait();
        }
        else {
          LaunchWPF();
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
