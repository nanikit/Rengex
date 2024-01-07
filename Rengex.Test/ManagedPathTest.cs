namespace Rengex.Tests {

  using Microsoft.VisualStudio.TestTools.UnitTesting;
  using Rengex.Model;

  [TestClass]
  public class ManagedPathTest {

    [TestMethod]
    public void TestExternalPath() {
      var external = new ManagedPath(@"D:\some\other\file.json", root: @"D:\some");
      Assert.AreEqual(@"other\file.json", external.RelativePath);
      Assert.AreEqual(@"rengex\1_original\other\file.json", external.OriginalPath);
      Assert.AreEqual(@"rengex\2_meta\other\file.json.meta.txt", external.MetadataPath);
      Assert.AreEqual(@"rengex\3_source\other\file.json.txt", external.SourcePath);
      Assert.AreEqual(@"rengex\4_target\other\file.json.txt", external.TargetPath);
      Assert.AreEqual(@"rengex\5_result\other\file.json", external.ResultPath);
    }

    [TestMethod]
    public void TestMetaPath() {
      var meta = new ManagedPath(@"rengex\2_meta\001.txt.meta.txt");
      Assert.AreEqual(@"001.txt", meta.RelativePath);
      Assert.AreEqual(@"rengex\1_original\001.txt", meta.OriginalPath);
      Assert.AreEqual(@"rengex\2_meta\001.txt.meta.txt", meta.MetadataPath);
      Assert.AreEqual(@"rengex\3_source\001.txt.txt", meta.SourcePath);
      Assert.AreEqual(@"rengex\4_target\001.txt.txt", meta.TargetPath);
      Assert.AreEqual(@"rengex\5_result\001.txt", meta.ResultPath);
    }

    [TestMethod]
    public void TestOriginalPath() {
      var imported = new ManagedPath(@"rengex\1_original\001.txt");
      Assert.AreEqual(@"001.txt", imported.RelativePath);
      Assert.AreEqual(@"rengex\1_original\001.txt", imported.OriginalPath);
      Assert.AreEqual(@"rengex\2_meta\001.txt.meta.txt", imported.MetadataPath);
      Assert.AreEqual(@"rengex\3_source\001.txt.txt", imported.SourcePath);
      Assert.AreEqual(@"rengex\4_target\001.txt.txt", imported.TargetPath);
      Assert.AreEqual(@"rengex\5_result\001.txt", imported.ResultPath);
    }

    [TestMethod]
    public void TestResultPath() {
      var result = new ManagedPath(@"rengex\5_result\001.txt");
      Assert.AreEqual(@"001.txt", result.RelativePath);
      Assert.AreEqual(@"rengex\1_original\001.txt", result.OriginalPath);
      Assert.AreEqual(@"rengex\2_meta\001.txt.meta.txt", result.MetadataPath);
      Assert.AreEqual(@"rengex\3_source\001.txt.txt", result.SourcePath);
      Assert.AreEqual(@"rengex\4_target\001.txt.txt", result.TargetPath);
      Assert.AreEqual(@"rengex\5_result\001.txt", result.ResultPath);
    }

    [TestMethod]
    public void TestSourcePath() {
      var source = new ManagedPath(@"rengex\3_source\001.txt.txt");
      Assert.AreEqual(@"001.txt", source.RelativePath);
      Assert.AreEqual(@"rengex\1_original\001.txt", source.OriginalPath);
      Assert.AreEqual(@"rengex\2_meta\001.txt.meta.txt", source.MetadataPath);
      Assert.AreEqual(@"rengex\3_source\001.txt.txt", source.SourcePath);
      Assert.AreEqual(@"rengex\4_target\001.txt.txt", source.TargetPath);
      Assert.AreEqual(@"rengex\5_result\001.txt", source.ResultPath);
    }

    [TestMethod]
    public void TestTargetPath() {
      var target = new ManagedPath(@"rengex\4_target\001.txt.txt");
      Assert.AreEqual(@"001.txt", target.RelativePath);
      Assert.AreEqual(@"rengex\1_original\001.txt", target.OriginalPath);
      Assert.AreEqual(@"rengex\2_meta\001.txt.meta.txt", target.MetadataPath);
      Assert.AreEqual(@"rengex\3_source\001.txt.txt", target.SourcePath);
      Assert.AreEqual(@"rengex\4_target\001.txt.txt", target.TargetPath);
      Assert.AreEqual(@"rengex\5_result\001.txt", target.ResultPath);
    }
  }
}
