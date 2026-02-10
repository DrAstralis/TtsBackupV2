using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TtsBackup.Wpf.ViewModels;

public sealed class ObjectTreeNodeViewModel : INotifyPropertyChanged
{
    private bool? _isChecked = false;
    private int _ownUrlCount;
    private int _anyUrlCount;

    public ObjectTreeNodeViewModel(ObjectTreeNodeViewModel? parent = null)
    {
        Parent = parent;
        Children.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasChildren));
    }

    public ObjectTreeNodeViewModel? Parent { get; }

    public string Guid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool HasStates { get; set; }
    public string JsonPath { get; set; } = string.Empty;

    /// <summary>
    /// True if this node is a TTS "state" object or is contained under a state object.
    /// State subtrees are implicitly included when the parent object is selected.
    /// </summary>
    public bool IsSelectionLocked { get; set; }

    public ObservableCollection<ObjectTreeNodeViewModel> Children { get; } = new();

    /// <summary>
    /// Optional callback used to confirm discarding pending edits when a user unchecks this node.
    /// Return true to allow the uncheck; false to cancel.
    /// </summary>
    public Func<ObjectTreeNodeViewModel, bool>? ConfirmUncheck { get; set; }


    /// <summary>
    /// True if this node has children.
    /// Used by the TreeView checkbox to enable tri-state behavior only for parents.
    /// Leaf nodes should be strictly two-state (checked/unchecked).
    /// </summary>
    public bool HasChildren => Children.Count > 0;

    /// <summary>
    /// Number of URLs found in this object's own fields (excluding ContainedObjects / States / ObjectStates).
    /// This is computed during the automatic scan that runs after loading a save.
    /// </summary>
    public int OwnUrlCount
    {
        get => _ownUrlCount;
        set
        {
            if (_ownUrlCount == value) return;
            _ownUrlCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasOwnUrls));
        }
    }

    /// <summary>
    /// Total URLs found in this object and all descendants.
    /// This is computed as OwnUrlCount + sum(children.AnyUrlCount).
    /// </summary>
    public int AnyUrlCount
    {
        get => _anyUrlCount;
        set
        {
            if (_anyUrlCount == value) return;
            _anyUrlCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasAnyUrls));
        }
    }

    public bool HasOwnUrls => OwnUrlCount > 0;
    public bool HasAnyUrls => AnyUrlCount > 0;

    /// <summary>
    /// Tri-state checkbox: true = selected, false = not selected, null = partially selected.
    /// For locked nodes, this mirrors the nearest selectable ancestor and cannot be toggled.
    /// </summary>
    public bool? IsChecked
    {
        get => _isChecked;
        set
        {
            if (IsSelectionLocked)
            {
                // Ignore user changes; locked nodes are driven by parent/ancestors.
                return;
            }

            // WPF cycles tri-state checkboxes as: true -> null -> false.
            // For this app, "partial" (null) is a derived/read-only state that only appears
            // when children differ. A user click should never land on null.
            //
            // Desired click behavior:
            // - Checked parent click => Unchecked (not Partial)
            // - Partial parent click => Checked (select all)
            // - Leaf nodes are two-state only (never Partial)
            var coerced = value;
            if (!HasChildren)
            {
                coerced = value ?? false;
            }
            else
            {
                if (value == null)
                {
                    // true -> null is treated as uncheck.
                    coerced = false;
                }
                else if (_isChecked == null && value == false)
                {
                    // null -> false is treated as check-all.
                    coerced = true;
                }
            }

            if (_isChecked == coerced) return;

            // If the user is unchecking this node, confirm discarding any pending edits
            // for this subtree before we cascade the deselection.
            if (_isChecked == true && coerced == false)
            {
                var allow = ConfirmUncheck?.Invoke(this) ?? true;
                if (!allow)
                {
                    // Re-affirm the existing value so the UI stays checked.
                    OnPropertyChanged(nameof(IsChecked));
                    return;
                }
            }

            _isChecked = coerced;
            OnPropertyChanged();

            // Propagate to children.
            foreach (var child in Children)
            {
                child.SetFromAncestor(coerced);
            }

            // Recompute parents.
            Parent?.RecomputeFromChildren();
        }
    }

    /// <summary>
    /// Set this node's checkbox based on an ancestor selection change.
    /// This method works for both selectable and locked nodes.
    /// </summary>
    public void SetFromAncestor(bool? ancestorValue)
    {
        var newValue = ancestorValue ?? false;

        if (_isChecked == newValue) return;
        _isChecked = newValue;
        OnPropertyChanged(nameof(IsChecked));

        foreach (var child in Children)
        {
            child.SetFromAncestor(newValue);
        }
    }

    private void RecomputeFromChildren()
    {
        if (IsSelectionLocked)
        {
            // Locked nodes mirror their ancestor; don't compute a partial state from children.
            Parent?.RecomputeFromChildren();
            return;
        }

        if (Children.Count == 0)
        {
            Parent?.RecomputeFromChildren();
            return;
        }

        var anyTrue = false;
        var anyFalse = false;
        var anyNull = false;

        foreach (var child in Children)
        {
            if (child.IsChecked == true) anyTrue = true;
            else if (child.IsChecked == false) anyFalse = true;
            else anyNull = true;
        }

        bool? computed;
        if (anyNull || (anyTrue && anyFalse)) computed = null;
        else if (anyTrue) computed = true;
        else computed = false;

        if (_isChecked != computed)
        {
            _isChecked = computed;
            OnPropertyChanged(nameof(IsChecked));
        }

        Parent?.RecomputeFromChildren();
    }

    public string DisplayName
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(Name) ? "(unnamed object)" : Name;
            if (!string.IsNullOrWhiteSpace(Type))
            {
                name += $" [{Type}]";
            }

            if (HasStates)
            {
                name += " (states)";
            }

            if (IsSelectionLocked)
            {
                name += " (auto)";
            }

            return name;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
