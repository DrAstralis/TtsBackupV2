using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TtsBackup.Core.Models;
using TtsBackup.Core.Services;

namespace TtsBackup.Wpf.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IAssetScanner _assetScanner;

    private SaveDocument? _currentDocument;
    private JObject? _currentRootToken;

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

    public ObservableCollection<IncludedNodeRowViewModel> IncludedNodes { get; } = new();

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

        _currentDocument = document;
        _currentRootToken = ParseRootToken(document.RawJson);
        IncludedNodes.Clear();


        foreach (var node in tree)
        {
            var vm = ConvertNode(node, parent: null, ancestorLocked: false);
            RootNodes.Add(vm);
            WireNode(vm);
        }

        RebuildIncludedNodes();

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
        node.ConfirmUncheck = ConfirmUncheckNode;

        node.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ObjectTreeNodeViewModel.IsChecked))
            {
                DebounceSelectionRefresh();
            }
        };

        foreach (var child in node.Children)
            WireNode(child);
    }

    
    private bool ConfirmUncheckNode(ObjectTreeNodeViewModel node)
    {
        // Phase 3 behavior: edits are intent-only and live in the right panel rows.
        // If the user unselects a node, the corresponding rows will disappear and edits will be lost.
        if (!SubtreeHasPendingEdits(node))
            return true;

        var name = string.IsNullOrWhiteSpace(node.Name) ? "(unnamed object)" : node.Name;
        var msg = $"Unselecting '{name}' will discard pending edits for this object (and any selected children).\n\nContinue?";
        var result = MessageBox.Show(msg, "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        return result == MessageBoxResult.Yes;
    }

    private bool SubtreeHasPendingEdits(ObjectTreeNodeViewModel root)
    {
        if (IncludedNodes.Count == 0)
            return false;

        var dirtyGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in IncludedNodes)
        {
            if (row.HasOverrides)
                dirtyGuids.Add(row.Guid);
        }

        if (dirtyGuids.Count == 0)
            return false;

        static IEnumerable<ObjectTreeNodeViewModel> Flatten(ObjectTreeNodeViewModel n)
        {
            yield return n;
            foreach (var c in n.Children)
                foreach (var cc in Flatten(c))
                    yield return cc;
        }

        foreach (var n in Flatten(root))
        {
            if (dirtyGuids.Contains(n.Guid))
                return true;
        }

        return false;
    }


