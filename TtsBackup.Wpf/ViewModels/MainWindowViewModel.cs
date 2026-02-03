using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TtsBackup.Core.Models;

namespace TtsBackup.Wpf.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private string _title = "TTS Asset Backup";
    private string _statusText = "Ready.";
    private string _summaryText = "Open a TTS save file to see its objects.";

    public string Title
    {
        get => _title;
        set
        {
            if (_title == value) return;
            _title = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText == value) return;
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public string SummaryText
    {
        get => _summaryText;
        set
        {
            if (_summaryText == value) return;
            _summaryText = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<ObjectTreeNodeViewModel> RootNodes { get; } = new();

    public void LoadDocument(string path, SaveDocument document, IReadOnlyList<ObjectNode> tree)
    {
        RootNodes.Clear();

        // Build VMs with "state-subtree selection lock" semantics.
        foreach (var node in tree)
        {
            var vm = ConvertNode(node, parent: null, ancestorLocked: false);
            RootNodes.Add(vm);

            // Hook PropertyChanged so changes to IsChecked update the summary.
            WireNode(vm);
        }

        var totalObjects = CountNodes(tree);
        Title = $"TTS Asset Backup - {System.IO.Path.GetFileName(path)}";
        SummaryText = $"Objects in save: {totalObjects}. Select objects using the checkboxes.";

        // Initialize summary based on default check state.
        RecomputeSelectionSummary();
    }

    private static int CountNodes(IReadOnlyList<ObjectNode> nodes)
    {
        var count = 0;
        foreach (var node in nodes)
        {
            count++;
            if (node.Children.Count > 0)
            {
                count += CountNodes(node.Children);
            }
        }
        return count;
    }

    /// <summary>
    /// Recompute summary text based on current checkbox states.
    /// </summary>
    private void RecomputeSelectionSummary()
    {
        var total = 0;
        var selected = 0;
        var partial = 0;

        foreach (var root in RootNodes)
        {
            CountVm(root, ref total, ref selected, ref partial);
        }

        SummaryText = partial > 0
            ? $"Selected objects: {selected} / {total} (plus {partial} partial branches)."
            : $"Selected objects: {selected} / {total}.";
    }

    private static void CountVm(ObjectTreeNodeViewModel node, ref int total, ref int selected, ref int partial)
    {
        total++;
        if (node.IsChecked == true) selected++;
        else if (node.IsChecked == null) partial++;

        foreach (var child in node.Children)
        {
            CountVm(child, ref total, ref selected, ref partial);
        }
    }

    /// <summary>
    /// Subscribe to IsChecked changes for this node and all descendants.
    /// </summary>
    private void WireNode(ObjectTreeNodeViewModel node)
    {
        node.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ObjectTreeNodeViewModel.IsChecked))
            {
                RecomputeSelectionSummary();
            }
        };

        foreach (var child in node.Children)
        {
            WireNode(child);
        }
    }

    /// <summary>
    /// Convert the parsed ObjectNode tree into a VM tree.
    /// The key P3 rule here:
    /// - If a node is a "state" OR is under a state, it is selection-locked (checkbox disabled).
    /// - Locked nodes are still shown and included when the parent is selected.
    /// </summary>
    private static ObjectTreeNodeViewModel ConvertNode(ObjectNode node, ObjectTreeNodeViewModel? parent, bool ancestorLocked)
    {
        // The important rule you called out:
        // states are always included and can't be toggled independently.
        var locked = ancestorLocked || node.IsState;

        var vm = new ObjectTreeNodeViewModel(parent)
        {
            Guid = node.Guid,
            Name = node.Name,
            Type = node.Type,
            HasStates = node.HasStates,
            JsonPath = node.JsonPath,
            IsSelectionLocked = locked
        };

        foreach (var child in node.Children)
        {
            vm.Children.Add(ConvertNode(child, vm, locked));
        }

        return vm;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
