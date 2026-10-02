using System.Collections.Concurrent;
using SmartVideoOptimizer.Core.Domain;
using SmartVideoOptimizer.Core.Interfaces;

namespace SmartVideoOptimizer.Core.Queue;

public sealed class QueueManager
{
    private readonly IQueueRepository _repository;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runningTokens = new();
    private readonly List<JobItem> _jobs = [];
    private readonly object _lock = new();

    public event Action<JobItem>? JobStateChanged;
    public event Action<Guid, double>? JobProgressChanged;

    public QueueManager(IQueueRepository repository)
    {
        _repository = repository;
    }

    public IReadOnlyList<JobItem> Jobs
    {
        get
        {
            lock (_lock) return [.. _jobs];
        }
    }

    public async Task InitializeAndRecoverAsync(CancellationToken cancellationToken = default)
    {
        await _repository.InitializeAsync(cancellationToken).ConfigureAwait(false);
        var allJobs = await _repository.GetAllJobsAsync(cancellationToken).ConfigureAwait(false);

        lock (_lock)
        {
            _jobs.Clear();
            _jobs.AddRange(allJobs);
        }
    }

    public async Task<JobItem> EnqueueAsync(JobItem job, CancellationToken cancellationToken = default)
    {
        var queuedJob = job with { State = JobState.Queued };
        await _repository.InsertJobAsync(queuedJob, cancellationToken).ConfigureAwait(false);

        lock (_lock)
        {
            _jobs.Insert(0, queuedJob);
        }

        JobStateChanged?.Invoke(queuedJob);
        return queuedJob;
    }

    public async Task UpdateProgressAsync(Guid id, double progress, CancellationToken cancellationToken = default)
    {
        JobItem? updated = null;
        lock (_lock)
        {
            var idx = _jobs.FindIndex(j => j.Id == id);
            if (idx >= 0)
            {
                _jobs[idx] = _jobs[idx] with { ProgressPercentage = progress, State = JobState.Encoding };
                updated = _jobs[idx];
            }
        }

        if (updated != null)
        {
            await _repository.UpdateJobStateAsync(id, JobState.Encoding, progress, null, cancellationToken).ConfigureAwait(false);
            JobProgressChanged?.Invoke(id, progress);
        }
    }

    public async Task CompleteJobAsync(Guid id, long outputSizeBytes, CancellationToken cancellationToken = default)
    {
        JobItem? updated = null;
        lock (_lock)
        {
            var idx = _jobs.FindIndex(j => j.Id == id);
            if (idx >= 0)
            {
                _jobs[idx] = _jobs[idx] with
                {
                    State = JobState.Completed,
                    ProgressPercentage = 100.0,
                    OutputSizeBytes = outputSizeBytes,
                    CompletedAt = DateTime.UtcNow
                };
                updated = _jobs[idx];
            }
        }

        if (updated != null)
        {
            await _repository.CompleteJobAsync(id, outputSizeBytes, cancellationToken).ConfigureAwait(false);
            JobStateChanged?.Invoke(updated);
        }
    }

    public async Task FailJobAsync(Guid id, string errorMessage, CancellationToken cancellationToken = default)
    {
        JobItem? updated = null;
        lock (_lock)
        {
            var idx = _jobs.FindIndex(j => j.Id == id);
            if (idx >= 0)
            {
                _jobs[idx] = _jobs[idx] with
                {
                    State = JobState.Failed,
                    ErrorMessage = errorMessage
                };
                updated = _jobs[idx];
            }
        }

        if (updated != null)
        {
            await _repository.UpdateJobStateAsync(id, JobState.Failed, 0, errorMessage, cancellationToken).ConfigureAwait(false);
            JobStateChanged?.Invoke(updated);
        }
    }

    public async Task CancelJobAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_runningTokens.TryRemove(id, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        JobItem? updated = null;
        lock (_lock)
        {
            var idx = _jobs.FindIndex(j => j.Id == id);
            if (idx >= 0)
            {
                _jobs[idx] = _jobs[idx] with { State = JobState.Cancelled };
                updated = _jobs[idx];
            }
        }

        if (updated != null)
        {
            await _repository.UpdateJobStateAsync(id, JobState.Cancelled, 0, "Cancelled by user", cancellationToken).ConfigureAwait(false);
            JobStateChanged?.Invoke(updated);
        }
    }
}
