namespace Aire.Services
{
    public sealed partial class SpeechRecognitionService
    {
        private void StartSilenceCountdown()
        {
            if (_countdownTimer != null || _isPaused) return;
            _silenceCountdown = SilenceSeconds;
            CountdownTick?.Invoke(_silenceCountdown);
            _activeElapsedHandler = OnSilenceElapsed;
            _countdownTimer = new System.Timers.Timer(1000) { AutoReset = true };
            _countdownTimer.Elapsed += _activeElapsedHandler;
            _countdownTimer.Start();
        }

        private void OnSilenceElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            _silenceCountdown--;
            if (_silenceCountdown <= 0)
            {
                StopCountdown();
                _transcribing = true;
                _ = TranscribeAsync();
            }
            else
            {
                CountdownTick?.Invoke(_silenceCountdown);
            }
        }

        private void StartSendCountdown()
        {
            if (_countdownTimer != null) return;
            _silenceCountdown = SilenceSeconds;
            CountdownTick?.Invoke(_silenceCountdown);
            _activeElapsedHandler = OnSendElapsed;
            _countdownTimer = new System.Timers.Timer(1000) { AutoReset = true };
            _countdownTimer.Elapsed += _activeElapsedHandler;
            _countdownTimer.Start();
        }

        private void OnSendElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            _silenceCountdown--;
            if (_silenceCountdown <= 0)
            {
                StopCountdown();
                SilenceTimeout?.Invoke();
            }
            else
            {
                CountdownTick?.Invoke(_silenceCountdown);
            }
        }

        private void StopCountdown()
        {
            if (_countdownTimer == null) return;
            _countdownTimer.Stop();
            if (_activeElapsedHandler != null)
                _countdownTimer.Elapsed -= _activeElapsedHandler;
            _activeElapsedHandler = null;
            _countdownTimer.Dispose();
            _countdownTimer = null;
            _pendingSend = false;
            _transcribing = false;
            CountdownTick?.Invoke(0);
        }
    }
}
