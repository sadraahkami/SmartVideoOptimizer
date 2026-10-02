namespace SmartVideoOptimizer.Media.Probing;

public static class FfprobeLocator
{
    private static string? _cachedPath;

    public static string FindFfprobe(string? customPath = null)
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
        candidates.Add(Path.Combine(baseDir, "ffprobe.exe"));
        candidates.Add(Path.Combine(baseDir, "tools", "ffprobe.exe"));
        candidates.Add(Path.Combine(baseDir, "tools", "bin", "ffprobe.exe"));
        candidates.Add(Path.Combine(baseDir, "runtimes", "win-x64", "native", "ffprobe.exe"));

        // Check parent directory hierarchy (useful during development in IDE/VS/dotnet run)
        var dir = new DirectoryInfo(baseDir);
        for (var i = 0; i < 5 && dir != null; i++)
        {
            candidates.Add(Path.Combine(dir.FullName, "tools", "ffprobe.exe"));
            candidates.Add(Path.Combine(dir.FullName, "tools", "bin", "ffprobe.exe"));
            candidates.Add(Path.Combine(dir.FullName, "tools", "ffmpeg", "bin", "ffprobe.exe"));
            dir = dir.Parent;
        }

        // Also check fixed workspace path if on dev machine
        candidates.Add(@"A:\AGENT\github\SmartVideoOptimizer\tools\ffprobe.exe");
        candidates.Add(@"A:\AGENT\github\SmartVideoOptimizer\tools\bin\ffprobe.exe");

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                _cachedPath = Path.GetFullPath(candidate);
                return _cachedPath;
            }
        }

        // Check PATH environment variable
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var path in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var full = Path.Combine(path.Trim(), "ffprobe.exe");
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
            "ffprobe.exe was not found. Please ensure ffprobe is installed in PATH or in the application's 'tools' folder.");
    }

    public static bool IsAvailable(string? customPath = null)
    {
        try
        {
            FindFfprobe(customPath);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
