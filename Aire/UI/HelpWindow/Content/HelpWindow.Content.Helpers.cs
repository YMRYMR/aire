using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Aire.Services;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using Brush = System.Windows.Media.Brush;

namespace Aire.UI
{
    public partial class HelpWindow
    {
        internal static bool SectionMatchesQuery(HelpSection section, string lq)
        {
            if (section.Title.Contains(lq, StringComparison.OrdinalIgnoreCase)) return true;
            if (section.Content?.Contains(lq, StringComparison.OrdinalIgnoreCase) == true) return true;
            if (section.Intro?.Contains(lq, StringComparison.OrdinalIgnoreCase) == true) return true;
            if (section.ImageCaption?.Contains(lq, StringComparison.OrdinalIgnoreCase) == true) return true;
            if (section.Rows != null)
            {
                foreach (var row in section.Rows)
                {
                    foreach (var cell in row)
                    {
                        if (cell.Contains(lq, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                }
            }
            return false;
        }

        private static string? ResolveHelpImagePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;
            if (Path.IsPathRooted(relativePath)) return relativePath;

            // Try language-specific subdirectory first
            var langCode = LocalizationService.CurrentCode;
            if (!string.IsNullOrEmpty(langCode) && langCode != "en")
            {
                var langSpecificPath = Path.Combine(
                    Path.GetDirectoryName(relativePath) ?? "",
                    langCode,
                    Path.GetFileName(relativePath));
                var fullLangPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, langSpecificPath));
                if (File.Exists(fullLangPath))
                {
                    Debug.WriteLine($"[HelpWindow] Using language-specific image: {fullLangPath}");
                    return fullLangPath;
                }
            }

            // Fall back to default location
            var defaultPath = Path.Combine(AppContext.BaseDirectory, relativePath);
            return Path.GetFullPath(defaultPath);
        }

        private static Brush GetBrush(string key)
        {
            var res = Application.Current.Resources[key];
            return res is Brush b ? b : Brushes.Gray;
        }

        private static string LocalizeTabName(string tabName)
        {
            return tabName switch
            {
                "Getting Started" => LocalizationService.S("help.tab.gettingStarted", "Getting Started"),
                "Tools & Voice" => LocalizationService.S("help.tab.toolsVoice", "Tools & Voice"),
                "Context & Templates" => LocalizationService.S("help.tab.contextTemplates", "Context & Templates"),
                "Providers" => LocalizationService.S("help.tab.providers", "Providers"),
                "Local API" => LocalizationService.S("help.tab.localApi", "Local API"),
                _ => tabName
            };
        }
    }
}
