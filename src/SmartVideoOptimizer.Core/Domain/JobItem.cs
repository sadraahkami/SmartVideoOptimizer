namespace SmartVideoOptimizer.Core.Domain;

public sealed record JobItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string SourcePath { get; init; }
    public required string OutputPath { get; init; }
    public CompressionGoal Goal { get; init; } = CompressionGoal.ExactSize;
    public JobState State { get; init; } = JobState.Created;
    public double ProgressPercentage { get; init; }
    public long OriginalSizeBytes { get; init; }
    public long? OutputSizeBytes { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; init; }
    public int RetryCount { get; init; }

    public string FileName => Path.GetFileName(SourcePath);

    public double CompressionRatio => OriginalSizeBytes > 0 && OutputSizeBytes.HasValue
        ? Math.Round((1.0 - ((double)OutputSizeBytes.Value / OriginalSizeBytes)) * 100.0, 1)
        : 0.0;
}
