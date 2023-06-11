namespace Rengex.Tests {
  using Microsoft.VisualStudio.TestTools.UnitTesting;
  using Moq;
  using Rengex.Model;
  using Rengex.Translator;
  using System.IO;
  using System.Text;
  using System.Threading.Tasks;

  [TestClass]
  public class ReconstructorTest {
    private readonly Reconstructor _reconstructor;

    public ReconstructorTest() {
      // ReconstructionFlow: +FileSystem;
      // CompilationUnit: FlowFileSet;
      var config = new RegexConfiguration(new MatchConfig(), new ReplaceConfig());
      _reconstructor = new Reconstructor(config);
    }

    [TestMethod]
    public async Task TestExtraction() {
      using var original = new MemoryStream(Encoding.UTF8.GetBytes("Just sample"));
      using var meta = new StringWriter();
      using var source = new StringWriter();
      await _reconstructor.Extract(original, meta, source).ConfigureAwait(false);

      Assert.AreEqual("0,11,text\r\n", meta.ToString());
      Assert.AreEqual("Just sample\r\n", source.ToString());
    }

    [TestMethod]
    public async Task TestTranslate() {
      var translator = new Mock<ITranslator>();
      translator.Setup(x => x.Translate("extracted\r\n")).Returns(Task.FromResult("TRANSLATED\r\n"));
      using var meta = new StringReader("0,11,text\n");
      using var source = new StringReader("extracted\r\n");
      using var target = new StringWriter();

      await _reconstructor.Translate(meta, source, translator.Object, target).ConfigureAwait(false);

      Assert.AreEqual("TRANSLATED\r\n", target.ToString());
    }

    [TestMethod]
    public async Task TestMerge() {
      using var original = new MemoryStream(Encoding.UTF8.GetBytes("Just sample"));
      using var meta = new StringReader("0,11,text\n");
      using var target = new StringReader("TRANSLATED\n");
      using var result = new MemoryStream();

      await _reconstructor.Merge(original, meta, target, result).ConfigureAwait(false);

      result.Position = 0;
      string resultContent = new StreamReader(result).ReadToEnd();
      Assert.AreEqual("TRANSLATED\n", resultContent);
    }
  }
}
