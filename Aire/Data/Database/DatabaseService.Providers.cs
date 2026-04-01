using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aire.Data;
using Aire.Services;

namespace Aire.Data
{
    public partial class DatabaseService
    {
        /// <summary>
        /// Loads all configured providers ordered for UI display.
        /// Decrypts secrets before returning the in-memory provider objects.
        /// </summary>
        /// <returns>Configured providers ordered by sort order and name.</returns>
        public async Task<List<Provider>> GetProvidersAsync()
        {
            var providers = new List<Provider>();
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Type, ApiKey, BaseUrl, Model, IsEnabled, Color, TimeoutMinutes, SortOrder FROM Providers ORDER BY SortOrder, Name";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                providers.Add(new Provider
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Type = reader.GetString(2),
                    ApiKey = reader.IsDBNull(3) ? null : SecureStorage.Unprotect(reader.GetString(3)),
                    BaseUrl = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Model = reader.GetString(5),
                    IsEnabled = reader.GetBoolean(6),
                    Color = reader.GetString(7),
                    TimeoutMinutes = reader.IsDBNull(8) ? Provider.DefaultTimeoutMinutes : reader.GetInt32(8),
                    SortOrder = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                });
            }
            return providers;
        }

        /// <summary>
        /// Persists edits to an existing provider, encrypting sensitive fields before writing them to SQLite.
        /// </summary>
        /// <param name="provider">Provider values to write back to storage.</param>
        public async Task UpdateProviderAsync(Provider provider)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                UPDATE Providers
                SET Name = @name, Type = @type, ApiKey = @apiKey, BaseUrl = @baseUrl, Model = @model, IsEnabled = @isEnabled, Color = @color, TimeoutMinutes = @timeoutMinutes
                WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", provider.Id);
            cmd.Parameters.AddWithValue("@name", provider.Name);
            cmd.Parameters.AddWithValue("@type", provider.Type);
            cmd.Parameters.AddWithValue("@apiKey", (object?)SecureStorage.Protect(provider.ApiKey) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@baseUrl", (object?)provider.BaseUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@model", provider.Model);
            cmd.Parameters.AddWithValue("@isEnabled", provider.IsEnabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@color", provider.Color);
            cmd.Parameters.AddWithValue("@timeoutMinutes", provider.TimeoutMinutes > 0 ? provider.TimeoutMinutes : Provider.DefaultTimeoutMinutes);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Saves the explicit ordering of providers as shown in the settings UI.
        /// </summary>
        /// <param name="orderedProviders">Providers in the order they should appear.</param>
        public async Task SaveProviderOrderAsync(IEnumerable<Provider> orderedProviders)
        {
            using var transaction = _connection!.BeginTransaction();
            int index = 0;
            foreach (var p in orderedProviders)
            {
                using var cmd = _connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "UPDATE Providers SET SortOrder = @order WHERE Id = @id";
                cmd.Parameters.AddWithValue("@order", index++);
                cmd.Parameters.AddWithValue("@id", p.Id);
                await cmd.ExecuteNonQueryAsync();
            }
            transaction.Commit();
        }

        /// <summary>
        /// Inserts a new provider row and returns its generated id.
        /// Sensitive fields are encrypted before being stored.
        /// </summary>
        /// <param name="provider">Provider values to insert.</param>
        /// <returns>The SQLite row id assigned to the new provider.</returns>
        public async Task<int> InsertProviderAsync(Provider provider)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Providers (Name, Type, ApiKey, BaseUrl, Model, IsEnabled, Color, TimeoutMinutes)
                VALUES (@name, @type, @apiKey, @baseUrl, @model, @isEnabled, @color, @timeoutMinutes);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@name", provider.Name);
            cmd.Parameters.AddWithValue("@type", provider.Type);
            cmd.Parameters.AddWithValue("@apiKey", (object?)SecureStorage.Protect(provider.ApiKey) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@baseUrl", (object?)provider.BaseUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@model", provider.Model);
            cmd.Parameters.AddWithValue("@isEnabled", provider.IsEnabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@color", provider.Color);
            cmd.Parameters.AddWithValue("@timeoutMinutes", provider.TimeoutMinutes > 0 ? provider.TimeoutMinutes : Provider.DefaultTimeoutMinutes);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        /// <summary>
        /// Deletes a provider row by id.
        /// </summary>
        /// <param name="providerId">Provider to remove.</param>
        public async Task DeleteProviderAsync(int providerId)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "DELETE FROM Providers WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", providerId);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
