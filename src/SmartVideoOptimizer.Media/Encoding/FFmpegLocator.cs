namespace SmartVideoOptimizer.Media.Encoding;

public static class FFmpegLocator
{
    private static string? _cachedPath;

    public static string FindFFmpeg(string? customPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
        {
            _cachedPath = Path.GetFullPath(customPath);
            return _cachedPath;
        }

        if (!string.IsNullOrEmpty(_cachedPath) && File.Exists(_cachedPath))
        {
            return _cachedPath;
        }

        var candidates = new List<string>();

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        candidates.Add(Path.Combine(baseDir, "ffmpeg.exe"));
        candidates.Add(Path.Combine(baseDir, "tools", "ffmpeg.exe"));
        candidates.Add(Path.Combine(baseDir, "tools", "bin", "ffmpeg.exe"));
        candidates.Add(Path.Combine(baseDir, "runtimes", "win-x64", "native", "ffmpeg.exe"));

        var dir = new DirectoryInfo(baseDir);
        for (var i = 0; i < 5 && dir != null; i++)
        {
            candidates.Add(Path.Combine(dir.FullName, "tools", "ffmpeg.exe"));
            candidates.Add(Path.Combine(dir.FullName, "tools", "bin", "ffmpeg.exe"));
            candidates.Add(Path.Combine(dir.FullName, "tools", "ffmpeg", "bin", "ffmpeg.exe"));
            dir = dir.Parent;
        }

        candidates.Add(@"A:\AGENT\github\SmartVideoOptimizer\tools\ffmpeg.exe");
        candidates.Add(@"A:\AGENT\github\SmartVideoOptimizer\tools\bin\ffmpeg.exe");

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                _cachedPath = Path.GetFullPath(candidate);
                return _cachedPath;
            }
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var path in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var full = Path.Combine(path.Trim(), "ffmpeg.exe");
                    if (File.Exists(full))
                    {
                        _cachedPath = Path.GetFullPath(full);
                        return _cachedPath;
                    }
                }
                catch
                {
                    // Ignore invalid path entries
                }
            }
        }

        throw new FileNotFoundException(
            "ffmpeg.exe was not found. Please ensure ffmpeg is installed in PATH or in the application's 'tools' folder.");
    }
}
