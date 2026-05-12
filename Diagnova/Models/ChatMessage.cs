namespace Diagnova.Models;

public class ChatMessage
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public ChatSession Session { get; set; } = null!;

    /// <summary>OpenAI-style roles: user, assistant, system.</summary>
    public string Role { get; set; } = "user";

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
