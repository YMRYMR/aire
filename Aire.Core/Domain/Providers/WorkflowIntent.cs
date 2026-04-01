using System.Text.Json;

namespace Aire.Domain.Providers
{
    /// <summary>
    /// High-level intent produced by interpreting a provider response.
    /// These are product-level semantics owned by Aire, not by individual providers.
    /// Every provider adapter should map its raw response into one of these intents.
    /// </summary>
    public sealed class WorkflowIntent
    {
        /// <summary>
        /// The kind of workflow action the provider response represents.
        /// </summary>
        public WorkflowIntentKind Kind { get; init; }

        /// <summary>
        /// Visible assistant text that should be displayed to the user, regardless of the intent kind.
        /// May be empty for pure tool-call responses.
        /// </summary>
        public string DisplayText { get; init; } = string.Empty;

        /// <summary>
        /// When <see cref="Kind"/> is <see cref="WorkflowIntentKind.ExecuteTool"/>,
        /// contains the tool that the provider requested.
        /// </summary>
        public ToolIntent? Tool { get; init; }

        /// <summary>
        /// Optional provider-independent workflow parameters for non-tool intents,
        /// such as switch-model arguments, follow-up-question text, or completion payloads.
        /// </summary>
        public JsonElement? Parameters { get; init; }

        /// <summary>
        /// Raw workflow payload as received from the provider before semantic normalization.
        /// Useful for diagnostics; not intended for control flow.
        /// </summary>
        public string? RawPayload { get; init; }

        /// <summary>
        /// When <see cref="Kind"/> is <see cref="WorkflowIntentKind.Error"/>,
        /// contains a human-readable error description.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>Convenience factory for a plain assistant text response.</summary>
        public static WorkflowIntent AssistantText(string text) => new()
        {
            Kind = WorkflowIntentKind.AssistantText,
            DisplayText = text
        };

        /// <summary>Convenience factory for a tool-call request.</summary>
        public static WorkflowIntent ToolCall(string displayText, ToolIntent tool) => new()
        {
            Kind = WorkflowIntentKind.ExecuteTool,
            DisplayText = displayText,
            Tool = tool
        };

        /// <summary>Convenience factory for a model-switch request.</summary>
        public static WorkflowIntent SwitchModel(string displayText, string parametersJson, string? rawPayload = null) => new()
        {
            Kind = WorkflowIntentKind.SwitchModel,
            DisplayText = displayText,
            Parameters = ParseParameters(parametersJson),
            RawPayload = rawPayload
        };

        /// <summary>Convenience factory for a follow-up question.</summary>
        public static WorkflowIntent FollowUpQuestion(string displayText, string parametersJson, string? rawPayload = null) => new()
        {
            Kind = WorkflowIntentKind.FollowUpQuestion,
            DisplayText = displayText,
            Parameters = ParseParameters(parametersJson),
            RawPayload = rawPayload
        };

        /// <summary>Convenience factory for a completion attempt.</summary>
        public static WorkflowIntent AttemptCompletion(string displayText, string parametersJson, string? rawPayload = null) => new()
        {
            Kind = WorkflowIntentKind.AttemptCompletion,
            DisplayText = displayText,
            Parameters = ParseParameters(parametersJson),
            RawPayload = rawPayload
        };

        /// <summary>Convenience factory for a todo-list update.</summary>
        public static WorkflowIntent UpdateTodoList(string displayText, string parametersJson, string? rawPayload = null) => new()
        {
            Kind = WorkflowIntentKind.UpdateTodoList,
            DisplayText = displayText,
            Parameters = ParseParameters(parametersJson),
            RawPayload = rawPayload
        };

        /// <summary>Convenience factory for an error outcome.</summary>
        public static WorkflowIntent Error(string errorMessage) => new()
        {
            Kind = WorkflowIntentKind.Error,
            ErrorMessage = errorMessage
        };

        /// <summary>Convenience factory for a canceled turn.</summary>
        public static WorkflowIntent Canceled() => new()
        {
            Kind = WorkflowIntentKind.Canceled
        };

        private static JsonElement ParseParameters(string parametersJson)
        {
            using JsonDocument document = JsonDocument.Parse(parametersJson);
            return document.RootElement.Clone();
        }
    }

    /// <summary>
    /// Discriminator for <see cref="WorkflowIntent"/>.
    /// Mirrors the existing <c>OutcomeKind</c> branches in <c>ChatTurnWorkflowService</c>.
    /// </summary>
    public enum WorkflowIntentKind
    {
        /// <summary>Normal assistant text response with no tool or workflow action.</summary>
        AssistantText,
        /// <summary>Provider requested execution of an Aire tool.</summary>
        ExecuteTool,
        /// <summary>Provider requested switching to a different model.</summary>
        SwitchModel,
        /// <summary>Provider requested updating the user's todo list.</summary>
        UpdateTodoList,
        /// <summary>Provider asked the user a follow-up question.</summary>
        FollowUpQuestion,
        /// <summary>Provider signaled task completion.</summary>
        AttemptCompletion,
        /// <summary>The turn failed due to a provider or transport error.</summary>
        Error,
        /// <summary>The turn was canceled by the user or system.</summary>
        Canceled
    }
}
