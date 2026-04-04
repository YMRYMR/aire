using System;
using System.Windows;
using System.Windows.Input;
using Aire.Services;

namespace Aire.UI
{
    public partial class SessionPanicButton : Window
    {
        public event Action? StopRequested;

        public SessionPanicButton()
        {
            InitializeComponent();
            ApplyLocalization();
            LocalizationService.LanguageChanged += OnLanguageChanged;

            // Default position: top-right corner of the work area, clear of the taskbar.
            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 12;
            Top  = area.Top  + 12;
        }

        private void OnLanguageChanged()
            => Dispatcher.Invoke(ApplyLocalization);

        private void ApplyLocalization()
        {
            Title = LocalizationService.S("panic.title", "Aire Stop");
            DragBorder.ToolTip = LocalizationService.S("panic.tooltip", "Click to stop the active keyboard/mouse session immediately.\nDrag to reposition.");
            StopButton.Content = LocalizationService.S("panic.stopButton", "⏹ STOP SESSION");
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopRequested?.Invoke();
        }

        private void DragBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        protected override void OnClosed(EventArgs e)
        {
            LocalizationService.LanguageChanged -= OnLanguageChanged;
            base.OnClosed(e);
        }
    }
}
