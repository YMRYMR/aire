using System;
using System.Threading.Tasks;
using System.Windows;
using Aire.Services;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace Aire
{
    public partial class MainWindow
    {
        private void MicButton_Click(object sender, RoutedEventArgs e)
        {
            _ = VoiceFlow.ToggleMicrophoneAsync();
        }

        private void OnWhisperDownloadProgress(double progress)
        {
            Dispatcher.Invoke(() =>
            {
                MicButton.Content = $"{(int)(progress * 100)}%";
                MicButton.FontSize = 10;
            });
        }

        private void OnPhraseRecognized(string text)
        {
            Dispatcher.Invoke(() =>
            {
                var existing = InputTextBox.Text;
                var separator = existing.Length > 0 && !existing.EndsWith(' ') ? " " : string.Empty;
                InputTextBox.AppendText(separator + text);
                InputTextBox.CaretIndex = InputTextBox.Text.Length;
                InputTextBox.Focus();
            });
        }

        private void OnCountdownTick(int seconds)
        {
            Dispatcher.Invoke(() =>
            {
                if (seconds <= 0)
                {
                    MicButton.Content = "\u23F9";
                    MicButton.FontSize = 16;
                }
                else
                {
                    MicButton.Content = seconds.ToString();
                    MicButton.FontSize = 20;
                }
            });
        }

        private void OnSilenceTimeout()
        {
            Dispatcher.Invoke(() =>
            {
                MicButton.Content = "\u23F9";
                MicButton.FontSize = 16;
                VoiceFlow.HandleSilenceTimeout();
            });
        }

        private void OnCommandRecognized(string command)
        {
            Dispatcher.Invoke(() => VoiceFlow.HandleRecognizedCommand(command));
        }

        private void OnRecognitionStopped()
        {
            Dispatcher.Invoke(VoiceFlow.HandleRecognitionStopped);
        }

        private enum MicState { Idle, Recording, Paused, Downloading }

        private void SetMicButtonState(MicState state)
        {
            switch (state)
            {
                case MicState.Recording:
                    MicButton.Content = "\u23F9";
                    MicButton.FontSize = 16;
                    MicButton.Background = new SolidColorBrush(Color.FromRgb(0x7A, 0x1A, 0x1A));
                    MicButton.ToolTip = LocalizationService.S("tooltip.stopRecording", "Stop recording  (say 'AIRE PAUSE' to pause)");
                    MicButton.IsEnabled = true;
                    break;
                case MicState.Paused:
                    MicButton.Content = "\u23F8";
                    MicButton.FontSize = 16;
                    MicButton.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x2A, 0x3A));
                    MicButton.ToolTip = LocalizationService.S("tooltip.voicePaused", "Voice paused  (say 'AIRE RESUME' to continue)");
                    MicButton.IsEnabled = true;
                    break;
                case MicState.Downloading:
                    MicButton.Content = "…";
                    MicButton.FontSize = 16;
                    MicButton.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x2A, 0x1A));
                    MicButton.ToolTip = LocalizationService.S("tooltip.downloadingWhisper", "Downloading Whisper model…");
                    MicButton.IsEnabled = false;
                    break;
                default:
                    MicButton.Content = "\uD83C\uDFA4";
                    MicButton.FontSize = 13;
                    MicButton.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
                    MicButton.ToolTip = _speechService.ModelExists
                        ? LocalizationService.S("tooltip.mic", "Start voice input")
                        : LocalizationService.S("tooltip.downloadWhisper", "Click to download Whisper model (~150 MB)");
                    MicButton.IsEnabled = _speechService.HasMic;
                    break;
            }
        }

        private void VoiceOutputButton_Click(object sender, RoutedEventArgs e)
        {
            VoiceFlow.ToggleVoiceOutput();
        }

        private void UpdateVoiceOutputButton()
        {
            if (_ttsService.VoiceEnabled)
            {
                VoiceOutputButton.Content = "\uD83D\uDD0A";
                VoiceOutputButton.ToolTip = LocalizationService.S("tooltip.voiceOn", "Voice output on — click to disable");
            }
            else
            {
                VoiceOutputButton.Content = "\uD83D\uDD07";
                VoiceOutputButton.ToolTip = LocalizationService.S("tooltip.voiceOff", "Voice output off — click to enable");
            }
        }

        private void SpeakResponseIfNeeded(string text, bool wasVoice)
        {
            VoiceFlow.SpeakResponseIfNeeded(text, wasVoice);
        }

        private void OnTtsSpeakingCompleted()
        {
        }

        internal Task<bool> TryEnableStartupVoiceInputAsync()
            => VoiceFlow.TryStartAccessibilityVoiceInputAsync();
    }
}
