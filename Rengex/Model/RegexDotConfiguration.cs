namespace Rengex.Model {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;

  public class RegexDotConfiguration {
    public event Action<FileSystemEventArgs> ConfigReloaded = delegate { };
    public event Action<FileSystemEventArgs, Exception> ConfigFaulted = delegate { };

    private readonly DotConfigurator<MatchConfig> Matcher;
    private readonly DotConfigurator<ReplaceConfig> Replacer;

    public IEnumerable<string> RegionPaths => Matcher.RegionPaths.Concat(Replacer.RegionPaths);

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

    public RegexConfiguration GetConfiguration(string path) {
      var matcher = Matcher.GetConfiguration(path);
      var replacer = Replacer.GetConfiguration(path);
      return new RegexConfiguration(matcher, replacer);
    }

    private void Reloaded(FileSystemEventArgs obj) {
      ConfigReloaded.Invoke(obj);
    }

    private void Faulted(FileSystemEventArgs fse, Exception e) {
      ConfigFaulted.Invoke(fse, e);
    }
  }
}
