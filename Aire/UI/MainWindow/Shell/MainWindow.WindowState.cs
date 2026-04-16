using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using Aire.Bootstrap;
using Aire.Services;
using WinFormsScreen = System.Windows.Forms.Screen;

namespace Aire
{
    public partial class MainWindow
    {
        private void LoadWindowSize()
        {
            SetupPreferences setupPreferences = SetupPreferencesStore.Load();

            try
            {
                JsonElement? root = null;
                if (File.Exists(_windowStatePath))
                {
                    var json = File.ReadAllText(_windowStatePath);
                    using var doc = JsonDocument.Parse(json);
                    root = doc.RootElement.Clone();
                }

                var workArea = SystemParameters.WorkArea;
                double? savedWidth = root.HasValue && root.Value.TryGetProperty("width", out var w) && w.TryGetDouble(out var widthVal) ? widthVal : null;
                double? savedHeight = root.HasValue && root.Value.TryGetProperty("height", out var h) && h.TryGetDouble(out var heightVal) ? heightVal : null;
                double? savedLeft = root.HasValue && root.Value.TryGetProperty("left", out var l) && l.ValueKind != JsonValueKind.Null && l.TryGetDouble(out var leftVal) ? leftVal : null;
                double? savedTop = root.HasValue && root.Value.TryGetProperty("top", out var topJson) && topJson.ValueKind != JsonValueKind.Null && topJson.TryGetDouble(out var topVal) ? topVal : null;

                if (savedWidth.HasValue && savedHeight.HasValue &&
                    savedLeft.HasValue && savedTop.HasValue)
                {
                    var restoreBounds = ResolveRestoreBounds(
                        savedLeft.Value,
                        savedTop.Value,
                        savedWidth.Value,
                        savedHeight.Value,
                        MinWidth,
                        MinHeight,
                        GetWorkAreas());

                    if (restoreBounds is Rect restored)
                    {
                        Left = restored.Left;
                        Top = restored.Top;
                        Width = restored.Width;
                        Height = restored.Height;
                    }
                }
                else
                {
                    if (savedWidth.HasValue && savedWidth.Value >= MinWidth)
                        Width = Math.Min(savedWidth.Value, workArea.Width);
                    if (savedHeight.HasValue && savedHeight.Value >= MinHeight)
                        Height = Math.Min(savedHeight.Value, workArea.Height);
                }
                if (root.HasValue && root.Value.TryGetProperty("fontSize", out var f) && f.GetDouble() is >= 8 and <= 24)
                    AppearanceService.SetFontSize(f.GetDouble());
                if (root.HasValue && root.Value.TryGetProperty("sidebarWidth", out var sw) && sw.GetDouble() is >= 80 and <= 800)
                    _sidebarWidth = sw.GetDouble();

                double brightness = AppearanceService.Brightness;
                double tintPosition = AppearanceService.TintPosition;
                double accentBrightness = AppearanceService.AccentBrightness;
                double accentTintPosition = AppearanceService.AccentTintPosition;
                if (root.HasValue && root.Value.TryGetProperty("brightness", out var b) && b.GetDouble() is >= 0 and <= 1)
                    brightness = b.GetDouble();
                else if (root.HasValue && root.Value.TryGetProperty("usesDarkPalette", out var udp))
                    brightness = udp.GetBoolean() ? 0.0 : 1.0;
                else if (root.HasValue && root.Value.TryGetProperty("isDark", out var d))
                    brightness = d.GetBoolean() ? 0.0 : 1.0;
                if (root.HasValue && root.Value.TryGetProperty("tintPosition", out var t) && t.GetDouble() is >= 0 and <= 1)
                    tintPosition = t.GetDouble();
                if (root.HasValue && root.Value.TryGetProperty("accentBrightness", out var ab) && ab.GetDouble() is >= 0 and <= 1)
                    accentBrightness = ab.GetDouble();
                if (root.HasValue && root.Value.TryGetProperty("accentTintPosition", out var at) && at.GetDouble() is >= 0 and <= 1)
                    accentTintPosition = at.GetDouble();
                AppearanceService.Apply(brightness, tintPosition);
                AppearanceService.ApplyAccent(accentBrightness, accentTintPosition);

                if (root.HasValue && root.Value.TryGetProperty("isAttached", out var ia))
                    _isAttached = ia.GetBoolean();

                string language = !string.IsNullOrWhiteSpace(setupPreferences.LanguageCode)
                    ? setupPreferences.LanguageCode
                    : AppState.GetLanguage();
                if (string.IsNullOrWhiteSpace(language) &&
                    root.HasValue &&
                    root.Value.TryGetProperty("language", out var lang) &&
                    !string.IsNullOrEmpty(lang.GetString()))
                {
                    language = lang.GetString()!;
                }
                if (string.IsNullOrWhiteSpace(language))
                    language = "en";
                LocalizationService.SetLanguage(language);

                bool voiceEnabled = setupPreferences.VoiceOutputEnabled;
                bool useLocalOnly = setupPreferences.UseLocalVoicesOnly;
                string? voiceName = setupPreferences.SelectedVoice;
                int voiceRate = setupPreferences.VoiceRate;

                if (root.HasValue && root.Value.TryGetProperty("voiceEnabled", out var ve))
                    voiceEnabled = ve.GetBoolean();
                if (root.HasValue && root.Value.TryGetProperty("useLocalOnly", out var ulo))
                    useLocalOnly = ulo.GetBoolean();
                if (root.HasValue && root.Value.TryGetProperty("voiceName", out var vn) && !string.IsNullOrEmpty(vn.GetString()))
                    voiceName = vn.GetString();
                if (root.HasValue && root.Value.TryGetProperty("voiceRate", out var vr) && vr.TryGetInt32(out var rate))
                    voiceRate = rate;

                _ttsService.SetVoiceEnabled(voiceEnabled, notify: false);
                _ttsService.SetUseLocalOnly(useLocalOnly, notify: false);
                if (!string.IsNullOrWhiteSpace(voiceName))
                    _ttsService.SetVoice(voiceName, notify: false);
                _ttsService.SetRate(voiceRate, notify: false);
            }
            catch (Exception ex) { AppLogger.Warn("MainWindow.LoadWindowState", "Failed to restore window state", ex); }

            UpdateTopmost();
        }

