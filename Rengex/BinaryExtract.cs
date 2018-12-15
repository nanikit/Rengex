using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Rengex {
  /// <summary>
  /// 이전 시도의 잔재. 버릴까 말까.
  /// </summary>
  class BinaryExtract {
    /// <summary>
    /// 추출할 바이너리 패턴 정규식
    /// </summary>
    public Regex RxExtractBinary = new Regex("[^\x00-\x08\x0B\x0C\x0E-\x1F\x7F]+", RegexOptions.Compiled);

    private void CarveUtf8Strings(IProjectStorage workspace) {
      string utf8WithSOH = File.ReadAllText(workspace.SourcePath, TextUtils.SOHFallbackUTF8);
      MatchCollection matches = RxExtractBinary.Matches(utf8WithSOH);

      byte[] bytes = File.ReadAllBytes(workspace.SourcePath);
      List<Substitution> metas = ScanUtf8Strings(bytes, matches);

      metas = PostprocessLengthPrefix(bytes, metas);

      using (StreamWriter meta = TextUtils.GetReadSharedWriter(workspace.MetadataPath))
      using (StreamWriter trans = TextUtils.GetReadSharedWriter(workspace.TranslationPath)) {
        foreach (Substitution m in metas) {
          meta.WriteLine(m.MetaString);
          trans.WriteLine(m.Value);
        }
      }
    }

    private static List<Substitution> ScanUtf8Strings(byte[] bytes, MatchCollection matches) {
      var metas = new List<Substitution>();
      int readChars = 0;
      var raw = new MemoryStream(bytes, 0, bytes.Length, true, true);
      foreach (Match match in matches) {
        // TODO: overlapping group should be blocked
        int skipChars = match.Index - readChars;
        TextUtils.SkipUtf8Chars(raw, skipChars);
        int index = (int)raw.Position;
        TextUtils.SkipUtf8Chars(raw, match.Length);
        int length = (int)raw.Position - index;
        metas.Add(new Substitution() {
          Offset = index,
          Length = length,
          Value = match.Value
        });
        readChars = match.Index + match.Length;
      }
      return metas;
    }

    private static List<Substitution> PostprocessLengthPrefix(byte[] bytes, List<Substitution> metas) {
      var pls = new PrefixedLengthSearcher(bytes);
      var sures = new List<Substitution>();
      for (int i = 0; i < metas.Count; i++) {
        Substitution m = metas[i];
        if (pls.ScanPrefix(m, out Substitution refined)) {
          if (refined is IntPrefixedSubst) {
            sures.Add(refined);
          }
          else if (refined.Length >= 7) {
            sures.Add(m);
          }
        }
        else if (m.Length >= 6) {
          sures.Add(m);
        }
      }
      return sures;
    }
  }
}
