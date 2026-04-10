using System.Linq;
using System.Text.Json;

namespace Aire.Providers;

/// <summary>
/// Canonical tool definitions shared by all providers that support tool calling.
/// Each provider calls the appropriate format adapter to convert these into its
/// API-specific representation.
/// </summary>
public static partial class SharedToolDefinitions
{
    private static readonly Lazy<IReadOnlyList<ToolDescriptor>> AllToolsLazy = new(BuildAllTools);

    /// <summary>
    /// Complete canonical tool list assembled lazily from the category partials.
    /// </summary>
    public static IReadOnlyList<ToolDescriptor> AllTools => AllToolsLazy.Value;

    /// <summary>
    /// Merges the category-specific tool arrays into one ordered catalog.
    /// </summary>
    private static IReadOnlyList<ToolDescriptor> BuildAllTools()
    {
        var categories = new[]
        {
            FileSystemTools ?? Array.Empty<ToolDescriptor>(),
            BrowserTools ?? Array.Empty<ToolDescriptor>(),
            KeyboardTools ?? Array.Empty<ToolDescriptor>(),
            MouseTools ?? Array.Empty<ToolDescriptor>(),
            AgentTools ?? Array.Empty<ToolDescriptor>(),
            SystemTools ?? Array.Empty<ToolDescriptor>(),
            EmailTools ?? Array.Empty<ToolDescriptor>(),
        };

        return categories.SelectMany(static tools => tools).ToArray();
    }

    /// <summary>
    /// Filters the canonical catalog against model capability flags before tools are exposed to a provider.
    /// </summary>
    /// <param name="capabilities">Model capability tags, or <see langword="null"/> to use the default safe subset.</param>
    /// <returns>The tool descriptors that should be exposed.</returns>
    private static IEnumerable<ToolDescriptor> GetFilteredTools(IEnumerable<string>? capabilities, IEnumerable<string>? enabledCategories = null)
    {
        var allowedCategories = enabledCategories != null
            ? new HashSet<string>(enabledCategories, StringComparer.OrdinalIgnoreCase)
            : null;

        if (capabilities == null)
        {
            return AllTools.Where(t =>
                (allowedCategories == null || allowedCategories.Contains(t.Category)) &&
                t.Category != "mouse" &&
                t.Category != "keyboard" &&
                t.Category != "system" &&
                t.Category != "email");
        }

        var caps = new HashSet<string>(capabilities, StringComparer.OrdinalIgnoreCase);
        bool hasTools = caps.Contains("tools") || caps.Contains("toolcalling");
        if (!hasTools) return Array.Empty<ToolDescriptor>();

        return AllTools.Where(t =>
            (allowedCategories == null || allowedCategories.Contains(t.Category)) &&
            (t.Category != "mouse" || caps.Contains("mouse")) &&
            (t.Category != "keyboard" || caps.Contains("keyboard")) &&
            (t.Category != "system" || caps.Contains("system")) &&
            (t.Category != "email" || caps.Contains("email")));
    }

    /// <summary>
    /// Converts the canonical tool catalog into the OpenAI-compatible function schema format.
    /// </summary>
    public static IReadOnlyList<object> ToOpenAiFunctions(IEnumerable<string>? capabilities = null, IEnumerable<string>? enabledCategories = null, bool compact = false)
    {
        return GetFilteredTools(capabilities, enabledCategories).Select(t => (object)new
        {
            name = t.Name,
            description = t.GetDescription(compact),
            parameters = BuildJsonSchema(t),
        }).ToList();
    }

    /// <summary>
    /// Converts the canonical tool catalog into Anthropic's tool schema format.
    /// </summary>
    public static IReadOnlyList<object> ToAnthropicTools(IEnumerable<string>? capabilities = null, IEnumerable<string>? enabledCategories = null, bool compact = false)
    {
        return GetFilteredTools(capabilities, enabledCategories).Select(t => (object)new
        {
            name = t.Name,
            description = t.GetDescription(compact),
            input_schema = BuildJsonSchema(t),
        }).ToList();
    }

    /// <summary>
    /// Converts the canonical tool catalog into Gemini function declarations.
    /// </summary>
    public static IReadOnlyList<object> ToGeminiFunctionDeclarations(IEnumerable<string>? capabilities = null, IEnumerable<string>? enabledCategories = null, bool compact = false)
    {
        return GetFilteredTools(capabilities, enabledCategories).Select(t => (object)new
        {
            name = t.Name,
            description = t.GetDescription(compact),
            parameters = BuildJsonSchema(t),
        }).ToList();
    }

    /// <summary>
    /// Converts the canonical tool catalog into Ollama's native tool schema format.
    /// </summary>
    public static List<object> ToOllamaTools(IEnumerable<string>? capabilities = null, IEnumerable<string>? enabledCategories = null, bool compact = false)
    {
        return GetFilteredTools(capabilities, enabledCategories).Select(t => (object)new
        {
            type = "function",
            function = new
            {
                name = t.Name,
                description = t.GetDescription(compact),
                parameters = BuildJsonSchema(t),
            }
        }).ToList();
    }

    /// <summary>
    /// Builds a JSON-schema-like object for one tool descriptor.
    /// </summary>
    private static object BuildJsonSchema(ToolDescriptor tool)
    {
        var props = new Dictionary<string, object>();
        foreach (var (name, param) in tool.Parameters)
        {
            if (param.Items != null)
                props[name] = new { type = param.Type, items = new { type = param.Items.Type }, description = param.Description };
            else
                props[name] = new { type = param.Type, description = param.Description };
        }

        return new { type = "object", properties = props, required = tool.Required };
    }
}

/// <summary>
/// Provider-agnostic description of one tool available to LLMs.
/// </summary>
public class ToolDescriptor
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    /// <summary>
    /// Concise 1-2 sentence description used when compact tool schemas are requested.
    /// Falls back to <see cref="Description"/> when empty.
    /// </summary>
    public string ShortDescription { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public Dictionary<string, ToolParam> Parameters { get; init; } = new();
    public string[] Required { get; init; } = Array.Empty<string>();

    /// <summary>Returns <see cref="ShortDescription"/> when set; otherwise <see cref="Description"/>.</summary>
    public string GetDescription(bool compact) =>
        compact && !string.IsNullOrEmpty(ShortDescription) ? ShortDescription : Description;
}

/// <summary>
/// Description of one tool parameter in the canonical tool catalog.
/// </summary>
public class ToolParam
{
    public string Type { get; init; }
    public string Description { get; init; }
    public ToolParam? Items { get; init; }

    public ToolParam(string type, string description)
    {
        Type = type;
        Description = description;
    }
}
