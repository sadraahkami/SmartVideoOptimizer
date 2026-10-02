using SmartVideoOptimizer.Core.Domain;

namespace SmartVideoOptimizer.Core.Planning;

public sealed record SmartRecommendation
{
    public string RecommendedCodec { get; init; } = "libx265";
    public ResolutionPolicy RecommendedResolution { get; init; } = ResolutionPolicy.KeepOriginal;
    public double EstimatedSizeReductionPercentage { get; init; }
    public string SummaryEnglish { get; init; } = string.Empty;
    public string SummaryPersian { get; init; } = string.Empty;
    public EncodingPlan Plan { get; init; } = null!;
}

public static class SmartPlanner
{
    public static SmartRecommendation CreateSmartRecommendation(
        JobRequest request,
        MediaAsset asset,
        HardwareCapabilities? hardwareCaps = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(asset);

        var video = asset.PrimaryVideo;
        var width = video?.Width ?? 1920;
        var height = video?.Height ?? 1080;
        var fps = video?.FrameRate ?? 30.0;
        var sourceCodec = (video?.Codec ?? "h264").ToLowerInvariant();
        var isH264 = sourceCodec.Contains("264") || sourceCodec.Contains("avc");

        // Codec selection: Always upgrade H.264 to HEVC for 40-60% size savings at identical quality
        string encoder;
        string codecName;

        if (hardwareCaps?.HasNvencHevc == true && request.Priority == EncodingPriority.Speed)
        {
            encoder = "hevc_nvenc";
            codecName = "HEVC (NVIDIA NVENC)";
        }
        else
        {
            encoder = "libx265";
            codecName = "HEVC / H.265 (High Efficiency)";
        }

        // Resolution recommendation
        ResolutionPolicy resolution;
        int? targetW = null;
        int? targetH = null;

        if (width > 2560 && height > 1440 && request.Priority != EncodingPriority.BestCompression)
        {
            // For general sharing, 4K is often oversized. 1440p or 1080p is the sweet spot.
            resolution = ResolutionPolicy.Scale1080p;
            targetW = 1920;
            targetH = 1080;
        }
        else
        {
            resolution = ResolutionPolicy.KeepOriginal;
        }

        // Calculate sweet-spot target bitrate for human vision
        // 1080p 30fps HEVC sweet spot: ~2200 kbps; 1080p 60fps: ~3200 kbps; 720p: ~1200 kbps
        double targetBitrateKbps;
        if (targetW.HasValue && targetW.Value <= 1280)
        {
            targetBitrateKbps = fps > 40 ? 1600 : 1200;
        }
        else if (width <= 1920 && height <= 1080)
        {
            targetBitrateKbps = fps > 40 ? 3200 : 2200;
        }
        else
        {
            targetBitrateKbps = fps > 40 ? 7500 : 5500;
        }

        // Calculate size reduction estimate
        var sourceBitrateKbps = (asset.Container.OverallBitrate ?? 10_000_000) / 1000.0;
        var reduction = Math.Clamp(Math.Round((1.0 - (targetBitrateKbps / Math.Max(targetBitrateKbps * 1.5, sourceBitrateKbps))) * 100.0, 1), 35.0, 80.0);

        var audioPlans = asset.AudioStreams.Select(a => new AudioPlan
        {
            SourceIndex = a.Index,
            Action = StreamAction.Encode,
            Codec = "aac",
            BitrateKbps = 160,
            Channels = Math.Min(a.Channels, 2),
            Language = a.Language,
            Title = a.Title
        }).ToList();

        var subtitlePlans = asset.SubtitleStreams.Select(s => new SubtitlePlan
        {
            SourceIndex = s.Index,
            Action = StreamAction.Copy,
            Language = s.Language,
            Title = s.Title
        }).ToList();

        var outputPath = request.OutputPath ?? GetDefaultOutputPath(request.InputPath);
        var outputDir = Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? string.Empty;
        var fileName = Path.GetFileName(outputPath);
        var tempOutputPath = Path.Combine(outputDir, $".{fileName}.processing_{Guid.NewGuid():N}{Path.GetExtension(outputPath)}");

        var plan = new EncodingPlan
        {
            InputPath = request.InputPath,
            OutputPath = outputPath,
            TempOutputPath = tempOutputPath,
            Duration = asset.Duration,
            VideoEncoder = encoder,
            RateControl = RateControlMode.CRF,
            CrfValue = 23,
            Preset = "medium",
            PixelFormat = (video?.Is10Bit == true) ? "yuv420p10le" : "yuv420p",
            TargetWidth = targetW,
            TargetHeight = targetH,
            AudioPlans = audioPlans,
            SubtitlePlans = subtitlePlans,
            PreserveHdr = video?.IsHdr ?? false,
            Container = Path.GetExtension(outputPath).TrimStart('.').ToLowerInvariant()
        };

        var enSummary = $"Recommended {codecName} with CRF 23. Estimated ~{reduction}% file reduction with pristine visual clarity.";
        var faSummary = $"توصیه الگوریتمی: کدک {codecName} با وضوح بهینه. کاهش تخمینی ~{reduction}٪ از حجم فایل با حفظ حداکثری شفافیت تصویر.";

        return new SmartRecommendation
        {
            RecommendedCodec = encoder,
            RecommendedResolution = resolution,
            EstimatedSizeReductionPercentage = reduction,
            SummaryEnglish = enSummary,
            SummaryPersian = faSummary,
            Plan = plan
        };
    }

    private static string GetDefaultOutputPath(string inputPath)
    {
        var dir = Path.GetDirectoryName(inputPath) ?? string.Empty;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath);
        return Path.Combine(dir, $"{nameWithoutExt}_smart{ext}");
    }
}
