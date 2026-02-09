using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using TtsBackup.Wpf.Commands;

namespace TtsBackup.Wpf.ViewModels;

public sealed class IncludedNodeRowViewModel : INotifyPropertyChanged
{
    private bool _isEditing;
    private bool _fieldsLoaded;
    private bool _hasOverrides;
    private readonly Func<ObservableCollection<EditableFieldViewModel>> _loadFields;

    public IncludedNodeRowViewModel(
        string guid,
        string name,
        string type,
        int depth,
        bool isAutoIncluded,
        Func<ObservableCollection<EditableFieldViewModel>> loadFields)
    {
        Guid = guid;
        Name = name;
        Type = type;
        Depth = depth;
        IsAutoIncluded = isAutoIncluded;
        _loadFields = loadFields;

        BeginEditCommand = new RelayCommand(BeginEdit, () => !IsEditing);
        SaveCommand = new RelayCommand(Save, () => IsEditing);
        CancelCommand = new RelayCommand(Cancel, () => IsEditing);
        GenerateFilenameCommand = new RelayCommand<EditableFieldViewModel>(GenerateFilename, f => IsEditing && f is not null && f.IsFilenameField);
        ResetFieldToDefaultCommand = new RelayCommand<EditableFieldViewModel>(ResetFieldToDefault, f => IsEditing && f is not null && f.IsEditable && f.IsOverridden);
    }

    public string Guid { get; }
    public string Name { get; }
    public string Type { get; }
    public int Depth { get; }
    public bool IsAutoIncluded { get; }

    public string HeaderText
    {
        get
        {
            var n = string.IsNullOrWhiteSpace(Name) ? "(unnamed object)" : Name;
            var t = string.IsNullOrWhiteSpace(Type) ? string.Empty : $" [{Type}]";
            var auto = IsAutoIncluded ? " (auto)" : string.Empty;
            return $"{n}{t} - {Guid}{auto}";
        }
    }

    public ObservableCollection<EditableFieldViewModel> Fields { get; } = new();

    public bool HasOverrides
    {
        get => _hasOverrides;
        private set
        {
            if (_hasOverrides == value) return;
            _hasOverrides = value;
            OnPropertyChanged();
        }
    }

    public bool HasFieldsLoaded
    {
        get => _fieldsLoaded;
        private set
        {
            if (_fieldsLoaded == value) return;
            _fieldsLoaded = value;
            OnPropertyChanged();
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (_isEditing == value) return;
            _isEditing = value;
            // Propagate edit state to child field view models so XAML doesn't need ancestor bindings.
            foreach (var f in Fields)
                f.IsParentEditing = value;
            OnPropertyChanged();
            BeginEditCommand.RaiseCanExecuteChanged();
            SaveCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();
            GenerateFilenameCommand.RaiseCanExecuteChanged();
            ResetFieldToDefaultCommand.RaiseCanExecuteChanged();
        }
    }

    public RelayCommand BeginEditCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand<EditableFieldViewModel> GenerateFilenameCommand { get; }
    public RelayCommand<EditableFieldViewModel> ResetFieldToDefaultCommand { get; }

    private void BeginEdit()
    {
        EnsureFieldsLoaded();
        foreach (var f in Fields)
            f.BeginEdit();

        IsEditing = true;
    }

    private void EnsureFieldsLoaded()
    {
        if (HasFieldsLoaded) return;

        var loaded = _loadFields();
        Fields.Clear();
        foreach (var f in loaded)
        {
            f.PropertyChanged += Field_PropertyChanged;
            Fields.Add(f);
        }

        HasFieldsLoaded = true;
        RefreshHasOverrides();
    }

    private void Save()
    {
        foreach (var f in Fields.Where(x => x.IsEditable))
            f.CommitEdit();

        IsEditing = false;
        RefreshHasOverrides();
    }

    private void Cancel()
    {
        foreach (var f in Fields)
            f.CancelEdit();

        IsEditing = false;
        RefreshHasOverrides();
    }

    private void GenerateFilename(EditableFieldViewModel? field)
    {
        if (field is null) return;
        if (!field.IsFilenameField) return;

        // GUID only for now. Extension inference comes later (downloader response).
        field.EditValue = System.Guid.NewGuid().ToString("N");
    }

    private void ResetFieldToDefault(EditableFieldViewModel? field)
    {
        if (field is null) return;
        if (!field.IsEditable) return;

        field.ResetEditToDefault();
        RefreshHasOverrides();
    }

    private void Field_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Keep the row-level "modified" badge accurate.
        if (e.PropertyName is nameof(EditableFieldViewModel.IsOverridden) or nameof(EditableFieldViewModel.Value))
        {
            RefreshHasOverrides();
            // Enable/disable the per-field Default button as the user edits.
            ResetFieldToDefaultCommand.RaiseCanExecuteChanged();
        }
    }

    private void RefreshHasOverrides()
        => HasOverrides = Fields.Any(f => f.IsOverridden);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
