using System.Drawing;
using System.Runtime.InteropServices;

namespace Aire.Screenshots;

internal static class WindowCaptureService
{
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const int MaxAutoTrimPerSide = 32;
    private const int BorderLuminanceThreshold = 52;
    private const double BorderPixelRatioThreshold = 0.94;
    private const int MaxCornerCleanupDepth = 48;

    public static Bitmap Capture(ScreenshotRequest request)
    {
        var window = NativeWindowFinder.GetWindow(request);

        if (request.ActivateWindow)
            NativeWindowFinder.ActivateWindow(window.Handle);

        if (!TryGetCaptureRect(window.Handle, out var rect))
            throw new InvalidOperationException($"Failed to get bounds for '{window.Title}'.");

        var padding = Math.Max(0, request.Padding);
        var x = Math.Max(0, rect.Left - padding);
        var y = Math.Max(0, rect.Top - padding);
        var width = Math.Max(1, (rect.Right - rect.Left) + (padding * 2));
        var height = Math.Max(1, (rect.Bottom - rect.Top) + (padding * 2));

        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height));
        return CleanCornerArtifacts(TrimCapturedFrame(bitmap));
    }

    private static bool TryGetCaptureRect(IntPtr hwnd, out Rect rect)
    {
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf<Rect>()) == 0 &&
            rect.Right > rect.Left &&
            rect.Bottom > rect.Top)
        {
            return true;
        }

        return GetWindowRect(hwnd, out rect);
    }

    private static Bitmap TrimCapturedFrame(Bitmap bitmap)
    {
        var left = 0;
        var top = 0;
        var right = bitmap.Width - 1;
        var bottom = bitmap.Height - 1;

        for (var i = 0; i < MaxAutoTrimPerSide && left < right; i++)
        {
            if (!IsBorderColumn(bitmap, left, top, bottom))
                break;

            left++;
        }

        for (var i = 0; i < MaxAutoTrimPerSide && right > left; i++)
        {
            if (!IsBorderColumn(bitmap, right, top, bottom))
                break;

            right--;
        }

        for (var i = 0; i < MaxAutoTrimPerSide && top < bottom; i++)
        {
            if (!IsBorderRow(bitmap, top, left, right))
                break;

            top++;
        }

        for (var i = 0; i < MaxAutoTrimPerSide && bottom > top; i++)
        {
            if (!IsBorderRow(bitmap, bottom, left, right))
                break;

            bottom--;
        }

        if (left == 0 && top == 0 && right == bitmap.Width - 1 && bottom == bitmap.Height - 1)
            return bitmap;

        var trimmedWidth = Math.Max(1, right - left + 1);
        var trimmedHeight = Math.Max(1, bottom - top + 1);
        var trimmed = new Bitmap(trimmedWidth, trimmedHeight);

        using var graphics = Graphics.FromImage(trimmed);
        graphics.DrawImage(
            bitmap,
            new Rectangle(0, 0, trimmedWidth, trimmedHeight),
            new Rectangle(left, top, trimmedWidth, trimmedHeight),
            GraphicsUnit.Pixel);

        bitmap.Dispose();
        return trimmed;
    }

    private static Bitmap CleanCornerArtifacts(Bitmap bitmap)
    {
        FloodCorner(bitmap, 0, 0);
        FloodCorner(bitmap, bitmap.Width - 1, 0);
        FloodCorner(bitmap, 0, bitmap.Height - 1);
        FloodCorner(bitmap, bitmap.Width - 1, bitmap.Height - 1);
        return bitmap;
    }

    private static void FloodCorner(Bitmap bitmap, int startX, int startY)
    {
        if (startX < 0 || startY < 0 || startX >= bitmap.Width || startY >= bitmap.Height)
            return;

        if (!IsBorderPixel(bitmap.GetPixel(startX, startY)))
            return;

        var visited = new bool[bitmap.Width, bitmap.Height];
        var queue = new Queue<(int X, int Y, int Depth)>();
        queue.Enqueue((startX, startY, 0));
        visited[startX, startY] = true;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Depth > MaxCornerCleanupDepth)
                continue;

            var color = bitmap.GetPixel(current.X, current.Y);
            if (!IsBorderPixel(color))
                continue;

            bitmap.SetPixel(current.X, current.Y, Color.Transparent);

            Enqueue(bitmap, visited, queue, current.X + 1, current.Y, current.Depth + 1);
            Enqueue(bitmap, visited, queue, current.X - 1, current.Y, current.Depth + 1);
            Enqueue(bitmap, visited, queue, current.X, current.Y + 1, current.Depth + 1);
            Enqueue(bitmap, visited, queue, current.X, current.Y - 1, current.Depth + 1);
        }
    }

    private static void Enqueue(Bitmap bitmap, bool[,] visited, Queue<(int X, int Y, int Depth)> queue, int x, int y, int depth)
    {
        if (x < 0 || y < 0 || x >= bitmap.Width || y >= bitmap.Height || visited[x, y])
            return;

        visited[x, y] = true;
        queue.Enqueue((x, y, depth));
    }

    private static bool IsBorderRow(Bitmap bitmap, int y, int left, int right)
    {
        var borderPixels = 0;
        var totalPixels = right - left + 1;

        for (var x = left; x <= right; x++)
        {
            if (IsBorderPixel(bitmap.GetPixel(x, y)))
                borderPixels++;
        }

        return totalPixels > 0 && (double)borderPixels / totalPixels >= BorderPixelRatioThreshold;
    }

    private static bool IsBorderColumn(Bitmap bitmap, int x, int top, int bottom)
    {
        var borderPixels = 0;
        var totalPixels = bottom - top + 1;

        for (var y = top; y <= bottom; y++)
        {
            if (IsBorderPixel(bitmap.GetPixel(x, y)))
                borderPixels++;
        }

        return totalPixels > 0 && (double)borderPixels / totalPixels >= BorderPixelRatioThreshold;
    }

    private static bool IsBorderPixel(Color color)
    {
        if (color.A <= 8)
            return true;

        var luminance = (0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B);
        return luminance <= BorderLuminanceThreshold;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out Rect pvAttribute, int cbAttribute);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
