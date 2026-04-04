using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Aire.Services;

namespace Aire
{
    public partial class MainWindow
    {
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        private void OnAttachedToTrayChanged(object? sender, EventArgs e)
        {
            if (TrayService != null)
                _isAttached = TrayService.IsAttachedToTray;
            UpdateTopmost();
        }

        private void UpdateTopmost()
        {
            this.Topmost = TrayService?.IsAttachedToTray ?? _isAttached;
            if (_panicButton != null)
                _panicButton.Topmost = this.Topmost;
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            if (_helpWindow != null) { _helpWindow.Activate(); return; }

            _helpWindow = new UI.HelpWindow { Owner = this };
            _helpWindow.Closed += (_, _) => _helpWindow = null;
            _helpWindow.Show();
        }

        private void HeaderBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                TrayService?.DetachFromTray();
                DragMove();
                SaveWindowSize();
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            _ = ShowSettingsWindowAsync();
        }

        public Task ShowSettingsWindowAsync()
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.Activate();
                return Task.CompletedTask;
            }

            _settingsWindow = new UI.SettingsWindow(_ttsService) { Owner = this };
            _settingsWindow.ProvidersChanged += async () => await RefreshProvidersAsync();
            _settingsWindow.AppearanceChanged += SaveWindowSize;
            _settingsWindow.Closed += async (_, _) =>
            {
                _settingsWindow = null;
                await RefreshProvidersAsync();
            };
            _settingsWindow.Show();
            return Task.CompletedTask;
        }

        public Task ShowMainWindowAsync()
        {
            Dispatcher.Invoke(() => TrayService?.ShowMainWindow());
            return Task.CompletedTask;
        }

        public Task HideMainWindowAsync()
        {
            Dispatcher.Invoke(Hide);
            return Task.CompletedTask;
        }

        public Task ShowBrowserWindowAsync()
        {
            Dispatcher.Invoke(() =>
            {
                if (UI.WebViewWindow.Current is { } existing)
                {
                    existing.Activate();
                    existing.WindowState = WindowState.Normal;
                }
                else
                {
                    new UI.WebViewWindow().Show();
                }
            });
            return Task.CompletedTask;
        }

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            if (TrayService == null) return;
            if (TrayService.IsAttachedToTray)
            {
                TrayService.ShowMainWindow();
            }
            else
            {
                TrayService.IsAttachedToTray = true;
                _isAttached = true;
                TrayService.ShowMainWindow();
                SaveWindowSize();
            }
            UpdatePinButton();
        }

        private void UpdatePinButton()
        {
            bool attached = TrayService?.IsAttachedToTray ?? _isAttached;
            PinButton.Content = attached ? "\U0001F4CC" : "\U0001F4CD";
            PinButton.ToolTip = attached
                ? LocalizationService.S("tooltip.pinAttached", "Pinned to tray — click to re-snap above system tray")
                : LocalizationService.S("tooltip.pinDetached", "Unpinned — click to snap back above system tray");
        }

        private void BrowserButton_Click(object sender, RoutedEventArgs e)
        {
            if (Aire.UI.WebViewWindow.Current is { } existing)
            {
                existing.Activate();
                existing.WindowState = System.Windows.WindowState.Normal;
            }
            else
            {
                new Aire.UI.WebViewWindow().Show();
            }
        }

        private void RestoreWindowSizes_Click(object sender, RoutedEventArgs e)
        {
            // ── Reset MainWindow ──
            Width  = 460;
            Height = 620;
            TrayService?.ShowMainWindow(); // re-snap to taskbar position

            // ── Reset SettingsWindow if open ──
            if (Aire.UI.SettingsWindow.Current is { } sw)
            {
                sw.Width  = 760;
                sw.Height = 700;
                sw.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                sw.Left   = (SystemParameters.WorkArea.Width  - sw.Width)  / 2;
                sw.Top    = (SystemParameters.WorkArea.Height - sw.Height) / 2;
            }

            // ── Reset WebViewWindow if open ──
            if (Aire.UI.WebViewWindow.Current is { } bw)
            {
                bw.Width  = 1100;
                bw.Height = 720;
                bw.Left   = (SystemParameters.WorkArea.Width  - bw.Width)  / 2;
                bw.Top    = (SystemParameters.WorkArea.Height - bw.Height) / 2;
            }

            // ── Delete persisted state files ──
            try { if (File.Exists(Aire.UI.SettingsWindow.StatePath)) File.Delete(Aire.UI.SettingsWindow.StatePath); } catch { }
            try { if (File.Exists(Aire.UI.WebViewWindow.StatePath))  File.Delete(Aire.UI.WebViewWindow.StatePath);  } catch { }

            // ── Persist reset MainWindow size ──
            SaveWindowSize();
        }

        private void ApplyLocalization()
        {
            var L = LocalizationService.S;
            HelpButton.ToolTip = L("tooltip.help", "Help");
            SettingsButton.ToolTip = L("tooltip.settings", "Settings");
            BrowserButton.ToolTip = L("tooltip.browser", "Open browser  (AI can read open tabs)");
            SearchButton.ToolTip = L("tooltip.searchChat", "Find in chat  (Ctrl+F)");
            ModeButton.ToolTip = L("tooltip.mode", "Assistant mode");
            MouseSessionLabel.Text = L("main.sessionActive", "Session active");
            EndSessionButton.Content = L("main.endSession", "End session");
            ThinkingText.Text = L("main.thinking", "Thinking\u2026");
            RemoveImageButton.Content = L("main.remove", "Remove");
            RemoveImageButton.ToolTip = L("tooltip.removeAttachment", "Remove attachment");
            StopAiButton.ToolTip = L("tooltip.stopThinking", "Stop AI thinking");
            SidebarToggleButton.ToolTip = L("tooltip.sidebar", "Conversation history");
            CheckAgainButton.ToolTip = L("tooltip.checkAvailability", "Check if provider is available again");
            ConversationSidebar.ToolTip = null;
            ConversationSidebar.NewConversationButtonToolTip = L("tooltip.newConversation", "New conversation");
            SearchPrevButton.ToolTip = L("tooltip.previousMatch", "Previous match");
            SearchNextButton.ToolTip = L("tooltip.nextMatch", "Next match");
            CloseSearchButton.ToolTip = L("tooltip.close", "Close");
            FileChipBorder.ToolTip = L("tooltip.openFile", "Click to open file");
            UpdatePinButton();

            var warningText = LargeFileWarning.Child as System.Windows.Controls.TextBlock;
            if (warningText != null)
                warningText.Text = L("warning.largeFile", "⚠ Large file — provider may reject");

            Resources["PlaceholderSearchText"] = L("placeholder.search", "Search…");
            UpdateModeButtonState();
            UpdateVoiceOutputButton();
            SetMicButtonState(_speechService.IsListening ? MicState.Recording : MicState.Idle);
            RefreshToolsCategoryMenuLocalization();
            UpdateToolsButtonState();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTLEFT = 10;
            const int HTRIGHT = 11;
            const int HTTOP = 12;
            const int HTTOPLEFT = 13;
            const int HTTOPRIGHT = 14;
            const int HTBOTTOM = 15;
            const int HTBOTTOMLEFT = 16;
            const int HTBOTTOMRIGHT = 17;
            const int topBorder = 6;
            const int border = 4;

            if (msg == WM_NCHITTEST && ResizeMode == ResizeMode.CanResize)
            {
                int screenX = unchecked((short)(lParam.ToInt32() & 0xFFFF));
                int screenY = unchecked((short)(lParam.ToInt32() >> 16));
                GetWindowRect(hwnd, out var r);

                bool left = screenX >= r.Left && screenX < r.Left + border;
                bool right = screenX <= r.Right && screenX > r.Right - border;
                bool top = screenY >= r.Top && screenY < r.Top + topBorder;
                bool bottom = screenY <= r.Bottom && screenY > r.Bottom - border;

                if (top && left) { handled = true; return new IntPtr(HTTOPLEFT); }
                if (top && right) { handled = true; return new IntPtr(HTTOPRIGHT); }
                if (bottom && left) { handled = true; return new IntPtr(HTBOTTOMLEFT); }
                if (bottom && right) { handled = true; return new IntPtr(HTBOTTOMRIGHT); }
                if (top) { handled = true; return new IntPtr(HTTOP); }
                if (bottom) { handled = true; return new IntPtr(HTBOTTOM); }
                if (left) { handled = true; return new IntPtr(HTLEFT); }
                if (right) { handled = true; return new IntPtr(HTRIGHT); }
            }

            return IntPtr.Zero;
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            InitializeNativeWindow();
            InputTextBox.Focus();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        public void Cleanup()
        {
            _speechService.Dispose();
            _ttsService.Dispose();
        }
    }
}
