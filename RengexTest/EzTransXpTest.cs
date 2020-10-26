using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rengex.Translator;
using System.Threading.Tasks;

namespace Rengex.Tests {
  [TestClass]
  public class EztransXpTest {
    readonly EztransXp trans;

    public EztransXpTest() {
      trans = EztransXp.Create().GetAwaiter().GetResult();
    }

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
