using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using Whisper.net;
using Whisper.net.Ggml;

namespace Aire.Services
{
    /// <summary>
    /// Speech recognition using OpenAI Whisper (via Whisper.net).
    /// Records 16 kHz mono PCM with NAudio, detects silence, then transcribes
    /// the buffered audio locally — no API key required.
    ///
    /// On first use the ~150 MB "base" GGML model is downloaded to
    /// %LOCALAPPDATA%\Aire\whisper-models\ and reused on subsequent launches.
    /// </summary>
    public sealed partial class SpeechRecognitionService : IDisposable
    {
        // ── Model ─────────────────────────────────────────────────────────────

        public static readonly GgmlType ModelType = GgmlType.Base;

        /// <summary>Approximate compressed size of the base model for progress reporting.</summary>
        private const long ModelSizeApprox = 148_000_000L;

        public static string ModelPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aire", "whisper-models", "ggml-base.bin");

        // ── Audio settings ────────────────────────────────────────────────────

        /// <summary>Whisper requires 16 kHz, 16-bit, mono PCM.</summary>
        private static readonly WaveFormat AudioFormat = new(sampleRate: 16_000, channels: 1);

        /// <summary>RMS level above which incoming audio is considered speech.</summary>
        private const double SpeechRmsThreshold = 500;

        /// <summary>Seconds of silence after speech before transcribing.</summary>
        private const int SilenceSeconds = 3;

        // ── Internals ─────────────────────────────────────────────────────────

        private WaveInEvent?         _waveIn;
        private MemoryStream         _pcmBuffer = new();
        private WhisperFactory?      _factory;
        private System.Timers.Timer? _countdownTimer;
        private System.Timers.ElapsedEventHandler? _activeElapsedHandler;
        private int                  _silenceCountdown;
        private bool                 _hasSpeech;
        private bool                 _isPaused;
        private bool                 _transcribing;
        private bool                 _pendingSend;   // true while the post-transcription send countdown is running
        private bool                 _disposed;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Complete transcription of one phrase, fired after transcription (before the send countdown).</summary>
        public event Action<string>? PhraseRecognized;

        /// <summary>An AIRE voice command (upper-case, e.g. "AIRE SEND").</summary>
        public event Action<string>? CommandRecognized;

        /// <summary>Fires with remaining seconds (3..1) during both the silence and the send countdowns, then 0 to clear the display.</summary>
        public event Action<int>?    CountdownTick;

        /// <summary>Fired after the send countdown expires — caller should auto-send.</summary>
        public event Action?         SilenceTimeout;

        /// <summary>Fired when listening stops.</summary>
        public event Action?         Stopped;

        /// <summary>Model download progress 0.0 – 1.0.</summary>
        public event Action<double>? DownloadProgress;

        // ── State ─────────────────────────────────────────────────────────────

        public bool IsListening  { get; private set; }
        public bool IsPaused     => _isPaused;
        public bool IsPendingSend => _pendingSend;
        public bool ModelExists  => File.Exists(ModelPath);
        public bool HasMic       => WaveIn.DeviceCount > 0;
        public bool IsAvailable  => ModelExists && HasMic && _factory != null;

        public string? UnavailableReason =>
            !HasMic ? "No microphone found on this system." : null;

        /// <summary>
        /// ISO 639-1 language code passed to Whisper (e.g. "en", "es", "fr").
        /// Whisper will still understand audio in other languages, but strongly prefers this one,
        /// preventing it from randomly switching to Japanese/Chinese/etc.
        /// Defaults to "en"; update from <see cref="LocalizationService.CurrentCode"/> at runtime.
        /// </summary>
        public string Language { get; set; } = "en";

        // ── Construction ──────────────────────────────────────────────────────

        public SpeechRecognitionService()
        {
            if (ModelExists)
                TryLoadFactory();
        }

        // ── Transcription ─────────────────────────────────────────────────────

        // Minimum audio duration to bother transcribing (0.5 s at 16 kHz, 16-bit, mono)
        private const int MinPcmBytes = 16_000;

        // Whisper outputs these strings when it receives silence or very short audio.
        private static readonly string[] WhisperBlankPhrases =
        {
            "[BLANK_AUDIO]", "[ Blank_Audio ]", "(silence)", "[ silence ]",
            "[silence]", "(Silence)", "[ Silence ]", "[ BLANK_AUDIO ]",
        };

        // Removes Whisper hallucination artifacts: [Música], [Sighs], [Pause],
        // (speaking in foreign language), (Risas), (applause), etc.
        // Square-bracket tokens are ALWAYS Whisper markers, never real speech.
        // Parenthesised tokens likewise indicate stage directions, not transcribed words.
        private static readonly System.Text.RegularExpressions.Regex WhisperArtifactRegex = new(
            @"\[[^\]]*\]|\([^)]*\)",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    }
}
