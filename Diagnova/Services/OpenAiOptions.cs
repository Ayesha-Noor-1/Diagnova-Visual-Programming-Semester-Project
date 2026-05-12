namespace Diagnova.Services;

public class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Chat Completions model id (e.g. gpt-4o-mini, or openai/gpt-4o-mini on OpenRouter).</summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>API root ending with / (default OpenAI). Use https://openrouter.ai/api/v1/ for OpenRouter.</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";

    /// <summary>Optional Referer URL (OpenRouter recommends setting this).</summary>
    public string? Referer { get; set; }

    /// <summary>Optional app name sent as X-Title (OpenRouter).</summary>
    public string? SiteTitle { get; set; }
}
