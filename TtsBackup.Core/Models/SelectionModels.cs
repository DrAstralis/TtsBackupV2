namespace TtsBackup.Core.Models;

public enum SelectionState
{
    Unselected = 0,
    Selected,
    Partial
}

/// <summary>
/// Represents the current selection set (tree + checkbox states).
/// </summary>
public sealed class SelectionSnapshot
{
    public IReadOnlyList<SelectedObject> SelectedObjects { get; init; } = Array.Empty<SelectedObject>();
}

public sealed record SelectedObject(
    string Guid,
    string Name,
    bool IncludeChildren,
    bool IncludeStates // always true in our design, but kept for clarity/extension
);