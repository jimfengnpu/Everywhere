using System.Text.Json;
using System.Text.Json.Serialization;

namespace Everywhere.Chat.Debugging;

public record DebugVisualNode(
    string Id,
    string Type,
    string? Name,
    int[] Rect, // [x, y, w, h]
    IList<string> ChildrenIds,
    bool IsCore
);

public record DebugTraversalStep(
    int StepIndex,
    string NodeId,
    string Action, // "Enqueue", "Visit", "Skip", "Prune"
    double Score,
    string Reason,
    int CurrentTokens,
    int QueueSize
);

public record DebugSession(
    IList<DebugVisualNode> AllNodes,
    IList<DebugTraversalStep> Steps,
    string AlgorithmName,
    int TokenLimit
)
{
    public string ToJson()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        return JsonSerializer.Serialize(this, options);
    }
}
