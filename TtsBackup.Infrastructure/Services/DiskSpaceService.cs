using System.IO;
using TtsBackup.Core.Services;

namespace TtsBackup.Infrastructure.Services;

public sealed class DiskSpaceService : IDiskSpaceService
{
    public long GetAvailableFreeSpace(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return 0;

        var root = Path.GetPathRoot(Path.GetFullPath(path));
        if (string.IsNullOrEmpty(root))
            return 0;

        var drive = new DriveInfo(root);
        return drive.AvailableFreeSpace;
    }
}
