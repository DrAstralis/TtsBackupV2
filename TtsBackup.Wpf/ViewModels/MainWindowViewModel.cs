using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using TtsBackup.Core.Models;
using TtsBackup.Core.Services;

namespace TtsBackup.Wpf.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IAssetScanner _assetScanner;

    private string _title = "TTS Asset Backup";
    private string _statusText = "Ready.";
    private string _summaryText = "Open a TTS save file to see its objects.";
    
    private int _assetTotal;
    private int _assetInvalid;
    private int _assetLocal;
    private int _assetMissingFilename;
private string _urlSummaryText = string.Empty;

    public MainWindowViewModel(IAssetScanner assetScanner)
    {
        _assetScanner = assetScanner;
    }

    public ObservableCollection<ObjectTreeNodeViewModel> RootNodes { get; } = new();

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
        private set
        {
            if (_summaryText == value) return;
            _summaryText = value;
            OnPropertyChanged();
        }
    }

    public string UrlSummaryText
    {
        get => _urlSummaryText;
        private set
        {
            if (_urlSummaryText == value) return;
            _urlSummaryText = value;
            OnPropertyChanged();
        }
    }

    public int AssetTotal
    {
        get => _assetTotal;
        private set
        {
            if (_assetTotal == value) return;
            _assetTotal = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AssetScanOk));
            OnPropertyChanged(nameof(AssetScanHasWarnings));
        }
    }

    public int AssetInvalid
    {
        get => _assetInvalid;
        private set
        {
            if (_assetInvalid == value) return;
            _assetInvalid = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AssetScanOk));
            OnPropertyChanged(nameof(AssetScanHasWarnings));
        }
    }

    public int AssetLocal
    {
        get => _assetLocal;
        private set
        {
            if (_assetLocal == value) return;
            _assetLocal = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AssetScanOk));
            OnPropertyChanged(nameof(AssetScanHasWarnings));
        }
    }

    public int AssetMissingFilename
    {
        get => _assetMissingFilename;
        private set
        {
            if (_assetMissingFilename == value) return;
            _assetMissingFilename = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AssetScanOk));
            OnPropertyChanged(nameof(AssetScanHasWarnings));
        }
    }

    public bool AssetScanOk => AssetTotal > 0 && AssetInvalid == 0 && AssetLocal == 0 && AssetMissingFilename == 0;

    public bool AssetScanHasWarnings => AssetTotal > 0 && !AssetScanOk;

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task LoadDocumentAsync(string path, SaveDocument document, IReadOnlyList<ObjectNode> tree)
    {
        RootNodes.Clear();

        foreach (var node in tree)
        {
            var vm = ConvertNode(node, parent: null, ancestorLocked: false);
            RootNodes.Add(vm);
            WireNode(vm);
        }

        var totalObjects = CountNodes(tree);
        Title = $"TTS Asset Backup - {System.IO.Path.GetFileName(path)}";
        SummaryText = $"Objects in save: {totalObjects}. Select objects using the checkboxes.";

        // Phase 3: asset scan runs off-thread via IAssetScanner to keep UI responsive.
        StatusText = "Scanning for assets...";
        UrlSummaryText = "Scanning for assets...";
        await ScanAssetsAndApplyAsync(document);
        StatusText = "Save loaded.";
    }

    private async Task ScanAssetsAndApplyAsync(SaveDocument document)
    {
        IReadOnlyList<AssetReference> assets;
        try
        {
            // Empty selection snapshot means "scan everything" for analysis (see AssetScanner).
            assets = await _assetScanner.ScanAssetsAsync(document, new SelectionSnapshot(), CancellationToken.None);
        }
        catch (Exception ex)
        {
            UrlSummaryText = $"Asset scan failed: {ex.Message}";
            return;
        }

        var ownByGuid = assets
            .GroupBy(a => a.SourceObjectGuid)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        // Apply on UI thread.
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            foreach (var root in RootNodes)
            {
                ApplyOwnCountsRecursive(root, ownByGuid);
                ComputeAnyCountsRecursive(root);
            }

            AssetTotal = assets.Count;
            AssetLocal = assets.Count(a => LooksLikeLocalPath(a.OriginalUrl));
            AssetInvalid = assets.Count(a => IsInvalidUrl(a.OriginalUrl));
            AssetMissingFilename = assets.Count(a => IsMissingFilename(a.OriginalUrl));

            UrlSummaryText = $"Assets found: {AssetTotal} (invalid: {AssetInvalid}, local paths: {AssetLocal}, missing filenames: {AssetMissingFilename})";
        });
    }

    private static void ApplyOwnCountsRecursive(ObjectTreeNodeViewModel node, Dictionary<string, int> ownByGuid)
    {
        node.OwnUrlCount = ownByGuid.TryGetValue(node.Guid, out var c) ? c : 0;

        foreach (var child in node.Children)
            ApplyOwnCountsRecursive(child, ownByGuid);
    }

    private static int ComputeAnyCountsRecursive(ObjectTreeNodeViewModel node)
    {
        var sum = node.OwnUrlCount;
        foreach (var child in node.Children)
            sum += ComputeAnyCountsRecursive(child);

        node.AnyUrlCount = sum;
        return sum;
    }

    private static bool LooksLikeLocalPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        // Windows absolute drive path or UNC.
        if (value.Length >= 3 && char.IsLetter(value[0]) && value[1] == ':' && (value[2] == '\\' || value[2] == '/'))
            return true;

        if (value.StartsWith("\\\\", StringComparison.Ordinal))
            return true;

        return value.StartsWith("file:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInvalidUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (LooksLikeLocalPath(value)) return false;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return true;
        return uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps;
    }

    private static bool IsMissingFilename(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;

        var path = uri.AbsolutePath;
        if (string.IsNullOrWhiteSpace(path) || path.EndsWith('/')) return true;

        var last = path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (string.IsNullOrWhiteSpace(last)) return true;

        // If there's no dot, it's often an ID route or extensionless asset.
        return !last.Contains('.');
    }

    private void RecomputeSelectionSummary()
    {
        // Minimal: show how many objects are fully selected.
        var selected = 0;

        void Walk(ObjectTreeNodeViewModel n)
        {
            if (n.IsChecked == true && !n.IsSelectionLocked)
                selected++;

            foreach (var c in n.Children)
                Walk(c);
        }

        foreach (var r in RootNodes)
            Walk(r);

        SummaryText = $"Selected objects: {selected}."; 
    }

    private void WireNode(ObjectTreeNodeViewModel node)
    {
        node.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ObjectTreeNodeViewModel.IsChecked))
            {
                RecomputeSelectionSummary();
            }
        };

        foreach (var child in node.Children)
            WireNode(child);
    }

    private static ObjectTreeNodeViewModel ConvertNode(ObjectNode node, ObjectTreeNodeViewModel? parent, bool ancestorLocked)
    {
        var isLocked = ancestorLocked || node.IsState;

        var vm = new ObjectTreeNodeViewModel(parent)
        {
            Guid = node.Guid,
            Name = node.Name,
            Type = node.Type,
            HasStates = node.HasStates,
            JsonPath = node.JsonPath,
            IsSelectionLocked = isLocked
        };

        foreach (var child in node.Children)
        {
            vm.Children.Add(ConvertNode(child, vm, isLocked));
        }

        return vm;
    }

    private static int CountNodes(IReadOnlyList<ObjectNode> nodes)
    {
        var count = 0;
        foreach (var node in nodes)
        {
            count++;
            if (node.Children.Count > 0)
                count += CountNodes(node.Children);
        }
        return count;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
