using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Disposables;
using System.Reflection;
using DynamicData;
using Everywhere.AI;
using Everywhere.Common;
using Everywhere.Configuration;
using Everywhere.Interop;
using Everywhere.Utilities;
using Lucide.Avalonia;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ZLinq;

namespace Everywhere.Chat.Plugins;

public class ChatPluginManager : IChatPluginManager
{
    public IReadOnlyList<BuiltInChatPlugin> BuiltInPlugins { get; }

    public IReadOnlyList<McpChatPlugin> McpPlugins { get; }

    private readonly IWatchdogManager _watchdogManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ChatPluginManager> _logger;

    private readonly CompositeDisposable _disposables = new();
    private readonly SourceList<BuiltInChatPlugin> _builtInPluginsSource = new();
    private readonly ConcurrentDictionary<Guid, RunningMcpClient> _runningMcpClients = [];

    public ChatPluginManager(
        IEnumerable<BuiltInChatPlugin> builtInPlugins,
        IWatchdogManager watchdogManager,
        ILoggerFactory loggerFactory,
        Settings settings)
    {
        _builtInPluginsSource.AddRange(builtInPlugins);
        _watchdogManager = watchdogManager;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<ChatPluginManager>();
        McpPlugins = settings.Plugin.McpPlugins;

        var isEnabledRecords = settings.Plugin.IsEnabled;
        foreach (var builtInPlugin in _builtInPluginsSource.Items)
        {
            builtInPlugin.IsEnabled = GetIsEnabled(builtInPlugin.Key, false);
            foreach (var function in builtInPlugin.Functions)
            {
                function.IsEnabled = GetIsEnabled($"{builtInPlugin.Key}.{function.KernelFunction.Name}", true);
            }
        }

        BuiltInPlugins = _builtInPluginsSource
            .Connect()
            .ObserveOnDispatcher()
            .BindEx(_disposables);
        _disposables.Add(_builtInPluginsSource);

        new ObjectObserver(HandleBuiltInPluginsChange).Observe((INotifyPropertyChanged)BuiltInPlugins);


        bool GetIsEnabled(string path, bool defaultValue)
        {
            return isEnabledRecords.TryGetValue(path, out var isEnabled) ? isEnabled : defaultValue;
        }

        void HandleBuiltInPluginsChange(in ObjectObserverChangedEventArgs e)
        {
            if (!e.Path.EndsWith(nameof(ChatFunction.IsEnabled), StringComparison.Ordinal))
            {
                return;
            }

            var parts = e.Path.Split(':');
            if (parts.Length < 2 || !int.TryParse(parts[0], out var pluginIndex) || pluginIndex < 0 || pluginIndex >= _builtInPluginsSource.Count)
            {
                return;
            }

            var plugin = _builtInPluginsSource.Items[pluginIndex];
            switch (parts.Length)
            {
                case 2:
                {
                    settings.Plugin.IsEnabled[plugin.Key] = plugin.IsEnabled;
                    break;
                }
                case 4 when
                    int.TryParse(parts[2], out var functionIndex) &&
                    functionIndex >= 0 &&
                    functionIndex < plugin.Functions.Count:
                {
                    var function = plugin.Functions[functionIndex];
                    settings.Plugin.IsEnabled[$"{plugin.Key}.{function.KernelFunction.Name}"] = function.IsEnabled;
                    break;
                }
            }
        }
    }

    public void AddMcpPlugin(McpTransportConfiguration configuration)
    {
        throw new NotImplementedException();
    }

