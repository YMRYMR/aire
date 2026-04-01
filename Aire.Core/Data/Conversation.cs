namespace Aire.Data
{
    /// <summary>
    /// Represents a conversation thread.
    /// </summary>
    public class Conversation
    {
        public int Id { get; set; }
        public int ProviderId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
