using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace Rengex {
  /// <summary>
  /// Interaction logic for DebugWindow.xaml
  /// </summary>
  public partial class DebugWindow : Window {
    private RegexDotConfiguration RegexDot;
    private int RegionIdx;
    private string RegionPath;
    private Regex LookbehindGroup = ExtendedMatcher.GetExtendedGroupRegex("<[=!]");
    private Regex NamedGroup = ExtendedMatcher.GetExtendedGroupRegex(@"<([^>\s]+?)>");
    private bool PageChanged;

    public DebugWindow(RegexDotConfiguration rdot) {
      InitializeComponent();
      RegexDot = rdot;
      SetRegionWithClamp(0);
      ReflectConfigToTextBox();
      rdot.ConfigReloaded += ConfigChanged;
    }

    private void ConfigChanged(FileSystemEventArgs obj) {
      var existing = RegexDot.RegionPaths
        .Select((p, idx) => new { p, idx })
        .FirstOrDefault(x => x.p == RegionPath);
      if (existing == null) {
        SetRegionWithClamp(RegionIdx);
      }
      ReflectConfigToTextBox();
    }

    private void SetRegionWithClamp(int idx) {
      int length = RegexDot.RegionPaths.Count();
      if (length > 0) {
        RegionIdx = Math.Max(0, Math.Min(length - 1, idx));
        // 파일이 없을 경우가 있음. 그래도 인정해야 함.
        string path = RegexDot.RegionPaths.ElementAt(RegionIdx);
        RegionPath = File.Exists(path) ? path : null;
        Title = $"{RegionIdx}: {RegionPath}";
        return;
      }
      RegionIdx = 0;
      RegionPath = null;
      Title = $"null";
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e) {
      if (e.RightButton == MouseButtonState.Pressed) {
        e.Handled = true;
        SetRegionWithClamp(RegionIdx - Math.Sign(e.Delta));
        ReflectConfigToTextBox();
        PageChanged = true;
      }
      else {
        base.OnPreviewMouseWheel(e);
      }
    }

    protected override void OnPreviewMouseRightButtonUp(MouseButtonEventArgs e) {
      if (PageChanged) {
        e.Handled = true;
        PageChanged = false;
      }
      else {
        base.OnPreviewMouseRightButtonUp(e);
      }
    }

    private void ReflectConfigToTextBox() {
      if (string.IsNullOrWhiteSpace(RegionPath)) {
        TbDebug.Text = "";
        return;
      }
      string txt = File.ReadAllText(RegionPath);
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
      TbDebug.Text = "(?imx)" + txt;
    }

    private void OnRegex101Click(object sender, RoutedEventArgs e) {
      System.Diagnostics.Process.Start("https://regex101.com/r/IQeg4l/1");
    }
  }
}
