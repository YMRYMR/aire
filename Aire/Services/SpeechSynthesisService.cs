using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using Windows.Media.SpeechSynthesis;

namespace Aire.Services
{
    /// <summary>
    /// Text-to-speech with dual backend:
    ///   1. Edge TTS (free Microsoft neural voices, online)
    ///   2. WinRT SpeechSynthesizer fallback (local voices, works offline)
    /// NAudio handles audio playback for both backends.
    /// </summary>
    public sealed class SpeechSynthesisService : IDisposable
    {
        private SpeechSynthesizer?        _synth;
        private IWavePlayer?              _wavePlayer;
        private CancellationTokenSource?  _cts;
        private bool _disposed;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Fired after playback finishes naturally (not when cancelled).</summary>
        public event Action? SpeakingCompleted;

        /// <summary>Fired when VoiceEnabled, UseLocalOnly, SelectedVoice, or Rate changes.</summary>
        public event Action? SettingsChanged;

        // ── State ─────────────────────────────────────────────────────────────

        public bool IsAvailable { get; private set; } = true;
        public bool IsSpeaking  { get; private set; }

        /// <summary>Neural online voices (Edge TTS).</summary>
        public IReadOnlyList<string> EdgeVoiceNames { get; } =
            EdgeTtsService.Voices.Select(v => v.Display).ToList();

        /// <summary>Local WinRT voices installed on this system.</summary>
        public IReadOnlyList<string> LocalVoiceNames { get; private set; } = Array.Empty<string>();

        /// <summary>Combined voice list for the settings dropdown.</summary>
        public IReadOnlyList<string> AvailableVoices { get; private set; } = Array.Empty<string>();

        private bool    _voiceEnabled;
        private bool    _useLocalOnly;
        private int     _rate;
        private string? _selectedVoice;

        public bool    VoiceEnabled  => _voiceEnabled;
        public bool    UseLocalOnly  => _useLocalOnly;
        public int     Rate          => _rate;
        public string? SelectedVoice => _selectedVoice;

        // ── Singleton accessor ────────────────────────────────────────────────

        /// <summary>
        /// The most recently constructed <see cref="SpeechSynthesisService"/>.
        /// Set automatically in the constructor so that consumers (e.g. SettingsWindow
        /// opened from App.xaml.cs without an injected reference) can always reach the
        /// shared instance without tight coupling.
        /// </summary>
        public static SpeechSynthesisService? Current { get; private set; }

        // ── Construction ──────────────────────────────────────────────────────

        public SpeechSynthesisService()
        {
            try
            {
                _synth = new SpeechSynthesizer();
                LocalVoiceNames = SpeechSynthesizer.AllVoices
                    .Select(v => v.DisplayName)
                    .ToList();
            }
            catch
            {
                // WinRT unavailable — Edge TTS still works when online
                LocalVoiceNames = Array.Empty<string>();
            }

            RebuildVoiceList();
            Current = this;
        }

        private void RebuildVoiceList()
        {
            var voices = new List<string>();
            if (!_useLocalOnly)
                voices.AddRange(EdgeVoiceNames);
            voices.AddRange(LocalVoiceNames);
            AvailableVoices = voices;
        }

        // ── Configuration ─────────────────────────────────────────────────────

        public void SetVoiceEnabled(bool enabled, bool notify = true)
        {
            _voiceEnabled = enabled;
            if (!enabled) StopSpeaking();
            if (notify) SettingsChanged?.Invoke();
        }

        public void SetUseLocalOnly(bool localOnly, bool notify = true)
        {
            _useLocalOnly = localOnly;
            RebuildVoiceList();
            if (notify) SettingsChanged?.Invoke();
        }

        public void SetVoice(string? voiceName, bool notify = true)
        {
            _selectedVoice = voiceName;
            // Pre-configure WinRT synthesizer if this is a local voice
            if (_synth != null && !string.IsNullOrEmpty(voiceName))
            {
                var voice = SpeechSynthesizer.AllVoices
                    .FirstOrDefault(v => v.DisplayName == voiceName);
                if (voice != null)
                    _synth.Voice = voice;
            }
            if (notify) SettingsChanged?.Invoke();
        }

