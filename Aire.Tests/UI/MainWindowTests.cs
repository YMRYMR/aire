using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Aire.AppLayer.Api;
using Aire.AppLayer.Chat;
using Aire.AppLayer.Providers;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using Aire.UI;
using Aire.UI.MainWindow.Controls;
using Aire.UI.MainWindow.Models;
using Aire.UI.Settings.Models;
using Xunit;
using UiModels = Aire.UI.MainWindow.Models;
using ProviderChatMessage = Aire.Providers.ChatMessage;

namespace Aire.Tests.UI
{
    [Collection("AppState Isolation")]
    public class MainWindowTests : TestBase
    {
        [Fact]
        public void MainWindow_ApiPendingApprovalHelpers_Work()
        {
            RunOnStaThread(async () =>
            {
                AppStartupState.MarkReady();
                MainWindow window = new MainWindow(initializeUi: false);

                ObservableCollection<UiModels.ChatMessage> messages = new ObservableCollection<UiModels.ChatMessage>
                {
                    new UiModels.ChatMessage
                    {
                        IsApprovalPending = true,
                        PendingToolCall = new ToolCallRequest
                        {
                            Tool = "execute_command",
                            Description = "run",
                            RawJson = "{}"
                        },
                        ApprovalTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
                        Timestamp = "10:00"
                    },
                    new UiModels.ChatMessage
                    {
                        IsApprovalPending = true,
                        PendingToolCall = new ToolCallRequest
                        {
                            Tool = "read_file",
                            Description = "read",
                            RawJson = "{}"
                        },
                        ApprovalTcs = TaskCompletionSource_GetValue(true),
                        Timestamp = "10:01"
                    }
                };
                window.Messages = messages;

                var pending = await window.ApiListPendingApprovalsAsync();
                Assert.Single(pending);
                Assert.Equal("execute_command", pending[0].Tool);
                Assert.Equal(0, pending[0].Index);

                Assert.False(await window.ApiSetPendingApprovalAsync(-1, true));
                Assert.False(await window.ApiSetPendingApprovalAsync(1, true));
                Assert.True(await window.ApiSetPendingApprovalAsync(0, false));
                
                Assert.True(messages[0].ApprovalTcs.Task.IsCompleted);
                Assert.Empty(await window.ApiListPendingApprovalsAsync());
            });
        }

