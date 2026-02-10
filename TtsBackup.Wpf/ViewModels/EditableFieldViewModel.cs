using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TtsBackup.Wpf.ViewModels;

public sealed class EditableFieldViewModel : INotifyPropertyChanged
{
    private string _value;
    private string _editValue;
    private bool _isParentEditing;
    private int _urlGroupId;
    private readonly string _originalValue;

    public EditableFieldViewModel(
        string path,
        string displayName,
        string value,
        bool isUrlField,
        bool isFilenameField,
        bool isEditable,
        bool isBooleanField,
        int urlGroupId = -1)
    {
        Path = path;
        DisplayName = displayName;
        _originalValue = value;
        _value = value;
        _editValue = value;
        IsUrlField = isUrlField;
        IsFilenameField = isFilenameField;
        IsEditable = isEditable;
        IsBooleanField = isBooleanField;
        _urlGroupId = urlGroupId;
    }

    public string Path { get; }
    public string DisplayName { get; }

    /// <summary>Committed value (currently effective in memory).</summary>
    public string Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOverridden));
            OnPropertyChanged(nameof(NeedsFilename));
        }
    }

    /// <summary>Working edit value while a node row is in edit mode.</summary>
    public string EditValue
    {
        get => _editValue;
        set
        {
            if (_editValue == value) return;
            _editValue = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOverridden));
            OnPropertyChanged(nameof(NeedsFilename));
        }
    }

    public bool IsUrlField { get; }
    public bool IsFilenameField { get; }
    public bool IsEditable { get; }
    public bool IsBooleanField { get; }

    public int UrlGroupId
    {
        get => _urlGroupId;
        private set
        {
            if (_urlGroupId == value) return;
            _urlGroupId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsUrlGroupMember));
            OnPropertyChanged(nameof(IsUrlGroupStripeA));
        }
    }

    
    public void SetUrlGroupId(int urlGroupId)
    {
        UrlGroupId = urlGroupId;
    }

public bool IsUrlGroupMember => UrlGroupId >= 0;

    public bool IsUrlGroupStripeA => IsUrlGroupMember && (UrlGroupId % 2 == 0);


    /// <summary>Original value captured from the loaded save (used for "Default").</summary>
    public string OriginalValue => _originalValue;

    public bool IsOverridden => IsParentEditing
        ? !string.Equals(EditValue, OriginalValue, StringComparison.Ordinal)
        : !string.Equals(Value, OriginalValue, StringComparison.Ordinal);

    /// <summary>Whether the owning row is currently in edit mode.</summary>
    public bool IsParentEditing
    {
        get => _isParentEditing;
        set
        {
            if (_isParentEditing == value) return;
            _isParentEditing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOverridden));
            OnPropertyChanged(nameof(NeedsFilename));
        }
    }

    public bool NeedsFilename => IsFilenameField && string.IsNullOrWhiteSpace(IsParentEditing ? EditValue : Value);

    public void BeginEdit() => EditValue = Value;

    public void CommitEdit() => Value = EditValue;

    public void CancelEdit() => EditValue = Value;

    public void ResetEditToDefault() => EditValue = OriginalValue;

    /// <summary>Revert this field fully back to its original value (clears any overrides).</summary>
    public void RevertToDefault()
    {
        Value = OriginalValue;
        EditValue = OriginalValue;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
