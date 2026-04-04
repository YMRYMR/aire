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
        private readonly Func<CodexCliStatus> _getStatus;
        private readonly Func<string?> _findNpm;
        private readonly Func<string, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>> _runInstall;

        /// <summary>
        /// Creates the Codex CLI management client over the default filesystem and process helpers.
        /// </summary>
        public CodexManagementClient()
            : this(
                static () => CodexProvider.GetCliStatus(),
                FindNpm,
                RunInstallAsync)
        {
        }

        internal CodexManagementClient(
            Func<CodexCliStatus> getStatus,
            Func<string?> findNpm,
            Func<string, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>> runInstall)
        {
            _getStatus = getStatus;
            _findNpm = findNpm;
            _runInstall = runInstall;
        }

        /// <inheritdoc />
        public Task<CodexCliStatus> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_getStatus());

        /// <inheritdoc />
        public async Task InstallAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            if (_getStatus().IsInstalled)
            {
                progress?.Report("Codex CLI is already installed.");
                return;
            }

            var npmPath = _findNpm();
            if (npmPath == null)
                throw new InvalidOperationException("npm was not found. Install Node.js first.");

            progress?.Report("Installing Codex CLI with npm…");

            var (exitCode, stdout, stderr) = await _runInstall(npmPath, cancellationToken).ConfigureAwait(false);

            if (exitCode != 0)
            {
                var message = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(message)
                    ? $"npm exited with code {exitCode}."
                    : message.Trim());
            }

            progress?.Report("Codex CLI installed.");
        }

        internal static string? FindNpm()
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
                AppLogger.Warn("CodexManagement.FindNpm", $"'where.exe npm' probe failed: {ex.GetType().Name}");
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

        private static async Task<(int ExitCode, string StdOut, string StdErr)> RunInstallAsync(string npmPath, CancellationToken cancellationToken)
        {
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
                catch { }
            });

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            return (process.ExitCode, await stdoutTask.ConfigureAwait(false), await stderrTask.ConfigureAwait(false));
        }
    }
}
