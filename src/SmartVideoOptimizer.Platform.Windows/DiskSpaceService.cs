namespace SmartVideoOptimizer.Platform.Windows;

public static class DiskSpaceService
{
    public static long GetAvailableFreeSpace(string directoryOrFilePath)
    {
        try
        {
            var fullPath = Path.GetFullPath(directoryOrFilePath);
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root)) return -1;

            var driveInfo = new DriveInfo(root);
            return driveInfo.AvailableFreeSpace;
        }
        catch
        {
            return -1;
        }
    }

    public static string FormatBytes(long bytes)
    {
        const double kb = 1024.0;
        const double mb = kb * 1024.0;
        const double gb = mb * 1024.0;

        if (bytes >= gb)
            return $"{bytes / gb:F2} GB";
        if (bytes >= mb)
            return $"{bytes / mb:F1} MB";
        if (bytes >= kb)
            return $"{bytes / kb:F1} KB";
        return $"{bytes} B";
    }
}
