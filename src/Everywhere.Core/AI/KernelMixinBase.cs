using Everywhere.Common;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Everywhere.AI;

public abstract class KernelMixinBase(CustomAssistant customAssistant) : IKernelMixin
{
    // cache properties for comparison
    public ModelProviderSchema Schema { get; } = customAssistant.Schema;

    public string Endpoint { get; } = customAssistant.Endpoint?.Trim().Trim('/') ??
        throw new HandledChatException(
            new InvalidOperationException("Endpoint cannot be empty."),
            HandledChatExceptionType.InvalidEndpoint);

    public string? ApiKey { get; } = Configuration.ApiKey.GetKey(customAssistant.ApiKey);

    protected string EnsureApiKey() => ApiKey ??
        throw new HandledChatException(
            new InvalidOperationException("API Key cannot be empty."),
            HandledChatExceptionType.InvalidApiKey);

    public string ModelId { get; } = customAssistant.ModelId ??
        throw new HandledChatException(
            new InvalidOperationException("Model ID cannot be empty."),
            HandledChatExceptionType.InvalidConfiguration);

    public int RequestTimeoutSeconds { get; } = customAssistant.RequestTimeoutSeconds;

    public int ContextWindow => _customAssistant.MaxTokens;

    public bool IsImageInputSupported => _customAssistant.IsImageInputSupported;

    public bool IsFunctionCallingSupported => _customAssistant.IsFunctionCallingSupported;

    public bool IsDeepThinkingSupported => _customAssistant.IsDeepThinkingSupported;

    public abstract IChatCompletionService ChatCompletionService { get; }

    /// <summary>
    /// WARNING: properties are mutable!
    /// </summary>
    protected readonly CustomAssistant _customAssistant = customAssistant;

    /// <summary>
    /// indicates whether the model is reasoning
    /// </summary>
    protected static readonly AdditionalPropertiesDictionary ReasoningProperties = new()
    {
        ["reasoning"] = true
    };

    protected static AdditionalPropertiesDictionary ApplyReasoningProperties(AdditionalPropertiesDictionary? dictionary)
    {
        if (dictionary is null) return ReasoningProperties;
        dictionary["reasoning"] = true;
        return dictionary;
    }

    public abstract PromptExecutionSettings? GetPromptExecutionSettings(FunctionChoiceBehavior? functionChoiceBehavior = null);

    public Task CheckConnectivityAsync(CancellationToken cancellationToken = default) => ChatCompletionService.GetChatMessageContentAsync(
        [
            new ChatMessageContent(AuthorRole.System, "You're a helpful assistant."),
            new ChatMessageContent(AuthorRole.User, Prompts.TestPrompt)
        ],
        GetPromptExecutionSettings(),
        cancellationToken: cancellationToken);

    public virtual void Dispose() { }
}