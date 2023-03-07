using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Rengex {
  public static class TextUtils {
    public const string ClassJap = ""
      + @"\u2E80-\u2EFF" // 한,중,일 부수 보충, ⺀-⻿
      + @"\u3040-\u309F" // 히라가나, ぀-ゟ
      + @"\u30A0-\u30FF" // 가타카나, ゠-ヿ
      + @"\u31F0-\u31FF" // 가타카나 음성 확장, ㇰ-ㇿ
      + @"\u31C0-\u31EF" // CJK Strokes, ㇀-㇯
      + @"\u3200-\u32FF" // Enclosed CJK Letters and Months, ㈀-㋿
      + @"\u3400-\u4DBF\u4E00-\u9FBF\uF900-\uFAFF" // CJK Unified ~, 㐀-䶿一-龿豈-﫿
                                                   //+ @"\uFF64-\uFF9F" // half-width katakana
      + @"\uFF00-\uFF9F" // Full-width alphabet, half-width katakana ,＀-ﾟ
      ;

    public static bool Match(this Regex rx, string input, int index, out Match m) {
      m = rx.Match(input, index);
      return m.Success;
    }

    public static int Count(this string str, char ch) {
      char[] ar = str.ToCharArray();
      int len = ar.Length;
      int cnt = 0;
      for (int i = 0; i < len; i++) {
        if (ar[i] == ch) {
          cnt++;
        }
      }
      return cnt;
    }

    public static int CountLines(string str) {
      char[] ar = str.ToCharArray();
      int len = ar.Length;
      int cnt = 0;
      for (int i = 0; i < len; i++) {
        char c = ar[i];
        if (c == '\n') {
          cnt++;
        }
        else if (c == '\r') {
          cnt++;
          int ni = i + 1;
          if (ni < len && ar[ni] == '\n') {
            i++;
          }
        }
      }
      return cnt;
    }

    public static StreamWriter GetReadSharedWriter(string path) {
      var file = File.Open(Util.PrecreateDirectory(path), FileMode.Create, FileAccess.Write, FileShare.Read);
      file.SetLength(0);
      return new StreamWriter(file, Encoding.UTF8);
    }

    public static int SkipUtf8Chars(MemoryStream raw, int chars) {
      int readChars = 0;
      while (readChars < chars) {
        int headByte = raw.ReadByte();
        if (headByte == -1) {
          break;
        }
        int byteLength = GetUtf8CharByteLength(headByte);

        int readByte = 1;
        while (readByte < byteLength) {
          int tailByte = raw.ReadByte();
          if (tailByte == -1) {
            return readChars + 1;
          }
          if ((tailByte & 0xC0) != 0x80) {
            break;
          }
          if (readByte == 1) {
            bool utf8UpperBoundExceeded = headByte == 0xF4 && tailByte > 0x8F;
            bool isOverlongEncoding4 = headByte == 0xF0 && tailByte < 0x90;
            if (utf8UpperBoundExceeded || isOverlongEncoding4) {
              readByte = byteLength;
              readChars--;
              break;
            }
            bool isOverlongEncoding3 = headByte == 0xE0 && tailByte < 0xA0;
            bool isUtf16Surrogate = headByte == 0xED && tailByte >= 0xA0;
            if (isOverlongEncoding3 || isUtf16Surrogate) {
              readByte = byteLength;
              break;
            }
          }
          readByte++;
        }

        bool charComplete = readByte == byteLength;
        if (!charComplete) {
          raw.Position--;
        }
        readChars += (charComplete && byteLength == 4) ? 2 : 1;
      }
      return readChars;
    }

    private static int GetUtf8CharByteLength(int headByte) {
      // ASCII
      if (headByte < 0x80) {
        return 1;
      }
      else if (headByte < 0xC2) {
        // Less than 0xC0 means continuation. error, so 1.
        // 0xC1 causes overlapping bit with ASCII range, this is called
        // overlong encoding, and it's not permitted.
        return 1;
      }
      else if (headByte < 0xE0) {
        return 2;
      }
      else if (headByte < 0xF0) {
        return 3;
      }
      // UTF-8 upper bound is 0x10FFFF, first byte is 0xF4
      else if (headByte < 0xF5) {
        return 4;
      }
      else {
        // error.
        return 1;
      }
    }
  }

  class CharCountingReader {
    public int Position { get; private set; }

    private readonly TextReader Base;
    private char[] Buffer;

    public CharCountingReader(TextReader reader, char[] buffer = null) {
      Base = reader;
      Buffer = buffer ?? new char[2048];
    }

    public int TextCopyTo(TextWriter destination, int length) {
      int remain = length;
      while (remain > 0) {
        int read = Base.ReadBlock(Buffer, 0, Math.Min(remain, Buffer.Length));
        if (read <= 0) {
          break;
        }
        destination.Write(Buffer, 0, read);
        remain -= read;
      }

      int totalRead = length - remain;
      Position += totalRead;
      return totalRead;
    }

    public string ReadString(int length) {
      if (Buffer.Length < length) {
        Buffer = new char[length];
      }

      int read = Base.ReadBlock(Buffer, 0, length);
      Position += read;

      return read == length ? new string(Buffer, 0, length) : null;
    }
  }

  class StringWithCodePage {
    public static bool ReadAllTextAutoDetect(string path, out StringWithCodePage guessed) {
      string[] encodingNames = new string[] {
        "utf-8",
        "shift_jis",
        "ks_c_5601-1987",
        "utf-16",
        "unicodeFFFE",
      };
      EncoderFallback efall = EncoderFallback.ExceptionFallback;
      DecoderFallback dfall = DecoderFallback.ExceptionFallback;
      guessed = null;
      foreach (string name in encodingNames) {
        try {
          var enc = Encoding.GetEncoding(name, efall, dfall);
          guessed = new StringWithCodePage(File.ReadAllText(path, enc), enc);
          return true;
        }
        catch (DecoderFallbackException) { }
      }
      return false;
    }

    public string Content { get; set; }
    public Encoding Encoding { get; set; }

    public StringWithCodePage(string content, Encoding encoding) {
      Content = content;
      Encoding = encoding;
    }
  }
}
