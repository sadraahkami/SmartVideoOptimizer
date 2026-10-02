namespace SmartVideoOptimizer.Core.Domain;

public sealed record EncodingProgress
{
    public double Percentage { get; init; }
    public TimeSpan CurrentTime { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public double Fps { get; init; }
    public double SpeedMultiplier { get; init; }
    public double CurrentBitrateKbps { get; init; }
    public long ProcessedBytes { get; init; }
    public TimeSpan? EstimatedRemaining { get; init; }

    public string HumanReadableSpeed => $"{SpeedMultiplier:F1}x";
    public string HumanReadableEta => EstimatedRemaining.HasValue
        ? $"{(int)EstimatedRemaining.Value.TotalMinutes:D2}:{EstimatedRemaining.Value.Seconds:D2}"
        : "--:--";
}
