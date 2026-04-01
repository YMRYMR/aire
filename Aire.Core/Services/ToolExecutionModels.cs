namespace Aire.Services
{
    public class DirectoryEntry
    {
        public bool IsDirectory { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Modified { get; set; } = string.Empty;

        public string DisplayName => IsDirectory ? Name + "/" : Name;
        public string Meta => IsDirectory ? string.Empty : Size;
    }

    public class DirectoryListing
    {
        public string Path { get; set; } = string.Empty;
        public List<DirectoryEntry> Entries { get; set; } = new();

        public string Summary
        {
            get
            {
                var dirs = Entries.Count(e => e.IsDirectory);
                var files = Entries.Count(e => !e.IsDirectory);
                var parts = new List<string>();

                if (dirs > 0)
                    parts.Add($"{dirs} folder{(dirs == 1 ? string.Empty : "s")}");
                if (files > 0)
                    parts.Add($"{files} file{(files == 1 ? string.Empty : "s")}");

                return parts.Count == 0 ? "Empty directory" : string.Join(", ", parts);
            }
        }
    }

    public class ToolExecutionResult
    {
        public string TextResult { get; set; } = string.Empty;
        public DirectoryListing? DirectoryListing { get; set; }
        public string? ScreenshotPath { get; set; }
    }
}
