using System.Linq;
using Aire.Services;

namespace Aire.UI
{
    public partial class HelpWindow
    {
        private void OnThemeChanged() =>
            Dispatcher.Invoke(() => FontSize = AppearanceService.FontSize);

        private void OnLanguageChanged() =>
            Dispatcher.Invoke(ApplyLocalization);

        private void ApplyLocalization()
        {
            TitleText.Text = LocalizationService.S("help.title", "Help — Aire");
            SearchPlaceholder.Text = LocalizationService.S("help.search", "Search help\u2026");
            _allSections = LocalizationService.HelpSections.ToList();
            SearchBox.Text = "";
            RebuildTabs();
        }
    }
}

