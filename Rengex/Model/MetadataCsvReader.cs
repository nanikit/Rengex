namespace Rengex.Model {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Text;

  /// <summary>
  /// State
  /// </summary>
  sealed class MetadataCsvReader : IDisposable {
    readonly TextReader _base;

    public MetadataCsvReader(TextReader reader) {
      _base = reader;
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
      if (csv.Length < 1 || !int.TryParse(csv[0], out int off)) {
        return null;
      }
      else if (csv.Length < 2 || !int.TryParse(csv[1], out int len)) {
        return new TextSpan(off, 0, null, null);
      }
      else if (csv.Length < 3) {
        return new TextSpan(off, len, null, null);
      }
      else {
        return new TextSpan(off, len, null, csv[2]);
      }
    }

    public void Dispose() {
      _base?.Dispose();
    }
  }
}
