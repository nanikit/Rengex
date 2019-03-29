using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;

namespace Rengex {

  public static class SerialUtility {
    public static BinaryFormatter Formatter = new BinaryFormatter();

    public static async Task WriteObjAsync(this Stream s, object o) {
      await WriteObjAsync(s, o, CancellationToken.None);
    }

    public static async Task WriteObjAsync(this Stream s, object o, CancellationToken token) {
      MemoryStream ms = GetPrefixedSerialStream(o);
      token.ThrowIfCancellationRequested();
      await ms.CopyToAsync(s, 8192, token);
    }

    // async is not useful for MemoryStream: http://stackoverflow.com/a/20805616
    static MemoryStream GetPrefixedSerialStream(object o) {
      var ms = new MemoryStream();
      ms.Write(BitConverter.GetBytes(0L), 0, sizeof(long));
      Formatter.Serialize(ms, o);
      ms.Seek(0, SeekOrigin.Begin);
      ms.Write(BitConverter.GetBytes(ms.Length - sizeof(long)), 0, sizeof(long));
      ms.Seek(0, SeekOrigin.Begin);
      return ms;
    }

    public static byte[] GetPrefixedSerial(object o) {
      MemoryStream ms = GetPrefixedSerialStream(o);
      byte[] buf = new byte[ms.Length];
      ms.Read(buf, 0, Convert.ToInt32(ms.Length));
      return buf;
    }

    public static async Task<T> ReadObjAsync<T>(this Stream s, Action<double> progress = null) {
      byte[] lenHeader = await s.ReadLenAsync(sizeof(long));

      int len = (int)BitConverter.ToInt64(lenHeader, 0);
      byte[] buf = await s.ReadLenAsync(len, progress);

      return (T)Formatter.Deserialize(new MemoryStream(buf));
    }

    // CancellationToken does nothing for NetworkStream.ReadAsync
    public static async Task<byte[]> ReadLenAsync(this Stream s, int len, Action<double> progress = null) {
      if (len < 0) throw new ArgumentException("len cannnot be negative");
      byte[] buf = new byte[len];
      int read = 0, justRead;

      while (read < len && (justRead = await s.ReadAsync(buf, read, len - read)) > 0) {
        read += justRead;
        progress?.Invoke((double)read / len);
      }
      if (read != len) {
        throw new EndOfStreamException();
      }

      return buf;
    }
  }
}
