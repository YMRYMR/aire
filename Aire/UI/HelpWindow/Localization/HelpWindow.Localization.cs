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
            CloseButton.ToolTip = LocalizationService.S("tooltip.close", "Close");
            SearchPlaceholder.Text = LocalizationService.S("help.search", "Search help\u2026");
            FlowDirection = LocalizationService.IsRightToLeftLanguage(LocalizationService.CurrentCode)
                ? System.Windows.FlowDirection.RightToLeft
                : System.Windows.FlowDirection.LeftToRight;
            _allSections = LocalizationService.HelpSections.ToList();
            SearchBox.Text = "";
            RebuildTabs();
        }
    }
}

