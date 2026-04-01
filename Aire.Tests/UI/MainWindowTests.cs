using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
using Xunit;
using UiModels = Aire.UI.MainWindow.Models;
using ProviderChatMessage = Aire.Providers.ChatMessage;

namespace Aire.Tests.UI
{
    [Collection("AppState Isolation")]
    public class MainWindowTests : TestBase
    {
        [Fact]
        public async Task MainWindow_ApiPendingApprovalHelpers_Work()
        {
            AppStartupState.MarkReady();
            // GetUninitializedObject avoids InitializeComponent(); only Messages and the API
            // approval helpers are exercised here — no other constructor state is needed.
            MainWindow window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));

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
                    
                    // GetUninitializedObject avoids InitializeComponent(); required service
                    // fields are injected below.
                    MainWindow window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
                    
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
                // GetUninitializedObject avoids InitializeComponent(); required service
                // fields are injected below.
                MainWindow window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
                window.Messages = new ObservableCollection<UiModels.ChatMessage>();

                // DispatcherObject._dispatcher is null on an uninitialized object; wire it
                // explicitly so Dispatcher.Invoke calls in the tested code resolve correctly.
                FieldInfo dispatcherField = typeof(DispatcherObject).GetField("_dispatcher", BindingFlags.Instance | BindingFlags.NonPublic);
                dispatcherField?.SetValue(window, Application.Current.Dispatcher);

                string tempDir = Path.Combine(Path.GetTempPath(), "aire-tool-tests-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);
                string testFile = Path.Combine(tempDir, "tool.txt");
                File.WriteAllText(testFile, "hello from tool");

                try
                {
                    window._toolExecutionService = new ToolExecutionService(new FileSystemService(), new CommandExecutionService());
                    window.MessagesScrollViewer = new ScrollViewer();
                    SettingsWindow.SetAutoAcceptCache(string.Empty);

                    using JsonDocument emptyParams = JsonDocument.Parse("[]");
                    var execTask = window.ApiExecuteToolAsync("read_file", emptyParams.RootElement, false, 300);
                    var result = execTask.GetAwaiter().GetResult();

                    Assert.Equal("pending_approval", result.Status);
                    Assert.NotNull(result.PendingApprovalIndex);

                    UiModels.ChatMessage msg1 = new UiModels.ChatMessage
                    {
                        PendingToolCall = new ToolCallRequest { Tool = "read_file", Description = "read", RawJson = "{}" }
                    };
                    var denyResult = (ApiToolExecutionResult)window.ProcessApiToolApprovalAsync(msg1, msg1.PendingToolCall, Task.FromResult(false)).GetAwaiter().GetResult();
                    Assert.Equal("denied", denyResult.Status);
                    Assert.Equal("✗ Denied", msg1.ToolCallStatus);

                    var toolReq = new ToolCallRequest
                    {
                        Tool = "read_file",
                        Description = "read_file",
                        Parameters = JsonElementFor(new { path = testFile })
                    };
                    UiModels.ChatMessage msg2 = new UiModels.ChatMessage { PendingToolCall = toolReq };
                    var allowResult = (ApiToolExecutionResult)window.ProcessApiToolApprovalAsync(msg2, toolReq, Task.FromResult(true)).GetAwaiter().GetResult();
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

                    // GetUninitializedObject avoids InitializeComponent(); required service
                    // fields are injected below. The dispatcher is wired explicitly because
                    // DispatcherObject._dispatcher is null on an uninitialized object.
                    MainWindow window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
                    typeof(DispatcherObject).GetField("_dispatcher", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(window, Application.Current.Dispatcher);

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
                    window._settingsWindow = (SettingsWindow)RuntimeHelpers.GetUninitializedObject(typeof(SettingsWindow)); // stub — not used by the tested API methods
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

        private static TaskCompletionSource<bool> TaskCompletionSource_GetValue(bool value)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            tcs.SetResult(value);
            return tcs;
        }
    }
}
