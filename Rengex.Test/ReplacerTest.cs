namespace Rengex.Tests {

  using Microsoft.VisualStudio.TestTools.UnitTesting;
  using Rengex.Model;

  [TestClass]
  public class ReplacerTest {

    [TestMethod]
    public void TestPostExtendedReplace() {
      string source = "x10혼마석(조각)\r";
      string expected = "x10 혼마석(조각)\r";
      var pattern = new ReplaceConfig.ReplacePattern(
        @"(?:)(x)(\d{1,5})(?=.*?\0(text$))",
        @"$1$2\u0020"
      );
      var rule = new ReplaceConfig.PostprocessPattern(pattern);

      string actual = rule.Postprocess("x10 魂魔石（欠片）\r\0text", source);

      Assert.AreEqual(actual, expected);
    }

    [TestMethod]
    public void TestPreExtendedReplace() {
      string source = "x10혼마석(조각)\r";
      string expected = "___________";
      var pattern = new ReplaceConfig.ReplacePattern(
        @"(?:).",
        @"_"
      );
      var rule = new ReplaceConfig.PreprocessPattern(pattern);

      string actual = rule.Preprocess("\0text", source);

      Assert.AreEqual(actual, expected);
    }
  }
}
