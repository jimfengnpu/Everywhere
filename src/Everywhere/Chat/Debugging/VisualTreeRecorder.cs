using System.Text.Json;
using Everywhere.Interop;
using ZLinq;

namespace Everywhere.Chat.Debugging;

public class VisualTreeRecorder(
    IReadOnlyList<IVisualElement> coreElements,
    int tokenLimit,
    string algorithmName)
{
    private readonly List<DebugVisualNode> allNodes = [];
    private readonly List<DebugTraversalStep> steps = [];
    private readonly HashSet<string> knownIds = [];
    private int stepCounter = 0;

    public void RegisterNode(IVisualElement element)
    {
        if (!knownIds.Add(element.Id)) return;

        IList<string> childrenIds;
        try 
        {
            childrenIds = element.Children.AsValueEnumerable().Take(100).Select(child => child.Id).ToList();
        }
        catch 
        {
            childrenIds = [];
        }

        var rect = element.BoundingRectangle;
        allNodes.Add(new DebugVisualNode(
            element.Id,
            element.Type.ToString(),
            element.Name,
            [rect.X, rect.Y, rect.Width, rect.Height],
            childrenIds,
            coreElements.AsValueEnumerable().Any(c => c.Id == element.Id)
        ));
    }

    public void RecordStep(IVisualElement node, string action, double score, string reason, int currentTokens, int queueSize)
    {
        steps.Add(new DebugTraversalStep(
            stepCounter++,
            node.Id,
            action,
            score,
            reason,
            currentTokens,
            queueSize
        ));
    }

    public void SaveSession(string filePath)
    {
        var session = new DebugSession(
            [.. allNodes],
            [.. steps],
            algorithmName,
            tokenLimit
        );
        File.WriteAllText(filePath, session.ToJson());
    }
}
