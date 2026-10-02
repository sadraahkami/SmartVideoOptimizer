using SmartVideoOptimizer.Platform.Windows.Hardware;
using Xunit;

namespace SmartVideoOptimizer.Tests;

public class CapabilityDetectorTests : IDisposable
{
    private readonly string _testCachePath;

    public CapabilityDetectorTests()
    {
        _testCachePath = Path.Combine(Path.GetTempPath(), $"caps_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        try { if (File.Exists(_testCachePath)) File.Delete(_testCachePath); } catch { /* ignore */ }
    }

    [Fact]
    public async Task DetectCapabilitiesAsync_ExecutesAndCachesResults()
    {
        var detector = new CapabilityDetector(customCachePath: _testCachePath);
        var caps = await detector.DetectCapabilitiesAsync(forceRefresh: true);

        Assert.NotNull(caps);
        Assert.True(caps.HasCpuH264);
        Assert.True(caps.HasCpuHevc);
        Assert.NotEmpty(caps.DetectedGpuName);

        // Verify JSON cache file was created
        Assert.True(File.Exists(_testCachePath));

        // Second call should return from cache
        var cachedCaps = await detector.DetectCapabilitiesAsync(forceRefresh: false);
        Assert.Equal(caps.DetectedGpuName, cachedCaps.DetectedGpuName);
    }
}
