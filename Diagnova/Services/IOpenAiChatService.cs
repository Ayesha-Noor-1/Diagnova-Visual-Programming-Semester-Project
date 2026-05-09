namespace Diagnova.Services;

public interface IOpenAiChatService
{
    Task<string> GetAssistantReplyAsync(
        IReadOnlyList<ChatTurn> priorTurns,
        string userMessage,
        CancellationToken cancellationToken = default);
}

public sealed record ChatTurn(string Role, string Content);
