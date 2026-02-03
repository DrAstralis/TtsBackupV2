using TtsBackup.Core.Models;

namespace TtsBackup.Core.Services;

/// <summary>
/// Finds all asset URLs in the selected subset of objects.
/// </summary>
public interface IAssetScanner
{
    Task<IReadOnlyList<AssetReference>> ScanAssetsAsync(
        SaveDocument document,
        SelectionSnapshot selection,
        CancellationToken cancellationToken = default);
}
