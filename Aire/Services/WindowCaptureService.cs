using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Aire.Services;

public sealed record TopLevelWindowInfo
{
    public string WindowId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool IsSelected { get; init; }
}

public sealed record WindowSelectionRequest
{
    public string? WindowId { get; init; }
    public string? ExactTitle { get; init; }
    public string? TitleContains { get; init; }
    public string? ProcessName { get; init; }
    public bool UseActiveWindow { get; init; }
}

public sealed record WindowCaptureOptions
{
    public string? OutputPath { get; init; }
    public int Padding { get; init; } = 16;
    public bool ActivateWindow { get; init; } = true;
    public bool ReturnBase64 { get; init; }
}

public sealed record WindowCaptureResult
{
    public bool Ok { get; init; }
    public string WindowId { get; init; } = string.Empty;
    public string WindowTitle { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public string? PngPath { get; init; }
    public string? PngBase64 { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Enumerates and captures top-level windows for both the local API and the screenshots tool.
/// </summary>
public static class WindowCaptureService
{
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const int MaxAutoTrimPerSide = 32;
    private const int BorderLuminanceThreshold = 52;
    private const double BorderPixelRatioThreshold = 0.94;
    private const int MaxCornerCleanupDepth = 48;

    public static IReadOnlyList<TopLevelWindowInfo> ListWindows()
    {
        var active = GetForegroundWindow();
        var selectedId = AppState.GetSelectedWindowId();

        return EnumerateTopLevelWindowRecords()
            .Select(window => ToInfo(window, active, selectedId))
            .OrderBy(window => window.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(window => window.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static TopLevelWindowInfo? GetSelectedWindow()
    {
        var selected = GetSelectedNativeWindow();
        if (selected == null)
            return null;

        var active = GetForegroundWindow();
        var selectedId = GetWindowId(selected.Handle);
        return ToInfo(selected, active, selectedId);
    }

    public static TopLevelWindowInfo SelectWindow(string windowId)
    {
        if (string.IsNullOrWhiteSpace(windowId))
            throw new InvalidOperationException("A window id is required.");

        var window = FindWindowById(windowId)
            ?? throw new InvalidOperationException($"No window found with id '{windowId}'.");

        AppState.SetSelectedWindowId(windowId);
        return ToInfo(window, GetForegroundWindow(), windowId);
    }

    public static TopLevelWindowInfo SelectWindow(WindowSelectionRequest request)
    {
        var window = ResolveWindow(request);
        var windowId = GetWindowId(window.Handle);
        AppState.SetSelectedWindowId(windowId);
        return ToInfo(window, GetForegroundWindow(), windowId);
    }

    public static WindowCaptureResult CaptureSelectedWindow(WindowCaptureOptions? options = null)
    {
        var selected = GetSelectedNativeWindow()
            ?? throw new InvalidOperationException("No window is currently selected.");

        return Capture(selected, options ?? new WindowCaptureOptions());
    }

    public static WindowCaptureResult CaptureWindow(string windowId, WindowCaptureOptions? options = null)
        => CaptureWindow(new WindowSelectionRequest { WindowId = windowId }, options);

    public static WindowCaptureResult CaptureWindow(WindowSelectionRequest request, WindowCaptureOptions? options = null)
    {
        var window = ResolveWindow(request);
        return Capture(window, options ?? new WindowCaptureOptions());
    }

    public static WindowCaptureResult CaptureActiveWindow(WindowCaptureOptions? options = null)
        => Capture(GetActiveWindow(), options ?? new WindowCaptureOptions { ActivateWindow = false });

    public static void ActivateWindow(IntPtr handle)
    {
        ShowWindow(handle, SwRestore);
        SetForegroundWindow(handle);
        Thread.Sleep(150);
    }

    public static void CloseWindow(IntPtr handle)
        => PostMessage(handle, WmClose, IntPtr.Zero, IntPtr.Zero);

    private static WindowCaptureResult Capture(NativeWindowRecord window, WindowCaptureOptions options)
    {
        if (options.ActivateWindow)
            ActivateWindow(window.Handle);

        if (!TryGetCaptureRect(window.Handle, out var rect))
            throw new InvalidOperationException($"Failed to get bounds for '{window.Title}'.");

        var padding = Math.Max(0, options.Padding);
        var x = Math.Max(0, rect.Left - padding);
        var y = Math.Max(0, rect.Top - padding);
        var width = Math.Max(1, (rect.Right - rect.Left) + (padding * 2));
        var height = Math.Max(1, (rect.Bottom - rect.Top) + (padding * 2));

        using var bitmap = new Bitmap(width, height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height));
        }

        using var prepared = CleanCornerArtifacts(TrimCapturedFrame(bitmap));
        using var pngStream = new MemoryStream();
        prepared.Save(pngStream, System.Drawing.Imaging.ImageFormat.Png);
        var pngBytes = pngStream.ToArray();

        string? outputPath = null;
        if (!string.IsNullOrWhiteSpace(options.OutputPath))
        {
            outputPath = Path.GetFullPath(options.OutputPath);
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
                Directory.CreateDirectory(outputDirectory);
            File.WriteAllBytes(outputPath, pngBytes);
        }
        else if (!options.ReturnBase64)
        {
            outputPath = Path.Combine(
                Path.GetTempPath(),
                "Aire",
                $"capture-{window.ProcessName}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.png");
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
                Directory.CreateDirectory(outputDirectory);
            File.WriteAllBytes(outputPath, pngBytes);
        }

        return new WindowCaptureResult
        {
            Ok = true,
            WindowId = GetWindowId(window.Handle),
            WindowTitle = window.Title,
            ProcessName = window.ProcessName,
            PngPath = outputPath,
            PngBase64 = options.ReturnBase64 ? Convert.ToBase64String(pngBytes) : null
        };
    }

    private static NativeWindowRecord ResolveWindow(WindowSelectionRequest request)
    {
        var activeHandle = GetForegroundWindow();
        if (activeHandle == IntPtr.Zero)
            throw new InvalidOperationException("No active window was found.");

        var selectedId = AppState.GetSelectedWindowId();
        var nativeWindows = EnumerateTopLevelWindowRecords().ToList();
        var windowInfos = nativeWindows
            .Select(window => ToInfo(window, activeHandle, selectedId))
            .ToList();

        var selected = windowInfos.FirstOrDefault(window => window.IsSelected);
        var active = windowInfos.FirstOrDefault(window => window.IsActive);
        var resolved = ResolveWindowCandidate(request, windowInfos, selected, active);

        return nativeWindows.FirstOrDefault(window => string.Equals(
                GetWindowId(window.Handle),
                resolved.WindowId,
                StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No window found with id '{resolved.WindowId}'.");
    }

    private static NativeWindowRecord? GetSelectedNativeWindow()
    {
        var selectedId = AppState.GetSelectedWindowId();
        if (string.IsNullOrWhiteSpace(selectedId))
            return null;

        return FindWindowById(selectedId);
    }

    private static NativeWindowRecord? FindWindowById(string windowId)
        => EnumerateTopLevelWindowRecords()
            .FirstOrDefault(window => string.Equals(GetWindowId(window.Handle), windowId, StringComparison.OrdinalIgnoreCase));

    private static NativeWindowRecord GetActiveWindow()
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException("No active window was found.");

        return CreateWindow(handle)
            ?? throw new InvalidOperationException("Failed to inspect the active window.");
    }

    private static IEnumerable<NativeWindowRecord> EnumerateTopLevelWindowRecords()
    {
        var windows = new List<NativeWindowRecord>();

        EnumWindows((handle, _) =>
        {
            var window = CreateWindow(handle);
            if (window != null)
                windows.Add(window);

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static NativeWindowRecord? CreateWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero || !IsWindowVisible(handle))
            return null;

        var length = GetWindowTextLength(handle);
        if (length <= 0)
            return null;

        var titleBuffer = new StringBuilder(length + 1);
        var copied = GetWindowText(handle, titleBuffer, titleBuffer.Capacity);
        var title = titleBuffer.ToString(0, copied);
        if (string.IsNullOrWhiteSpace(title))
            return null;

        _ = GetWindowThreadProcessId(handle, out var processId);
        string processName;

        try
        {
            processName = Process.GetProcessById((int)processId).ProcessName;
        }
        catch
        {
            processName = string.Empty;
        }

        return new NativeWindowRecord(handle, title, processName);
    }

    private static TopLevelWindowInfo ToInfo(NativeWindowRecord window, IntPtr activeHandle, string selectedId)
    {
        var windowId = GetWindowId(window.Handle);
        return new TopLevelWindowInfo
        {
            WindowId = windowId,
            Title = window.Title,
            ProcessName = window.ProcessName,
            IsActive = window.Handle == activeHandle,
            IsSelected = string.Equals(windowId, selectedId, StringComparison.OrdinalIgnoreCase)
        };
    }

    internal static TopLevelWindowInfo ResolveWindowCandidate(
        WindowSelectionRequest request,
        IReadOnlyList<TopLevelWindowInfo> windows,
        TopLevelWindowInfo? selected,
        TopLevelWindowInfo? active)
    {
        if (request.UseActiveWindow)
            return active ?? throw new InvalidOperationException("No active window was found.");

        if (!string.IsNullOrWhiteSpace(request.WindowId))
        {
            return windows.FirstOrDefault(window => string.Equals(window.WindowId, request.WindowId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"No window found with id '{request.WindowId}'.");
        }

        if (!HasSelectionCriteria(request))
            return selected ?? active ?? throw new InvalidOperationException("No window is currently selected.");

        var matches = windows
            .Where(window => Matches(window.Title, window.ProcessName, request))
            .ToList();

        if (matches.Count > 0)
        {
            var nonUpdateMatches = matches
                .Where(window => !window.Title.Contains("Update", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var candidates = nonUpdateMatches.Count > 0 ? nonUpdateMatches : matches;
            return candidates
                .OrderByDescending(window => window.Title.Length)
                .First();
        }

        throw new InvalidOperationException("No window matched the requested filters.");
    }

    private static bool Matches(string title, string processName, WindowSelectionRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ExactTitle) &&
            !string.Equals(title, request.ExactTitle, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(request.TitleContains) &&
            title.IndexOf(request.TitleContains, StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        if (!string.IsNullOrWhiteSpace(request.ProcessName) &&
            !string.Equals(processName, request.ProcessName, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private static bool HasSelectionCriteria(WindowSelectionRequest request)
        => !string.IsNullOrWhiteSpace(request.WindowId)
           || !string.IsNullOrWhiteSpace(request.ExactTitle)
           || !string.IsNullOrWhiteSpace(request.TitleContains)
           || !string.IsNullOrWhiteSpace(request.ProcessName);

    private static string GetWindowId(IntPtr handle)
        => unchecked((ulong)handle.ToInt64()).ToString("X16", CultureInfo.InvariantCulture);

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

            Enqueue(visited, queue, current.X + 1, current.Y, current.Depth + 1);
            Enqueue(visited, queue, current.X - 1, current.Y, current.Depth + 1);
            Enqueue(visited, queue, current.X, current.Y + 1, current.Depth + 1);
            Enqueue(visited, queue, current.X, current.Y - 1, current.Depth + 1);
        }
    }

    private static void Enqueue(bool[,] visited, Queue<(int X, int Y, int Depth)> queue, int x, int y, int depth)
    {
        if (x < 0 || y < 0 || x >= visited.GetLength(0) || y >= visited.GetLength(1) || visited[x, y])
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

    private sealed record NativeWindowRecord(IntPtr Handle, string Title, string ProcessName);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out Rect pvAttribute, int cbAttribute);

    private const int SwRestore = 9;
    private const uint WmClose = 0x0010;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
