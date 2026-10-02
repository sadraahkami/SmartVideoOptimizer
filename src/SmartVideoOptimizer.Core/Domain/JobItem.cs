using System.IO;

namespace SmartVideoOptimizer.Core.Domain;

public sealed record JobItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string SourcePath { get; init; }
    public required string OutputPath { get; init; }
    public CompressionGoal Goal { get; init; } = CompressionGoal.ExactSize;
    public JobState State { get; init; } = JobState.Created;
    public double ProgressPercentage { get; init; }
    public long OriginalSizeBytes { get; init; }
    public long? OutputSizeBytes { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; init; }
    public int RetryCount { get; init; }

    public string FileName => Path.GetFileName(SourcePath);
    public string OutputFileName => Path.GetFileName(OutputPath);

    public double CompressionRatio => OriginalSizeBytes > 0 && OutputSizeBytes.HasValue
        ? Math.Round((1.0 - ((double)OutputSizeBytes.Value / OriginalSizeBytes)) * 100.0, 1)
        : 0.0;

    public string HumanReadableOriginalSize
    {
        get
        {
            const double mb = 1024.0 * 1024.0;
            return $"{OriginalSizeBytes / mb:F1} MB";
        }
    }

    public string HumanReadableOutputSize
    {
        get
        {
            if (!OutputSizeBytes.HasValue) return "—";
            const double mb = 1024.0 * 1024.0;
            return $"{OutputSizeBytes.Value / mb:F1} MB";
        }
    }

    public string SavingsLabel
    {
        get
        {
            if (!OutputSizeBytes.HasValue || OriginalSizeBytes <= 0) return "—";
            var saved = OriginalSizeBytes - OutputSizeBytes.Value;
            const double mb = 1024.0 * 1024.0;
            if (saved > 0)
            {
                return $"-{saved / mb:F1} MB ({CompressionRatio}%)";
            }
            return $"+{Math.Abs(saved) / mb:F1} MB";
        }
    }

    public string GoalTitlePersian => Goal switch
    {
        CompressionGoal.ExactSize => "اندازه دقیق",
        CompressionGoal.BestQuality => "حداکثر کیفیت",
        CompressionGoal.SmartCompress => "فشرده‌سازی هوشمند",
        _ => "پیش‌فرض"
    };

    public string GoalTitleEnglish => Goal switch
    {
        CompressionGoal.ExactSize => "Exact Size",
        CompressionGoal.BestQuality => "Best Quality",
        CompressionGoal.SmartCompress => "Smart Compress",
        _ => "Default"
    };

    public string StateTitlePersian => State switch
    {
        JobState.Completed => "موفق ✓",
        JobState.Failed => "ناموفق ✗",
        JobState.Cancelled => "لغو شده",
        JobState.Encoding => "در حال فشرده‌سازی...",
        JobState.Validating => "در حال ارزیابی...",
        JobState.Preparing => "آماده‌سازی...",
        JobState.Queued => "در صف انتظار",
        _ => "شروع نشده"
    };

    public string StateBadgeColor => State switch
    {
        JobState.Completed => "#10B981",
        JobState.Failed => "#EF4444",
        JobState.Cancelled => "#F59E0B",
        JobState.Encoding => "#38BDF8",
        _ => "#64748B"
    };

    public string FormattedDate => CreatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
}
