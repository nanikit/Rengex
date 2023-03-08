namespace Rengex.Model {
  using Rengex.Helper;
  using Rengex.Translator;
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Text;
  using System.Threading.Tasks;

  public class TranslationUnit {

    public ManagedPath ManagedPath { get; set; }
    public IRegexDotConfiguration DotConfig { get; set; }

    private static readonly UTF8Encoding _utf8WithBom = new(true);
    private static readonly Encoding _cp949 = Encoding.GetEncoding(949);
    private RegexConfiguration? _config;

    public TranslationUnit(IRegexDotConfiguration dot, ManagedPath path) {
      DotConfig = dot;
      ManagedPath = path;
    }

    public async Task ExtractSourceText() {
      if (!ManagedPath.IsInProject) {
        await ManagedPath.CopyToSourceDirectory().ConfigureAwait(false);
      }
      if (!StringWithCodePage.ReadAllTextAutoDetect(ManagedPath.OriginalPath, out var sourceText)) {
        // TODO: UI message
        return;
      }

      string txt = sourceText.Content;
      _config = DotConfig.GetConfiguration(ManagedPath.OriginalPath);
      await WriteIntermediates(_config.Matches(txt)).ConfigureAwait(false);
    }

    public async Task MachineTranslate(ITranslator translator) {
      if (!File.Exists(ManagedPath.SourcePath)) {
        await ExtractSourceText().ConfigureAwait(false);
      }
      if (!File.Exists(ManagedPath.SourcePath)) {
        return;
      }

      string jp = await File.ReadAllTextAsync(ManagedPath.SourcePath).ConfigureAwait(false);
      string kr = await translator.Translate(jp).ConfigureAwait(false);
      string targetPath = Util.PrecreateDirectory(ManagedPath.TargetPath);
      await File.WriteAllTextAsync(targetPath, kr).ConfigureAwait(false);
    }

    public void BuildTranslation() {
      string resultPath = Util.PrecreateDirectory(ManagedPath.ResultPath);
      if (!StringWithCodePage.ReadAllTextAutoDetect(ManagedPath.OriginalPath, out var sourceText)) {
        File.Copy(ManagedPath.OriginalPath, resultPath, true);
        return;
      }

      var targetEncoding = sourceText.Encoding.CodePage == 932 ? _cp949 : _utf8WithBom;
      using var meta = new MetadataCsvReader(ManagedPath.MetadataPath);
      using var trans = File.OpenText(ManagedPath.TargetPath);
      using var source = new StringReader(sourceText.Content);
      using var result = new StreamWriter(File.Create(resultPath), targetEncoding);
      CompileTranslation(meta, trans, source, result);
    }

    private async Task WriteIntermediates(IEnumerable<TextSpan> spans) {
      _config = DotConfig.GetConfiguration(ManagedPath.OriginalPath);
      using var meta = TextUtils.GetReadSharedWriter(ManagedPath.MetadataPath);
      using var trans = TextUtils.GetReadSharedWriter(ManagedPath.SourcePath);
      foreach (var span in spans) {
        string value = span.Value;
        string newLines = new('\n', TextUtils.CountLines(value));
        await meta.WriteLineAsync($"{span.Offset},{span.Length},{span.Title}{newLines}").ConfigureAwait(false);

        string preprocessed = _config.PreReplace(span.Title, value);
        await trans.WriteLineAsync(preprocessed).ConfigureAwait(false);
      }
    }

    private void CompileTranslation(MetadataCsvReader meta, TextReader translation, TextReader source, TextWriter dest) {
      var src = new CharCountingReader(source);
      var substitution = new SpanPairReader(translation);
      _config = DotConfig.GetConfiguration(ManagedPath.OriginalPath);

      foreach (var span in meta.GetSpans()) {
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
    private readonly StringBuilder Buffer = new();
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
