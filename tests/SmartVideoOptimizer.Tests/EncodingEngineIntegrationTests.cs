using SmartVideoOptimizer.Core.Planning;
using SmartVideoOptimizer.Media.Encoding;
using SmartVideoOptimizer.Media.Probing;
using Xunit;

namespace SmartVideoOptimizer.Tests;

public class EncodingEngineIntegrationTests
{
    private static string FindFixturePath(string relativePath)
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;

            if (File.Exists(Path.Combine(dir.FullName, "SmartVideoOptimizer.slnx")))
            {
                var fromRoot = Path.Combine(dir.FullName, relativePath);
                if (File.Exists(fromRoot)) return fromRoot;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate fixture file {relativePath}");
    }

    [Fact]
    public async Task ExecutePlanAsync_RealVideoEncode_ProducesValidOutputFile()
    {
        var sourcePath = FindFixturePath(Path.Combine("tests", "fixtures", "synthetic_sample.mkv"));
        var outputPath = Path.Combine(Path.GetDirectoryName(sourcePath)!, "encoded_test_output.mp4");

        if (File.Exists(outputPath)) File.Delete(outputPath);

        var probe = new MediaProbe();
        var asset = await probe.ProbeAsync(sourcePath);

        var plan = new EncodingPlan
        {
            InputPath = sourcePath,
            OutputPath = outputPath,
            TempOutputPath = "",
            Duration = asset.Duration,
            VideoEncoder = "libx264",
            RateControl = RateControlMode.CRF,
            CrfValue = 28,
            Preset = "ultrafast",
            TargetWidth = 1280,
            TargetHeight = 720
        };

        var engine = new EncodingEngine();
        var progressReports = new List<double>();
        var progress = new Progress<Core.Domain.EncodingProgress>(p => progressReports.Add(p.Percentage));

        var finalPath = await engine.ExecutePlanAsync(plan, progress);

        Assert.True(File.Exists(finalPath));
        var fi = new FileInfo(finalPath);
        Assert.True(fi.Length > 0, "Output video file must not be empty.");

        // Probe the encoded output to verify stream validity
        var encodedAsset = await probe.ProbeAsync(finalPath);
        Assert.NotNull(encodedAsset.PrimaryVideo);
        Assert.Equal("h264", encodedAsset.PrimaryVideo.Codec);
        Assert.Equal(1280, encodedAsset.PrimaryVideo.Width);
        Assert.Equal(720, encodedAsset.PrimaryVideo.Height);

        // Clean up test output
        try { File.Delete(finalPath); } catch { /* ignore */ }
    }
}
