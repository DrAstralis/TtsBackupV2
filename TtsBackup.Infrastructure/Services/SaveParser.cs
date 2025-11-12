using System.Text.Json;
using TtsBackup.Core.Models;
using TtsBackup.Core.Services;

namespace TtsBackup.Infrastructure.Services;

public sealed class SaveParser : ISaveParser
{
    public Task<SaveDocument> ParseAsync(string json, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON is empty.", nameof(json));

        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;

        if (!root.TryGetProperty("ObjectStates", out var objectStates) || objectStates.ValueKind != JsonValueKind.Array)
        {
            // Still return something, but empty roots.
            var empty = new SaveDocument(json, Array.Empty<ObjectNode>(), OriginalName: null);
            return Task.FromResult(empty);
        }

        var nodes = new List<ObjectNode>();

        foreach (var obj in objectStates.EnumerateArray())
        {
            var node = BuildNode(obj);
            nodes.Add(node);
        }

        string? saveName = null;
        if (root.TryGetProperty("SaveName", out var saveNameProp) && saveNameProp.ValueKind == JsonValueKind.String)
        {
            saveName = saveNameProp.GetString();
        }

        var save = new SaveDocument(json, nodes, saveName);
        return Task.FromResult(save);
    }

    private static ObjectNode BuildNode(JsonElement element)
    {
        var guid = element.TryGetProperty("GUID", out var guidProp) && guidProp.ValueKind == JsonValueKind.String
            ? guidProp.GetString() ?? string.Empty
            : string.Empty;

        var name = element.TryGetProperty("Nickname", out var nicknameProp) && nicknameProp.ValueKind == JsonValueKind.String
            ? nicknameProp.GetString() ?? string.Empty
            : element.TryGetProperty("Name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String
                ? nameProp.GetString() ?? string.Empty
                : string.Empty;

        var type = element.TryGetProperty("Name", out var typeProp) && typeProp.ValueKind == JsonValueKind.String
            ? typeProp.GetString() ?? string.Empty
            : string.Empty;

        bool hasStates = element.TryGetProperty("States", out var statesProp) &&
                         (statesProp.ValueKind == JsonValueKind.Object || statesProp.ValueKind == JsonValueKind.Array);

        var children = new List<ObjectNode>();

        // ContainedObjects: array of more ObjectStates
        if (element.TryGetProperty("ContainedObjects", out var containedObjectsProp) &&
            containedObjectsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in containedObjectsProp.EnumerateArray())
            {
                var childNode = BuildNode(child);
                children.Add(childNode);
            }
        }

        // States: dictionary-like object with numbered keys ("1", "2", ...)
        if (element.TryGetProperty("States", out var statesElement) &&
            statesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in statesElement.EnumerateObject())
            {
                var stateNode = BuildNode(prop.Value);
                children.Add(stateNode);
            }
        }

        // For now we don't inspect asset fields; HasOwnAssets stays false.
        return new ObjectNode
        {
            Guid = guid,
            Name = name,
            Type = type,
            HasStates = hasStates,
            Children = children,
            HasOwnAssets = false
        };
    }
}
