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

      var imported = new ManagedPath(@"rengex\1_source\001.tran.txt");
      Assert.Equal(@"001.tran.txt", imported.RelativePath);
      Assert.Equal(@"rengex\1_source\001.tran.txt", imported.SourcePath);
      Assert.Equal(@"rengex\2_meta\001.tran.txt.meta.txt", imported.MetadataPath);
      Assert.Equal(@"rengex\3_translation\001.tran.txt.tran.txt", imported.TranslationPath);
      Assert.Equal(@"rengex\4_result\001.tran.txt", imported.DestinationPath);
    }
  }
}
