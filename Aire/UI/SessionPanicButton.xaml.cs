using System;
using System.Windows;
using System.Windows.Input;

namespace Aire.UI
{
    public partial class SessionPanicButton : Window
    {
        public event Action? StopRequested;

        public SessionPanicButton()
        {
            InitializeComponent();

            // Default position: top-right corner of the work area, clear of the taskbar.
            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 12;
            Top  = area.Top  + 12;
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
    }
}