        [Fact]
        public void MainWindow_ProviderCoordinator_CooldownAndEmptyProviderPaths_Work()
        {
            RunOnStaThread(delegate
            {
                EnsureApplication();
                string tempDir = Path.Combine(Path.GetTempPath(), "aire-tests-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);
                string originalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
                Environment.SetEnvironmentVariable("LOCALAPPDATA", tempDir);
                
                try
                {
                    using var databaseService = new DatabaseService();
                    databaseService.InitializeAsync().GetAwaiter().GetResult();
                    
                    Provider provider = new Provider
                    {
                        Name = "Cooldown Provider",
                        Type = "OpenAI",
                        ApiKey = "sk-cooldown",
                        Model = "gpt-4o-mini",
                        IsEnabled = true,
                        Color = "#336699"
                    };
                    provider.Id = databaseService.InsertProviderAsync(provider).GetAwaiter().GetResult();
                    
                    MainWindow window = new MainWindow(initializeUi: false);
                    
                    MainHeaderControl header = new MainHeaderControl();
                    header._testProviderComboBox = new ComboBox();
                    header._testCheckAgainButton = new Button();
                    header.ProviderComboBox.ItemsSource = new Provider[] { provider };
                    header.ProviderComboBox.SelectedItem = provider;
                    
                    window.HeaderControl = header;
                    window._availabilityTracker = ProviderAvailabilityTracker.Instance;
                    window._conversationHistory = new List<ProviderChatMessage>();
                    window._databaseService = databaseService;
                    window._providerFactory = new ProviderFactory(databaseService);
                    window._chatSessionApplicationService = new ChatSessionApplicationService(databaseService, databaseService);
                    window._conversationApplicationService = new ConversationApplicationService(databaseService);
                    window._speechService = new SpeechRecognitionService();
                    window._currentProviderId = provider.Id;
                    window.Messages = new ObservableCollection<UiModels.ChatMessage>();

                    ProviderAvailabilityTracker.Instance.SetCooldown(provider.Id, CooldownReason.RateLimit, "Rate limit reached");
                    
                    string modelList = window.BuildModelListSection();
                    Assert.Contains("gpt-4o-mini", modelList);
                    Assert.Contains("UNAVAILABLE", modelList, StringComparison.OrdinalIgnoreCase);
                    
                    window.RefreshProviderAvailabilityUI();
                    Assert.Equal(Visibility.Visible, header.CheckAgainButton.Visibility);
                    
                    window.CheckAgainButton_Click(header.CheckAgainButton, new RoutedEventArgs());
                    window.RefreshProviderAvailabilityUI();
                    Assert.Equal(Visibility.Collapsed, header.CheckAgainButton.Visibility);
                }
                finally
                {
                    Environment.SetEnvironmentVariable("LOCALAPPDATA", originalAppData);
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            });
        }

        [Fact]
        public void MainWindow_ApiExecuteToolAndApprovalPaths_Work()
        {
            RunOnStaThread(delegate
            {
                EnsureApplication();
                AppStartupState.MarkReady();
                MainWindow window = new MainWindow(initializeUi: false);
                window.Messages = new ObservableCollection<UiModels.ChatMessage>();

                string tempDir = Path.Combine(Path.GetTempPath(), "aire-tool-tests-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);
                string testFile = Path.Combine(tempDir, "tool.txt");
                File.WriteAllText(testFile, "hello from tool");
                using var databaseService = new DatabaseService();
                databaseService.InitializeAsync().GetAwaiter().GetResult();

                try
                {
                    window._databaseService = databaseService;
                    window._toolExecutionService = new ToolExecutionService(new FileSystemService(), new CommandExecutionService());
                    typeof(MainWindow).GetField("_toolApprovalExecutionApplicationService", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .SetValue(window, null);
                    window.MessagesScrollViewer = new ScrollViewer();
                    SettingsWindow.SetAutoAcceptCache(string.Empty);

                    using JsonDocument emptyParams = JsonDocument.Parse("[]");
                    var execTask = window.ApiExecuteToolAsync("read_file", emptyParams.RootElement, false, 300);
                    var result = execTask.GetAwaiter().GetResult();

                    Assert.Equal("pending_approval", result.Status);
                    Assert.NotNull(result.PendingApprovalIndex);

                    SettingsWindow.SetAutoAcceptCache(JsonSerializer.Serialize(new AutoAcceptSettings
                    {
                        Enabled = true,
                        AllowedTools = new List<string> { "read_file" },
                        AllowMouseTools = false,
                        AllowKeyboardTools = false
                    }));
                    using JsonDocument pathParams = JsonDocument.Parse($@"{{""path"":""{testFile.Replace("\\", "\\\\")}""}}");
                    var allowResult = window.ApiExecuteToolAsync("read_file", pathParams.RootElement, true, 300).GetAwaiter().GetResult();
                    Assert.Equal("completed", allowResult.Status);
                    Assert.Contains("hello from tool", allowResult.TextResult);
                }
                finally
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            });
        }

        [Fact]
        public void MainWindow_ApiListingStateAndConversationCreation_Work()
        {
            RunOnStaThread(delegate
            {
                EnsureApplication();
                AppStartupState.MarkReady();
                string tempDir = Path.Combine(Path.GetTempPath(), "aire-list-tests-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);
                string originalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
                Environment.SetEnvironmentVariable("LOCALAPPDATA", tempDir);

                try
                {
                    AppState.SetApiAccessEnabled(true);
                    AppState.SetApiAccessToken("test-token");

                    using var db = new DatabaseService();
                    db.InitializeAsync().GetAwaiter().GetResult();

                    Provider p1 = new Provider { Name = "P1", Type = "OpenAI", ApiKey = "k1", Model = "m1", IsEnabled = true };
                    p1.Id = db.InsertProviderAsync(p1).GetAwaiter().GetResult();
                    
                    int convId = db.CreateConversationAsync(p1.Id, "Needle Chat").GetAwaiter().GetResult();
                    db.SaveMessageAsync(convId, "user", "needle content").GetAwaiter().GetResult();

                    MainWindow window = new MainWindow(initializeUi: false);

                    MainHeaderControl header = new MainHeaderControl();
                    header._testProviderComboBox = new ComboBox();
                    header._testCheckAgainButton = new Button();
                    header.ProviderComboBox.ItemsSource = new Provider[] { p1 };
                    header.ProviderComboBox.SelectedItem = p1;
                    
                    window.HeaderControl = header;
                    window._availabilityTracker = ProviderAvailabilityTracker.Instance;
                    window._databaseService = db;
                    window._providerFactory = new ProviderFactory(db);
                    window._chatSessionApplicationService = new ChatSessionApplicationService(db, db);
                    window._conversationApplicationService = new ConversationApplicationService(db);
                    window._currentConversationId = convId;
                    window._settingsWindow = new SettingsWindow(initializeUi: false); // stub — not used by the tested API methods
                    window.Messages = new ObservableCollection<UiModels.ChatMessage>();
                    window._conversationHistory = new List<ProviderChatMessage>();

                    var providers = window.ApiListProvidersAsync().GetAwaiter().GetResult();
                    Assert.Contains(providers, x => x.Name == "P1");

                    var convs = window.ApiListConversationsAsync("needle").GetAwaiter().GetResult();
                    Assert.Single(convs);

                    var state = window.ApiGetStateAsync().GetAwaiter().GetResult();
                    Assert.Equal(convId, state.CurrentConversationId);
                    Assert.Equal("P1", state.CurrentProviderName);

                    header.ProviderComboBox.SelectedItem = null;
                    Assert.Throws<InvalidOperationException>(() => window.ApiCreateConversationAsync().GetAwaiter().GetResult());

                    header.ProviderComboBox.SelectedItem = p1;
                    int newId = window.ApiCreateConversationAsync("New").GetAwaiter().GetResult();
                    Assert.True(newId > 0);
                }
                finally
                {
                    Environment.SetEnvironmentVariable("LOCALAPPDATA", originalAppData);
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            });
        }

        [Fact]
        public void MainWindow_ConversationSelection_RestoresPreviousConversation_WhenLoadFails()
        {
            RunOnStaThread(delegate
            {
                EnsureApplication();
                AppStartupState.MarkReady();

                string tempDir = Path.Combine(Path.GetTempPath(), "aire-conversation-switch-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);
                string originalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
                Environment.SetEnvironmentVariable("LOCALAPPDATA", tempDir);

                try
                {
                    using var db = new DatabaseService();
                    db.InitializeAsync().GetAwaiter().GetResult();

                    Provider provider = new Provider
                    {
                        Name = "P1",
                        Type = "OpenAI",
                        ApiKey = "k1",
                        Model = "m1",
                        IsEnabled = true,
                        Color = "#336699"
                    };
                    provider.Id = db.InsertProviderAsync(provider).GetAwaiter().GetResult();

                    Provider targetProvider = new Provider
                    {
                        Name = "P2",
                        Type = "Groq",
                        ApiKey = "k2",
                        Model = "m2",
                        IsEnabled = true,
                        Color = "#993366"
                    };
                    targetProvider.Id = db.InsertProviderAsync(targetProvider).GetAwaiter().GetResult();

                    int currentConversationId = db.CreateConversationAsync(provider.Id, "Current").GetAwaiter().GetResult();
                    int targetConversationId = db.CreateConversationAsync(targetProvider.Id, "Target").GetAwaiter().GetResult();

                    MainWindow window = new MainWindow(initializeUi: false);

                    window.Messages = new ObservableCollection<UiModels.ChatMessage>();
                    window._conversationHistory = new List<ProviderChatMessage>();
                    window._currentConversationId = currentConversationId;
                    window._currentProviderId = provider.Id;
                    window._providerFactory = new ProviderFactory(db);
                    typeof(MainWindow).GetField("_chatService", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .SetValue(window, new ChatService(window._providerFactory));
                    window._chatSessionApplicationService = new ChatSessionApplicationService(db, db);

                    var header = new MainHeaderControl();
                    header._testProviderComboBox = new ComboBox();
                    header._testCheckAgainButton = new Button();
                    header.ProviderComboBox.ItemsSource = new[] { provider, targetProvider };
                    header.ProviderComboBox.SelectedItem = provider;
                    window.HeaderControl = header;
                    window.ConversationSidebar = new ConversationSidebarControl();

                    typeof(MainWindow).GetField("_assistantModeApplicationService", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .SetValue(window, new AssistantModeApplicationService());

                    var throwingRepo = new ThrowingConversationRepository(db, targetConversationId);
                    window._conversationApplicationService = new ConversationApplicationService(throwingRepo);

                    var currentSummary = new ConversationSummary
                    {
                        Id = currentConversationId,
                        Title = "Current",
                        UpdatedAt = DateTime.UtcNow.AddMinutes(-1),
                        ProviderName = provider.Name,
                        ProviderColor = provider.Color,
                        AssistantModeKey = "general"
                    };

                    var targetSummary = new ConversationSummary
                    {
                        Id = targetConversationId,
                        Title = "Target",
                        UpdatedAt = DateTime.UtcNow,
                        ProviderName = targetProvider.Name,
                        ProviderColor = targetProvider.Color,
                        AssistantModeKey = "general"
                    };

                    window.ConversationSidebar.ItemsSource = new[] { currentSummary, targetSummary };
                    window.ConversationSidebar.SelectedItem = currentSummary;

                    var method = typeof(MainWindow).GetMethod("SwitchConversationAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    ((Task)method.Invoke(window, [targetSummary])!).GetAwaiter().GetResult();

                    Assert.Equal(currentConversationId, window._currentConversationId);
                    Assert.Equal(provider, window.ProviderComboBox.SelectedItem);
                    Assert.Equal(currentConversationId, ((ConversationSummary)window.ConversationSidebar.SelectedItem).Id);
                    Assert.Contains(window.Messages, msg => msg.Sender == "System" && msg.Text.Contains("Failed to load conversation", StringComparison.Ordinal));
                }
                finally
                {
                    Environment.SetEnvironmentVariable("LOCALAPPDATA", originalAppData);
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            });
        }

        [Fact]
        public void MainWindow_RefreshProvidersAsync_ClearsSelection_WhenCurrentProviderDisappears()
        {
            RunOnStaThread(delegate
            {
                EnsureApplication();
                AppStartupState.MarkReady();

                string tempDir = Path.Combine(Path.GetTempPath(), "aire-provider-refresh-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);
                string originalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
                Environment.SetEnvironmentVariable("LOCALAPPDATA", tempDir);

                try
                {
                    using var db = new DatabaseService();
                    db.InitializeAsync().GetAwaiter().GetResult();

                    Provider provider = new Provider
                    {
                        Name = "P1",
                        Type = "OpenAI",
                        ApiKey = "k1",
                        Model = "m1",
                        IsEnabled = true,
                        Color = "#336699"
                    };
                    provider.Id = db.InsertProviderAsync(provider).GetAwaiter().GetResult();

                    MainWindow window = new MainWindow(initializeUi: false);

                    var header = new MainHeaderControl();
                    header._testProviderComboBox = new ComboBox();
                    header._testCheckAgainButton = new Button();
                    header.ProviderComboBox.ItemsSource = new[] { provider };
                    header.ProviderComboBox.SelectedItem = provider;

                    window.HeaderControl = header;
                    window.Messages = new ObservableCollection<UiModels.ChatMessage>();
                    window._availabilityTracker = ProviderAvailabilityTracker.Instance;
                    window._databaseService = db;
                    window._providerFactory = new ProviderFactory(db);
                    window._speechService = new SpeechRecognitionService();
                    typeof(MainWindow).GetField("ComposerControl", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
                        .SetValue(window, new MainComposerControl());
                    typeof(MainWindow).GetField("_chatService", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .SetValue(window, new ChatService(window._providerFactory));
                    typeof(MainWindow).GetField("_providerCatalogApplicationService", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .SetValue(window, new ProviderCatalogApplicationService(db));
                    window._currentProviderId = provider.Id;
                    typeof(MainWindow).GetField("_enabledToolCategories", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .SetValue(window, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

                    provider.IsEnabled = false;
                    db.UpdateProviderAsync(provider).GetAwaiter().GetResult();

                    window.RefreshProvidersAsync().GetAwaiter().GetResult();

                    Assert.Null(window.ProviderComboBox.SelectedItem);
                    Assert.Null(window._currentProviderId);
                    Assert.Null(typeof(MainWindow).GetField("_currentProvider", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window));
                    Assert.Null(typeof(ChatService).GetField("_currentProvider", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(typeof(MainWindow).GetField("_chatService", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)));
                }
                finally
                {
                    Environment.SetEnvironmentVariable("LOCALAPPDATA", originalAppData);
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            });
        }

        [Fact]
        public void MainWindow_ApiSetProviderAsync_SwitchesSilently_WithoutAppendingSystemMessage()
        {
            RunOnStaThread(async () =>
            {
                EnsureApplication();
                AppStartupState.MarkReady();

                string tempDir = Path.Combine(Path.GetTempPath(), "aire-provider-switch-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);
                string originalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
                Environment.SetEnvironmentVariable("LOCALAPPDATA", tempDir);

                try
                {
                    using var db = new DatabaseService();
                    await db.InitializeAsync();

                    Provider currentProvider = new Provider
                    {
                        Name = "Current",
                        Type = "OpenAI",
                        ApiKey = "k1",
                        Model = "m1",
                        IsEnabled = true,
                        Color = "#336699"
                    };
                    currentProvider.Id = await db.InsertProviderAsync(currentProvider);

                    Provider targetProvider = new Provider
                    {
                        Name = "Target",
                        Type = "OpenAI",
                        ApiKey = "k2",
                        Model = "m2",
                        IsEnabled = true,
                        Color = "#993366"
                    };
                    targetProvider.Id = await db.InsertProviderAsync(targetProvider);

                    int conversationId = await db.CreateConversationAsync(currentProvider.Id, "Current conversation");

                    MainWindow window = new MainWindow(initializeUi: false);
                    var header = new MainHeaderControl();
                    header._testProviderComboBox = new ComboBox();
                    header._testCheckAgainButton = new Button();
                    header.ProviderComboBox.ItemsSource = new[] { currentProvider, targetProvider };
                    header.ProviderComboBox.SelectedItem = currentProvider;

                    window.HeaderControl = header;
                    window.Messages = new ObservableCollection<UiModels.ChatMessage>();
                    window._currentConversationId = conversationId;
                    window._currentProviderId = currentProvider.Id;
                    window._databaseService = db;
                    window._providerFactory = new ProviderFactory(db);
                    window._chatSessionApplicationService = new ChatSessionApplicationService(db, db);
                    window._conversationApplicationService = new ConversationApplicationService(db);
                    typeof(MainWindow).GetField("_chatService", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .SetValue(window, new ChatService(window._providerFactory));

                    bool switched = await window.ApiSetProviderAsync(targetProvider.Id);

                    Assert.True(switched);
                    Assert.Same(targetProvider, header.ProviderComboBox.SelectedItem);
                    Assert.Equal(targetProvider.Id, window._currentProviderId);
                    Assert.DoesNotContain(
                        window.Messages,
                        msg => msg.Sender == "System" && msg.Text.Contains("Switched to", StringComparison.OrdinalIgnoreCase));
                }
                finally
                {
                    Environment.SetEnvironmentVariable("LOCALAPPDATA", originalAppData);
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            });
        }

        [Fact]
        public void MainWindow_ApiSelectConversationAsync_LoadsConversation_WithoutChangingProvider()
        {
            RunOnStaThread(async () =>
            {
                EnsureApplication();
                AppStartupState.MarkReady();

                string tempDir = Path.Combine(Path.GetTempPath(), "aire-conversation-select-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);
                string originalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
                Environment.SetEnvironmentVariable("LOCALAPPDATA", tempDir);

                try
                {
                    using var db = new DatabaseService();
                    await db.InitializeAsync();

                    Provider currentProvider = new Provider
                    {
                        Name = "Current",
                        Type = "OpenAI",
                        ApiKey = "k1",
                        Model = "m1",
                        IsEnabled = true,
                        Color = "#336699"
                    };
                    currentProvider.Id = await db.InsertProviderAsync(currentProvider);

                    Provider targetProvider = new Provider
                    {
                        Name = "Target",
                        Type = "OpenAI",
                        ApiKey = "k2",
                        Model = "m2",
                        IsEnabled = true,
                        Color = "#993366"
                    };
                    targetProvider.Id = await db.InsertProviderAsync(targetProvider);

                    int conversationId = await db.CreateConversationAsync(targetProvider.Id, "Target conversation");
                    await db.SaveMessageAsync(conversationId, "user", "hello");

                    MainWindow window = new MainWindow(initializeUi: false);
                    var header = new MainHeaderControl();
                    header._testProviderComboBox = new ComboBox();
                    header._testCheckAgainButton = new Button();
                    header.ProviderComboBox.ItemsSource = new[] { currentProvider, targetProvider };
                    header.ProviderComboBox.SelectedItem = currentProvider;
                    var composer = new MainComposerControl();

                    window.HeaderControl = header;
                    window.Messages = new ObservableCollection<UiModels.ChatMessage>();
                    window._currentConversationId = null;
                    window._currentProviderId = currentProvider.Id;
                    window._databaseService = db;
                    window._providerFactory = new ProviderFactory(db);
                    window._chatSessionApplicationService = new ChatSessionApplicationService(db, db);
                    window._conversationApplicationService = new ConversationApplicationService(db);
                    typeof(MainWindow).GetField("ComposerControl", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .SetValue(window, composer);
                    typeof(MainWindow).GetField("_chatService", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .SetValue(window, new ChatService(window._providerFactory));

                    bool selected = await window.ApiSelectConversationAsync(conversationId);

                    Assert.True(selected);
                    Assert.Same(currentProvider, header.ProviderComboBox.SelectedItem);
                    Assert.Equal(currentProvider.Id, window._currentProviderId);
                    Assert.Contains(window.Messages, msg => msg.Text == "hello");
                }
                finally
                {
                    Environment.SetEnvironmentVariable("LOCALAPPDATA", originalAppData);
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            });
        }

        private static TaskCompletionSource<bool> TaskCompletionSource_GetValue(bool value)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            tcs.SetResult(value);
            return tcs;
        }

        private sealed class ThrowingConversationRepository : Aire.AppLayer.Abstractions.IConversationRepository
        {
            private readonly DatabaseService _databaseService;
            private readonly int _throwingConversationId;

            public ThrowingConversationRepository(DatabaseService databaseService, int throwingConversationId)
            {
                _databaseService = databaseService;
                _throwingConversationId = throwingConversationId;
            }

            public Task<int> CreateConversationAsync(int providerId, string title) => _databaseService.CreateConversationAsync(providerId, title);
            public Task<Aire.Data.Conversation?> GetLatestConversationAsync(int providerId) => _databaseService.GetLatestConversationAsync(providerId);
            public Task<Aire.Data.Conversation?> GetConversationAsync(int conversationId) => _databaseService.GetConversationAsync(conversationId);
            public Task<List<Aire.Data.ConversationSummary>> ListConversationsAsync(string? search = null) => _databaseService.ListConversationsAsync(search);
            public Task UpdateConversationTitleAsync(int conversationId, string title) => _databaseService.UpdateConversationTitleAsync(conversationId, title);
            public Task UpdateConversationProviderAsync(int conversationId, int providerId) => _databaseService.UpdateConversationProviderAsync(conversationId, providerId);
            public Task UpdateConversationAssistantModeAsync(int conversationId, string assistantModeKey) => _databaseService.UpdateConversationAssistantModeAsync(conversationId, assistantModeKey);

            public Task<List<Aire.Data.Message>> GetMessagesAsync(int conversationId)
            {
                if (conversationId == _throwingConversationId)
                    throw new InvalidOperationException("forced load failure");

                return _databaseService.GetMessagesAsync(conversationId);
            }

            public Task DeleteMessagesByConversationIdAsync(int conversationId) => _databaseService.DeleteMessagesByConversationIdAsync(conversationId);
            public Task DeleteConversationAsync(int conversationId) => _databaseService.DeleteConversationAsync(conversationId);
            public Task DeleteAllConversationsAsync() => _databaseService.DeleteAllConversationsAsync();
            public Task SaveMessageAsync(int conversationId, string role, string content, string? imagePath = null, IEnumerable<Aire.Data.MessageAttachment>? attachments = null)
                => _databaseService.SaveMessageAsync(conversationId, role, content, imagePath, attachments);
        }
    }
}
