using TtsBackup.Core.Models;

namespace TtsBackup.Core.Services;

public interface IManifestService
{
    Task WriteManifestAsync(
        ExportManifest manifest,
        string outputFolder,
        CancellationToken cancellationToken = default);
}
