using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI;
using OpenAI.Responses;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using FunctionCallContent = Microsoft.Extensions.AI.FunctionCallContent;
using TextContent = Microsoft.Extensions.AI.TextContent;

namespace Everywhere.AI;

/// <summary>
/// An implementation of <see cref="IKernelMixin"/> for OpenAI models via Responses API.
/// </summary>
public sealed class OpenAIResponsesKernelMixin : KernelMixinBase
{
    public override IChatCompletionService ChatCompletionService { get; }

    public OpenAIResponsesKernelMixin(
        CustomAssistant customAssistant,
        HttpClient httpClient,
        ILoggerFactory loggerFactory
    ) : base(customAssistant)
    {
        ChatCompletionService = new OptimizedOpenAIApiClient(
            new OpenAIResponseClient(
                ModelId,
                new ApiKeyCredential(ApiKey.IsNullOrWhiteSpace() ? "NO_API_KEY" : ApiKey),
                new OpenAIClientOptions
                {
                    Endpoint = new Uri(Endpoint, UriKind.Absolute),
                    Transport = new HttpClientPipelineTransport(httpClient, true, loggerFactory)
                }
            ).AsIChatClient(),
            this
        ).AsChatCompletionService();
    }

    public override PromptExecutionSettings GetPromptExecutionSettings(FunctionChoiceBehavior? functionChoiceBehavior = null)
    {
        double? temperature = _customAssistant.Temperature.IsCustomValueSet ? _customAssistant.Temperature.ActualValue : null;
        double? topP = _customAssistant.TopP.IsCustomValueSet ? _customAssistant.TopP.ActualValue : null;
        double? presencePenalty = _customAssistant.PresencePenalty.IsCustomValueSet ? _customAssistant.PresencePenalty.ActualValue : null;
        double? frequencyPenalty = _customAssistant.FrequencyPenalty.IsCustomValueSet ? _customAssistant.FrequencyPenalty.ActualValue : null;

        return new OpenAIPromptExecutionSettings
        {
            Temperature = temperature,
            TopP = topP,
            PresencePenalty = presencePenalty,
            FrequencyPenalty = frequencyPenalty,
            FunctionChoiceBehavior = functionChoiceBehavior
        };
    }

    /// <summary>
    /// optimized wrapper around OpenAI's IChatClient to extract reasoning content from internal properties.
    /// </summary>
    private sealed class OptimizedOpenAIApiClient(IChatClient client, OpenAIResponsesKernelMixin owner) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            messages = EnsureCompatibilityFields(messages);
            return client.GetResponseAsync(messages, options, cancellationToken);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            messages = EnsureCompatibilityFields(messages);

            // MEAI not supporting Deep Thinking will skip adding the reasoning options
            // This is a workaround
            options ??= new ChatOptions();
            options.RawRepresentationFactory = RawRepresentationFactory;

            // cache the value to avoid property changes during enumeration
            await foreach (var update in client.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                // Ensure that all FunctionCallContent items have a unique CallId.
                for (var i = 0; i < update.Contents.Count; i++)
                {
                    var content = update.Contents[i];
                    switch (content)
                    {
                        case FunctionCallContent { Name.Length: > 0, CallId: null or { Length: 0 } } missingIdContent:
                        { 
                            // Generate a unique ToolCallId for the function call update.
                            update.Contents[i] = new FunctionCallContent(
                                Guid.CreateVersion7().ToString("N"),
                                missingIdContent.Name,
                                missingIdContent.Arguments);
                            break;
                        }
                        case TextReasoningContent reasoningContent:
                        { 
                            // Semantic Kernel won't handle TextReasoningContent, convert it to TextContent with reasoning properties
                            update.Contents[i] = new TextContent(reasoningContent.Text)
                            {
                                AdditionalProperties = ReasoningProperties
                            };
                            update.AdditionalProperties = ApplyReasoningProperties(update.AdditionalProperties);
                            break;
                        }
                    }
                }

                yield return update;
            }
        }

        private object? RawRepresentationFactory(IChatClient chatClient) => owner.IsDeepThinkingSupported ?
            new ResponseCreationOptions
            {
                ReasoningOptions = new ResponseReasoningOptions
                {
                    ReasoningSummaryVerbosity = ResponseReasoningSummaryVerbosity.Detailed
                }
            } :
            null;

        /// <summary>
        /// Ensure each ChatMessage contains the compatibility fields required by some models/clients.
        /// We use reflection to avoid compile-time dependency on the concrete ChatMessage shape.
        /// The fields added are: 'refusal', 'annotations', 'audio', 'function_call' (all set to null).
        /// </summary>
        private static IEnumerable<ChatMessage> EnsureCompatibilityFields(IEnumerable<ChatMessage> messages)
        {
            foreach (var msg in messages)
            {
                if (msg.AdditionalProperties is { } dict)
                {
                    dict.TryAdd("refusal", null);
                    dict.TryAdd("annotations", null);
                    dict.TryAdd("audio", null);
                    dict.TryAdd("function_call", null);
                }
                else
                {
                    msg.AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        ["refusal"] = null,
                        ["annotations"] = null,
                        ["audio"] = null,
                        ["function_call"] = null
                    };
                }

                yield return msg;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return client.GetService(serviceType, serviceKey);
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }
}