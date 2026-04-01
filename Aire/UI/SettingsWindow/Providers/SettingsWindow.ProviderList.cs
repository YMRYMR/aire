using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Aire.AppLayer.Providers;
using Aire.Data;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private SettingsProviderListApplicationService? _providerListService;
        private SettingsProviderListApplicationService ProviderListWorkflow => _providerListService ??= new();

        private async Task RefreshProvidersList(int? reSelectId = null)
        {
            var state = await ProviderListWorkflow.LoadAsync(
                _databaseService,
                reSelectId,
                (ProvidersListView.SelectedItem as Provider)?.Id);

            _providers = state.Providers.ToList();
            _isRefreshing = true;
            try
            {
                ProvidersListView.ItemsSource = null;
                ProvidersListView.ItemsSource = _providers;
                ProvidersListView.SelectedItem = state.SelectedProvider;

                if (ProvidersListView.SelectedItem == null)
                {
                    ClearForm();
                }
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private void DragHandle_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(ProvidersListView);
            _draggedProvider = (sender as FrameworkElement)?.DataContext as Provider;
            e.Handled = false;
        }

        private void ProvidersListView_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed || _draggedProvider == null)
            {
                return;
            }

            var pos = e.GetPosition(ProvidersListView);
            var diff = pos - _dragStartPoint;
            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            DragDrop.DoDragDrop(ProvidersListView, _draggedProvider, System.Windows.DragDropEffects.Move);
            _draggedProvider = null;
        }

        private void ProvidersListView_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(typeof(Provider))
                ? System.Windows.DragDropEffects.Move
                : System.Windows.DragDropEffects.None;
            e.Handled = true;
        }

        private async void ProvidersListView_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (_draggedProvider == null || !e.Data.GetDataPresent(typeof(Provider)))
            {
                return;
            }

            var target = GetProviderAtPoint(e.GetPosition(ProvidersListView));
            if (target == null || target == _draggedProvider)
            {
                _draggedProvider = null;
                return;
            }
            var reorder = ProviderListWorkflow.Reorder(_providers, _draggedProvider.Id, target.Id);
            var reselect = reorder.SelectedProvider;
            _draggedProvider = null;
            if (!reorder.OrderChanged)
            {
                return;
            }

            _isRefreshing = true;
            try
            {
                _providers = reorder.Providers.ToList();
                ProvidersListView.ItemsSource = null;
                ProvidersListView.ItemsSource = _providers;
                ProvidersListView.SelectedItem = reselect;
            }
            finally
            {
                _isRefreshing = false;
            }

            await ProviderListWorkflow.SaveOrderAsync(_databaseService, _providers);
            ProvidersChanged?.Invoke();
        }

        private Provider? GetProviderAtPoint(System.Windows.Point point)
        {
            var hit = ProvidersListView.InputHitTest(point) as DependencyObject;
            while (hit != null)
            {
                if (hit is FrameworkElement fe && fe.DataContext is Provider p)
                {
                    return p;
                }

                hit = System.Windows.Media.VisualTreeHelper.GetParent(hit);
            }

            return null;
        }
    }
}