private System.Windows.Threading.DispatcherTimer? _selectionDebounce;

    private void DebounceSelectionRefresh()
    {
        // Selection cascades cause many IsChecked changes; debounce to keep UI responsive.
        _selectionDebounce ??= new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };

        _selectionDebounce.Stop();
        _selectionDebounce.Tick -= SelectionDebounceTick;
        _selectionDebounce.Tick += SelectionDebounceTick;
        _selectionDebounce.Start();
    }

    private void SelectionDebounceTick(object? sender, EventArgs e)
    {
        if (_selectionDebounce is not null)
        {
            _selectionDebounce.Stop();
            _selectionDebounce.Tick -= SelectionDebounceTick;
        }

        RecomputeSelectionSummary();
        RebuildIncludedNodes();
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


    private void RebuildIncludedNodes()
    {
        IncludedNodes.Clear();
        if (_currentRootToken is null) return;

        static IEnumerable<ObjectTreeNodeViewModel> Flatten(ObjectTreeNodeViewModel n)
        {
            yield return n;
            foreach (var c in n.Children)
                foreach (var cc in Flatten(c))
                    yield return cc;
        }

        var anySelected = RootNodes.SelectMany(r => Flatten(r)).Any(n => !n.IsSelectionLocked && n.IsChecked == true);
        if (!anySelected)
            return; // Only show included nodes when the user has selected something.

        foreach (var root in RootNodes)
            Walk(root, depth: 0);

        void Walk(ObjectTreeNodeViewModel node, int depth)
        {
            if (node.IsChecked != false)
            {
                var row = BuildRow(node, depth);
                if (row is not null)
                    IncludedNodes.Add(row);
            }

            foreach (var child in node.Children)
                Walk(child, depth + 1);
        }
    }

    private IncludedNodeRowViewModel? BuildRow(ObjectTreeNodeViewModel node, int depth)
    {
        if (_currentRootToken is null) return null;
        if (string.IsNullOrWhiteSpace(node.JsonPath)) return null;

        var token = _currentRootToken.SelectToken(NormalizeJsonPathForSelectToken(node.JsonPath), errorWhenNoMatch: false);
        if (token is null) return null;

        // NOTE: Extracting all scalar fields can be expensive on large objects (scripts, decks, etc.).
        // We load fields lazily when the user clicks Edit for a row.
        var row = new IncludedNodeRowViewModel(
            guid: node.Guid,
            name: node.Name,
            type: node.Type,
            depth: depth,
            isAutoIncluded: node.IsSelectionLocked,
            loadFields: () => ExtractScalarFields(token));

        return row;
    }

    private static ObservableCollection<EditableFieldViewModel> ExtractScalarFields(JToken objectToken)
    {
        var fields = new ObservableCollection<EditableFieldViewModel>();
        
        void WalkToken(JToken t, string prefix, string leafNameHint)
        {
            switch (t)
            {
                case JValue v:
                    if (v.Type == JTokenType.Null)
                    {
                        fields.Add(new EditableFieldViewModel(
                            path: prefix,
                            displayName: prefix,
                            value: string.Empty,
                            isUrlField: false,
                            isFilenameField: false,
                            isEditable: true,
                            isBooleanField: false,
                            urlGroupId: -1));
                        return;
                    }

                    // Treat everything scalar as editable text for now (validation stub).
                    var isBooleanToken = v.Type == JTokenType.Boolean;
                    var str = isBooleanToken
                        ? (v.Value<bool>() ? "true" : "false")
                        : (v.Value?.ToString() ?? string.Empty);
                    var leaf = leafNameHint;
                    var isUrlField = leaf.EndsWith("URL", StringComparison.OrdinalIgnoreCase) || leaf.EndsWith("Url", StringComparison.OrdinalIgnoreCase);
                    fields.Add(new EditableFieldViewModel(
                        path: prefix,
                        displayName: prefix,
                        value: str,
                        isUrlField: isUrlField,
                        isFilenameField: false,
                        isEditable: true,
                        isBooleanField: isBooleanToken,
                        urlGroupId: -1));

                    if (isUrlField)
                    {
                        var filename = TryExtractFilename(str);
                        fields.Add(new EditableFieldViewModel(
                            path: prefix + "#Filename",
                            displayName: prefix + " (Filename)",
                            value: filename ?? string.Empty,
                            isUrlField: false,
                            isFilenameField: true,
                            isEditable: true,
                            isBooleanField: false,
                            urlGroupId: -1));

                        
                    }
                    break;

                case JObject o:
                    foreach (var p in o.Properties())
                    {
                        if (IsStructuralContainer(p.Name))
                            continue;

                        var childPrefix = string.IsNullOrWhiteSpace(prefix) ? p.Name : prefix + "." + p.Name;
                        WalkToken(p.Value, childPrefix, p.Name);
                    }
                    break;

                case JArray a:
                    // Only expand arrays of scalars to avoid JSON editing.
                    var allScalar = a.All(x => x is JValue);
                    if (!allScalar) return;

                    for (var i = 0; i < a.Count; i++)
                    {
                        var idxName = $"{leafNameHint}[{i}]";
                        var childPrefix = $"{prefix}[{i}]";
                        WalkToken(a[i], childPrefix, idxName);
                    }
                    break;
            }
        }

        // Start from object root: expose scalar properties anywhere inside, excluding structural containers.
        WalkToken(objectToken, prefix: string.Empty, leafNameHint: "root");

        // Sort: keep root out, keep stable.
        var ordered = fields
            .Where(f => f.DisplayName != "root")
            .OrderBy(f => f.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Assign URL group striping in *visual* order so each URL+Filename pair shares a color,
        // and the colors alternate per URL set (not per row / not affected by non-URL fields).
        var byPath = ordered.ToDictionary(f => f.Path, StringComparer.OrdinalIgnoreCase);
        var urlSetIndex = 0;
        foreach (var f in ordered)
        {
            if (!f.IsUrlField) continue;

            f.SetUrlGroupId(urlSetIndex);
            var filenamePath = f.Path + "#Filename";
            if (byPath.TryGetValue(filenamePath, out var fn))
            {
                fn.SetUrlGroupId(urlSetIndex);
            }

            urlSetIndex++;
        }


        return new ObservableCollection<EditableFieldViewModel>(ordered);
    }

    private static bool IsStructuralContainer(string name)
        => name.Equals("ContainedObjects", StringComparison.OrdinalIgnoreCase)
           || name.Equals("States", StringComparison.OrdinalIgnoreCase)
           || name.Equals("ObjectStates", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeJsonPathForSelectToken(string jsonPath)
    {
        var p = jsonPath.Trim();
        if (p == "$") return "$";
        if (p.StartsWith("$."))
            return p;
        if (p.StartsWith("$["))
            return p;
        if (p.StartsWith("."))
            return "$" + p;
        return "$." + p;
    }

    private static JObject? ParseRootToken(string rawJson)
    {
        try
        {
            return JObject.Parse(rawJson);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryExtractFilename(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            var path = uri.AbsolutePath;
            if (string.IsNullOrWhiteSpace(path) || path.EndsWith('/')) return null;
            var last = path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            if (string.IsNullOrWhiteSpace(last)) return null;
            // Require dot to treat as a filename in v1.
            if (!last.Contains('.')) return null;
            return last;
        }

        // Local path guess (treat backslashes as path separators).
        var parts = value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var leaf = parts.LastOrDefault();
        if (string.IsNullOrWhiteSpace(leaf)) return null;
        return leaf.Contains('.') ? leaf : null;
    }


    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
