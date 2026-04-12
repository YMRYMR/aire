using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Aire.Services
{
    /// <summary>
    /// Registers a Win32 global hotkey and invokes a callback when pressed,
    /// regardless of which application has focus.
    /// </summary>
    internal sealed class GlobalHotkeyService : IDisposable
    {
        // ── P/Invoke ────────────────────────────────────────────────────────

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // ── Constants ───────────────────────────────────────────────────────

        private const int HotkeyId = 0x0001;

        private const uint WM_HOTKEY = 0x0312;

        // MOD_ALT = 0x0001, VK_SPACE = 0x20
        private const uint DefaultModifiers = 0x0001;
        private const uint DefaultVirtualKey = 0x20;

        // ── State ───────────────────────────────────────────────────────────

        private readonly Window _window;
        private HwndSource? _hwndSource;
        private bool _isRegistered;
        private bool _disposed;
        private readonly object _lock = new();

        /// <summary>
        /// Callback invoked on the UI thread when the hotkey is pressed.
        /// Default behaviour: toggle main window visibility.
        /// </summary>
        public Action? ToggleCallback { get; set; }

        // ── Lifecycle ───────────────────────────────────────────────────────

        public GlobalHotkeyService(Window window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
        }

        /// <summary>
        /// Registers the global hotkey. Idempotent — calling Start() while
        /// already started is a no-op.
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(GlobalHotkeyService));
                if (_isRegistered) return;

                var hwnd = new WindowInteropHelper(_window).Handle;
                if (hwnd == IntPtr.Zero)
                    throw new InvalidOperationException("Window handle is not yet available. Call Start() after the window is loaded.");

                _hwndSource = HwndSource.FromHwnd(hwnd);
                _hwndSource.AddHook(WndProc);

                if (!RegisterHotKey(hwnd, HotkeyId, DefaultModifiers, DefaultVirtualKey))
                {
                    var error = Marshal.GetLastWin32Error();
                    _hwndSource.RemoveHook(WndProc);
                    _hwndSource = null;

                    // ERROR_HOTKEY_ALREADY_REGISTERED (1409) — another app owns it.
                    AppLogger.Warn(
                        "GlobalHotkeyService",
                        $"Failed to register Alt+Space hotkey (Win32 error {error}). " +
                        "Another application may have registered this hotkey.");
                    return;
                }

                _isRegistered = true;
            }
        }

        /// <summary>
        /// Unregisters the global hotkey. Idempotent — calling Stop() while
        /// not started is a no-op.
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (!_isRegistered) return;

                var hwnd = new WindowInteropHelper(_window).Handle;

                if (hwnd != IntPtr.Zero)
                    UnregisterHotKey(hwnd, HotkeyId);

                if (_hwndSource != null)
                {
                    _hwndSource.RemoveHook(WndProc);
                    _hwndSource = null;
                }

                _isRegistered = false;
            }
        }

        // ── WndProc hook ────────────────────────────────────────────────────

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                handled = true;
                ToggleCallback?.Invoke();
            }

            return IntPtr.Zero;
        }

        // ── Dispose ─────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            Stop();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~GlobalHotkeyService()
        {
            // Safety net: unregister on finalization if Dispose was not called.
            // This runs on the finalizer thread so we call UnregisterHotKey
            // directly (no _hwndSource manipulation needed).
            try
            {
                if (_isRegistered)
                {
                    var hwnd = new WindowInteropHelper(_window).Handle;
                    if (hwnd != IntPtr.Zero)
                        UnregisterHotKey(hwnd, HotkeyId);
                }
            }
            catch
            {
                // Best-effort cleanup in finalizer — swallow exceptions.
            }
        }
    }
}
