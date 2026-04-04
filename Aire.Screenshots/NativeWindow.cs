using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Aire.Screenshots;

internal sealed record NativeWindow(IntPtr Handle, string Title, string ProcessName);

internal static class NativeWindowFinder
{
    public static IReadOnlyList<NativeWindow> ListWindows()
        => EnumerateTopLevelWindows()
            .OrderBy(window => window.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(window => window.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static NativeWindow GetWindow(ScreenshotRequest request)
    {
        if (request.UseActiveWindow)
            return GetActiveWindow();

        var matches = EnumerateTopLevelWindows()
            .Where(window => Matches(window, request))
            .ToList();

        if (matches.Count == 0)
            throw new InvalidOperationException("No window matched the requested filters.");

        return matches
            .OrderByDescending(window => window.Title.Length)
            .First();
    }

    public static bool TryGetWindow(ScreenshotRequest request, out NativeWindow? window)
    {
        try
        {
            window = GetWindow(request);
            return true;
        }
        catch
        {
            window = null;
            return false;
        }
    }

    public static void ActivateWindow(IntPtr handle)
    {
        ShowWindow(handle, SwRestore);
        SetForegroundWindow(handle);
        Thread.Sleep(150);
    }

    private static NativeWindow GetActiveWindow()
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException("No active window was found.");

        return CreateWindow(handle)
            ?? throw new InvalidOperationException("Failed to inspect the active window.");
    }

    private static IEnumerable<NativeWindow> EnumerateTopLevelWindows()
    {
        var windows = new List<NativeWindow>();

        EnumWindows((handle, _) =>
        {
            var window = CreateWindow(handle);
            if (window != null)
                windows.Add(window);

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static NativeWindow? CreateWindow(IntPtr handle)
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

        return new NativeWindow(handle, title, processName);
    }

    private static bool Matches(NativeWindow window, ScreenshotRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ExactTitle) &&
            !string.Equals(window.Title, request.ExactTitle, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(request.TitleContains) &&
            window.Title.IndexOf(request.TitleContains, StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        if (!string.IsNullOrWhiteSpace(request.ProcessName) &&
            !string.Equals(window.ProcessName, request.ProcessName, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

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

    private const int SwRestore = 9;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
}
