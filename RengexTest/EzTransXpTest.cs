using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rengex.Tests {
  [TestClass]
  public class EzTransXpTest {
    //EzTransXp Trans = EzTransXp.Instance;

    [TestMethod]
    public void InitializationTest() {
      //string res = Trans.Translate("命令を無視することを指定します");
      //Console.WriteLine(res);
    }

    [TestMethod]
    public void SjisWhitespaceTest() {
      var tester = new EncodingTester(932);
      for (char c = '\0'; c <= 65535; c++) {
        if (!tester.IsEncodable(c) && char.IsWhiteSpace(c)) {
          Console.WriteLine(c);
        }
        if (c == 65535) {
          Console.WriteLine("Success");
          break;
        }
      }
    }
  }
}
