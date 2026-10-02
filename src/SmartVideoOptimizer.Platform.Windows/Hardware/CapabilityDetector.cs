using System.Diagnostics;
using System.Text.Json;
using SmartVideoOptimizer.Core.Domain;
using SmartVideoOptimizer.Core.Interfaces;

namespace SmartVideoOptimizer.Platform.Windows.Hardware;

public sealed class CapabilityDetector : ICapabilityDetector
{
    private readonly string _ffmpegPath;
    private readonly string _cacheFilePath;
    private HardwareCapabilities? _cached;

    public CapabilityDetector(string? customFfmpegPath = null, string? customCachePath = null)
    {
        _ffmpegPath = customFfmpegPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "bin", "ffmpeg.exe");
        if (!File.Exists(_ffmpegPath))
        {
            _ffmpegPath = @"A:\AGENT\github\SmartVideoOptimizer\tools\bin\ffmpeg.exe";
        }

        _cacheFilePath = customCachePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hardware_capabilities.json");
    }

    public async Task<HardwareCapabilities> DetectCapabilitiesAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (!forceRefresh && _cached != null)
        {
            return _cached;
        }

        if (!forceRefresh && File.Exists(_cacheFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_cacheFilePath, cancellationToken).ConfigureAwait(false);
                var cached = JsonSerializer.Deserialize<HardwareCapabilities>(json);
                if (cached != null && (DateTime.UtcNow - cached.LastDetectedAt).TotalDays < 7)
                {
                    _cached = cached;
                    return _cached;
                }
            }
            catch
            {
                // Cache corrupt or outdated, re-detect
            }
        }

        var nvencH264 = await TestEncoderAsync("h264_nvenc", cancellationToken).ConfigureAwait(false);
        var nvencHevc = await TestEncoderAsync("hevc_nvenc", cancellationToken).ConfigureAwait(false);
        var nvencAv1 = await TestEncoderAsync("av1_nvenc", cancellationToken).ConfigureAwait(false);

        var qsvH264 = await TestEncoderAsync("h264_qsv", cancellationToken).ConfigureAwait(false);
        var qsvHevc = await TestEncoderAsync("hevc_qsv", cancellationToken).ConfigureAwait(false);
        var qsvAv1 = await TestEncoderAsync("av1_qsv", cancellationToken).ConfigureAwait(false);

        var amfH264 = await TestEncoderAsync("h264_amf", cancellationToken).ConfigureAwait(false);
        var amfHevc = await TestEncoderAsync("hevc_amf", cancellationToken).ConfigureAwait(false);

        var gpuName = DetectGpuName();

        var capabilities = new HardwareCapabilities
        {
            HasNvencH264 = nvencH264,
            HasNvencHevc = nvencHevc,
            HasNvencAv1 = nvencAv1,
            HasQsvH264 = qsvH264,
            HasQsvHevc = qsvHevc,
            HasQsvAv1 = qsvAv1,
            HasAmfH264 = amfH264,
            HasAmfHevc = amfHevc,
            HasCpuH264 = true,
            HasCpuHevc = true,
            DetectedGpuName = gpuName,
            LastDetectedAt = DateTime.UtcNow
        };

        try
        {
            var serialized = JsonSerializer.Serialize(capabilities, new JsonSerializerOptions { WriteIndented = true });
            var dir = Path.GetDirectoryName(_cacheFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            await File.WriteAllTextAsync(_cacheFilePath, serialized, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Non-critical if cache fails to write
        }

        _cached = capabilities;
        return capabilities;
    }

    private async Task<bool> TestEncoderAsync(string encoderName, CancellationToken cancellationToken)
    {
        if (!File.Exists(_ffmpegPath)) return false;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = $"-y -hide_banner -f lavfi -i nullsrc=s=64x64:d=0.04 -c:v {encoderName} -f null NUL",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start()) return false;

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string DetectGpuName()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -Command \"(Get-CimInstance Win32_VideoController).Name\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p != null)
            {
                var outStr = p.StandardOutput.ReadToEnd();
                p.WaitForExit(2000);
                var lines = outStr.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                                  .Select(l => l.Trim())
                                  .Where(l => !string.IsNullOrEmpty(l))
                                  .ToList();

                if (lines.Count > 0)
                {
                    return string.Join(" / ", lines);
                }
            }
        }
        catch
        {
            // ignored
        }

        return "Standard Graphics Adapter";
    }
}
