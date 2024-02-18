using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Rengex.Translator {

  public static class SerialUtility {

    public static byte[] GetPrefixedSerial(object o) {
      using var ms = GetPrefixedSerialStream(o);
      byte[] buf = new byte[ms.Length];
      ms.Read(buf, 0, Convert.ToInt32(ms.Length));
      return buf;
    }

    // CancellationToken does nothing for NetworkStream.ReadAsync
    public static async Task<byte[]> ReadLenAsync(this Stream s, int len) {
      if (len < 0) {
        throw new ArgumentException("len cannot be negative");
      }

      byte[] buf = new byte[len];
      await s.ReadExactlyAsync(buf).ConfigureAwait(false);
      return buf;
    }

    public static async Task<T> ReadObjAsync<T>(this Stream s) {
      byte[] lenHeader = await s.ReadLenAsync(sizeof(long));

      int len = (int)BitConverter.ToInt64(lenHeader, 0);
      byte[] buf = await s.ReadLenAsync(len);

      using var ms = new MemoryStream(buf);
      return JsonSerializer.Deserialize<T>(ms)!;
    }

    public static async Task WriteObjAsync(this Stream stream, object obj) {
      await WriteObjAsync(stream, obj, CancellationToken.None);
    }

    public static async Task WriteObjAsync(this Stream stream, object obj, CancellationToken token) {
      using var ms = GetPrefixedSerialStream(obj);
      token.ThrowIfCancellationRequested();
      await ms.CopyToAsync(stream, 8192, token);
    }

    // async is not useful for MemoryStream: http://stackoverflow.com/a/20805616
    private static MemoryStream GetPrefixedSerialStream(object o) {
      var ms = new MemoryStream();
      ms.Write(BitConverter.GetBytes(0L), 0, sizeof(long));
      JsonSerializer.Serialize(ms, o);
      ms.Seek(0, SeekOrigin.Begin);
      ms.Write(BitConverter.GetBytes(ms.Length - sizeof(long)), 0, sizeof(long));
      ms.Seek(0, SeekOrigin.Begin);
      return ms;
    }
  }
}
