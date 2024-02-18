namespace Rengex.Model {

  using Rengex.Helper;
  using Rengex.Translator;
  using System.IO;
  using System.Threading.Tasks;

  public class TranslationUnit {

    public TranslationUnit(IRegexDotConfiguration dot, ManagedPath path) {
      DotConfig = dot;
      ManagedPath = path;
    }

    public IRegexDotConfiguration DotConfig { get; set; }
    public ManagedPath ManagedPath { get; set; }

    public async Task BuildTranslation() {
      var reconstructor = new Reconstructor(DotConfig.GetConfiguration(ManagedPath.OriginalPath));

      using var original = File.OpenRead(ManagedPath.OriginalPath);
      using var meta = File.OpenText(ManagedPath.MetadataPath);
      using var target = File.OpenText(ManagedPath.TargetPath);
      using var result = File.Create(Util.PrepareDirectory(ManagedPath.ResultPath));

      await reconstructor.Merge(original, meta, target, result).ConfigureAwait(false);
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

      using var meta = File.OpenText(ManagedPath.MetadataPath);
      using var jp = File.OpenText(ManagedPath.SourcePath);
      using var kr = File.CreateText(Util.PrepareDirectory(ManagedPath.TargetPath));
      var reconstructor = new Reconstructor(DotConfig.GetConfiguration(ManagedPath.OriginalPath));
      await reconstructor.Translate(meta, jp, translator, kr).ConfigureAwait(false);
    }
  }
}
