using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Diagnova.Services;

public sealed class OpenAiChatService : IOpenAiChatService
{
    private readonly HttpClient _http;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiChatService> _logger;

    public OpenAiChatService(HttpClient http, Microsoft.Extensions.Options.IOptions<OpenAiOptions> options, ILogger<OpenAiChatService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        _http.BaseAddress = new Uri("https://api.openai.com/v1/");
        _http.Timeout = TimeSpan.FromMinutes(2);
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey.Trim());
    }

    public async Task<string> GetAssistantReplyAsync(
        IReadOnlyList<ChatTurn> priorTurns,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return """
                **Configuration needed:** Add your OpenAI API key under configuration (`OpenAI:ApiKey` or User Secrets).

                Until then, Diagnova cannot call the model. This app is not a substitute for professional care.
                """;
        }

        var messages = new List<object>
        {
            new { role = "system", content = MedicalSystemPrompt },
        };

        foreach (var t in priorTurns)
        {
            if (string.IsNullOrWhiteSpace(t.Content))
                continue;
            messages.Add(new { role = t.Role, content = t.Content });
        }

        messages.Add(new { role = "user", content = userMessage });

        var payload = new
        {
            model = string.IsNullOrWhiteSpace(_options.Model) ? "gpt-4o-mini" : _options.Model,
            messages,
            temperature = 0.4,
        };

        using var response = await _http.PostAsJsonAsync("chat/completions", payload, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI error {Status}: {Body}", response.StatusCode, body);
            return $"The assistant could not respond right now ({(int)response.StatusCode}). Please try again.";
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                return "No response text returned from the model.";

            var first = choices[0];
            if (!first.TryGetProperty("message", out var message))
                return "No response text returned from the model.";

            var content = message.TryGetProperty("content", out var c) ? c.GetString() : null;
            return string.IsNullOrWhiteSpace(content)
                ? "No response text returned from the model."
                : content.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse OpenAI response");
            return "Received an unexpected response from the assistant.";
        }
    }

    private const string MedicalSystemPrompt = """
        You are Diagnova, a cautious medical triage assistant (not a licensed clinician).

        Always:
        - Respond in clear Markdown with headings.
        - Include: possible conditions (as possibilities, not diagnoses), severity estimate (mild/moderate/severe),
          whether to rest at home vs seek urgent/emergency care, and which medical specialist type fits best with a short rationale.
        - If emergency symptoms might apply, tell the user to call local emergency services immediately.
        - Ask clarifying questions when needed.

        Never claim certainty. Never prescribe medications or doses.
        """;
}
