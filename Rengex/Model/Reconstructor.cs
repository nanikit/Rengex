using Rengex.Helper;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Rengex.Model {
  internal class Reconstructor {
    private readonly RegexConfiguration _configuration;

    public Reconstructor(RegexConfiguration configuration) {
      _configuration = configuration;
    }

    public async Task Extract(Stream original, TextWriter meta, TextWriter source) {
      switch (await StringWithCodePage.ReadAllTextWithDetectionAsync(original).ConfigureAwait(false)) {
      case (string text, _):
        foreach (var span in _configuration.Matches(text)) {
          string value = span.Value;
          string newLines = new('\n', TextUtils.CountLines(value));
          await meta.WriteLineAsync($"{span.Offset},{span.Length},{span.Title}{newLines}").ConfigureAwait(false);

          string preprocessed = _configuration.PreReplace(span.Title, value);
          await source.WriteLineAsync(preprocessed).ConfigureAwait(false);
        }
        break;
      case null:
        // TODO: UI message
        break;
      }
    }

    public async Task Merge(Stream original, TextReader meta, TextReader target, Stream result) {
      switch (await StringWithCodePage.ReadAllTextWithDetectionAsync(original).ConfigureAwait(false)) {
      case (string text, Encoding encoding): {
          using var metaReader = new MetadataCsvReader(meta);
          using var resultWriter = new StreamWriter(result, encoding, leaveOpen: true);
          await CompileTranslation(text, metaReader, target, resultWriter).ConfigureAwait(false);
          break;
        }
      default:
        await original.CopyToAsync(result).ConfigureAwait(false);
        return;
      }
    }

    private async Task CompileTranslation(string original, MetadataCsvReader meta, TextReader target, TextWriter result) {
      var originalReader = new CharCountingReader(new StringReader(original));
      var substitution = new SpanPairReader(target);

      foreach (var span in meta.GetSpans()) {
        int preserveSize = (int)span.Offset - originalReader.Position;
        if (await originalReader.TextCopyTo(result, preserveSize).ConfigureAwait(false) != preserveSize) {
          return;
        }

        string translated = substitution.ReadCorrespondingSpan(span);
        translated = await ApplyPostProcess(span, originalReader, translated).ConfigureAwait(false);
        if (translated == null) {
          continue;
        }
        await result.WriteAsync(translated).ConfigureAwait(false);
      }
      await originalReader.TextCopyTo(result, int.MaxValue).ConfigureAwait(false);
      // TODO: warn mismatch of translation having more line
    }

    private async Task<string> ApplyPostProcess(TextSpan span, CharCountingReader src, string translation) {
      string? original = await src.ReadStringAsync((int)span.Length).ConfigureAwait(false);
      return _configuration.PostReplace(span.Title ?? "", original ?? "", translation);
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
