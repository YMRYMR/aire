using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls.Primitives;
using Aire.Services;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        public void NavigateTo(string? tab)
        {
            if (tab == null)
            {
                return;
            }

            var item = tab switch
            {
                "providers" => (System.Windows.Controls.TabItem)TabProviders,
                "appearance" => TabAppearance,
                "voice" => TabVoice,
                "auto-accept" => TabAutoAccept,
                "connections" => TabConnections,
                _ => null
            };

            if (item != null)
            {
                MainTabControl.SelectedItem = item;
            }
        }

        private void LoadWindowState()
        {
            try
            {
                if (!File.Exists(StatePath))
                {
                    return;
                }

                var json = File.ReadAllText(StatePath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var workArea = SystemParameters.WorkArea;

                if (root.TryGetProperty("width", out var w) && w.GetDouble() >= MinWidth)
                {
                    Width = Math.Min(w.GetDouble(), workArea.Width);
                }

                if (root.TryGetProperty("height", out var h) && h.GetDouble() >= MinHeight)
                {
                    Height = Math.Min(h.GetDouble(), workArea.Height);
                }

                if (root.TryGetProperty("left", out var l) &&
                    l.ValueKind != JsonValueKind.Null &&
                    l.TryGetDouble(out var lv) && lv >= workArea.Left)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    Left = Math.Min(lv, workArea.Right - Width);
                }

                if (root.TryGetProperty("top", out var t) &&
                    t.ValueKind != JsonValueKind.Null &&
                    t.TryGetDouble(out var tv) && tv >= workArea.Top)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    Top = Math.Min(tv, workArea.Bottom - Height);
                }

                if (root.TryGetProperty("editPanelWidth", out var ep) &&
                    ep.TryGetDouble(out var epv) && epv >= 180)
                {
                    _savedEditPanelWidth = epv;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("SettingsWindow.LoadWindowState", "Failed to restore window state", ex);
            }
        }

        private void SaveWindowState()
        {
            try
            {
                var editWidth = EditPanelColumn?.ActualWidth;
                Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
                var json = JsonSerializer.Serialize(new
                {
                    left = !double.IsNaN(Left) ? Left : (double?)null,
                    top = !double.IsNaN(Top) ? Top : (double?)null,
                    width = Width,
                    height = Height,
                    editPanelWidth = editWidth is > 0 ? editWidth : (double?)null,
                });
                File.WriteAllText(StatePath, json);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("SettingsWindow.SaveWindowState", "Failed to persist window state", ex);
            }
        }

        private void ProvidersSplitter_DragCompleted(object sender, DragCompletedEventArgs e) => SaveWindowState();
    }
}
