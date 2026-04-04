using System.Windows;
using Aire.Bootstrap;
using Aire.Services;

namespace Aire.Setup;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppearanceService.ApplySaved();
        LocalizationService.LoadAll();
        LocalizationService.SetLanguage(SetupPreferencesStore.Load().LanguageCode);
        base.OnStartup(e);
    }
}
