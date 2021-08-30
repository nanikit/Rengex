namespace Rengex {
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
    public static string PrecreateDirectory(string path) {
      string? directory = Path.GetDirectoryName(path);
      if (directory != null) {
        Directory.CreateDirectory(directory);
      }
      return path;
    }

    public static string GetEllipsisPath(string path, int len) {
      string ellipsisPath = path.Length > len
        ? $"...{path.Substring(path.Length - len)}"
        : path;
      return ellipsisPath;
    }

    public static async Task CopyFileAsync(string sourcePath, string destinationPath) {
      using Stream source = File.Open(sourcePath, FileMode.Open);
      using Stream destination = File.Create(destinationPath);
      await source.CopyToAsync(destination).ConfigureAwait(false);
    }

    public static Task ForEachAsync<T>(this IEnumerable<T> source, int dop, Func<T, Task> body) {
      IList<IEnumerator<T>> parts = Partitioner.Create(source).GetPartitions(dop);
      IEnumerable<Task> tasks = parts.Select(p => Task.Run(async () => {
        using (p) {
          while (p.MoveNext()) {
            await body(p.Current).ConfigureAwait(false);
          }
        }
      }));
      return Task.WhenAll(tasks);
    }

    public static Task ForEachPinnedAsync<T>(this IEnumerable<T> source, int dop, Func<T, Task> body) {
      IList<IEnumerator<T>> parts = Partitioner.Create(source).GetPartitions(dop);
      IEnumerable<Task> tasks = parts.Select(async p => {
        using (p) {
          while (p.MoveNext()) {
            await body(p.Current);
          }
        }
      });
      return Task.WhenAll(tasks);
    }
  }

  public class ViewModelBase : INotifyPropertyChanged {
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// It should be called only in the property setter.
    /// </summary>
    protected void Set<T>(ref T member, T value, [CallerMemberName] string name = null) {
      if (Equals(member, value)) {
        return;
      }
      member = value;
      NotifyChange(name);
    }

    protected void NotifyChange(string name) {
      var ev = new PropertyChangedEventArgs(name);
      PropertyChanged?.Invoke(this, ev);
    }
  }

  public class RelayCommand : ICommand {
    readonly Action _execute;
    readonly Func<bool> _canExecute;

    public RelayCommand(Action execute) : this(execute, null) {
    }

    public RelayCommand(Action execute, Func<bool> canExecute) {
      _execute = execute;
      _canExecute = canExecute ?? (() => true);
    }

    public event EventHandler CanExecuteChanged;

    public bool CanExecute(object parameter) {
      return _canExecute();
    }

    public void Execute(object parameter) {
      _execute();
    }

    public void NotifyCanExecute() {
      CanExecuteChanged.Invoke(null, null);
    }
  }

  public class RelayCommand<T> : ICommand {

    private static T NullToDefault(object parameter) {
      return parameter == null ? default : (T)parameter;
    }

    readonly Action<T> _execute;
    readonly Predicate<T> _canExecute;

    /// <summary>
    /// Initializes a new instance of <see cref="DelegateCommand{T}"/>.
    /// </summary>
    /// <param name="execute">Delegate to execute when Execute is called on
    /// the command. This can be null to just hook up a CanExecute delegate.</param>
    /// <remarks><seealso cref="CanExecute"/> will always return true.</remarks>
    public RelayCommand(Action<T> execute) : this(execute, null) {
    }

    /// <summary>
    /// Creates a new command.
    /// </summary>
    /// <param name="execute">The execution logic.</param>
    /// <param name="canExecute">The execution status logic.</param>
    public RelayCommand(Action<T> execute, Predicate<T> canExecute) {
      _execute = execute ?? throw new ArgumentNullException("execute");
      _canExecute = canExecute ?? (_ => true);
    }

    #region ICommand Members

    ///<summary>
    ///Defines the method that determines whether the command can execute in its current state.
    ///</summary>
    ///<param name="parameter">Data used by the command. If the command does not require
    ///data to be passed, this object can be set to null.</param>
    ///<returns>
    ///true if this command can be executed; otherwise, false.
    ///</returns>
    public bool CanExecute(object parameter) {
      return _canExecute(NullToDefault(parameter));
    }

    ///<summary>
    ///Occurs when changes occur that affect whether or not the command should execute.
    ///</summary>
    public event EventHandler CanExecuteChanged;

    ///<summary>
    ///Defines the method to be called when the command is invoked.
    ///</summary>
    ///<param name="parameter">Data used by the command. If the command does not
    ///require data to be passed, this object can be set to <see langword="null" />.</param>
    public void Execute(object parameter) {
      _execute(NullToDefault(parameter));
    }

    #endregion

    public void NotifyCanExecute() {
      CanExecuteChanged.Invoke(null, null);
    }
  }
}
