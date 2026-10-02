using SmartVideoOptimizer.Core.Domain;

namespace SmartVideoOptimizer.Core.Planning;

public static class ExactSizePlanner
{
    public static EncodingPlan CreatePlan(JobRequest request, MediaAsset asset, string? customOutputPath = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(asset);

        if (!request.TargetSizeBytes.HasValue || request.TargetSizeBytes.Value <= 0)
        {
            throw new ArgumentException("TargetSizeBytes must be specified and greater than 0 for ExactSize goal.", nameof(request));
        }

        var durationSec = asset.Duration.TotalSeconds;
        if (durationSec <= 0.1)
        {
            durationSec = 1.0; // fallback minimum
        }

        var targetBytes = request.TargetSizeBytes.Value;

        // Dynamic Safety Margin calculation
        // Hardware encoders (NVENC/QSV) have higher bitrate fluctuation than 2-pass CPU
        var marginPercent = request.SafetyMarginPercent;
        if (request.Hardware == HardwarePolicy.PreferGpu)
        {
            marginPercent = Math.Max(marginPercent, 5.5);
        }
        else if (request.Priority == EncodingPriority.BestCompression)
        {
            marginPercent = Math.Max(marginPercent, 3.0);
        }

        if (request.NeverExceedTarget)
        {
            marginPercent += 1.5; // Additional safeguard buffer
        }

        var internalTargetBytes = (long)(targetBytes * (1.0 - (marginPercent / 100.0)));
        var totalAvailableBitrateBps = (internalTargetBytes * 8.0) / durationSec;

        // Determine audio plans and calculate audio bitrate consumption
        var audioPlans = PlanAudioStreams(request, asset);
        var totalAudioBitrateBps = audioPlans.Sum(a => a.BitrateKbps * 1000.0);

        // Container muxing overhead and subtitle allowance
        var subtitlePlans = PlanSubtitleStreams(request, asset);
        var subtitleAllowanceBps = subtitlePlans.Count * 24_000.0; // ~24 kbps per stream
        var containerOverheadBps = Math.Max(32_000.0, totalAvailableBitrateBps * 0.015); // ~1.5%

        var videoBudgetBps = totalAvailableBitrateBps - totalAudioBitrateBps - subtitleAllowanceBps - containerOverheadBps;
        if (videoBudgetBps < 64_000.0) // 64 kbps minimum floor
        {
            videoBudgetBps = 64_000.0;
        }

        var videoBitrateKbps = (int)Math.Round(videoBudgetBps / 1000.0);

        // Determine encoder and rate control mode
        string encoder;
        RateControlMode rateControl;
        string preset;

        if (request.Hardware == HardwarePolicy.CpuOnly || request.Priority == EncodingPriority.BestCompression || request.NeverExceedTarget)
        {
            // Software 2-Pass VBR provides the highest accuracy
            encoder = request.Priority == EncodingPriority.BestCompression ? "libx265" : "libx264";
            rateControl = RateControlMode.VBR_TwoPass;
            preset = request.Priority == EncodingPriority.BestCompression ? "slow" : "medium";
        }
        else
        {
            // Speed priority with hardware or 1-pass
            encoder = "libx264";
            rateControl = RateControlMode.VBR_OnePass;
            preset = "fast";
        }

        // Determine scaling resolution
        var (targetWidth, targetHeight) = DetermineResolution(request.Resolution, asset.PrimaryVideo);

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
            RateControl = rateControl,
            TargetVideoBitrateKbps = videoBitrateKbps,
            Preset = preset,
            TargetWidth = targetWidth,
            TargetHeight = targetHeight,
            AudioPlans = audioPlans,
            SubtitlePlans = subtitlePlans,
            ExpectedSizeBytes = internalTargetBytes,
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
            _ => (null, null) // Keep original
        };
    }

    private static (int width, int height) ScaleToMax(int origW, int origH, int maxW, int maxH)
    {
        if (origW <= maxW && origH <= maxH)
            return (origW, origH); // Don't upscale

        var ratio = Math.Min((double)maxW / origW, (double)maxH / origH);
        var w = (int)Math.Round(origW * ratio / 2.0) * 2;
        var h = (int)Math.Round(origH * ratio / 2.0) * 2;
        return (w, h);
    }

    private static List<AudioPlan> PlanAudioStreams(JobRequest request, MediaAsset asset)
    {
        var plans = new List<AudioPlan>();
        if (asset.AudioStreams.Count == 0) return plans;

        if (request.Streams.Mode == StreamSelectionMode.FirstOnly)
        {
            var first = asset.PrimaryAudio ?? asset.AudioStreams[0];
            plans.Add(new AudioPlan
            {
                SourceIndex = first.Index,
                Action = StreamAction.Encode,
                Codec = "aac",
                BitrateKbps = 128,
                Channels = Math.Min(first.Channels, 2),
                Language = first.Language,
                Title = first.Title
            });
            return plans;
        }

        // Keep all or custom selected
        foreach (var a in asset.AudioStreams)
        {
            if (request.Streams.Mode == StreamSelectionMode.Custom &&
                !request.Streams.SelectedAudioStreamIndices.Contains(a.Index))
            {
                continue;
            }

            var kbps = a.Channels >= 6 ? 256 : 128;
            plans.Add(new AudioPlan
            {
                SourceIndex = a.Index,
                Action = StreamAction.Encode,
                Codec = "aac",
                BitrateKbps = kbps,
                Channels = a.Channels,
                Language = a.Language,
                Title = a.Title
            });
        }

        return plans;
    }

    private static List<SubtitlePlan> PlanSubtitleStreams(JobRequest request, MediaAsset asset)
    {
        var plans = new List<SubtitlePlan>();
        foreach (var s in asset.SubtitleStreams)
        {
            if (request.Streams.Mode == StreamSelectionMode.Custom &&
                !request.Streams.SelectedSubtitleStreamIndices.Contains(s.Index))
            {
                continue;
            }

            plans.Add(new SubtitlePlan
            {
                SourceIndex = s.Index,
                Action = StreamAction.Copy,
                Language = s.Language,
                Title = s.Title
            });
        }

        return plans;
    }

    private static string GetDefaultOutputPath(string inputPath)
    {
        var dir = Path.GetDirectoryName(inputPath) ?? string.Empty;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath);
        return Path.Combine(dir, $"{nameWithoutExt}_optimized{ext}");
    }
}
