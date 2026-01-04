using System.Text.Json.Nodes;

namespace Everywhere.Configuration;

/// <summary>
/// The base class for settings migrations.
/// </summary>
public abstract class SettingsMigration
{
    /// <summary>
    /// The target version of the migration.
    /// </summary>
    public abstract Version Version { get; }

    /// <summary>
    /// Performs the migration on the given JSON root object.
    /// </summary>
    /// <param name="root"></param>
    /// <returns>true if the migration made changes; otherwise, false.</returns>
    internal protected abstract bool Migrate(JsonObject root);

    /// <summary>
    /// Helper to get a JsonNode value by a dot-separated path.
    /// </summary>
    /// <param name="root"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    protected static JsonNode? GetPathNode(JsonObject root, string path)
    {
        var ranges = path.AsSpan().Split('.');
        JsonNode? currentNode = root;

        foreach (var range in ranges)
        {
            var segment = path[range];
            if (currentNode is not JsonObject currentObj || !currentObj.TryGetPropertyValue(segment, out currentNode))
            {
                return null;
            }
        }

        return currentNode;
    }
}
