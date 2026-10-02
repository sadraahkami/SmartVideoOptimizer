using System.Text.Json.Serialization;

namespace SmartVideoOptimizer.Media.Probing;

public sealed class FfprobeResultDto
{
    [JsonPropertyName("streams")]
    public List<FfprobeStreamDto>? Streams { get; set; }

    [JsonPropertyName("format")]
    public FfprobeFormatDto? Format { get; set; }

    [JsonPropertyName("chapters")]
    public List<FfprobeChapterDto>? Chapters { get; set; }
}

public sealed class FfprobeFormatDto
{
    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    [JsonPropertyName("format_name")]
    public string? FormatName { get; set; }

    [JsonPropertyName("format_long_name")]
    public string? FormatLongName { get; set; }

    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    [JsonPropertyName("size")]
    public string? Size { get; set; }

    [JsonPropertyName("bit_rate")]
    public string? BitRate { get; set; }

    [JsonPropertyName("tags")]
    public Dictionary<string, string>? Tags { get; set; }
}

public sealed class FfprobeDispositionDto
{
    [JsonPropertyName("default")]
    public int Default { get; set; }

    [JsonPropertyName("forced")]
    public int Forced { get; set; }

    [JsonPropertyName("attached_pic")]
    public int AttachedPic { get; set; }
}

public sealed class FfprobeSideDataDto
{
    [JsonPropertyName("side_data_type")]
    public string? SideDataType { get; set; }

    [JsonPropertyName("dv_version_major")]
    public int? DvVersionMajor { get; set; }

    [JsonPropertyName("rotation")]
    public int? Rotation { get; set; }
}

public sealed class FfprobeStreamDto
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("codec_name")]
    public string? CodecName { get; set; }

    [JsonPropertyName("codec_long_name")]
    public string? CodecLongName { get; set; }

    [JsonPropertyName("codec_type")]
    public string? CodecType { get; set; }

    [JsonPropertyName("profile")]
    public string? Profile { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("r_frame_rate")]
    public string? RFrameRate { get; set; }

    [JsonPropertyName("avg_frame_rate")]
    public string? AvgFrameRate { get; set; }

    [JsonPropertyName("pix_fmt")]
    public string? PixFmt { get; set; }

    [JsonPropertyName("bits_per_raw_sample")]
    public string? BitsPerRawSample { get; set; }

    [JsonPropertyName("bit_rate")]
    public string? BitRate { get; set; }

    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    [JsonPropertyName("sample_rate")]
    public string? SampleRate { get; set; }

    [JsonPropertyName("channels")]
    public int? Channels { get; set; }

    [JsonPropertyName("channel_layout")]
    public string? ChannelLayout { get; set; }

    [JsonPropertyName("color_range")]
    public string? ColorRange { get; set; }

    [JsonPropertyName("color_space")]
    public string? ColorSpace { get; set; }

    [JsonPropertyName("color_transfer")]
    public string? ColorTransfer { get; set; }

    [JsonPropertyName("color_primaries")]
    public string? ColorPrimaries { get; set; }

    [JsonPropertyName("display_aspect_ratio")]
    public string? DisplayAspectRatio { get; set; }

    [JsonPropertyName("sample_aspect_ratio")]
    public string? SampleAspectRatio { get; set; }

    [JsonPropertyName("nb_frames")]
    public string? NbFrames { get; set; }

    [JsonPropertyName("disposition")]
    public FfprobeDispositionDto? Disposition { get; set; }

    [JsonPropertyName("tags")]
    public Dictionary<string, string>? Tags { get; set; }

    [JsonPropertyName("side_data_list")]
    public List<FfprobeSideDataDto>? SideDataList { get; set; }
}

public sealed class FfprobeChapterDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("start_time")]
    public string? StartTime { get; set; }

    [JsonPropertyName("end_time")]
    public string? EndTime { get; set; }

    [JsonPropertyName("tags")]
    public Dictionary<string, string>? Tags { get; set; }
}
