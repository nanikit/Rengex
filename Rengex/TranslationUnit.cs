using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Rengex {
  interface JpToKrable {
    void ExtractSourceText();
    Task MachineTranslate(ITranslator translator);
    void BuildTranslation();
  }

  public class TranslationUnit : JpToKrable {

    static UTF8Encoding UTF8WithBom = new UTF8Encoding(true);

    public readonly IProjectStorage Workspace;
    public readonly RegexDotConfiguration DotConfig;
    private RegexConfiguration Config;

    public TranslationUnit(RegexDotConfiguration dot, IProjectStorage workspace) {
      DotConfig = dot;
      Workspace = workspace;
    }

    public void ExtractSourceText() {
      string txt = ReadAllTextOfMajorEncoding(Workspace.SourcePath);
      if (txt != null) {
        Config = DotConfig.GetConfiguration(Workspace.SourcePath);
        WriteIntermediates(Config.Matches(txt));
        return;
      }
      else {
        // TODO: binary files
        var utf8bom = new byte[] { 0xEF, 0xBB, 0xBF };
        File.WriteAllBytes(Workspace.MetadataPath, utf8bom);
        File.WriteAllBytes(Workspace.TranslationPath, utf8bom);
      }
    }

    public async Task MachineTranslate(ITranslator translator) {
      if (!File.Exists(Workspace.TranslationPath)) {
        ExtractSourceText();
      }
      string gluedJp = GetGluedTranslation();
      string gluedKr = await translator.Translate(gluedJp).ConfigureAwait(false);
      string kr = RemoveSpanGlue(gluedKr);
      File.WriteAllText(GetAlternativeTranslationPath(), kr);
    }

    private string GetGluedTranslation() {
      Config = DotConfig.GetConfiguration(Workspace.SourcePath);
      var sb = new StringBuilder();
      using (var meta = new MetadataCsvReader(Workspace.MetadataPath))
      using (StreamReader trans = File.OpenText(Workspace.TranslationPath)) {
        foreach (TextSpan span in meta.GetSpans()) {
          string txt = ReadCorrespondingTranslation(span.Value, trans);
          txt = Config.PreReplace(span.Title, txt);
          if (TextUtils.CountLines(txt) < 1) {
            sb.Append("xq>");
          }
          sb.AppendLine(txt);
        }
      }
      return sb.ToString();
    }

    private static string RemoveSpanGlue(string glued) {
      return glued.Replace("xq>", "");
    }

    public void MachineTranslate() {
      MachineTranslate(new SelfTranslator()).Wait();
    }

    public void BuildTranslation() {
      string translation = GetAlternativeTranslationIfExists();
      string destPath = Util.PrecreateDirectory(Workspace.DestinationPath);
      using (var meta = new MetadataCsvReader(Workspace.MetadataPath))
      using (StreamReader trans = File.OpenText(translation))
      using (StreamReader source = File.OpenText(Workspace.SourcePath))
      using (var dest = new StreamWriter(File.Create(destPath), UTF8WithBom))
        CompileTranslation(meta, trans, source, dest);
    }

    private static string ReadAllTextOfMajorEncoding(string path) {
      string[] encodingNames = new string[] {
        "utf-8",
        "shift_jis",
        "ks_c_5601-1987",
        "utf-16",
        "unicodeFFFE",
      };
      EncoderFallback efall = EncoderFallback.ExceptionFallback;
      DecoderFallback dfall = DecoderFallback.ExceptionFallback;
      foreach (string name in encodingNames) {
        try {
          var enc = Encoding.GetEncoding(name, efall, dfall);
          return File.ReadAllText(path, enc);
        }
        catch (DecoderFallbackException) { }
      }
      return null;
    }

    private void WriteIntermediates(IEnumerable<TextSpan> spans) {
      using (StreamWriter meta = TextUtils.GetReadSharedWriter(Workspace.MetadataPath))
      using (StreamWriter trans = TextUtils.GetReadSharedWriter(Workspace.TranslationPath)) {
        foreach (TextSpan span in spans) {
          string value = span.Value;
          string newLines = new string('\n', TextUtils.CountLines(value));
          meta.WriteLine($"{span.Offset},{span.Length},{span.Title}{newLines}");
          trans.WriteLine(value);
        }
      }
    }

    private string GetAlternativeTranslationIfExists() {
      string anemone = GetAlternativeTranslationPath();
      return File.Exists(anemone) ? anemone : Workspace.TranslationPath;
    }

    private string GetAlternativeTranslationPath() {
      string basename = GetPathWithoutExtension(Workspace.TranslationPath);
      string extension = Path.GetExtension(Workspace.TranslationPath);
      return $"{basename}_번역{extension}";
    }

    private static string GetPathWithoutExtension(string path) {
      string dir = Path.GetDirectoryName(path);
      string filename = Path.GetFileNameWithoutExtension(path);
      return $"{dir}\\{filename}";
    }

    private void CompileTranslation(MetadataCsvReader meta, StreamReader trans, StreamReader source, StreamWriter dest) {
      var src = new CharCountingReader(source);
      Config = DotConfig.GetConfiguration(Workspace.SourcePath);
      foreach (TextSpan span in meta.GetSpans()) {
        int preserveSize = (int)span.Offset - src.Position;
        if (src.TextCopyTo(dest, preserveSize) != preserveSize) {
          return;
        }

        string translated = ReadCorrespondingTranslation(span.Value, trans);
        translated = ApplyPostProcess(span, src, translated);
        if (translated == null) {
          continue;
        }
        dest.Write(translated);
      }
      src.TextCopyTo(dest, int.MaxValue);
    }

    private string ApplyPostProcess(TextSpan span, CharCountingReader src, string translation) {
      if (translation == null) {
        return null;
      }
      string original = src.ReadString((int)span.Length);
      return Config.PostReplace(span.Title ?? "", original, translation);
    }

    private static string ReadCorrespondingTranslation(string value, StreamReader trans) {
      int cnt = TextUtils.CountLines(value);
      IEnumerable<string> lines = Enumerable.Range(0, cnt).Select(_ => trans.ReadLine());
      return string.Join("\r\n", lines);
    }
  }
}
