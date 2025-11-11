namespace TtsBackup.Core.Services;

public interface IDiskSpaceService
{
    /// <summary>
    /// Gets the available free space (in bytes) for the drive containing the specified path.
    /// </summary>
    long GetAvailableFreeSpace(string path);
}
