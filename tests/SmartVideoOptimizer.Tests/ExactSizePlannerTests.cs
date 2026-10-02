using SmartVideoOptimizer.Core.Domain;
using SmartVideoOptimizer.Core.Planning;
using Xunit;

namespace SmartVideoOptimizer.Tests;

public class ExactSizePlannerTests
{
    private static MediaAsset CreateTestAsset(int width = 1920, int height = 1080, double durationSec = 60.0)
    {
        return new MediaAsset
        {
            FilePath = @"D:\Videos\sample.mp4",
            Container = new ContainerInfo
            {
                FormatName = "mp4",
                Duration = TimeSpan.FromSeconds(durationSec),
                FileSizeBytes = 500_000_000
            },
            VideoStreams =
            [
                new VideoStream
                {
                    Index = 0,
                    Codec = "h264",
                    Width = width,
                    Height = height,
                    FrameRate = 30.0
                }
            ],
            AudioStreams =
            [
                new AudioStream
                {
                    Index = 1,
                    Codec = "aac",
                    Channels = 2,
                    Language = "eng"
                }
            ]
        };
    }

    [Fact]
    public void CreatePlan_ExactSize100MB_AppliesSafetyMarginAndDeductsAudio()
    {
        var asset = CreateTestAsset(1920, 1080, 60.0);
        const long targetBytes = 100 * 1024 * 1024; // 100 MB

        var request = new JobRequest
        {
            InputPath = @"D:\Videos\sample.mp4",
            Goal = CompressionGoal.ExactSize,
            TargetSizeBytes = targetBytes,
            NeverExceedTarget = true,
            Priority = EncodingPriority.Balanced
        };

        var plan = ExactSizePlanner.CreatePlan(request, asset);

        Assert.Equal(RateControlMode.VBR_TwoPass, plan.RateControl);
        Assert.NotNull(plan.TargetVideoBitrateKbps);
        Assert.True(plan.TargetVideoBitrateKbps.Value > 500);

        // Verify that expected size is strictly below the user's 100 MB target
        Assert.True(plan.ExpectedSizeBytes < targetBytes);
        Assert.True(plan.ExpectedSizeBytes > targetBytes * 0.90); // within safety margin
    }

    [Fact]
    public void CreatePlan_ResolutionPolicy_ScalesDownCorrectly()
    {
        var asset = CreateTestAsset(3840, 2160, 120.0); // 4K Source
        var request = new JobRequest
        {
            InputPath = @"D:\Videos\sample.mp4",
            Goal = CompressionGoal.ExactSize,
            TargetSizeBytes = 200 * 1024 * 1024,
            Resolution = ResolutionPolicy.Scale1080p
        };

        var plan = ExactSizePlanner.CreatePlan(request, asset);

        Assert.Equal(1920, plan.TargetWidth);
        Assert.Equal(1080, plan.TargetHeight);
    }
}
