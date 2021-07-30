using Rengex.Translator;
using System.Threading.Tasks;
using Xunit;

namespace Rengex.Tests {
  public class EztransXpTest {
    readonly EztransXp trans;

    public EztransXpTest() {
      trans = EztransXp.Create().GetAwaiter().GetResult();
    }

    private void TestPreservation(string str) {
      Task<string> t = trans.Translate(str);
      t.Wait();
      Assert.Equal(str, t.Result);
    }

    [Fact]
    public void SymbolPreservationTest() {
      TestPreservation("-----");
      TestPreservation("#####");
      TestPreservation("〞〞〞〞〞");
      TestPreservation("式式式式式");
      TestPreservation("--##〞〞@@--");
    }

    [Fact]
    public void WhitespacePreservationTest1() {
      TestPreservation("\r");
    }

    [Fact]
    public void WhitespacePreservationTest2() {
      TestPreservation("\n\nd");
    }

    [Fact]
    public void WhitespacePreservationTest3() {
      TestPreservation("\r\n");
    }

    [Fact]
    public void WhitespacePreservationTest4() {
      TestPreservation("\n\n\n ﹛\n\n");
    }
  }
}