    public async Task CreateMcpClientAsync(McpTransportConfiguration configuration, CancellationToken cancellationToken)
    {
        if (!configuration.IsValid)
        {
            throw new InvalidOperationException("MCP transport configuration is not valid.");
        }

        if (_runningMcpClients.ContainsKey(configuration.Id)) return; // Just return without error if already running.

        IClientTransport clientTransport = configuration switch
        {
            StdioMcpTransportConfiguration stdio => new StdioClientTransport(
                new StdioClientTransportOptions
                {
                    Name = stdio.Name,
                    Command = stdio.Command,
                    Arguments = stdio.ArgumentsList,
                    WorkingDirectory = stdio.WorkingDirectory,
                    EnvironmentVariables = stdio.EnvironmentVariables,
                },
                _loggerFactory),
            SseMcpTransportConfiguration sse => new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Name = sse.Name,
                    Endpoint = new Uri(sse.Endpoint, UriKind.Absolute),
                    AdditionalHeaders = sse.Headers,
                },
                _loggerFactory),
            _ => throw new NotSupportedException("Unsupported MCP transport configuration type."),
        };

        var client = await McpClient.CreateAsync(
            clientTransport,
            new McpClientOptions
            {
                InitializationTimeout = TimeSpan.FromSeconds(30)
            },
            _loggerFactory,
            cancellationToken);

        var processId = -1;
        try
        {
            if (clientTransport is StdioClientTransport)
            {
                // Add the underlying process to the watchdog to ensure it is cleaned up properly.
                // We need reflection here because StdioClientTransport does not expose the process directly.
                // client is `ModelContextProtocol.Client.McpClientImpl`
                var transportFieldInfo = client.GetType().GetField("_transport", BindingFlags.Instance | BindingFlags.NonPublic);
                var transport = transportFieldInfo?.GetValue(client); // StdioClientSessionTransport : ITransport
                var processFieldInfo = transport?.GetType().GetField("_process", BindingFlags.Instance | BindingFlags.NonPublic);
                if (processFieldInfo?.GetValue(transport) is Process { HasExited: false, Id: > 0 } process)
                {
                    await _watchdogManager.RegisterProcessAsync(process.Id);
                    processId = process.Id;
                }
            }
        }
        finally
        {
            _runningMcpClients[configuration.Id] = new RunningMcpClient(configuration, client, processId);

            if (processId == -1 && configuration is StdioMcpTransportConfiguration stdio)
            {
                _logger.LogWarning(
                    "MCP started with stdio transport, but failed to get the underlying process ID for watchdog registration. " +
                    "Command: {Command}, Arguments: {Arguments}",
                    stdio.Command,
                    stdio.Arguments);
            }
        }
    }

    public async Task<IReadOnlyList<ChatFunction>> ListMcpFunctionsAsync(McpTransportConfiguration configuration, CancellationToken cancellationToken)
    {
        await CreateMcpClientAsync(configuration, cancellationToken);

        if (!_runningMcpClients.TryGetValue(configuration.Id, out var runningClient))
        {
            throw new InvalidOperationException("MCP client is not running.");
        }

        await foreach (var tool in runningClient.Client.EnumerateToolsAsync(cancellationToken: cancellationToken))
        {

        }
    }

    public IChatPluginScope CreateScope(ChatContext chatContext, CustomAssistant customAssistant)
    {
        // Ensure that functions in the scope do not have the same name.
        var functionNames = new HashSet<string>();
        return new ChatPluginScope(
            _builtInPluginsSource
                .Items
                .AsValueEnumerable()
                .Cast<ChatPlugin>()
                .Concat(McpPlugins)
                .Where(p => p.IsEnabled)
                .Select(p => new ChatPluginSnapshot(p, chatContext, customAssistant, functionNames))
                .ToList());
    }

    private class ChatPluginScope(List<ChatPluginSnapshot> pluginSnapshots) : IChatPluginScope
    {
        public IEnumerable<ChatPlugin> Plugins => pluginSnapshots;

        public bool TryGetPluginAndFunction(
            string functionName,
            [NotNullWhen(true)] out ChatPlugin? plugin,
            [NotNullWhen(true)] out ChatFunction? function)
        {
            foreach (var pluginSnapshot in pluginSnapshots)
            {
                function = pluginSnapshot.Functions.AsValueEnumerable().FirstOrDefault(f => f.KernelFunction.Name == functionName);
                if (function is not null)
                {
                    plugin = pluginSnapshot;
                    return true;
                }
            }

            plugin = null;
            function = null;
            return false;
        }
    }

    private readonly record struct RunningMcpClient(McpTransportConfiguration Configuration, McpClient Client, int ProcessId);

    private class ChatPluginSnapshot : ChatPlugin
    {
        public override string Key => _originalChatPlugin.Key;
        public override DynamicResourceKeyBase HeaderKey => _originalChatPlugin.HeaderKey;
        public override DynamicResourceKeyBase DescriptionKey => _originalChatPlugin.DescriptionKey;
        public override LucideIconKind? Icon => _originalChatPlugin.Icon;
        public override string? BeautifulIcon => _originalChatPlugin.BeautifulIcon;

        private readonly ChatPlugin _originalChatPlugin;

        public ChatPluginSnapshot(
            ChatPlugin originalChatPlugin,
            ChatContext chatContext,
            CustomAssistant customAssistant,
            HashSet<string> functionNames) : base(originalChatPlugin.Name)
        {
            _originalChatPlugin = originalChatPlugin;
            AllowedPermissions = originalChatPlugin.AllowedPermissions.ActualValue;
            _functionsSource.AddRange(
                originalChatPlugin
                    .SnapshotFunctions(chatContext, customAssistant)
                    .Select(EnsureUniqueFunctionName));

            ChatFunction EnsureUniqueFunctionName(ChatFunction function)
            {
                var metadata = function.KernelFunction.Metadata;
                if (functionNames.Add(metadata.Name)) return function;

                var postfix = 1;
                string newName;
                do
                {
                    newName = $"{metadata.Name}_{postfix++}";
                }
                while (!functionNames.Add(newName));
                metadata.Name = newName;
                return function;
            }
        }
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}