using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Aire.Services
{
    /// <summary>
    /// Free Microsoft Edge neural TTS via the public WebSocket API.
    /// Returns MP3 bytes on success, or <c>null</c> if offline or synthesis fails.
    /// No API key required.
    /// </summary>
    public static class EdgeTtsService
    {
        private const string TrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
        private const string SecMsGecVersion    = "1-143.0.3650.75";
        private const string WssBase =
            "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1";

        /// <summary>
        /// Computes the time-based Sec-MS-GEC token required by the Edge TTS API.
        /// Algorithm: SHA256(roundedWindowsTicks + TrustedClientToken), uppercase hex.
        /// </summary>
        internal static string GenerateSecMsGec()
        {
            const long winEpochOffset = 11644473600L; // seconds from 1601-01-01 to 1970-01-01
            var unixSeconds   = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var winSeconds    = unixSeconds + winEpochOffset;
            var rounded       = winSeconds - (winSeconds % 300); // floor to 5-minute interval
            var ticks         = rounded * 10_000_000L;           // convert to 100-ns units
            var input         = $"{ticks}{TrustedClientToken}";
            var hash          = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hash); // uppercase in .NET 5+
        }

        internal static string BuildWssUrl()
        {
            var connectionId = Guid.NewGuid().ToString("N").ToUpper();
            var secMsGec     = GenerateSecMsGec();
            return $"{WssBase}?TrustedClientToken={TrustedClientToken}" +
                   $"&ConnectionId={connectionId}" +
                   $"&Sec-MS-GEC={secMsGec}" +
                   $"&Sec-MS-GEC-Version={SecMsGecVersion}";
        }

        /// <summary>Available Edge neural voices (display name, voice ShortName).</summary>
        public static readonly IReadOnlyList<(string Display, string VoiceId)> Voices =
            new List<(string, string)>
            {
                ("Aria  (US, Female)",       "en-US-AriaNeural"),
                ("Jenny (US, Female)",       "en-US-JennyNeural"),
                ("Michelle (US, Female)",    "en-US-MichelleNeural"),
                ("Ana (US, Female)",         "en-US-AnaNeural"),
                ("Guy (US, Male)",           "en-US-GuyNeural"),
                ("Eric (US, Male)",          "en-US-EricNeural"),
                ("Davis (US, Male)",         "en-US-DavisNeural"),
                ("Sonia (UK, Female)",       "en-GB-SoniaNeural"),
                ("Libby (UK, Female)",       "en-GB-LibbyNeural"),
                ("Ryan (UK, Male)",          "en-GB-RyanNeural"),
                ("Natasha (AU, Female)",     "en-AU-NatashaNeural"),
                ("William (AU, Male)",       "en-AU-WilliamNeural"),
                ("Clara (CA, Female)",       "en-CA-ClaraNeural"),
                ("Liam (CA, Male)",          "en-CA-LiamNeural"),
                ("Elvira (ES, Female)",      "es-ES-ElviraNeural"),
                ("Alvaro (ES, Male)",        "es-ES-AlvaroNeural"),
                ("Dalia (MX, Female)",       "es-MX-DaliaNeural"),
                ("Jorge (MX, Male)",         "es-MX-JorgeNeural"),
                ("Denise (FR, Female)",      "fr-FR-DeniseNeural"),
                ("Henri (FR, Male)",         "fr-FR-HenriNeural"),
                ("Katja (DE, Female)",       "de-DE-KatjaNeural"),
                ("Conrad (DE, Male)",        "de-DE-ConradNeural"),
                ("Elsa (IT, Female)",        "it-IT-ElsaNeural"),
                ("Diego (IT, Male)",         "it-IT-DiegoNeural"),
                ("Francisca (BR, Female)",   "pt-BR-FranciscaNeural"),
                ("Antonio (BR, Male)",       "pt-BR-AntonioNeural"),
                ("Nanami (JP, Female)",      "ja-JP-NanamiNeural"),
                ("Keita (JP, Male)",         "ja-JP-KeitaNeural"),
                ("Xiaoxiao (CN, Female)",    "zh-CN-XiaoxiaoNeural"),
                ("Yunxi (CN, Male)",         "zh-CN-YunxiNeural"),
            };

        /// <summary>
        /// Synthesises <paramref name="text"/> and returns MP3 bytes,
        /// or <c>null</c> if offline or any error occurs.
        /// </summary>
        /// <param name="text">Plain text (not SSML).</param>
        /// <param name="voiceId">Edge voice ShortName, e.g. "en-US-AriaNeural".</param>
        /// <param name="ratePercent">Speaking rate offset in percent (-100 … +100).</param>
        /// <param name="token">Cancellation token.</param>
        public static async Task<byte[]?> SynthesizeAsync(
            string text, string voiceId, int ratePercent, CancellationToken token)
        {
            try
            {
                using var ws = new ClientWebSocket();
                ws.Options.SetRequestHeader("Origin",
                    "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
                ws.Options.SetRequestHeader("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                    "(KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0");
                ws.Options.SetRequestHeader("Pragma",        "no-cache");
                ws.Options.SetRequestHeader("Cache-Control", "no-cache");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(TimeSpan.FromSeconds(30));

                await ws.ConnectAsync(new Uri(BuildWssUrl()), cts.Token);

                // 1. speech.config
                var ts = Timestamp();
                await SendTextAsync(ws,
                    $"X-Timestamp:{ts}\r\n" +
                    "Content-Type:application/json; charset=utf-8\r\n" +
                    "Path:speech.config\r\n\r\n" +
                    """{"context":{"synthesis":{"audio":{"metadataoptions":{"sentenceBoundaryEnabled":"false","wordBoundaryEnabled":"false"},"outputFormat":"audio-24khz-48kbitrate-mono-mp3"}}}}""",
                    cts.Token);

                // 2. SSML
                var requestId  = Guid.NewGuid().ToString("N");
                var rateStr    = ratePercent >= 0 ? $"+{ratePercent}%" : $"{ratePercent}%";
                var escapedText = System.Security.SecurityElement.Escape(text) ?? text;
                await SendTextAsync(ws,
                    $"X-RequestId:{requestId}\r\n" +
                    $"X-Timestamp:{ts}\r\n" +
                    "Content-Type:application/ssml+xml\r\n" +
                    "Path:ssml\r\n\r\n" +
                    $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>" +
                    $"<voice name='{voiceId}'><prosody rate='{rateStr}'>{escapedText}</prosody></voice></speak>",
                    cts.Token);

                // 3. Receive frames
                var audio = new MemoryStream();
                var buf   = new byte[65536];

                while (ws.State == WebSocketState.Open && !cts.Token.IsCancellationRequested)
                {
                    using var msg = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token);
                        msg.Write(buf, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close) break;

                    var msgBytes = msg.ToArray();

                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        // First 2 bytes (big-endian) = header length
                        if (msgBytes.Length < 2) continue;
                        int headerLen = (msgBytes[0] << 8) | msgBytes[1];
                        if (msgBytes.Length < 2 + headerLen) continue;

                        var header = Encoding.UTF8.GetString(msgBytes, 2, headerLen);
                        if (header.Contains("Path:audio"))
                            audio.Write(msgBytes, 2 + headerLen, msgBytes.Length - 2 - headerLen);
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        if (Encoding.UTF8.GetString(msgBytes).Contains("Path:turn.end"))
                            break;
                    }
                }

                var mp3 = audio.ToArray();
                return mp3.Length > 0 ? mp3 : null;
            }
            catch (OperationCanceledException) { return null; }
            catch { return null; }
        }

        private static string Timestamp() =>
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        private static Task SendTextAsync(ClientWebSocket ws, string msg, CancellationToken token)
        {
            var bytes = Encoding.UTF8.GetBytes(msg);
            return ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
        }
    }
}
