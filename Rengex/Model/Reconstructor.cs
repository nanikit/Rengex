using Rengex.Helper;
using Rengex.Translator;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Rengex.Model {
  internal class Reconstructor(RegexConfiguration configuration) {
    public async Task Extract(Stream original, TextWriter meta, TextWriter source) {
      switch (await StringWithCodePage.ReadAllTextWithDetectionAsync(original).ConfigureAwait(false)) {
      case (string text, _):
        await ExtractFromString(meta, source, text).ConfigureAwait(false);
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

    public async Task Translate(TextReader meta, TextReader source, ITranslator translator, TextWriter target) {
      var preprocessed = new StringBuilder();
      using var metaReader = new MetadataCsvReader(meta);
      var substitution = new SpanPairReader(source);
      foreach (var span in metaReader.GetSpans()) {
        string extracted = substitution.ReadCorrespondingSpan(span);
        string replaced = configuration.PreReplace(span.Name ?? "", extracted);
        preprocessed.AppendLine(replaced);
      }

      string translated = await translator.Translate(preprocessed.ToString()).ConfigureAwait(false);
      await target.WriteAsync(translated).ConfigureAwait(false);
    }

    private async Task<string> ApplyPostProcess(TextSpan span, CharCountingReader src, string translation) {
      string? original = await src.ReadStringAsync((int)span.Length).ConfigureAwait(false);
      return configuration.PostReplace(span.Name ?? "", original ?? "", translation);
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

    private async Task ExtractFromString(TextWriter meta, TextWriter source, string text) {
      var metaWrite = ValueTask.CompletedTask;
      var sourceWrite = ValueTask.CompletedTask;

      async ValueTask ChainMeta(string line) {
        await metaWrite.ConfigureAwait(false);
        await meta.WriteLineAsync(line).ConfigureAwait(false);
      }

      async ValueTask ChainSource(string line) {
        await sourceWrite.ConfigureAwait(false);
        await source.WriteLineAsync(line).ConfigureAwait(false);
      }

      foreach (var span in configuration.Matches(text)) {
        string value = span.Value;
        string newLines = new('\n', TextUtils.CountLines(value));

        metaWrite = ChainMeta($"{span.Offset},{span.Length},{span.Name}{newLines}");
        sourceWrite = ChainSource(value);
      }

      await metaWrite.ConfigureAwait(false);
      await sourceWrite.ConfigureAwait(false);
    }
  }

  /// <summary>
  /// 메타 파일에 대응하는 번역문 부분을 가져오는 클래스.
  /// ReadLine을 쓰면 CR, LF, CR/LF을 구분할 수 없게 되어 수작업함.
  /// </summary>
  internal class SpanPairReader(TextReader reader) {
    private readonly StringBuilder Buffer = new();

    public string ReadCorrespondingSpan(TextSpan span) {
      int spanLineCount = TextUtils.CountLines(span.Value);
      return ReadCorrespondingLines(spanLineCount);
    }

    private void CopyTrailingLF(bool skipBuffer) {
      if (skipBuffer) {
        if (reader.Peek() == '\n') {
          _ = reader.Read();
        }
      }
      else {
        _ = Buffer.Append('\r');
        if (reader.Peek() == '\n') {
          _ = reader.Read();
          _ = Buffer.Append('\n');
        }
      }
    }

    private string ReadCorrespondingLines(int n) {
      _ = Buffer.Clear();

      int lineCount = 0;
      while (lineCount < n) {
        int c = reader.Read();
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
  }
}