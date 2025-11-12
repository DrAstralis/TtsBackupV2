using TtsBackup.Core.Models;
using TtsBackup.Core.Services;

namespace TtsBackup.Infrastructure.Services;

public sealed class ObjectTreeService : IObjectTreeService
{
    public IReadOnlyList<ObjectNode> BuildTree(SaveDocument document)
    {
        return document.Roots;
    }

    public SelectionSnapshot BuildSelectionSnapshot(
        IReadOnlyList<ObjectNode> tree,
        Func<ObjectNode, SelectionState> selectionResolver)
    {
        var selected = new List<SelectedObject>();

        void Walk(ObjectNode node, bool includeChildren)
        {
            var state = selectionResolver(node);
            if (state == SelectionState.Selected)
            {
                selected.Add(new SelectedObject(
                    node.Guid,
                    node.Name,
                    IncludeChildren: true,
                    IncludeStates: true));
            }

            foreach (var child in node.Children)
            {
                Walk(child, includeChildren: true);
            }
        }

        foreach (var root in tree)
        {
            Walk(root, includeChildren: true);
        }

        return new SelectionSnapshot
        {
            SelectedObjects = selected
        };
    }
}
