using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aire.Data;
using Aire.Services;

namespace Aire.Data
{
    public partial class DatabaseService
    {
        private async Task CreateTablesAsync()
        {
            var commands = new[]
            {
                @"CREATE TABLE IF NOT EXISTS Providers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Type TEXT NOT NULL,
                    ApiKey TEXT,
                    BaseUrl TEXT,
                    Model TEXT,
                    IsEnabled INTEGER DEFAULT 1,
                    Color TEXT DEFAULT '#007ACC',
                    TimeoutMinutes INTEGER NOT NULL DEFAULT 5,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                )",
                @"CREATE TABLE IF NOT EXISTS Settings (
                    Key TEXT PRIMARY KEY,
                    Value TEXT,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                )",
                @"CREATE TABLE IF NOT EXISTS Conversations (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProviderId INTEGER,
                    Title TEXT,
                    AssistantModeKey TEXT NOT NULL DEFAULT 'general',
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (ProviderId) REFERENCES Providers (Id)
                )",
                @"CREATE TABLE IF NOT EXISTS Messages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ConversationId INTEGER,
                    Role TEXT NOT NULL,
                    Content TEXT NOT NULL,
                    ImagePath TEXT,
                    AttachmentsJson TEXT,
                    Tokens INTEGER,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (ConversationId) REFERENCES Conversations (Id)
                )",
                @"CREATE TABLE IF NOT EXISTS FileAccessLog (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Operation TEXT NOT NULL,
                    Path TEXT NOT NULL,
                    Allowed INTEGER DEFAULT 0,
                    RequestedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    ApprovedAt DATETIME
                )",
                @"CREATE TABLE IF NOT EXISTS McpServers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Command TEXT NOT NULL,
                    Arguments TEXT DEFAULT '',
                    WorkingDirectory TEXT DEFAULT '',
                    EnvVarsJson TEXT DEFAULT '{}',
                    IsEnabled INTEGER DEFAULT 1,
                    SortOrder INTEGER DEFAULT 0,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                )",
                @"CREATE TABLE IF NOT EXISTS EmailAccounts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    DisplayName TEXT NOT NULL,
                    Provider TEXT NOT NULL,
                    ImapHost TEXT NOT NULL,
                    ImapPort INTEGER NOT NULL DEFAULT 993,
                    SmtpHost TEXT NOT NULL,
                    SmtpPort INTEGER NOT NULL DEFAULT 587,
                    Username TEXT NOT NULL,
                    EncryptedPassword TEXT DEFAULT '',
                    IsEnabled INTEGER DEFAULT 1,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                )"
            };

            foreach (var sql in commands)
            {
                using var command = _connection!.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync();
            }
        }

        private async Task MigrateMessagesAttachmentsJsonAsync()
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "ALTER TABLE Messages ADD COLUMN AttachmentsJson TEXT";
                await cmd.ExecuteNonQueryAsync();
            }
            catch { /* column already exists — SQLite does not support IF NOT EXISTS on ALTER TABLE */ }
        }

        private async Task MigrateEmailOAuthAsync()
        {
            foreach (var col in new[] { "UseOAuth INTEGER DEFAULT 0", "OAuthRefreshToken TEXT DEFAULT ''" })
            {
                try
                {
                    using var cmd = _connection!.CreateCommand();
                    cmd.CommandText = $"ALTER TABLE EmailAccounts ADD COLUMN {col}";
                    await cmd.ExecuteNonQueryAsync();
                }
                catch { /* column already exists — SQLite does not support IF NOT EXISTS on ALTER TABLE */ }
            }
        }

        private async Task MigrateEncryptEmailOAuthTokensAsync()
        {
            using var selectCmd = _connection!.CreateCommand();
            selectCmd.CommandText = "SELECT Id, OAuthRefreshToken FROM EmailAccounts WHERE OAuthRefreshToken IS NOT NULL AND OAuthRefreshToken != ''";

            var toUpdate = new List<(int Id, string Encrypted)>();
            using (var reader = await selectCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(0);
                    var raw = reader.GetString(1);
                    if (!SecureStorage.IsProtected(raw))
                        toUpdate.Add((id, SecureStorage.Protect(raw)!));
                }
            }

            foreach (var (id, encrypted) in toUpdate)
            {
                using var updateCmd = _connection.CreateCommand();
                updateCmd.CommandText = "UPDATE EmailAccounts SET OAuthRefreshToken = @token WHERE Id = @id";
                updateCmd.Parameters.AddWithValue("@token", encrypted);
                updateCmd.Parameters.AddWithValue("@id", id);
                await updateCmd.ExecuteNonQueryAsync();
            }
        }

        private async Task MigrateEncryptMcpEnvVarsAsync()
        {
            using var selectCmd = _connection!.CreateCommand();
            selectCmd.CommandText = "SELECT Id, EnvVarsJson FROM McpServers WHERE EnvVarsJson IS NOT NULL AND EnvVarsJson != ''";

            var toUpdate = new List<(int Id, string Encrypted)>();
            using (var reader = await selectCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(0);
                    var raw = reader.GetString(1);
                    if (!SecureStorage.IsProtected(raw))
                        toUpdate.Add((id, SecureStorage.Protect(raw)!));
                }
            }

            foreach (var (id, encrypted) in toUpdate)
            {
                using var updateCmd = _connection.CreateCommand();
                updateCmd.CommandText = "UPDATE McpServers SET EnvVarsJson = @env WHERE Id = @id";
                updateCmd.Parameters.AddWithValue("@env", encrypted);
                updateCmd.Parameters.AddWithValue("@id", id);
                await updateCmd.ExecuteNonQueryAsync();
            }
        }

        private async Task MigrateAddSortOrderAsync()
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "ALTER TABLE Providers ADD COLUMN SortOrder INTEGER DEFAULT 0";
                await cmd.ExecuteNonQueryAsync();
            }
            catch { }
        }

        private async Task MigrateProviderTypesAsync()
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                UPDATE Providers
                SET Type = 'Inception'
                WHERE Type = 'Mercury'";
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task MigrateEncryptApiKeysAsync()
        {
            using var selectCmd = _connection!.CreateCommand();
            selectCmd.CommandText = "SELECT Id, ApiKey FROM Providers WHERE ApiKey IS NOT NULL AND ApiKey != ''";
            var toUpdate = new List<(int Id, string Encrypted)>();
            using (var reader = await selectCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(0);
                    var raw = reader.GetString(1);
                    if (!SecureStorage.IsProtected(raw))
                        toUpdate.Add((id, SecureStorage.Protect(raw)!));
                }
            }

            foreach (var (id, encrypted) in toUpdate)
            {
                using var updateCmd = _connection.CreateCommand();
                updateCmd.CommandText = "UPDATE Providers SET ApiKey = @key WHERE Id = @id";
                updateCmd.Parameters.AddWithValue("@key", encrypted);
                updateCmd.Parameters.AddWithValue("@id", id);
                await updateCmd.ExecuteNonQueryAsync();
            }
        }

        private async Task MigrateClaudeSessionProvidersAsync()
        {
            using var selectCmd = _connection!.CreateCommand();
            selectCmd.CommandText = "SELECT Id, ApiKey FROM Providers WHERE Type = 'Anthropic' AND ApiKey IS NOT NULL AND ApiKey != ''";

            var idsToMigrate = new List<int>();
            using (var reader = await selectCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(0);
                    var apiKey = reader.GetString(1);
                    if (string.Equals(SecureStorage.Unprotect(apiKey), "claude.ai-session", StringComparison.Ordinal))
                        idsToMigrate.Add(id);
                }
            }

            foreach (var id in idsToMigrate)
            {
                using var updateCmd = _connection.CreateCommand();
                updateCmd.CommandText = @"
                    UPDATE Providers
                    SET Type = 'ClaudeWeb', BaseUrl = NULL
                    WHERE Id = @id";
                updateCmd.Parameters.AddWithValue("@id", id);
                await updateCmd.ExecuteNonQueryAsync();
            }
        }

        private async Task MigrateProviderBaseUrlsAsync()
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                UPDATE Providers
                SET BaseUrl = 'https://api.deepseek.com'
                WHERE Name LIKE '%DeepSeek%' AND Type != 'Ollama' AND (BaseUrl IS NULL OR BaseUrl = '')";
            await cmd.ExecuteNonQueryAsync();

            using var cmd2 = _connection!.CreateCommand();
            cmd2.CommandText = @"
                UPDATE Providers
                SET BaseUrl = 'https://api.inceptionlabs.ai'
                WHERE Name LIKE '%Mercury%' AND Type != 'Ollama' AND (BaseUrl IS NULL OR BaseUrl = '')";
            await cmd2.ExecuteNonQueryAsync();
        }

        private async Task MigrateProviderTimeoutsAsync()
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "ALTER TABLE Providers ADD COLUMN TimeoutMinutes INTEGER NOT NULL DEFAULT 5";
                await cmd.ExecuteNonQueryAsync();
            }
            catch { }
        }

        private async Task MigrateConversationAssistantModesAsync()
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "ALTER TABLE Conversations ADD COLUMN AssistantModeKey TEXT NOT NULL DEFAULT 'general'";
                await cmd.ExecuteNonQueryAsync();
            }
            catch { }

            using var normalizeCmd = _connection!.CreateCommand();
            normalizeCmd.CommandText = @"
                UPDATE Conversations
                SET AssistantModeKey = 'general'
                WHERE AssistantModeKey IS NULL OR trim(AssistantModeKey) = ''";
            await normalizeCmd.ExecuteNonQueryAsync();
        }

        private async Task MigrateConversationColorsAsync()
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "ALTER TABLE Conversations ADD COLUMN Color TEXT";
                await cmd.ExecuteNonQueryAsync();
            }
            catch { }

            using var normalizeCmd = _connection!.CreateCommand();
            normalizeCmd.CommandText = @"
                UPDATE Conversations
                SET Color = CASE (abs(Id) % 10)
                    WHEN 0 THEN '#E6B800'
                    WHEN 1 THEN '#2FBF71'
                    WHEN 2 THEN '#3B82F6'
                    WHEN 3 THEN '#EC4899'
                    WHEN 4 THEN '#F97316'
                    WHEN 5 THEN '#8B5CF6'
                    WHEN 6 THEN '#14B8A6'
                    WHEN 7 THEN '#EF4444'
                    WHEN 8 THEN '#06B6D4'
                    ELSE '#A3A948'
                END
                WHERE Color IS NULL OR trim(Color) = ''";
            await normalizeCmd.ExecuteNonQueryAsync();
        }

        private async Task MigrateRemoveLegacySwitchedProviderMessagesAsync()
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                DELETE FROM Messages
                WHERE Role = 'system'
                  AND Content = 'Switched to Codex'";
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task SeedDefaultProvidersAsync()
        {
            using var checkCmd = _connection!.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM Providers";
            var count = Convert.ToInt64(await checkCmd.ExecuteScalarAsync());
            if (count > 0) return;

            var defaultProviders = new[]
            {
                (Name: "OpenAI (ChatGPT)", Type: "OpenAI", Model: "gpt-4o", Color: "#10A37F", BaseUrl: (string?)null),
                (Name: "Anthropic API", Type: "Anthropic", Model: "claude-sonnet-4-5", Color: "#D4A059", BaseUrl: (string?)null),
                (Name: "Google AI (Gemini)", Type: "GoogleAI", Model: "gemini-2.0-flash", Color: "#4285F4", BaseUrl: (string?)null),
                (Name: "DeepSeek (OpenAI-compatible)", Type: "DeepSeek", Model: "deepseek-chat", Color: "#00B4D8", BaseUrl: "https://api.deepseek.com"),
                (Name: "Mistral AI (OpenAI-compatible)", Type: "Mistral", Model: "mistral-large-latest", Color: "#FF6D00", BaseUrl: "https://api.mistral.ai"),
                (Name: "Inception (OpenAI-compatible)", Type: "Inception", Model: "mercury-latest", Color: "#FF6B6B", BaseUrl: "https://api.inceptionlabs.ai"),
                (Name: "Ollama (Local AI)", Type: "Ollama", Model: "qwen2.5-coder:7b", Color: "#8A2BE2", BaseUrl: "http://localhost:11434")
            };

            foreach (var provider in defaultProviders)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Providers (Name, Type, Model, Color, BaseUrl, IsEnabled, TimeoutMinutes)
                    VALUES (@name, @type, @model, @color, @baseUrl, 1, @timeoutMinutes)";
                cmd.Parameters.AddWithValue("@name", provider.Name);
                cmd.Parameters.AddWithValue("@type", provider.Type);
                cmd.Parameters.AddWithValue("@model", provider.Model);
                cmd.Parameters.AddWithValue("@color", provider.Color);
                cmd.Parameters.AddWithValue("@baseUrl", (object?)provider.BaseUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@timeoutMinutes", Provider.DefaultTimeoutMinutes);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}
