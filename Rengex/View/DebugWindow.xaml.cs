using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace Rengex {

  class ConfigTabItemVM {
    private static readonly Regex LookbehindGroup = ExtendedMatcher.GetExtendedGroupRegex("<[=!]");
    private static readonly Regex NamedGroup = ExtendedMatcher.GetExtendedGroupRegex(@"<(\p{L}+?)>");

    private static string ConvertToRegex101Regex(string pattern) {
      string txt = pattern;
      txt = Regex.Replace(txt, @"^\s*?(?:#.*?)?\n", "", RegexOptions.Multiline);
      txt = Regex.Replace(txt, "/", @"\/");
      txt = Regex.Replace(txt, @"{0,\d{3,9}}", "*");
      txt = Regex.Replace(txt, @"{1,\d{3,9}}", "+");
      txt = Regex.Replace(txt, @"\\u(....)", @"\x{$1}");
      for (int idx = 0; LookbehindGroup.Match(txt, idx, out Match m); idx = m.Index + 1) {
        if (m.Groups[1].Value.Contains("*")) {
          txt = txt.Remove(m.Index) + txt.Substring(m.Index + m.Length);
        }
      }
      for (int idx = 0, seq = 0; NamedGroup.Match(txt, idx, out Match m); idx = m.Index + 1) {
        string rep = m.Result($"(?<${{1}}{seq++}>$2)");
        txt = txt.Remove(m.Index) + rep + txt.Substring(m.Index + m.Length);
      }
      string jp = Regex.Replace(TextUtils.ClassJap, @"\\u(....)", @"\x{$1}");
      txt = txt.Replace("\\jp", jp);
      txt = txt.Replace("\\w", @"[\pL\pN_]");
      txt = "(?imx)" + txt;
      return txt;
    }

    public string RegionPath { get; private set; }

    public string Title {
      get {
        return Path.GetFileName(RegionPath ?? "");
      }
    }

    public string Content {
      get {
        return File.Exists(RegionPath) ? ConvertToRegex101Regex(File.ReadAllText(RegionPath)) : "";
      }
    }

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

      if (prevIdx != -1) {
        TcConfig.SelectedIndex = Math.Min(prevIdx, vms.Count - 1);
      }
      else {
        TcConfig.SelectedIndex = 0;
      }
    }

    private void OnRegex101Click(object sender, RoutedEventArgs e) {
      Process.Start(new ProcessStartInfo("explorer.exe", "https://regex101.com/r/IQeg4l/1"));
    }
  }
}
