using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Collections;

namespace Everywhere.Chat.Plugins;

[JsonPolymorphic]
[JsonDerivedType(typeof(StdioMcpTransportConfiguration), "stdio")]
[JsonDerivedType(typeof(SseMcpTransportConfiguration), "sse")]
public abstract partial class McpTransportConfiguration : ObservableObject
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Description { get; set; }

    public virtual bool IsValid => !string.IsNullOrWhiteSpace(Name);
}

public partial class StdioMcpTransportConfiguration : McpTransportConfiguration
{
    [ObservableProperty]
    public partial string Command { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Arguments { get; set; }

    [ObservableProperty]
    public partial string? WorkingDirectory { get; set; }

    [ObservableProperty]
    public partial ObservableDictionary<string, string?> EnvironmentVariables { get; set; } = new();

    public override bool IsValid => base.IsValid && !string.IsNullOrWhiteSpace(Command);

    [JsonIgnore]
    public IList<string> ArgumentsList =>
}

public partial class SseMcpTransportConfiguration : McpTransportConfiguration
{
    [ObservableProperty]
    public partial string Endpoint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableDictionary<string, string> Headers { get; set; } = new();

    public override bool IsValid => base.IsValid && Uri.IsWellFormedUriString(Endpoint, UriKind.Absolute);
}