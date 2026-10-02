namespace SmartVideoOptimizer.Core.Domain;

public enum CompressionGoal
{
    ExactSize,
    BestQuality,
    SmartCompress
}

public enum EncodingPriority
{
    Balanced,
    Speed,
    BestCompression,
    Manual
}

public enum HardwarePolicy
{
    Auto,
    PreferGpu,
    CpuOnly
}

public enum ResolutionPolicy
{
    KeepOriginal,
    Scale4K,
    Scale1440p,
    Scale1080p,
    Scale720p,
    Scale480p,
    Custom
}

public enum StreamSelectionMode
{
    KeepAll,
    FirstOnly,
    Custom
}

public enum HdrType
{
    None,
    Hdr10,
    Hdr10Plus,
    DolbyVision,
    Hlg
}

public enum QualityRisk
{
    Minimal,
    Low,
    Moderate,
    High,
    Severe
}

public enum JobState
{
    Created,
    Analyzing,
    Ready,
    Queued,
    Preparing,
    Encoding,
    Validating,
    Completed,
    Paused,
    Cancelled,
    Failed,
    Retrying
}