        private void SaveWindowSize()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_windowStatePath)!);
                if (_sidebarOpen && SidebarColumn.Width.Value > 0)
                    _sidebarWidth = SidebarColumn.Width.Value;

                var bounds = WindowState == WindowState.Normal
                    ? new Rect(Left, Top, Width, Height)
                    : RestoreBounds;

                var json = JsonSerializer.Serialize(new
                {
                    width = bounds.Width,
                    height = bounds.Height,
                    left = !double.IsNaN(bounds.Left) ? bounds.Left : (double?)null,
                    top = !double.IsNaN(bounds.Top) ? bounds.Top : (double?)null,
                    fontSize = AppearanceService.FontSize,
                    brightness = AppearanceService.Brightness,
                    usesDarkPalette = AppearanceService.UsesDarkPalette,
                    tintPosition = AppearanceService.TintPosition,
                    accentBrightness = AppearanceService.AccentBrightness,
                    accentTintPosition = AppearanceService.AccentTintPosition,
                    isAttached = TrayService?.IsAttachedToTray ?? _isAttached,
                    language = LocalizationService.CurrentCode,
                    voiceEnabled = _ttsService.VoiceEnabled,
                    useLocalOnly = _ttsService.UseLocalOnly,
                    voiceName = _ttsService.SelectedVoice,
                    voiceRate = _ttsService.Rate,
                    sidebarWidth = _sidebarWidth
                });
                File.WriteAllText(_windowStatePath, json);
            }
            catch (Exception ex) { AppLogger.Warn("MainWindow.SaveWindowSize", "Failed to persist window state", ex); }
        }

        private static Rect[] GetWorkAreas()
            => WinFormsScreen.AllScreens
                .Select(screen => screen.WorkingArea)
                .Select(rect => new Rect(rect.Left, rect.Top, rect.Width, rect.Height))
                .ToArray();

        internal static Rect? ResolveRestoreBounds(
            double left,
            double top,
            double width,
            double height,
            double minWidth,
            double minHeight,
            params Rect[] workAreas)
        {
            if (double.IsNaN(left) || double.IsNaN(top) || double.IsNaN(width) || double.IsNaN(height))
                return null;

            if (width < minWidth || height < minHeight || workAreas.Length == 0)
                return null;

            var requested = new Rect(left, top, width, height);
            var matchingArea = workAreas
                .Select(area => new { Area = area, Intersection = Rect.Intersect(area, requested) })
                .Where(x => !x.Intersection.IsEmpty)
                .OrderByDescending(x => x.Intersection.Width * x.Intersection.Height)
                .Select(x => x.Area)
                .FirstOrDefault();

            if (matchingArea.Width <= 0 || matchingArea.Height <= 0)
                return null;

            var resolvedWidth = Math.Min(width, matchingArea.Width);
            var resolvedHeight = Math.Min(height, matchingArea.Height);
            var resolvedLeft = Math.Min(Math.Max(left, matchingArea.Left), matchingArea.Right - resolvedWidth);
            var resolvedTop = Math.Min(Math.Max(top, matchingArea.Top), matchingArea.Bottom - resolvedHeight);

            return new Rect(resolvedLeft, resolvedTop, resolvedWidth, resolvedHeight);
        }
    }
}
