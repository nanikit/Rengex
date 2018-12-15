using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rengex {
  /// <summary>
  /// State
  /// </summary>
  sealed class MetadataCsvReader : IDisposable {
    StreamReader Base;

    public MetadataCsvReader(string path) {
      Base = new StreamReader(path);
    }

    public IEnumerable<TextSpan> GetSpans() {
      var sb = new StringBuilder("\n", 16);
      TextSpan span = MakeSpan();
      if (span == null) {
        yield break;
      }

      while (!Base.EndOfStream) {
        TextSpan next = MakeSpan();
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

    private TextSpan MakeSpan() {
      string[] csv = (Base.ReadLine() ?? "").Split(',');
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
      if (Base != null) {
        Base.Dispose();
        Base = null;
      }
    }
  }
}
