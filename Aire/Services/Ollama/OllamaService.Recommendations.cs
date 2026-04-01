using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Aire.Services
{
    public partial class OllamaService
    {
        /// <summary>
        /// Hardware snapshot used to rank local-model recommendations for the current machine.
        /// </summary>
        public record OllamaSystemProfile(
            double TotalRamGb,
            double FreeDiskGb,
            double VideoRamGb,
            string PrimaryGpuName,
            string PerformanceTier,
            string Summary);

        /// <summary>
        /// Recommendation outcome for one Ollama model after combining machine limits and Aire-specific preferences.
        /// </summary>
        public record OllamaModelRecommendation(
            bool RecommendedForThisPc,
            bool AireFriendly,
            bool DiskSpaceLikelyInsufficient,
            bool TooLargeForThisPc,
            string SummaryLabel,
            string Reason,
            string[] Badges);

        /// <summary>
        /// Reads the current machine profile used for local-model recommendations.
        /// </summary>
        /// <returns>RAM, disk, GPU, and summary information for the current machine.</returns>
        public static OllamaSystemProfile GetLocalSystemProfile()
        {
            var ramGb = GetInstalledRamGb();
            var freeDiskGb = GetSystemDriveFreeSpaceGb();
            var (videoRamGb, gpuName) = GetInstalledVideoRamGb();
            var tier = GetPerformanceTier(ramGb);
            var summary = BuildSystemSummary(ramGb, freeDiskGb, videoRamGb, gpuName);
            return new OllamaSystemProfile(ramGb, freeDiskGb, videoRamGb, gpuName, tier, summary);
        }

        /// <summary>
        /// Scores one Ollama model against the current machine and Aire's preferred capabilities.
        /// </summary>
        /// <param name="modelName">Ollama model name or tag.</param>
        /// <param name="sizeBytes">Known model size in bytes, when already available from Ollama metadata.</param>
        /// <param name="profile">Optional precomputed machine profile to reuse across many recommendations.</param>
        /// <returns>A recommendation result including fit, reason, and UI badges.</returns>
        public static OllamaModelRecommendation GetModelRecommendation(
            string modelName,
            long sizeBytes = 0,
            OllamaSystemProfile? profile = null)
        {
            profile ??= GetLocalSystemProfile();

            KnownModelMeta.TryGetValue(modelName, out var meta);
            var effectiveSizeBytes = sizeBytes > 0 ? sizeBytes : meta?.SizeBytes ?? 0;
            var sizeGb = effectiveSizeBytes > 0 ? effectiveSizeBytes / 1_073_741_824.0 : 0;
            var tags = meta?.Tags ?? [];

            var isEmbedding = tags.Contains("embedding", StringComparer.OrdinalIgnoreCase);
            var hasTools = tags.Contains("tools", StringComparer.OrdinalIgnoreCase);
            var hasVision = tags.Contains("vision", StringComparer.OrdinalIgnoreCase);
            var hasCode = tags.Contains("code", StringComparer.OrdinalIgnoreCase);
            var hasThinking = tags.Contains("thinking", StringComparer.OrdinalIgnoreCase);

            // Use the more restrictive of RAM-based and VRAM-based limits.
            // When a discrete GPU is present, Ollama targets full-GPU inference; a model that
            // exceeds VRAM comfortable headroom will spill to CPU and feel sluggish even on
            // machines with plenty of RAM.
            var comfortableGb = Math.Min(
                GetComfortableModelSizeGb(profile.TotalRamGb),
                GetComfortableModelSizeGbForVram(profile.VideoRamGb));
            var workableGb = Math.Min(
                GetWorkableModelSizeGb(profile.TotalRamGb),
                GetWorkableModelSizeGbForVram(profile.VideoRamGb));
            var tooLargeForThisPc = sizeGb > 0 && sizeGb > workableGb;
            var diskSpaceLikelyInsufficient = sizeGb > 0 && profile.FreeDiskGb > 0 && profile.FreeDiskGb < sizeGb + 4.0;

            var aireFriendly = !isEmbedding && (hasTools || hasVision || hasCode || !string.IsNullOrEmpty(modelName));

            var score = 0;
            if (meta?.Recommended == true) score += 2;
            if (hasTools) score += 4;
            if (hasVision) score += 2;
            if (hasCode) score += 2;
            if (hasThinking) score += 1;
            if (isEmbedding) score -= 8;

            if (sizeGb <= 0)
                score += 1;
            else if (sizeGb <= comfortableGb)
                score += 4;
            else if (sizeGb <= workableGb)
                score += 1;
            else
                score -= 5;

            if (diskSpaceLikelyInsufficient)
                score -= 4;

            var recommendedForThisPc =
                aireFriendly &&
                !tooLargeForThisPc &&
                !diskSpaceLikelyInsufficient &&
                score >= 5;

            var badges = BuildRecommendationBadges(
                recommendedForThisPc,
                tooLargeForThisPc,
                diskSpaceLikelyInsufficient,
                hasTools,
                hasVision,
                hasCode,
                hasThinking);

            var summaryLabel = recommendedForThisPc
                ? "best fit"
                : tooLargeForThisPc
                    ? "too large"
                    : diskSpaceLikelyInsufficient
                        ? "needs more disk"
                        : sizeGb > comfortableGb && sizeGb > 0
                            ? "may be slow"
                            : aireFriendly
                                ? "good fit"
                                : "specialized";

            var reason = BuildRecommendationReason(
                modelName,
                recommendedForThisPc,
                tooLargeForThisPc,
                diskSpaceLikelyInsufficient,
                aireFriendly,
                hasVision,
                hasCode,
                hasThinking,
                sizeGb,
                profile.TotalRamGb,
                profile.FreeDiskGb);

            return new OllamaModelRecommendation(
                recommendedForThisPc,
                aireFriendly,
                diskSpaceLikelyInsufficient,
                tooLargeForThisPc,
                summaryLabel,
                reason,
                badges);
        }

        /// <summary>
        /// Formats the machine profile into the compact status line shown in onboarding and settings.
        /// </summary>
        /// <param name="profile">Machine profile to summarize.</param>
        /// <returns>A short human-readable hardware summary.</returns>
        public static string FormatHardwareSummary(OllamaSystemProfile profile)
        {
            var parts = new List<string>();

            if (profile.TotalRamGb > 0)
                parts.Add($"{profile.TotalRamGb:0.#} GB RAM");

            if (profile.VideoRamGb > 0)
            {
                var vramLabel = $"{profile.VideoRamGb:0.#} GB VRAM";
                if (!string.IsNullOrWhiteSpace(profile.PrimaryGpuName))
                    vramLabel += $" ({profile.PrimaryGpuName})";

                parts.Add(vramLabel);
            }

            if (profile.FreeDiskGb > 0)
                parts.Add($"{profile.FreeDiskGb:0.#} GB free disk");

            return parts.Count > 0
                ? string.Join("  ·  ", parts)
                : "Hardware information unavailable";
        }
    }
}
