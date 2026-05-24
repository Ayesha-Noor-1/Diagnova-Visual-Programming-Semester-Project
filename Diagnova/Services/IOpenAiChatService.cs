using Diagnova.Models;

namespace Diagnova.Services;

public interface IOpenAiChatService
{
    /// <param name="patientProfileContext">Self-reported profile text from MongoDB (medications, allergies, etc.).</param>
    Task<string> GetAssistantReplyAsync(
        IReadOnlyList<ChatTurn> priorTurns,
        string userMessage,
        string? patientProfileContext = null,
        CancellationToken cancellationToken = default);

    /// <summary>Uses the chat model to decide if the message is urgently risky given the profile (e.g. diabetes + bleeding).</summary>
    Task<AlarmingClassification?> ClassifyAlarmingAsync(
        string userMessage,
        string? patientProfileContext,
        CancellationToken cancellationToken = default);

    /// <summary>Second-stage model (see <c>OpenAI:EmergencyRoutingModel</c>): JSON facility suggestions near coordinates.</summary>
    Task<IReadOnlyList<EmergencyFacilityClientDto>> SuggestEmergencyFacilitiesAsync(
        double patientLatitude,
        double patientLongitude,
        string symptomMessage,
        string? patientProfileContext,
        CancellationToken cancellationToken = default);

    /// <summary>One condition-specific daily tip (diet, exercise, or lifestyle).</summary>
    Task<DailyTipGeneration?> GenerateDailyConditionTipAsync(
        IReadOnlyList<string> conditions,
        string category,
        string? patientProfileContext,
        CancellationToken cancellationToken = default);
}

public sealed class DailyTipGeneration
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public sealed record ChatTurn(string Role, string Content);
