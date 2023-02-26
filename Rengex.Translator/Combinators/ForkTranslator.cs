namespace Rengex.Translator {
  using Nanikit.Ehnd;
  using System;
  using System.Collections.Generic;
  using System.Threading;
  using System.Threading.Tasks;
  using System.Threading.Tasks.Dataflow;

  public class ForkTranslator : ITranslator {
    private readonly int _poolSize;
    private readonly Task _managerTask;
    private readonly List<Task> _workers = new();
    private readonly List<ITranslator> _translators = new();
    private readonly BufferBlock<Job> _jobs = new();
    private readonly CancellationTokenSource _cancel = new();
    private bool _isDisposed;
    private DateTime _lastSpawnTime;

    public ForkTranslator(int poolSize, ITranslator basis) {
      _poolSize = Math.Max(1, poolSize);
      _translators.Add(basis);
      _lastSpawnTime = DateTime.Now;
      _managerTask = Task.Run(() => Manager());
    }

    public Task<string> Translate(string source) {
      var job = new Job(source);
      _ = _jobs.Post(job);
      return job.Client.Task;
    }

    /// <summary>
    /// Monitor translation completion at most 3 trials.
    /// </summary>
    private async Task Worker(ITranslator translator, Job job) {
      try {
        string res = await translator.Translate(job.Source).ConfigureAwait(false);
        _ = job.Client.TrySetResult(res);
      }
      catch (Exception exception) {
        if (exception is EhndNotFoundException) {
          _ = job.Client.TrySetException(exception);
          return;
        }
        if (job.RetryCount < 3) {
          job.RetryCount++;
          _ = _jobs.Post(job);
        }
        else {
          _ = job.Client.TrySetException(exception);
          throw;
        }
      }
    }

    private async Task Manager() {
      while (!_cancel?.IsCancellationRequested ?? false) {
        ReflectPoolSize();
        var job = await GetJobOrDefault().ConfigureAwait(false);
        if (job == null) {
          break;
        }
        await ScheduleAndWaitFreeWorker(job).ConfigureAwait(false);
      }
      foreach (var translator in _translators) {
        translator.Dispose();
      }
    }

    private void ReflectPoolSize() {
      if (_translators.Count == _poolSize) {
        return;
      }
      else if (_translators.Count < _poolSize) {
        for (int i = 1; i < _poolSize; i++) {
          _translators.Add(new ParentForkTranslator(_cancel!.Token));
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

    private async Task<Job?> GetJobOrDefault() {
      var dispatch = _jobs.ReceiveAsync(_cancel!.Token);
      _ = await Task.WhenAny(dispatch).ConfigureAwait(false);
      if (_cancel?.IsCancellationRequested ?? true) {
        return null;
      }
      var job = await dispatch.ConfigureAwait(false);
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
      var fin = await Task.WhenAny(abort, seats).ConfigureAwait(false);
      if (fin == abort) {
        return;
      }

      var vacant = await seats.ConfigureAwait(false);
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
        if (!_isDisposed) {
          _isDisposed = true;
          _cancel.Cancel();
          _cancel.Dispose();
        }
      }
    }

    private class Job {
      public Job(string source) {
        Source = source;
      }

      public TaskCompletionSource<string> Client = new();
      public string Source;
      public int RetryCount;
    }
  }
}
