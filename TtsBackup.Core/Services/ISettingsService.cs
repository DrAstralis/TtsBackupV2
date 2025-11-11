using TtsBackup.Core.Models;

namespace TtsBackup.Core.Services;

public interface ISettingsService
{
    AppSettings Current { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}

public sealed class AppSettings
{
    public string LastSavePath { get; set; } = string.Empty;
    public string LastOutputFolder { get; set; } = string.Empty;

    public bool DownloadAssetsByDefault { get; set; } = true;
    public bool CollapseSharedAssetsByDefault { get; set; } = true;
    public bool RepositionObjectsByDefault { get; set; } = false;
    public bool KeepEnvironmentByDefault { get; set; } = true;

    public int MaxConcurrency { get; set; } = 8;
}