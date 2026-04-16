using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Aire.Services
{
    public record LanguageInfo(string Code, string NativeName, string Flag);

    public record LinkItem(string Label, string Action);

    public record HelpSection(
        string Type,
        string Title,
        string? Tab       = null,
        string? Content   = null,
        string? Intro     = null,
        string[]? Cols    = null,
        string[][]? Rows  = null,
        LinkItem[]? Links = null,
        string? ImagePath = null,
        string? ImageCaption = null);

    /// <summary>
    /// Loads and switches UI languages from JSON files in the app's translations directory.
    /// The directory name can be "Languages" or "Translations" — both are checked.
    /// </summary>
    public static class LocalizationService
    {
        public static event Action? LanguageChanged;

        public static string CurrentCode { get; private set; } = "en";
        public static IReadOnlyList<LanguageInfo> AvailableLanguages { get; private set; } = Array.Empty<LanguageInfo>();
        public static IReadOnlyList<HelpSection> HelpSections { get; private set; } = Array.Empty<HelpSection>();

        private static Dictionary<string, string> _strings = new();
        private static readonly Dictionary<string, (Dictionary<string, string> strings, List<HelpSection> help)>
            _cache = new();

        private static string TranslationsDir
        {
            get
            {
                var base_ = AppContext.BaseDirectory;
                foreach (var name in new[] { "Languages", "Translations" })
                {
                    var dir = Path.Combine(base_, name);
                    if (Directory.Exists(dir))
                        return dir;
                }
                return Path.Combine(base_, "Languages");
            }
        }

        public static void LoadAll()
        {
            _cache.Clear();
            var langs = new List<LanguageInfo>();

            var dir = TranslationsDir;
            if (Directory.Exists(dir))
            {
                foreach (var file in Directory.GetFiles(dir, "*.json"))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        var code = ReadText(root.GetProperty("code"));
                        var name = ReadText(root.GetProperty("nativeName"), fallback: code);
                        var flag = root.TryGetProperty("flag", out var f) ? ReadText(f) : "";

                        var strings = new Dictionary<string, string>();
                        if (root.TryGetProperty("strings", out var strEl))
                            foreach (var prop in strEl.EnumerateObject())
                                strings[prop.Name] = ReadText(prop.Value);

                        var helpSections = new List<HelpSection>();
                        if (root.TryGetProperty("help", out var helpEl))
                        {
                            foreach (var section in helpEl.EnumerateArray())
                            {
                                var type  = section.TryGetProperty("type",  out var typeEl)  ? ReadText(typeEl, "text") : "text";
                                var title = section.TryGetProperty("title", out var titleEl) ? ReadText(titleEl) : "";

                                var tab = section.TryGetProperty("tab", out var tabEl) ? ReadText(tabEl) : null;
                                var imagePath = section.TryGetProperty("imagePath", out var imagePathEl) ? ReadText(imagePathEl) : null;
                                var imageCaption = section.TryGetProperty("imageCaption", out var imageCaptionEl) ? ReadText(imageCaptionEl) : null;

                                if (type == "table")
                                {
                                    var intro = section.TryGetProperty("intro", out var iEl) ? ReadText(iEl) : null;

                                    string[]? cols = null;
                                    if (section.TryGetProperty("cols", out var colsEl))
                                    {
                                        var list = new List<string>();
                                        foreach (var c in colsEl.EnumerateArray())
                                            list.Add(c.GetString() ?? "");
                                        cols = list.ToArray();
                                    }

                                    string[][]? rows = null;
                                    if (section.TryGetProperty("rows", out var rowsEl))
                                    {
                                        var rowList = new List<string[]>();
                                        foreach (var row in rowsEl.EnumerateArray())
                                        {
                                            var cells = new List<string>();
                                            foreach (var cell in row.EnumerateArray())
                                                cells.Add(cell.GetString() ?? "");
                                            rowList.Add(cells.ToArray());
                                        }
                                        rows = rowList.ToArray();
                                    }

                                    helpSections.Add(new HelpSection(type, title, Tab: tab, Intro: intro, Cols: cols, Rows: rows, Links: ParseLinks(section), ImagePath: imagePath, ImageCaption: imageCaption));
                                }
                                else
                                {
                                    var content = section.TryGetProperty("content", out var c) ? ReadText(c) : "";
                                    var intro   = section.TryGetProperty("intro", out var introEl) ? ReadText(introEl) : null;
                                    helpSections.Add(new HelpSection(type, title, Tab: tab, Content: content, Intro: intro, Links: ParseLinks(section), ImagePath: imagePath, ImageCaption: imageCaption));
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(code))
                        {
                            _cache[code] = (strings, helpSections);
                            langs.Add(new LanguageInfo(code, name, flag));
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Warn("LocalizationService.LoadAll", $"Skipping language file '{Path.GetFileName(file)}'", ex);
                    }
                }
            }

            // English first, then alphabetically by native name
            langs.Sort((a, b) =>
                a.Code == "en" ? -1 : b.Code == "en" ? 1
                : string.Compare(a.NativeName, b.NativeName, StringComparison.Ordinal));

            AvailableLanguages = langs;
        }

        private static LinkItem[]? ParseLinks(JsonElement section)
        {
            if (!section.TryGetProperty("links", out var linksEl)) return null;
            var list = new List<LinkItem>();
            foreach (var link in linksEl.EnumerateArray())
            {
                var label  = link.TryGetProperty("label",  out var lEl) ? lEl.GetString()  ?? "" : "";
                var action = link.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(action))
                    list.Add(new LinkItem(label, action));
            }
            return list.Count > 0 ? list.ToArray() : null;
        }

        private static string ReadText(JsonElement element, string fallback = "")
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? fallback,
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.ToString(),
                JsonValueKind.Array => string.Join(
                    "\n",
                    element.EnumerateArray()
                           .Select(item => ReadText(item))
                           .Where(value => !string.IsNullOrWhiteSpace(value))),
                JsonValueKind.Null or JsonValueKind.Undefined => fallback,
                _ => element.ToString(),
            };
        }

        public static void SetLanguage(string code)
        {
            if (!_cache.TryGetValue(code, out var data))
            {
                if (!_cache.TryGetValue("en", out data)) return;
                code = "en";
            }
            CurrentCode  = code;
            _strings     = data.strings;
            HelpSections = ShouldFallbackToEnglishHelp(code, data.help)
                && _cache.TryGetValue("en", out var english)
                    ? english.help
                    : data.help;
            LanguageChanged?.Invoke();
        }

        /// <summary>Returns the translation for <paramref name="key"/>, falling back to <paramref name="fallback"/> or the key itself.</summary>
        public static string S(string key, string? fallback = null) =>
            _strings.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : fallback ?? key;

        public static bool IsRightToLeftLanguage(string? code = null)
            => code is not null && RightToLeftLanguageCodes.Contains(code);

        private static bool ShouldFallbackToEnglishHelp(string code, IReadOnlyList<HelpSection> helpSections)
        {
            if (code == "en")
                return false;

            return helpSections.Any(ContainsReplacementCharacters);
        }

        private static bool ContainsReplacementCharacters(HelpSection section)
        {
            return HasReplacementCharacters(section.Type)
                || HasReplacementCharacters(section.Title)
                || HasReplacementCharacters(section.Tab)
                || HasReplacementCharacters(section.Content)
                || HasReplacementCharacters(section.Intro)
                || HasReplacementCharacters(section.ImagePath)
                || HasReplacementCharacters(section.ImageCaption)
                || HasReplacementCharacters(section.Cols)
                || HasReplacementCharacters(section.Rows)
                || HasReplacementCharacters(section.Links?.Select(link => $"{link.Label} {link.Action}"));
        }

        private static bool HasReplacementCharacters(string? value)
            => !string.IsNullOrEmpty(value) && value.Contains('\uFFFD');

        private static bool HasReplacementCharacters(IEnumerable<string>? values)
            => values?.Any(HasReplacementCharacters) == true;

        private static bool HasReplacementCharacters(IEnumerable<IEnumerable<string>>? values)
            => values?.Any(HasReplacementCharacters) == true;

        private static readonly HashSet<string> RightToLeftLanguageCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "ar",
            "fa",
            "he",
            "iw",
            "ps",
            "ur",
            "yi",
            "dv",
            "ku",
        };
    }
}
