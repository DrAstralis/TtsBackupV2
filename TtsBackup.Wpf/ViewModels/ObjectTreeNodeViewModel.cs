using System.Collections.ObjectModel;

namespace TtsBackup.Wpf.ViewModels;

public sealed class ObjectTreeNodeViewModel
{
    public string Guid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool HasStates { get; set; }

    public ObservableCollection<ObjectTreeNodeViewModel> Children { get; } = new();

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

            return name;
        }
    }
}
