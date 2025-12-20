namespace Everywhere.AI;

/// <summary>
/// Represents a provider template for customizing assistant.
/// This used for both online and local models.
/// </summary>
public record ModelProviderTemplate
{
    /// <summary>
    /// Unique identifier for the model provider.
    /// This ID is used to distinguish between different providers.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name of the model provider, used for UI.
    /// This name is shown to the user in the application's settings or model selection UI.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// This icon is displayed next to the provider's name in the UI.
    /// </summary>
    public string? LightIconUrl { get; set; }

    /// <summary>
    /// This icon is displayed next to the provider's name in the UI.
    /// </summary>
    public string? DarkIconUrl { get; set; }

    /// <summary>
    /// Endpoint URL for the model provider's API.
    /// e.g., "https://api.example.com/v1/models".
    /// This URL is used to send requests to the model provider's servers.
    /// </summary>
    public required string Endpoint { get; set; }

    /// <summary>
    /// Official website URL for the model provider, if available.
    /// This URL is displayed to the user for more information about the provider.
    /// </summary>
    public string? OfficialWebsiteUrl { get; set; }

    /// <summary>
    /// Documentation URL for the model provider, if available.
    /// This usually points to the Everywhere's user guide or API documentation.
    /// This URL provides users with detailed information on how to use
    /// the model provider's features and API.
    /// </summary>
    public string? DocumentationUrl { get; set; }

    /// <summary>
    /// Schema used by the model provider.
    /// This schema defines the structure of the data exchanged with the provider.
    /// </summary>
    public ModelProviderSchema Schema { get; set; }

    /// <summary>
    /// API key used to authenticate requests to the model provider.
    /// This key is required to access the model provider's API and is
    /// specific to the user's account.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Timeout for each request to the model, in seconds.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 20;

    /// <summary>
    /// A list of model definitions provided by this model provider.
    /// Each model definition describes a specific model offered by the provider,
    /// including its capabilities and limitations.
    /// </summary>
    public required IReadOnlyList<ModelDefinitionTemplate> ModelDefinitions { get; set; }

    public virtual bool Equals(ModelProviderTemplate? other) => Id == other?.Id;

    public override int GetHashCode() => Id.GetHashCode();

