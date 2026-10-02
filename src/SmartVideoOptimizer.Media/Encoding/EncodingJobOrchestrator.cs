using SmartVideoOptimizer.Core.Domain;
using SmartVideoOptimizer.Core.Interfaces;
using SmartVideoOptimizer.Core.Planning;
using SmartVideoOptimizer.Core.Quality;
using SmartVideoOptimizer.Core.Queue;
using SmartVideoOptimizer.Core.Validation;
using SmartVideoOptimizer.Media.Probing;

namespace SmartVideoOptimizer.Media.Encoding;

public sealed class EncodingJobOrchestrator
{
    private readonly IMediaProbe _probe;
    private readonly EncodingEngine _engine;
    private readonly OutputValidator _validator;
    private readonly ICapabilityDetector? _capabilityDetector;

    public EncodingJobOrchestrator(
        IMediaProbe? probe = null,
        EncodingEngine? engine = null,
        ICapabilityDetector? capabilityDetector = null)
    {
        _probe = probe ?? new MediaProbe();
        _engine = engine ?? new EncodingEngine();
        _validator = new OutputValidator(_probe);
        _capabilityDetector = capabilityDetector;
    }

    public async Task<ValidationResult> RunJobWithRetryAsync(
        JobRequest request,
        MediaAsset asset,
        IProgress<EncodingProgress>? progress = null,
        QueueManager? queueManager = null,
        Guid? jobId = null,
        CancellationToken cancellationToken = default)
    {
        HardwareCapabilities? hardwareCaps = null;
        if (_capabilityDetector != null)
        {
            try { hardwareCaps = await _capabilityDetector.DetectCapabilitiesAsync(false, cancellationToken); } catch { /* ignore */ }
        }

        // 1. Initial Plan generation
        var plan = request.Goal switch
        {
            CompressionGoal.ExactSize => ExactSizePlanner.CreatePlan(request, asset),
            CompressionGoal.BestQuality => QualityPlanner.CreatePlan(request, asset, hardwareCaps),
            CompressionGoal.SmartCompress => SmartPlanner.CreateSmartRecommendation(request, asset, hardwareCaps).Plan,
            _ => ExactSizePlanner.CreatePlan(request, asset)
        };

        const int maxAttempts = 2;
        var attempt = 0;
        ValidationResult? lastValidation = null;

        while (attempt < maxAttempts)
        {
            attempt++;
            cancellationToken.ThrowIfCancellationRequested();

            var currentPlan = plan;
            if (attempt > 1 && request.Goal == CompressionGoal.ExactSize && request.TargetSizeBytes.HasValue)
            {
                // Retry attempt: increase safety margin by 4.5% to guarantee target compliance
                var retryRequest = request with { SafetyMarginPercent = request.SafetyMarginPercent + 4.5 };
                currentPlan = ExactSizePlanner.CreatePlan(retryRequest, asset);
            }

            var finalOutputPath = await _engine.ExecutePlanAsync(currentPlan, progress, cancellationToken).ConfigureAwait(false);

            lastValidation = await _validator.ValidateOutputAsync(finalOutputPath, currentPlan, request, cancellationToken).ConfigureAwait(false);

            if (lastValidation.IsValid)
            {
                if (queueManager != null && jobId.HasValue)
                {
                    await queueManager.CompleteJobAsync(jobId.Value, lastValidation.ActualSizeBytes, cancellationToken);
                }
                return lastValidation;
            }

            if (!lastValidation.SizeExceeded || !request.NeverExceedTarget)
            {
                // Non-size validation failure or NeverExceedTarget is off, don't retry
                break;
            }
        }

        var failureMessage = lastValidation?.ErrorMessage ?? "Encoding validation failed.";
        if (queueManager != null && jobId.HasValue)
        {
            await queueManager.FailJobAsync(jobId.Value, failureMessage, cancellationToken);
        }

        return lastValidation ?? new ValidationResult { IsValid = false, ErrorMessage = failureMessage };
    }
}
