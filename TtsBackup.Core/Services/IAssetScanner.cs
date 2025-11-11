using TtsBackup.Core.Models;

namespace TtsBackup.Core.Services;

/// <summary>
/// Finds all asset URLs in the selected subset of objects.
/// </summary>
public interface IAssetScanner
{
    IReadOnlyList<AssetReference> ScanAssets(
        SaveDocument document,
        SelectionSnapshot selection);
}
