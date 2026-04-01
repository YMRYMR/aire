namespace Aire.UI.Settings.Models
{
    internal sealed class OllamaModelItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public bool IsInstalled { get; set; }
        public string SizeStr { get; set; } = string.Empty;
        public bool IsRecommended { get; set; }
        public string RecommendationLabel { get; set; } = string.Empty;
    }
}
