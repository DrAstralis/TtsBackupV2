namespace TtsBackup.Core.Models;

/// <summary>
/// Minimal internal representation of a save.
/// We don't mirror every TTS field, only what we care about.
/// </summary>
public sealed record SaveDocument(
    string RawJson,
    IReadOnlyList<ObjectNode> Roots,
    string? OriginalName
);

/// <summary>
/// Tree node for selection UI (bag/children/etc.).
/// </summary>
public sealed class ObjectNode
{
    public string Guid { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty; // e.g. "Bag", "DeckCustom", etc.
    public bool HasStates { get; init; }
    public IReadOnlyList<ObjectNode> Children { get; init; } = Array.Empty<ObjectNode>();

    /// <summary>
    /// JSONPath pointing to this object's JObject inside the original save JSON.
    /// Used for surgical patching in later phases.
    /// </summary>
    public string JsonPath { get; init; } = string.Empty;

    /// <summary>
    /// True if this node represents a TTS "state" object (i.e., came from parent.States.*).
    /// </summary>
    public bool IsState { get; init; }


    /// <summary>
    /// Indicates whether this node actually has any asset URLs of its own.
    /// Children may still have.
    /// </summary>
    public bool HasOwnAssets { get; init; }

}