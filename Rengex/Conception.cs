using System.Collections.Generic;
using System.IO;

namespace Rengex {
  /// <summary>
  /// 파일에서 규명된 부분
  /// </summary>
  public interface Extractable {
    string Title { get; }
    long Offset { get; set; }
    long Length { get; set; }
  }

  /// <summary>
  /// 파일에서 규명된 부분
  /// </summary>
  interface Extractable<T> {
    T Extract();
  }

  /// <summary>
  /// 쭉 읽고 규명가능한 것을 추출
  /// </summary>
  interface Extractor {
    double Test(Stream stream);
    Extractable Keep(Stream stream);
  }

  class PngImage : Extractable {
    public string Title { get; set; }

    public long Offset { get; set; }
    public long Length { get; set; }

    public List<Extractable> Childs => null;

    public void Archive() {
      throw new System.NotImplementedException();
    }

    public void Extract() {
      throw new System.NotImplementedException();
    }
  }

  class PngExtractor : Extractor {
    static readonly byte[] signature = new byte[] {
      0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
    };
    int Count = 0;

    public Extractable Keep(Stream stream) {
      long beg = stream.Seek(0, SeekOrigin.Current);
      ReadPng(stream);
      long end = stream.Seek(0, SeekOrigin.Current);
      return new PngImage() {
        Title = $"{Count++}.png",
        Offset = beg,
        Length = end - beg,
      };
    }

    public double Test(Stream stream) {
      return ReadPng(stream);
    }

    private static double ReadPng(Stream stream) {
      if (!stream.HasBytes(signature)) {
        return 0;
      }
      long size = 0;
      while (size <= 10 * 1024 * 1024) {
        int len = stream.ReadI32Be();
        if (len == -1) {
          break;
        }
        if (stream.HasAscii("IEND")) {
          stream.Seek(len + 4, SeekOrigin.Current);
          return 1;
        }
        size += len + 8;
        stream.Seek(len + 8, SeekOrigin.Current);
      }
      return 0;
    }
  }

  static class StreamUtils {
    public static bool HasAscii(this Stream stream, string ascii) {
      long origin = stream.Seek(0, SeekOrigin.Current);
      foreach (char ch in ascii) {
        if (ch != stream.ReadByte()) {
          stream.Seek(origin, SeekOrigin.Begin);
          return false;
        }
      }
      return true;
    }

    public static bool HasBytes(this Stream stream, byte[] bytes) {
      long origin = stream.Seek(0, SeekOrigin.Current);
      foreach (byte b in bytes) {
        if (b != stream.ReadByte()) {
          stream.Seek(origin, SeekOrigin.Begin);
          return false;
        }
      }
      return true;
    }

    public static int ReadI32Be(this Stream stream) {
      int ret = 0;
      for (int i = 0; i < 4; i++) {
        int b = stream.ReadByte();
        if (b == -1) {
          return -1;
        }
        ret = (ret << 8) | b;
      }
      return ret;
    }
  }
}
