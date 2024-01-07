namespace Rengex.Model {

  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;

  public interface IRegexDotConfiguration {

    RegexConfiguration GetConfiguration(string path);
  }

  public class RegexDotConfiguration : IRegexDotConfiguration {
    private readonly DotConfigurator<MatchConfig> Matcher;

    private readonly DotConfigurator<ReplaceConfig> Replacer;

    public RegexDotConfiguration(
      string projectDir,
      Action<FileSystemEventArgs> reloaded,
      Action<FileSystemEventArgs, Exception> faulted
    ) {
      Directory.CreateDirectory(projectDir);
      ConfigReloaded += reloaded;
      ConfigFaulted += faulted;
      Matcher = new DotConfigurator<MatchConfig>(projectDir, Reloaded, Faulted);
      Replacer = new DotConfigurator<ReplaceConfig>(projectDir, Reloaded, Faulted);
    }

    public event Action<FileSystemEventArgs, Exception> ConfigFaulted = delegate { };

    public event Action<FileSystemEventArgs> ConfigReloaded = delegate { };

    public IEnumerable<string> RegionPaths => Matcher.RegionPaths.Concat(Replacer.RegionPaths);

    public RegexConfiguration GetConfiguration(string path) {
      var matcher = Matcher.GetConfiguration(path);
      var replacer = Replacer.GetConfiguration(path);
      return new RegexConfiguration(matcher, replacer);
    }

    private void Faulted(FileSystemEventArgs fse, Exception e) {
      ConfigFaulted.Invoke(fse, e);
    }

    private void Reloaded(FileSystemEventArgs obj) {
      ConfigReloaded.Invoke(obj);
    }
  }
}
