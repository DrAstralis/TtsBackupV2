using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TtsBackup.Wpf.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private string _title = "TTS Asset Backup";

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

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
