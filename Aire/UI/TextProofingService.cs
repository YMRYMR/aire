using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using SpellCheck.Dictionaries;

namespace Aire.UI
{
    internal static class TextProofingService
    {
        private static readonly SpellCheckFactory Factory = new();

        private static readonly IReadOnlyDictionary<string, string> LanguageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ar"] = "en-US",
            ["de"] = "de-DE",
            ["en"] = "en-US",
            ["es"] = "es-ES",
            ["fr"] = "fr-FR",
            ["hi"] = "en-US",
            ["it"] = "it-IT",
            ["ja"] = "en-US",
            ["ko"] = "en-US",
            ["pt"] = "pt-PT",
            ["uk"] = "en-US",
            ["zh"] = "en-US",
        };

        private static readonly Dictionary<string, Task<SpellCheck.SpellChecker>> CheckerCache = new(StringComparer.OrdinalIgnoreCase);

        public static string ResolveCultureCode(string uiLanguageCode)
            => LanguageMap.TryGetValue(uiLanguageCode, out var culture) ? culture : "en-US";

        public static SpellCheck.SpellChecker GetChecker(string uiLanguageCode)
            => GetCheckerAsync(uiLanguageCode).GetAwaiter().GetResult();

        public static Task<SpellCheck.SpellChecker> GetCheckerAsync(string uiLanguageCode)
        {
            var cultureCode = ResolveCultureCode(uiLanguageCode);
            lock (CheckerCache)
            {
                if (!CheckerCache.TryGetValue(cultureCode, out var task))
                {
                    task = Factory.CreateSpellChecker(CultureInfo.GetCultureInfo(cultureCode));
                    CheckerCache[cultureCode] = task;
                }
                return task;
            }
        }
    }
}
