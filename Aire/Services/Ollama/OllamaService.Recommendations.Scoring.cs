using System;
using System.Collections.Generic;
using System.Linq;

namespace Aire.Services
{
    public partial class OllamaService
    {
        private static double GetComfortableModelSizeGb(double ramGb)
            => ramGb switch
            {
                <= 0 => 3.5,
                < 8  => 2.0,
                < 16 => 5.5,
                < 24 => 9.0,
                < 32 => 15.0,
                _    => 24.0,
            };

        private static double GetWorkableModelSizeGb(double ramGb)
            => ramGb switch
            {
                <= 0 => 5.0,
                < 8  => 3.5,
                < 16 => 8.0,
                < 24 => 13.0,
                < 32 => 20.0,
                _    => 36.0,
            };

        /// <summary>
        /// Comfortable model size given available VRAM, accounting for ~35% KV cache and compute overhead.
        /// When a discrete GPU is present Ollama targets full-GPU inference; if the model + cache
        /// exceed VRAM it spills to CPU and slows down dramatically.
        /// </summary>
        private static double GetComfortableModelSizeGbForVram(double vramGb)
            => vramGb switch
            {
                <= 0  => double.MaxValue,   // no GPU detected — ignore VRAM constraint
                < 4   => 1.5,
                < 6   => 2.5,
                < 8   => 3.0,
                < 10  => 4.5,
                < 12  => 5.5,
                < 16  => 8.0,
                < 24  => 14.0,
                _     => 22.0,
            };

        private static double GetWorkableModelSizeGbForVram(double vramGb)
            => vramGb switch
            {
                <= 0  => double.MaxValue,
                < 4   => 2.5,
                < 6   => 4.0,
                < 8   => 5.0,
                < 10  => 6.5,
                < 12  => 8.5,
                < 16  => 12.0,
                < 24  => 20.0,
                _     => 36.0,
            };

        private static string[] BuildRecommendationBadges(
            bool recommendedForThisPc,
            bool tooLargeForThisPc,
            bool diskSpaceLikelyInsufficient,
            bool hasTools,
            bool hasVision,
            bool hasCode,
            bool hasThinking)
        {
            var badges = new List<string>();

            if (recommendedForThisPc)
                badges.Add("best fit");
            else if (tooLargeForThisPc)
                badges.Add("too large");
            else if (diskSpaceLikelyInsufficient)
                badges.Add("needs disk");
            else
                badges.Add("may be slow");

            if (hasTools) badges.Add("tools");
            if (hasVision) badges.Add("vision");
            if (hasCode) badges.Add("coding");
            if (hasThinking) badges.Add("reasoning");

            return badges.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static string BuildRecommendationReason(
            string modelName,
            bool recommendedForThisPc,
            bool tooLargeForThisPc,
            bool diskSpaceLikelyInsufficient,
            bool aireFriendly,
            bool hasVision,
            bool hasCode,
            bool hasThinking,
            double sizeGb,
            double ramGb,
            double freeDiskGb)
        {
            if (recommendedForThisPc)
            {
                if (hasVision)
                    return $"{modelName} is a strong match for Aire on this PC and also supports image understanding.";
                if (hasCode)
                    return $"{modelName} is a strong match for Aire on this PC and is especially good for coding tasks.";
                if (hasThinking)
                    return $"{modelName} is a strong match for Aire on this PC and should handle deeper reasoning better than lighter models.";
                return $"{modelName} is a strong match for Aire on this PC and should run comfortably.";
            }

            if (tooLargeForThisPc)
                return $"{modelName} is likely too heavy for this PC. It may feel slow or fail to load with {ramGb:0.#} GB RAM.";

            if (diskSpaceLikelyInsufficient)
                return $"{modelName} needs a large download and your free disk space ({freeDiskGb:0.#} GB) looks tight for it.";

            if (!aireFriendly)
                return $"{modelName} is more specialized than a typical chat model, so it is not the best first choice for Aire.";

            if (sizeGb > 0)
                return $"{modelName} may still work on this PC, but its download size ({sizeGb:0.#} GB) suggests a slower experience.";

            return $"{modelName} may work, but Aire cannot confidently rate how well it will run on this PC.";
        }
    }
}
