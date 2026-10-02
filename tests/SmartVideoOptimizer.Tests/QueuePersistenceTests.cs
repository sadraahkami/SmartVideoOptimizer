using SmartVideoOptimizer.Core.Domain;
using SmartVideoOptimizer.Core.Queue;
using SmartVideoOptimizer.Platform.Windows.Storage;
using Xunit;

namespace SmartVideoOptimizer.Tests;

public class QueuePersistenceTests : IDisposable
{
    private readonly string _testDbPath;

    public QueuePersistenceTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_queue_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
        }
        catch
        {
            // ignored
        }
    }

    [Fact]
    public async Task SqliteQueueRepository_LifecycleAndCrashRecovery_WorksAccurately()
    {
        var repo = new SqliteQueueRepository(_testDbPath);
        await repo.InitializeAsync();

        var job1 = new JobItem
        {
            SourcePath = @"D:\Videos\clip1.mp4",
            OutputPath = @"D:\Videos\clip1_out.mp4",
            Goal = CompressionGoal.ExactSize,
            State = JobState.Queued,
            OriginalSizeBytes = 100_000_000
        };

        var job2 = new JobItem
        {
            SourcePath = @"D:\Videos\clip2.mp4",
            OutputPath = @"D:\Videos\clip2_out.mp4",
            Goal = CompressionGoal.BestQuality,
            State = JobState.Encoding,
            ProgressPercentage = 45.0,
            OriginalSizeBytes = 200_000_000
        };

        await repo.InsertJobAsync(job1);
        await repo.InsertJobAsync(job2);

        // Crash recovery test: both job1 and job2 were unfinished
        var unfinished = await repo.GetUnfinishedJobsAsync();
        Assert.Equal(2, unfinished.Count);

        // Complete job1
        await repo.CompleteJobAsync(job1.Id, 50_000_000);

        var updatedUnfinished = await repo.GetUnfinishedJobsAsync();
        Assert.Single(updatedUnfinished);
        Assert.Equal(job2.Id, updatedUnfinished[0].Id);

        var all = await repo.GetAllJobsAsync();
        Assert.Equal(2, all.Count);
        var completed = all.First(j => j.Id == job1.Id);
        Assert.Equal(JobState.Completed, completed.State);
        Assert.Equal(50_000_000, completed.OutputSizeBytes);
        Assert.Equal(50.0, completed.CompressionRatio);
    }

    [Fact]
    public async Task QueueManager_EnqueueAndEvents_DispatchesStateChanges()
    {
        var repo = new SqliteQueueRepository(_testDbPath);
        var manager = new QueueManager(repo);
        await manager.InitializeAndRecoverAsync();

        var stateChanges = new List<JobState>();
        manager.JobStateChanged += j => stateChanges.Add(j.State);

        var job = new JobItem
        {
            SourcePath = @"D:\Videos\video.mkv",
            OutputPath = @"D:\Videos\video_out.mkv",
            OriginalSizeBytes = 50_000_000
        };

        var enqueued = await manager.EnqueueAsync(job);
        Assert.Equal(JobState.Queued, enqueued.State);

        await manager.UpdateProgressAsync(enqueued.Id, 25.0);
        await manager.CompleteJobAsync(enqueued.Id, 25_000_000);

        Assert.Contains(JobState.Queued, stateChanges);
        Assert.Contains(JobState.Completed, stateChanges);
    }
}
