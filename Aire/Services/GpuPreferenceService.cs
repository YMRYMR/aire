using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace Aire.Services
{
    /// <summary>
    /// Applies best-effort GPU preference hints for hybrid-GPU Windows systems.
    /// Windows remains in control, but these hints strongly prefer the
    /// high-performance GPU for Aire and its WebView2 child processes.
    /// </summary>
    internal static class GpuPreferenceService
    {
        private const string UserGpuPreferencesKey = @"Software\Microsoft\DirectX\UserGpuPreferences";
        private const string HighPerformanceValue = "GpuPreference=2;";

        public static void ApplyHighPerformancePreference()
        {
            TrySetWindowsGraphicsPreference();
            TrySetWebView2HighPerformanceHint();
        }

        private static void TrySetWindowsGraphicsPreference()
        {
            try
            {
                var processPath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(processPath))
                    return;

                using var key = Registry.CurrentUser.CreateSubKey(UserGpuPreferencesKey);
                var currentValue = key?.GetValue(processPath) as string;
                if (!string.Equals(currentValue, HighPerformanceValue, StringComparison.Ordinal))
                    key?.SetValue(processPath, HighPerformanceValue, RegistryValueKind.String);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not set Windows GPU preference: {ex.Message}");
            }
        }

        private static void TrySetWebView2HighPerformanceHint()
        {
            try
            {
                const string envKey = "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS";
                const string highPerfArg = "--force_high_performance_gpu";

                var existing = Environment.GetEnvironmentVariable(envKey);
                if (string.IsNullOrWhiteSpace(existing))
                {
                    Environment.SetEnvironmentVariable(envKey, highPerfArg);
                }
                else if (!existing.Contains(highPerfArg, StringComparison.OrdinalIgnoreCase))
                {
                    Environment.SetEnvironmentVariable(envKey, $"{existing} {highPerfArg}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not set WebView2 GPU hint: {ex.Message}");
            }
        }
    }
}
