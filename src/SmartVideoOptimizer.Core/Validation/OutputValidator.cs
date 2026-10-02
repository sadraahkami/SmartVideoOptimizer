using SmartVideoOptimizer.Core.Domain;
using SmartVideoOptimizer.Core.Interfaces;
using SmartVideoOptimizer.Core.Planning;

namespace SmartVideoOptimizer.Core.Validation;

public sealed record ValidationResult
{
    public bool IsValid { get; init; }
    public long ActualSizeBytes { get; init; }
    public long? TargetSizeBytes { get; init; }
    public bool SizeExceeded { get; init; }
    public double SizeDeltaPercent { get; init; }
    public string? ErrorMessage { get; init; }

    public string HumanReadableActualSize
    {
        get
        {
            const double mb = 1024.0 * 1024.0;
            return $"{ActualSizeBytes / mb:F1} MB";
        }
    }
}

public sealed class OutputValidator
{
    private readonly IMediaProbe _probe;

    public OutputValidator(IMediaProbe probe)
    {
        _probe = probe;
    }

    public async Task<ValidationResult> ValidateOutputAsync(
        string outputFilePath,
        EncodingPlan plan,
        JobRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(outputFilePath))
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Output file was not created: {outputFilePath}"
            };
        }

        var fileInfo = new FileInfo(outputFilePath);
        var actualSize = fileInfo.Length;
        if (actualSize == 0)
        {
            return new ValidationResult
            {
                IsValid = false,
                ActualSizeBytes = 0,
                ErrorMessage = "Output file has zero bytes."
            };
        }

        // Check target size constraint
        var sizeExceeded = false;
        double sizeDelta = 0.0;
        if (request.Goal == CompressionGoal.ExactSize && request.TargetSizeBytes.HasValue)
        {
            var target = request.TargetSizeBytes.Value;
            if (actualSize > target)
            {
                sizeExceeded = true;
                sizeDelta = Math.Round(((double)(actualSize - target) / target) * 100.0, 2);

                if (request.NeverExceedTarget)
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        ActualSizeBytes = actualSize,
                        TargetSizeBytes = target,
                        SizeExceeded = true,
                        SizeDeltaPercent = sizeDelta,
                        ErrorMessage = $"Target size exceeded by {sizeDelta}% ({actualSize / (1024 * 1024):F1} MB vs {target / (1024 * 1024):F1} MB). Strict NeverExceedTarget policy failed."
                    };
                }
            }
        }

        // Probe the output file to verify media streams integrity
        try
        {
            var outputAsset = await _probe.ProbeAsync(outputFilePath, cancellationToken).ConfigureAwait(false);

            if (outputAsset.VideoStreams.Count == 0)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ActualSizeBytes = actualSize,
                    ErrorMessage = "Validation failed: Output container has no video streams."
                };
            }

            // Duration check: tolerance of +- 1.5 seconds
            var durationDelta = Math.Abs((outputAsset.Duration - plan.Duration).TotalSeconds);
            if (plan.Duration.TotalSeconds > 5.0 && durationDelta > 3.0)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ActualSizeBytes = actualSize,
                    ErrorMessage = $"Validation warning: Output duration ({outputAsset.Duration:mm\\:ss}) deviates from source ({plan.Duration:mm\\:ss})."
                };
            }

            return new ValidationResult
            {
                IsValid = true,
                ActualSizeBytes = actualSize,
                TargetSizeBytes = request.TargetSizeBytes,
                SizeExceeded = sizeExceeded,
                SizeDeltaPercent = sizeDelta
            };
        }
        catch (Exception ex)
        {
            return new ValidationResult
            {
                IsValid = false,
                ActualSizeBytes = actualSize,
                ErrorMessage = $"Failed to probe and read output file: {ex.Message}"
            };
        }
    }
}
