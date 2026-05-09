namespace Diagnova.Services;

public class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Chat Completions model id (e.g. gpt-4o-mini).</summary>
    public string Model { get; set; } = "gpt-4o-mini";
}
