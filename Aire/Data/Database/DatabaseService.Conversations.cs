using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using Aire.Services;

namespace Aire.Data
{
    public partial class DatabaseService
    {
        private static readonly string[] ConversationPalette =
        {
            "#E6B800",
            "#2FBF71",
            "#3B82F6",
            "#EC4899",
            "#F97316",
            "#8B5CF6",
            "#14B8A6",
            "#EF4444",
            "#06B6D4",
            "#A3A948",
        };

        private static string PickConversationColor(long seed)
        {
            var index = (int)(Math.Abs(seed) % ConversationPalette.Length);
            return ConversationPalette[index];
        }

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
            var color = PickConversationColor(DateTime.UtcNow.Ticks ^ providerId ^ (title?.GetHashCode() ?? 0));
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Conversations (ProviderId, Title, AssistantModeKey, Color, CreatedAt, UpdatedAt)
                VALUES (@providerId, @title, 'general', @color, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@providerId", providerId);
            cmd.Parameters.AddWithValue("@title", title);
            cmd.Parameters.AddWithValue("@color", color);
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
                    SELECT c.Id, c.Title, c.UpdatedAt, p.Name, COALESCE(c.Color, p.Color), c.AssistantModeKey
                    FROM Conversations c
                    LEFT JOIN Providers p ON c.ProviderId = p.Id
                    ORDER BY c.UpdatedAt DESC
                    LIMIT 200";
            }
            else
            {
                cmd.CommandText = @"
                    SELECT DISTINCT c.Id, c.Title, c.UpdatedAt, p.Name, COALESCE(c.Color, p.Color), c.AssistantModeKey
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
        /// <param name="attachments">Optional persisted file attachments associated with the message.</param>
        public async Task SaveMessageAsync(
            int conversationId,
            string role,
            string content,
            string? imagePath = null,
            IEnumerable<MessageAttachment>? attachments = null,
            int? tokens = null)
        {
            var attachmentsJson = attachments == null
                ? null
                : JsonSerializer.Serialize(attachments);

            // System status lines are occasionally emitted twice when a provider change
            // re-enters the conversation-selection flow. Keep the transcript stable by
            // collapsing consecutive identical system rows at the persistence boundary.
            if (string.Equals(role, "system", StringComparison.OrdinalIgnoreCase))
            {
                using var lastCmd = _connection!.CreateCommand();
                lastCmd.CommandText = @"
                    SELECT Role, Content
                    FROM Messages
                    WHERE ConversationId = @conversationId
                    ORDER BY Id DESC
                    LIMIT 1";
                lastCmd.Parameters.AddWithValue("@conversationId", conversationId);

                using var lastReader = await lastCmd.ExecuteReaderAsync();
                if (await lastReader.ReadAsync() &&
                    string.Equals(lastReader.GetString(0), role, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(lastReader.GetString(1), content, StringComparison.Ordinal))
                {
                    return;
                }
            }

            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Messages (ConversationId, Role, Content, ImagePath, AttachmentsJson, Tokens, CreatedAt)
                VALUES (@conversationId, @role, @content, @imagePath, @attachmentsJson, @tokens, CURRENT_TIMESTAMP)";
            cmd.Parameters.AddWithValue("@conversationId", conversationId);
            cmd.Parameters.AddWithValue("@role", role);
            cmd.Parameters.AddWithValue("@content", content);
            cmd.Parameters.AddWithValue("@imagePath", (object?)imagePath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@attachmentsJson", (object?)attachmentsJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tokens", (object?)tokens ?? DBNull.Value);
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
                SELECT Id, ConversationId, Role, Content, ImagePath, AttachmentsJson, Tokens, CreatedAt
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
                    AttachmentsJson = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Tokens = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    Attachments = DeserializeAttachments(reader.IsDBNull(5) ? null : reader.GetString(5)),
                    CreatedAt = reader.GetDateTime(7)
                });
            }
            return messages;
        }

