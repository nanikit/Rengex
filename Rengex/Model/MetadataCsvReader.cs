namespace Rengex.Model {

  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Text;

  /// <summary>
  /// State
  /// </summary>
  internal sealed class MetadataCsvReader : IDisposable {
    private readonly TextReader _base;

    public MetadataCsvReader(TextReader reader) {
      _base = reader;
    }

    public void Dispose() {
      _base?.Dispose();
    }

    public IEnumerable<TextSpan> GetSpans() {
      var sb = new StringBuilder("\n", 16);
      var span = MakeSpan();
      if (span == null) {
        yield break;
      }

      while (_base.Peek() != -1) {
        var next = MakeSpan();
        if (next == null) {
          sb.Append('\n');
          continue;
        }
        span.Value = sb.ToString();
        sb.Clear();
        sb.Append('\n');
        yield return span;

        span = next;
      }

      span.Value = sb.ToString();
      yield return span;
    }

    private TextSpan? MakeSpan() {
      string[] csv = (_base.ReadLine() ?? "").Split(',');
      if (csv.Length < 1 || !int.TryParse(csv[0], out int offset)) {
        return null;
      }
      else if (csv.Length < 2 || !int.TryParse(csv[1], out int length)) {
        return new TextSpan(offset, 0, null, null);
      }
      else if (csv.Length < 3) {
        return new TextSpan(offset, length, null, null);
      }
      else {
        return new TextSpan(offset, length, csv[2], null);
      }
    }
  }
}
