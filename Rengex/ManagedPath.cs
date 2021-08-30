namespace Rengex {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Text.RegularExpressions;
  using System.Threading.Tasks;

  public class ManagedPath {

    public const string ProjectDirectory = "rengex";
    public static readonly string SourceDirectory = Path.Combine(ProjectDirectory, "1_source");
    public static readonly string MetadataDirectory = Path.Combine(ProjectDirectory, "2_meta");
    public static readonly string TranslationDirectory = Path.Combine(ProjectDirectory, "3_translation");
    public static readonly string DestinationDirectory = Path.Combine(ProjectDirectory, "4_result");

    #region Fields and properties (mainly for path)

    /// <summary>
    /// 원본 파일 절대 경로
    /// </summary>
    public string? OriginalPath { get; private set; }
    /// <summary>
    /// 프로젝트 내에서의 상대 경로
    /// </summary>
    public string RelativePath { get; private set; }
    /// <summary>
    /// 프로젝트 경로
    /// </summary>
    public string ProjectPath { get; private set; }

    /// <summary>
    /// 프로젝트 폴더 안에 복사한 원본 파일 경로.
    /// </summary>
    public string SourcePath => Path.Combine(SourceDirectory, RelativePath);
    /// <summary>
    /// 메타데이터 파일 경로.
    /// </summary>
    public string MetadataPath => Path.Combine(MetadataDirectory, $"{RelativePath}.meta.txt");
    /// <summary>
    /// 번역작업 파일 경로.
    /// </summary>
    public string TranslationPath => Path.Combine(TranslationDirectory, $"{RelativePath}.tran.txt");
    /// <summary>
    /// 결과 파일 경로.
    /// </summary>
    public string DestinationPath => Path.Combine(DestinationDirectory, RelativePath);

    public bool IsInProject {
      get {
        string projectPath = $"{ProjectPath}{Path.DirectorySeparatorChar}";
        return OriginalPath?.StartsWith(projectPath, StringComparison.InvariantCultureIgnoreCase) ?? true;
      }
    }

    #endregion

    /// <summary>
    /// Represents matching paths in project.
    /// </summary>
    /// <param name="originalPath">Original file out of project directory.</param>
    /// <param name="projectPath">Project root path.</param>
    /// <param name="root">Original file's anchor path deriving relative path.</param>
    public ManagedPath(string originalPath, string? root = null) {
      OriginalPath = Path.GetFullPath(originalPath);
      ProjectPath = Path.GetFullPath(ProjectDirectory);

      if (IsInProject) {
        string relative = Path.GetRelativePath(ProjectPath, OriginalPath);
        char separator = Path.DirectorySeparatorChar;
        relative = string.Join(separator, relative.Split(separator).Skip(1));
        relative = Regex.Replace(relative, @"\.(meta|tran(_번역)?)\.txt$", "");
        RelativePath = relative == "" ? "." : relative;
      }
      else if (root != null) {
        string rootPath = Path.GetFullPath(root);
        RelativePath = Path.GetRelativePath(rootPath, OriginalPath);
        if (RelativePath.Contains("..\\")) {
          throw new RengexException("Incorrect file anchor.");
        }
      }
      else {
        RelativePath = Path.GetFileName(originalPath);
      }
    }

    public static List<ManagedPath> WalkForSources(string path) {
      var units = new List<ManagedPath>();
      if (Directory.Exists(path)) {
        IEnumerable<string>? files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories);
        foreach (string file in files) {
          if (!IsConfigFile(file)) {
            units.Add(new ManagedPath(file, path));
          }
        }
      }
      else if (File.Exists(path)) {
        units.Add(new ManagedPath(path, Path.GetDirectoryName(path)));
      }
      return units;
    }

    public Task CopyToSourceDirectory() {
      _ = Util.PrecreateDirectory(SourcePath);
      return Util.CopyFileAsync(OriginalPath!, SourcePath);
    }

    private static bool IsConfigFile(string file) {
      return file.EndsWith(MatchConfig.Extension, StringComparison.Ordinal)
        || file.EndsWith(ReplaceConfig.Extension, StringComparison.Ordinal);
    }
  }
}
