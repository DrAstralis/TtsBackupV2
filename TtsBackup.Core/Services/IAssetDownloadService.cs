using TtsBackup.Core.Models;

namespace TtsBackup.Core.Services;

public interface IAssetDownloadService
{
    Task<IReadOnlyList<AssetDownloadResult>> DownloadAsync(
        IReadOnlyList<AssetReference> assets,
        ExportOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
