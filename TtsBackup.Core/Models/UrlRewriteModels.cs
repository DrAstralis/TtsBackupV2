namespace TtsBackup.Core.Models;

/// <summary>
/// Legacy simple rewrite rule used by early export orchestration.
/// Kept to avoid breaking existing contracts; Phase 4 uses <see cref="UrlRewriteRequest"/>.
/// </summary>
public sealed record UrlRewriteRule(
    string? GlobalBaseUrl
);

/// <summary>
/// Planned single-value replacement for a specific JSON field.
/// The patcher applies these changes surgically (no full re-serialization).
/// </summary>
public sealed record UrlPatchInstruction(
    string ObjectGuid,
    string JsonPath,
    string? OriginalValue,
    string NewValue
);

/// <summary>
/// User rewrite settings for Phase 4.
/// Overrides are keyed by the target JSON path (JToken.Path) or other stable key.
/// </summary>
public sealed record UrlRewriteRequest(
    string? BaseUrl,
    IReadOnlyDictionary<string, string> OverridesByPath
);
