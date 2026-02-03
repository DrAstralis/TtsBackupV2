using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TtsBackup.Core.Models;
using TtsBackup.Core.Services;

namespace TtsBackup.Infrastructure.Services;

public sealed class AssetScanner : IAssetScanner
{
    private static readonly Regex HttpUrlRegex = new(
        @"https?://[^\s""'<>]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly (string RelativePath, AssetType Type)[] KnownFields =
    {
        ("CustomMesh.MeshURL", AssetType.Mesh),
        ("CustomMesh.TextureURL", AssetType.Image),
        ("CustomMesh.NormalURL", AssetType.Image),
        ("CustomMesh.ColliderURL", AssetType.Mesh),

        ("CustomImage.ImageURL", AssetType.Image),
        ("CustomAssetbundle.AssetbundleURL", AssetType.AssetBundle),
        ("CustomAssetbundle.AssetbundleSecondaryURL", AssetType.AssetBundle),

        ("CustomPlaymat.ImageURL", AssetType.EnvironmentTable),
        ("CustomDecal.ImageURL", AssetType.Decal),

        ("CustomUI.AssetURL", AssetType.UiAsset),
        ("CardCustom.AssetURL", AssetType.UiAsset),
    };

    public Task<IReadOnlyList<AssetReference>> ScanAssetsAsync(
        SaveDocument document,
        SelectionSnapshot selection,
        CancellationToken cancellationToken = default)
    {
        // CPU-bound: parse + traverse. Run off-thread so the UI stays responsive.
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var root = ParseRootToken(document.RawJson);
            if (root is null) return (IReadOnlyList<AssetReference>)Array.Empty<AssetReference>();

            var byGuid = BuildGuidMap(document.Roots);
            var includedGuids = ResolveIncludedGuids(document.Roots, byGuid, selection);

            var results = new List<AssetReference>();

            foreach (var guid in includedGuids)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!byGuid.TryGetValue(guid, out var node))
                    continue;

                if (string.IsNullOrWhiteSpace(node.JsonPath))
                    continue;

                var token = root.SelectToken(NormalizeJsonPathForSelectToken(node.JsonPath), errorWhenNoMatch: false);
                if (token is null)
                    continue;

                ScanObjectToken(node, token, results);
            }

