using SmartVideoOptimizer.Core.Domain;

namespace SmartVideoOptimizer.Core.Planning;

public static class QualityPlanner
{
    public static EncodingPlan CreatePlan(
        JobRequest request,
        MediaAsset asset,
        HardwareCapabilities? hardwareCaps = null,
        string? customOutputPath = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(asset);

        string encoder;
        int crf;
        string preset;
        string pixFmt = "yuv420p";

        var preferGpu = request.Hardware == HardwarePolicy.PreferGpu ||
                        (request.Hardware == HardwarePolicy.Auto && request.Priority == EncodingPriority.Speed);

        if (preferGpu && hardwareCaps?.HasNvencHevc == true)
        {
            encoder = "hevc_nvenc";
            crf = 24;
            preset = "p5";
        }
        else if (preferGpu && hardwareCaps?.HasQsvHevc == true)
        {
            encoder = "hevc_qsv";
            crf = 24;
            preset = "medium";
        }
        else if (request.Priority == EncodingPriority.BestCompression)
        {
            encoder = "libx265";
            crf = 22;
            preset = "slow";
            pixFmt = "yuv420p10le"; // 10-bit reduces gradient banding
        }
        else if (request.Priority == EncodingPriority.Speed)
        {
            encoder = "libx264";
            crf = 21;
            preset = "fast";
        }
        else // Balanced
        {
            encoder = "libx265";
            crf = 23;
            preset = "medium";
        }

        var (targetWidth, targetHeight) = DetermineResolution(request.Resolution, asset.PrimaryVideo);

        var audioPlans = asset.AudioStreams.Select(a => new AudioPlan
        {
            SourceIndex = a.Index,
            Action = StreamAction.Encode,
            Codec = "aac",
            BitrateKbps = a.Channels >= 6 ? 256 : 160,
            Channels = a.Channels,
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

        var outputPath = customOutputPath ?? request.OutputPath ?? GetDefaultOutputPath(request.InputPath);
        var outputDir = Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? string.Empty;
        var fileName = Path.GetFileName(outputPath);
        var tempOutputPath = Path.Combine(outputDir, $".{fileName}.processing_{Guid.NewGuid():N}{Path.GetExtension(outputPath)}");

        return new EncodingPlan
        {
            InputPath = request.InputPath,
            OutputPath = outputPath,
            TempOutputPath = tempOutputPath,
            Duration = asset.Duration,
            VideoEncoder = encoder,
            RateControl = RateControlMode.CRF,
            CrfValue = crf,
            Preset = preset,
            PixelFormat = pixFmt,
            TargetWidth = targetWidth,
            TargetHeight = targetHeight,
            AudioPlans = audioPlans,
            SubtitlePlans = subtitlePlans,
            PreserveHdr = asset.PrimaryVideo?.IsHdr ?? false,
            ColorPrimaries = asset.PrimaryVideo?.Color.ColorPrimaries,
            ColorTransfer = asset.PrimaryVideo?.Color.ColorTransfer,
            ColorSpace = asset.PrimaryVideo?.Color.ColorSpace,
            ColorRange = asset.PrimaryVideo?.Color.ColorRange,
            Rotation = asset.PrimaryVideo?.Rotation,
            Container = Path.GetExtension(outputPath).TrimStart('.').ToLowerInvariant()
        };
    }

    private static (int? width, int? height) DetermineResolution(ResolutionPolicy policy, VideoStream? primaryVideo)
    {
        if (primaryVideo == null) return (null, null);

        var origW = primaryVideo.Width;
        var origH = primaryVideo.Height;
        if (origW <= 0 || origH <= 0) return (null, null);

        return policy switch
        {
            ResolutionPolicy.Scale4K => ScaleToMax(origW, origH, 3840, 2160),
            ResolutionPolicy.Scale1440p => ScaleToMax(origW, origH, 2560, 1440),
            ResolutionPolicy.Scale1080p => ScaleToMax(origW, origH, 1920, 1080),
            ResolutionPolicy.Scale720p => ScaleToMax(origW, origH, 1280, 720),
            ResolutionPolicy.Scale480p => ScaleToMax(origW, origH, 854, 480),
            _ => (null, null)
        };
    }

    private static (int width, int height) ScaleToMax(int origW, int origH, int maxW, int maxH)
    {
        if (origW <= maxW && origH <= maxH) return (origW, origH);
        var ratio = Math.Min((double)maxW / origW, (double)maxH / origH);
        return ((int)Math.Round(origW * ratio / 2.0) * 2, (int)Math.Round(origH * ratio / 2.0) * 2);
    }

    private static string GetDefaultOutputPath(string inputPath)
    {
        var dir = Path.GetDirectoryName(inputPath) ?? string.Empty;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath);
        return Path.Combine(dir, $"{nameWithoutExt}_hq{ext}");
    }
}
