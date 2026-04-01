using System;
using System.IO;
using NAudio.Wave;

namespace Aire.Services
{
    public sealed partial class SpeechRecognitionService
    {
        public string? StartListening()
        {
            if (_disposed) return "Service is disposed.";
            if (!IsAvailable) return UnavailableReason ?? "Unavailable.";
            if (IsListening) return null;

            _pcmBuffer = new MemoryStream();
            _hasSpeech = false;
            _transcribing = false;
            _pendingSend = false;

            _waveIn = new WaveInEvent { WaveFormat = AudioFormat, BufferMilliseconds = 100 };
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;

            try
            {
                _waveIn.StartRecording();
                IsListening = true;
                _isPaused = false;
                return null;
            }
            catch (Exception ex)
            {
                CleanupWaveIn();
                return ex.Message;
            }
        }

        public void StopListening()
        {
            if (!IsListening) return;
            IsListening = false;
            StopCountdown();
            CleanupWaveIn();
            Stopped?.Invoke();
        }

        public void Pause()
        {
            if (!IsListening) return;
            _isPaused = true;
            StopCountdown();
        }

        public void Resume() => _isPaused = false;

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_isPaused) return;

            if (_pendingSend)
            {
                var pendingRms = CalculateRms(e.Buffer, e.BytesRecorded);
                if (pendingRms > SpeechRmsThreshold)
                {
                    StopCountdown();
                    _pcmBuffer = new MemoryStream();
                    _pcmBuffer.Write(e.Buffer, 0, e.BytesRecorded);
                    _hasSpeech = true;
                }
                return;
            }

            if (_transcribing) return;

            _pcmBuffer.Write(e.Buffer, 0, e.BytesRecorded);

            var rms = CalculateRms(e.Buffer, e.BytesRecorded);
            if (rms > SpeechRmsThreshold)
            {
                _hasSpeech = true;
                if (_countdownTimer != null) StopCountdown();
            }
            else if (_hasSpeech && _countdownTimer == null && !_isPaused)
            {
                StartSilenceCountdown();
            }
        }

        internal static double CalculateRms(byte[] buf, int count)
        {
            if (count < 2) return 0;
            double sum = 0;
            for (int i = 0; i < count - 1; i += 2)
            {
                short s = (short)(buf[i] | (buf[i + 1] << 8));
                sum += (double)s * s;
            }
            return Math.Sqrt(sum / (count / 2));
        }

        internal static MemoryStream BuildWav(byte[] pcm, WaveFormat fmt)
        {
            var ms = new MemoryStream(44 + pcm.Length);
            ms.Write("RIFF"u8);
            ms.Write(BitConverter.GetBytes(36 + pcm.Length));
            ms.Write("WAVE"u8);
            ms.Write("fmt "u8);
            ms.Write(BitConverter.GetBytes(16));
            ms.Write(BitConverter.GetBytes((short)1));
            ms.Write(BitConverter.GetBytes((short)fmt.Channels));
            ms.Write(BitConverter.GetBytes(fmt.SampleRate));
            ms.Write(BitConverter.GetBytes(fmt.AverageBytesPerSecond));
            ms.Write(BitConverter.GetBytes((short)fmt.BlockAlign));
            ms.Write(BitConverter.GetBytes((short)fmt.BitsPerSample));
            ms.Write("data"u8);
            ms.Write(BitConverter.GetBytes(pcm.Length));
            ms.Write(pcm);
            ms.Position = 0;
            return ms;
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (!IsListening) return;
            IsListening = false;
            StopCountdown();
            Stopped?.Invoke();
        }

        private void CleanupWaveIn()
        {
            if (_waveIn == null) return;
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            try { _waveIn.StopRecording(); } catch { }
            _waveIn.Dispose();
            _waveIn = null;
        }
    }
}
