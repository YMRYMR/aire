namespace Aire.Services.Email
{
    public class EmailSummary
    {
        public string Id          { get; set; } = string.Empty;   // UniqueId.ToString()
        public string Subject     { get; set; } = string.Empty;
        public string From        { get; set; } = string.Empty;
        public string Date        { get; set; } = string.Empty;
        public string BodyPreview { get; set; } = string.Empty;   // first ~200 chars
        public bool   IsRead      { get; set; }
    }
}
