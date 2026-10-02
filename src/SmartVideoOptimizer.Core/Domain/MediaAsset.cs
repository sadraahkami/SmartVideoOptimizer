namespace SmartVideoOptimizer.Core.Domain;

public sealed record MediaAsset
{
    public required string FilePath { get; init; }
    public string FileName => System.IO.Path.GetFileName(FilePath);
    public ContainerInfo Container { get; init; } = new();
    public TimeSpan Duration => Container.Duration;
    public long FileSizeBytes => Container.FileSizeBytes;

    public IReadOnlyList<VideoStream> VideoStreams { get; init; } = [];
    public IReadOnlyList<AudioStream> AudioStreams { get; init; } = [];
    public IReadOnlyList<SubtitleStream> SubtitleStreams { get; init; } = [];
    public IReadOnlyList<ChapterInfo> Chapters { get; init; } = [];

    public VideoStream? PrimaryVideo => VideoStreams.Count > 0 ? VideoStreams[0] : null;
    public AudioStream? PrimaryAudio => AudioStreams.FirstOrDefault(a => a.IsDefault) ?? AudioStreams.FirstOrDefault();

    public string HumanReadableFileSize
    {
        get
        {
            const double kb = 1024.0;
            const double mb = kb * 1024.0;
            const double gb = mb * 1024.0;

            if (FileSizeBytes >= gb)
                return $"{FileSizeBytes / gb:F2} GB";
            if (FileSizeBytes >= mb)
                return $"{FileSizeBytes / mb:F1} MB";
            if (FileSizeBytes >= kb)
                return $"{FileSizeBytes / kb:F1} KB";
            return $"{FileSizeBytes} B";
        }
    }

    public string HumanReadableDuration
    {
        get
        {
            var d = Duration;
            return d.TotalHours >= 1
                ? $"{(int)d.TotalHours:D2}:{d.Minutes:D2}:{d.Seconds:D2}"
                : $"{d.Minutes:D2}:{d.Seconds:D2}";
        }
    }
}