        public void SetRate(int rate, bool notify = true)
        {
            _rate = Math.Clamp(rate, -10, 10);
            if (_synth != null)
                _synth.Options.SpeakingRate = Math.Pow(1.15, _rate);
            if (notify) SettingsChanged?.Invoke();
        }

        // ── Playback ──────────────────────────────────────────────────────────

        public void Speak(string text)
        {
            if (_disposed || !IsAvailable) return;
            if (string.IsNullOrWhiteSpace(text)) return;

            StopSpeaking();

            var cleaned = CleanForSpeech(text);
            if (string.IsNullOrWhiteSpace(cleaned)) return;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            IsSpeaking = true;
            _ = SpeakCoreAsync(cleaned, token);
        }

        public void StopSpeaking()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            var player = _wavePlayer;
            _wavePlayer = null;
            IsSpeaking  = false;

            if (player != null)
            {
                try { player.Stop(); }
                catch (Exception ex)
                {
                    AppLogger.Warn(nameof(SpeechSynthesisService) + ".StopSpeaking", "Failed to stop audio playback", ex);
                }

                try { player.Dispose(); }
                catch (Exception ex)
                {
                    AppLogger.Warn(nameof(SpeechSynthesisService) + ".StopSpeaking", "Failed to dispose audio playback", ex);
                }
            }
        }

        /// <summary>
        /// Speaks a short test phrase using the currently selected voice.
        /// Returns <c>true</c> on success, <c>false</c> if Edge TTS is unreachable
        /// and no local fallback is available.
        /// </summary>
        public async Task<bool> TestVoiceAsync()
        {
            const string testText = "Hello! This is a test of the selected voice.";
            var edgeVoiceId = GetEdgeVoiceId();

            if (!_useLocalOnly && edgeVoiceId != null)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var mp3 = await EdgeTtsService.SynthesizeAsync(testText, edgeVoiceId, 0, cts.Token);
                if (mp3 == null) return false;

                StopSpeaking();
                _cts = new CancellationTokenSource();
                IsSpeaking = true;
                _ = PlayMp3Async(mp3, _cts.Token).ContinueWith(_ =>
                {
                    if (!_cts.IsCancellationRequested)
                    {
                        IsSpeaking = false;
                        SpeakingCompleted?.Invoke();
                    }
                }, TaskScheduler.Default);
                return true;
            }

            if (_synth == null) return false;
            Speak(testText);
            return true;
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private async Task SpeakCoreAsync(string text, CancellationToken token)
        {
            try
            {
                var chunks = SplitIntoSpeechChunks(text);
                if (chunks.Count == 0)
                {
                    IsSpeaking = false;
                    return;
                }

                var edgeVoiceId = GetEdgeVoiceId();

                if (!_useLocalOnly && edgeVoiceId != null)
                {
                    foreach (var chunk in chunks)
                    {
                        token.ThrowIfCancellationRequested();

                        // Rate: -10..10 → -100%..+100% for Edge prosody
                        var mp3Bytes = await EdgeTtsService.SynthesizeAsync(
                            chunk, edgeVoiceId, _rate * 10, token);

                        if (mp3Bytes == null || token.IsCancellationRequested)
                            break;

                        await PlayMp3Async(mp3Bytes, token);
                    }

                    if (!token.IsCancellationRequested)
                    {
                        IsSpeaking = false;
                        SpeakingCompleted?.Invoke();
                    }
                    return;
                }

                if (token.IsCancellationRequested) { IsSpeaking = false; return; }
                if (_synth == null) { IsSpeaking = false; return; }

                foreach (var chunk in chunks)
                {
                    token.ThrowIfCancellationRequested();
                    await SpeakWinRtAsync(chunk, token);
                }

                if (!token.IsCancellationRequested)
                {
                    IsSpeaking = false;
                    SpeakingCompleted?.Invoke();
                }
            }
            catch (OperationCanceledException) { IsSpeaking = false; }
            catch                              { IsSpeaking = false; }
        }

