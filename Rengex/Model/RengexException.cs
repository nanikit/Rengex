namespace Rengex {
  using System;

  public class RengexException : Exception {
    public RengexException(string message, Exception? innerException = null) : base(message, innerException) { }
  }
}
