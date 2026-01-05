using System.Text.Json.Nodes;
using Everywhere.Chat.Plugins;

namespace Everywhere.Configuration.Migrations;

/// <summary>
/// This migration handles 0.5.6 settings changes.
/// It has 6 changes:
/// 1. Flatten properties from a Customizable{string} to a simple string, delete the DefaultValue of "SystemPrompt" property
/// 2. Moves the ApiKeys that not in GUID format to a new "$.LegacyApiKeys" dictionary (Key is path, Value is ApiKey)
/// 3. Flatten the "Endpoint" property in $.Common.Proxy
/// 4. Convert $Plugin.WebSearchEngine.WebSearchEngineProviders to dictionary at ($Plugin.WebSearchEngine.Providers) with provider id as key and delete key property, also move ApiKey property to "$.LegacyApiKeys" if not in GUID format
/// 5. Move $Plugin.WebSearchEngine.SelectedWebSearchEngineProviderId to $Plugin.WebSearchEngine.SelectedProviderId
/// 6. Delete $.Model.ModelProvider, $.Model.SelectedModelProviderId, $.Model.SelectedModelDefinitionId $.Internal and $.Behavior sections
/// </summary>
public class _20260103124001_0_5_6 : SettingsMigration
{
    public override Version Version => new(0, 5, 6);

    protected internal override bool Migrate(JsonObject root)
    {
        var modified = false;
        modified |= MigrateTask1(root);
        modified |= MigrateTask2(root);
        modified |= MigrateTask3(root);
        modified |= MigrateTask4(root);
        modified |= MigrateTask5(root);
        modified |= MigrateTask6(root);
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

            // 3. remove DefaultValue of SystemPrompt
            if (assistantObj.TryGetPropertyValue("SystemPrompt", out var systemPromptNode) && systemPromptNode is JsonObject systemPromptObj)
            {
                if (systemPromptObj.Remove("DefaultValue"))
                {
                    modified = true;
                }
            }
        }

        return modified;
    }

    private static bool MigrateTask2(JsonObject root)
    {
        // 1. get ApiKey value from $.Model.CustomAssistants[*].ApiKey
        var customAssistantsNode = GetPathNode(root, "Model.CustomAssistants");
        if (customAssistantsNode is not JsonArray customAssistantsArray) return false;

        var legacyApiKeysNode = GetPathNode(root, "LegacyApiKeys");
        if (legacyApiKeysNode is not JsonObject legacyApiKeysObj)
        {
            legacyApiKeysObj = new JsonObject();
            root["LegacyApiKeys"] = legacyApiKeysObj;
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
                    var apiKeyPath = $"Model.CustomAssistants[{customAssistantsArray.IndexOf(assistantNode)}].ApiKey";
                    if (!legacyApiKeysObj.ContainsKey(apiKeyPath))
                    {
                        legacyApiKeysObj[apiKeyPath] = apiKeyValue.DeepClone();
                        assistantObj.Remove("ApiKey");
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
        var webSearchEngineNode = GetPathNode(root, "Plugin.WebSearchEngine");
        if (webSearchEngineNode is not JsonObject webSearchEngineObj) return false;

        if (!webSearchEngineObj.TryGetPropertyValue("WebSearchEngineProviders", out var providersNode) || providersNode is not JsonArray providersArray)
        {
            return false;
        }

        var legacyApiKeysNode = GetPathNode(root, "LegacyApiKeys");
        if (legacyApiKeysNode is not JsonObject legacyApiKeysObj)
        {
            legacyApiKeysObj = new JsonObject();
            root["LegacyApiKeys"] = legacyApiKeysObj;
        }

        var newProvidersObj = new JsonObject();

        foreach (var item in providersArray)
        {
            if (item is not JsonObject providerObj) continue;

            if (providerObj.TryGetPropertyValue("Id", out var idNode) && idNode is JsonValue idValue)
            {
                var idStr = idValue.GetValue<string>();
                if (!Enum.TryParse<WebSearchEngineProviderId>(idStr, out var id)) continue;

                providerObj.Remove("Id");

                if (providerObj.TryGetPropertyValue("ApiKey", out var apiKeyNode) && apiKeyNode is JsonValue apiKeyValue)
                {
                    var apiKey = apiKeyValue.GetValue<string>();
                    if (!Guid.TryParse(apiKey, out _))
                    {
                        var apiKeyPath = $"Plugin.WebSearchEngine.Providers.{id}.ApiKey";
                        if (!legacyApiKeysObj.ContainsKey(apiKeyPath))
                        {
                            legacyApiKeysObj[apiKeyPath] = apiKeyValue.DeepClone();
                            providerObj.Remove("ApiKey");
                        }
                    }
                }

                newProvidersObj[id.ToString()] = providerObj.DeepClone();
            }
        }

        webSearchEngineObj.Remove("WebSearchEngineProviders");
        webSearchEngineObj["Providers"] = newProvidersObj;

        return true;
    }

    private static bool MigrateTask5(JsonObject root)
    {
        var webSearchEngineNode = GetPathNode(root, "Plugin.WebSearchEngine");
        if (webSearchEngineNode is not JsonObject webSearchEngineObj) return false;

        if (webSearchEngineObj.TryGetPropertyValue("SelectedWebSearchEngineProviderId", out var selectedIdNode))
        {
            webSearchEngineObj.Remove("SelectedWebSearchEngineProviderId");
            webSearchEngineObj["SelectedProviderId"] = selectedIdNode?.DeepClone();
            return true;
        }

        return false;
    }

    private static bool MigrateTask6(JsonObject root)
    {
        var modified = false;
        var modelNode = GetPathNode(root, "Model") as JsonObject;
        modified |= modelNode?.Remove("ModelProvider") ?? false;
        modified |= modelNode?.Remove("SelectedModelProviderId") ?? false;
        modified |= modelNode?.Remove("SelectedModelDefinitionId") ?? false;
        modified |= root.Remove("Internal");
        modified |= root.Remove("Behavior");
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