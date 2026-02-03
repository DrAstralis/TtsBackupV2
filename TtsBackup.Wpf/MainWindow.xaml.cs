using System.Windows;
using Microsoft.Win32;
using TtsBackup.Core.Services;
using TtsBackup.Wpf.ViewModels;
using System.IO;

namespace TtsBackup.Wpf;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly ISaveParser _saveParser;
    private readonly IObjectTreeService _treeService;

    public MainWindow(MainWindowViewModel viewModel, ISaveParser saveParser, IObjectTreeService treeService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _saveParser = saveParser;
        _treeService = treeService;

        DataContext = _viewModel;
    }

    private async void OpenSave_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open TTS Save",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            _viewModel.StatusText = "Loading save...";
            var json = await File.ReadAllTextAsync(dialog.FileName);

            var doc = await _saveParser.ParseAsync(json);
            var tree = _treeService.BuildTree(doc);

            await _viewModel.LoadDocumentAsync(dialog.FileName, doc, tree);
            _viewModel.StatusText = "Save loaded.";
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = "Failed to load save.";
            MessageBox.Show(this, ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