            return (IReadOnlyList<AssetReference>)results;
        }, cancellationToken);
    }

    private static JObject? ParseRootToken(string rawJson)
    {
        using var sr = new StringReader(rawJson);
        using var jr = new JsonTextReader(sr);
        var token = JToken.ReadFrom(jr);
        return token as JObject;
    }

    private static Dictionary<string, ObjectNode> BuildGuidMap(IReadOnlyList<ObjectNode> roots)
    {
        var map = new Dictionary<string, ObjectNode>(StringComparer.OrdinalIgnoreCase);

        void Walk(ObjectNode n)
        {
            if (!string.IsNullOrWhiteSpace(n.Guid))
                map[n.Guid] = n;

            foreach (var c in n.Children)
                Walk(c);
        }

        foreach (var r in roots)
            Walk(r);

        return map;
    }

    private static HashSet<string> ResolveIncludedGuids(
        IReadOnlyList<ObjectNode> roots,
        Dictionary<string, ObjectNode> byGuid,
        SelectionSnapshot selection)
    {
        var included = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // If nothing is selected, treat it as "scan everything" (Phase 3: analysis panel).
        if (selection.SelectedObjects.Count == 0)
        {
            foreach (var r in roots)
                AddAllDescendants(r, included);
            return included;
        }

        foreach (var so in selection.SelectedObjects)
        {
            if (string.IsNullOrWhiteSpace(so.Guid)) continue;

            included.Add(so.Guid);

            if (so.IncludeChildren && byGuid.TryGetValue(so.Guid, out var node))
            {
                foreach (var child in node.Children)
                    AddAllDescendants(child, included);
            }
        }

        return included;
    }

    private static void AddAllDescendants(ObjectNode node, HashSet<string> included)
    {
        if (!string.IsNullOrWhiteSpace(node.Guid))
            included.Add(node.Guid);

        foreach (var c in node.Children)
            AddAllDescendants(c, included);
    }

    private static void ScanObjectToken(ObjectNode node, JToken objectToken, List<AssetReference> results)
    {
        // Avoid duplicate entries for the exact same field path.
        var seenFieldPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1) Known fields (including local paths / non-http values so we can warn later).
        foreach (var (rel, type) in KnownFields)
        {
            var t = objectToken.SelectToken(rel, errorWhenNoMatch: false);
            if (t is JValue v && v.Type == JTokenType.String)
            {
                var s = (string?)v.Value;
                TryAdd(node, v, s, type, results, seenFieldPaths);
            }
        }

        // CustomDeck is a special case: keyed dictionary of card backs/faces.
        ScanCustomDeck(node, objectToken, results, seenFieldPaths);

        // 2) Any other URL-like strings (real-world saves have vendor fields).
        WalkValues(objectToken, v =>
        {
            if (v.Type != JTokenType.String) return;

            var s = (string?)v.Value;
            if (string.IsNullOrWhiteSpace(s)) return;

            // Exclude Tablet URL (website reference; not a game asset).
            if (IsTabletUrl(v.Path)) return;

            if (HttpUrlRegex.IsMatch(s))
            {
                TryAdd(node, v, s, AssetType.Unknown, results, seenFieldPaths);
            }
        });
    }

    private static void ScanCustomDeck(ObjectNode node, JToken objectToken, List<AssetReference> results, HashSet<string> seenFieldPaths)
    {
        var customDeck = objectToken["CustomDeck"];
        if (customDeck is not JObject deckObj) return;

        foreach (var deckEntry in deckObj.Properties())
        {
            if (deckEntry.Value is not JObject deckDef) continue;

            AddDeckField(node, deckDef, "FaceURL", AssetType.DeckFace, results, seenFieldPaths);
            AddDeckField(node, deckDef, "BackURL", AssetType.DeckBack, results, seenFieldPaths);

            // Some saves use UniqueBackURL per card.
            AddDeckField(node, deckDef, "UniqueBackURL", AssetType.DeckBack, results, seenFieldPaths);
        }
    }

    private static void AddDeckField(
        ObjectNode node,
        JObject deckDef,
        string field,
        AssetType type,
        List<AssetReference> results,
        HashSet<string> seenFieldPaths)
    {
        var t = deckDef.SelectToken(field, errorWhenNoMatch: false);
        if (t is JValue v && v.Type == JTokenType.String)
        {
            var s = (string?)v.Value;
            TryAdd(node, v, s, type, results, seenFieldPaths);
        }
    }

    private static void WalkValues(JToken token, Action<JValue> onValue)
    {
        switch (token)
        {
            case JValue v:
                onValue(v);
                break;

            case JObject o:
                foreach (var p in o.Properties())
                    WalkValues(p.Value, onValue);
                break;

            case JArray a:
                foreach (var item in a)
                    WalkValues(item, onValue);
                break;
        }
    }

    private static bool IsTabletUrl(string tokenPath)
    {
        // Typical: "...Tablet.URL"
        // Keep it conservative: any ".Tablet.URL" segment is excluded.
        return tokenPath.EndsWith(".Tablet.URL", StringComparison.OrdinalIgnoreCase)
               || tokenPath.Contains(".Tablet.URL.", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryAdd(
        ObjectNode node,
        JValue valueToken,
        string? value,
        AssetType type,
        List<AssetReference> results,
        HashSet<string> seenFieldPaths)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        var fieldPath = valueToken.Path;
        if (!seenFieldPaths.Add(fieldPath)) return;

        // Keep extension inference for later phases (download/export). For now, only fill when obvious.
        var inferredExt = TryInferExtensionFromUrl(value);

        results.Add(new AssetReference(
            OriginalUrl: value,
            Type: type,
            InferredExtension: inferredExt,
            SourceObjectGuid: node.Guid,
            SourceObjectName: node.Name,
            FieldPath: fieldPath));
    }

    private static string? TryInferExtensionFromUrl(string value)
    {
        // If it's not an http(s) URL, don't guess.
        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Strip query/fragment.
        var cut = value;
        var q = cut.IndexOfAny(new[] { '?', '#' });
        if (q >= 0) cut = cut[..q];

        // Last segment extension.
        var slash = cut.LastIndexOf('/');
        if (slash >= 0) cut = cut[(slash + 1)..];

        var dot = cut.LastIndexOf('.');
        if (dot < 0 || dot == cut.Length - 1) return null;

        var ext = cut[dot..];
        if (ext.Length > 10) return null;
        return ext;
    }

    private static string NormalizeJsonPathForSelectToken(string jsonPath)
    {
        // Our ObjectNode.JsonPath is a JSONPath produced by the parser.
        // SelectToken expects paths without the leading "$." in some cases.
        // Keep the same normalization behavior used by the WPF ViewModel.
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
}
