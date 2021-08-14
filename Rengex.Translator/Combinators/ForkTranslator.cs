using Nanikit.Ehnd;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Rengex.Translator {
  public class ForkTranslator : ITranslator {
    private readonly int _poolSize;
    private readonly Task _managerTask;
    private readonly List<Task> _workers = new List<Task>();
    private readonly List<ITranslator> _translators = new List<ITranslator>();
    private readonly SimpleBufferBlock<Job> _jobs = new SimpleBufferBlock<Job>();
    private CancellationTokenSource? _cancel = new CancellationTokenSource();

    public ForkTranslator(int poolSize, ITranslator basis) {
      _poolSize = Math.Max(1, poolSize);
      _translators.Add(basis);
      _managerTask = Task.Run(() => Manager());
    }

    public Task<string> Translate(string source) {
      var job = new Job(source);
      _jobs.Enqueue(job);
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
      catch (Exception exception) {
        if (exception is EhndNotFoundException) {
          job.Client.TrySetException(exception);
          return;
        }
        if (job.RetryCount < 3) {
          job.RetryCount++;
          _jobs.Enqueue(job);
        }
        else {
          job.Client.TrySetException(exception);
          throw;
        }
      }
    }

    private async Task Manager() {
      while (!_cancel?.IsCancellationRequested ?? false) {
        ReflectPoolSize();
        Job job = await GetJobOrDefault().ConfigureAwait(false);
        if (job == null) {
          break;
        }
        await ScheduleAndWaitFreeWorker(job).ConfigureAwait(false);
      }
      foreach (ITranslator translator in _translators) {
        translator.Dispose();
      }
    }

    private void ReflectPoolSize() {
      if (_translators.Count == _poolSize) {
        return;
      }
      else if (_translators.Count < _poolSize) {
        for (int i = 1; i < _poolSize; i++) {
          _translators.Add(new ParentForkTranslator());
        }
      }
      else if (_translators.Count > _poolSize) {
        int pool = Math.Max(1, _poolSize);
        _translators.RemoveRange(pool, _translators.Count - pool);
        if (_workers.Count > _poolSize) {
          _workers.RemoveRange(_poolSize, _workers.Count - _poolSize);
        }
      }
    }

    private async Task<Job> GetJobOrDefault() {
      Task<Job> dispatch = _jobs.ReceiveAsync(_cancel!.Token);
      _ = await Task.WhenAny(dispatch).ConfigureAwait(false);
      if (_cancel?.IsCancellationRequested ?? true) {
        return null;
      }
      Job job = await dispatch.ConfigureAwait(false);
      return job;
    }

    private async Task ScheduleAndWaitFreeWorker(Job job) {
      if (ScheduleAtCompleted(job) || ScheduleWithMoreWorker(job)) {
        return;
      }
      await ScheduleAfterCompletion(job).ConfigureAwait(false);
    }

    private bool ScheduleWithMoreWorker(Job job) {
      if (_workers.Count < _translators.Count) {
        _workers.Add(Worker(_translators[_workers.Count], job));
        return true;
      }
      return false;
    }

    private async Task ScheduleAfterCompletion(Job job) {
      var abort = Task.Delay(TimeSpan.FromDays(10), _cancel!.Token);
      var seats = Task.WhenAny(_workers);
      Task fin = await Task.WhenAny(abort, seats).ConfigureAwait(false);
      if (fin == abort) {
        return;
      }

      Task vacant = await seats.ConfigureAwait(false);
      int endedIdx = _workers.IndexOf(vacant);
      _workers[endedIdx] = Worker(_translators[endedIdx], job);
    }

    private bool ScheduleAtCompleted(Job job) {
      int endedIdx = _workers.FindIndex(x => x.IsCompleted);
      if (endedIdx != -1) {
        _workers[endedIdx] = Worker(_translators[endedIdx], job);
        return true;
      }
      return false;
    }

    public void Dispose() {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) {
      if (disposing) {
        if (_cancel != null) {
          _cancel.Cancel();
          _cancel.Dispose();
          _cancel = null;
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
