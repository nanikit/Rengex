using Nanikit.Ehnd;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Rengex.Translator {
  public class ForkTranslator : ITranslator {
    readonly int PoolSize;
    readonly Task ManagerTask;
    readonly List<Task> Workers = new List<Task>();
    readonly List<ITranslator> Translators = new List<ITranslator>();
    readonly SimpleBufferBlock<Job> Jobs = new SimpleBufferBlock<Job>();
    CancellationTokenSource Cancel = new CancellationTokenSource();

    public ForkTranslator(int poolSize, ITranslator basis) {
      PoolSize = Math.Max(1, poolSize);
      Translators.Add(basis);
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
    private async Task Worker(ITranslator translator, Job job) {
      try {
        string res = await translator.Translate(job.Source).ConfigureAwait(false);
        job.Client.TrySetResult(res);
      }
      catch (Exception e) {
        if (e is EhndNotFoundException) {
          job.Client.TrySetException(e);
          return;
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
      foreach (ITranslator translator in Translators) {
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
      _ = await Task.WhenAny(dispatch).ConfigureAwait(false);
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
      var abort = Task.Delay(TimeSpan.FromDays(10), Cancel.Token);
      var seats = Task.WhenAny(Workers);
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
          Cancel.Dispose();
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
}
