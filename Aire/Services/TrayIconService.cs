using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Drawing;

namespace Aire.Services
{
    public class TrayIconService : IDisposable
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT rect, int size);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

        private readonly NotifyIcon _trayIcon;
        private readonly Window _mainWindow;
        private bool _disposed;
        private bool _isAttachedToTray = true;
        public bool IsAttachedToTray
        {
            get => _isAttachedToTray;
            set
            {
                if (_isAttachedToTray == value) return;
                _isAttachedToTray = value;
                AttachedToTrayChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? AttachedToTrayChanged;
        public event EventHandler? OpenChatRequested;
        public event EventHandler? SettingsRequested;
        public event EventHandler? ExitRequested;

        private const double DefaultWidth  = 460;
        private const double DefaultHeight = 620;

        public TrayIconService(Window mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Open Aire",           null, (s, e) => OpenChatRequested?.Invoke(this, EventArgs.Empty));
            contextMenu.Items.Add("Settings",            null, (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty));
            contextMenu.Items.Add("Move to task bar", null, (s, e) => MoveToTaskBar());
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Exit",                null, (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty));

            // Try to load the app icon (aire.ico)
            Icon? appIcon = null;
            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aire.ico");
                if (File.Exists(iconPath))
                {
                    appIcon = new Icon(iconPath);
                }
            }
            catch
            {
                // Fall back to system icon if custom icon fails to load
            }

            _trayIcon = new NotifyIcon
            {
                Icon = appIcon ?? SystemIcons.Application,
                Text = "Aire",
                ContextMenuStrip = contextMenu,
                Visible = true
            };

            _trayIcon.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ToggleMainWindow(); };
            _trayIcon.DoubleClick += (s, e) => OpenChatRequested?.Invoke(this, EventArgs.Empty);
            _trayIcon.BalloonTipClicked += (s, e) => ShowMainWindow();

            _mainWindow.SizeChanged += (s, e) => { if (_mainWindow.IsVisible) SnapToEdge(); };
        }

        public void ShowMainWindow()
        {
            if (IsAttachedToTray)
                PositionAboveTray();

            if (!_mainWindow.IsVisible)
                _mainWindow.Show();
            _mainWindow.Activate();
            _mainWindow.WindowState = WindowState.Normal;
        }

        public void HideMainWindow() => _mainWindow.Hide();

        private void MoveToTaskBar()
        {
            IsAttachedToTray = true;
            _mainWindow.Width  = DefaultWidth;
            _mainWindow.Height = DefaultHeight;
            PositionAboveTray();
        }

        public void ToggleMainWindow()
        {
            if (_mainWindow.IsVisible)
            {
                HideMainWindow();
            }
            else
            {
                ShowMainWindow();
            }
        }

        private void PositionAboveTray()
        {
            var workArea = SystemParameters.WorkArea;

            // Clamp size to work area so resize handles are always reachable
            if (_mainWindow.Width  > workArea.Width)  _mainWindow.Width  = workArea.Width;
            if (_mainWindow.Height > workArea.Height) _mainWindow.Height = workArea.Height;

            SnapToEdge();
        }

        public void DetachFromTray()
        {
            IsAttachedToTray = false;
        }

        /// Returns the invisible frame thickness (in WPF logical pixels) by comparing
        /// GetWindowRect (full bounds) with DwmGetWindowAttribute (visible bounds).
        private (double right, double bottom) GetInvisibleFrame()
        {
            var hwnd = new WindowInteropHelper(_mainWindow).Handle;
            if (hwnd == IntPtr.Zero) return (0, 0);

            if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out var visible, Marshal.SizeOf<RECT>()) != 0)
                return (0, 0);

            GetWindowRect(hwnd, out var full);

            // Values from Win32 are in physical pixels; convert to WPF logical pixels
            var source = PresentationSource.FromVisual(_mainWindow);
            double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

            double invisRight  = (full.Right  - visible.Right)  / dpiX;
            double invisBottom = (full.Bottom - visible.Bottom) / dpiY;
            return (invisRight, invisBottom);
        }

        /// Repositions the window so its bottom-right corner stays flush with the
        /// work area edge (accounting for the invisible WPF resize-grip frame).
        /// Called both on ShowMainWindow and on every SizeChanged while visible.
        internal void SnapToEdge()
        {
            if (!IsAttachedToTray) return;

            var workArea = SystemParameters.WorkArea;
            var (frameRight, frameBottom) = GetInvisibleFrame();

            _mainWindow.Left = workArea.Right  - _mainWindow.Width  + frameRight;
            _mainWindow.Top  = workArea.Bottom - _mainWindow.Height + frameBottom;

            // Guard against partial off-screen on multi-monitor setups
            var screen = System.Windows.Forms.Screen.FromPoint(
                new System.Drawing.Point((int)_mainWindow.Left, (int)_mainWindow.Top));
            var sb = screen.WorkingArea;

            if (_mainWindow.Left < sb.Left) _mainWindow.Left = sb.Left;
            if (_mainWindow.Top  < sb.Top)  _mainWindow.Top  = sb.Top;
            if (_mainWindow.Left + _mainWindow.Width  > sb.Right)
                _mainWindow.Left = sb.Right  - _mainWindow.Width;
            if (_mainWindow.Top  + _mainWindow.Height > sb.Bottom)
                _mainWindow.Top  = sb.Bottom - _mainWindow.Height;
        }

        public void ShowNotification(string title, string text)
        {
            _trayIcon.ShowBalloonTip(6000, title, text, ToolTipIcon.None);
        }

        public void SetToolTip(string text) => _trayIcon.Text = text;
        public void SetIcon(Icon icon) => _trayIcon.Icon = icon;

        public void Dispose()
        {
            if (!_disposed)
            {
                _trayIcon.Dispose();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
