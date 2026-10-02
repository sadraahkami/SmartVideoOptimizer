using System.Diagnostics;
using System.Text;
using SmartVideoOptimizer.Core.Domain;
using SmartVideoOptimizer.Core.Interfaces;

namespace SmartVideoOptimizer.Media.Probing;

public sealed class MediaProbe : IMediaProbe
{
    private readonly string? _customFfprobePath;

    public MediaProbe(string? customFfprobePath = null)
    {
        _customFfprobePath = customFfprobePath;
    }

    public async Task<MediaAsset> ProbeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Input video file was not found: {filePath}", filePath);

        var ffprobePath = FfprobeLocator.FindFfprobe(_customFfprobePath);

        var startInfo = new ProcessStartInfo
        {
            FileName = ffprobePath,
            Arguments = $"-v quiet -print_format json -show_format -show_streams -show_chapters \"{filePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) errorBuilder.AppendLine(e.Data);
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start ffprobe process at {ffprobePath}");
        }

        process.BeginOutputReadLine();
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
            var err = errorBuilder.ToString();
            throw new InvalidOperationException(
                $"ffprobe exited with error code {process.ExitCode}: {(string.IsNullOrWhiteSpace(err) ? "Unknown error" : err.Trim())}");
        }

        var json = outputBuilder.ToString();
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("ffprobe returned empty output.");
        }

        return FfprobeParser.ParseJson(json, filePath);
    }
}
