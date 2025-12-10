namespace Everywhere.Chat;

public interface IChatService
{
    /// <summary>
    /// Send a message to the chat service.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task SendMessageAsync(UserChatMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Retry sending a message that previously failed. This will create a branch in the chat history.
    /// </summary>
    /// <param name="node"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task RetryAsync(ChatMessageNode node, CancellationToken cancellationToken);

    /// <summary>
    /// Edit a previously sent user message. This will create a branch in the chat history.
    /// </summary>
    /// <param name="originalNode"></param>
    /// <param name="newMessage"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task EditAsync(ChatMessageNode originalNode, UserChatMessage newMessage, CancellationToken cancellationToken);
}