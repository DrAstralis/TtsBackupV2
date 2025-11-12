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

        foreach (var node in tree)
        {
            RootNodes.Add(ConvertNode(node));
        }

        var totalObjects = CountNodes(tree);
        Title = $"TTS Asset Backup - {System.IO.Path.GetFileName(path)}";
        SummaryText = $"Objects in save: {totalObjects}. (Selection & assets coming in later phases.)";
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

    private static ObjectTreeNodeViewModel ConvertNode(ObjectNode node)
    {
        var vm = new ObjectTreeNodeViewModel
        {
            Guid = node.Guid,
            Name = node.Name,
            Type = node.Type,
            HasStates = node.HasStates
        };

        foreach (var child in node.Children)
        {
            vm.Children.Add(ConvertNode(child));
        }

        return vm;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
