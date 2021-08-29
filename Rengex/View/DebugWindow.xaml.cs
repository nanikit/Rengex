namespace Rengex {
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.IO;
  using System.Linq;
  using System.Text.RegularExpressions;
  using System.Windows;

  internal class ConfigTabItemVM {
    private static readonly Regex LookbehindGroup = ExtendedMatcher.GetExtendedGroupRegex(@"\(<[=!]");

    private static string ConvertToRegex101Regex(string pattern) {
      string txt = pattern;
      txt = Regex.Replace(txt, @"^\s*?(?:#.*?)?\n", "", RegexOptions.Multiline);
      txt = Regex.Replace(txt, "/", @"\/");
      txt = Regex.Replace(txt, @"{0,\d{3,9}}", "*");
      txt = Regex.Replace(txt, @"{1,\d{3,9}}", "+");
      txt = Regex.Replace(txt, @"\\u(....)", @"\x{$1}");

      for (int idx = 0; LookbehindGroup.Match(txt, idx, out Match m); idx = m.Index + 1) {
        if (m.Groups[1].Value.Contains("*")) {
          txt = txt.Remove(m.Index) + txt[(m.Index + m.Length)..];
        }
      }

      string jp = Regex.Replace(TextUtils.ClassJap, @"\\u(....)", @"\x{$1}");
      txt = txt.Replace("\\jp", jp);
      txt = txt.Replace("\\w", @"[\pL\pN_]");
      txt = "(?imx)" + txt;
      return txt;
    }

    public string RegionPath { get; private set; }

    public string Title => Path.GetFileName(RegionPath ?? "");

    public string Content => File.Exists(RegionPath) ? ConvertToRegex101Regex(File.ReadAllText(RegionPath)) : "";

    public ConfigTabItemVM(string path) {
      RegionPath = path;
    }
  }

  /// <summary>
  /// Interaction logic for DebugWindow.xaml
  /// </summary>
  public partial class DebugWindow : Window {
    private readonly RegexDotConfiguration RegexDot;

    public DebugWindow(RegexDotConfiguration rdot) {
      InitializeComponent();
      RegexDot = rdot;
      RegexDot.ConfigReloaded += ConfigChanged;
      ReflectConfigChanges();
    }

    private void ConfigChanged(FileSystemEventArgs obj) {
      ReflectConfigChanges();
    }

    private void ReflectConfigChanges() {
      int prevIdx = TcConfig.SelectedIndex;

      List<ConfigTabItemVM> vms = RegexDot.RegionPaths.Select(x => new ConfigTabItemVM(x)).ToList();
      TcConfig.DataContext = vms;

      TcConfig.SelectedIndex = prevIdx != -1 ? Math.Min(prevIdx, vms.Count - 1) : 0;
    }

    private void OnRegex101Click(object sender, RoutedEventArgs e) {
      _ = Process.Start(new ProcessStartInfo("explorer.exe", "https://regex101.com/r/dqgl16/1"));
    }
  }
}
