using SmartVideoOptimizer.Media.Probing;
using Xunit;

namespace SmartVideoOptimizer.Tests;

public class MediaProbeIntegrationTests
{
    private static string FindFixturePath(string relativePath)
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;

            if (File.Exists(Path.Combine(dir.FullName, "SmartVideoOptimizer.sln")))
            {
                var fromRoot = Path.Combine(dir.FullName, relativePath);
                if (File.Exists(fromRoot)) return fromRoot;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate fixture file {relativePath}");
    }

    [Fact]
    public async Task ProbeAsync_RealMkvFile_ProbesAccurately()
    {
        var testFilePath = FindFixturePath(Path.Combine("tests", "fixtures", "synthetic_sample.mkv"));
        Assert.True(File.Exists(testFilePath), $"Test file not found at {testFilePath}");

        var probe = new MediaProbe();
        var asset = await probe.ProbeAsync(testFilePath);

        Assert.NotNull(asset);
        Assert.Equal("synthetic_sample.mkv", asset.FileName);
        Assert.True(asset.FileSizeBytes > 0);
        Assert.Equal(2.0, asset.Duration.TotalSeconds, precision: 1);

        // Check video stream
        Assert.NotNull(asset.PrimaryVideo);
        Assert.Equal("h264", asset.PrimaryVideo.Codec);
        Assert.Equal(1920, asset.PrimaryVideo.Width);
        Assert.Equal(1080, asset.PrimaryVideo.Height);
        Assert.Equal(30.0, asset.PrimaryVideo.FrameRate);

        // Check audio stream
        Assert.NotNull(asset.PrimaryAudio);
        Assert.Equal("aac", asset.PrimaryAudio.Codec);
        Assert.Equal("fas", asset.PrimaryAudio.Language);
        Assert.Equal("PersianDub", asset.PrimaryAudio.Title);
    }
}
