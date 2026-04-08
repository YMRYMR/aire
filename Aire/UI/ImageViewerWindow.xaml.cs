using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;

namespace Aire.UI;

public partial class ImageViewerWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private readonly BitmapSource _bitmap;
    private readonly MatrixTransform _transform = new();
    private Matrix _matrix = Matrix.Identity;

    private bool _isPanning;
    private Point _panStart;
    private Matrix _matrixAtPanStart;
    private bool _isInFitMode = true;
    private bool _hasInitialFit;
    private bool _isAtActualSize;

    public ImageViewerWindow(ImageSource source)
    {
        InitializeComponent();

        _bitmap = source as BitmapSource
            ?? throw new ArgumentException("Image source must be a BitmapSource.", nameof(source));

        ViewerImage.Source = _bitmap;
        ViewerImage.RenderTransform = _transform;

        Loaded += OnLoaded;
    }
}
