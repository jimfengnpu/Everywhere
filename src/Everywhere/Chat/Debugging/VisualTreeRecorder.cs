using Everywhere.Interop;
using ZLinq;

namespace Everywhere.Chat.Debugging;

public class VisualTreeRecorder(
    IReadOnlyList<IVisualElement> coreElements,
    int tokenLimit,
    string algorithmName)
{
    private readonly List<DebugVisualNode> _allNodes = [];
    private readonly List<DebugTraversalStep> _steps = [];
    private readonly HashSet<string> _knownIds = [];
    private int _stepCounter;

    public void RegisterNode(IVisualElement element)
    {
        if (!_knownIds.Add(element.Id)) return;

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
        _allNodes.Add(new DebugVisualNode(
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
        _steps.Add(new DebugTraversalStep(
            _stepCounter++,
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
            [.. _allNodes],
            [.. _steps],
            algorithmName,
            tokenLimit
        );
        File.WriteAllText(filePath, session.ToJson());
    }
}
