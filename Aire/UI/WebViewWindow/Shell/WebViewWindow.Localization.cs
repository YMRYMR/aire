using System.Windows;
using Aire.Services;

namespace Aire.UI;

public partial class WebViewWindow
{
    private void OnLanguageChanged()
        => Dispatcher.Invoke(ApplyLocalization);

    private void ApplyLocalization()
    {
        var L = LocalizationService.S;
        FlowDirection = LocalizationService.IsRightToLeftLanguage(LocalizationService.CurrentCode)
            ? System.Windows.FlowDirection.RightToLeft
            : System.Windows.FlowDirection.LeftToRight;

        Title = L("browser.title", "Aire — Browser");
        BackButton.ToolTip = L("browser.back", "Back");
        ForwardButton.ToolTip = L("browser.forward", "Forward");
        ReloadButton.ToolTip = L("browser.reload", "Reload  (F5)");
        NewTabButton.ToolTip = L("browser.newTab", "New tab  (Ctrl+T)");
        CloseAllTabsButton.ToolTip = L("browser.closeAll", "Close all tabs");
        OpenExternalButton.ToolTip = L("browser.openExternal", "Open current page in default browser");
        CloseButton.ToolTip = L("browser.closeBrowser", "Close browser");
    }
}
