using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Diagnova.Models;

namespace Diagnova.Services;

public sealed class OpenAiChatService : IOpenAiChatService
{
    private static readonly JsonSerializerOptions JsonRelaxed = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly HttpClient _http;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiChatService> _logger;

    public OpenAiChatService(HttpClient http, Microsoft.Extensions.Options.IOptions<OpenAiOptions> options, ILogger<OpenAiChatService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl)
            ? "https://api.openai.com/v1/"
            : _options.BaseUrl.Trim().TrimEnd('/') + "/";
        _http.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        _http.Timeout = TimeSpan.FromMinutes(2);
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey.Trim());
        if (!string.IsNullOrWhiteSpace(_options.Referer))
            _http.DefaultRequestHeaders.TryAddWithoutValidation("HTTP-Referer", _options.Referer.Trim());
        if (!string.IsNullOrWhiteSpace(_options.SiteTitle))
            _http.DefaultRequestHeaders.TryAddWithoutValidation("X-Title", _options.SiteTitle.Trim());
    }

    public async Task<string> GetAssistantReplyAsync(
        IReadOnlyList<ChatTurn> priorTurns,
        string userMessage,
        string? patientProfileContext = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return """
                **Configuration needed:** Add your OpenAI API key under configuration (`OpenAI:ApiKey` or User Secrets).

                Until then, Diagnova cannot call the model. This app is not a substitute for professional care.
                """;
        }

        var systemContent = string.IsNullOrWhiteSpace(patientProfileContext)
            ? MedicalSystemPrompt
            : $"""
                {MedicalSystemPrompt}

                ## Self-reported patient profile (from this user's Diagnova account)

                {patientProfileContext.Trim()}

                Use this when the user asks about their own medications, conditions, or background. It is self-reported and may be outdated — remind them to confirm with a clinician or pharmacist when discussing specific drugs or doses.
                """;

        var messages = new List<object>
        {
            new { role = "system", content = systemContent },
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
            return UserFacingChatHttpError((int)response.StatusCode);
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
        When the patient profile lists medications, you may summarize or discuss interactions at a high level, but do not invent doses or tell the user to start/stop a drug.
        """;

    public async Task<AlarmingClassification?> ClassifyAlarmingAsync(
        string userMessage,
        string? patientProfileContext,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return null;

        var profileBlock = string.IsNullOrWhiteSpace(patientProfileContext)
            ? "No structured profile was provided; use only the user's message."
            : patientProfileContext.Trim();

        var system = """
            You classify whether a self-reported symptom message is an ALARMING or time-sensitive medical situation for THIS user, using their profile when present.

            Examples of alarming (non-exhaustive):
            - Diabetes (any type in profile) + active bleeding, vomiting with inability to keep fluids down, confusion, severe abdominal pain, fruity breath, repeated hypoglycemia symptoms, or infection with high fever.
            - Pregnancy in profile + heavy bleeding or severe abdominal pain.
            - Anticoagulant / blood thinner context + significant bleeding.
            - Severe chest pain, stroke symptoms, anaphylaxis, airway compromise, altered consciousness — always alarming.

            Return ONLY valid JSON with keys:
            - "is_alarming" (boolean)
            - "reason" (one short sentence for the patient, no markdown)
            - "specialist_hint" (short phrase, e.g. "Emergency department" or "Endocrinology / diabetes care")

            Be conservative: if unsure but plausible emergency, set is_alarming true.
            """;

        var user = $"""
            Patient profile (self-reported; may be incomplete):
            {profileBlock}

            User message:
            {userMessage.Trim()}
            """;

        var model = string.IsNullOrWhiteSpace(_options.Model) ? "gpt-4o-mini" : _options.Model;
        var raw = await PostChatJsonAsync(model, system, user, temperature: 0.1, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            var slice = ExtractJsonObject(raw);
            var dto = JsonSerializer.Deserialize<AlarmingJsonDto>(slice, JsonRelaxed);
            if (dto is null)
                return null;
            return new AlarmingClassification
            {
                IsAlarming = dto.IsAlarming,
                Reason = string.IsNullOrWhiteSpace(dto.Reason) ? "Potential urgent risk based on your message and profile." : dto.Reason.Trim(),
                SpecialistHint = string.IsNullOrWhiteSpace(dto.SpecialistHint) ? "Emergency department" : dto.SpecialistHint.Trim(),
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse alarming classification JSON");
            return null;
        }
    }

    public async Task<IReadOnlyList<EmergencyFacilityClientDto>> SuggestEmergencyFacilitiesAsync(
        double patientLatitude,
        double patientLongitude,
        string symptomMessage,
        string? patientProfileContext,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return Array.Empty<EmergencyFacilityClientDto>();

        var routingModel = string.IsNullOrWhiteSpace(_options.EmergencyRoutingModel)
            ? (string.IsNullOrWhiteSpace(_options.Model) ? "gpt-4o-mini" : _options.Model)
            : _options.EmergencyRoutingModel.Trim();

        var profileBlock = string.IsNullOrWhiteSpace(patientProfileContext)
            ? "No profile text."
            : patientProfileContext.Trim();

        var system = """
            You help locate emergency care. The patient shared approximate GPS coordinates (WGS84).

            Return ONLY valid JSON:
            {
              "facilities": [
                {
                  "name": "string",
                  "address": "string",
                  "latitude": number,
                  "longitude": number,
                  "notes": "string optional"
                }
              ]
            }

            Rules:
            - Provide 1 to 3 facilities: emergency departments or major hospitals suitable for the symptoms.
            - Each latitude/longitude MUST be within 30 km of the patient's coordinates (prefer closer).
            - Prefer real, well-known hospitals in that geographic region when you know them; otherwise give plausible public hospitals and coordinates still within 30 km.
            - Never output facilities on another continent.
            - Notes should remind the user to verify hours/ER availability by phone.
            """;

        var user = $"""
            Patient latitude: {patientLatitude:F6}
            Patient longitude: {patientLongitude:F6}

            Profile (self-reported):
            {profileBlock}

            Symptom / user message:
            {symptomMessage.Trim()}
            """;

        var raw = await PostChatJsonAsync(routingModel, system, user, temperature: 0.2, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<EmergencyFacilityClientDto>();

        try
        {
            var slice = ExtractJsonObject(raw);
            var root = JsonSerializer.Deserialize<FacilitiesJsonRoot>(slice, JsonRelaxed);
            if (root?.Facilities is null || root.Facilities.Count == 0)
                return Array.Empty<EmergencyFacilityClientDto>();

            var list = new List<EmergencyFacilityClientDto>();
            foreach (var f in root.Facilities)
            {
                if (f is null || string.IsNullOrWhiteSpace(f.Name))
                    continue;
                if (f.Latitude is null || f.Longitude is null)
                    continue;
                if (Math.Abs(f.Latitude.Value) > 90 || Math.Abs(f.Longitude.Value) > 180)
                    continue;

                var d = GeoUtils.DistanceKm(patientLatitude, patientLongitude, f.Latitude.Value, f.Longitude.Value);
                if (d > 35)
                    continue;

                list.Add(new EmergencyFacilityClientDto
                {
                    Name = f.Name.Trim(),
                    Address = (f.Address ?? string.Empty).Trim(),
                    Latitude = f.Latitude.Value,
                    Longitude = f.Longitude.Value,
                    DistanceKm = d,
                    Notes = string.IsNullOrWhiteSpace(f.Notes) ? null : f.Notes.Trim(),
                });
            }

            return list.OrderBy(x => x.DistanceKm).Take(3).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse emergency facilities JSON");
            return Array.Empty<EmergencyFacilityClientDto>();
        }
    }

    private async Task<string?> PostChatJsonAsync(
        string model,
        string systemContent,
        string userContent,
        double temperature,
        CancellationToken cancellationToken)
    {
        var messages = new List<object>
        {
            new { role = "system", content = systemContent },
            new { role = "user", content = userContent },
        };

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["messages"] = messages,
            ["temperature"] = temperature,
            ["response_format"] = new { type = "json_object" },
        };

        using var response = await _http.PostAsJsonAsync("chat/completions", payload, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI JSON call error {Status}: {Body}", response.StatusCode, body);
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                return null;
            var first = choices[0];
            if (!first.TryGetProperty("message", out var message))
                return null;
            var content = message.TryGetProperty("content", out var c) ? c.GetString() : null;
            return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse OpenAI JSON chat response");
            return null;
        }
    }

    private static string ExtractJsonObject(string content)
    {
        var t = content.Trim();
        if (t.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNl = t.IndexOf('\n');
            if (firstNl >= 0)
                t = t[(firstNl + 1)..].Trim();
            var fence = t.LastIndexOf("```", StringComparison.Ordinal);
            if (fence >= 0)
                t = t[..fence].Trim();
        }

        var start = t.IndexOf('{');
        var end = t.LastIndexOf('}');
        if (start >= 0 && end > start)
            return t[start..(end + 1)];
        return t;
    }

    private sealed class AlarmingJsonDto
    {
        [JsonPropertyName("is_alarming")]
        public bool IsAlarming { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("specialist_hint")]
        public string? SpecialistHint { get; set; }
    }

    private sealed class FacilitiesJsonRoot
    {
        [JsonPropertyName("facilities")]
        public List<FacilityJsonDto>? Facilities { get; set; }
    }

    private sealed class FacilityJsonDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("latitude")]
        public double? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }

    public async Task<DailyTipGeneration?> GenerateDailyConditionTipAsync(
        IReadOnlyList<string> conditions,
        string category,
        string? patientProfileContext,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return null;

        var conditionList = conditions.Count > 0
            ? string.Join(", ", conditions)
            : "general wellness (no specific condition listed)";

        var profileBlock = string.IsNullOrWhiteSpace(patientProfileContext)
            ? ""
            : $"\nAdditional profile context:\n{patientProfileContext.Trim()}";

        var system = """
            You write one short, practical daily health tip for a patient app (Diagnova).
            Tips must be educational only — not diagnosis or prescription.
            Be specific to the patient's condition(s) when provided.
            Return ONLY valid JSON with keys:
            - "title" (short headline, max 8 words)
            - "body" (2-3 sentences, actionable, friendly)
            - "category" (echo the requested category: diet, exercise, or lifestyle)
            """;

        var user = $"""
            Today's focus category: {category}
            Registered conditions: {conditionList}
            {profileBlock}

            Example for diabetes + diet: suggest one low-GI meal idea appropriate for South Asia if region unknown.
            Write today's tip for this user.
            """;

        var model = string.IsNullOrWhiteSpace(_options.Model) ? "gpt-4o-mini" : _options.Model;
        var raw = await PostChatJsonAsync(model, system, user, temperature: 0.7, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            var slice = ExtractJsonObject(raw);
            var dto = JsonSerializer.Deserialize<DailyTipJsonDto>(slice, JsonRelaxed);
            if (dto is null || string.IsNullOrWhiteSpace(dto.Body))
                return null;

            return new DailyTipGeneration
            {
                Title = string.IsNullOrWhiteSpace(dto.Title) ? "Your daily tip" : dto.Title.Trim(),
                Body = dto.Body.Trim(),
                Category = string.IsNullOrWhiteSpace(dto.Category) ? category : dto.Category.Trim().ToLowerInvariant(),
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse daily tip JSON");
            return null;
        }
    }

    private sealed class DailyTipJsonDto
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }
    }

    private static string UserFacingChatHttpError(int statusCode)
    {
        if (statusCode == 401)
        {
            return """
                **401 Unauthorized** — the API host rejected authentication (usually the key, or the wrong API URL for that key).

                Typical fixes:
                - Match **key** to **BaseUrl**: OpenAI keys (`sk-…`) need `https://api.openai.com/v1/` (or leave BaseUrl empty). OpenRouter keys need `https://openrouter.ai/api/v1/` — they are **not** interchangeable.
                - Store secrets on **this** project: open a terminal in the **Diagnova** folder (where `Diagnova.csproj` is), then `dotnet user-secrets list` — you should see `OpenAI:ApiKey`.
                - Remove a bad **environment variable** override: `OpenAI__ApiKey` (Windows user/system env) overrides User Secrets if set.
                - Re-paste the key with **no spaces** or line breaks at the start/end.

                The console log line `OpenAI error 401:` includes the provider response body.
                """;
        }

        return $"The assistant could not respond right now ({statusCode}). Please try again.";
    }
}
