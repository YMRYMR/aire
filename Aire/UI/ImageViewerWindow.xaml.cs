using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MouseEventArgs       = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using KeyEventArgs         = System.Windows.Input.KeyEventArgs;
using MouseWheelEventArgs  = System.Windows.Input.MouseWheelEventArgs;
using Key                  = System.Windows.Input.Key;
using Keyboard             = System.Windows.Input.Keyboard;
using ModifierKeys         = System.Windows.Input.ModifierKeys;
using Cursors              = System.Windows.Input.Cursors;
using Point                = System.Windows.Point;
using Clipboard            = System.Windows.Clipboard;
using SaveFileDialog       = Microsoft.Win32.SaveFileDialog;

namespace Aire.UI;

public partial class ImageViewerWindow : Window
{
    // ── P/Invoke for dark title border (Windows 11) ───────────────────────
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    // ── Fields ────────────────────────────────────────────────────────────
    private readonly BitmapSource _bitmap;
    private readonly MatrixTransform _transform = new();
    private Matrix _matrix = Matrix.Identity;

    // Pan / fit state
    private bool   _isPanning;
    private Point  _panStart;
    private Matrix _matrixAtPanStart;
    private bool   _isInFitMode    = true;   // refit on resize while true
    private bool   _hasInitialFit  = false;  // true once the first fit has run

    // ── Constructor ───────────────────────────────────────────────────────
    public ImageViewerWindow(ImageSource source)
    {
        InitializeComponent();

        _bitmap = source as BitmapSource
            ?? throw new ArgumentException("Image source must be a BitmapSource.", nameof(source));

        ViewerImage.Source          = _bitmap;
        ViewerImage.RenderTransform = _transform;

        Loaded += OnLoaded;
    }

    // ── Window chrome / DWM ───────────────────────────────────────────────
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
        HwndSource.FromHwnd(hwnd).AddHook(WndProc);

        // ContentRendered fires once, after the window is fully shown and all
        // layout passes are complete — the only reliable moment ActualWidth/Height
        // of every child element are their final values.
        ContentRendered += OnContentRendered;

