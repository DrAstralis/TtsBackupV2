using System.Windows;
using TtsBackup.Wpf.ViewModels;

namespace TtsBackup.Wpf;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
