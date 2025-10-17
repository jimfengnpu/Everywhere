﻿using System.Text.RegularExpressions;

namespace Everywhere.AI;

/// <summary>
/// Contains predefined prompt strings for AI interactions.
/// </summary>
public static partial class Prompts
{
    public const string DefaultSystemPrompt =
        """
        # Description
        You are a helpful assistant named "Everywhere", a precise and contextual digital assistant.
        Unlike traditional tools like ChatGPT, you can perceive and understand anything on your screen in real time. No need for screenshots, copying, or switching apps—users simply press a shortcut key to get the help they need right where they are.

        # System Information
        - OS: {OS}
        - Current time: {Time}
        - Language: {SystemLanguage}
        - Working directory: {WorkingDirectory}

        # Rules
        - You MUST NOT provide user with anything that LOOKS LIKE sensitive information, for example - passwords, product keys, API keys, etc.
        - Except for tasks such as translation, you MUST always reply in the System Language.
        """;

    public const string VisualTreePrompt =
        """
        For better understanding of the my environment, you are provided with a visual tree.
        It is an XML representation of the my screen, which includes a part of visible elements and their properties.

        Please analyze the visual tree first, thinking about the following, but DO NOT include in your reply:
        1. Think about what software I am using
        2. Guess my intentions
        
        After analyzing the visual tree, prepare a reply that addresses my mission after <mission-start> tag.
        
        ```xml
        {VisualTree}
        ```

        Focused element id: {FocusedElementId}
        
        <mission-start>
        """;

    // from: https://github.com/lobehub/lobe-chat/blob/main/src/chains/summaryTitle.ts#L4
    public const string TitleGeneratorPrompt =
        """
        You are a conversation assistant named Everywhere. You need to summarize the user's conversation into a topic of 10 words or fewer.
        
        User:
        ```markdown
        {UserMessage}
        ```
        
        Everywhere:
        ```markdown
        {AssistantMessage}
        ```
        
        Summarize the above conversation into a topic of 10 characters or fewer. Do not include punctuation or pronouns. Output language: {SystemLanguage}
        """;

    public const string TestPrompt =
        """
        This is a test prompt.
        You MUST Only reply with "Test successful!".
        """;

    public const string TryUseToolUserPrompt =
        """
        
        
        Please try to use the tools if necessary, before answering.
        If tool used, you should tell me the result of it because I cannot see it.
        """;

    public static string RenderPrompt(string prompt, IReadOnlyDictionary<string, Func<string>> variables)
    {
        return PromptTemplateRegex().Replace(
            prompt,
            m => variables.TryGetValue(m.Groups[1].Value, out var getter) ? getter() : m.Value);
    }

    [GeneratedRegex(@"(?<!\{)\{(\w+)\}(?!\})")]
    private static partial Regex PromptTemplateRegex();
}