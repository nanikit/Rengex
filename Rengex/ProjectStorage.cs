namespace Rengex {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Text.RegularExpressions;

  public interface IProjectStorage {
    /// <summary>
    /// 상대 경로.
    /// </summary>
    string RelativePath { get; }
    /// <summary>
    /// 사본 파일 경로.
    /// </summary>
    string SourcePath { get; }
    /// <summary>
    /// 메타데이터 파일 경로.
    /// </summary>
    string MetadataPath { get; }
    /// <summary>
    /// 번역작업 파일 경로.
    /// </summary>
    string TranslationPath { get; }
    /// <summary>
    /// 결과 파일 경로.
    /// </summary>
    string DestinationPath { get; }
  }

  internal class CwdDesignator : IProjectStorage {

    // TODO: Remove separator suffix
    public const string ProjectDirectory = "rengex";
    public const string SourceDirectory = ProjectDirectory + "\\1_source\\";
    public const string MetadataDirectory = ProjectDirectory + "\\2_meta\\";
    public const string TranslationDirectory = ProjectDirectory + "\\3_translation\\";
    public const string DestinationDirectory = ProjectDirectory + "\\4_result\\";
    private static readonly Regex RxTransExt = new Regex(@"(.*)\.tran(?:[_-]번역)?\.txt$");

    static CwdDesignator() {
      _ = Directory.CreateDirectory(ProjectDirectory);
    }

    public static IEnumerable<CwdDesignator> FindTranslations() {
      return Directory
        .EnumerateFiles(MetadataDirectory, "*.meta.txt", SearchOption.AllDirectories)
        .Select(GetDesignatorFromMetadata);
    }

    private static CwdDesignator GetDesignatorFromMetadata(string path) {
      string sourcePathSuffixed = path.Replace(MetadataDirectory, SourceDirectory);
      string sourcePath = sourcePathSuffixed.Replace(".meta.txt", "");
      return new CwdDesignator(SourceDirectory, sourcePath);
    }

    #region Fields and properties (mainly for path)

    /// <summary>
    /// 원본 파일 절대 경로
    /// </summary>
    public string OriginalPath { get; private set; }
    /// <summary>
    /// 프로젝트 큐 내에서의 상대 경로
    /// </summary>
    public string RelativePath { get; private set; }

    /// <summary>
    /// 프로젝트 폴더 안에 복사한 원본 파일 경로.
    /// </summary>
    public string SourcePath => $"{SourceDirectory}\\{RelativePath}";

    public string MetadataPath => $"{MetadataDirectory}\\{RelativePath}.meta.txt";

    public string TranslationPath => $"{TranslationDirectory}\\{RelativePath}.tran.txt";

    public string DestinationPath => $"{DestinationDirectory}\\{RelativePath}";

    #endregion

    public CwdDesignator(string root, string path) {
      OriginalPath = Path.GetFullPath(path);
      RelativePath = GetRelativePathFromQueuePath(OriginalPath)
        ?? GetRelativePath(root, OriginalPath);
      CopyToSourceDirectory(OriginalPath);
    }

    private static bool StartsWithAbsoluteOf(string includer, string includee) {
      return includer.StartsWith(Path.GetFullPath(includee), StringComparison.InvariantCultureIgnoreCase);
    }

    private static string? GetRelativePathFromQueuePath(string absolute) {
      if (StartsWithAbsoluteOf(absolute, SourceDirectory)) {
        return GetRelativePathTo(SourceDirectory, absolute);
      }
      if (StartsWithAbsoluteOf(absolute, MetadataDirectory) &&
        absolute.EndsWith(".meta.txt", StringComparison.Ordinal)) {
        string rel = GetRelativePathTo(MetadataDirectory, absolute);
        return rel[0..^9];
      }
      if (StartsWithAbsoluteOf(absolute, TranslationDirectory)) {
        string rel = GetRelativePathTo(TranslationDirectory, absolute);
        Match m = RxTransExt.Match(rel);
        if (m.Success) {
          return m.Result("$1");
        }
      }
      return StartsWithAbsoluteOf(absolute, DestinationDirectory)
        ? GetRelativePathTo(DestinationDirectory, absolute)
        : null;
    }

    private static string GetRelativePath(string root, string absolute) {
      string relative = GetRelativePathTo($"{root}\\", absolute);
      return relative.Contains("..\\")
        ? throw new Exception("path cannot be upper than root")
        : relative;
    }

    /// <summary>
    /// 상대 경로를 반환. 그러나 anchor 인자에 /를 뒤에 붙여야 디렉토리로 인식한다.
    /// </summary>
    /// <param name="anchor">기준</param>
    /// <param name="point">상대경로로 변환할 대상</param>
    /// <returns></returns>
    private static string GetRelativePathTo(string anchor, string point) {
      var anchorUri = new Uri(Path.GetFullPath(anchor));
      var pointUri = new Uri(Path.GetFullPath(point));
      Uri relative = anchorUri.MakeRelativeUri(pointUri);
      string path = Uri.UnescapeDataString(relative.OriginalString);
      return path.Replace('/', '\\');
    }

    public static List<CwdDesignator> WalkForSources(string path) {
      var units = new List<CwdDesignator>();
      if (Directory.Exists(path)) {
        string parent = Path.GetDirectoryName(path)!;
        IEnumerable<string>? files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories);
        foreach (string file in files) {
          if (!IsConfigFile(file)) {
            units.Add(new CwdDesignator(parent, file));
          }
        }
      }
      else if (File.Exists(path)) {
        string dir = Path.GetDirectoryName(path)!;
        units.Add(new CwdDesignator(dir, path));
      }
      return units;
    }

    private static bool IsConfigFile(string file) {
      return file.EndsWith(MatchConfig.Extension, StringComparison.Ordinal)
        || file.EndsWith(ReplaceConfig.Extension, StringComparison.Ordinal);
    }

    private void CopyToSourceDirectory(string path) {
      if (File.Exists(SourcePath)) {
        return;
      }
      _ = Util.PrecreateDirectory(SourcePath);
      File.Copy(path, SourcePath, true);
    }
  }
}
