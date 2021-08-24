namespace Rengex {
  using Rengex.Translator;
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Text;
  using System.Threading.Tasks;

  internal interface IJpToKrable {
    void ExtractSourceText();
    Task MachineTranslate(ITranslator translator);
    void BuildTranslation();
  }

  public class TranslationUnit : IJpToKrable {

    public IProjectStorage Workspace { get; set; }
    public RegexDotConfiguration DotConfig { get; set; }

    private static readonly UTF8Encoding _utf8WithBom = new UTF8Encoding(true);
    private static readonly Encoding _cp949 = Encoding.GetEncoding(949);
    private RegexConfiguration? _config;

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
      _config = DotConfig.GetConfiguration(Workspace.SourcePath);
      WriteIntermediates(_config.Matches(txt));
    }

    public async Task MachineTranslate(ITranslator translator) {
      if (!File.Exists(Workspace.TranslationPath)) {
        ExtractSourceText();
      }
      if (!File.Exists(Workspace.TranslationPath)) {
        return;
      }

      string jp = await File.ReadAllTextAsync(Workspace.TranslationPath).ConfigureAwait(false);
      string kr = await translator.Translate(jp).ConfigureAwait(false);
      await File.WriteAllTextAsync(GetAlternativeTranslationPath(), kr).ConfigureAwait(false);
    }

    public void BuildTranslation() {
      string translation = GetAlternativeTranslationIfExists();
      string destPath = Util.PrecreateDirectory(Workspace.DestinationPath);
      if (!StringWithCodePage.ReadAllTextAutoDetect(Workspace.SourcePath, out StringWithCodePage sourceText)) {
        File.Copy(Workspace.SourcePath, destPath, true);
        return;
      }

      Encoding destinationEncoding = sourceText.Encoding.CodePage == 932 ? _cp949 : _utf8WithBom;
      using var meta = new MetadataCsvReader(Workspace.MetadataPath);
      using StreamReader trans = File.OpenText(translation);
      using var source = new StringReader(sourceText.Content);
      using var dest = new StreamWriter(File.Create(destPath), destinationEncoding);
      CompileTranslation(meta, trans, source, dest);
    }

    private void WriteIntermediates(IEnumerable<TextSpan> spans) {
      _config = DotConfig.GetConfiguration(Workspace.SourcePath);
      using StreamWriter meta = TextUtils.GetReadSharedWriter(Workspace.MetadataPath);
      using StreamWriter trans = TextUtils.GetReadSharedWriter(Workspace.TranslationPath);
      foreach (TextSpan span in spans) {
        string value = span.Value;
        string newLines = new string('\n', TextUtils.CountLines(value));
        meta.WriteLine($"{span.Offset},{span.Length},{span.Title}{newLines}");

        string preprocessed = _config.PreReplace(span.Title, value);
        trans.WriteLine(preprocessed);
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
      string dir = Path.GetDirectoryName(path)!;
      string filename = Path.GetFileNameWithoutExtension(path);
      return $"{dir}\\{filename}";
    }

    private void CompileTranslation(MetadataCsvReader meta, TextReader translation, TextReader source, TextWriter dest) {
      var src = new CharCountingReader(source);
      var substitution = new SpanPairReader(translation);
      _config = DotConfig.GetConfiguration(Workspace.SourcePath);

      foreach (TextSpan span in meta.GetSpans()) {
        int preserveSize = (int)span.Offset - src.Position;
        if (src.TextCopyTo(dest, preserveSize) != preserveSize) {
          return;
        }

        string translated = substitution.ReadCorrespondingSpan(span);
        translated = ApplyPostProcess(span, src, translated);
        if (translated == null) {
          continue;
        }
        dest.Write(translated);
      }
      _ = src.TextCopyTo(dest, int.MaxValue);
      // TODO: warn mismatch of translation having more line
    }

    private string ApplyPostProcess(TextSpan span, CharCountingReader src, string translation) {
      string original = src.ReadString((int)span.Length);
      return _config!.PostReplace(span.Title ?? "", original, translation);
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
      int spanLineCount = TextUtils.CountLines(span.Value);
      return ReadCorrespondingLines(spanLineCount);
    }

    private string ReadCorrespondingLines(int n) {
      _ = Buffer.Clear();

      int lineCount = 0;
      while (lineCount < n) {
        int c = Reader.Read();
        if (c < 0) {
          throw new ApplicationException("번역파일 줄 수가 부족합니다");
        }
        if (c == '\r') {
          lineCount++;
          bool isEnd = n <= lineCount;
          CopyTrailingLF(isEnd);
          continue;
        }
        if (c == '\n') {
          lineCount++;
        }
        _ = Buffer.Append((char)c);
      }

      return Buffer.ToString();
    }

    private void CopyTrailingLF(bool skipBuffer) {
      if (skipBuffer) {
        if (Reader.Peek() == '\n') {
          _ = Reader.Read();
        }
      }
      else {
        _ = Buffer.Append('\r');
        if (Reader.Peek() == '\n') {
          _ = Reader.Read();
          _ = Buffer.Append('\n');
        }
      }
    }
  }
}
