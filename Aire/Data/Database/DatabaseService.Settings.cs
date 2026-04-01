using System.Threading.Tasks;

namespace Aire.Data
{
    public partial class DatabaseService
    {
        /// <summary>
        /// Looks up one application setting by key from the shared settings table.
        /// </summary>
        /// <param name="key">Setting key to load.</param>
        /// <returns>The stored value, or <see langword="null"/> when the key does not exist.</returns>
        public async Task<string?> GetSettingAsync(string key)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "SELECT Value FROM Settings WHERE Key = @key";
            cmd.Parameters.AddWithValue("@key", key);
            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString();
        }

        /// <summary>
        /// Inserts or replaces one application setting value.
        /// </summary>
        /// <param name="key">Setting key to write.</param>
        /// <param name="value">Serialized setting value to persist.</param>
        public async Task SetSettingAsync(string key, string value)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO Settings (Key, Value, UpdatedAt)
                VALUES (@key, @value, CURRENT_TIMESTAMP)";
            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@value", value);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Records one file access decision for later auditing of tool activity.
        /// </summary>
        /// <param name="operation">High-level file operation, such as read, write, or delete.</param>
        /// <param name="path">Path the tool attempted to access.</param>
        /// <param name="allowed">Whether the access was allowed or blocked.</param>
        public async Task LogFileAccessAsync(string operation, string path, bool allowed)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO FileAccessLog (Operation, Path, Allowed, ApprovedAt)
                VALUES (@operation, @path, @allowed, CURRENT_TIMESTAMP)";
            cmd.Parameters.AddWithValue("@operation", operation);
            cmd.Parameters.AddWithValue("@path", path);
            cmd.Parameters.AddWithValue("@allowed", allowed ? 1 : 0);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
