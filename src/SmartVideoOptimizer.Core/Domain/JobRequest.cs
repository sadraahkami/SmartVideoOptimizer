namespace SmartVideoOptimizer.Core.Domain;

public sealed record StreamSelection
{
    public StreamSelectionMode Mode { get; init; } = StreamSelectionMode.KeepAll;
    public IReadOnlyList<int> SelectedAudioStreamIndices { get; init; } = [];
    public IReadOnlyList<int> SelectedSubtitleStreamIndices { get; init; } = [];
}

public sealed record JobRequest
{
    public required string InputPath { get; init; }
    public string? OutputPath { get; init; }
    public required CompressionGoal Goal { get; init; }
    public long? TargetSizeBytes { get; init; }
    public ResolutionPolicy Resolution { get; init; } = ResolutionPolicy.KeepOriginal;
    public int? CustomWidth { get; init; }
    public int? CustomHeight { get; init; }
    public EncodingPriority Priority { get; init; } = EncodingPriority.Balanced;
    public HardwarePolicy Hardware { get; init; } = HardwarePolicy.Auto;
    public StreamSelection Streams { get; init; } = new();
    public bool NeverExceedTarget { get; init; } = true;
    public double SafetyMarginPercent { get; init; } = 3.5;
}
