using SmartVideoOptimizer.Core.Domain;
using SmartVideoOptimizer.Core.Quality;
using Xunit;

namespace SmartVideoOptimizer.Tests;

public class QualityGuardTests
{
    [Fact]
    public void AssessRisk_HighBitrateModernCodec_ReturnsMinimalRisk()
    {
        // 1080p 30fps with 6000 kbps HEVC -> very high quality
        var report = QualityGuard.AssessRisk(
            videoBitrateKbps: 6000,
            width: 1920,
            height: 1080,
            fps: 30.0,
            duration: TimeSpan.FromMinutes(5),
            codec: "libx265");

        Assert.Equal(QualityRisk.Minimal, report.Risk);
        Assert.False(report.IsDownscaleRecommended);
        Assert.NotEmpty(report.RecommendationPersian);
    }

    [Fact]
    public void AssessRisk_ExtremelyLowBitrate4K_ReturnsSevereRiskAndSuggestsDownscale()
    {
        // 4K 60fps with only 500 kbps -> catastrophic artifacts!
        var report = QualityGuard.AssessRisk(
            videoBitrateKbps: 500,
            width: 3840,
            height: 2160,
            fps: 60.0,
            duration: TimeSpan.FromMinutes(10),
            codec: "libx264");

        Assert.Equal(QualityRisk.Severe, report.Risk);
        Assert.True(report.IsDownscaleRecommended);
        Assert.NotNull(report.SuggestedResolution);
    }
}
