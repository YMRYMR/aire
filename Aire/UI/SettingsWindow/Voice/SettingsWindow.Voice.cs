using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private void PopulateVoiceSection()
        {
            if (_ttsService == null)
            {
                return;
            }

            _suppressAppearance = true;

            VoiceLocalOnlyCheckBox.IsChecked = _ttsService.UseLocalOnly;
            VoiceSpeedDisplay.Text = _ttsService.Rate.ToString();

            VoiceComboBox.Items.Clear();

            if (!_ttsService.UseLocalOnly && _ttsService.EdgeVoiceNames.Count > 0)
            {
                AddVoiceGroupHeader("── Online (Neural) ──");
                foreach (var voice in _ttsService.EdgeVoiceNames)
                {
                    AddVoiceItem(voice);
                }
            }

            if (_ttsService.LocalVoiceNames.Count > 0)
            {
                if (!_ttsService.UseLocalOnly && _ttsService.EdgeVoiceNames.Count > 0)
                {
                    AddVoiceGroupHeader("── Local ──");
                }

                foreach (var voice in _ttsService.LocalVoiceNames)
                {
                    AddVoiceItem(voice);
                }
            }

            _suppressAppearance = false;
        }

        private void AddVoiceGroupHeader(string label)
        {
            var tb = new TextBlock
            {
                Text = label,
                FontSize = 10,
                IsEnabled = false,
                Padding = new Thickness(4, 4, 4, 2),
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            VoiceComboBox.Items.Add(new ComboBoxItem
            {
                Content = tb,
                IsEnabled = false,
                Focusable = false,
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left,
            });
        }

        private void AddVoiceItem(string voice)
        {
            var item = new ComboBoxItem { Content = voice, Tag = voice };
            VoiceComboBox.Items.Add(item);
            if (voice == _ttsService!.SelectedVoice)
            {
                VoiceComboBox.SelectedItem = item;
            }
        }

        private void VoiceLocalOnly_Changed(object sender, RoutedEventArgs e)
        {
            if (_ttsService == null || _suppressAppearance)
            {
                return;
            }

            _ttsService.SetUseLocalOnly(VoiceLocalOnlyCheckBox.IsChecked == true);
            PopulateVoiceSection();
        }

        private async void TestVoiceButton_Click(object sender, RoutedEventArgs e)
        {
            if (_ttsService == null)
            {
                return;
            }

            VoiceTestErrorBorder.Visibility = Visibility.Collapsed;
            TestVoiceButton.IsEnabled = false;
            TestVoiceButton.Content = "\u2026";

            try
            {
                var ok = await _ttsService.TestVoiceAsync();
                if (!ok)
                {
                    VoiceTestErrorText.Text =
                        "Could not connect to the online voice service. " +
                        "Check your internet connection, or enable \"Use local voices only\" and pick a local voice.";
                    VoiceTestErrorBorder.Visibility = Visibility.Visible;
                }
            }
            finally
            {
                TestVoiceButton.IsEnabled = true;
                TestVoiceButton.Content = "\uD83D\uDD0A";
            }
        }

        private void DownloadVoicesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:speech",
                    UseShellExecute = true,
                });
            }
            catch { /* Shell URI launch fails silently if the OS blocks it */ }
        }

        private void VoiceSpeedDecrease_Click(object sender, RoutedEventArgs e)
        {
            if (_ttsService == null || _suppressAppearance)
            {
                return;
            }

            _ttsService.SetRate(_ttsService.Rate - 1);
            VoiceSpeedDisplay.Text = _ttsService.Rate.ToString();
        }

        private void VoiceSpeedIncrease_Click(object sender, RoutedEventArgs e)
        {
            if (_ttsService == null || _suppressAppearance)
            {
                return;
            }

            _ttsService.SetRate(_ttsService.Rate + 1);
            VoiceSpeedDisplay.Text = _ttsService.Rate.ToString();
        }

        private void VoiceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ttsService == null || _suppressAppearance)
            {
                return;
            }

            VoiceTestErrorBorder.Visibility = Visibility.Collapsed;
            var item = VoiceComboBox.SelectedItem as ComboBoxItem;
            if (item?.Tag is string voice)
            {
                _ttsService.SetVoice(voice);
            }
        }
    }
}
