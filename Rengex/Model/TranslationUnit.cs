namespace Rengex.Model {
  using Rengex.Helper;
  using Rengex.Translator;
  using System.IO;
  using System.Threading.Tasks;

  public class TranslationUnit {

    public ManagedPath ManagedPath { get; set; }
    public IRegexDotConfiguration DotConfig { get; set; }

    public TranslationUnit(IRegexDotConfiguration dot, ManagedPath path) {
      DotConfig = dot;
      ManagedPath = path;
    }

    public async Task ExtractSourceText() {
      if (!ManagedPath.IsInProject) {
        await ManagedPath.CopyToSourceDirectory().ConfigureAwait(false);
      }

      var reconstructor = new Reconstructor(DotConfig.GetConfiguration(ManagedPath.OriginalPath));

      using var original = File.OpenRead(ManagedPath.OriginalPath);
      using var meta = TextUtils.GetReadSharedWriter(ManagedPath.MetadataPath);
      using var source = TextUtils.GetReadSharedWriter(ManagedPath.SourcePath);
      await reconstructor.Extract(original, meta, source).ConfigureAwait(false);
    }

    public async Task MachineTranslate(ITranslator translator) {
      if (!File.Exists(ManagedPath.SourcePath)) {
        await ExtractSourceText().ConfigureAwait(false);
      }
      if (!File.Exists(ManagedPath.SourcePath)) {
        return;
      }

      string jp = await File.ReadAllTextAsync(ManagedPath.SourcePath).ConfigureAwait(false);
      string kr = await translator.Translate(jp).ConfigureAwait(false);
      string targetPath = Util.PrecreateDirectory(ManagedPath.TargetPath);
      await File.WriteAllTextAsync(targetPath, kr).ConfigureAwait(false);
    }

    public async Task BuildTranslation() {
      var reconstructor = new Reconstructor(DotConfig.GetConfiguration(ManagedPath.OriginalPath));

      using var original = File.OpenRead(ManagedPath.OriginalPath);
      using var meta = File.OpenText(ManagedPath.MetadataPath);
      using var target = File.OpenText(ManagedPath.TargetPath);
      using var result = File.Create(Util.PrecreateDirectory(ManagedPath.ResultPath));

      await reconstructor.Merge(original, meta, target, result).ConfigureAwait(false);
    }
  }
}
