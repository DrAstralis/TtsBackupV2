using TtsBackup.Core.Models;

namespace TtsBackup.Core.Services;

/// <summary>
/// Builds and manages the selection tree and selection snapshots.
/// </summary>
public interface IObjectTreeService
{
    IReadOnlyList<ObjectNode> BuildTree(SaveDocument document);

    SelectionSnapshot BuildSelectionSnapshot(
        IReadOnlyList<ObjectNode> tree,
        Func<ObjectNode, SelectionState> selectionResolver);
}