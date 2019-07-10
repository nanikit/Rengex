using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Rengex {
  interface IJpToKrable {
    void ExtractSourceText();
    Task MachineTranslate(IJp2KrTranslator translator);
    void BuildTranslation();
  }

  public class TranslationUnit : IJpToKrable {

    static readonly UTF8Encoding UTF8WithBom = new UTF8Encoding(true);
    static readonly Encoding CP949 = Encoding.GetEncoding(949);

    public readonly IProjectStorage Workspace;
    public readonly RegexDotConfiguration DotConfig;
    private RegexConfiguration Config;

    public TranslationUnit(RegexDotConfiguration dot, IProjectStorage workspace) {
      DotConfig = dot;
      Workspace = workspace;
    }

    public void ExtractSourceText() {
      if (!StringWithCodePage.ReadAllTextAutoDetect(Workspace.SourcePath, out StringWithCodePage sourceText)) {
        // TODO: UI message
        return;
      }
      string txt = sourceText.Content;
      Config = DotConfig.GetConfiguration(Workspace.SourcePath);
      WriteIntermediates(Config.Matches(txt));
    }

    public async Task MachineTranslate(IJp2KrTranslator translator) {
      if (!File.Exists(Workspace.TranslationPath)) {
        ExtractSourceText();
      }
      if (!File.Exists(Workspace.TranslationPath)) {
        return;
      }
      string jp = File.ReadAllText(Workspace.TranslationPath);
      string kr = await translator.Translate(jp).ConfigureAwait(false);
      File.WriteAllText(GetAlternativeTranslationPath(), kr);
    }

    public void MachineTranslate() {
      MachineTranslate(new SelfTranslator()).Wait();
    }

    public void BuildTranslation() {
      string translation = GetAlternativeTranslationIfExists();
      string destPath = Util.PrecreateDirectory(Workspace.DestinationPath);
      if (!StringWithCodePage.ReadAllTextAutoDetect(Workspace.SourcePath, out StringWithCodePage sourceText)) {
        File.Copy(Workspace.SourcePath, destPath, true);
        return;
      }
      Encoding destEnc = sourceText.Encoding.CodePage == 932 ? CP949 : UTF8WithBom;
      using (var meta = new MetadataCsvReader(Workspace.MetadataPath))
      using (StreamReader trans = File.OpenText(translation))
      using (var source = new StringReader(sourceText.Content))
      using (var dest = new StreamWriter(File.Create(destPath), destEnc))
        CompileTranslation(meta, trans, source, dest);
    }

    private void WriteIntermediates(IEnumerable<TextSpan> spans) {
      Config = DotConfig.GetConfiguration(Workspace.SourcePath);
      using (StreamWriter meta = TextUtils.GetReadSharedWriter(Workspace.MetadataPath))
      using (StreamWriter trans = TextUtils.GetReadSharedWriter(Workspace.TranslationPath)) {
        foreach (TextSpan span in spans) {
          string value = span.Value;
          string newLines = new string('\n', TextUtils.CountLines(value));
          meta.WriteLine($"{span.Offset},{span.Length},{span.Title}{newLines}");

          string preprocessed = Config.PreReplace(span.Title, value);
          trans.WriteLine(preprocessed);
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

    private void CompileTranslation(MetadataCsvReader meta, TextReader trans, TextReader source, TextWriter dest) {
      var src = new CharCountingReader(source);
      var sub = new SpanPairReader(trans);
      Config = DotConfig.GetConfiguration(Workspace.SourcePath);

      foreach (TextSpan span in meta.GetSpans()) {
        int preserveSize = (int)span.Offset - src.Position;
        if (src.TextCopyTo(dest, preserveSize) != preserveSize) {
          return;
        }

        string translated = sub.ReadCorrespondingSpan(span);
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

  }

  /// <summary>
  /// 메타 파일에 대응하는 번역문 부분을 가져오는 클래스.
  /// ReadLine을 쓰면 CR, LF, CR/LF을 구분할 수 없게 되어 수작업함.
  /// </summary>
  internal class SpanPairReader {
    private readonly StringBuilder Buffer = new StringBuilder();
    private readonly TextReader Reader;

    public SpanPairReader(TextReader reader) {
      Reader = reader;
    }

    public string ReadCorrespondingSpan(TextSpan span) {
      int spanLineCnt = TextUtils.CountLines(span.Value);
      return ReadCorrespondingLines(spanLineCnt);
    }

    private string ReadCorrespondingLines(int n) {
      Buffer.Clear();

      int lineReadCnt = 0;
      while (lineReadCnt < n) {
        int c = Reader.Read();
        if (c < 0) {
          throw new EndOfStreamException();
        }
        if (c == '\r') {
          lineReadCnt++;
          bool isEnd = lineReadCnt >= n;
          CopyTrailingLF(isEnd);
          continue;
        }
        if (c == '\n') {
          lineReadCnt++;
        }
        Buffer.Append((char)c);
      }

      return Buffer.ToString();
    }

    private void CopyTrailingLF(bool skipBuffer) {
      if (skipBuffer) {
        if (Reader.Peek() == '\n') {
          Reader.Read();
        }
      }
      else {
        Buffer.Append('\r');
        if (Reader.Peek() == '\n') {
          Reader.Read();
          Buffer.Append('\n');
        }
      }
    }
  }
}