        ViewportBorder.Focus();
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= OnContentRendered;
        FitToWindow();
        _hasInitialFit = true;
    }

    /// <summary>Lets WPF handle resize on all four edges despite WindowStyle=None.</summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_NCHITTEST = 0x0084;
        const int HTTOP        = 12;
        const int resizePx     = 6;

        if (msg == WM_NCHITTEST)
        {
            int screenX = unchecked((short)(lParam.ToInt32() & 0xFFFF));
            int screenY = unchecked((short)(lParam.ToInt32() >> 16));
            GetWindowRect(hwnd, out var r);
            if (screenY >= r.Top && screenY < r.Top + resizePx)
            {
                handled = true;
                return new IntPtr(HTTOP);
            }
        }
        return IntPtr.Zero;
    }

    // ── Title bar ─────────────────────────────────────────────────────────
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        DragMove();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    // ── Fit / actual size ─────────────────────────────────────────────────

    // Natural DIP dimensions: what the Image element with Stretch=None lays out to.
    // _bitmap.Width/Height = PixelWidth/Height * (96 / embeddedDpi) — correct DIP size.
    private double BitmapDipWidth  => _bitmap.Width;
    private double BitmapDipHeight => _bitmap.Height;

    internal void FitToWindow(double? containerWidth = null, double? containerHeight = null)
    {
        var cw = containerWidth  ?? ViewportBorder.ActualWidth;
        var ch = containerHeight ?? ViewportBorder.ActualHeight;
        if (cw <= 0 || ch <= 0 || BitmapDipWidth <= 0 || BitmapDipHeight <= 0) return;

        var scale = Math.Min(cw / BitmapDipWidth, ch / BitmapDipHeight);
        var tx    = (cw - BitmapDipWidth  * scale) / 2.0;
        var ty    = (ch - BitmapDipHeight * scale) / 2.0;

        _isInFitMode = true;
        _matrix = new Matrix(scale, 0, 0, scale, tx, ty);
        ApplyTransform();
    }

    private void FitButton_Click(object sender, RoutedEventArgs e) => FitToWindow();

    private void ActualSizeButton_Click(object sender, RoutedEventArgs e)
        => ShowActualSize();

    private void Viewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Skip layout-time firings; the deferred OnLoaded handler does the initial fit.
        if (!_hasInitialFit) return;
        if (_isInFitMode && e.NewSize.Width > 0 && e.NewSize.Height > 0)
            FitToWindow(e.NewSize.Width, e.NewSize.Height);
    }

    // ── Zoom ──────────────────────────────────────────────────────────────
    private const double ZoomStep    = 1.18;
    private const double ZoomMin     = 0.04;
    private const double ZoomMax     = 20.0;

    internal void ZoomAt(Point pivot, double factor)
    {
        var currentScale = _matrix.M11;
        var newScale     = Math.Clamp(currentScale * factor, ZoomMin, ZoomMax);
        factor = newScale / currentScale;

        _isInFitMode = false;
        _matrix.ScaleAt(factor, factor, pivot.X, pivot.Y);
        ApplyTransform();
    }

    private Point ViewportCenter =>
        new(ViewportBorder.ActualWidth / 2, ViewportBorder.ActualHeight / 2);

    private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var factor = e.Delta > 0 ? ZoomStep : 1.0 / ZoomStep;
        ZoomAt(e.GetPosition(ViewportBorder), factor);
    }

    private void ZoomInButton_Click(object sender, RoutedEventArgs e)  => ZoomAt(ViewportCenter, ZoomStep);
    private void ZoomOutButton_Click(object sender, RoutedEventArgs e) => ZoomAt(ViewportCenter, 1.0 / ZoomStep);

    // ── Pan ───────────────────────────────────────────────────────────────
    private void Viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Double-click: toggle between fit and 1:1
        if (e.ClickCount == 2)
        {
            ToggleFitActual();
            return;
        }

        _isPanning         = true;
        _isInFitMode       = false;
        _panStart          = e.GetPosition(ViewportBorder);
        _matrixAtPanStart  = _matrix;
        ViewportBorder.CaptureMouse();
        ViewportBorder.Cursor = Cursors.SizeAll;
    }

    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning) return;
        var pos = e.GetPosition(ViewportBorder);
        _matrix = _matrixAtPanStart;
        _matrix.Translate(pos.X - _panStart.X, pos.Y - _panStart.Y);
        ApplyTransform();
    }

    private void Viewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isPanning = false;
        ViewportBorder.ReleaseMouseCapture();
        ViewportBorder.Cursor = Cursors.Arrow;
    }

    /// <summary>Right-click resets to fit-to-window.</summary>
    private void Viewport_MouseRightButtonUp(object sender, MouseButtonEventArgs e) =>
        FitToWindow();

    // ── Double-click toggle fit ↔ 1:1 ────────────────────────────────────
    private bool _isAtActualSize;
    private void ToggleFitActual()
    {
        if (_isAtActualSize)
            FitToWindow();
        else
            ShowActualSize();

        _isAtActualSize = !_isAtActualSize;
    }

    internal void ShowActualSize()
    {
        var cw = ViewportBorder.ActualWidth;
        var ch = ViewportBorder.ActualHeight;
        var tx = (cw - BitmapDipWidth) / 2.0;
        var ty = (ch - BitmapDipHeight) / 2.0;
        _isInFitMode = false;
        _matrix = new Matrix(1, 0, 0, 1, tx, ty);
        ApplyTransform();
    }

    // ── Keyboard ──────────────────────────────────────────────────────────
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                break;
            case Key.F:
                FitToWindow();
                break;
            case Key.Add:
            case Key.OemPlus:
                ZoomAt(ViewportCenter, ZoomStep);
                break;
            case Key.Subtract:
            case Key.OemMinus:
                ZoomAt(ViewportCenter, 1.0 / ZoomStep);
                break;
            case Key.C when (Keyboard.Modifiers & ModifierKeys.Control) != 0:
                CopyToClipboard();
                e.Handled = true;
                break;
            case Key.S when (Keyboard.Modifiers & ModifierKeys.Control) != 0:
                SaveImage();
                e.Handled = true;
                break;
        }
    }

    // ── Copy ──────────────────────────────────────────────────────────────
    private void CopyButton_Click(object sender, RoutedEventArgs e) => CopyToClipboard();

    private void CopyToClipboard()
    {
        try
        {
            Clipboard.SetImage(_bitmap);
        }
        catch (Exception ex)
        {
            ConfirmationDialog.ShowAlert(this, "Error", $"Copy failed: {ex.Message}");
        }
    }

    // ── Save ──────────────────────────────────────────────────────────────
    private void SaveButton_Click(object sender, RoutedEventArgs e) => SaveImage();

    private void SaveImage()
    {
        var dlg = new SaveFileDialog
        {
            Title      = "Save Image",
            Filter     = "PNG image|*.png|JPEG image|*.jpg;*.jpeg|BMP image|*.bmp",
            DefaultExt = ".png",
            FileName   = "image",
        };

        if (dlg.ShowDialog(this) != true) return;

        try
        {
            BitmapEncoder encoder = Path.GetExtension(dlg.FileName).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
                ".bmp"            => new BmpBitmapEncoder(),
                _                 => new PngBitmapEncoder(),
            };

            encoder.Frames.Add(BitmapFrame.Create(_bitmap));
            using var fs = File.Create(dlg.FileName);
            encoder.Save(fs);
        }
        catch (Exception ex)
        {
            ConfirmationDialog.ShowAlert(this, "Error", $"Save failed: {ex.Message}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    internal void ApplyTransform()
    {
        _transform.Matrix = _matrix;
        ZoomLabel.Text    = $"{(int)Math.Round(_matrix.M11 * 100)}%";
    }
}
