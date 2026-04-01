using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace Aire.Services
{
    /// <summary>
    /// Controls the mouse, keyboard, and captures screenshots via Win32 APIs.
    /// Windows-only — wrap all call sites with an <see cref="OperatingSystem.IsWindows"/> guard.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class MouseControlService
    {
        // ── Win32 constants ───────────────────────────────────────────────────

        private const uint INPUT_MOUSE    = 0;
        private const uint INPUT_KEYBOARD = 1;

        private const uint MOUSEEVENTF_LEFTDOWN   = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP     = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN  = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP    = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP   = 0x0040;
        private const uint MOUSEEVENTF_WHEEL      = 0x0800;
        private const int  WHEEL_DELTA            = 120;

        private const uint KEYEVENTF_KEYUP   = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        // ── Win32 structs ─────────────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx, dy, mouseData;
            public uint dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk, wScan;
            public uint dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUT_UNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUT_UNION u;
        }

        // ── P/Invoke ──────────────────────────────────────────────────────────

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        // ── Public API ────────────────────────────────────────────────────────

        public void MoveMouse(int x, int y) => SetCursorPos(x, y);

        public void Click(int x, int y, string button = "left")
        {
            SetCursorPos(x, y);
            Thread.Sleep(50);

            (uint downFlag, uint upFlag) = button.ToLowerInvariant() switch
            {
                "right"  => (MOUSEEVENTF_RIGHTDOWN,  MOUSEEVENTF_RIGHTUP),
                "middle" => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP),
                _        => (MOUSEEVENTF_LEFTDOWN,   MOUSEEVENTF_LEFTUP)
            };

            SendMouseEvent(downFlag);
            Thread.Sleep(50);
            SendMouseEvent(upFlag);
        }

        public void DoubleClick(int x, int y)
        {
            Click(x, y);
            Thread.Sleep(100);
            Click(x, y);
        }

        public void Drag(int fromX, int fromY, int toX, int toY)
        {
            SetCursorPos(fromX, fromY);
            Thread.Sleep(100);
            SendMouseEvent(MOUSEEVENTF_LEFTDOWN);
            Thread.Sleep(100);

            const int steps = 20;
            for (int i = 1; i <= steps; i++)
            {
                SetCursorPos(fromX + (toX - fromX) * i / steps,
                             fromY + (toY - fromY) * i / steps);
                Thread.Sleep(10);
            }

            SendMouseEvent(MOUSEEVENTF_LEFTUP);
        }

        public void Scroll(int x, int y, int delta = 3)
        {
            SetCursorPos(x, y);
            Thread.Sleep(50);
            int wheelDelta = delta * WHEEL_DELTA;
            var input = new INPUT
            {
                type = INPUT_MOUSE,
                u = { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_WHEEL, mouseData = wheelDelta } }
            };
            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }

        public void TypeText(string text)
        {
            var inputs = new INPUT[2];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[1].type = INPUT_KEYBOARD;
            int sz = Marshal.SizeOf<INPUT>();

            foreach (char c in text)
            {
                inputs[0].u.ki = new KEYBDINPUT { wScan = c, dwFlags = KEYEVENTF_UNICODE };
                inputs[1].u.ki = new KEYBDINPUT { wScan = c, dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP };
                SendInput(2, inputs, sz);
                Thread.Sleep(10);
            }
        }

        public void KeyPress(string key)
        {
            var vk = ParseVirtualKey(key);
            if (vk == 0) return;

            int sz = Marshal.SizeOf<INPUT>();
            var inputs = new[]
            {
                new INPUT { type = INPUT_KEYBOARD, u = { ki = new KEYBDINPUT { wVk = vk } } },
                new INPUT { type = INPUT_KEYBOARD, u = { ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } } }
            };
            SendInput(2, inputs, sz);
        }

        /// <summary>
        /// Sends a key combination (e.g. Ctrl+N, Alt+Tab, Ctrl+Shift+S).
        /// All keys are pressed down in order then released in reverse.
        /// </summary>
        public void KeyCombo(string[] keys)
        {
            var vks = keys.Select(ParseVirtualKey).Where(v => v != 0).ToArray();
            if (vks.Length == 0) return;

            int sz = Marshal.SizeOf<INPUT>();
            var inputs = new INPUT[vks.Length * 2];
            for (int i = 0; i < vks.Length; i++)
                inputs[i] = new INPUT { type = INPUT_KEYBOARD, u = { ki = new KEYBDINPUT { wVk = vks[i] } } };
            for (int i = 0; i < vks.Length; i++)
                inputs[vks.Length + i] = new INPUT { type = INPUT_KEYBOARD, u = { ki = new KEYBDINPUT { wVk = vks[vks.Length - 1 - i], dwFlags = KEYEVENTF_KEYUP } } };
            SendInput((uint)inputs.Length, inputs, sz);
        }

        /// <summary>Captures the primary screen and returns the PNG bytes.</summary>
        public byte[] TakeScreenshot()
        {
            int w = GetSystemMetrics(SM_CXSCREEN);
            int h = GetSystemMetrics(SM_CYSCREEN);

            using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
                g.CopyFromScreen(0, 0, 0, 0, new Size(w, h));

            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }

        public (int Width, int Height) GetScreenSize() =>
            (GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void SendMouseEvent(uint flags)
        {
            var input = new INPUT { type = INPUT_MOUSE, u = { mi = new MOUSEINPUT { dwFlags = flags } } };
            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }

        private static ushort ParseVirtualKey(string key) => key.ToUpperInvariant() switch
        {
            "ENTER" or "RETURN"              => 0x0D,
            "TAB"                            => 0x09,
            "ESCAPE" or "ESC"                => 0x1B,
            "BACKSPACE"                      => 0x08,
            "DELETE" or "DEL"                => 0x2E,
            "HOME"                           => 0x24,
            "END"                            => 0x23,
            "PAGEUP" or "PGUP"               => 0x21,
            "PAGEDOWN" or "PGDN"             => 0x22,
            "LEFT"                           => 0x25,
            "UP"                             => 0x26,
            "RIGHT"                          => 0x27,
            "DOWN"                           => 0x28,
            "CTRL" or "CONTROL"              => 0x11,
            "ALT"                            => 0x12,
            "SHIFT"                          => 0x10,
            "WIN" or "WINDOWS" or "LWIN"     => 0x5B,
            "SPACE"                          => 0x20,
            "F1"  => 0x70, "F2"  => 0x71, "F3"  => 0x72, "F4"  => 0x73,
            "F5"  => 0x74, "F6"  => 0x75, "F7"  => 0x76, "F8"  => 0x77,
            "F9"  => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
            var s when s.Length == 1         => (ushort)s[0],
            _                                => 0
        };
    }
}
