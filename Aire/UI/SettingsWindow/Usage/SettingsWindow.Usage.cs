using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private UsageDashboardCoordinator? _usageDashboardCoordinator;
        private UsageDashboardCoordinator UsageCoordinator => _usageDashboardCoordinator ??= new(this);

        // ── Forwarding wrappers called from other partial files ──

        internal Task LoadUsageDashboardAsync() => UsageCoordinator.LoadUsageDashboardAsync();

        internal void UpdateUsageHeaderLocalization() => UsageCoordinator.UpdateUsageHeaderLocalization();

        // ── Event handlers (XAML-bound) ──

        private async void UsageRefreshButton_Click(object sender, RoutedEventArgs e)
            => await UsageCoordinator.LoadUsageDashboardAsync();

        private void UsageCurrencyButton_Click(object sender, RoutedEventArgs e)
            => UsageCoordinator.ShowCurrencyMenu(UsageCurrencyButton);

        private void UsageTrendChartHost_SizeChanged(object sender, SizeChangedEventArgs e)
            => UsageCoordinator.RenderUsageTrendChart();
    }
}
