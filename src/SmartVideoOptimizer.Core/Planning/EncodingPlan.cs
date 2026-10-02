namespace SmartVideoOptimizer.Core.Planning;

public enum RateControlMode
{
    CRF,
    VBR_TwoPass,
    VBR_OnePass,
    CBR
}

public enum StreamAction
{
    Copy,
    Encode,
    Drop
}

public sealed record AudioPlan
{
    public int SourceIndex { get; init; }
    public StreamAction Action { get; init; } = StreamAction.Copy;
    public string Codec { get; init; } = "aac";
    public int BitrateKbps { get; init; } = 160;
    public int? Channels { get; init; }
    public string? Language { get; init; }
    public string? Title { get; init; }
}

public sealed record SubtitlePlan
{
    public int SourceIndex { get; init; }
    public StreamAction Action { get; init; } = StreamAction.Copy;
    public string Codec { get; init; } = "copy";
    public string? Language { get; init; }
    public string? Title { get; init; }
}

public sealed record EncodingPlan
{
    public required string InputPath { get; init; }
    public required string OutputPath { get; init; }
    public required string TempOutputPath { get; init; }
    public TimeSpan Duration { get; init; }

    public string VideoEncoder { get; init; } = "libx264";
    public RateControlMode RateControl { get; init; } = RateControlMode.CRF;
    public int? TargetVideoBitrateKbps { get; init; }
    public int? CrfValue { get; init; } = 22;
    public string Preset { get; init; } = "medium";

    public int? TargetWidth { get; init; }
    public int? TargetHeight { get; init; }
    public double? TargetFps { get; init; }
    public string PixelFormat { get; init; } = "yuv420p";

    public IReadOnlyList<AudioPlan> AudioPlans { get; init; } = [];
    public IReadOnlyList<SubtitlePlan> SubtitlePlans { get; init; } = [];

    public int PassNumber { get; init; } = 0; // 0: single pass, 1: pass 1, 2: pass 2
    public string? PassLogFilePrefix { get; init; }

    public long? ExpectedSizeBytes { get; init; }
    public bool PreserveHdr { get; init; }
    public string? ColorPrimaries { get; init; }
    public string? ColorTransfer { get; init; }
    public string? ColorSpace { get; init; }
    public string? ColorRange { get; init; }
    public int? Rotation { get; init; }
    public string Container { get; init; } = "mkv";
    public IReadOnlyList<string> CustomArguments { get; init; } = [];
}
