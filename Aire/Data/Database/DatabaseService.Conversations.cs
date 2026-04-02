using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aire.Services;

namespace Aire.Data
{
    public partial class DatabaseService
    {
        /// <summary>
        /// Deletes all message rows that belong to one conversation.
        /// </summary>
        /// <param name="conversationId">Conversation whose messages should be removed.</param>
        public async Task DeleteMessagesByConversationIdAsync(int conversationId)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "DELETE FROM Messages WHERE ConversationId = @conversationId";
            cmd.Parameters.AddWithValue("@conversationId", conversationId);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Deletes one conversation and all of its associated messages.
        /// </summary>
        /// <param name="conversationId">Conversation to remove.</param>
        public async Task DeleteConversationAsync(int conversationId)
        {
            await DeleteMessagesByConversationIdAsync(conversationId);
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "DELETE FROM Conversations WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", conversationId);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Deletes every stored conversation and message in the database.
        /// </summary>
        public async Task DeleteAllConversationsAsync()
        {
            using var cmd1 = _connection!.CreateCommand();
            cmd1.CommandText = "DELETE FROM Messages";
            await cmd1.ExecuteNonQueryAsync();

            using var cmd2 = _connection!.CreateCommand();
            cmd2.CommandText = "DELETE FROM Conversations";
            await cmd2.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Creates a new conversation row for the specified provider.
        /// </summary>
        /// <param name="providerId">Provider that owns the conversation.</param>
        /// <param name="title">Initial conversation title.</param>
        /// <returns>The new conversation id.</returns>
        public async Task<int> CreateConversationAsync(int providerId, string title = "New Chat")
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Conversations (ProviderId, Title, AssistantModeKey, CreatedAt, UpdatedAt)
                VALUES (@providerId, @title, 'general', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@providerId", providerId);
            cmd.Parameters.AddWithValue("@title", title);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        /// <summary>
        /// Returns the most recently updated conversation for a provider.
        /// </summary>
        /// <param name="providerId">Provider whose latest conversation should be loaded.</param>
        /// <returns>The latest conversation, or <see langword="null"/> when none exists.</returns>
        public async Task<Conversation?> GetLatestConversationAsync(int providerId)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, ProviderId, Title, AssistantModeKey, CreatedAt, UpdatedAt
                FROM Conversations
                WHERE ProviderId = @providerId
                ORDER BY UpdatedAt DESC
                LIMIT 1";
            cmd.Parameters.AddWithValue("@providerId", providerId);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Conversation
                {
                    Id = reader.GetInt32(0),
                    ProviderId = reader.GetInt32(1),
                    Title = reader.GetString(2),
                    AssistantModeKey = reader.IsDBNull(3) ? "general" : reader.GetString(3),
                    CreatedAt = reader.GetDateTime(4),
                    UpdatedAt = reader.GetDateTime(5)
                };
            }
            return null;
        }

