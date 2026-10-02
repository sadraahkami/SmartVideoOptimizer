using SmartVideoOptimizer.Core.Domain;

namespace SmartVideoOptimizer.Core.Interfaces;

public interface IQueueRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task InsertJobAsync(JobItem job, CancellationToken cancellationToken = default);
    Task UpdateJobStateAsync(Guid id, JobState state, double progress = 0.0, string? errorMessage = null, CancellationToken cancellationToken = default);
    Task CompleteJobAsync(Guid id, long outputSizeBytes, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobItem>> GetUnfinishedJobsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobItem>> GetAllJobsAsync(CancellationToken cancellationToken = default);
    Task DeleteJobAsync(Guid id, CancellationToken cancellationToken = default);
}
