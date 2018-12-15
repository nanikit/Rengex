using System;
using System.Text;

namespace Rengex {

  class Substitution {
    public int Offset;
    public int Length;
    public string Value;

    public int Newlines => TextUtils.CountLines(Value);

    public override string ToString() {
      return $"{Offset}-{Length},{Value}";
    }

    public virtual string Tag => "";

    public string MetaString => $"{Offset},{Length}{Tag}{new string('\n', Newlines)}";
  }

  class IntPrefixedSubst : Substitution {
    public override string Tag => $",Int";
  }

  class VLQPrefixedSubst : Substitution {
    public override string Tag => $",VLQ";
  }

  class LEB128PrefixedSubst : Substitution {
    public override string Tag => $",LEB";
  }

  class PrefixedLengthSearcher {
    readonly byte[] Bytes;
    Substitution Span;
    int LeastLength;

    public PrefixedLengthSearcher(byte[] bytes) {
      Bytes = bytes;
    }

    public bool ScanPrefix(Substitution span, out Substitution result) {
      Span = span;
      LeastLength = Math.Max(2, Span.Length - 10);
      result = null;

      // Reference document: MS-NRBF
      int scanEnd = Span.Offset + Math.Min(10, Span.Length);
      for (int idx = Span.Offset; idx < scanEnd; idx++) {
        if (DecodeIntPrefix(idx, out result)) {
          return true;
        }

        if (DecodeVLQPrefix(idx, out result)) {
          return true;
        }
      }
      return false;
    }

    private bool DecodeIntPrefix(int idx, out Substitution result) {
      result = null;
      int lengthLowIdx = idx - 4;
      if (lengthLowIdx < 0) {
        return false;
      }
      int decodedLength = BitConverter.ToInt32(Bytes, lengthLowIdx);
      bool isLengthValid = decodedLength >= LeastLength && decodedLength <= Span.Length;
      if (isLengthValid) {
        bool isSeparable = (Bytes[idx + decodedLength] & 0xC0) != 0x80;
        if (isSeparable) {
          string newValue = decodedLength == Span.Length ? Span.Value
            : Encoding.UTF8.GetString(Bytes, idx, decodedLength);
          result = new IntPrefixedSubst() {
            Offset = lengthLowIdx,
            Length = decodedLength + 4,
            Value = newValue,
          };
          return true;
        }
      }
      return false;
    }

    private bool DecodeVLQPrefix(int idx, out Substitution result) {
      result = null;

      if (idx <= 0 || (Bytes[idx - 1] & 0x80) != 0) {
        return false;
      }
      int decodedLength = 0;
      for (int i = 1; i <= 5; i++) {
        int lengthLowIdx = idx - i;
        if (lengthLowIdx < 0) {
          break;
        }
        int b = Bytes[lengthLowIdx];
        bool metLengthFinalByte = i != 1 && (b & 0x80) == 0;
        if (metLengthFinalByte) {
          break;
        }
        decodedLength = (decodedLength << 7) | (b & 0x7F);
        if (decodedLength > Span.Length) {
          break;
        }
        if (decodedLength < LeastLength) {
          continue;
        }
        string newValue = decodedLength == Span.Length ? Span.Value
          : Encoding.UTF8.GetString(Bytes, lengthLowIdx, decodedLength);
        result = new VLQPrefixedSubst() {
          Offset = lengthLowIdx,
          Length = decodedLength,
          Value = newValue,
        };
        return true;
      }
      return false;
    }
  }
}
