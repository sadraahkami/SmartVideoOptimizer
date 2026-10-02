using System.Text;
using SmartVideoOptimizer.Core.Planning;

namespace SmartVideoOptimizer.Media.Encoding;

public static class FFmpegCommandBuilder
{
    public static string BuildArguments(EncodingPlan plan)
    {
        var sb = new StringBuilder();

        // Overwrite output without prompting, standard progress output
        sb.Append("-y -hide_banner ");

        // Input file
        sb.Append($"-i \"{plan.InputPath}\" ");

        // Video stream mapping (primary video)
        sb.Append("-map 0:v:0 ");

        // Video codec & preset
        sb.Append($"-c:v {plan.VideoEncoder} ");
        if (!string.IsNullOrWhiteSpace(plan.Preset))
        {
            sb.Append($"-preset {plan.Preset} ");
        }

        if (!string.IsNullOrWhiteSpace(plan.PixelFormat))
        {
            sb.Append($"-pix_fmt {plan.PixelFormat} ");
        }

        // Color metadata & HDR preservation
        if (plan.PreserveHdr)
        {
            if (!string.IsNullOrWhiteSpace(plan.ColorPrimaries))
                sb.Append($"-color_primaries {plan.ColorPrimaries} ");
            if (!string.IsNullOrWhiteSpace(plan.ColorTransfer))
                sb.Append($"-color_trc {plan.ColorTransfer} ");
            if (!string.IsNullOrWhiteSpace(plan.ColorSpace))
                sb.Append($"-colorspace {plan.ColorSpace} ");
            if (!string.IsNullOrWhiteSpace(plan.ColorRange))
                sb.Append($"-color_range {plan.ColorRange} ");
        }

        if (plan.Rotation.HasValue && plan.Rotation.Value != 0)
        {
            sb.Append($"-metadata:s:v:0 rotate={plan.Rotation.Value} ");
        }

        // Filters: Resolution scaling
        var filters = new List<string>();
        if (plan.TargetWidth.HasValue && plan.TargetHeight.HasValue && plan.TargetWidth.Value > 0 && plan.TargetHeight.Value > 0)
        {
            // Ensure dimensions are even numbers (required by h264/hevc)
            var w = (plan.TargetWidth.Value / 2) * 2;
            var h = (plan.TargetHeight.Value / 2) * 2;
            filters.Add($"scale={w}:{h}:flags=lanczos");
        }

        if (filters.Count > 0)
        {
            sb.Append($"-vf \"{string.Join(",", filters)}\" ");
        }

        if (plan.TargetFps.HasValue && plan.TargetFps.Value > 0)
        {
            sb.Append($"-r {plan.TargetFps.Value:F3} ");
        }

        // Rate control
        switch (plan.RateControl)
        {
            case RateControlMode.CRF:
                sb.Append($"-crf {plan.CrfValue ?? 22} ");
                break;

            case RateControlMode.VBR_TwoPass:
                var br = plan.TargetVideoBitrateKbps ?? 2500;
                sb.Append($"-b:v {br}k ");
                if (plan.PassNumber == 1)
                {
                    sb.Append($"-pass 1 ");
                    if (!string.IsNullOrWhiteSpace(plan.PassLogFilePrefix))
                        sb.Append($"-passlogfile \"{plan.PassLogFilePrefix}\" ");
                    // First pass doesn't need audio or subtitle processing
                    sb.Append("-an -sn -f null NUL");
                    return sb.ToString().Trim();
                }
                else if (plan.PassNumber == 2)
                {
                    sb.Append($"-pass 2 ");
                    if (!string.IsNullOrWhiteSpace(plan.PassLogFilePrefix))
                        sb.Append($"-passlogfile \"{plan.PassLogFilePrefix}\" ");
                }
                break;

            case RateControlMode.VBR_OnePass:
                var br1 = plan.TargetVideoBitrateKbps ?? 2500;
                sb.Append($"-b:v {br1}k -maxrate {(int)(br1 * 1.35)}k -bufsize {br1 * 2}k ");
                break;

            case RateControlMode.CBR:
                var brCbr = plan.TargetVideoBitrateKbps ?? 2500;
                sb.Append($"-b:v {brCbr}k -minrate {brCbr}k -maxrate {brCbr}k -bufsize {brCbr}k ");
                break;
        }

        // Audio streams
        if (plan.AudioPlans.Count == 0)
        {
            sb.Append("-map 0:a? -c:a copy ");
        }
        else
        {
            for (var i = 0; i < plan.AudioPlans.Count; i++)
            {
                var a = plan.AudioPlans[i];
                sb.Append($"-map 0:{a.SourceIndex} ");
                if (a.Action == StreamAction.Copy)
                {
                    sb.Append($"-c:a:{i} copy ");
                }
                else
                {
                    sb.Append($"-c:a:{i} {a.Codec} -b:a:{i} {a.BitrateKbps}k ");
                    if (a.Channels.HasValue)
                        sb.Append($"-ac:{i} {a.Channels.Value} ");
                }

                if (!string.IsNullOrEmpty(a.Language))
                    sb.Append($"-metadata:s:a:{i} language={a.Language} ");
                if (!string.IsNullOrEmpty(a.Title))
                    sb.Append($"-metadata:s:a:{i} title=\"{a.Title}\" ");
            }
        }

        // Subtitle streams
        if (plan.SubtitlePlans.Count == 0)
        {
            sb.Append("-map 0:s? -c:s copy ");
        }
        else
        {
            var activeSubIndex = 0;
            for (var i = 0; i < plan.SubtitlePlans.Count; i++)
            {
                var s = plan.SubtitlePlans[i];
                if (s.Action == StreamAction.Drop) continue;

                sb.Append($"-map 0:{s.SourceIndex} ");
                if (plan.Container == "mp4" && s.Codec != "mov_text")
                {
                    // MP4 requires mov_text for text subtitles
                    sb.Append($"-c:s:{activeSubIndex} mov_text ");
                }
                else
                {
                    sb.Append($"-c:s:{activeSubIndex} copy ");
                }

                if (!string.IsNullOrEmpty(s.Language))
                    sb.Append($"-metadata:s:s:{activeSubIndex} language={s.Language} ");
                if (!string.IsNullOrEmpty(s.Title))
                    sb.Append($"-metadata:s:s:{activeSubIndex} title=\"{s.Title}\" ");

                activeSubIndex++;
            }
        }

        // Copy chapters and metadata
        sb.Append("-map_chapters 0 -map_metadata 0 ");

        // Append custom user / planner arguments if any
        foreach (var arg in plan.CustomArguments)
        {
            sb.Append($"{arg} ");
        }

        // Output destination (temp file)
        sb.Append($"\"{plan.TempOutputPath}\"");

        return sb.ToString().Trim();
    }
}
