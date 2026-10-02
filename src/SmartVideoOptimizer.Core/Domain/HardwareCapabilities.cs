namespace SmartVideoOptimizer.Core.Domain;

public sealed record HardwareCapabilities
{
    public bool HasNvencH264 { get; init; }
    public bool HasNvencHevc { get; init; }
    public bool HasNvencAv1 { get; init; }

    public bool HasQsvH264 { get; init; }
    public bool HasQsvHevc { get; init; }
    public bool HasQsvAv1 { get; init; }

    public bool HasAmfH264 { get; init; }
    public bool HasAmfHevc { get; init; }

    public bool HasCpuH264 { get; init; } = true;
    public bool HasCpuHevc { get; init; } = true;

    public string DetectedGpuName { get; init; } = "Generic / Software";
    public DateTime LastDetectedAt { get; init; } = DateTime.UtcNow;

    public bool HasAnyHardwareAcceleration =>
        HasNvencH264 || HasNvencHevc || HasNvencAv1 ||
        HasQsvH264 || HasQsvHevc || HasQsvAv1 ||
        HasAmfH264 || HasAmfHevc;

    public string BestH264Encoder =>
        HasNvencH264 ? "h264_nvenc" :
        HasQsvH264 ? "h264_qsv" :
        HasAmfH264 ? "h264_amf" : "libx264";

    public string BestHevcEncoder =>
        HasNvencHevc ? "hevc_nvenc" :
        HasQsvHevc ? "hevc_qsv" :
        HasAmfHevc ? "hevc_amf" : "libx265";
}
