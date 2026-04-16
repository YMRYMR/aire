using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aire.Services
{
    /// <summary>
    /// A user-defined prompt template with optional placeholder parameters.
    /// </summary>
    public sealed class PromptTemplate
    {
        public string Name { get; set; } = "";
        public string Prefix { get; set; } = "";
        public string? Shortcut { get; set; }

        /// <summary>
        /// Example: "Explain this code: {{code}}" — <c>{{code}}</c> becomes a placeholder the user fills in.
        /// </summary>
        public string? Template { get; set; }

        /// <summary>
        /// Resolves the template by replacing placeholders with provided values.
        /// If no template is defined, returns the prefix directly.
        /// </summary>
        public string Resolve(Dictionary<string, string>? parameters = null)
        {
            if (string.IsNullOrWhiteSpace(Template))
                return Prefix;

            var result = Template;
            if (parameters != null)
            {
                foreach (var (key, value) in parameters)
                    result = result.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);
            }

            return result;
        }
    }

    /// <summary>
    /// Manages CRUD for user-defined prompt templates stored as JSON.
    /// </summary>
    public sealed class PromptTemplateService
    {
        private static readonly string TemplatesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aire", "prompt_templates.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private List<PromptTemplate> _templates = [];

        public IReadOnlyList<PromptTemplate> Templates => _templates;

        public void Load()
        {
            try
            {
                if (!File.Exists(TemplatesPath))
                {
                    _templates = CreateDefaultTemplates();
                    Save();
                    return;
                }
                var json = File.ReadAllText(TemplatesPath);
                _templates = JsonSerializer.Deserialize<List<PromptTemplate>>(json, JsonOptions) ?? [];
            }
            catch
            {
                _templates = [];
            }
        }

        /// <summary>
        /// Creates starter templates for first-run users.
        /// </summary>
        private static List<PromptTemplate> CreateDefaultTemplates() =>
        [
            new()
            {
                Name = "Explain",
                Prefix = "",
                Shortcut = "/explain",
                Template = "Explain the following code step by step:\n\n{{code}}"
            },
            new()
            {
                Name = "Fix bugs",
                Prefix = "",
                Shortcut = "/fix",
                Template = "Find and fix any bugs in this code:\n\n{{code}}"
            },
            new()
            {
                Name = "Code review",
                Prefix = "",
                Shortcut = "/review",
                Template = "Review this code for correctness, style, and potential improvements:\n\n{{code}}"
            },
            new()
            {
                Name = "Summarize",
                Prefix = "",
                Shortcut = "/summarize",
                Template = "Summarize the following text concisely:\n\n{{text}}"
            },
        ];

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(TemplatesPath)!;
                Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(_templates, JsonOptions);
                File.WriteAllText(TemplatesPath, json);
            }
            catch
            {
                // Non-fatal: templates are a convenience feature.
            }
        }

        public void Add(PromptTemplate template)
        {
            _templates.Add(template);
            Save();
        }

        public void Remove(PromptTemplate template)
        {
            _templates.Remove(template);
            Save();
        }

        public void Update(int index, PromptTemplate template)
        {
            if (index >= 0 && index < _templates.Count)
            {
                _templates[index] = template;
                Save();
            }
        }

        /// <summary>
        /// Returns templates that match the given shortcut prefix (e.g. "/" matches all, "/exp" matches "/explain").
        /// </summary>
        public IEnumerable<PromptTemplate> MatchShortcut(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return [];
            return _templates.Where(t =>
                !string.IsNullOrWhiteSpace(t.Shortcut) &&
                t.Shortcut.StartsWith(input, StringComparison.OrdinalIgnoreCase));
        }
    }
}
