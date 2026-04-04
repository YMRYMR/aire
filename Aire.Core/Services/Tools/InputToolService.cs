using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using static Aire.Services.Tools.ToolHelpers;

namespace Aire.Services.Tools
{
    /// <summary>
    /// Handles keyboard, mouse, and screenshot tools.
    /// Requires Windows; on other platforms every tool returns an "unsupported" message.
    /// </summary>
    public class InputToolService
    {
        private readonly Func<Task>? _hideWindowAsync;
        private readonly Func<Task>? _showWindowAsync;

        // Lazily created so the Win32 types are never loaded on non-Windows platforms.
#pragma warning disable CA1416
        private MouseControlService? _mouseService;
        private MouseControlService MouseService =>
            _mouseService ??= new MouseControlService();
#pragma warning restore CA1416

        private static readonly string ScreenshotTempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aire", "screenshots");

        public InputToolService(Func<Task>? hideWindowAsync = null, Func<Task>? showWindowAsync = null)
        {
            _hideWindowAsync = hideWindowAsync;
            _showWindowAsync = showWindowAsync;
        }

        public async Task<ToolExecutionResult> ExecuteAsync(ToolCallRequest request)
        {
            if (!OperatingSystem.IsWindows())
                return new ToolExecutionResult
                {
                    TextResult = $"Tool '{request.Tool}' is only supported on Windows."
                };

            try
            {
                switch (request.Tool)
                {
                    case "begin_keyboard_session":
                    {
#pragma warning disable CA1416
                        var (kw, kh) = MouseService.GetScreenSize();
#pragma warning restore CA1416
                        return new ToolExecutionResult
                        {
                            TextResult = $"Keyboard session started. Screen resolution: {kw}x{kh} pixels. " +
                                         $"You may now use key_combo, key_press, type_text, and take_screenshot."
                        };
                    }

                    case "end_keyboard_session":
                        return new ToolExecutionResult { TextResult = "Keyboard session ended." };

                    case "begin_mouse_session":
                    {
#pragma warning disable CA1416
                        var (bw, bh) = MouseService.GetScreenSize();
#pragma warning restore CA1416
                        return new ToolExecutionResult
                        {
                            TextResult = $"Mouse session started. Screen resolution: {bw}x{bh} pixels. " +
                                         $"Use pixel coordinates within 0-{bw - 1} (x) and 0-{bh - 1} (y) for all mouse operations."
                        };
                    }

                    case "end_mouse_session":
                        return new ToolExecutionResult { TextResult = "Mouse session ended." };

                    case "take_screenshot":
                    {
                        if (_hideWindowAsync != null)
                        {
                            await _hideWindowAsync();
                            await Task.Delay(400);
                        }

                        byte[] bytes;
                        try
                        {
#pragma warning disable CA1416
                            bytes = MouseService.TakeScreenshot();
#pragma warning restore CA1416
                        }
                        finally
                        {
                            if (_showWindowAsync != null)
                                await _showWindowAsync();
                        }

                        Directory.CreateDirectory(ScreenshotTempDir);
                        var path = Path.Combine(ScreenshotTempDir, $"shot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
                        await File.WriteAllBytesAsync(path, bytes);

#pragma warning disable CA1416
                        var (sw, sh) = MouseService.GetScreenSize();
#pragma warning restore CA1416
                        return new ToolExecutionResult
                        {
                            TextResult = $"Screenshot captured ({bytes.Length / 1024} KB). " +
                                         $"Screen resolution: {sw}x{sh} pixels. " +
                                         $"The image is shown in the chat. " +
                                         $"Use pixel coordinates within 0-{sw - 1} (x) and 0-{sh - 1} (y) for mouse operations.",
                            ScreenshotPath = path
                        };
                    }

                    case "mouse_move":
                    {
                        int x = GetInt(request, "x");
                        int y = GetInt(request, "y");
#pragma warning disable CA1416
                        MouseService.MoveMouse(x, y);
#pragma warning restore CA1416
                        return new ToolExecutionResult { TextResult = $"Mouse moved to ({x}, {y})." };
                    }

                    case "mouse_click":
                    {
                        int x      = GetInt(request, "x");
                        int y      = GetInt(request, "y");
                        var button = GetString(request, "button");
                        if (string.IsNullOrEmpty(button)) button = "left";
#pragma warning disable CA1416
                        MouseService.Click(x, y, button);
#pragma warning restore CA1416
                        return new ToolExecutionResult { TextResult = $"{button} click at ({x}, {y})." };
                    }

                    case "mouse_double_click":
                    {
                        int x = GetInt(request, "x");
                        int y = GetInt(request, "y");
#pragma warning disable CA1416
                        MouseService.DoubleClick(x, y);
#pragma warning restore CA1416
                        return new ToolExecutionResult { TextResult = $"Double-click at ({x}, {y})." };
                    }

                    case "mouse_drag":
                    {
                        int fx = GetInt(request, "from_x");
                        int fy = GetInt(request, "from_y");
                        int tx = GetInt(request, "to_x");
                        int ty = GetInt(request, "to_y");
#pragma warning disable CA1416
                        MouseService.Drag(fx, fy, tx, ty);
#pragma warning restore CA1416
                        return new ToolExecutionResult
                        {
                            TextResult = $"Dragged from ({fx}, {fy}) to ({tx}, {ty})."
                        };
                    }

                    case "type_text":
                    {
                        var text = GetString(request, "text");
#pragma warning disable CA1416
                        MouseService.TypeText(text);
#pragma warning restore CA1416
                        return new ToolExecutionResult { TextResult = $"Typed {text.Length} character(s)." };
                    }

                    case "key_press":
                    {
                        var key = GetString(request, "key");
#pragma warning disable CA1416
                        MouseService.KeyPress(key);
#pragma warning restore CA1416
                        return new ToolExecutionResult { TextResult = $"Key pressed: {key}." };
                    }

                    case "key_combo":
                    {
                        var keys = new List<string>();
                        if (request.Parameters.TryGetProperty("keys", out var keysEl))
                        {
                            if (keysEl.ValueKind == JsonValueKind.Array)
                                foreach (var k in keysEl.EnumerateArray())
                                    keys.Add(k.GetString() ?? string.Empty);
                            else if (keysEl.ValueKind == JsonValueKind.String)
                                keys.AddRange((keysEl.GetString() ?? string.Empty)
                                    .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                        }
                        keys.RemoveAll(string.IsNullOrWhiteSpace);
                        if (keys.Count == 0)
                            return new ToolExecutionResult { TextResult = "key_combo: no keys specified." };
#pragma warning disable CA1416
                        MouseService.KeyCombo(keys.ToArray());
#pragma warning restore CA1416
                        return new ToolExecutionResult
                        {
                            TextResult = $"Key combo: {string.Join("+", keys)}."
                        };
                    }

                    case "mouse_scroll":
                    {
                        int x     = GetInt(request, "x");
                        int y     = GetInt(request, "y");
                        int delta = request.Parameters.TryGetProperty("delta", out var dEl)
                            ? (dEl.ValueKind == JsonValueKind.Number ? dEl.GetInt32() : 3)
                            : 3;
#pragma warning disable CA1416
                        MouseService.Scroll(x, y, delta);
#pragma warning restore CA1416
                        return new ToolExecutionResult
                        {
                            TextResult = $"Scrolled {(delta >= 0 ? "up" : "down")} at ({x}, {y}) by {Math.Abs(delta)} unit(s)."
                        };
                    }

                    default:
                        return new ToolExecutionResult { TextResult = $"Unknown input tool: {request.Tool}" };
                }
            }
        catch
            {
            return new ToolExecutionResult { TextResult = $"Error executing {request.Tool}." };
            }
        }
    }
}
