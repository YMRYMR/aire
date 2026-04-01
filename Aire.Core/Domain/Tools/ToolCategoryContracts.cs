using System;
using System.Collections.Generic;
using System.Linq;

namespace Aire.Domain.Tools
{
    /// <summary>
    /// Stable tool-category definitions shared by UI, application services, and provider filtering.
    /// </summary>
    public static class ToolCategoryCatalog
    {
        public static readonly string[] KnownCategories =
        {
            "filesystem",
            "browser",
            "agent",
            "mouse",
            "keyboard",
            "system",
            "email",
        };

        public static IReadOnlyList<ToolCategoryOption> Options { get; } = new[]
        {
            new ToolCategoryOption("filesystem", "Files", "Read and modify local files and folders."),
            new ToolCategoryOption("browser", "Browser", "Inspect and control browser tabs and pages."),
            new ToolCategoryOption("agent", "Agent", "Use planning, follow-up, and task-management tools."),
            new ToolCategoryOption("mouse", "Mouse", "Move the mouse, click, drag, scroll, and take screenshots."),
            new ToolCategoryOption("keyboard", "Keyboard", "Type text and send key presses."),
            new ToolCategoryOption("system", "System", "Inspect processes, open apps, and launch local system actions."),
            new ToolCategoryOption("email", "Email", "Read, search, send, and reply to email."),
        };

        public static HashSet<string> AllEnabled()
            => new(KnownCategories, StringComparer.OrdinalIgnoreCase);

        public static HashSet<string> Normalize(IEnumerable<string>? categories)
        {
            var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (categories == null)
                return AllEnabled();

            foreach (var category in categories)
            {
                if (KnownCategories.Contains(category, StringComparer.OrdinalIgnoreCase))
                    normalized.Add(category);
            }

            return normalized.Count == 0 ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : normalized;
        }
    }

    /// <summary>
    /// User-facing description of one selectable tool category.
    /// </summary>
    public sealed record ToolCategoryOption(string Id, string Label, string Description);

    /// <summary>
    /// Persisted selection of enabled tool categories.
    /// </summary>
    public sealed record ToolCategorySelection(IReadOnlyList<string> EnabledCategories)
    {
        public bool ToolsEnabled => EnabledCategories.Count > 0;
    }
}
