using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using Aire.Services;

namespace Aire.UI
{
    public partial class HelpWindow : Window
    {
        // ── Window state persistence ──────────────────────────────────────────
        internal static readonly string StatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aire", "helpstate.json");

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        // ── Tab / search state ────────────────────────────────────────────────

        private string _activeTab = "";
        private List<HelpSection> _allSections = new();

        // ─────────────────────────────────────────────────────────────────────

        public HelpWindow()
        {
            InitializeComponent();
            LoadWindowState();
            SizeChanged     += (_, _) => SaveWindowState();
            LocationChanged += (_, _) => SaveWindowState();
            Loaded          += OnWindowLoaded;
        }
    }
}
