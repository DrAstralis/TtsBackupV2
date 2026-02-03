using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TtsBackup.Core.Services;
using TtsBackup.Infrastructure.Services;
using TtsBackup.Wpf.ViewModels;

namespace TtsBackup.Wpf;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder(e.Args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
            })
            .ConfigureServices((context, services) =>
            {
                // Core/Infra services
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton<IDiskSpaceService, DiskSpaceService>();
                services.AddSingleton<ISaveParser, SaveParser>();
                services.AddSingleton<IObjectTreeService, ObjectTreeService>();
                services.AddSingleton<IAssetScanner, AssetScanner>();

                // ViewModels + UI
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        _host.Start();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
