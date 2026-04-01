using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Aire.Domain.Providers;
using Aire.Providers;
using Aire.Services;
using Aire.AppLayer.Abstractions;

namespace Aire.Services.Providers
{
    /// <summary>
    /// Default infrastructure adapter for installing the Codex CLI through npm.
    /// </summary>
    public sealed class CodexManagementClient : ICodexManagementClient
    {
        /// <inheritdoc />
        public Task<CodexCliStatus> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CodexProvider.GetCliStatus());

        /// <inheritdoc />
        public async Task InstallAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            if (CodexProvider.HasLaunchableCli())
            {
                progress?.Report("Codex CLI is already installed.");
                return;
            }

            var npmPath = FindNpm();
            if (npmPath == null)
                throw new InvalidOperationException("npm was not found. Install Node.js first.");

            progress?.Report("Installing Codex CLI with npm…");

            var psi = new ProcessStartInfo
            {
                FileName = npmPath,
                Arguments = "install -g @openai/codex",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            };

            using var process = new Process { StartInfo = psi };
            if (!process.Start())
                throw new InvalidOperationException("Failed to start npm.");

            using var _ = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch { /* best-effort process kill on cancellation */ }
            });

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                var message = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(message)
                    ? $"npm exited with code {process.ExitCode}."
                    : message.Trim());
            }

            progress?.Report("Codex CLI installed.");
        }

        private static string? FindNpm()
        {
            try
            {
                var wherePsi = new ProcessStartInfo("where.exe", "npm")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var wp = Process.Start(wherePsi);
                if (wp != null)
                {
                    string? line;
                    while ((line = wp.StandardOutput.ReadLine()?.Trim()) != null)
                    {
                        if (!string.IsNullOrEmpty(line) && File.Exists(line))
                        {
                            wp.WaitForExit(3000);
                            return line;
                        }
                    }

                    wp.WaitForExit(3000);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("CodexManagement.FindNpm", $"'where.exe npm' probe failed: {ex.Message}");
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var candidates = new[]
            {
                Path.Combine(programFiles, "nodejs", "npm.cmd"),
                Path.Combine(programFiles, "nodejs", "npm.exe"),
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }
    }
}
