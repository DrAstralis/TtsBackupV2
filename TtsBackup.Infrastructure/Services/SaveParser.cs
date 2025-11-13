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
            foreach (var objToken in objectStatesArray)
            {
                if (objToken is JObject obj)
                {
                    roots.Add(BuildNode(obj));
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

    private static ObjectNode BuildNode(JObject obj)
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
            foreach (var childToken in containedArray)
            {
                if (childToken is JObject childObj)
                {
                    children.Add(BuildNode(childObj));
                }
            }
        }

        if (obj["States"] is JObject statesObj)
        {
            foreach (var prop in statesObj.Properties())
            {
                if (prop.Value is JObject stateObj)
                {
                    children.Add(BuildNode(stateObj));
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
            HasOwnAssets = false
        };
    }
}
