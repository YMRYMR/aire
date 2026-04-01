using System;
using System.Windows;
using Aire.Services;

namespace Aire.UI
{
    public partial class OnboardingWindow
    {
        private void ApplyThemeFontSize()
            => Dispatcher.Invoke(() => FontSize = AppearanceService.FontSize);

        internal void StartChatting_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetHasCompletedOnboarding(true);
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => DragMove();

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => Close();

        protected override void OnClosed(EventArgs e)
        {
            AppearanceService.AppearanceChanged -= ApplyThemeFontSize;
            _testCts?.Cancel();
            _modelFetchCts?.Cancel();
            CancelOllamaOps();
            base.OnClosed(e);
        }
    }
}

