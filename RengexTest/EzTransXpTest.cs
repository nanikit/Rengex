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
    readonly EzTransXp trans = new EzTransXp();

    private void TestPreservation(string str) {
      Task<string> t = trans.Translate(str);
      t.Wait();
      Assert.AreEqual(str, t.Result);
    }

    [TestMethod]
    public void SymbolPreservationTest() {
      TestPreservation("-----");
      TestPreservation("#####");
      TestPreservation("―――――");
      TestPreservation("─────");
      TestPreservation("--##――@@--");
    }

    [TestMethod]
    public void WhitespacePreservationTest1() {
      TestPreservation("\r");
    }

    [TestMethod]
    public void WhitespacePreservationTest2() {
      TestPreservation("\n\nd");
    }

    [TestMethod]
    public void WhitespacePreservationTest3() {
      TestPreservation("\r\n");
    }

    [TestMethod]
    public void WhitespacePreservationTest4() {
      TestPreservation("\n\n\n 　\n\n");
    }
  }
}
