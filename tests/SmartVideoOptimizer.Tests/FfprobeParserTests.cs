using SmartVideoOptimizer.Core.Domain;
using SmartVideoOptimizer.Media.Probing;
using Xunit;

namespace SmartVideoOptimizer.Tests;

public class FfprobeParserTests
{
    [Fact]
    public void ParseJson_StandardMp4_ExtractsMetadataAccurately()
    {
        var sampleJson = """
        {
          "streams": [
            {
              "index": 0,
              "codec_name": "h264",
              "codec_long_name": "H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10",
              "profile": "High",
              "codec_type": "video",
              "width": 1920,
              "height": 1080,
              "avg_frame_rate": "60/1",
              "pix_fmt": "yuv420p",
              "bits_per_raw_sample": "8",
              "bit_rate": "8500000"
            },
            {
              "index": 1,
              "codec_name": "aac",
              "codec_long_name": "AAC (Advanced Audio Coding)",
              "codec_type": "audio",
              "sample_rate": "48000",
              "channels": 2,
              "channel_layout": "stereo",
              "bit_rate": "192000",
              "tags": { "language": "eng", "title": "Main Stereo" },
              "disposition": { "default": 1, "forced": 0 }
            }
          ],
          "format": {
            "format_name": "mov,mp4,m4a,3gp,3g2,mj2",
            "format_long_name": "QuickTime / MOV",
            "duration": "125.500000",
            "size": "135000000",
            "bit_rate": "8605577"
          }
        }
        """;

        var asset = FfprobeParser.ParseJson(sampleJson, @"D:\Videos\sample.mp4");

        Assert.Equal(@"D:\Videos\sample.mp4", asset.FilePath);
        Assert.Equal("sample.mp4", asset.FileName);
        Assert.Equal(135000000, asset.FileSizeBytes);
        Assert.Equal(125.5, asset.Duration.TotalSeconds, precision: 1);

        // Video verification
        Assert.Single(asset.VideoStreams);
        var video = asset.PrimaryVideo!;
        Assert.Equal("h264", video.Codec);
        Assert.Equal(1920, video.Width);
        Assert.Equal(1080, video.Height);
        Assert.Equal("1920x1080", video.ResolutionLabel);
        Assert.Equal(60.0, video.FrameRate);
        Assert.Equal(8, video.BitDepth);
        Assert.False(video.Is10Bit);
        Assert.False(video.IsHdr);

        // Audio verification
        Assert.Single(asset.AudioStreams);
        var audio = asset.PrimaryAudio!;
        Assert.Equal("aac", audio.Codec);
        Assert.Equal(2, audio.Channels);
        Assert.Equal("eng", audio.Language);
        Assert.Equal("Main Stereo", audio.Title);
        Assert.True(audio.IsDefault);
    }

    [Fact]
    public void ParseJson_MultiTrackMkv_PreservesAllAudioAndSubtitleTracks()
    {
        var sampleJson = """
        {
          "streams": [
            {
              "index": 0,
              "codec_name": "hevc",
              "codec_type": "video",
              "width": 3840,
              "height": 2160,
              "avg_frame_rate": "24000/1001",
              "pix_fmt": "yuv420p10le",
              "color_space": "bt2020nc",
              "color_transfer": "smpte2084",
              "color_primaries": "bt2020"
            },
            {
              "index": 1,
              "codec_name": "dts",
              "codec_type": "audio",
              "channels": 6,
              "channel_layout": "5.1(side)",
              "tags": { "language": "fas", "title": "Persian Dub" },
              "disposition": { "default": 1, "forced": 0 }
            },
            {
              "index": 2,
              "codec_name": "ac3",
              "codec_type": "audio",
              "channels": 6,
              "channel_layout": "5.1(side)",
              "tags": { "language": "eng", "title": "English Original" },
              "disposition": { "default": 0, "forced": 0 }
            },
            {
              "index": 3,
              "codec_name": "subrip",
              "codec_type": "subtitle",
              "tags": { "language": "fas", "title": "Persian Full" },
              "disposition": { "default": 1, "forced": 0 }
            },
            {
              "index": 4,
              "codec_name": "hdmv_pgs_subtitle",
              "codec_type": "subtitle",
              "tags": { "language": "eng", "title": "English PGS" },
              "disposition": { "default": 0, "forced": 0 }
            }
          ],
          "format": {
            "format_name": "matroska,webm",
            "duration": "7200.000000",
            "size": "8589934592"
          }
        }
        """;

        var asset = FfprobeParser.ParseJson(sampleJson, @"D:\Movies\4k_movie.mkv");

        Assert.Equal(2, asset.AudioStreams.Count);
        Assert.Equal(2, asset.SubtitleStreams.Count);

        // Check HDR & 10-bit on 4K HEVC
        var video = asset.PrimaryVideo!;
        Assert.Equal(3840, video.Width);
        Assert.Equal(2160, video.Height);
        Assert.Equal(23.976, video.FrameRate, precision: 3);
        Assert.True(video.Is10Bit);
        Assert.True(video.IsHdr);
        Assert.Equal(HdrType.Hdr10, video.Color.HdrType);

        // Check audio tracks
        Assert.Equal("fas", asset.AudioStreams[0].Language);
        Assert.Equal("Persian Dub", asset.AudioStreams[0].Title);
        Assert.Equal("5.1 Surround", asset.AudioStreams[0].ChannelsLabel);
        Assert.True(asset.AudioStreams[0].IsDefault);

        Assert.Equal("eng", asset.AudioStreams[1].Language);
        Assert.False(asset.AudioStreams[1].IsDefault);

        // Check subtitle tracks & PGS image-based detection
        Assert.Equal("fas", asset.SubtitleStreams[0].Language);
        Assert.False(asset.SubtitleStreams[0].IsImageBased);

        Assert.Equal("eng", asset.SubtitleStreams[1].Language);
        Assert.True(asset.SubtitleStreams[1].IsImageBased);
    }

    [Fact]
    public void ParseJson_DolbyVision_IdentifiesDolbyVision()
    {
        var sampleJson = """
        {
          "streams": [
            {
              "index": 0,
              "codec_name": "hevc",
              "codec_type": "video",
              "width": 3840,
              "height": 2160,
              "avg_frame_rate": "24/1",
              "pix_fmt": "yuv420p10le",
              "side_data_list": [
                {
                  "side_data_type": "DOVI configuration record",
                  "dv_version_major": 1,
                  "dv_version_minor": 0,
                  "dv_profile": 5
                }
              ]
            }
          ],
          "format": {
            "format_name": "mp4",
            "duration": "60.0",
            "size": "50000000"
          }
        }
        """;

        var asset = FfprobeParser.ParseJson(sampleJson, @"D:\DolbyVisionSample.mp4");
        var video = asset.PrimaryVideo!;

        Assert.True(video.IsHdr);
        Assert.Equal(HdrType.DolbyVision, video.Color.HdrType);
    }
}
