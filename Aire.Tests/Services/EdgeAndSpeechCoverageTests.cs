using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aire.Services;
using NAudio.Wave;
using Xunit;

namespace Aire.Tests.Services;

public class EdgeAndSpeechCoverageTests
{
    [Fact]
    public void EdgeTtsService_HelperMethodsAndVoiceCatalog_Work()
    {
        string secMsGec = EdgeTtsService.GenerateSecMsGec();
        string wssUrl   = EdgeTtsService.BuildWssUrl();

        Assert.Matches("^[0-9A-F]{64}$", secMsGec);
        Assert.StartsWith("wss://speech.platform.bing.com/", wssUrl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TrustedClientToken=", wssUrl);
        Assert.Contains("Sec-MS-GEC=", wssUrl);
        Assert.True(EdgeTtsService.Voices.Count >= 10);
        Assert.Contains(EdgeTtsService.Voices, v => v.VoiceId == "en-US-AriaNeural");
    }

    [Fact]
    public void SpeechSynthesisService_SettingsAndCleaningHelpers_Work()
    {
        using var service = new SpeechSynthesisService();
        int changed = 0;
        service.SettingsChanged += () => changed++;

        service.SetVoiceEnabled(enabled: false);
        service.SetUseLocalOnly(localOnly: true);
        service.SetVoice("not-a-real-local-voice");
        service.SetRate(99);

        string cleaned = SpeechSynthesisService.CleanForSpeech(
            "# Header\nVisit [site](https://example.com) and `code`.\n- item 1\nEmoji ✓");
        string? edgeVoiceId = service.GetEdgeVoiceId();

        Assert.False(service.VoiceEnabled);
        Assert.True(service.UseLocalOnly);
        Assert.Equal(10, service.Rate);
        Assert.True(changed >= 4);
        Assert.DoesNotContain("https://", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("`code`", cleaned, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Header", cleaned);
        Assert.Contains("site", cleaned);
        Assert.Null(edgeVoiceId);
    }

    [Fact]
    public async Task SpeechRecognitionService_HelpersAndUnavailablePaths_Work()
    {
        using var service = new SpeechRecognitionService();

        var silentBuf = new byte[8];
        var pcm       = new byte[] { 1, 0, 255, 127, 0, 0, 0, 128 };

        double silent = SpeechRecognitionService.CalculateRms(silentBuf, silentBuf.Length);
        double loud   = SpeechRecognitionService.CalculateRms(pcm, pcm.Length);
        using var wav = SpeechRecognitionService.BuildWav(pcm, new WaveFormat(16000, 16, 1));

        Assert.Equal(0.0, silent);
        Assert.True(loud > 0.0);
        Assert.Equal(44 + pcm.Length, wav.Length);
        wav.Position = 0;
        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav.ToArray(), 0, 4));

        Assert.EndsWith(Path.Combine("Aire", "whisper-models", "ggml-base.bin"),
            SpeechRecognitionService.ModelPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("en", service.Language);

        service.Pause();
        service.Resume();
        service.StopListening();

        Assert.False(await service.DownloadModelAsync(new CancellationToken(canceled: true)));

        if (!service.IsAvailable)
        {
            string startResult = service.StartListening();
            Assert.False(string.IsNullOrWhiteSpace(startResult));
        }
    }
}
