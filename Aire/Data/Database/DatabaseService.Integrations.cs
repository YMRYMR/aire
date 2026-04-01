using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Aire.Services;
using Aire.Services.Email;
using Aire.Services.Mcp;

namespace Aire.Data
{
    public partial class DatabaseService
    {
        /// <summary>
        /// Loads all configured MCP servers, decrypting stored environment variables before returning them.
        /// </summary>
        /// <returns>Configured MCP servers ordered for display and startup.</returns>
        public async Task<List<McpServerConfig>> GetMcpServersAsync()
        {
            var results = new List<McpServerConfig>();
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Command, Arguments, WorkingDirectory, EnvVarsJson, IsEnabled, SortOrder FROM McpServers ORDER BY SortOrder, Name";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var envJson = reader.IsDBNull(5) ? "{}" : DecryptMcpEnvVarsJson(reader.GetString(5));
                Dictionary<string, string> envVars;
                try { envVars = JsonSerializer.Deserialize<Dictionary<string, string>>(envJson) ?? new(); }
                catch { envVars = new(); }

                results.Add(new McpServerConfig
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Command = reader.GetString(2),
                    Arguments = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    WorkingDirectory = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    EnvVars = envVars,
                    IsEnabled = reader.GetBoolean(6),
                    SortOrder = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                });
            }
            return results;
        }

        /// <summary>
        /// Inserts a new MCP server configuration and returns its generated database id.
        /// </summary>
        /// <param name="config">MCP server values to persist.</param>
        /// <returns>The SQLite row id assigned to the new server.</returns>
        public async Task<int> InsertMcpServerAsync(McpServerConfig config)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"INSERT INTO McpServers (Name, Command, Arguments, WorkingDirectory, EnvVarsJson, IsEnabled, SortOrder)
                                VALUES (@name, @cmd, @args, @wd, @env, @enabled, @order);
                                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@name", config.Name);
            cmd.Parameters.AddWithValue("@cmd", config.Command);
            cmd.Parameters.AddWithValue("@args", config.Arguments);
            cmd.Parameters.AddWithValue("@wd", config.WorkingDirectory);
            cmd.Parameters.AddWithValue("@env", ProtectMcpEnvVarsJson(config.EnvVars));
            cmd.Parameters.AddWithValue("@enabled", config.IsEnabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@order", config.SortOrder);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        /// <summary>
        /// Updates one existing MCP server configuration, including encrypted environment variables.
        /// </summary>
        /// <param name="config">Server values to write back to storage.</param>
        public async Task UpdateMcpServerAsync(McpServerConfig config)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"UPDATE McpServers SET Name=@name, Command=@cmd, Arguments=@args,
                                WorkingDirectory=@wd, EnvVarsJson=@env, IsEnabled=@enabled WHERE Id=@id";
            cmd.Parameters.AddWithValue("@name", config.Name);
            cmd.Parameters.AddWithValue("@cmd", config.Command);
            cmd.Parameters.AddWithValue("@args", config.Arguments);
            cmd.Parameters.AddWithValue("@wd", config.WorkingDirectory);
            cmd.Parameters.AddWithValue("@env", ProtectMcpEnvVarsJson(config.EnvVars));
            cmd.Parameters.AddWithValue("@enabled", config.IsEnabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", config.Id);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Deletes one MCP server configuration by id.
        /// </summary>
        /// <param name="id">Database id of the MCP server to remove.</param>
        public async Task DeleteMcpServerAsync(int id)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "DELETE FROM McpServers WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Serializes and encrypts the MCP environment-variable dictionary before it is written to SQLite.
        /// </summary>
        private static string ProtectMcpEnvVarsJson(Dictionary<string, string> envVars)
        {
            var json = JsonSerializer.Serialize(envVars);
            return SecureStorage.Protect(json) ?? string.Empty;
        }

        /// <summary>
        /// Decrypts MCP environment-variable storage and falls back to the raw value for legacy plaintext rows.
        /// </summary>
        private static string DecryptMcpEnvVarsJson(string storedValue)
            => SecureStorage.Unprotect(storedValue) ?? storedValue;

        /// <summary>
        /// Encrypts an optional secret string while preserving empty values as empty strings.
        /// </summary>
        private static string ProtectOptionalSecret(string value)
            => string.IsNullOrEmpty(value) ? string.Empty : (SecureStorage.Protect(value) ?? string.Empty);

        /// <summary>
        /// Loads configured email accounts from storage.
        /// Passwords and refresh tokens stay encrypted here; callers decide when to decrypt them.
        /// </summary>
        /// <returns>All configured email accounts ordered by id.</returns>
        public async Task<List<EmailAccount>> GetEmailAccountsAsync()
        {
            var results = new List<EmailAccount>();
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "SELECT Id, DisplayName, Provider, ImapHost, ImapPort, SmtpHost, SmtpPort, Username, EncryptedPassword, IsEnabled, UseOAuth, OAuthRefreshToken FROM EmailAccounts ORDER BY Id";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new EmailAccount
                {
                    Id = reader.GetInt32(0),
                    DisplayName = reader.GetString(1),
                    Provider = Enum.TryParse<EmailProvider>(reader.GetString(2), out var p) ? p : EmailProvider.Custom,
                    ImapHost = reader.GetString(3),
                    ImapPort = reader.GetInt32(4),
                    SmtpHost = reader.GetString(5),
                    SmtpPort = reader.GetInt32(6),
                    Username = reader.GetString(7),
                    EncryptedPassword = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    IsEnabled = reader.GetBoolean(9),
                    UseOAuth = !reader.IsDBNull(10) && reader.GetBoolean(10),
                    OAuthRefreshToken = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                });
            }
            return results;
        }

        /// <summary>
        /// Inserts a new email account and returns its generated database id.
        /// Sensitive values are encrypted before they are stored.
        /// </summary>
        /// <param name="account">Email account values to persist.</param>
        /// <returns>The SQLite row id assigned to the new account.</returns>
        public async Task<int> InsertEmailAccountAsync(EmailAccount account)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"INSERT INTO EmailAccounts (DisplayName, Provider, ImapHost, ImapPort, SmtpHost, SmtpPort, Username, EncryptedPassword, IsEnabled, UseOAuth, OAuthRefreshToken)
                                VALUES (@dn, @prov, @ih, @ip, @sh, @sp, @user, @pwd, @enabled, @useOAuth, @oauthToken);
                                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@dn", account.DisplayName);
            cmd.Parameters.AddWithValue("@prov", account.Provider.ToString());
            cmd.Parameters.AddWithValue("@ih", account.ImapHost);
            cmd.Parameters.AddWithValue("@ip", account.ImapPort);
            cmd.Parameters.AddWithValue("@sh", account.SmtpHost);
            cmd.Parameters.AddWithValue("@sp", account.SmtpPort);
            cmd.Parameters.AddWithValue("@user", account.Username);
            cmd.Parameters.AddWithValue("@pwd", SecureStorage.Protect(account.PlaintextPassword ?? account.EncryptedPassword) ?? string.Empty);
            cmd.Parameters.AddWithValue("@enabled", account.IsEnabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@useOAuth", account.UseOAuth ? 1 : 0);
            cmd.Parameters.AddWithValue("@oauthToken", ProtectOptionalSecret(account.OAuthRefreshToken));
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        /// <summary>
        /// Updates one stored email account, preserving encrypted secrets when no new plaintext value was provided.
        /// </summary>
        /// <param name="account">Email account values to write back to storage.</param>
        public async Task UpdateEmailAccountAsync(EmailAccount account)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"UPDATE EmailAccounts SET DisplayName=@dn, Provider=@prov, ImapHost=@ih, ImapPort=@ip,
                                SmtpHost=@sh, SmtpPort=@sp, Username=@user, EncryptedPassword=@pwd, IsEnabled=@enabled,
                                UseOAuth=@useOAuth, OAuthRefreshToken=@oauthToken WHERE Id=@id";
            cmd.Parameters.AddWithValue("@dn", account.DisplayName);
            cmd.Parameters.AddWithValue("@prov", account.Provider.ToString());
            cmd.Parameters.AddWithValue("@ih", account.ImapHost);
            cmd.Parameters.AddWithValue("@ip", account.ImapPort);
            cmd.Parameters.AddWithValue("@sh", account.SmtpHost);
            cmd.Parameters.AddWithValue("@sp", account.SmtpPort);
            cmd.Parameters.AddWithValue("@user", account.Username);
            var pwd = !string.IsNullOrEmpty(account.PlaintextPassword)
                ? SecureStorage.Protect(account.PlaintextPassword)
                : account.EncryptedPassword;
            cmd.Parameters.AddWithValue("@pwd", pwd ?? string.Empty);
            cmd.Parameters.AddWithValue("@enabled", account.IsEnabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@useOAuth", account.UseOAuth ? 1 : 0);
            cmd.Parameters.AddWithValue("@oauthToken", ProtectOptionalSecret(account.OAuthRefreshToken));
            cmd.Parameters.AddWithValue("@id", account.Id);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Deletes one stored email account by id.
        /// </summary>
        /// <param name="id">Database id of the email account to remove.</param>
        public async Task DeleteEmailAccountAsync(int id)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "DELETE FROM EmailAccounts WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
