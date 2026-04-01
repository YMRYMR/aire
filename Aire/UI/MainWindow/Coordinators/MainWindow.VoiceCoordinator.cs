using System;
using System.Threading.Tasks;
using System.Windows;

namespace Aire
{
    public partial class MainWindow
    {
        private VoiceCoordinator? _voiceCoordinator;
        private VoiceCoordinator VoiceFlow => _voiceCoordinator ??= new VoiceCoordinator(this);

        private sealed class VoiceCoordinator
        {
            private readonly MainWindow _owner;
            private bool _isMicActive;
            private bool _isMicPaused;
            private bool _lastMessageWasVoice;

            public VoiceCoordinator(MainWindow owner)
            {
                _owner = owner;
            }

            public bool ConsumeVoiceOrigin()
            {
                var wasVoice = _lastMessageWasVoice || _isMicActive;
                _lastMessageWasVoice = false;
                return wasVoice;
            }

            public async Task ToggleMicrophoneAsync()
            {
                if (_isMicActive)
                {
                    _isMicActive = false;
                    _isMicPaused = false;
                    _owner._speechService.StopListening();
                    _owner.SetMicButtonState(MicState.Idle);
                    return;
                }

                if (!_owner._speechService.ModelExists)
                {
                    await DownloadWhisperModelAndStartListeningAsync();
                    return;
                }

                StartListeningOrAlert();
            }

            public async Task DownloadWhisperModelAndStartListeningAsync()
            {
                _owner.SetMicButtonState(MicState.Downloading);

                _owner._speechService.DownloadProgress += _owner.OnWhisperDownloadProgress;
                var success = await _owner._speechService.DownloadModelAsync();
                _owner._speechService.DownloadProgress -= _owner.OnWhisperDownloadProgress;

                if (success)
                {
                    _owner.SetMicButtonState(MicState.Idle);
                    StartListeningOrAlert();
                    return;
                }

                _owner.SetMicButtonState(MicState.Idle);
                UI.ConfirmationDialog.ShowAlert(
                    _owner,
                    "Download Failed",
                    "Failed to download the Whisper model. Check your internet connection and try again.");
            }

            public void HandleSilenceTimeout()
            {
                if (_isMicActive && _owner.InputTextBox.Text.Trim().Length > 0)
                {
                    _lastMessageWasVoice = true;
                    _ = _owner.SendMessageAsync();
                }
            }

            public void HandleRecognizedCommand(string command)
            {
                switch (command)
                {
                    case "AIRE STOP":
                        _owner.EmergencyStopSession();
                        break;
                    case "AIRE PAUSE":
                        if (_isMicActive && !_isMicPaused)
                        {
                            _isMicPaused = true;
                            _owner._speechService.Pause();
                            _owner.SetMicButtonState(MicState.Paused);
                        }
                        break;
                    case "AIRE RESUME":
                        if (_isMicActive && _isMicPaused)
                        {
                            _isMicPaused = false;
                            _owner._speechService.Resume();
                            _owner.SetMicButtonState(MicState.Recording);
                        }
                        break;
                    case "AIRE SEND":
                        if (_owner.InputTextBox.Text.Trim().Length > 0)
                        {
                            _lastMessageWasVoice = true;
                            _owner._speechService.StopListening();
                            _isMicActive = false;
                            _isMicPaused = false;
                            _owner.SetMicButtonState(MicState.Idle);
                            _ = _owner.SendMessageAsync();
                        }
                        break;
                    case "AIRE CLEAR":
                        _owner.InputTextBox.Clear();
                        break;
                    case "AIRE CANCEL":
                        if (_isMicActive)
                        {
                            _owner._speechService.StopListening();
                            _isMicActive = false;
                            _isMicPaused = false;
                            _owner.SetMicButtonState(MicState.Idle);
                        }
                        break;
                    case "AIRE UNDO":
                        UndoLastUserMessage();
                        break;
                    case "AIRE CLOSE":
                    case "AIRE HIDE":
                        _owner.Hide();
                        break;
                    case "AIRE OPEN":
                    case "AIRE SHOW":
                        _owner.TrayService?.ShowMainWindow();
                        break;
                    case "AIRE NEW":
                        _ = _owner.DoClearConversationAsync();
                        break;
                    case "AIRE SETTINGS":
                        _ = _owner.ShowSettingsWindowAsync();
                        break;
                    case "AIRE YES":
                        ResolveVoiceApproval(approved: true);
                        break;
                    case "AIRE NO":
                        ResolveVoiceApproval(approved: false);
                        break;
                }
            }

            public void HandleRecognitionStopped()
            {
                _isMicActive = false;
                _isMicPaused = false;
                _owner.SetMicButtonState(MicState.Idle);
            }

            public void ToggleVoiceOutput()
            {
                _owner._ttsService.SetVoiceEnabled(!_owner._ttsService.VoiceEnabled);
                _owner.UpdateVoiceOutputButton();
                _owner.SaveWindowSize();
            }

            public void SpeakResponseIfNeeded(string text, bool wasVoice)
            {
                if (!_owner._ttsService.IsAvailable || !_owner._ttsService.VoiceEnabled)
                    return;

                _owner._ttsService.Speak(text);
            }

            private void StartListeningOrAlert()
            {
                _owner._ttsService.StopSpeaking();

                var startError = _owner._speechService.StartListening();
                if (startError == null)
                {
                    _isMicActive = true;
                    _isMicPaused = false;
                    _owner.SetMicButtonState(MicState.Recording);
                }
                else
                {
                    UI.ConfirmationDialog.ShowAlert(_owner, "Voice Input Unavailable", startError);
                }
            }

            private void UndoLastUserMessage()
            {
                if (!string.IsNullOrWhiteSpace(_owner.InputTextBox.Text))
                {
                    _owner.InputTextBox.Clear();
                    return;
                }

                for (int i = _owner._messages.Count - 1; i >= 0; i--)
                {
                    if (_owner._messages[i].Sender == "You")
                    {
                        var text = _owner._messages[i].Text;
                        if (i + 1 < _owner._messages.Count && _owner._messages[i + 1].Sender == "AI")
                            _owner._messages.RemoveAt(i + 1);
                        _owner._messages.RemoveAt(i);

                        for (int j = _owner._conversationHistory.Count - 1; j >= 0; j--)
                        {
                            if (_owner._conversationHistory[j].Role == "user")
                            {
                                _owner._conversationHistory.RemoveAt(j);
                                if (j < _owner._conversationHistory.Count && _owner._conversationHistory[j].Role == "assistant")
                                    _owner._conversationHistory.RemoveAt(j);
                                break;
                            }
                        }

                        _owner.InputTextBox.Text = text;
                        _owner.InputTextBox.CaretIndex = text.Length;
                        _owner.InputTextBox.Focus();
                        return;
                    }
                }
            }

            private void ResolveVoiceApproval(bool approved)
            {
                foreach (var msg in _owner._messages)
                {
                    if (msg.ApprovalTcs != null && !msg.ApprovalTcs.Task.IsCompleted)
                    {
                        msg.ApprovalTcs.TrySetResult(approved);
                        break;
                    }
                }
            }
        }
    }
}
