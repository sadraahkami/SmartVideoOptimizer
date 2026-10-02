namespace SmartVideoOptimizer.Core.Domain;

public sealed record ColorInfo
{
    public string ColorSpace { get; init; } = string.Empty;
    public string ColorRange { get; init; } = string.Empty;
    public string ColorPrimaries { get; init; } = string.Empty;
    public string ColorTransfer { get; init; } = string.Empty;
    public int BitDepth { get; init; } = 8;
    public bool Is10Bit => BitDepth >= 10;
    public bool IsHdr => HdrType != HdrType.None;
    public HdrType HdrType { get; init; } = HdrType.None;
}

public sealed record ChapterInfo
{
    public long Id { get; init; }
    public double StartTimeSeconds { get; init; }
    public double EndTimeSeconds { get; init; }
    public string Title { get; init; } = string.Empty;
}

public sealed record ContainerInfo
{
    public string FormatName { get; init; } = string.Empty;
    public string FormatLongName { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public TimeSpan Duration { get; init; }
    public long? OverallBitrate { get; init; }
    public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();
}
