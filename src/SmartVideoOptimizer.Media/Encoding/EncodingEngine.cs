using SmartVideoOptimizer.Core.Domain;
using SmartVideoOptimizer.Core.Planning;

namespace SmartVideoOptimizer.Media.Encoding;

public sealed class EncodingEngine
{
    private readonly FFmpegProcessRunner _runner;

    public EncodingEngine(string? customFfmpegPath = null)
    {
        _runner = new FFmpegProcessRunner(customFfmpegPath);
    }

    public async Task<string> ExecutePlanAsync(
        EncodingPlan plan,
        IProgress<EncodingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(plan.InputPath) || !File.Exists(plan.InputPath))
            throw new FileNotFoundException($"Input video does not exist: {plan.InputPath}", plan.InputPath);

        var outputDir = Path.GetDirectoryName(Path.GetFullPath(plan.OutputPath));
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var tempOutput = plan.TempOutputPath;
        if (string.IsNullOrWhiteSpace(tempOutput))
        {
            var fileName = Path.GetFileName(plan.OutputPath);
            tempOutput = Path.Combine(outputDir ?? string.Empty, $".{fileName}.processing_{Guid.NewGuid():N}{Path.GetExtension(plan.OutputPath)}");
        }

        var effectivePlan = plan with { TempOutputPath = tempOutput };

        try
        {
            if (effectivePlan.RateControl == RateControlMode.VBR_TwoPass)
            {
                var passLogPrefix = Path.Combine(outputDir ?? string.Empty, $"ffmpeg2pass_{Guid.NewGuid():N}");
                var pass1Plan = effectivePlan with
                {
                    PassNumber = 1,
                    PassLogFilePrefix = passLogPrefix
                };

                var pass1Args = FFmpegCommandBuilder.BuildArguments(pass1Plan);
                await _runner.RunAsync(pass1Args, effectivePlan.Duration, progress, cancellationToken).ConfigureAwait(false);

                var pass2Plan = effectivePlan with
                {
                    PassNumber = 2,
                    PassLogFilePrefix = passLogPrefix
                };

                var pass2Args = FFmpegCommandBuilder.BuildArguments(pass2Plan);
                await _runner.RunAsync(pass2Args, effectivePlan.Duration, progress, cancellationToken).ConfigureAwait(false);

                // Clean up pass log files
                CleanupPassLogs(passLogPrefix);
            }
            else
            {
                var args = FFmpegCommandBuilder.BuildArguments(effectivePlan);
                await _runner.RunAsync(args, effectivePlan.Duration, progress, cancellationToken).ConfigureAwait(false);
            }

            // Atomic rename from .processing temp file to final output file
            if (File.Exists(effectivePlan.OutputPath))
            {
                File.Delete(effectivePlan.OutputPath);
            }

            File.Move(tempOutput, effectivePlan.OutputPath);
            return effectivePlan.OutputPath;
        }
        catch
        {
            // Clean up incomplete temporary file on cancellation or failure
            try
            {
                if (File.Exists(tempOutput))
                {
                    File.Delete(tempOutput);
                }
            }
            catch
            {
                // ignored
            }
            throw;
        }
    }

    private static void CleanupPassLogs(string prefix)
    {
        try
        {
            var dir = Path.GetDirectoryName(prefix);
            var filePrefix = Path.GetFileName(prefix);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                foreach (var f in Directory.GetFiles(dir, $"{filePrefix}*"))
                {
                    try { File.Delete(f); } catch { /* ignore */ }
                }
            }
        }
        catch
        {
            // ignored
        }
    }
}
