using System;
using System.IO;
using System.Text.Json.Serialization;

namespace Aire.Data
{
    /// <summary>
    /// Describes one file attached to a chat message.
    /// The payload is path-based so the app can persist and reopen local files without
    /// forcing every provider to support arbitrary binary transport.
    /// </summary>
    public sealed class MessageAttachment
    {
        public string FilePath { get; set; } = string.Empty;
        public string? FileName { get; set; }
        public string? MimeType { get; set; }
        public long SizeBytes { get; set; }
        public bool IsImage { get; set; }
        public bool IsInlinePreview { get; set; }

        [JsonIgnore]
        public string DisplayName => string.IsNullOrWhiteSpace(FileName)
            ? Path.GetFileName(FilePath)
            : FileName;

        [JsonIgnore]
        public string SizeLabel => SizeBytes switch
        {
            < 1024 => $"{SizeBytes} B",
            < 1_048_576 => $"{SizeBytes / 1024.0:F1} KB",
            _ => $"{SizeBytes / 1_048_576.0:F1} MB"
        };

        [JsonIgnore]
        public string DisplayLabel => $"{DisplayName} ({SizeLabel})";
    }
}
