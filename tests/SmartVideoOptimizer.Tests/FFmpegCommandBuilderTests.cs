using SmartVideoOptimizer.Core.Planning;
using SmartVideoOptimizer.Media.Encoding;
using Xunit;

namespace SmartVideoOptimizer.Tests;

public class FFmpegCommandBuilderTests
{
    [Fact]
    public void BuildArguments_CrfMode_BuildsCorrectCommand()
    {
        var plan = new EncodingPlan
        {
            InputPath = @"D:\Videos\input.mp4",
            OutputPath = @"D:\Videos\output.mp4",
            TempOutputPath = @"D:\Videos\.output.processing.mp4",
            Duration = TimeSpan.FromSeconds(60),
            VideoEncoder = "libx264",
            RateControl = RateControlMode.CRF,
            CrfValue = 20,
            Preset = "slow",
            TargetWidth = 1920,
            TargetHeight = 1080
        };

        var args = FFmpegCommandBuilder.BuildArguments(plan);

        Assert.Contains("-i \"D:\\Videos\\input.mp4\"", args);
        Assert.Contains("-c:v libx264", args);
        Assert.Contains("-crf 20", args);
        Assert.Contains("-preset slow", args);
        Assert.Contains("scale=1920:1080:flags=lanczos", args);
        Assert.Contains("\"D:\\Videos\\.output.processing.mp4\"", args);
    }

    [Fact]
    public void BuildArguments_TwoPassMode_Pass1AndPass2Differ()
    {
        var plan = new EncodingPlan
        {
            InputPath = @"D:\Videos\input.mkv",
            OutputPath = @"D:\Videos\output.mkv",
            TempOutputPath = @"D:\Videos\.output.processing.mkv",
            Duration = TimeSpan.FromMinutes(2),
            VideoEncoder = "libx265",
            RateControl = RateControlMode.VBR_TwoPass,
            TargetVideoBitrateKbps = 3500,
            PassLogFilePrefix = @"D:\Videos\passlog"
        };

        var pass1Args = FFmpegCommandBuilder.BuildArguments(plan with { PassNumber = 1 });
        var pass2Args = FFmpegCommandBuilder.BuildArguments(plan with { PassNumber = 2 });

        Assert.Contains("-pass 1", pass1Args);
        Assert.Contains("-f null NUL", pass1Args);
        Assert.Contains("-b:v 3500k", pass1Args);

        Assert.Contains("-pass 2", pass2Args);
        Assert.Contains("-b:v 3500k", pass2Args);
        Assert.Contains("\"D:\\Videos\\.output.processing.mkv\"", pass2Args);
    }

    [Fact]
    public void TryParseProgress_ValidStderrLine_ExtractsMetrics()
    {
        var line = "frame=  150 fps= 45.2 q=28.0 size=    1536KiB time=00:00:05.00 bitrate=2516.6kbits/s speed=1.51x";
        var total = TimeSpan.FromSeconds(10);

        var progress = FFmpegProcessRunner.TryParseProgress(line, total);

        Assert.NotNull(progress);
        Assert.Equal(50.0, progress.Percentage);
        Assert.Equal(TimeSpan.FromSeconds(5), progress.CurrentTime);
        Assert.Equal(45.2, progress.Fps);
        Assert.Equal(1.51, progress.SpeedMultiplier);
        Assert.Equal(2516.6, progress.CurrentBitrateKbps);
        Assert.Equal(1536 * 1024, progress.ProcessedBytes);
    }

    [Fact]
    public void BuildArguments_Hdr10AndRotation_EmitsHdrFlagsAndRotationMetadata()
    {
        var plan = new EncodingPlan
        {
            InputPath = @"D:\Videos\hdr.mkv",
            OutputPath = @"D:\Videos\hdr_out.mkv",
            TempOutputPath = @"D:\Videos\.hdr_out.processing.mkv",
            Duration = TimeSpan.FromMinutes(1),
            VideoEncoder = "libx265",
            PixelFormat = "yuv420p10le",
            PreserveHdr = true,
            ColorPrimaries = "bt2020",
            ColorTransfer = "smpte2084",
            ColorSpace = "bt2020nc",
            ColorRange = "tv",
            Rotation = 90
        };

        var args = FFmpegCommandBuilder.BuildArguments(plan);

        Assert.Contains("-pix_fmt yuv420p10le", args);
        Assert.Contains("-color_primaries bt2020", args);
        Assert.Contains("-color_trc smpte2084", args);
        Assert.Contains("-colorspace bt2020nc", args);
        Assert.Contains("-color_range tv", args);
        Assert.Contains("-metadata:s:v:0 rotate=90", args);
    }
}
