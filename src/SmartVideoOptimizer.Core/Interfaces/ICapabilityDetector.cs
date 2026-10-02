using SmartVideoOptimizer.Core.Domain;

namespace SmartVideoOptimizer.Core.Interfaces;

public interface ICapabilityDetector
{
    Task<HardwareCapabilities> DetectCapabilitiesAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
}
