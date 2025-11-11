namespace TtsBackup.Core.Models;

public enum AssetType
{
    Unknown = 0,
    Image,
    Mesh,
    AssetBundle,
    DeckFace,
    DeckBack,
    Decal,
    UiAsset,
    EnvironmentTable,
    EnvironmentSky,
    EnvironmentLut
}

public sealed record AssetReference(
    string OriginalUrl,
    AssetType Type,
    string? InferredExtension,
    string SourceObjectGuid,
    string SourceObjectName,
    string FieldPath
);

public sealed record AssetDownloadResult(
    AssetReference Asset,
    string? LocalPath,
    string? Hash,
    AssetStatus Status,
    string? Error
);

public enum AssetStatus
{
    Pending = 0,
    Downloaded,
    ReusedFromCache,
    SkippedDuplicate,
    Failed,
    LocalPathWarning
}