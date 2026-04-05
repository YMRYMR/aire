using System;
using System.IO;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Microsoft.Data.Sqlite;

namespace Aire.Data
{
    /// <summary>
    /// Manages SQLite database operations for settings, providers, and conversation history.
    /// </summary>
    public partial class DatabaseService : IDisposable, IProviderRepository, IConversationRepository, ISettingsRepository, IDatabaseInitializer, IMcpConfigRepository, IEmailAccountRepository
    {
        private readonly string _databasePath;
        private SqliteConnection? _connection;
        private bool _disposed;

        /// <summary>
        /// Creates the database service using the default per-user database path.
        /// </summary>
        public DatabaseService() : this(GetDefaultPath()) { }

        /// <summary>
        /// Creates the database service using an explicit SQLite file path.
        /// </summary>
        /// <param name="databasePath">Absolute path to the SQLite database file.</param>
        public DatabaseService(string databasePath)
        {
            _databasePath = databasePath;
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        }

        /// <summary>
        /// Resolves the default SQLite path under the user's local application data directory.
        /// </summary>
        private static string GetDefaultPath()
        {
            var appData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (string.IsNullOrWhiteSpace(appData))
                appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appData, "Aire");
            Directory.CreateDirectory(appFolder);
            return Path.Combine(appFolder, "aire.db");
        }

        /// <summary>
        /// Opens the SQLite connection, applies migrations, and seeds default data when needed.
        /// </summary>
        public async Task InitializeAsync()
        {
            _connection = new SqliteConnection($"Data Source={_databasePath}");
            await _connection.OpenAsync();

            await CreateTablesAsync();
            await MigrateProviderTypesAsync();
            await MigrateProviderBaseUrlsAsync();
            await MigrateAddSortOrderAsync();
            await MigrateProviderTimeoutsAsync();
            await MigrateConversationAssistantModesAsync();
            await MigrateConversationColorsAsync();
            await MigrateEncryptApiKeysAsync();
            await MigrateClaudeSessionProvidersAsync();
            await MigrateMessagesAttachmentsJsonAsync();
            await MigrateEmailOAuthAsync();
            await MigrateEncryptEmailOAuthTokensAsync();
            await MigrateEncryptMcpEnvVarsAsync();
            await SeedDefaultProvidersAsync();
        }

        /// <summary>
        /// Closes the SQLite connection and releases database resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _connection?.Dispose();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
