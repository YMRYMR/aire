namespace Aire.Data
{
    /// <summary>
    /// Represents a chat message within a conversation.
    /// </summary>
    public class Message
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