        /// <summary>
        /// Returns the Edge TTS voice ID for the currently selected voice,
        /// or the first available Edge voice if none is selected.
        /// Returns <c>null</c> if a local WinRT voice is selected.
        /// </summary>
        internal string? GetEdgeVoiceId()
        {
            if (string.IsNullOrEmpty(_selectedVoice))
                return EdgeTtsService.Voices.Count > 0 ? EdgeTtsService.Voices[0].VoiceId : null;

            foreach (var (display, voiceId) in EdgeTtsService.Voices)
                if (display == _selectedVoice)
                    return voiceId;

            return null; // local WinRT voice
        }

        private Task PlayMp3Async(byte[] mp3Bytes, CancellationToken token)
        {
            var ms     = new MemoryStream(mp3Bytes);
            var reader = new Mp3FileReader(ms);
            var output = new WaveOutEvent();
            _wavePlayer = output;
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            output.PlaybackStopped += (_, _) =>
            {
                reader.Dispose();
                ms.Dispose();
                tcs.TrySetResult();
            };

            output.Init(reader);
            output.Play();
            if (token.CanBeCanceled)
                token.Register(() => tcs.TrySetCanceled(token));
            return tcs.Task;
        }

        private async Task SpeakWinRtAsync(string text, CancellationToken token)
        {
            var rtStream = await _synth!.SynthesizeTextToStreamAsync(text);

            if (token.IsCancellationRequested) { IsSpeaking = false; return; }

            var ms = new MemoryStream();
            using (var netStream = rtStream.AsStreamForRead())
                await netStream.CopyToAsync(ms, token);

            if (token.IsCancellationRequested) { IsSpeaking = false; ms.Dispose(); return; }

            ms.Position = 0;

            var reader = new WaveFileReader(ms);
            var output = new WaveOutEvent();
            _wavePlayer = output;
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            output.PlaybackStopped += (_, _) =>
            {
                reader.Dispose();
                ms.Dispose();
                tcs.TrySetResult();
            };

            output.Init(reader);
            output.Play();
            if (token.CanBeCanceled)
                token.Register(() => tcs.TrySetCanceled(token));
            await tcs.Task;
        }

