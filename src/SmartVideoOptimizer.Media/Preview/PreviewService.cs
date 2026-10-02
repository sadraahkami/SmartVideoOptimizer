using SmartVideoOptimizer.Core.Planning;
using SmartVideoOptimizer.Media.Encoding;

namespace SmartVideoOptimizer.Media.Preview;

public sealed record PreviewResult
{
    public required string OriginalSamplePath { get; init; }
    public required string CompressedSamplePath { get; init; }
    public long OriginalSizeBytes { get; init; }
    public long CompressedSizeBytes { get; init; }
    public TimeSpan SampleDuration { get; init; }

    public double ReductionPercentage => OriginalSizeBytes > 0
        ? Math.Round((1.0 - ((double)CompressedSizeBytes / OriginalSizeBytes)) * 100.0, 1)
        : 0.0;
}

public sealed class PreviewService
{
    private readonly FFmpegProcessRunner _runner;

    public PreviewService(string? customFfmpegPath = null)
    {
        _runner = new FFmpegProcessRunner(customFfmpegPath);
    }

    public async Task<PreviewResult> GenerateQuickPreviewAsync(
        string sourcePath,
        EncodingPlan fullPlan,
        double sampleDurationSeconds = 10.0,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"Source video not found: {sourcePath}", sourcePath);

        var tempDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(fullPlan.OutputPath)) ?? Path.GetTempPath(), ".previews");
        if (!Directory.Exists(tempDir))
        {
            Directory.CreateDirectory(tempDir);
        }

        var sampleDuration = TimeSpan.FromSeconds(Math.Min(sampleDurationSeconds, fullPlan.Duration.TotalSeconds));
        // Start at 20% mark into the video, or 0 if very short
        var startOffsetSec = Math.Max(0.0, fullPlan.Duration.TotalSeconds * 0.2);

        var origSampleExt = Path.GetExtension(sourcePath);
        var compSampleExt = Path.GetExtension(fullPlan.OutputPath);

        var uid = Guid.NewGuid().ToString("N");
        var originalSamplePath = Path.Combine(tempDir, $"orig_sample_{uid}{origSampleExt}");
        var compressedSamplePath = Path.Combine(tempDir, $"comp_sample_{uid}{compSampleExt}");

        // 1. Extract 10-second original sample segment
        var origArgs = $"-y -hide_banner -ss {startOffsetSec:F2} -t {sampleDuration.TotalSeconds:F2} -i \"{sourcePath}\" -c copy \"{originalSamplePath}\"";
        await _runner.RunAsync(origArgs, sampleDuration, null, cancellationToken).ConfigureAwait(false);

        // 2. Encode 10-second preview sample with EncodingPlan settings
        var samplePlan = fullPlan with
        {
            InputPath = originalSamplePath,
            OutputPath = compressedSamplePath,
            TempOutputPath = compressedSamplePath,
            Duration = sampleDuration,
            RateControl = fullPlan.RateControl == RateControlMode.VBR_TwoPass ? RateControlMode.VBR_OnePass : fullPlan.RateControl
        };

        var compArgs = FFmpegCommandBuilder.BuildArguments(samplePlan);
        await _runner.RunAsync(compArgs, sampleDuration, null, cancellationToken).ConfigureAwait(false);

        var origSize = File.Exists(originalSamplePath) ? new FileInfo(originalSamplePath).Length : 0;
        var compSize = File.Exists(compressedSamplePath) ? new FileInfo(compressedSamplePath).Length : 0;

        return new PreviewResult
        {
            OriginalSamplePath = originalSamplePath,
            CompressedSamplePath = compressedSamplePath,
            OriginalSizeBytes = origSize,
            CompressedSizeBytes = compSize,
            SampleDuration = sampleDuration
        };
    }
}
