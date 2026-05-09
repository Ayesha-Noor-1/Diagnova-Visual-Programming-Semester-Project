namespace Diagnova.Models;

public class ChatSession
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>When set, this session is closed and a new one will be created on next message.</summary>
    public DateTimeOffset? ClosedAt { get; set; }

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
