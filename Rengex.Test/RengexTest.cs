namespace Rengex.Tests {
  using Rengex;
  using Xunit;

  public class RengexTest {
    [Fact]
    public void TestStub() {
    }
  }

  public class ManagedPathTest {
    [Fact]
    public void TestDerivedPath() {
      var external = new ManagedPath(@"D:\some\other\file.json", root: @"D:\some");
      Assert.Equal(@"other\file.json", external.RelativePath);
      Assert.Equal(@"rengex\1_source\other\file.json", external.SourcePath);
      Assert.Equal(@"rengex\2_meta\other\file.json.meta.txt", external.MetadataPath);
      Assert.Equal(@"rengex\3_translation\other\file.json.tran.txt", external.TranslationPath);
      Assert.Equal(@"rengex\4_result\other\file.json", external.DestinationPath);
    }
  }
}