        /// <summary>
        /// Builds the token-usage snapshot displayed in the settings dashboard.
        /// </summary>
        public async Task<UsageDashboardSnapshot> GetUsageDashboardSnapshotAsync()
        {
            using var totalsCmd = _connection!.CreateCommand();
            totalsCmd.CommandText = @"
                SELECT
                    COALESCE(SUM(COALESCE(Tokens, 0)), 0) AS TotalTokens,
                    COUNT(*) AS AssistantMessageCount,
                    COUNT(DISTINCT ConversationId) AS ConversationCount
                FROM Messages
                WHERE Role = 'assistant'";

            long totalTokens = 0;
            int assistantMessageCount = 0;
            int conversationCount = 0;
            using (var reader = await totalsCmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    totalTokens = reader.GetInt64(0);
                    assistantMessageCount = reader.GetInt32(1);
                    conversationCount = reader.GetInt32(2);
                }
            }

            var providers = await GetProvidersAsync();
            var providerSummaries = new List<ProviderUsageSummary>(providers.Count);
            foreach (var provider in providers)
            {
                providerSummaries.Add(await GetProviderUsageSummaryAsync(provider));
            }

            var conversations = await GetConversationUsageSummariesAsync();
            var trendSeries = await GetUsageTrendSummariesAsync();

