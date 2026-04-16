using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Markup;
using Aire.Services;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace Aire.UI
{
    internal static class TextEntryLanguageHelper
    {
        private static readonly IReadOnlyDictionary<string, string> LanguageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ar"] = "ar-SA",
            ["de"] = "de-DE",
            ["en"] = "en-US",
            ["es"] = "es-ES",
            ["fr"] = "fr-FR",
            ["hi"] = "hi-IN",
            ["it"] = "it-IT",
            ["ja"] = "ja-JP",
            ["ko"] = "ko-KR",
            ["pt"] = "pt-PT",
            ["uk"] = "uk-UA",
            ["zh"] = "zh-CN",
        };

        public static void Apply(WpfTextBox textBox)
        {
            if (textBox == null)
                return;

            var code = LocalizationService.CurrentCode;
            var languageTag = LanguageMap.TryGetValue(code, out var mapped) ? mapped : "en-US";
            textBox.Language = XmlLanguage.GetLanguage(languageTag);
            TextProofingManager.AttachOrUpdate(textBox, code);
        }
    }
}
