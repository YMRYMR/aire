using System;
using System.Windows;
using System.Windows.Media;
using Cursors = System.Windows.Input.Cursors;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseWheelEventArgs = System.Windows.Input.MouseWheelEventArgs;
using Point = System.Windows.Point;

namespace Aire.UI;

public partial class ImageViewerWindow
{
    private const double ZoomStep = 1.18;
    private const double ZoomMin = 0.04;
    private const double ZoomMax = 20.0;

    // Natural DIP dimensions: _bitmap.Width/Height already account for embedded DPI.
    private double BitmapDipWidth => _bitmap.Width;
    private double BitmapDipHeight => _bitmap.Height;

    private Point ViewportCenter =>
        new(ViewportBorder.ActualWidth / 2, ViewportBorder.ActualHeight / 2);

    internal void FitToWindow(double? containerWidth = null, double? containerHeight = null)
    {
        double width = containerWidth ?? ViewportBorder.ActualWidth;
        double height = containerHeight ?? ViewportBorder.ActualHeight;
        if (width <= 0 || height <= 0 || BitmapDipWidth <= 0 || BitmapDipHeight <= 0)
        {
            return;
        }

        double scale = Math.Min(width / BitmapDipWidth, height / BitmapDipHeight);
        double tx = (width - BitmapDipWidth * scale) / 2.0;
        double ty = (height - BitmapDipHeight * scale) / 2.0;

        _isInFitMode = true;
        _matrix = new Matrix(scale, 0, 0, scale, tx, ty);
        ApplyTransform();
    }

    internal void ShowActualSize()
    {
        double width = ViewportBorder.ActualWidth;
        double height = ViewportBorder.ActualHeight;
        double tx = (width - BitmapDipWidth) / 2.0;
        double ty = (height - BitmapDipHeight) / 2.0;

        _isInFitMode = false;
        _matrix = new Matrix(1, 0, 0, 1, tx, ty);
        ApplyTransform();
    }

    internal void ZoomAt(Point pivot, double factor)
    {
        double currentScale = _matrix.M11;
        double newScale = Math.Clamp(currentScale * factor, ZoomMin, ZoomMax);
        double appliedFactor = newScale / currentScale;

        _isInFitMode = false;
        _matrix.ScaleAt(appliedFactor, appliedFactor, pivot.X, pivot.Y);
        ApplyTransform();
    }

    internal void ApplyTransform()
    {
        _transform.Matrix = _matrix;
        ZoomLabel.Text = $"{(int)Math.Round(_matrix.M11 * 100)}%";
    }

    private void ToggleFitActual()
    {
        if (_isAtActualSize)
        {
            FitToWindow();
        }
        else
        {
            ShowActualSize();
        }

        _isAtActualSize = !_isAtActualSize;
    }

    private void FitButton_Click(object sender, RoutedEventArgs e)
        => FitToWindow();

    private void ActualSizeButton_Click(object sender, RoutedEventArgs e)
        => ShowActualSize();

    private void Viewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_hasInitialFit)
        {
            return;
        }

        if (_isInFitMode && e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            FitToWindow(e.NewSize.Width, e.NewSize.Height);
        }
    }

    private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        double factor = e.Delta > 0 ? ZoomStep : 1.0 / ZoomStep;
        ZoomAt(e.GetPosition(ViewportBorder), factor);
    }

    private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        => ZoomAt(ViewportCenter, ZoomStep);

    private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        => ZoomAt(ViewportCenter, 1.0 / ZoomStep);

    private void Viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleFitActual();
            return;
        }

        _isPanning = true;
        _isInFitMode = false;
        _panStart = e.GetPosition(ViewportBorder);
        _matrixAtPanStart = _matrix;
        ViewportBorder.CaptureMouse();
        ViewportBorder.Cursor = Cursors.SizeAll;
    }

    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        Point pos = e.GetPosition(ViewportBorder);
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
    private void Viewport_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        => FitToWindow();
}