    public static ModelProviderTemplate[] SupportedTemplates { get; } =
    [
        new()
        {
            Id = "openai",
            DisplayName = "OpenAI",
            Endpoint = "https://api.openai.com/v1",
            OfficialWebsiteUrl = "https://openai.com",
            DarkIconUrl = "avares://Everywhere.Core/Assets/Icons/openai-dark.svg",
            LightIconUrl = "avares://Everywhere.Core/Assets/Icons/openai-light.svg",
            Schema = ModelProviderSchema.OpenAIResponses,
            ModelDefinitions =
            [
                new ModelDefinitionTemplate
                {
                    Id = "gpt-5.2",
                    ModelId = "gpt-5.2",
                    DisplayName = "GPT-5.2",
                    MaxTokens = 400_000,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                },
                new ModelDefinitionTemplate
                {
                    Id = "gpt-5.1",
                    ModelId = "gpt-5.1",
                    DisplayName = "GPT-5.1",
                    MaxTokens = 400_000,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                },
                new ModelDefinitionTemplate
                {
                    Id = "gpt-5",
                    ModelId = "gpt-5",
                    DisplayName = "GPT-5",
                    MaxTokens = 400_000,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                },
                new ModelDefinitionTemplate
                {
                    Id = "gpt-5-mini",
                    ModelId = "gpt-5-mini",
                    DisplayName = "GPT-5 mini",
                    MaxTokens = 400_000,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                },
                new ModelDefinitionTemplate
                {
                    Id = "o4-mini",
                    ModelId = "o4-mini",
                    DisplayName = "o4-mini",
                    MaxTokens = 200_000,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                },
                new ModelDefinitionTemplate
                {
                    Id = "gpt-4.1",
                    ModelId = "gpt-4.1",
                    DisplayName = "GPT 4.1",
                    MaxTokens = 1_000_000,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = false,
                    IsDefault = true
                },
                new ModelDefinitionTemplate
                {
                    Id = "gpt-4.1-mini",
                    ModelId = "gpt-4.1-mini",
                    DisplayName = "GPT 4.1 mini",
                    MaxTokens = 1_000_000,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = false,
                },
                new ModelDefinitionTemplate
                {
                    Id = "gpt-4o",
                    ModelId = "gpt-4o",
                    DisplayName = "GPT-4o",
                    MaxTokens = 128_000,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = false,
                }
            ]
        },
        new()
        {
            Id = "anthropic",
            DisplayName = "Anthropic (Claude)",
            Endpoint = "https://api.anthropic.com",
            OfficialWebsiteUrl = "https://www.anthropic.com",
            DarkIconUrl = "avares://Everywhere.Core/Assets/Icons/anthropic-dark.svg",
            LightIconUrl = "avares://Everywhere.Core/Assets/Icons/anthropic-light.svg",
            Schema = ModelProviderSchema.Anthropic,
            ModelDefinitions =
            [
                new ModelDefinitionTemplate
                {
                    Id = "claude-opus-4-5-20251101",
                    ModelId = "claude-opus-4-5-20251101",
                    DisplayName = "Claude Opus 4.5",
                    MaxTokens = 200_000,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                },
                new ModelDefinitionTemplate
                {
                    Id = "claude-sonnet-4-5-20250929",
                    ModelId = "claude-sonnet-4-5-20250929",
                    DisplayName = "Claude Sonnet 4.5",
                    MaxTokens = 200_000,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                },
                new ModelDefinitionTemplate
                {
                    Id = "claude-haiku-4-5-20251001",
                    ModelId = "claude-haiku-4-5-20251001",
                    DisplayName = "Claude Haiku 4.5",
                    MaxTokens = 200_000,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                },
                new ModelDefinitionTemplate
                {
                    Id = "claude-opus-4-1-20250805",
                    ModelId = "claude-opus-4-1-20250805",
                    DisplayName = "Claude Opus 4.1",
                    MaxTokens = 200_000,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                },
                new ModelDefinitionTemplate
                {
                    Id = "claude-opus-4-20250514",
                    ModelId = "claude-opus-4-20250514",
                    DisplayName = "Claude Opus 4",
                    MaxTokens = 200_000,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                },
                new ModelDefinitionTemplate
                {
                    Id = "claude-sonnet-4-20250514",
                    ModelId = "claude-sonnet-4-20250514",
                    DisplayName = "Claude Sonnet 4",
                    MaxTokens = 200_000,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                },
                new ModelDefinitionTemplate
                {
                    Id = "claude-3-7-sonnet-20250219",
                    ModelId = "claude-3-7-sonnet-20250219",
                    DisplayName = "Claude 3.7 Sonnet",
                    MaxTokens = 200_000,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                    IsDefault = true
                },
                new ModelDefinitionTemplate
                {
                    Id = "claude-3-5-haiku-20241022",
                    ModelId = "claude-3-5-haiku-20241022",
                    DisplayName = "Claude 3.5 Haiku",
                    MaxTokens = 200_000,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = false,
                }
            ]
        },
        new()
        {
            Id = "google",
            DisplayName = "Google (Gemini)",
            OfficialWebsiteUrl = "https://gemini.google.com",
            Endpoint = "https://generativelanguage.googleapis.com/v1beta",
            DarkIconUrl = "avares://Everywhere.Core/Assets/Icons/google-color.svg",
            LightIconUrl = "avares://Everywhere.Core/Assets/Icons/google-color.svg",
            Schema = ModelProviderSchema.Google,
            ModelDefinitions =
            [
                new ModelDefinitionTemplate
                {
                    Id = "gemini-3-pro-preview",
                    ModelId = "gemini-3-pro-preview",
                    DisplayName = "Gemini 3 Pro Preview",
                    MaxTokens = 1_048_576,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                },
                new ModelDefinitionTemplate
                {
                    Id = "gemini-3-flash-preview",
                    ModelId = "gemini-3-flash-preview",
                    DisplayName = "Gemini 3 Flash Preview",
                    MaxTokens = 1_048_576,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                },
                new ModelDefinitionTemplate
                {
                    Id = "gemini-2.5-pro",
                    ModelId = "gemini-2.5-pro",
                    DisplayName = "Gemini 2.5 Pro",
                    MaxTokens = 1_048_576,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                },
                new ModelDefinitionTemplate
                {
                    Id = "gemini-2.5-flash",
                    ModelId = "gemini-2.5-flash",
                    DisplayName = "Gemini 2.5 Flash",
                    MaxTokens = 1_048_576,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                    IsDefault = true
                },
                new ModelDefinitionTemplate
                {
                    Id = "gemini-2.5-flash-lite",
                    ModelId = "gemini-2.5-flash-lite",
                    DisplayName = "Gemini 2.5 Flash-Lite",
                    MaxTokens = 1_048_576,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                }
            ]
        },
        new()
        {
            Id = "deepseek",
            DisplayName = "DeepSeek",
            Endpoint = "https://api.deepseek.com",
            OfficialWebsiteUrl = "https://www.deepseek.com",
            DarkIconUrl = "avares://Everywhere.Core/Assets/Icons/deepseek-color.svg",
            LightIconUrl = "avares://Everywhere.Core/Assets/Icons/deepseek-color.svg",
            Schema = ModelProviderSchema.OpenAI,
            ModelDefinitions =
            [
                new ModelDefinitionTemplate
                {
                    Id = "deepseek-chat",
                    ModelId = "deepseek-chat",
                    DisplayName = "DeepSeek V3.2 (Non-thinking Mode)",
                    MaxTokens = 128_000,
                    IsImageInputSupported = false,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = false,
                    IsDefault = true
                },
                new ModelDefinitionTemplate
                {
                    Id = "deepseek-reasoner",
                    ModelId = "deepseek-reasoner",
                    DisplayName = "DeepSeek V3.2 (Thinking Mode)",
                    MaxTokens = 128_000,
                    IsImageInputSupported = false,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                }
            ]
        },
        new()
        {
            Id = "moonshot",
            DisplayName = "Moonshot (Kimi)",
            Endpoint = "https://api.moonshot.cn/v1",
            OfficialWebsiteUrl = "https://www.moonshot.cn",
            DarkIconUrl = "avares://Everywhere.Core/Assets/Icons/moonshot-dark.svg",
            LightIconUrl = "avares://Everywhere.Core/Assets/Icons/moonshot-light.svg",
            Schema = ModelProviderSchema.OpenAI,
            ModelDefinitions =
            [
                new ModelDefinitionTemplate
                {
                    Id = "kimi-k2-0905-preview",
                    ModelId = "kimi-k2-0905-preview",
                    DisplayName = "Kimi K2",
                    MaxTokens = 262_144,
                    IsImageInputSupported = false,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = false,
                },
                new ModelDefinitionTemplate
                {
                    Id = "kimi-k2-turbo-preview",
                    ModelId = "kimi-k2-turbo-preview",
                    DisplayName = "Kimi K2 Turbo",
                    MaxTokens = 262_144,
                    IsImageInputSupported = false,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = false,
                },
                new ModelDefinitionTemplate
                {
                    Id = "kimi-k2-thinking",
                    ModelId = "kimi-k2-thinking",
                    DisplayName = "Kimi K2 Thinking",
                    MaxTokens = 262_144,
                    IsImageInputSupported = false,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                },
                new ModelDefinitionTemplate
                {
                    Id = "kimi-k2-thinking-turbo",
                    ModelId = "kimi-k2-thinking-turbo",
                    DisplayName = "Kimi K2 Thinking Turbo",
                    MaxTokens = 262_144,
                    IsImageInputSupported = false,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                },
                new ModelDefinitionTemplate
                {
                    Id = "kimi-latest",
                    ModelId = "kimi-latest",
                    DisplayName = "Kimi Latest",
                    MaxTokens = 128_000,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = false,
                    IsDefault = true
                }
            ]
        },
        new()
        {
            Id = "openrouter",
            DisplayName = "OpenRouter",
            OfficialWebsiteUrl = "https://openrouter.ai",
            Endpoint = "https://openrouter.ai/api/v1",
            DarkIconUrl = "avares://Everywhere.Core/Assets/Icons/openrouter-dark.svg",
            LightIconUrl = "avares://Everywhere.Core/Assets/Icons/openrouter-light.svg",
            Schema = ModelProviderSchema.OpenAI,
            ModelDefinitions =
            [
                new ModelDefinitionTemplate
                {
                    Id = "google/gemini-2.5-flash",
                    ModelId = "google/gemini-2.5-flash",
                    DisplayName = "Google: Gemini 2.5 Flash",
                    MaxTokens = 1_048_576,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                },
                new ModelDefinitionTemplate
                {
                    Id = "anthropic/claude-sonnet-4.5",
                    ModelId = "anthropic/claude-sonnet-4.5",
                    DisplayName = "Anthropic: Claude Sonnet 4.5",
                    MaxTokens = 1_000_000,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = false,
                },
                new ModelDefinitionTemplate
                {
                    Id = "anthropic/claude-opus-4.5",
                    ModelId = "anthropic/claude-sonnet-4.5",
                    DisplayName = "Anthropic: Claude Opus 4.5",
                    MaxTokens = 200_000,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = false,
                },
                new ModelDefinitionTemplate
                {
                    Id = "deepseek/deepseek-v3.2",
                    ModelId = "deepseek/deepseek-v3.2",
                    DisplayName = "DeepSeek: DeepSeek V3.2",
                    MaxTokens = 163_840,
                    IsImageInputSupported = false,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                },
                new ModelDefinitionTemplate
                {
                    Id = "openai/gpt-oss-120b",
                    ModelId = "openai/gpt-oss-120b",
                    DisplayName = "OpenAI: GPT-OSS 120B",
                    MaxTokens = 131_072,
                    IsImageInputSupported = false,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true
                },
                new ModelDefinitionTemplate
                {
                    Id = "x-ai/grok-4-fast",
                    ModelId = "x-ai/grok-4-fast",
                    DisplayName = "X-AI: Grok 4 Fast",
                    MaxTokens = 2_000_000,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                    IsDefault = true
                }
            ]
        },
        new()
        {
            Id = "siliconcloud",
            DisplayName = "SiliconCloud (SiliconFlow)",
            OfficialWebsiteUrl = "https://www.siliconflow.cn",
            Endpoint = "https://api.siliconflow.cn/v1",
            DarkIconUrl = "avares://Everywhere.Core/Assets/Icons/siliconcloud-color.svg",
            LightIconUrl = "avares://Everywhere.Core/Assets/Icons/siliconcloud-color.svg",
            Schema = ModelProviderSchema.OpenAI,
            ModelDefinitions =
            [
                new ModelDefinitionTemplate
                {
                    Id = "Qwen/Qwen3-8B",
                    ModelId = "Qwen/Qwen3-8B",
                    DisplayName = "Qwen3-8B (free)",
                    MaxTokens = 128_000,
                    IsImageInputSupported = false,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                    IsDefault = true
                },
                new ModelDefinitionTemplate
                {
                    Id = "zai-org/GLM-4.6V",
                    ModelId = "zai-org/GLM-4.6V",
                    DisplayName = "GLM 4.6V",
                    MaxTokens = 128_000,
                    IsImageInputSupported = true,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true
                },
                new ModelDefinitionTemplate
                {
                    Id = "moonshotai/Kimi-K2-Thinking",
                    ModelId = "moonshotai/Kimi-K2-Thinking",
                    DisplayName = "Kimi K2 Thinking",
                    MaxTokens = 256_000,
                    IsImageInputSupported = false,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true
                },
                new ModelDefinitionTemplate
                {
                    Id = "MiniMaxAI/MiniMax-M2",
                    ModelId = "MiniMaxAI/MiniMax-M2",
                    DisplayName = "MiniMax M2",
                    MaxTokens = 192_000,
                    IsImageInputSupported = false,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true
                },
                new ModelDefinitionTemplate
                {
                    Id = "deepseek-ai/DeepSeek-V3.2",
                    ModelId = "deepseek-ai/DeepSeek-V3.2",
                    DisplayName = "DeepSeek-V3.2",
                    MaxTokens = 160_000,
                    IsImageInputSupported = false,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                }
            ]
        },
        new()
        {
            Id = "ollama",
            DisplayName = "Ollama",
            OfficialWebsiteUrl = "https://ollama.com",
            Endpoint = "http://127.0.0.1:11434",
            DarkIconUrl = "avares://Everywhere.Core/Assets/Icons/ollama-dark.svg",
            LightIconUrl = "avares://Everywhere.Core/Assets/Icons/ollama-light.svg",
            Schema = ModelProviderSchema.Ollama,
            RequestTimeoutSeconds = 120, // Local models may take longer time.
            ModelDefinitions =
            [
                new ModelDefinitionTemplate
                {
                    Id = "gpt-oss:20b",
                    ModelId = "gpt-oss:20b",
                    DisplayName = "GPT-OSS 20B",
                    MaxTokens = 128_000,
                    IsImageInputSupported = false,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = true,
                },
                new ModelDefinitionTemplate
                {
                    Id = "deepseek-r1:8b",
                    ModelId = "deepseek-r1:8b",
                    DisplayName = "DeepSeek R1 8B",
                    MaxTokens = 128_000,
                    IsImageInputSupported = false,
                    IsFunctionCallingSupported = false,
                    IsDeepThinkingSupported = true,
                    IsDefault = true
                },
                new ModelDefinitionTemplate
                {
                    Id = "qwen3:8b",
                    ModelId = "qwen3:8b",
                    DisplayName = "Qwen 3 8B",
                    MaxTokens = 40_000,
                    IsImageInputSupported = false,
                    IsFunctionCallingSupported = true,
                    IsDeepThinkingSupported = false,
                }
            ]
        }
    ];
}