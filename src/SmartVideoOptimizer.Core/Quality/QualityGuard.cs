using SmartVideoOptimizer.Core.Domain;

namespace SmartVideoOptimizer.Core.Quality;

public sealed record QualityRiskReport
{
    public QualityRisk Risk { get; init; }
    public double EffectiveBitsPerPixel { get; init; }
    public string RecommendationEnglish { get; init; } = string.Empty;
    public string RecommendationPersian { get; init; } = string.Empty;
    public bool IsDownscaleRecommended { get; init; }
    public ResolutionPolicy? SuggestedResolution { get; init; }
    public long? RecommendedMinSizeBytes { get; init; }
}

public static class QualityGuard
{
    public static QualityRiskReport AssessRisk(
        int videoBitrateKbps,
        int width,
        int height,
        double fps,
        TimeSpan duration,
        string codec)
    {
        if (width <= 0) width = 1920;
        if (height <= 0) height = 1080;
        if (fps <= 0) fps = 30.0;

        var bitrateBps = videoBitrateKbps * 1000.0;
        var pixelsPerSecond = (double)width * height * fps;

        var rawBppf = pixelsPerSecond > 0 ? (bitrateBps / pixelsPerSecond) : 0.1;

        var isModernCodec = codec.Contains("hevc", StringComparison.OrdinalIgnoreCase) ||
                            codec.Contains("265", StringComparison.OrdinalIgnoreCase) ||
                            codec.Contains("av1", StringComparison.OrdinalIgnoreCase);

        // Modern codecs achieve ~1.8x visual efficiency compared to h264
        var effectiveBppf = rawBppf * (isModernCodec ? 1.75 : 1.0);

        QualityRisk risk;
        string recEn;
        string recFa;
        bool downscale = false;
        ResolutionPolicy? suggestedRes = null;
        long? recommendedBytes = null;

        if (effectiveBppf >= 0.11)
        {
            risk = QualityRisk.Minimal;
            recEn = "Excellent visual fidelity expected with virtually zero compression artifacts.";
            recFa = "کیفیت تصویر بسیار عالی و بدون افت بصری محسوس پیش‌بینی می‌شود.";
        }
        else if (effectiveBppf >= 0.075)
        {
            risk = QualityRisk.Low;
            recEn = "Good visual quality. Minimal loss visible only in high-motion scenes.";
            recFa = "کیفیت تصویر خوب با جزییات مناسب و افت بسیار اندک در صحنه‌های پرتحرک.";
        }
        else if (effectiveBppf >= 0.045)
        {
            risk = QualityRisk.Moderate;
            recEn = "Noticeable compression in complex scenes. Balanced for general sharing.";
            recFa = "افت کیفیت متوسط در صحنه‌های پیچیده؛ مناسب برای اشتراک‌گذاری عمومی.";
        }
        else if (effectiveBppf >= 0.028)
        {
            risk = QualityRisk.High;
            downscale = true;
            suggestedRes = width > 1920 ? ResolutionPolicy.Scale1080p : ResolutionPolicy.Scale720p;
            recommendedBytes = (long)((pixelsPerSecond * 0.065 / 8.0) * duration.TotalSeconds);

            recEn = $"Bitrate is low for {width}x{height}. Downscaling or increasing target size is recommended.";
            recFa = $"نرخ بیت برای وضوح {width}x{height} پایین است. کاهش وضوح یا افزایش حجم هدف توصیه می‌شود.";
        }
        else
        {
            risk = QualityRisk.Severe;
            downscale = true;
            suggestedRes = width > 1280 ? ResolutionPolicy.Scale720p : ResolutionPolicy.Scale480p;
            recommendedBytes = (long)((pixelsPerSecond * 0.075 / 8.0) * duration.TotalSeconds);

            recEn = $"Severe compression artifacts expected. Resolution downscaling is strongly advised.";
            recFa = $"احتمال شطرنجی شدن و افت شدید کیفیت بسیار بالاست. کاهش وضوح تصویر اکیداً توصیه می‌شود.";
        }

        return new QualityRiskReport
        {
            Risk = risk,
            EffectiveBitsPerPixel = Math.Round(effectiveBppf, 4),
            RecommendationEnglish = recEn,
            RecommendationPersian = recFa,
            IsDownscaleRecommended = downscale,
            SuggestedResolution = suggestedRes,
            RecommendedMinSizeBytes = recommendedBytes
        };
    }
}
