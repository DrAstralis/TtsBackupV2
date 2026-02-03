using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TtsBackup.Core.Models;

namespace TtsBackup.Wpf.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private static readonly Regex UrlRegex = new Regex(
    "https?://[^\\s\"'<>]+",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);


    private string _title = "TTS Asset Backup";
    private string _statusText = "Ready.";
    private string _summaryText = "Open a TTS save file to see its objects.";
    private string _urlSummaryText = string.Empty;

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

    public string UrlSummaryText
    {
        get => _urlSummaryText;
        set
        {
            if (_urlSummaryText == value) return;
            _urlSummaryText = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<ObjectTreeNodeViewModel> RootNodes { get; } = new();

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

        // Scan for URLs immediately on load (no 'Scan' button).
        // This runs off-thread to avoid freezing the UI on large saves.
        StatusText = "Scanning for URLs...";
        UrlSummaryText = "Scanning for URLs...";
        await ScanUrlsAndApplyAsync(document.RawJson);
        StatusText = "Save loaded.";
    }

    private async Task ScanUrlsAndApplyAsync(string rawJson)
    {
        // 1) Parse the root token once using JsonTextReader (Infinity-safe).
        // 2) For each node VM, compute OwnUrlCount by scanning the token for URLs,
        //    excluding big child containers so parent nodes don't light up just because
        //    their children have URLs.
        // 3) Compute AnyUrlCount bottom-up: OwnUrlCount + sum(child.AnyUrlCount).

        Dictionary<string, int> ownCounts;

        try
        {
            ownCounts = await Task.Run(() =>
            {
                using var sr = new StringReader(rawJson);
                using var reader = new JsonTextReader(sr);
                var root = JToken.ReadFrom(reader);

                var map = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var node in Flatten(RootNodes))
                {
                    var path = NormalizeJsonPathForSelectToken(node.JsonPath);
                    var token = root.SelectToken(path, errorWhenNoMatch: false);
                    map[node.JsonPath] = CountUrlsInOwnFields(token);
                }

                return map;
            });
        }
        catch (Exception ex)
        {
            // Don't block the app if scanning fails; just surface a message.
            UrlSummaryText = $"URL scan failed: {ex.Message}";
            return;
        }

        // Apply results on UI thread.
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            foreach (var node in Flatten(RootNodes))
            {
                if (ownCounts.TryGetValue(node.JsonPath, out var count))
                {
                    node.OwnUrlCount = count;
                }
            }

            // Compute AnyUrlCount bottom-up.
            foreach (var root in RootNodes)
            {
                ComputeAggregateCounts(root);
            }

            var totalUrls = 0;
            foreach (var root in RootNodes)
            {
                totalUrls += root.AnyUrlCount;
            }

            UrlSummaryText = $"Total URLs found in save: {totalUrls}.";
        });
    }

    private static int ComputeAggregateCounts(ObjectTreeNodeViewModel node)
    {
        var sum = node.OwnUrlCount;
        foreach (var child in node.Children)
        {
            sum += ComputeAggregateCounts(child);
        }
        node.AnyUrlCount = sum;
        return sum;
    }

    private static IEnumerable<ObjectTreeNodeViewModel> Flatten(IEnumerable<ObjectTreeNodeViewModel> roots)
    {
        var stack = new Stack<ObjectTreeNodeViewModel>(roots.Reverse());
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            yield return n;
            for (var i = n.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(n.Children[i]);
            }
        }
    }

    private static string NormalizeJsonPathForSelectToken(string jsonPath)
    {
        // Newtonsoft's SelectToken can be picky about numeric property names (e.g., States."1").
        // We build paths like: $.ObjectStates[0].States.1
        // Normalize to:        $.ObjectStates[0].States['1']
        if (string.IsNullOrWhiteSpace(jsonPath)) return jsonPath;

        // Replace occurrences of ".States.<digits>" with ".States['<digits>']".
        return Regex.Replace(
            jsonPath,
            @"\.States\.(\d+)(?=\b|\.|\[|$)",
            m => $".States['{m.Groups[1].Value}']",
            RegexOptions.CultureInvariant);
    }

    private static int CountUrlsInOwnFields(JToken? token)
    {
        if (token is null) return 0;
        return CountUrlsRecursive(token);
    }

    private static int CountUrlsRecursive(JToken token)
    {
        // Exclude large child containers so "parent contains URLs" is meaningful.
        if (token is JObject obj)
        {
            var count = 0;
            foreach (var prop in obj.Properties())
            {
                var name = prop.Name;
                if (name.Equals("ContainedObjects", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("States", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("ObjectStates", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                count += CountUrlsRecursive(prop.Value);
            }
            return count;
        }

        if (token is JArray arr)
        {
            var count = 0;
            foreach (var item in arr)
            {
                count += CountUrlsRecursive(item);
            }
            return count;
        }

        if (token is JValue val && val.Type == JTokenType.String)
        {
            var s = (string?)val.Value;
            if (string.IsNullOrWhiteSpace(s)) return 0;
            return UrlRegex.Matches(s).Count;
        }

        return 0;
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

    private void RecomputeSelectionSummary()
    {
        var total = 0;
        var selected = 0;
        var partial = 0;

        foreach (var root in RootNodes)
        {
            CountVm(root, ref total, ref selected, ref partial);
        }

        SummaryText = partial > 0
            ? $"Selected objects: {selected} / {total} (plus {partial} partial branches)."
            : $"Selected objects: {selected} / {total}.";
    }

    private static void CountVm(ObjectTreeNodeViewModel node, ref int total, ref int selected, ref int partial)
    {
        total++;
        if (node.IsChecked == true) selected++;
        else if (node.IsChecked == null) partial++;

        foreach (var child in node.Children)
        {
            CountVm(child, ref total, ref selected, ref partial);
        }
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
        {
            WireNode(child);
        }
    }

    private static ObjectTreeNodeViewModel ConvertNode(ObjectNode node, ObjectTreeNodeViewModel? parent, bool ancestorLocked)
    {
        var locked = ancestorLocked || node.IsState;

        var vm = new ObjectTreeNodeViewModel(parent)
        {
            Guid = node.Guid,
            Name = node.Name,
            Type = node.Type,
            HasStates = node.HasStates,
            JsonPath = node.JsonPath,
            IsSelectionLocked = locked
        };

        foreach (var child in node.Children)
        {
            vm.Children.Add(ConvertNode(child, vm, locked));
        }

        return vm;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
