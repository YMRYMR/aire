using System;
using System.Drawing;

namespace Aire.Data
{
    /// <summary>
    /// Shared provider palette used to keep colors consistent across the chat list, usage chart, and settings panes.
    /// </summary>
    public static class ProviderColorPalette
    {
        private static readonly string[] Palette =
        {
            "#E6B800",
            "#2FBF71",
            "#3B82F6",
            "#EC4899",
            "#F97316",
            "#8B5CF6",
            "#14B8A6",
            "#EF4444",
            "#06B6D4",
            "#A3A948",
        };

        public static string GetColorForSeed(int seed)
        {
            var index = (int)(Math.Abs((long)seed) % Palette.Length);
            return Palette[index];
        }

        public static string GetColorForText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Palette[0];

            unchecked
            {
                const uint offsetBasis = 2166136261;
                const uint prime = 16777619;
                var hash = offsetBasis;
                foreach (var ch in text.Trim())
                {
                    hash ^= char.ToUpperInvariant(ch);
                    hash *= prime;
                }

                return Palette[(int)(hash % (uint)Palette.Length)];
            }
        }

        public static string Resolve(string? color, int seed, string? fallbackKey = null)
        {
            if (TryParseColor(color, out var parsed) && !IsMutedGray(parsed))
                return color!.Trim();

            if (!string.IsNullOrWhiteSpace(fallbackKey))
                return GetColorForText(fallbackKey);

            return GetColorForSeed(seed);
        }

        public static bool TryParseColor(string? color, out Color parsed)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(color))
                {
                    parsed = ColorTranslator.FromHtml(color.Trim());
                    return true;
                }
            }
            catch
            {
            }

            parsed = default;
            return false;
        }

        public static bool IsMutedGray(Color color)
        {
            var maxChannel = Math.Max(color.R, Math.Max(color.G, color.B));
            var minChannel = Math.Min(color.R, Math.Min(color.G, color.B));
            return maxChannel - minChannel < 18 && maxChannel < 190;
        }
    }
}
