using System.Text.Json;

namespace Aire.Domain.Providers
{
    /// <summary>
    /// Provider-independent representation of a tool call requested by an AI model.
    /// Adapters decode provider-specific tool-call formats (OpenAI function calling,
    /// Hermes XML tags, ReAct JSON, Aire text format, Codex CLI, etc.) into this
    /// shared shape so that the rest of the app never needs to understand transport details.
    /// </summary>
    public sealed class ToolIntent
    {
        /// <summary>
        /// Canonical Aire tool name (e.g. "read_file", "execute_command", "switch_model").
        /// </summary>
        public string ToolName { get; init; } = string.Empty;

        /// <summary>
        /// Parsed tool parameters as a <see cref="JsonElement"/>.
        /// Use <c>.GetProperty()</c> and <c>.GetString()</c> / <c>.GetInt32()</c> to read values.
        /// </summary>
        public JsonElement Parameters { get; init; }

        /// <summary>
        /// Human-readable one-line description of what the tool call does,
        /// suitable for showing in an approval prompt.
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// The raw tool-call payload as received from the provider before normalization.
        /// Useful for diagnostics and debugging; should not be used for control flow.
        /// </summary>
        public string? RawPayload { get; init; }

        /// <summary>
        /// Creates a <see cref="ToolIntent"/> with a simple string or object parameter set.
        /// </summary>
        /// <param name="toolName">Canonical tool name.</param>
        /// <param name="parametersJson">JSON object containing tool parameters.</param>
        /// <param name="description">Human-readable description for approval UI.</param>
        /// <param name="rawPayload">Optional raw payload from the provider.</param>
        public static ToolIntent Create(
            string toolName,
            string parametersJson,
            string description,
            string? rawPayload = null)
        {
            using var doc = JsonDocument.Parse(parametersJson);
            return new ToolIntent
            {
                ToolName = toolName,
                Parameters = doc.RootElement.Clone(),
                Description = description,
                RawPayload = rawPayload
            };
        }
    }
}
