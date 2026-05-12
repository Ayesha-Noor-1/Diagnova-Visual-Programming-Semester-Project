namespace Diagnova.Services;

public interface IOpenAiChatService
{
    /// <param name="patientProfileContext">Self-reported profile text from MongoDB (medications, allergies, etc.).</param>
    Task<string> GetAssistantReplyAsync(
        IReadOnlyList<ChatTurn> priorTurns,
        string userMessage,
        string? patientProfileContext = null,
        CancellationToken cancellationToken = default);
}

public sealed record ChatTurn(string Role, string Content);
