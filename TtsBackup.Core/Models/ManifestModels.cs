namespace TtsBackup.Core.Models;

public sealed class ExportManifest
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string OriginalSavePath { get; init; } = string.Empty;
    public string NewSavePath { get; init; } = string.Empty;

    public ExportOptions Options { get; init; } = new();
    public IReadOnlyList<AssetDownloadResult> Assets { get; init; } = Array.Empty<AssetDownloadResult>();
    public IReadOnlyList<ManifestObjectEntry> Objects { get; init; } = Array.Empty<ManifestObjectEntry>();
}

public sealed record ManifestObjectEntry(
    string Guid,
    string Name,
    IReadOnlyList<string> AssetOriginalUrls
);