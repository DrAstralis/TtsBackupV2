using TtsBackup.Core.Models;

namespace TtsBackup.Core.Services;

/// <summary>
/// Phase 4 contract: plans surgical URL patches (does not mutate JSON).
/// A separate patcher will apply these instructions to the parsed JToken tree.
/// </summary>
public interface IUrlRewriteService
{
    /// <summary>
    /// Creates a list of URL changes to apply.
    ///
    /// Inputs are the scanned assets (already limited to the currently included nodes)
    /// and the user's rewrite settings.
    /// </summary>
    Task<IReadOnlyList<UrlPatchInstruction>> PlanPatchesAsync(
        IReadOnlyList<AssetReference> assets,
        UrlRewriteRequest request,
        CancellationToken cancellationToken = default);
}
