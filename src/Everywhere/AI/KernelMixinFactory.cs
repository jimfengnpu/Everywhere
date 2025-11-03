using CommunityToolkit.Mvvm.Messaging;
using Everywhere.Common;
using Everywhere.Configuration;
using Everywhere.Utilities;

namespace Everywhere.AI;

/// <summary>
/// A factory for creating instances of <see cref="IKernelMixin"/>.
/// </summary>
public class KernelMixinFactory : IKernelMixinFactory, IRecipient<NetworkProxyChangedMessage>
{
    private readonly Lock _syncLock = new();

    private KernelMixinBase? _cachedKernelMixin;

    public KernelMixinFactory()
    {
        WeakReferenceMessenger.Default.Register(this);
    }

    /// <summary>
    /// Gets an existing <see cref="IKernelMixin"/> instance from the cache or creates a new one.
    /// </summary>
    /// <param name="customAssistant">The custom assistant configuration to use for creating the kernel mixin.</param>
    /// <param name="apiKeyOverride">An optional API key to override the one in the settings.</param>
    /// <returns>A cached or new instance of <see cref="IKernelMixin"/>.</returns>
    /// <exception cref="HandledChatException">Thrown if the model provider or definition is not found or not supported.</exception>
    public IKernelMixin GetOrCreate(CustomAssistant customAssistant, string? apiKeyOverride = null)
    {
        using var lockScope = _syncLock.EnterScope();

        if (!Uri.TryCreate(customAssistant.Endpoint.ActualValue, UriKind.Absolute, out _))
        {
            throw new HandledChatException(
                new InvalidOperationException("Invalid endpoint URL."),
                HandledChatExceptionType.InvalidEndpoint);
        }

        if (customAssistant.ModelId.ActualValue.IsNullOrWhiteSpace())
        {
            throw new HandledChatException(
                new InvalidOperationException("Model ID cannot be empty."),
                HandledChatExceptionType.InvalidConfiguration);
        }

        var apiKey = apiKeyOverride ?? customAssistant.ApiKey;
        if (_cachedKernelMixin is not null &&
            _cachedKernelMixin.Schema == customAssistant.Schema &&
            _cachedKernelMixin.ModelId == customAssistant.ModelId &&
            _cachedKernelMixin.Endpoint == customAssistant.Endpoint.ActualValue.Trim().Trim('/') &&
            _cachedKernelMixin.ApiKey == apiKey)
        {
            return _cachedKernelMixin;
        }

        _cachedKernelMixin?.Dispose();
        return _cachedKernelMixin = customAssistant.Schema.ActualValue switch
        {
            ModelProviderSchema.OpenAI => new OpenAIKernelMixin(customAssistant),
            ModelProviderSchema.Anthropic => new AnthropicKernelMixin(customAssistant),
            ModelProviderSchema.Ollama => new OllamaKernelMixin(customAssistant),
            _ => throw new HandledChatException(
                new NotSupportedException($"Model provider schema '{customAssistant.Schema}' is not supported."),
                HandledChatExceptionType.InvalidConfiguration,
                new DynamicResourceKey(LocaleKey.KernelMixinFactory_UnsupportedModelProviderSchema))
        };
    }

    public void Receive(NetworkProxyChangedMessage message)
    {
        using var _ = _syncLock.EnterScope();

        // Invalidate the cached kernel mixin when the network proxy changes.
        DisposeCollector.DisposeToDefault(ref _cachedKernelMixin);
    }
}