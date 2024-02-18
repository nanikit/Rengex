namespace Rengex.Helper {

  using System;
  using System.Collections.Concurrent;
  using System.Collections.Generic;
  using System.ComponentModel;
  using System.IO;
  using System.Linq;
  using System.Runtime.CompilerServices;
  using System.Threading.Tasks;
  using System.Windows.Input;

  public static class Util {

    public static async Task CopyFileAsync(string sourcePath, string destinationPath) {
      using Stream source = File.Open(sourcePath, FileMode.Open);
      using Stream destination = File.Create(destinationPath);
      await source.CopyToAsync(destination).ConfigureAwait(false);
    }

    public static Task ForEachAsync<T>(this IEnumerable<T> source, int dop, Func<T, Task> body) {
      var parts = Partitioner.Create(source).GetPartitions(dop);
      var tasks = parts.Select(p => Task.Run(async () => {
        using (p) {
          while (p.MoveNext()) {
            await body(p.Current).ConfigureAwait(false);
          }
        }
      }));
      return Task.WhenAll(tasks);
    }

    public static Task ForEachPinnedAsync<T>(this IEnumerable<T> source, int dop, Func<T, Task> body) {
      var parts = Partitioner.Create(source).GetPartitions(dop);
      var tasks = parts.Select(async p => {
        using (p) {
          while (p.MoveNext()) {
            await body(p.Current);
          }
        }
      });
      return Task.WhenAll(tasks);
    }

    public static string GetEllipsisPath(string path, int len) {
      string ellipsisPath = path.Length > len
        ? $"...{path[^len..]}"
        : path;
      return ellipsisPath;
    }

    public static string PrepareDirectory(string path) {
      string? directory = Path.GetDirectoryName(path);
      if (directory != null) {
        Directory.CreateDirectory(directory);
      }
      return path;
    }
  }

  /// <summary>
  /// Creates a new command.
  /// </summary>
  /// <param name="execute">The execution logic.</param>
  /// <param name="canExecute">The execution status logic.</param>
  public class RelayCommand<T>(Action<T> execute, Predicate<T>? canExecute = null) : ICommand {
    private readonly Predicate<T> _canExecute = canExecute ?? (_ => true);

    private readonly Action<T> _execute = execute ?? throw new ArgumentNullException(nameof(execute));

    public void NotifyCanExecute() {
      CanExecuteChanged?.Invoke(null, new EventArgs());
    }

    #region ICommand Members

    ///<summary>
    ///Occurs when changes occur that affect whether or not the command should execute.
    ///</summary>
    public event EventHandler? CanExecuteChanged;

    ///<summary>
    ///Defines the method that determines whether the command can execute in its current state.
    ///</summary>
    ///<param name="parameter">Data used by the command. If the command does not require
    ///data to be passed, this object can be set to null.</param>
    ///<returns>
    ///true if this command can be executed; otherwise, false.
    ///</returns>
    public bool CanExecute(object? parameter) {
      return parameter is T t && _canExecute(t);
    }

    ///<summary>
    ///Defines the method to be called when the command is invoked.
    ///</summary>
    ///<param name="parameter">Data used by the command. If the command does not
    ///require data to be passed, this object can be set to <see langword="null" />.</param>
    public void Execute(object? parameter) {
      if (parameter is T t) {
        _execute(t);
      }
    }

    #endregion ICommand Members
  }

  public class ViewModelBase : INotifyPropertyChanged {

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void NotifyChange(string? name) {
      var ev = new PropertyChangedEventArgs(name);
      PropertyChanged?.Invoke(this, ev);
    }

    /// <summary>
    /// It should be called only in the property setter.
    /// </summary>
    protected void Set<T>(ref T member, T value, [CallerMemberName] string? name = null) {
      if (Equals(member, value)) {
        return;
      }
      member = value;
      NotifyChange(name);
    }
  }
}
