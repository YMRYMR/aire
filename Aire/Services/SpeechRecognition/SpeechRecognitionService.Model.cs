using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;

namespace Aire.Services
{
    public sealed partial class SpeechRecognitionService
    {
        private bool TryLoadFactory()
        {
            try
            {
                _factory?.Dispose();
                _factory = WhisperFactory.FromPath(ModelPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DownloadModelAsync(CancellationToken ct = default)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ModelPath)!);
                var tmp = ModelPath + ".tmp";

                using var src = await Whisper.net.Ggml.WhisperGgmlDownloader.Default.GetGgmlModelAsync(ModelType);
                using var dst = File.Create(tmp);

                var buf = new byte[65_536];
                long total = 0;
                int read;
                while ((read = await src.ReadAsync(buf, ct)) > 0)
                {
                    await dst.WriteAsync(buf.AsMemory(0, read), ct);
                    total += read;
                    DownloadProgress?.Invoke(Math.Min((double)total / ModelSizeApprox, 0.99));
                }

                dst.Close();
                File.Move(tmp, ModelPath, overwrite: true);
                DownloadProgress?.Invoke(1.0);
                return TryLoadFactory();
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
