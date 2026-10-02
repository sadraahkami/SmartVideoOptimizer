using SmartVideoOptimizer.Core.Domain;

namespace SmartVideoOptimizer.Core.Interfaces;

public interface IMediaProbe
{
    Task<MediaAsset> ProbeAsync(string filePath, CancellationToken cancellationToken = default);
}
