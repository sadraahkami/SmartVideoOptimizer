using System.Globalization;
using System.Text.Json;
using SmartVideoOptimizer.Core.Domain;

namespace SmartVideoOptimizer.Media.Probing;

public static class FfprobeParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static MediaAsset ParseJson(string json, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var dto = JsonSerializer.Deserialize<FfprobeResultDto>(json, JsonOptions)
                  ?? throw new InvalidOperationException("Failed to parse ffprobe JSON output: Root object was null.");

        var container = ParseContainer(dto.Format, filePath);
        var streams = dto.Streams ?? [];

        var videoStreams = new List<VideoStream>();
        var audioStreams = new List<AudioStream>();
        var subtitleStreams = new List<SubtitleStream>();

        foreach (var s in streams)
        {
            var codecType = s.CodecType?.ToLowerInvariant();
            if (codecType == "video")
            {
                // Ignore attached pictures (cover art) as video streams
                if (s.Disposition?.AttachedPic == 1)
                    continue;

                videoStreams.Add(ParseVideoStream(s));
            }
            else if (codecType == "audio")
            {
                audioStreams.Add(ParseAudioStream(s));
            }
            else if (codecType == "subtitle")
            {
                subtitleStreams.Add(ParseSubtitleStream(s));
            }
        }

        var chapters = ParseChapters(dto.Chapters);

        return new MediaAsset
        {
            FilePath = filePath,
            Container = container,
            VideoStreams = videoStreams,
            AudioStreams = audioStreams,
            SubtitleStreams = subtitleStreams,
            Chapters = chapters
        };
    }

    private static ContainerInfo ParseContainer(FfprobeFormatDto? format, string filePath)
    {
        long fileSize = 0;
        if (!string.IsNullOrEmpty(format?.Size) && long.TryParse(format.Size, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSize))
        {
            fileSize = parsedSize;
        }
        else if (File.Exists(filePath))
        {
            try
            {
                fileSize = new FileInfo(filePath).Length;
            }
            catch
            {
                // ignored
            }
        }

        var duration = TimeSpan.Zero;
        if (!string.IsNullOrEmpty(format?.Duration) && double.TryParse(format.Duration, NumberStyles.Float, CultureInfo.InvariantCulture, out var durationSec))
        {
            duration = TimeSpan.FromSeconds(durationSec);
        }

        long? overallBitrate = null;
        if (!string.IsNullOrEmpty(format?.BitRate) && long.TryParse(format.BitRate, NumberStyles.Integer, CultureInfo.InvariantCulture, out var br))
        {
            overallBitrate = br;
        }
        else if (fileSize > 0 && duration.TotalSeconds > 0)
        {
            overallBitrate = (long)((fileSize * 8) / duration.TotalSeconds);
        }

        return new ContainerInfo
        {
            FormatName = format?.FormatName ?? Path.GetExtension(filePath).TrimStart('.'),
            FormatLongName = format?.FormatLongName ?? string.Empty,
            FileSizeBytes = fileSize,
            Duration = duration,
            OverallBitrate = overallBitrate,
            Tags = format?.Tags != null ? new Dictionary<string, string>(format.Tags, StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>()
        };
    }

    private static VideoStream ParseVideoStream(FfprobeStreamDto s)
    {
        var fps = ParseFrameRate(s.AvgFrameRate) ?? ParseFrameRate(s.RFrameRate) ?? 0.0;
        var bitDepth = ParseBitDepth(s.BitsPerRawSample, s.PixFmt);
        var hdrType = DetectHdrType(s);

        var colorInfo = new ColorInfo
        {
            ColorSpace = s.ColorSpace ?? string.Empty,
            ColorRange = s.ColorRange ?? string.Empty,
            ColorPrimaries = s.ColorPrimaries ?? string.Empty,
            ColorTransfer = s.ColorTransfer ?? string.Empty,
            BitDepth = bitDepth,
            HdrType = hdrType
        };

        long? bitrate = null;
        if (!string.IsNullOrEmpty(s.BitRate) && long.TryParse(s.BitRate, NumberStyles.Integer, CultureInfo.InvariantCulture, out var br))
        {
            bitrate = br;
        }

        long? totalFrames = null;
        if (!string.IsNullOrEmpty(s.NbFrames) && long.TryParse(s.NbFrames, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tf))
        {
            totalFrames = tf;
        }

        var rotation = ParseRotation(s);

        return new VideoStream
        {
            Index = s.Index,
            Codec = s.CodecName ?? string.Empty,
            CodecLongName = s.CodecLongName ?? string.Empty,
            Profile = s.Profile ?? string.Empty,
            Width = s.Width ?? 0,
            Height = s.Height ?? 0,
            FrameRate = Math.Round(fps, 3),
            FrameRateRaw = s.AvgFrameRate ?? s.RFrameRate ?? string.Empty,
            Bitrate = bitrate,
            PixelFormat = s.PixFmt ?? string.Empty,
            BitDepth = bitDepth,
            Color = colorInfo,
            Rotation = rotation,
            AspectRatio = s.DisplayAspectRatio ?? s.SampleAspectRatio ?? string.Empty,
            TotalFrames = totalFrames
        };
    }

    private static AudioStream ParseAudioStream(FfprobeStreamDto s)
    {
        long? bitrate = null;
        if (!string.IsNullOrEmpty(s.BitRate) && long.TryParse(s.BitRate, NumberStyles.Integer, CultureInfo.InvariantCulture, out var br))
        {
            bitrate = br;
        }

        var sampleRate = 0;
        if (!string.IsNullOrEmpty(s.SampleRate) && int.TryParse(s.SampleRate, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sr))
        {
            sampleRate = sr;
        }

        var lang = GetTag(s.Tags, "language") ?? "und";
        var title = GetTag(s.Tags, "title") ?? string.Empty;

        return new AudioStream
        {
            Index = s.Index,
            Codec = s.CodecName ?? string.Empty,
            CodecLongName = s.CodecLongName ?? string.Empty,
            Bitrate = bitrate,
            SampleRate = sampleRate,
            Channels = s.Channels ?? 2,
            ChannelLayout = s.ChannelLayout ?? string.Empty,
            Language = lang,
            Title = title,
            IsDefault = s.Disposition?.Default == 1,
            IsForced = s.Disposition?.Forced == 1
        };
    }

    private static SubtitleStream ParseSubtitleStream(FfprobeStreamDto s)
    {
        var lang = GetTag(s.Tags, "language") ?? "und";
        var title = GetTag(s.Tags, "title") ?? string.Empty;
        var codec = (s.CodecName ?? string.Empty).ToLowerInvariant();

        // Image-based subtitle codecs
        var isImageBased = codec is "hdmv_pgs_subtitle" or "pgssub" or "dvd_subtitle" or "dvdsub" or "xsub";

        return new SubtitleStream
        {
            Index = s.Index,
            Codec = s.CodecName ?? string.Empty,
            Language = lang,
            Title = title,
            IsDefault = s.Disposition?.Default == 1,
            IsForced = s.Disposition?.Forced == 1,
            IsImageBased = isImageBased
        };
    }

    private static IReadOnlyList<ChapterInfo> ParseChapters(List<FfprobeChapterDto>? chaptersDto)
    {
        if (chaptersDto == null || chaptersDto.Count == 0)
            return [];

        var list = new List<ChapterInfo>();
        foreach (var c in chaptersDto)
        {
            double.TryParse(c.StartTime, NumberStyles.Float, CultureInfo.InvariantCulture, out var start);
            double.TryParse(c.EndTime, NumberStyles.Float, CultureInfo.InvariantCulture, out var end);
            var title = GetTag(c.Tags, "title") ?? $"Chapter {c.Id}";

            list.Add(new ChapterInfo
            {
                Id = c.Id,
                StartTimeSeconds = start,
                EndTimeSeconds = end,
                Title = title
            });
        }

        return list;
    }

    private static double? ParseFrameRate(string? rawRate)
    {
        if (string.IsNullOrWhiteSpace(rawRate) || rawRate == "0/0")
            return null;

        var parts = rawRate.Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var num) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var den) &&
            den > 0)
        {
            return num / den;
        }

        if (double.TryParse(rawRate, NumberStyles.Float, CultureInfo.InvariantCulture, out var directVal))
        {
            return directVal;
        }

        return null;
    }

    private static int ParseBitDepth(string? bitsPerRawSample, string? pixFmt)
    {
        if (!string.IsNullOrEmpty(bitsPerRawSample) &&
            int.TryParse(bitsPerRawSample, NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) &&
            b > 0)
        {
            return b;
        }

        if (string.IsNullOrEmpty(pixFmt))
            return 8;

        var lower = pixFmt.ToLowerInvariant();
        if (lower.Contains("12")) return 12;
        if (lower.Contains("10")) return 10;
        if (lower.Contains("16")) return 16;

        return 8;
    }

    private static HdrType DetectHdrType(FfprobeStreamDto s)
    {
        // 1. Dolby Vision check
        if (s.SideDataList != null)
        {
            foreach (var sideData in s.SideDataList)
            {
                var type = sideData.SideDataType?.ToLowerInvariant() ?? string.Empty;
                if (type.Contains("dovi") || type.Contains("dolby vision") || sideData.DvVersionMajor.HasValue)
                    return HdrType.DolbyVision;
                if (type.Contains("hdr10+") || type.Contains("dynamic hdr"))
                    return HdrType.Hdr10Plus;
            }
        }

        var transfer = s.ColorTransfer?.ToLowerInvariant() ?? string.Empty;
        var primaries = s.ColorPrimaries?.ToLowerInvariant() ?? string.Empty;

        // 2. HLG
        if (transfer is "arib-std-b67" or "hlg")
            return HdrType.Hlg;

        // 3. HDR10 (SMPTE 2084 / PQ)
        if (transfer is "smpte2084")
            return HdrType.Hdr10;

        // 4. BT.2020 with 10-bit color transfer
        if (primaries.Contains("bt2020") && (s.PixFmt?.Contains("10") == true || s.BitsPerRawSample == "10"))
        {
            return HdrType.Hdr10;
        }

        return HdrType.None;
    }

    private static int ParseRotation(FfprobeStreamDto s)
    {
        if (s.SideDataList != null)
        {
            foreach (var side in s.SideDataList)
            {
                if (side.Rotation.HasValue)
                    return side.Rotation.Value;
            }
        }

        var rotateTag = GetTag(s.Tags, "rotate");
        if (!string.IsNullOrEmpty(rotateTag) && int.TryParse(rotateTag, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r))
        {
            return r;
        }

        return 0;
    }

    private static string? GetTag(Dictionary<string, string>? tags, string key)
    {
        if (tags == null) return null;
        foreach (var kvp in tags)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }
        return null;
    }
}
