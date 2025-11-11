using TtsBackup.Core.Models;

namespace TtsBackup.Core.Services;

/// <summary>
/// Orchestrates the whole export workflow (without UI).
/// </summary>
public interface IExportService
{
    Task<ExportManifest> ExportAsync(
        string originalSavePath,
        string rawJson,
        SaveDocument document,
        SelectionSnapshot selection,
        UrlRewriteRule rewriteRule,
        ExportOptions options,
        CancellationToken cancellationToken = default);
}
