namespace Rengex.Model {

  using Rengex;
  using Rengex.Helper;
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Text.RegularExpressions;
  using System.Threading.Tasks;

  public class ManagedPath {
    public const string ProjectDirectory = "rengex";
    public static readonly string MetadataDirectory = Path.Combine(ProjectDirectory, MetadataName);
    public static readonly string OriginalDirectory = Path.Combine(ProjectDirectory, OriginalName);
    public static readonly string ResultDirectory = Path.Combine(ProjectDirectory, ResultName);
    public static readonly string SourceDirectory = Path.Combine(ProjectDirectory, SourceName);
    public static readonly string TargetDirectory = Path.Combine(ProjectDirectory, TargetName);
    private const string MetadataName = "2_meta";
    private const string OriginalName = "1_original";
    private const string ResultName = "5_result";
    private const string SourceName = "3_source";
    private const string TargetName = "4_target";

    #region Fields and properties (mainly for path)

    /// <summary>
    /// 원본 파일 절대 경로
    /// </summary>
    public string? ExternalPath { get; private set; }

    public bool IsInProject {
      get {
        string projectPath = $"{ProjectPath}{Path.DirectorySeparatorChar}";
        return ExternalPath?.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase) ?? true;
      }
    }

    /// <summary>
    /// 메타데이터 파일 경로.
    /// </summary>
    public string MetadataPath => Path.Combine(MetadataDirectory, $"{RelativePath}.meta.txt");

    /// <summary>
    /// 프로젝트 폴더 안에 복사한 원본 파일 경로.
    /// </summary>
    public string OriginalPath => Path.Combine(OriginalDirectory, RelativePath);

    /// <summary>
    /// 프로젝트 경로
    /// </summary>
    public string ProjectPath { get; private set; }

    /// <summary>
    /// 프로젝트 내에서의 상대 경로
    /// </summary>
    public string RelativePath { get; private set; }

    /// <summary>
    /// 결과 파일 경로.
    /// </summary>
    public string ResultPath => Path.Combine(ResultDirectory, RelativePath);

    /// <summary>
    /// 시작어 파일 경로.
    /// </summary>
    public string SourcePath => Path.Combine(SourceDirectory, $"{RelativePath}.txt");

    /// <summary>
    /// 도착어 파일 경로.
    /// </summary>
    public string TargetPath => Path.Combine(TargetDirectory, $"{RelativePath}.txt");

    #endregion Fields and properties (mainly for path)

    /// <summary>
    /// Represents matching paths in project.
    /// </summary>
    /// <param name="originalPath">Original file out of project directory.</param>
    /// <param name="projectPath">Project root path.</param>
    /// <param name="root">Original file's anchor path deriving relative path.</param>
    public ManagedPath(string originalPath, string? root = null) {
      ExternalPath = Path.GetFullPath(originalPath);
      ProjectPath = Path.GetFullPath(ProjectDirectory);

      if (IsInProject) {
        string relative = Path.GetRelativePath(ProjectPath, ExternalPath);
        relative = Regex.Replace(relative, $"^{MetadataName}\\\\(.*?)\\.meta\\.txt$", "$1");
        relative = Regex.Replace(relative, $"^(?:{SourceName}|{TargetName})\\\\(.*?)\\.txt$", "$1");
        relative = Regex.Replace(relative, $"^(?:{OriginalName}|{ResultName})\\\\", "");
        RelativePath = relative == "" ? "." : relative;
      }
      else if (root != null) {
        string rootPath = Path.GetFullPath(root);
        RelativePath = Path.GetRelativePath(rootPath, ExternalPath);
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
      string parent = Path.GetDirectoryName(path)!;
      if (Directory.Exists(path)) {
        var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories);
        foreach (string file in files) {
          if (!IsConfigFile(file)) {
            units.Add(new ManagedPath(file, parent));
          }
        }
      }
      else if (File.Exists(path)) {
        units.Add(new ManagedPath(path, parent));
      }
      return units;
    }

    public Task CopyToSourceDirectory() {
      _ = Util.PrepareDirectory(OriginalPath);
      return Util.CopyFileAsync(ExternalPath!, OriginalPath);
    }

    private static bool IsConfigFile(string file) {
      return file.EndsWith(MatchConfig.Extension, StringComparison.Ordinal)
        || file.EndsWith(ReplaceConfig.Extension, StringComparison.Ordinal);
    }
  }
}
