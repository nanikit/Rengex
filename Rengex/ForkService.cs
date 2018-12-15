using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Rengex {

  public interface ITranslator : IDisposable {
    Task<string> Translate(string source);
  }

  public class ConcurrentTransChild {
    NamedPipeClientStream PipeClient;
    ITranslator Translator;

    public ConcurrentTransChild(string pipeName) {
      PipeClient = new NamedPipeClientStream(".", pipeName);
    }

    public async Task Serve() {
      await PipeClient.ConnectAsync().ConfigureAwait(false);
      try {
        if (Translator == null) {
          Translator = new ConcurrentTransSelf();
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

  public class ConcurrentTransParent : IDisposable, ITranslator {
    string PipeName;
    NamedPipeServerStream PipeServer;

    public ConcurrentTransParent(string pipeName = "asdf") {
      PipeServer = new NamedPipeServerStream(pipeName, PipeDirection.InOut, Environment.ProcessorCount + 2);
      PipeName = pipeName;
    }

    public async Task<string> Translate(string source) {
      await SendTranslationWork(source).ConfigureAwait(false);
      return await ReceiveTranslation().ConfigureAwait(false);
    }

    private async Task SendTranslationWork(string script) {
      if (!PipeServer.IsConnected) {
        await InitializeChild().ConfigureAwait(false);
      }
      await PipeServer.WriteObjAsync(script).ConfigureAwait(false);
    }

    private async Task InitializeChild() {
      string path = Process.GetCurrentProcess().MainModule.FileName;
      Process child = Process.Start(path, PipeName);
      Task connection = PipeServer.WaitForConnectionAsync();
      Task fin = await Task.WhenAny(connection, Task.Delay(5000)).ConfigureAwait(false);
      if (fin != connection) {
        if (!child.HasExited) {
          child.Kill();
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
        if (PipeServer != null) {
          if (PipeServer.IsConnected) {
            PipeServer.WriteObjAsync(false).Wait(5000);
          }
          PipeServer.Dispose();
          PipeServer = null;
        }
      }
    }
  }

  sealed class ConcurrentTransSelf : ITranslator {
    private static EzTransXp Instance;

    public ConcurrentTransSelf() {
      if (Instance != null) {
        return;
      }
      try {
        string cfgEzt = Properties.Settings.Default.EzTransDir;
        Instance = new EzTransXp(cfgEzt);
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

  class ConcurrentTranslator : ITranslator {
    Task ManagerTask;
    Task[] TranslationTasks;
    ITranslator[] Translators;
    MyBufferBlock<Job> Jobs = new MyBufferBlock<Job>();
    CancellationTokenSource Cancel = new CancellationTokenSource();

    public ConcurrentTranslator(int poolSize) {
      poolSize = Math.Max(1, poolSize);
      Translators = new ITranslator[poolSize];
      TranslationTasks = new Task[poolSize];
      ManagerTask = Task.Run(() => Manager());
    }

    private void ForkChild(int i) {
      var translator = new ConcurrentTransParent();
      Translators[i] = translator;
      TranslationTasks[i] = Task.Run(() => Worker(translator));
    }

    public Task<string> Translate(string source) {
      var job = new Job(source);
      Jobs.Enqueue(job);
      return job.Client.Task;
    }

    private async Task Worker(ITranslator translator) {
      Job job = null;
      try {
        while (true) {
          Task<Job> getJob = Jobs.ReceiveAsync(Cancel.Token);
          Task guard = await Task.WhenAny(getJob).ConfigureAwait(false);
          if (getJob.IsCanceled) {
            break;
          }
          job = await getJob.ConfigureAwait(false);
          string res = await translator.Translate(job.Source).ConfigureAwait(false);
          job.Client.TrySetResult(res);
          job = null;
        }
      }
      catch (Exception e) {
        if (job != null) {
          Jobs.Enqueue(job);
        }
        throw e;
      }
      finally {
        translator.Dispose();
      }
    }

    private async Task Manager() {
      Translators[0] = new ConcurrentTransSelf();
      TranslationTasks[0] = Task.Run(() => Worker(Translators[0]));
      for (int i = 1; i < TranslationTasks.Length; i++) {
        ForkChild(i);
      }
      while (!Cancel?.IsCancellationRequested ?? false) {
        Task t = await Task.WhenAny(TranslationTasks).ConfigureAwait(false);
        int idx = Array.IndexOf(TranslationTasks, t);
        if (TranslationTasks[idx].IsFaulted) {
          ForkChild(idx);
        }
      }
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
    }
  }
}
