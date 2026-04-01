namespace Aire.Data
{
    public class Provider
    {
        /// <summary>Default provider request timeout in minutes, used when no override is configured.</summary>
        public const int DefaultTimeoutMinutes = 5;

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? ApiKey { get; set; }
        public string? BaseUrl { get; set; }
        public string Model { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public string Color { get; set; } = "#007ACC";
        public int SortOrder { get; set; }
        public int TimeoutMinutes { get; set; } = DefaultTimeoutMinutes;

        public string DisplayType => Type switch
        {
            "OpenAI" => "OpenAI",
            "Codex" => "Codex",
            "Anthropic" => "Anthropic API",
            "ClaudeWeb" => "Claude.ai",
            "GoogleAI" => "Google AI",
            "DeepSeek" => "DeepSeek",
            "Inception" => "Inception",
            "Ollama" => "Ollama",
            _ => Type
        };
    }
}
