using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Disposables;
using DynamicData;
using Everywhere.AI;
using Everywhere.Common;
using Everywhere.Configuration;
using Everywhere.Utilities;
using Lucide.Avalonia;
using ZLinq;

namespace Everywhere.Chat.Plugins;

public sealed class ChatPluginManager : IChatPluginManager, IDisposable
{
    public ReadOnlyObservableCollection<BuiltInChatPlugin> BuiltInPlugins { get; }

    public ReadOnlyObservableCollection<McpChatPlugin> McpPlugins { get; }

    private readonly SourceList<BuiltInChatPlugin> _builtInPluginsSource = new();
    private readonly SourceList<McpChatPlugin> _mcpPluginsSource = new();
    private readonly CompositeDisposable _disposables = new();

    public ChatPluginManager(IEnumerable<BuiltInChatPlugin> builtInPlugins, Settings settings)
    {
        _builtInPluginsSource.AddRange(builtInPlugins);

        var isEnabledRecords = settings.Plugin.IsEnabled;
        foreach (var builtInPlugin in _builtInPluginsSource.Items)
        {
            builtInPlugin.IsEnabled = GetIsEnabled($"builtin.{builtInPlugin.Name}", false);
            foreach (var function in builtInPlugin.Functions)
            {
                function.IsEnabled = GetIsEnabled($"builtin.{builtInPlugin.Name}.{function.KernelFunction.Name}", true);
            }
        }

        BuiltInPlugins = _builtInPluginsSource
            .Connect()
            .ObserveOnDispatcher()
            .BindEx(_disposables);
        McpPlugins = _mcpPluginsSource
            .Connect()
            .ObserveOnDispatcher()
            .BindEx(_disposables);

        _disposables.Add(_builtInPluginsSource);
        _disposables.Add(_mcpPluginsSource);

        new ObjectObserver(HandleBuiltInPluginsChange).Observe(BuiltInPlugins);


        bool GetIsEnabled(string path, bool defaultValue)
        {
            return isEnabledRecords.TryGetValue(path, out var isEnabled) ? isEnabled : defaultValue;
        }

        void HandleBuiltInPluginsChange(in ObjectObserverChangedEventArgs e)
        {
            if (!e.Path.EndsWith("IsEnabled", StringComparison.Ordinal))
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
                    settings.Plugin.IsEnabled[$"builtin.{plugin.Name}"] = plugin.IsEnabled;
                    break;
                }
                case 4 when
                    int.TryParse(parts[2], out var functionIndex) &&
                    functionIndex >= 0 &&
                    functionIndex < plugin.Functions.Count:
                {
                    var function = plugin.Functions[functionIndex];
                    settings.Plugin.IsEnabled[$"builtin.{plugin.Name}.{function.KernelFunction.Name}"] = function.IsEnabled;
                    break;
                }
            }
        }
    }

    public void AddMcpPlugin(McpTransportConfiguration configuration)
    {
        throw new NotImplementedException();
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
                .Concat(_mcpPluginsSource.Items)
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