        /// <summary>Strips markdown, URLs, and non-speech characters for natural TTS output.</summary>
        internal static string CleanForSpeech(string text)
        {
            // Drop fenced and inline code blocks entirely
            text = Regex.Replace(text, @"```[\s\S]*?```", " ", RegexOptions.Singleline);
            text = Regex.Replace(text, @"`[^`\n]+`",      " ");

            // Markdown links: keep label, discard URL
            text = Regex.Replace(text, @"\[([^\]]+)\]\([^\)]+\)", "$1");

            // Bare URLs (http/https/ftp and www.)
            text = Regex.Replace(text, @"https?://\S+", " ");
            text = Regex.Replace(text, @"www\.\S+",     " ");

            // Markdown formatting — strip markers, keep content
            text = Regex.Replace(text, @"\*\*(.+?)\*\*", "$1", RegexOptions.Singleline);
            text = Regex.Replace(text, @"__(.+?)__",      "$1", RegexOptions.Singleline);
            text = Regex.Replace(text, @"\*(.+?)\*",      "$1", RegexOptions.Singleline);
            text = Regex.Replace(text, @"_(.+?)_",        "$1", RegexOptions.Singleline);
            text = Regex.Replace(text, @"#{1,6}\s*",      "",   RegexOptions.Multiline);

            // List markers
            text = Regex.Replace(text, @"^\s*[-*+]\s+", "", RegexOptions.Multiline);
            text = Regex.Replace(text, @"^\s*\d+\.\s+",  "", RegexOptions.Multiline);

            // Common Unicode punctuation → readable equivalents or silence
            text = text.Replace("\u2026", "...")   // … → ...
                       .Replace("\u2014", ", ")    // — em dash
                       .Replace("\u2013", ", ")    // – en dash
                       .Replace("\u2019", "'")     // ' right single quote
                       .Replace("\u2018", "'")     // ' left single quote
                       .Replace("\u201C", "")      // " left double quote
                       .Replace("\u201D", "")      // " right double quote
                       .Replace("\u00AB", "")      // «
                       .Replace("\u00BB", "")      // »
                       .Replace("\u2022", "")      // • bullet
                       .Replace("\u00B7", "")      // · middle dot
                       .Replace("\u2192", " ")     // →
                       .Replace("\u2190", " ")     // ←
                       .Replace("\u2191", " ")     // ↑
                       .Replace("\u2193", " ")     // ↓
                       .Replace("\u2713", "")      // ✓
                       .Replace("\u2714", "")      // ✔
                       .Replace("\u2717", "")      // ✗
                       .Replace("\u2718", "")      // ✘
                       .Replace("\u00A9", "")      // ©
                       .Replace("\u00AE", "")      // ®
                       .Replace("\u2122", "")      // ™
                       .Replace("|", " ");

            // Strip emoji and non-Latin symbols, but keep accented European characters
            // (U+00C0–U+024F: Latin-1 Supplement + Latin Extended A/B — Spanish, French, German, etc.)
            text = Regex.Replace(text, @"[^\x00-\x7F\u00C0-\u024F]+", " ");

            // Paragraph breaks → natural pause
            text = Regex.Replace(text, @"\n{2,}", ". ");
            text = Regex.Replace(text, @"\n",     " ");

            // Collapse whitespace
            text = Regex.Replace(text, @"\s{2,}", " ");

            return text.Trim();
        }

        internal static IReadOnlyList<string> SplitIntoSpeechChunks(string text, int maxChunkLength = 280)
        {
            var cleaned = CleanForSpeech(text);
            if (string.IsNullOrWhiteSpace(cleaned))
                return Array.Empty<string>();

            var chunks = new List<string>();
            var parts = Regex.Split(cleaned, @"(?<=[\.\!\?\;\:])\s+")
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p));

            var current = string.Empty;
            foreach (var part in parts)
            {
                if (part.Length > maxChunkLength)
                {
                    FlushCurrentChunk(chunks, ref current);
                    SplitLongSegment(chunks, part, maxChunkLength);
                    continue;
                }

                if (string.IsNullOrEmpty(current))
                {
                    current = part;
                    continue;
                }

                if (current.Length + 1 + part.Length <= maxChunkLength)
                {
                    current += " " + part;
                }
                else
                {
                    chunks.Add(current);
                    current = part;
                }
            }

            FlushCurrentChunk(chunks, ref current);
            return chunks;
        }

        private static void FlushCurrentChunk(List<string> chunks, ref string current)
        {
            if (!string.IsNullOrWhiteSpace(current))
                chunks.Add(current.Trim());
            current = string.Empty;
        }

        private static void SplitLongSegment(List<string> chunks, string segment, int maxChunkLength)
        {
            var words = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var current = string.Empty;
            foreach (var word in words)
            {
                if (word.Length > maxChunkLength)
                {
                    FlushCurrentChunk(chunks, ref current);
                    for (var i = 0; i < word.Length; i += maxChunkLength)
                        chunks.Add(word.Substring(i, Math.Min(maxChunkLength, word.Length - i)));
                    continue;
                }

                if (string.IsNullOrEmpty(current))
                {
                    current = word;
                }
                else if (current.Length + 1 + word.Length <= maxChunkLength)
                {
                    current += " " + word;
                }
                else
                {
                    chunks.Add(current);
                    current = word;
                }
            }

            FlushCurrentChunk(chunks, ref current);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopSpeaking();
            _synth?.Dispose();
            _synth = null;
        }
    }
}
