using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SmartVideoOptimizer.Core.Domain;

namespace SmartVideoOptimizer.Media.Encoding;

public sealed partial class FFmpegProcessRunner
{
    [GeneratedRegex(@"time=(\d{2}):(\d{2}):(\d{2}\.\d+)", RegexOptions.Compiled)]
    private static partial Regex TimeRegex();

    [GeneratedRegex(@"fps=\s*(\d+(?:\.\d+)?)", RegexOptions.Compiled)]
    private static partial Regex FpsRegex();

    [GeneratedRegex(@"speed=\s*(\d+(?:\.\d+)?)x", RegexOptions.Compiled)]
    private static partial Regex SpeedRegex();

    [GeneratedRegex(@"bitrate=\s*(\d+(?:\.\d+)?)kbits/s", RegexOptions.Compiled)]
    private static partial Regex BitrateRegex();

    [GeneratedRegex(@"size=\s*(\d+)KiB", RegexOptions.Compiled)]
    private static partial Regex SizeRegex();

    private readonly string _ffmpegPath;

    public FFmpegProcessRunner(string? customFfmpegPath = null)
    {
        _ffmpegPath = FFmpegLocator.FindFFmpeg(customFfmpegPath);
    }

    public async Task RunAsync(
        string arguments,
        TimeSpan totalDuration,
        IProgress<EncodingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };
        var errorLogBuilder = new StringBuilder();

        process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;

            errorLogBuilder.AppendLine(e.Data);
            if (progress != null)
            {
                var parsedProgress = TryParseProgress(e.Data, totalDuration);
                if (parsedProgress != null)
                {
                    progress.Report(parsedProgress);
                }
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to launch ffmpeg at {_ffmpegPath}");
        }

        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw;
        }

        if (process.ExitCode != 0)
        {
            var logs = errorLogBuilder.ToString();
            var lastLines = logs.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                                .TakeLast(10);
            throw new InvalidOperationException(
                $"FFmpeg execution failed with exit code {process.ExitCode}:{Environment.NewLine}{string.Join(Environment.NewLine, lastLines)}");
        }
    }

    public static EncodingProgress? TryParseProgress(string line, TimeSpan totalDuration)
    {
        var timeMatch = TimeRegex().Match(line);
        if (!timeMatch.Success) return null;

        if (!int.TryParse(timeMatch.Groups[1].Value, out var hours) ||
            !int.TryParse(timeMatch.Groups[2].Value, out var minutes) ||
            !double.TryParse(timeMatch.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            return null;
        }

        var currentTime = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);

        double fps = 0;
        var fpsMatch = FpsRegex().Match(line);
        if (fpsMatch.Success && double.TryParse(fpsMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedFps))
        {
            fps = parsedFps;
        }

        double speed = 1.0;
        var speedMatch = SpeedRegex().Match(line);
        if (speedMatch.Success && double.TryParse(speedMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedSpeed))
        {
            speed = parsedSpeed;
        }

        double bitrate = 0;
        var brMatch = BitrateRegex().Match(line);
        if (brMatch.Success && double.TryParse(brMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedBr))
        {
            bitrate = parsedBr;
        }

        long sizeBytes = 0;
        var sizeMatch = SizeRegex().Match(line);
        if (sizeMatch.Success && long.TryParse(sizeMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var kib))
        {
            sizeBytes = kib * 1024;
        }

        double percent = 0;
        TimeSpan? eta = null;

        if (totalDuration.TotalSeconds > 0)
        {
            percent = Math.Clamp((currentTime.TotalSeconds / totalDuration.TotalSeconds) * 100.0, 0.0, 100.0);
            if (speed > 0.01)
            {
                var remainingSeconds = Math.Max(0, (totalDuration.TotalSeconds - currentTime.TotalSeconds) / speed);
                eta = TimeSpan.FromSeconds(remainingSeconds);
            }
        }

        return new EncodingProgress
        {
            Percentage = Math.Round(percent, 1),
            CurrentTime = currentTime,
            TotalDuration = totalDuration,
            Fps = fps,
            SpeedMultiplier = speed,
            CurrentBitrateKbps = bitrate,
            ProcessedBytes = sizeBytes,
            EstimatedRemaining = eta
        };
    }
}
