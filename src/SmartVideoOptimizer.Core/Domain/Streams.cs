namespace SmartVideoOptimizer.Core.Domain;

public sealed record VideoStream
{
    public int Index { get; init; }
    public string Codec { get; init; } = string.Empty;
    public string CodecLongName { get; init; } = string.Empty;
    public string Profile { get; init; } = string.Empty;
    public int Width { get; init; }
    public int Height { get; init; }
    public double FrameRate { get; init; }
    public string FrameRateRaw { get; init; } = string.Empty;
    public long? Bitrate { get; init; }
    public string PixelFormat { get; init; } = string.Empty;
    public int BitDepth { get; init; } = 8;
    public ColorInfo Color { get; init; } = new();
    public int Rotation { get; init; }
    public string AspectRatio { get; init; } = string.Empty;
    public long? TotalFrames { get; init; }

    public bool Is10Bit => BitDepth >= 10 || Color.Is10Bit;
    public bool IsHdr => Color.IsHdr;
    public string ResolutionLabel => $"{Width}x{Height}";
}

public sealed record AudioStream
{
    public int Index { get; init; }
    public string Codec { get; init; } = string.Empty;
    public string CodecLongName { get; init; } = string.Empty;
    public long? Bitrate { get; init; }
    public int SampleRate { get; init; }
    public int Channels { get; init; }
    public string ChannelLayout { get; init; } = string.Empty;
    public string Language { get; init; } = "und";
    public string Title { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public bool IsForced { get; init; }

    public string ChannelsLabel => Channels switch
    {
        1 => "Mono (1.0)",
        2 => "Stereo (2.0)",
        6 => "5.1 Surround",
        8 => "7.1 Surround",
        _ => $"{Channels} Channels"
    };
}

public sealed record SubtitleStream
{
    public int Index { get; init; }
    public string Codec { get; init; } = string.Empty;
    public string Language { get; init; } = "und";
    public string Title { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public bool IsForced { get; init; }
    public bool IsImageBased { get; init; }
}