        /// <summary>
        /// Lists recent conversations, optionally filtering by title or message content.
        /// </summary>
        /// <param name="search">Optional search text applied to titles and messages.</param>
        /// <returns>Conversation summaries ordered by most recently updated.</returns>
        public async Task<List<ConversationSummary>> ListConversationsAsync(string? search = null)
        {
            var results = new List<ConversationSummary>();
            using var cmd = _connection!.CreateCommand();

            if (string.IsNullOrWhiteSpace(search))
            {
                cmd.CommandText = @"
                    SELECT c.Id, c.Title, c.UpdatedAt, p.Name, p.Color, c.AssistantModeKey
                    FROM Conversations c
                    LEFT JOIN Providers p ON c.ProviderId = p.Id
                    ORDER BY c.UpdatedAt DESC
                    LIMIT 200";
            }
            else
            {
                cmd.CommandText = @"
                    SELECT DISTINCT c.Id, c.Title, c.UpdatedAt, p.Name, p.Color, c.AssistantModeKey
                    FROM Conversations c
                    LEFT JOIN Providers p ON c.ProviderId = p.Id
                    LEFT JOIN Messages  m ON m.ConversationId = c.Id
                    WHERE c.Title LIKE @search OR m.Content LIKE @search
                    ORDER BY c.UpdatedAt DESC
                    LIMIT 200";
                cmd.Parameters.AddWithValue("@search", $"%{search}%");
            }

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new ConversationSummary
                {
                    Id = reader.GetInt32(0),
                    Title = reader.IsDBNull(1) ? "Chat" : reader.GetString(1),
                    UpdatedAt = reader.IsDBNull(2) ? DateTime.Now : reader.GetDateTime(2),
                    ProviderName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    ProviderColor = reader.IsDBNull(4) ? "#888888" : reader.GetString(4),
                    AssistantModeKey = reader.IsDBNull(5) ? "general" : reader.GetString(5)
                });
            }
            return results;
        }

        /// <summary>
        /// Loads one conversation row by id.
        /// </summary>
        /// <param name="conversationId">Conversation id to load.</param>
        /// <returns>The conversation, or <see langword="null"/> when it does not exist.</returns>
        public async Task<Conversation?> GetConversationAsync(int conversationId)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, ProviderId, Title, AssistantModeKey, CreatedAt, UpdatedAt
                FROM Conversations
                WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", conversationId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return new Conversation
            {
                Id = reader.GetInt32(0),
                ProviderId = reader.GetInt32(1),
                Title = reader.IsDBNull(2) ? "Chat" : reader.GetString(2),
                AssistantModeKey = reader.IsDBNull(3) ? "general" : reader.GetString(3),
                CreatedAt = reader.GetDateTime(4),
                UpdatedAt = reader.GetDateTime(5),
            };
        }

        /// <summary>
        /// Updates the user-visible title of a conversation.
        /// </summary>
        /// <param name="conversationId">Conversation to update.</param>
        /// <param name="title">New title text.</param>
        public async Task UpdateConversationTitleAsync(int conversationId, string title)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "UPDATE Conversations SET Title = @title WHERE Id = @id";
            cmd.Parameters.AddWithValue("@title", title);
            cmd.Parameters.AddWithValue("@id", conversationId);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Reassigns a conversation to a different provider.
        /// </summary>
        /// <param name="conversationId">Conversation to update.</param>
        /// <param name="providerId">New owning provider id.</param>
        public async Task UpdateConversationProviderAsync(int conversationId, int providerId)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "UPDATE Conversations SET ProviderId = @providerId WHERE Id = @id";
            cmd.Parameters.AddWithValue("@providerId", providerId);
            cmd.Parameters.AddWithValue("@id", conversationId);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Updates the stored assistant mode for a conversation.
        /// </summary>
        public async Task UpdateConversationAssistantModeAsync(int conversationId, string assistantModeKey)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "UPDATE Conversations SET AssistantModeKey = @assistantModeKey WHERE Id = @id";
            cmd.Parameters.AddWithValue("@assistantModeKey", string.IsNullOrWhiteSpace(assistantModeKey) ? "general" : assistantModeKey.Trim());
            cmd.Parameters.AddWithValue("@id", conversationId);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Persists one chat message and updates the parent conversation timestamp.
        /// </summary>
        /// <param name="conversationId">Conversation that owns the message.</param>
        /// <param name="role">Role stored with the message, such as user, assistant, or tool.</param>
        /// <param name="content">Message text payload.</param>
        /// <param name="imagePath">Optional persisted image path associated with the message.</param>
        public async Task SaveMessageAsync(int conversationId, string role, string content, string? imagePath = null)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Messages (ConversationId, Role, Content, ImagePath, CreatedAt)
                VALUES (@conversationId, @role, @content, @imagePath, CURRENT_TIMESTAMP)";
            cmd.Parameters.AddWithValue("@conversationId", conversationId);
            cmd.Parameters.AddWithValue("@role", role);
            cmd.Parameters.AddWithValue("@content", content);
            cmd.Parameters.AddWithValue("@imagePath", (object?)imagePath ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();

            using var updateCmd = _connection.CreateCommand();
            updateCmd.CommandText = @"
                UPDATE Conversations
                SET UpdatedAt = CURRENT_TIMESTAMP
                WHERE Id = @conversationId";
            updateCmd.Parameters.AddWithValue("@conversationId", conversationId);
            await updateCmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Loads all messages for one conversation in chronological order.
        /// </summary>
        /// <param name="conversationId">Conversation whose messages should be returned.</param>
        /// <returns>Chronological message list for the conversation.</returns>
        public async Task<List<Message>> GetMessagesAsync(int conversationId)
        {
            var messages = new List<Message>();
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, ConversationId, Role, Content, ImagePath, CreatedAt
                FROM Messages
                WHERE ConversationId = @conversationId
                ORDER BY CreatedAt ASC";
            cmd.Parameters.AddWithValue("@conversationId", conversationId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                messages.Add(new Message
                {
                    Id = reader.GetInt32(0),
                    ConversationId = reader.GetInt32(1),
                    Role = reader.GetString(2),
                    Content = reader.GetString(3),
                    ImagePath = reader.IsDBNull(4) ? null : reader.GetString(4),
                    CreatedAt = reader.GetDateTime(5)
                });
            }
            return messages;
        }
    }
}
