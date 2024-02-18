using Rengex.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Rengex.Model {

  public class RegexConfiguration {
    private readonly MatchConfig Matcher;
    private readonly ReplaceConfig Replacer;

    internal RegexConfiguration(MatchConfig matcher, ReplaceConfig replacer) {
      Matcher = matcher;
      Replacer = replacer;
    }

    public IEnumerable<TextSpan> Matches(string input) {
      return Matcher.Matches(input);
    }

    public string PostReplace(string name, string src, string trans) {
      return Replacer.PostReplace(name, src, trans);
    }

    public string PreReplace(string name, string trans) {
      return Replacer.PreReplace(name, trans);
    }
  }

  /// <summary>
  /// 번역 전, 후 치환 규칙 설정을 담당하는 클래스
  /// </summary>
  public class ReplaceConfig : IDotConfig<ReplaceConfig> {
    public static readonly string Extension = ".repla.txt";

    private readonly string FullPath;

    private readonly List<IReplacer> Replacers;

    private Func<string, ReplaceConfig> ConfigResolver;

    public ReplaceConfig() {
      Replacers = new List<IReplacer>();
    }

    public ReplaceConfig(string replaceConfigPath) {
      FullPath = Path.GetFullPath(replaceConfigPath);
      Replacers = new ReplaceConfigLoader(this).Rules;
    }

    private interface IReplacer {

      string Postprocess(string meta, string trans);

      string Preprocess(string meta, string trans);
    }

    Func<string, ReplaceConfig> IDotConfig<ReplaceConfig>.ConfigResolver {
      set { ConfigResolver = value; }
    }

    public ReplaceConfig CreateFromFile(string path) {
      return new ReplaceConfig(path);
    }

    public string GetDefaultConfig() {
      return Properties.Resources.DefaultReplace;
    }

    public string GetExtension() {
      return Extension;
    }

    /// <summary>
    /// 번역 후처리. 원문첨부 매칭을 위해 원문도 필요.
    /// </summary>
    /// <param name="name">매칭 그룹 이름</param>
    /// <param name="src">번역 전 원문</param>
    /// <param name="trans">번역 후 문자열</param>
    /// <returns>최종 문자열</returns>
    public string PostReplace(string name, string src, string trans) {
      return PostReplaceInternal($"{src}\0{name}", trans);
    }

    /// <summary>
    /// 번역 전처리
    /// </summary>
    /// <param name="name">매칭 그룹 이름</param>
    /// <param name="src">번역 전 원문</param>
    /// <returns>전처리된 문자열</returns>
    public string PreReplace(string name, string src) {
      return PreReplaceInternal(name, src);
    }

    public string PreReplaceInternal(string meta, string src) {
      string ret = src;
      foreach (var rule in Replacers) {
        ret = rule.Preprocess(meta, ret);
      }
      return ret;
    }

    private string PostReplaceInternal(string meta, string trans) {
      string ret = trans;
      foreach (var rule in Replacers) {
        ret = rule.Postprocess(meta, ret);
      }
      return ret;
    }

    internal class PostprocessPattern(ReplacePattern pattern) : IReplacer {

      public string Postprocess(string meta, string trans) {
        if (!pattern.Extended) {
          return pattern.Original.Replace(trans, pattern.Replace);
        }

        string from = $"{trans}\0{meta}";
        string to = pattern.Original.Replace(from, (match) => {
          bool isStartInRange = match.Index <= trans.Length;
          bool isEndInRange = match.Index + match.Length <= from.Length;
          bool isInRange = isStartInRange && isEndInRange;
          return isInRange ? match.Result(pattern.Replace) : match.Value;
        });
        return to[..(to.Length - meta.Length - 1)];
      }

      public string Preprocess(string meta, string trans) {
        return trans;
      }
    }

    internal class PreprocessPattern(ReplacePattern pattern) : IReplacer {

      public string Postprocess(string meta, string trans) {
        return trans;
      }

      public string Preprocess(string meta, string trans) {
        if (!pattern.Extended) {
          return pattern.Original.Replace(trans, pattern.Replace);
        }

        string from = $"{trans}\0{meta}";
        string to = pattern.Original.Replace(from, (match) => {
          bool isStartInRange = match.Index <= trans.Length;
          bool isEndInRange = match.Index + match.Length <= from.Length;
          bool isInRange = isStartInRange && isEndInRange;
          return isInRange ? match.Result(pattern.Replace) : match.Value;
        });
        return to[..(to.Length - meta.Length - 1)];
      }
    }

    internal class ReplacePattern {
      public readonly bool Extended;
      public readonly Regex Original;
      public readonly string Replace;

      public ReplacePattern(string pat, string replace) {
        if (pat.StartsWith("(?:)")) {
          Extended = true;
          pat = pat[4..];
        }

        pat = pat.Replace(@"\jp", TextUtils.ClassJap);
        Original = new Regex(pat, RegexOptions.None, TimeSpan.FromSeconds(10));

        Replace = replace == "$" ? "" : Regex.Unescape(replace);
      }
    }

    private class Import : IReplacer {
      public string FullPath;
      public ReplaceConfig Includer;

      public Import(ReplaceConfig includer, string path) {
        Includer = includer;
        string dir = Path.GetDirectoryName(includer.FullPath)!;
        FullPath = Path.GetFullPath(Path.Combine(dir, path));
      }

      public string Postprocess(string meta, string trans) {
        return GetConfig().PostReplaceInternal(meta, trans);
      }

      public string Preprocess(string meta, string trans) {
        return GetConfig().PreReplaceInternal(meta, trans);
      }

      private ReplaceConfig GetConfig() {
        var cfg = Includer.ConfigResolver(FullPath);
        if (cfg == null) {
          string name = Path.GetFileName(FullPath);
          throw new ApplicationException($"{name}를 찾을 수 없습니다");
        }
        return cfg;
      }
    }

    private class ReplaceConfigLoader {
      public readonly ReplaceConfig ReplaceConfig;
      public readonly List<IReplacer> Rules;

      private IEnumerator<string> Lines;

      public ReplaceConfigLoader(ReplaceConfig config) {
        ReplaceConfig = config;
        Rules = LoadConfig();
      }

      private static bool IsCommentLine(string line) {
        return string.IsNullOrWhiteSpace(line) || line[0] == '#';
      }

      private void ExpectReplaceLine(string patLine) {
        while (Lines.MoveNext()) {
          if (!IsCommentLine(Lines.Current)) {
            return;
          }
        }
        throw new ApplicationException($"다음 패턴을 치환할 문자열이 없습니다: {patLine}");
      }

      private List<IReplacer> LoadConfig() {
        string[] lines = File.ReadAllLines(ReplaceConfig.FullPath);
        Lines = lines.AsEnumerable().GetEnumerator();

        var rules = new List<IReplacer>();
        while (Lines.MoveNext()) {
          if (IsCommentLine(Lines.Current)) {
            continue;
          }
          var import = ReadImportLine();
          if (import != null) {
            rules.Add(import);
          }
          else {
            var rule = ReadPatternLines();
            rules.Add(rule);
          }
        }
        return rules;
      }

      private Import? ReadImportLine() {
        string line = Lines.Current;
        if (line[0] != '*') {
          return null;
        }

        var import = new Import(ReplaceConfig, line[1..]);
        return !File.Exists(import.FullPath)
          ? throw new ApplicationException($"참조 파일이 존재하지 않습니다: {import.FullPath}")
          : import;
      }

      private IReplacer ReadPatternLines() {
        string patLine = Lines.Current;
        ExpectReplaceLine(patLine);

        bool isPrePattern = patLine.StartsWith("(?=)", StringComparison.Ordinal);
        string pat = isPrePattern ? patLine[4..] : patLine;
        var rp = new ReplacePattern(pat, Lines.Current);
        var rule = isPrePattern
          ? new PreprocessPattern(rp) as IReplacer
          : new PostprocessPattern(rp);
        return rule;
      }
    }
  }

  public class TextSpan {
    public string? Value;

    public TextSpan(long offset, long length, string? value, string? name) {
      Offset = offset;
      Length = length;
      Value = value;
      Title = name;
    }

    public long End => Offset + Length;
    public long Length { get; set; }
    public long Offset { get; set; }
    public string? Title { get; private set; }

    public string Extract() {
      return Value;
    }
  }

  internal class ExtendedMatcher {
    public static readonly Regex RxProcGroup = GetExtendedGroupRegex(@"<([^>\s]+?F)>");

    private const RegexOptions RxoDefault = RegexOptions.Compiled
      | RegexOptions.Multiline
      | RegexOptions.IgnorePatternWhitespace;

    private readonly Dictionary<string, Regex> Procedures;
    private readonly Regex Root;
    private readonly TimeSpan Timeout;

    public ExtendedMatcher(string pattern, TimeSpan timeout) {
      Timeout = timeout;
      Procedures = GetProcedures(pattern);
      Root = Procedures[""];
    }

    /// <summary>
    /// Matches to regex pattern &quot;(?prefix...)&quot;
    /// while ignoring whitespace.
    /// </summary>
    public static Regex GetExtendedGroupRegex(string prefix) {
      return new Regex(
        // Non-escaped (?
        @"\((?<=(?:[^\\]|^)(?:\\\\)*.)\?" +
        // target
        prefix +
        // Count open/close paren
        @"((?>" +
          @"[^()[\]\\]+" +
          @"|\\." +
          @"|(?<open>\()" +
          @"|(?<close-open>\))" +
          @"|\[(?>\\.|[^\]])*\]" +
        @")*?)\)" +
        @"(?(open)(?!))"
        , RegexOptions.Compiled);
    }

    public IEnumerable<TextSpan> Matches(string input) {
      return Matches(input, Root);
    }

    private static IEnumerable<(Group, Capture)> AllCapturesOrderByIdx(Match m) {
      return m.Groups
        .OfType<Group>()
        .Skip(1)
        .SelectMany(g => g.Captures.OfType<Capture>().Select(c => (g, c)))
        .Where(gc => !string.IsNullOrEmpty(gc.c.Value))
        .OrderBy(gc => gc.c.Index);
    }

    private Dictionary<string, Regex> GetProcedures(string pat) {
      var dict = new Dictionary<string, Regex>();
      string p = pat;
      for (int idx = 0; RxProcGroup.Match(p, idx, out var m); idx = m.Index + 1) {
        string name = m.Groups["1"].Value;
        string value = m.Groups["2"].Value;
        try {
          dict[name] = new Regex(value, RxoDefault, Timeout);
        }
        catch (Exception e) {
          throw new ApplicationException($"{name} 그룹 파싱 실패", e);
        }
        // Replace named part for preventing capture
        p = p.Remove(m.Index + 2) + ':' + p[(m.Index + 4 + name.Length)..];
      }
      try {
        dict[""] = new Regex(p, RxoDefault, Timeout);
      }
      catch (Exception e) {
        throw new ApplicationException($"전체 정규식 파싱 실패", e);
      }
      return dict;
    }

    private IEnumerable<TextSpan> Match(Match m) {
      var captures = AllCapturesOrderByIdx(m);
      foreach (var (group, capture) in captures) {
        if (group.Name.EndsWith("CC")) {
          string proc = group.Name[0..^2] + 'F';
          var spans = Matches(capture.Value, Procedures[proc]);
          foreach (var span in spans) {
            span.Offset += capture.Index;
            yield return span;
          }
        }
        else if (group.Name.EndsWith('C')) {
          string proc = group.Name[0..^1] + 'F';
          var mch = Procedures[proc].Match(capture.Value);
          var spans = Match(mch);
          foreach (var span in spans) {
            span.Offset += capture.Index;
            yield return span;
          }
        }
        else if (!group.Name.All(char.IsDigit)) {
          yield return new TextSpan(
            capture.Index, capture.Length,
            capture.Value, group.Name);
        }
      }
    }

    private IEnumerable<TextSpan> Matches(string input, Regex rule) {
      foreach (Match m in rule.Matches(input)) {
        foreach (var span in Match(m)) {
          yield return span;
        }
      }
    }
  }

  internal class MatchConfig : IDotConfig<MatchConfig> {
    public static readonly string Extension = ".match.txt";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private readonly ExtendedMatcher Matcher;

    public MatchConfig() {
      Matcher = new ExtendedMatcher(@"(?<text>.*)", Timeout);
    }

    public MatchConfig(string path) {
      string pat = GetPattern(path);
      Matcher = new ExtendedMatcher(pat, Timeout);
    }

    public Func<string, MatchConfig> ConfigResolver { set { } }

    public MatchConfig CreateFromFile(string path) {
      return new MatchConfig(path);
    }

    public string GetDefaultConfig() => Properties.Resources.DefaultMatch;

    public string GetExtension() => Extension;

    public IEnumerable<TextSpan> Matches(string input) {
      return Matcher.Matches(input);
    }

    private static string GetPattern(string path) {
      return File.ReadAllText(path).Replace(@"\jp", TextUtils.ClassJap);
    }
  }
}
