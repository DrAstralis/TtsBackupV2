namespace TtsBackup.Core.Models;

public sealed class ExportOptions
{
    public string OutputFolder { get; init; } = string.Empty;
    public string NewSaveName { get; init; } = string.Empty;

    public bool DownloadAssets { get; init; } = true;
    public bool CollapseSharedAssets { get; init; } = true;
    public bool RepositionObjects { get; init; } = false;
    public bool KeepEnvironment { get; init; } = true;

    public int MaxConcurrency { get; init; } = 8;
}