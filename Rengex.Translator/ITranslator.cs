#nullable enable

using System;
using System.Threading.Tasks;

namespace Rengex.Translator {

  /// <summary>
  /// It can translate japanese to korean.
  /// </summary>
  public interface ITranslator : IDisposable {

    /// <summary>
    /// Translate japanese string to korean.
    /// </summary>
    /// <param name="source">Japanese string</param>
    /// <returns>Korean string</returns>
    Task<string> Translate(string source);
  }
}
