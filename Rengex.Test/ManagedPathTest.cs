namespace Rengex.Tests {
  using Rengex;
  using Xunit;

  public class ManagedPathTest {
    private readonly ManagedPath external = new ManagedPath(@"D:\some\other\file.json", root: @"D:\some");
    private readonly ManagedPath imported = new ManagedPath(@"rengex\1_source\001.tran.txt");
    private readonly ManagedPath meta = new ManagedPath(@"rengex\2_meta\001.tran.txt.meta.txt");
    private readonly ManagedPath translated = new ManagedPath(@"rengex\3_translation\001.tran.txt.tran.txt");
    private readonly ManagedPath resulted = new ManagedPath(@"rengex\4_result\001.tran.txt");

    [Fact]
    public void TestExternalPath() {
      Assert.Equal(@"other\file.json", external.RelativePath);
      Assert.Equal(@"rengex\1_source\other\file.json", external.SourcePath);
      Assert.Equal(@"rengex\2_meta\other\file.json.meta.txt", external.MetadataPath);
      Assert.Equal(@"rengex\3_translation\other\file.json.tran.txt", external.TranslationPath);
      Assert.Equal(@"rengex\4_result\other\file.json", external.DestinationPath);
    }

    [Fact]
    public void TestImportedPath() {
      Assert.Equal(@"001.tran.txt", imported.RelativePath);
      Assert.Equal(@"rengex\1_source\001.tran.txt", imported.SourcePath);
      Assert.Equal(@"rengex\2_meta\001.tran.txt.meta.txt", imported.MetadataPath);
      Assert.Equal(@"rengex\3_translation\001.tran.txt.tran.txt", imported.TranslationPath);
      Assert.Equal(@"rengex\4_result\001.tran.txt", imported.DestinationPath);
    }

    [Fact]
    public void TestMetaPath() {
      Assert.Equal(@"001.tran.txt", meta.RelativePath);
      Assert.Equal(@"rengex\1_source\001.tran.txt", meta.SourcePath);
      Assert.Equal(@"rengex\2_meta\001.tran.txt.meta.txt", meta.MetadataPath);
      Assert.Equal(@"rengex\3_translation\001.tran.txt.tran.txt", meta.TranslationPath);
      Assert.Equal(@"rengex\4_result\001.tran.txt", meta.DestinationPath);
    }

    [Fact]
    public void TestTranslatedPath() {
      Assert.Equal(@"001.tran.txt", translated.RelativePath);
      Assert.Equal(@"rengex\1_source\001.tran.txt", translated.SourcePath);
      Assert.Equal(@"rengex\2_meta\001.tran.txt.meta.txt", translated.MetadataPath);
      Assert.Equal(@"rengex\3_translation\001.tran.txt.tran.txt", translated.TranslationPath);
      Assert.Equal(@"rengex\4_result\001.tran.txt", translated.DestinationPath);
    }

    [Fact]
    public void TestResultPath() {
      Assert.Equal(@"001.tran.txt", resulted.RelativePath);
      Assert.Equal(@"rengex\1_source\001.tran.txt", resulted.SourcePath);
      Assert.Equal(@"rengex\2_meta\001.tran.txt.meta.txt", resulted.MetadataPath);
      Assert.Equal(@"rengex\3_translation\001.tran.txt.tran.txt", resulted.TranslationPath);
      Assert.Equal(@"rengex\4_result\001.tran.txt", resulted.DestinationPath);
    }
  }
}