            return new UsageDashboardSnapshot(
                totalTokens,
                providers.Count,
                conversationCount,
                assistantMessageCount,
                providerSummaries,
                conversations,
                trendSeries);
        }

        /// <summary>
        /// Aggregates stored assistant-token usage for one provider.
        /// </summary>
        public async Task<ProviderUsageSummary> GetProviderUsageSummaryAsync(Provider provider)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    COALESCE(SUM(COALESCE(m.Tokens, 0)), 0) AS TotalTokens,
                    COUNT(m.Id) AS AssistantMessageCount,
                    COUNT(DISTINCT c.Id) AS ConversationCount,
                    MAX(m.CreatedAt) AS LastUsedAt
                FROM Conversations c
                LEFT JOIN Messages m
                    ON m.ConversationId = c.Id
                   AND m.Role = 'assistant'
                WHERE c.ProviderId = @providerId";
            cmd.Parameters.AddWithValue("@providerId", provider.Id);

            long totalTokens = 0;
            int assistantMessageCount = 0;
            int providerConversationCount = 0;
            DateTime? lastUsedAt = null;
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    totalTokens = reader.GetInt64(0);
                    assistantMessageCount = reader.GetInt32(1);
                    providerConversationCount = reader.GetInt32(2);
                    lastUsedAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
                }
            }

            return new ProviderUsageSummary(
                provider.Id,
                provider.Name,
                provider.Type,
                provider.Model,
                provider.Color,
                provider.IsEnabled,
                totalTokens,
                providerConversationCount,
                assistantMessageCount,
                lastUsedAt);
        }

        /// <summary>
        /// Aggregates stored assistant-token usage for recent conversations.
        /// </summary>
        public async Task<List<ConversationUsageSummary>> GetConversationUsageSummariesAsync(int limit = 20)
        {
            var results = new List<ConversationUsageSummary>();
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    c.Id,
                    COALESCE(c.Title, 'Chat') AS Title,
                    COALESCE(p.Name, '') AS ProviderName,
                    COALESCE(p.Type, '') AS ProviderType,
                    COALESCE(p.Model, '') AS Model,
                    COALESCE(c.Color, p.Color, '#888888') AS Color,
                    c.UpdatedAt,
                    COALESCE(SUM(COALESCE(m.Tokens, 0)), 0) AS TotalTokens,
                    COUNT(m.Id) AS AssistantMessageCount
                FROM Conversations c
                LEFT JOIN Providers p ON p.Id = c.ProviderId
                LEFT JOIN Messages m
                    ON m.ConversationId = c.Id
                   AND m.Role = 'assistant'
                GROUP BY c.Id, c.Title, c.UpdatedAt, p.Name, p.Type, p.Model, c.Color, p.Color
                HAVING COALESCE(SUM(COALESCE(m.Tokens, 0)), 0) > 0 OR COUNT(m.Id) > 0
                ORDER BY TotalTokens DESC, c.UpdatedAt DESC
                LIMIT @limit";
            cmd.Parameters.AddWithValue("@limit", Math.Max(1, limit));

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new ConversationUsageSummary(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetDateTime(6),
                    reader.GetInt64(7),
                    reader.GetInt32(8)));
            }

            return results;
        }

        /// <summary>
        /// Aggregates historical assistant-token usage by provider/model and day.
        /// </summary>
        public async Task<List<UsageTrendSeries>> GetUsageTrendSummariesAsync(int days = 30)
        {
            var window = Math.Max(1, days);
            var startDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-(window - 1)));
            var endDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            var lookback = $"-{window - 1} days";

            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    c.ProviderId,
                    COALESCE(p.Name, 'Unknown provider') AS ProviderName,
                    COALESCE(p.Type, '') AS ProviderType,
                    COALESCE(p.Model, '') AS Model,
                    COALESCE(p.Color, '#888888') AS Color,
                    date(m.CreatedAt) AS BucketDate,
                    COALESCE(SUM(COALESCE(m.Tokens, 0)), 0) AS TokensUsed
                FROM Messages m
                INNER JOIN Conversations c ON c.Id = m.ConversationId
                LEFT JOIN Providers p ON p.Id = c.ProviderId
                WHERE m.Role = 'assistant'
                  AND COALESCE(m.Tokens, 0) > 0
                  AND date(m.CreatedAt) >= date('now', @lookback)
                GROUP BY c.ProviderId, p.Name, p.Type, p.Model, p.Color, date(m.CreatedAt)
                ORDER BY BucketDate ASC, TokensUsed DESC, ProviderName ASC, Model ASC";
            cmd.Parameters.AddWithValue("@lookback", lookback);

            var builders = new Dictionary<(int ProviderId, string ProviderName, string ProviderType, string Model, string Color), Dictionary<DateOnly, long>>();

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var providerId = reader.GetInt32(0);
                    var providerName = reader.GetString(1);
                    var providerType = reader.GetString(2);
                    var model = reader.GetString(3);
                    var color = reader.GetString(4);
                    var bucketText = reader.GetString(5);
                    var tokensUsed = reader.GetInt64(6);

                    if (!DateOnly.TryParse(bucketText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var bucket))
                        continue;

                    var key = (providerId, providerName, providerType, model, color);
                    if (!builders.TryGetValue(key, out var points))
                    {
                        points = new Dictionary<DateOnly, long>();
                        builders[key] = points;
                    }

                    points[bucket] = tokensUsed;
                }
            }

            var series = new List<UsageTrendSeries>(builders.Count);
            foreach (var (key, pointsByDate) in builders)
            {
                var points = new List<UsageTrendPoint>(window);
                long totalTokens = 0;
                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    pointsByDate.TryGetValue(date, out var tokensUsed);
                    totalTokens += tokensUsed;
                    points.Add(new UsageTrendPoint(date, tokensUsed));
                }

                series.Add(new UsageTrendSeries(
                    key.ProviderId,
                    key.ProviderName,
                    key.ProviderType,
                    key.Model,
                    key.Color,
                    totalTokens,
                    points));
            }

            return series
                .OrderByDescending(item => item.TotalTokens)
                .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<MessageAttachment> DeserializeAttachments(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<MessageAttachment>();

            try
            {
                return JsonSerializer.Deserialize<List<MessageAttachment>>(json) ?? new List<MessageAttachment>();
            }
            catch
            {
                return new List<MessageAttachment>();
            }
        }
    }
}
