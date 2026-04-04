using System.Collections.Generic;
using System.Linq;
using Aire.Services;

namespace Aire.AppLayer.Chat
{
    /// <summary>
    /// Provides the available assistant operating modes and the provider-facing guidance for each.
    /// </summary>
    public sealed class AssistantModeApplicationService
    {
        public sealed record AssistantModeDefinition(
            string Key,
            string DisplayName,
            string Description,
            string PromptInstruction);

        public IReadOnlyList<AssistantModeDefinition> GetModes()
            => BaseModes
                .Select(Localize)
                .OrderBy(mode => mode.Key == "general" ? -1 : 0)
                .ThenBy(mode => mode.DisplayName, System.StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

        public AssistantModeDefinition GetDefaultMode()
            => ResolveMode("general");

        public AssistantModeDefinition ResolveMode(string? key)
            => GetModes().FirstOrDefault(mode => string.Equals(mode.Key, key?.Trim(), System.StringComparison.OrdinalIgnoreCase))
                ?? GetDefaultMode();

        public string BuildPromptSection(string key)
        {
            var mode = ResolveMode(key);

            return $"\n\nCURRENT OPERATING MODE: {mode.DisplayName}\n{mode.PromptInstruction}";
        }

        private static AssistantModeDefinition Localize(AssistantModeDefinition mode) =>
            mode with
            {
                DisplayName = LocalizationService.S($"assistantMode.{mode.Key}.name", mode.DisplayName),
                Description = LocalizationService.S($"assistantMode.{mode.Key}.description", mode.Description),
            };

        private static readonly IReadOnlyList<AssistantModeDefinition> BaseModes =
        [
            new("architect", "Architect", "Systems and design tradeoff mode", "Prioritize structure, interfaces, boundaries, tradeoffs, maintainability, and long-term design consequences. Make assumptions explicit."),
            new("creative-writer", "Creative writer", "Idea-rich expressive writing mode", "Prioritize originality, tone, rhythm, imagery, and stylistic variation. When writing, optimize for voice and vividness rather than dry completeness."),
            new("developer", "Developer", "Implementation-first technical mode", "Prioritize technical accuracy, concrete implementation detail, debugging, verification, and actionable engineering tradeoffs. Prefer concise direct answers over motivational language."),
            new("general", "General", "Balanced default behavior", "Be balanced and pragmatic. Adapt depth and tone to the user's request without forcing a specialty style."),
            new("painter", "Painter", "Picture-making and visual concept mode", "Prioritize visual composition, concrete scene description, stylistic coherence, color, lighting, framing, and image-ready prompts. When image generation is available, strongly prefer producing a picture over a purely textual answer."),
            new("philosopher", "Philosopher", "Conceptual reasoning and worldview mode", "Prioritize conceptual clarity, first principles, competing interpretations, and deeper reasoning about values, meaning, and assumptions."),
            new("psicologist", "Psychologist", "Empathetic reflective mode", "Prioritize empathy, reflective listening, emotional context, and careful non-judgmental framing. Avoid overclaiming clinical certainty."),
            new("scientist", "Scientist", "Evidence-driven analytical mode", "Prioritize evidence, hypotheses, falsifiability, careful assumptions, and clear separation between observation and inference. Prefer precision over rhetoric."),
            new("security", "Security", "Security-focused review and threat analysis mode", "Prioritize security posture, attack surfaces, trust boundaries, abuse paths, data exposure, and safe defaults. Prefer explicit risk analysis and concrete mitigations."),
            new("teacher", "Teacher", "Explanatory learning-focused mode", "Prioritize clarity, stepwise explanation, examples, and concept building. Explain why, not only what.")
        ];
    }
}
