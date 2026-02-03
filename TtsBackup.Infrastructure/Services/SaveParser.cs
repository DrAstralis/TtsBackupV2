using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TtsBackup.Core.Models;
using TtsBackup.Core.Services;

namespace TtsBackup.Infrastructure.Services;

public sealed class SaveParser : ISaveParser
{
    public Task<SaveDocument> ParseAsync(string json, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON is empty.", nameof(json));

        var originalJson = json;

        using var stringReader = new StringReader(originalJson);
        using var jsonReader = new JsonTextReader(stringReader);

        var rootToken = JToken.ReadFrom(jsonReader);
        if (rootToken is not JObject rootObj)
        {
            return Task.FromResult(new SaveDocument(originalJson, Array.Empty<ObjectNode>(), null));
        }

        var roots = new List<ObjectNode>();

        if (rootObj["ObjectStates"] is JArray objectStatesArray)
        {
            for (var i = 0; i < objectStatesArray.Count; i++)
            {
                if (objectStatesArray[i] is JObject obj)
                {
                    roots.Add(BuildNode(obj, $"$.ObjectStates[{i}]", isState: false));
                }
            }
        }

        string? saveName = null;
        if (rootObj["SaveName"] is JValue nameVal && nameVal.Type == JTokenType.String)
        {
            saveName = (string?)nameVal;
        }

        return Task.FromResult(new SaveDocument(originalJson, roots, saveName));
    }

    private static ObjectNode BuildNode(JObject obj, string jsonPath, bool isState)
    {
        var guid = (string?)obj["GUID"] ?? string.Empty;

        var nickname = (string?)obj["Nickname"];
        var nameField = (string?)obj["Name"];
        var name = !string.IsNullOrWhiteSpace(nickname) ? nickname! : nameField ?? string.Empty;

        var type = nameField ?? string.Empty;

        var hasStates = obj["States"] is JObject or JArray;

        var children = new List<ObjectNode>();

        if (obj["ContainedObjects"] is JArray containedArray)
        {
            for (var i = 0; i < containedArray.Count; i++)
            {
                if (containedArray[i] is JObject childObj)
                {
                    children.Add(BuildNode(childObj, $"{jsonPath}.ContainedObjects[{i}]", isState: false));
                }
            }
        }

        if (obj["States"] is JObject statesObj)
        {
            foreach (var prop in statesObj.Properties())
            {
                if (prop.Value is JObject stateObj)
                {
                    // State objects should be shown in the tree, but are not independently selectable in Phase 3.
                    children.Add(BuildNode(stateObj, $"{jsonPath}.States.{prop.Name}", isState: true));
                }
            }
        }

        // Some saves contain nested ObjectStates inside an object (including inside state objects).
        if (obj["ObjectStates"] is JArray nestedObjectStates)
        {
            for (var i = 0; i < nestedObjectStates.Count; i++)
            {
                if (nestedObjectStates[i] is JObject nestedObj)
                {
                    children.Add(BuildNode(nestedObj, $"{jsonPath}.ObjectStates[{i}]", isState: false));
                }
            }
        }

        return new ObjectNode
        {
            Guid = guid,
            Name = name,
            Type = type,
            HasStates = hasStates,
            Children = children,
            JsonPath = jsonPath,
            IsState = isState,
            HasOwnAssets = false
        };
    }
}
