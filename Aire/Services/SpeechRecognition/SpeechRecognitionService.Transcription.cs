using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Aire.Services
{
    public sealed partial class SpeechRecognitionService
    {
        /// <summary>
        /// Removes known Whisper boilerplate and duplicated whitespace from transcription output.
        /// </summary>
        /// <param name="text">Raw transcription text returned by Whisper.</param>
        /// <returns>Normalized text suitable for command detection and chat input.</returns>
        private static string StripWhisperArtifacts(string text)
        {
            var cleaned = WhisperArtifactRegex.Replace(text, " ");
            return Regex.Replace(cleaned, @"\s{2,}", " ").Trim();
        }

        /// <summary>
        /// Transcribes the current buffered PCM audio chunk and routes the result either to command handling
        /// or to the normal phrase pipeline.
        /// </summary>
        private async Task TranscribeAsync()
        {
            if (_factory == null || _disposed)
            {
                _transcribing = false;
                return;
            }

            var pcm = _pcmBuffer.ToArray();
            _pcmBuffer = new MemoryStream();
            _hasSpeech = false;

            try
            {
                if (pcm.Length < MinPcmBytes)
                {
                    _transcribing = false;
                    return;
                }

                using var processor = _factory.CreateBuilder()
                    .WithLanguage(Language)
                    .Build();

                using var wav = BuildWav(pcm, AudioFormat);
                var sb = new StringBuilder();
                await foreach (var seg in processor.ProcessAsync(wav))
                    sb.Append(seg.Text);

                var text = sb.ToString().Trim();

                if (string.IsNullOrEmpty(text))
                {
                    _transcribing = false;
                    return;
                }

                foreach (var blank in WhisperBlankPhrases)
                {
                    if (text.Equals(blank, StringComparison.OrdinalIgnoreCase))
                    {
                        _transcribing = false;
                        return;
                    }
                }

                text = StripWhisperArtifacts(text);
                if (string.IsNullOrEmpty(text))
                {
                    _transcribing = false;
                    return;
                }

                var normalized = text.TrimEnd('.', ',', '!', '?', '…', ' ').ToUpperInvariant();

                if (normalized.StartsWith("AIRE ") || normalized == "AIRE")
                {
                    _transcribing = false;
                    CommandRecognized?.Invoke(normalized);
                }
                else
                {
                    PhraseRecognized?.Invoke(text);
                    _pendingSend = true;
                    StartSendCountdown();
                }
            }
            catch
            {
                _transcribing = false;
            }
        }

        /// <summary>
        /// Stops listening and releases native Whisper resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                StopListening();
            }
            catch
            {
            }

            try
            {
                StopCountdown();
            }
            catch
            {
            }

            try
            {
                _factory?.Dispose();
            }
            catch
            {
            }

            try
            {
                _pcmBuffer.Dispose();
            }
            catch
            {
            }

            _countdownTimer = null;
            _activeElapsedHandler = null;
            _factory = null;
            _waveIn = null;
            GC.SuppressFinalize(this);
        }
    }
}
