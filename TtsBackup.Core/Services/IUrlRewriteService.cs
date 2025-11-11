using TtsBackup.Core.Models;

namespace TtsBackup.Core.Services;

public sealed record UrlRewriteRule(
    string? GlobalBaseUrl // v1 simple: one base URL; extended later
);

public interface IUrlRewriteService
{
    /// <summary>
    /// Applies URL rewrite rules to the save JSON and returns the updated JSON.
    /// </summary>
    Task<string> RewriteAsync(
        SaveDocument document,
        SelectionSnapshot selection,
        UrlRewriteRule rule,
        IReadOnlyDictionary<string, string> perAssetOverrides, // originalUrl -> newUrl
        CancellationToken cancellationToken = default);
}
