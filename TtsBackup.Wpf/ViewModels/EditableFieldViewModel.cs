using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TtsBackup.Wpf.ViewModels;

public sealed class EditableFieldViewModel : INotifyPropertyChanged
{
    private string _value;
    private string _editValue;
    private bool _isParentEditing;

    public EditableFieldViewModel(
        string path,
        string displayName,
        string value,
        bool isUrlField,
        bool isFilenameField,
        bool isEditable)
    {
        Path = path;
        DisplayName = displayName;
        _value = value;
        _editValue = value;
        IsUrlField = isUrlField;
        IsFilenameField = isFilenameField;
        IsEditable = isEditable;
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
        }
    }

    public bool IsUrlField { get; }
    public bool IsFilenameField { get; }
    public bool IsEditable { get; }

    /// <summary>Whether the owning row is currently in edit mode.</summary>
    public bool IsParentEditing
    {
        get => _isParentEditing;
        set
        {
            if (_isParentEditing == value) return;
            _isParentEditing = value;
            OnPropertyChanged();
        }
    }

    public bool NeedsFilename => IsFilenameField && string.IsNullOrWhiteSpace(Value);

    public void BeginEdit() => EditValue = Value;

    public void CommitEdit() => Value = EditValue;

    public void CancelEdit() => EditValue = Value;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
