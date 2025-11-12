using System.Text.Json;
using TtsBackup.Core.Services;

namespace TtsBackup.Infrastructure.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public AppSettings Current { get; private set; } = new();

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "TtsBackup");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
            return;

        await using var stream = File.OpenRead(_settingsPath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options, cancellationToken);
        if (settings is not null)
        {
            Current = settings;
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);

        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, Current, Options, cancellationToken);
    }
}
