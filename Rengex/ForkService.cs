using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Rengex {

  public class ChildForkTranslator {
    int MsDelay;
    IJp2KrTranslator Translator;
    NamedPipeClientStream PipeClient;

    public ChildForkTranslator(int msDelay, string pipeName = ParentForkTranslator.DefaultPipeName) {
      MsDelay = msDelay;
      PipeClient = new NamedPipeClientStream(".", pipeName);
    }

    public async Task Serve() {
      await PipeClient.ConnectAsync(5000).ConfigureAwait(false);
      try {
        if (!PipeClient.IsConnected) {
          return;
        }
        if (Translator == null) {
          Translator = new SelfTranslator(MsDelay);
        }
        while (true) {
          object src = await PipeClient.ReadObjAsync<object>().ConfigureAwait(false);
          if (src is bool) {
            break;
          }
          string done = await Translator.Translate(src as string).ConfigureAwait(false);
          await PipeClient.WriteObjAsync(done).ConfigureAwait(false);
        }
      }
      finally {
        PipeClient.Dispose();
      }
    }
  }

  public class ParentForkTranslator : IJp2KrTranslator {
    public const string DefaultPipeName = "rengex_subtrans";

    /// <summary>
    /// Ehnd에서 크래시가 나는 경우가 있는데 견고한 방법을 찾지 못함.
    /// </summary>
    int MsInitDelay = 200;
    Process Child;
    string PipeName;
    NamedPipeServerStream PipeServer;

    public ParentForkTranslator(string pipeName = DefaultPipeName) {
      PipeName = pipeName;
    }

    public async Task<string> Translate(string source) {
      try {
        await SendTranslationWork(source).ConfigureAwait(false);
        return await ReceiveTranslation().ConfigureAwait(false);
      }
      catch (Exception e) {
        MsInitDelay += 500;
        throw e;
      }
    }

    private async Task SendTranslationWork(string script) {
      if (!PipeServer?.IsConnected ?? true) {
        await InitializeChild().ConfigureAwait(false);
      }
      await PipeServer.WriteObjAsync(script).ConfigureAwait(false);
    }

    private async Task InitializeChild() {
      string path = Process.GetCurrentProcess().MainModule.FileName;
      Dispose();
      PipeServer = new NamedPipeServerStream(PipeName, PipeDirection.InOut, Environment.ProcessorCount + 2);
      Child = Process.Start(path, MsInitDelay.ToString());
      Task connection = PipeServer.WaitForConnectionAsync();
      Task fin = await Task.WhenAny(connection, Task.Delay(5000)).ConfigureAwait(false);
      if (fin != connection) {
        if (!Child.HasExited) {
          Child.Kill();
        }
        throw new ApplicationException("서브 프로세스가 응답하지 않습니다.");
      }
    }

    private async Task<string> ReceiveTranslation() {
      return await PipeServer.ReadObjAsync<string>().ConfigureAwait(false);
    }

    public void Dispose() {
      Dispose(true);
    }

    protected virtual void Dispose(bool disposing) {
      if (disposing) {
        DisposePipe();
        DisposeChild();
      }
    }

    private void DisposePipe() {
      if (PipeServer != null) {
        if (PipeServer.IsConnected) {
          PipeServer.WriteObjAsync(false).Wait(5000);
        }
        PipeServer.Dispose();
        PipeServer = null;
      }
    }

    private void DisposeChild() {
      if (Child != null) {
        if (!Child.HasExited) {
          Child.Kill();
        }
        Child.Dispose();
      }
    }
  }

  sealed class SelfTranslator : IJp2KrTranslator {
    private static EzTransXp Instance;

    public SelfTranslator(int msDelay = 200) {
      if (Instance != null) {
        return;
      }
      try {
        string cfgEzt = Properties.Settings.Default.EzTransDir;
        Instance = new EzTransXp(cfgEzt, msDelay);
      }
      catch (Exception e) {
        Properties.Settings.Default.EzTransDir = null;
        throw e;
      }
    }

    public Task<string> Translate(string source) {
      return Task.Run(() => Instance.Translate(source));
    }

    public void Dispose() { }
  }

  class ForkTranslator : IJp2KrTranslator {
    int PoolSize;
    Task ManagerTask;
    List<Task> Workers = new List<Task>();
    List<IJp2KrTranslator> Translators = new List<IJp2KrTranslator>();
    MyBufferBlock<Job> Jobs = new MyBufferBlock<Job>();
    CancellationTokenSource Cancel = new CancellationTokenSource();

    public ForkTranslator(int poolSize) {
      PoolSize = Math.Max(1, poolSize);
      Translators.Add(new SelfTranslator());
      ManagerTask = Task.Run(() => Manager());
    }

    public Task<string> Translate(string source) {
      var job = new Job(source);
      Jobs.Enqueue(job);
      return job.Client.Task;
    }

    /// <summary>
    /// Monitor translation completion at most 3 trials.
    /// </summary>
    private async Task Worker(IJp2KrTranslator translator, Job job) {
      try {
        string res = await translator.Translate(job.Source).ConfigureAwait(false);
        job.Client.TrySetResult(res);
      }
      catch (Exception e) {
        if (e is EzTransNotFoundException) {
          Cancel.Cancel();
        }
        if (job.RetryCount < 3) {
          job.RetryCount++;
          Jobs.Enqueue(job);
        }
        else {
          job.Client.TrySetException(e);
          throw e;
        }
      }
    }

    private async Task Manager() {
      while (!Cancel?.IsCancellationRequested ?? false) {
        ReflectPoolSize();
        Job job = await GetJobOrDefault().ConfigureAwait(false);
        if (job == null) {
          break;
        }
        await Schedule(job).ConfigureAwait(false);
      }
      foreach (IJp2KrTranslator translator in Translators) {
        translator.Dispose();
      }
    }

    private void ReflectPoolSize() {
      if (Translators.Count == PoolSize) {
        return;
      }
      else if (Translators.Count < PoolSize) {
        for (int i = 1; i < PoolSize; i++) {
          Translators.Add(new ParentForkTranslator());
        }
      }
      else if (Translators.Count > PoolSize) {
        int pool = Math.Max(1, PoolSize);
        Translators.RemoveRange(pool, Translators.Count - pool);
        if (Workers.Count > PoolSize) {
          Workers.RemoveRange(PoolSize, Workers.Count - PoolSize);
        }
      }
    }

    private async Task<Job> GetJobOrDefault() {
      Task<Job> dispatch = Jobs.ReceiveAsync(Cancel.Token);
      Task t = await Task.WhenAny(dispatch).ConfigureAwait(false);
      if (Cancel?.IsCancellationRequested ?? true) {
        return null;
      }
      Job job = await dispatch.ConfigureAwait(false);
      return job;
    }

    private async Task Schedule(Job job) {
      if (ScheduleAtCompleted(job) || ScheduleWithMoreWorker(job)) {
        return;
      }
      await ScheduleAfterCompletion(job).ConfigureAwait(false);
    }

    private bool ScheduleWithMoreWorker(Job job) {
      if (Workers.Count < Translators.Count) {
        Workers.Add(Worker(Translators[Workers.Count], job));
        return true;
      }
      return false;
    }

    private async Task ScheduleAfterCompletion(Job job) {
      Task abort = Task.Delay(TimeSpan.FromDays(10), Cancel.Token);
      Task<Task> seats = Task.WhenAny(Workers);
      Task fin = await Task.WhenAny(abort, seats).ConfigureAwait(false);
      if (fin == abort) {
        return;
      }
      Task vacant = await seats.ConfigureAwait(false);
      int endedIdx = Workers.IndexOf(vacant);
      Workers[endedIdx] = Worker(Translators[endedIdx], job);
    }

    private bool ScheduleAtCompleted(Job job) {
      int endedIdx = Workers.FindIndex(x => x.IsCompleted);
      if (endedIdx != -1) {
        Workers[endedIdx] = Worker(Translators[endedIdx], job);
        return true;
      }
      return false;
    }

    public void Dispose() {
      Dispose(true);
    }

    protected virtual void Dispose(bool disposing) {
      if (disposing) {
        if (Cancel != null) {
          Cancel.Cancel();
          Cancel = null;
        }
      }
    }

    private class Job {
      public Job(string source) {
        Source = source;
      }

      public TaskCompletionSource<string> Client = new TaskCompletionSource<string>();
      public string Source;
      public int RetryCount;
    }
  }

  /// <summary>
  /// It provides split translation with progress information.
  /// </summary>
  /// <remarks>
  /// It's Dispose() doesn't dispose base translator.
  /// </remarks>
  public class SplitTranslater : IJp2KrTranslator {

    public interface IJp2KrLogger {
      void OnStart(int total);
      void OnProgress(int current);
    }

    private static IEnumerable<string> ChunkByLines(string source) {
      int startIdx = 0;
      int endIdx = 0;
      while (true) {
        if (endIdx >= source.Length) {
          yield return source.Substring(startIdx, endIdx - startIdx);
          break;
        }
        if (source[endIdx] == '\n') {
          int len = endIdx - startIdx + 1;
          if (len > 2000) {
            yield return source.Substring(startIdx, len);
            startIdx = endIdx + 1;
          }
        }
        endIdx++;
      }
    }

    private IJp2KrTranslator Backend;
    private IJp2KrLogger Logger;

    public SplitTranslater(IJp2KrTranslator translator, IJp2KrLogger progress) {
      Backend = translator;
      Logger = progress;
    }

    public Task<string> Translate(string source) {
      return Translate(source, Logger);
    }

    public async Task<string> Translate(string source, IJp2KrLogger progress) {
      List<string> splits = ChunkByLines(source).ToList();
      List<Task<string>> splitTasks = splits.Select(x => Backend.Translate(x)).ToList();
      var runnings = new List<Task<string>>(splitTasks);
      int translatedLength = 0;

      progress?.OnStart(source.Length);

      while (runnings.Count != 0) {
        Task<string> done = await Task.WhenAny(runnings).ConfigureAwait(false);
        runnings.Remove(done);

        int doneIdx = splitTasks.IndexOf(done);
        translatedLength += splits[doneIdx].Length;
        progress?.OnProgress(translatedLength);
      }

      return string.Join("", splitTasks.Select(x => x.Result));
    }

    /// <summary>
    /// It does nothing.
    /// </summary>
    public void Dispose() {
    }
  }
}
