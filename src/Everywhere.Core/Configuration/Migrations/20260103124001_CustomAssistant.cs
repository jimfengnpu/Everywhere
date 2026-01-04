using System.Text.Json.Nodes;

namespace Everywhere.Configuration.Migrations;

/// <summary>
/// This migration handles 0.5.6 CustomAssistant settings changes and other related changes.
/// It has 4 changes:
/// 1. Flatten properties from a Customizable{string} to a simple string
/// 2. Moves the ApiKeys that not in GUID format to a new "LegacyApiKeys" collection
/// 3. Flatten the "Endpoint" property in $.Common.Proxy
/// 4. Delete $.Internal and $.Behavior sections
/// </summary>
public class _20260103124001_CustomAssistant : SettingsMigration
{
    public override Version Version => new Version(0, 5, 6);

    protected internal override bool Migrate(JsonObject root)
    {
        var modified = false;
        modified |= MigrateTask1(root);
        modified |= MigrateTask2(root);
        modified |= MigrateTask3(root);
        modified |= MigrateTask4(root);
        return modified;
    }

    private static bool MigrateTask1(JsonObject root)
    {
        // 1. enumerate CustomAssistants in $.Model.CustomAssistants
        var customAssistantsNode = GetPathNode(root, "Model.CustomAssistants");
        if (customAssistantsNode is not JsonArray customAssistantsArray) return false;

        var modified = false;
        foreach (var assistantNode in customAssistantsArray)
        {
            if (assistantNode is not JsonObject assistantObj) continue;

            // 2. flatten properties
            modified |= FlattenCustomizable(assistantObj, "Endpoint");
            modified |= FlattenCustomizable(assistantObj, "Schema");
            modified |= FlattenCustomizable(assistantObj, "ModelId");
            modified |= FlattenCustomizable(assistantObj, "IsImageInputSupported");
            modified |= FlattenCustomizable(assistantObj, "IsFunctionCallingSupported");
            modified |= FlattenCustomizable(assistantObj, "IsDeepThinkingSupported");
            modified |= FlattenCustomizable(assistantObj, "MaxTokens");
        }

        return modified;
    }

    private static bool MigrateTask2(JsonObject root)
    {
        // 1. get ApiKey value from $.Model.CustomAssistants[*].ApiKey
        var customAssistantsNode = GetPathNode(root, "Model.CustomAssistants");
        if (customAssistantsNode is not JsonArray customAssistantsArray) return false;

        var legacyApiKeysNode = GetPathNode(root, "Model.LegacyApiKeys");
        if (legacyApiKeysNode is not JsonArray legacyApiKeysArray)
        {
            legacyApiKeysArray = new JsonArray();
            var modelNode = GetPathNode(root, "Model") as JsonObject;
            modelNode?["LegacyApiKeys"] = legacyApiKeysArray;
        }

        var modified = false;
        foreach (var assistantNode in customAssistantsArray)
        {
            if (assistantNode is not JsonObject assistantObj) continue;

            if (assistantObj.TryGetPropertyValue("ApiKey", out var apiKeyNode) && apiKeyNode is JsonValue apiKeyValue)
            {
                var apiKey = apiKeyValue.GetValue<string>();
                // 2. check if it's in GUID format
                if (!Guid.TryParse(apiKey, out _))
                {
                    // 3. move to LegacyApiKeys array if not already present
                    apiKeyValue = (JsonValue)apiKeyValue.DeepClone();
                    if (!legacyApiKeysArray.Contains(apiKeyValue))
                    {
                        legacyApiKeysArray.Add(apiKeyValue);
                        modified = true;
                    }
                }
            }
        }

        return modified;
    }

    private static bool MigrateTask3(JsonObject root)
    {
        var proxyNode = GetPathNode(root, "Common.Proxy");
        if (proxyNode is not JsonObject proxyObj) return false;

        return FlattenCustomizable(proxyObj, "Endpoint");
    }

    private static bool MigrateTask4(JsonObject root)
    {
        var modified = false;
        modified |= root.Remove("Internal");;
        modified |= root.Remove("Behavior");;
        return modified;
    }

    /// <summary>
    /// Helper to flatten a Customizable{T} object structure to its value.
    /// Looks for "CustomValue" or "DefaultValue" and replaces the object with the value.
    /// </summary>
    private static bool FlattenCustomizable(JsonObject obj, string propertyName)
    {
        if (!obj.TryGetPropertyValue(propertyName, out var propertyNode) || propertyNode is not JsonObject customObj)
        {
            return false;
        }

        JsonNode? valueToKeep = null;

        // Check for CustomValue first
        if (customObj.TryGetPropertyValue("CustomValue", out var customValue) && customValue is not null)
        {
            valueToKeep = customValue;
        }
        // Fallback to DefaultValue
        else if (customObj.TryGetPropertyValue("DefaultValue", out var defaultValue))
        {
            valueToKeep = defaultValue;
        }

        if (valueToKeep != null)
        {
            // We must clone the node because it's attached to the old parent
            var newValue = valueToKeep.DeepClone();
            obj[propertyName] = newValue;
            return true;
        }

        return false;
    }
}