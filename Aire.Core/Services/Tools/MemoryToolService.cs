using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Aire.Services.Tools.ToolHelpers;

namespace Aire.Services.Tools
{
    /// <summary>
    /// Handles remember, recall, and set_reminder tools.
    /// Memory is stored at %LOCALAPPDATA%\Aire\memory.json.
    /// </summary>
    public class MemoryToolService
    {
        private const string MemoryPrefix = "dpapi:";
        private static readonly byte[] MemoryEntropy = Encoding.UTF8.GetBytes("Aire-MemoryToolService-v1");
        private static readonly string _memoryFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aire", "memory.json");

        public ToolExecutionResult ExecuteRemember(ToolCallRequest request)
        {
            var key   = GetString(request, "key").Trim();
            var value = GetString(request, "value");
            if (string.IsNullOrWhiteSpace(key))
                return new ToolExecutionResult { TextResult = "Error: key is required." };

            try
            {
                var dict = LoadMemory();
                if (string.IsNullOrEmpty(value))
                {
                    bool removed = dict.Remove(key);
                    SaveMemory(dict);
                    return new ToolExecutionResult
                    {
                        TextResult = removed ? $"Deleted memory: {key}" : $"Key not found: {key}"
                    };
                }
                dict[key] = value;
                SaveMemory(dict);
                return new ToolExecutionResult { TextResult = $"Remembered: {key} = {value}" };
            }
            catch (Exception ex)
            {
                return new ToolExecutionResult { TextResult = $"Error: {ex.Message}" };
            }
        }

        public ToolExecutionResult ExecuteRecall(ToolCallRequest request)
        {
            var key = GetString(request, "key").Trim();
            try
            {
                var dict = LoadMemory();
                if (string.IsNullOrEmpty(key))
                {
                    if (dict.Count == 0)
                        return new ToolExecutionResult { TextResult = "No memories stored." };
                    var list = string.Join("\n", dict.Keys.Select(k => $"  \u2022 {k}"));
                    return new ToolExecutionResult { TextResult = $"Stored memory keys:\n{list}" };
                }
                return new ToolExecutionResult
                {
                    TextResult = dict.TryGetValue(key, out var val)
                        ? $"{key}: {val}"
                        : $"No memory found for key: {key}"
                };
            }
            catch (Exception ex)
            {
                return new ToolExecutionResult { TextResult = $"Error: {ex.Message}" };
            }
        }

        public ToolExecutionResult ExecuteSetReminder(ToolCallRequest request)
        {
            var message = GetString(request, "message");
            if (string.IsNullOrWhiteSpace(message))
                return new ToolExecutionResult { TextResult = "Error: message is required." };

            double delayMinutes = 1;
            if (request.Parameters.TryGetProperty("delay_minutes", out var dEl))
            {
                if (dEl.ValueKind == JsonValueKind.Number)
                    delayMinutes = Math.Max(0, dEl.GetDouble());
                else if (double.TryParse(dEl.GetString(), out double parsed))
                    delayMinutes = Math.Max(0, parsed);
            }

            var fireAt  = DateTime.Now.AddMinutes(delayMinutes);
            var delayMs = (int)(delayMinutes * 60 * 1000);

            _ = Task.Run(async () =>
            {
                if (delayMs > 0) await Task.Delay(delayMs);
                // Notification will be injected via IUserNotificationService in the future.
                // For now, just log the reminder.
                System.Diagnostics.Debug.WriteLine($"[Reminder] {message}");
            });

            return new ToolExecutionResult
            {
                TextResult = $"Reminder set for {fireAt:HH:mm} ({delayMinutes:F1} min from now): \"{message}\""
            };
        }

        private static Dictionary<string, string> LoadMemory()
        {
            try
            {
                if (!File.Exists(_memoryFilePath)) return new();
                var stored = File.ReadAllText(_memoryFilePath);
                var json = UnprotectStoredJson(stored);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();

                // Transparently migrate legacy plaintext storage once it is successfully read.
                if (!IsProtected(stored))
                    SaveMemory(dict);

                return dict;
            }
            catch { return new(); }
        }

        private static void SaveMemory(Dictionary<string, string> dict)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_memoryFilePath)!);
            var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_memoryFilePath, ProtectJson(json));
        }

        private static bool IsProtected(string value)
            => !string.IsNullOrEmpty(value) && value.StartsWith(MemoryPrefix, StringComparison.Ordinal);

        internal static string ProtectJson(string json)
        {
            if (string.IsNullOrEmpty(json) || IsProtected(json))
                return json;

            if (!OperatingSystem.IsWindows())
                return json;

            var data = Encoding.UTF8.GetBytes(json);
            var encrypted = ProtectedData.Protect(data, MemoryEntropy, DataProtectionScope.CurrentUser);
            return MemoryPrefix + Convert.ToBase64String(encrypted);
        }

        internal static string UnprotectStoredJson(string stored)
        {
            if (!IsProtected(stored))
                return stored;

            if (!OperatingSystem.IsWindows())
                return "{}";

            try
            {
                var encrypted = Convert.FromBase64String(stored[MemoryPrefix.Length..]);
                var data = ProtectedData.Unprotect(encrypted, MemoryEntropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                return "{}";
            }
        }
    }
}
