using System.Windows;
using System.Windows.Controls;
using Aire.Providers;
using Aire.Services;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private void PopulateLanguageComboBox()
        {
            _suppressAppearance = true;
            LanguageComboBox.Items.Clear();
            foreach (var lang in LocalizationService.AvailableLanguages)
            {
                var item = CreateLanguageComboBoxItem(lang.Code, lang.NativeName);
                LanguageComboBox.Items.Add(item);
                if (lang.Code == LocalizationService.CurrentCode)
                {
                    LanguageComboBox.SelectedItem = item;
                }
            }

            _suppressAppearance = false;
        }

        private static ComboBoxItem CreateLanguageComboBoxItem(string code, string nativeName)
        {
            var flag = FlagPainter.Create(code, 22, 14);
            ((FrameworkElement)flag).Margin = new Thickness(0, 0, 7, 0);
            ((FrameworkElement)flag).VerticalAlignment = VerticalAlignment.Center;

            var nameText = new TextBlock { Text = nativeName, VerticalAlignment = VerticalAlignment.Center };

            var panel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            panel.Children.Add(flag);
            panel.Children.Add(nameText);

            return new ComboBoxItem { Tag = code, Content = panel };
        }

        private void OnLanguageChanged() => Dispatcher.Invoke(ApplyLocalization);

        private void ApplyLocalization()
        {
            var L = LocalizationService.S;

            TitleText.Text = L("settings.title", "Settings — Aire");
            CloseButton.ToolTip = L("tooltip.close", "Close");
            TabProviders.Header = L("settings.tab.providers", "AI Providers");
            TabAppearance.Header = L("settings.tab.appearance", "Appearance");
            TabVoice.Header = L("settings.tab.voice", "Voice");
            TabContext.Header = L("settings.tab.context", "Context");
            TabAutoAccept.Header = L("settings.tab.autoAccept", "Auto-accept");
            TabConnections.Header = L("settings.tab.connections", "Connections");
            TabUsage.Header = L("settings.tab.usage", "Usage");
            BrightnessLabel.Text = L("settings.brightness", "Brightness");
            ColorTintLabel.Text = L("settings.colorTint", "Color tint");
            NeutralLeftLabel.Text = L("settings.neutralLeft", "← Neutral");
            NeutralRightLabel.Text = L("settings.neutralRight", "Neutral →");
            AccentBrightnessLabel.Text = L("settings.accentBrightness", "Accent brightness");
            AccentTintLabel.Text = L("settings.accentTint", "Accent color");
            AccentNeutralLeftLabel.Text = L("settings.neutralLeft", "← Neutral");
            AccentNeutralRightLabel.Text = L("settings.neutralRight", "Neutral →");
            FontSizeLabel.Text = L("settings.fontSize", "Font size");
            LanguageLabel.Text = L("settings.language", "Language");
            ApiAccessTitle.Text = L("settings.apiAccessTitle", "Local API access");
            ApiAccessDescription.Text = L("settings.apiAccessDescription",
                "Allow trusted local apps to open Aire, read chat history, and request actions through the local API.");
            ApiAccessEnabledCheckBox.Content = L("settings.apiAccessEnabled", "Enable local API access");
            ApiAccessTokenTitle.Text = L("settings.apiAccessTokenTitle", "Auth token");
            ApiAccessTokenDescription.Text = L("settings.apiAccessTokenDescription",
                "Pass this token in local API requests to authorize control.");
            CopyApiAccessTokenButton.Content = L("settings.apiAccessTokenCopy", "Copy");
            RegenerateApiAccessTokenButton.Content = L("settings.apiAccessTokenRegenerate", "Regenerate");
            NameLabel.Text = L("settings.name", "Name");
            TypeLabel.Text = L("settings.type", "Type");
            ModelLabel.Text = L("settings.model", "Model");
            ApiKeyLabel.Text = L("settings.apiKey", "API Key");
            BaseUrlLabel.Text = L("settings.baseUrl", "Base URL (optional)");
            EnabledCheckBox.Content = L("settings.enabled", "Enabled");
            AddProviderButton.Content = L("settings.addProvider", "+ Add");
            SetupWizardButton.Content = L("settings.setupWizard", "Setup Wizard");
            AnthropicKeyHint.Text = L("settings.anthropicHint",
                "Tip: leave empty to use the ANTHROPIC_API_KEY environment variable.");
            ApiAccessEnabledCheckBox.ToolTip = L("settings.apiAccessEnabledTooltip",
                "Lets other local apps control Aire. Leave off unless you trust the caller.");

            VoiceLocalOnlyCheckBox.Content = L("settings.voiceLocalOnly", "Use local voices only (no internet required)");
            DownloadVoicesButton.Content = L("settings.downloadVoices", "Download Windows voices...");
            VoiceVoiceLabel.Text = L("settings.voice", "Voice");
            VoiceSpeedLabel.Text = L("settings.voiceSpeed", "Speed");
            TestVoiceButton.ToolTip = L("settings.testSelectedVoice", "Test selected voice");
            ContextDescriptionText.Text = L("settings.contextDescription", "Control how much conversation history Aire sends to the provider on each turn, and how caching and summarisation are applied.");
            ContextHistoryHeader.Text = L("settings.contextHistory", "HISTORY");
            MaxMessagesLabel.Text = L("settings.contextMaxMessages", "Window size");
            MaxMessagesSubLabel.Text = L("settings.contextMaxMessagesDescription", "Maximum messages sent to the provider per turn");
            AnchorMessagesLabel.Text = L("settings.contextAnchorMessages", "Anchor messages");
            AnchorMessagesSubLabel.Text = L("settings.contextAnchorMessagesDescription", "Early messages always kept, even when trimming older history");
            ContextCachingHeader.Text = L("settings.contextCaching", "CACHING");
            PromptCachingLabel.Text = L("settings.contextPromptCaching", "Prompt caching");
            PromptCachingSubLabel.Text = L("settings.contextPromptCachingDescription", "Mark stable prefixes cache-friendly when the provider supports it");
            UncachedRecentMessagesLabel.Text = L("settings.contextUncachedRecentMessages", "Uncached tail");
            UncachedRecentMessagesSubLabel.Text = L("settings.contextUncachedRecentMessagesDescription", "Most recent messages always sent fresh, never cache-marked");
            ContextSummariesHeader.Text = L("settings.contextSummaries", "SUMMARIES");
            AutoSummariseLabel.Text = L("settings.contextAutoSummarise", "Auto-summarise");
            AutoSummariseSubLabel.Text = L("settings.contextAutoSummariseDescription", "Condense trimmed turns into a brief summary kept in context");
            SummaryMaxCharactersLabel.Text = L("settings.contextSummaryMaxCharacters", "Summary limit");
            SummaryMaxCharactersSubLabel.Text = L("settings.contextSummaryMaxCharactersDescription", "Maximum characters the condensed summary may occupy");
            ContextHintText.Text = L("settings.contextHint", "System messages and anchor turns are always kept. Cached prefixes only reduce cost on providers that explicitly support prompt caching.");
            RestoreDefaultsButton.Content = L("settings.contextRestoreDefaults", "Restore defaults");
            ContextCompactionHeader.Text = L("settings.contextCompaction", "COMPACTION");
            TokenAwareTruncationLabel.Text = L("settings.contextTokenAwareTruncation", "Token‑aware truncation");
            TokenAwareTruncationSubLabel.Text = L("settings.contextTokenAwareTruncationDescription", "Trim history based on token counts instead of message counts");
            MaxTokensLabel.Text = L("settings.contextMaxTokens", "Max tokens");
            MaxTokensSubLabel.Text = L("settings.contextMaxTokensDescription", "Maximum token count for the conversation window (leave empty for model default)");
            AnchorTokensLabel.Text = L("settings.contextAnchorTokens", "Anchor tokens");
            AnchorTokensSubLabel.Text = L("settings.contextAnchorTokensDescription", "Tokens reserved for early messages");
            TailTokensLabel.Text = L("settings.contextTailTokens", "Tail tokens");
            TailTokensSubLabel.Text = L("settings.contextTailTokensDescription", "Tokens reserved for recent messages");
            ToolFocusWindowLabel.Text = L("settings.contextToolFocusWindow", "Tool‑focus window");
            ToolFocusWindowSubLabel.Text = L("settings.contextToolFocusWindowDescription", "Tighten context window when tool‑use is detected");
            RetryFollowUpWindowLabel.Text = L("settings.contextRetryFollowUpWindow", "Retry‑follow‑up window");
            RetryFollowUpWindowSubLabel.Text = L("settings.contextRetryFollowUpWindowDescription", "Expand context window for retry/follow‑up detection");
            FlowDirection = LocalizationService.IsRightToLeftLanguage(LocalizationService.CurrentCode)
                ? System.Windows.FlowDirection.RightToLeft
                : System.Windows.FlowDirection.LeftToRight;

            UpdateUsageHeaderLocalization();
            UsageLiveProviderText.Text = L("settings.usageNoProviderSelected", "No provider selected");
            UsageLiveUsageText.Text = L("settings.usageNoProviderUsage", "Select a provider in the AI Providers tab to see live quota or spend.");
            UsageLiveUsageDetailText.Text = L("settings.usageHistoricalNote", "Historical totals below still track stored assistant turns.");
            TabUsage.Header = L("settings.tab.usage", "Usage");

            // Auto-accept
            AutoAcceptCautionTitle.Text = L("settings.autoAcceptCautionTitle", "⚠  Caution");
            AutoAcceptCautionText.Text = L("settings.autoAcceptCaution", "Letting AIs act unsupervised can be dangerous. Only enable tools you fully understand.");
            AutoAcceptEnabledCheckBox.Content = L("settings.autoAcceptEnabled", "Enable auto\u2011accept for selected tools");
            AutoAcceptInstructionText.Text = L("settings.autoAcceptSelectTools", "Select which tools can be executed without confirmation:");
            AutoAcceptWebBrowserHeader.Text = L("autoAccept.category.webBrowser", "Web / Browser");
            AutoAcceptCommandsHeader.Text = L("autoAccept.category.commands", "Commands");
            AutoAcceptFileSystemHeader.Text = L("autoAccept.category.fileSystem", "File System");
            AutoAcceptSystemClipboardHeader.Text = L("autoAccept.category.systemClipboard", "System / Clipboard");
            AutoAcceptMemoryHeader.Text = L("autoAccept.category.memory", "Memory");
            AutoAcceptEmailHeader.Text = L("autoAccept.category.email", "Email");
            AutoAcceptAiTaskFlowHeader.Text = L("autoAccept.category.aiTaskFlow", "AI / Task flow");
            AutoAcceptMouseKeyboardHeader.Text = L("autoAccept.category.mouseKeyboard", "Mouse / Keyboard");
            AutoAcceptOpenUrlCheckBox.Content = L("autoAccept.tool.browseWeb", "Browse web (fetch URL)");
            AutoAcceptOpenUrlCheckBox.ToolTip = L("autoAccept.tooltip.browseWeb", "Fetches a web page and returns its text to the AI. Read-only and generally safe.");
            AutoAcceptHttpRequestCheckBox.Content = L("autoAccept.tool.httpRequest", "HTTP request");
            AutoAcceptHttpRequestCheckBox.ToolTip = L("autoAccept.tooltip.httpRequest", "Sends an arbitrary HTTP request (GET, POST, PUT, DELETE, etc.) to any URL. Can submit forms, call APIs, or trigger side effects on remote servers.");
            AutoAcceptOpenBrowserTabCheckBox.Content = L("autoAccept.tool.openBrowserTab", "Open browser tab");
            AutoAcceptOpenBrowserTabCheckBox.ToolTip = L("autoAccept.tooltip.openBrowserTab", "Opens a URL in the Aire browser window, which is visible to you. Generally safe.");
            AutoAcceptListBrowserTabsCheckBox.Content = L("autoAccept.tool.listBrowserTabs", "List browser tabs");
            AutoAcceptListBrowserTabsCheckBox.ToolTip = L("autoAccept.tooltip.listBrowserTabs", "Lists the titles and URLs of all open tabs in the Aire browser. Read-only and safe.");
            AutoAcceptReadBrowserTabCheckBox.Content = L("autoAccept.tool.readBrowserTab", "Read browser tab");
            AutoAcceptReadBrowserTabCheckBox.ToolTip = L("autoAccept.tooltip.readBrowserTab", "Reads the rendered text content of an open browser tab. Read-only and safe.");
            AutoAcceptSwitchBrowserTabCheckBox.Content = L("autoAccept.tool.switchBrowserTab", "Switch browser tab");
            AutoAcceptSwitchBrowserTabCheckBox.ToolTip = L("autoAccept.tooltip.switchBrowserTab", "Changes which tab is active in the Aire browser. Safe.");
            AutoAcceptCloseBrowserTabCheckBox.Content = L("autoAccept.tool.closeBrowserTab", "Close browser tab");
            AutoAcceptCloseBrowserTabCheckBox.ToolTip = L("autoAccept.tooltip.closeBrowserTab", "Closes one of the open tabs in the Aire browser. Safe, but closes content you may want to keep.");
            AutoAcceptGetBrowserHtmlCheckBox.Content = L("autoAccept.tool.getBrowserHtml", "Get browser HTML");
            AutoAcceptGetBrowserHtmlCheckBox.ToolTip = L("autoAccept.tooltip.getBrowserHtml", "Returns the raw HTML source of an open browser tab. Read-only and safe.");
            AutoAcceptExecuteBrowserScriptCheckBox.Content = L("autoAccept.tool.executeBrowserScript", "Execute browser script");
            AutoAcceptExecuteBrowserScriptCheckBox.ToolTip = L("autoAccept.tooltip.executeBrowserScript", "Runs arbitrary JavaScript in an open browser tab. High risk \u2014 can read cookies, submit forms, or interact with any page element.");
            AutoAcceptGetBrowserCookiesCheckBox.Content = L("autoAccept.tool.getBrowserCookies", "Get browser cookies");
            AutoAcceptGetBrowserCookiesCheckBox.ToolTip = L("autoAccept.tooltip.getBrowserCookies", "Reads cookies from an open browser tab. Can expose session tokens and authentication credentials.");
            AutoAcceptExecuteCommandCheckBox.Content = L("autoAccept.tool.executeCommand", "Execute command");
            AutoAcceptExecuteCommandCheckBox.ToolTip = L("autoAccept.tooltip.executeCommand", "Runs a shell command or launches any installed application without asking you first. High risk \u2014 only enable if you trust the AI to run commands autonomously.");
            AutoAcceptReadCommandOutputCheckBox.Content = L("autoAccept.tool.readCommandOutput", "Read command output");
            AutoAcceptReadCommandOutputCheckBox.ToolTip = L("autoAccept.tooltip.readCommandOutput", "Reads the output of a previously started background process. Generally safe.");
            AutoAcceptListFilesCheckBox.Content = L("autoAccept.tool.listFiles", "List files");
            AutoAcceptListFilesCheckBox.ToolTip = L("autoAccept.tooltip.listFiles", "Lists the contents of a folder. Read-only and safe.");
            AutoAcceptReadFileCheckBox.Content = L("autoAccept.tool.readFile", "Read file");
            AutoAcceptReadFileCheckBox.ToolTip = L("autoAccept.tooltip.readFile", "Opens and reads the contents of a file. Read-only and safe.");
            AutoAcceptSearchFilesCheckBox.Content = L("autoAccept.tool.searchFiles", "Search files");
            AutoAcceptSearchFilesCheckBox.ToolTip = L("autoAccept.tooltip.searchFiles", "Searches for files by name pattern inside a folder. Read-only and safe.");
            AutoAcceptSearchFileContentCheckBox.Content = L("autoAccept.tool.searchFileContent", "Search file content");
            AutoAcceptSearchFileContentCheckBox.ToolTip = L("autoAccept.tooltip.searchFileContent", "Searches for text patterns inside file contents (like grep). Read-only and safe.");
            AutoAcceptWriteToFileCheckBox.Content = L("autoAccept.tool.writeToFile", "Write to file");
            AutoAcceptWriteToFileCheckBox.ToolTip = L("autoAccept.tooltip.writeToFile", "Creates or overwrites a file with new content. Can modify or delete existing data.");
            AutoAcceptApplyDiffCheckBox.Content = L("autoAccept.tool.applyDiff", "Apply code diff");
            AutoAcceptApplyDiffCheckBox.ToolTip = L("autoAccept.tooltip.applyDiff", "Applies a patch to modify lines in an existing file. Can silently change source code or config files.");
            AutoAcceptCreateDirectoryCheckBox.Content = L("autoAccept.tool.createDirectory", "Create directory");
            AutoAcceptCreateDirectoryCheckBox.ToolTip = L("autoAccept.tooltip.createDirectory", "Creates a new folder. Generally safe.");
            AutoAcceptDeleteFileCheckBox.Content = L("autoAccept.tool.deleteFile", "Delete file");
            AutoAcceptDeleteFileCheckBox.ToolTip = L("autoAccept.tooltip.deleteFile", "Permanently deletes a file. High risk \u2014 deleted files may not be recoverable.");
            AutoAcceptMoveFileCheckBox.Content = L("autoAccept.tool.moveFile", "Move / rename file");
            AutoAcceptMoveFileCheckBox.ToolTip = L("autoAccept.tooltip.moveFile", "Moves or renames a file or folder. Can overwrite existing files at the destination.");
            AutoAcceptOpenFileCheckBox.Content = L("autoAccept.tool.openFile", "Open file");
            AutoAcceptOpenFileCheckBox.ToolTip = L("autoAccept.tooltip.openFile", "Opens a file with its default application (e.g. opens a PDF in your PDF viewer). Safe.");
            AutoAcceptGetClipboardCheckBox.Content = L("autoAccept.tool.getClipboard", "Get clipboard");
            AutoAcceptGetClipboardCheckBox.ToolTip = L("autoAccept.tooltip.getClipboard", "Reads the current clipboard contents. Can expose sensitive data you have copied.");
            AutoAcceptSetClipboardCheckBox.Content = L("autoAccept.tool.setClipboard", "Set clipboard");
            AutoAcceptSetClipboardCheckBox.ToolTip = L("autoAccept.tooltip.setClipboard", "Overwrites your clipboard with AI-generated text. Safe, but replaces whatever you had copied.");
            AutoAcceptNotifyCheckBox.Content = L("autoAccept.tool.notify", "Show notification");
            AutoAcceptNotifyCheckBox.ToolTip = L("autoAccept.tooltip.notify", "Displays a system notification. Safe.");
            AutoAcceptGetSystemInfoCheckBox.Content = L("autoAccept.tool.getSystemInfo", "Get system info");
            AutoAcceptGetSystemInfoCheckBox.ToolTip = L("autoAccept.tooltip.getSystemInfo", "Reads OS, hardware, and environment information. Read-only and safe.");
            AutoAcceptGetRunningProcessesCheckBox.Content = L("autoAccept.tool.getRunningProcesses", "List running processes");
            AutoAcceptGetRunningProcessesCheckBox.ToolTip = L("autoAccept.tooltip.getRunningProcesses", "Returns a list of processes currently running on your machine. Read-only and safe.");
            AutoAcceptGetActiveWindowCheckBox.Content = L("autoAccept.tool.getActiveWindow", "Get active window");
            AutoAcceptGetActiveWindowCheckBox.ToolTip = L("autoAccept.tooltip.getActiveWindow", "Returns the title and process of the currently focused window. Read-only and safe.");
            AutoAcceptGetSelectedTextCheckBox.Content = L("autoAccept.tool.getSelectedText", "Get selected text");
            AutoAcceptGetSelectedTextCheckBox.ToolTip = L("autoAccept.tooltip.getSelectedText", "Reads whatever text is currently selected on screen. Read-only and safe.");
            AutoAcceptRememberCheckBox.Content = L("autoAccept.tool.remember", "Remember");
            AutoAcceptRememberCheckBox.ToolTip = L("autoAccept.tooltip.remember", "Saves a key-value pair to the AI's persistent memory store. Safe.");
            AutoAcceptRecallCheckBox.Content = L("autoAccept.tool.recall", "Recall");
            AutoAcceptRecallCheckBox.ToolTip = L("autoAccept.tooltip.recall", "Reads a value from the AI's persistent memory store. Read-only and safe.");
            AutoAcceptSetReminderCheckBox.Content = L("autoAccept.tool.setReminder", "Set reminder");
            AutoAcceptSetReminderCheckBox.ToolTip = L("autoAccept.tooltip.setReminder", "Schedules a reminder notification to appear after a delay. Safe.");
            AutoAcceptReadEmailsCheckBox.Content = L("autoAccept.tool.readEmails", "Read emails");
            AutoAcceptReadEmailsCheckBox.ToolTip = L("autoAccept.tooltip.readEmails", "Reads emails from your connected mail account. Can expose private correspondence.");
            AutoAcceptSearchEmailsCheckBox.Content = L("autoAccept.tool.searchEmails", "Search emails");
            AutoAcceptSearchEmailsCheckBox.ToolTip = L("autoAccept.tooltip.searchEmails", "Searches your mailbox for matching messages. Read-only.");
            AutoAcceptSendEmailCheckBox.Content = L("autoAccept.tool.sendEmail", "Send email");
            AutoAcceptSendEmailCheckBox.ToolTip = L("autoAccept.tooltip.sendEmail", "Composes and sends an email on your behalf. High risk \u2014 emails are delivered immediately and may be irreversible.");
            AutoAcceptReplyToEmailCheckBox.Content = L("autoAccept.tool.replyToEmail", "Reply to email");
            AutoAcceptReplyToEmailCheckBox.ToolTip = L("autoAccept.tooltip.replyToEmail", "Sends a reply to an existing email thread. High risk \u2014 same as send email.");
            AutoAcceptNewTaskCheckBox.Content = L("autoAccept.tool.newTask", "Create new task");
            AutoAcceptNewTaskCheckBox.ToolTip = L("autoAccept.tooltip.newTask", "Starts a new sub-task. The AI may chain many tool calls inside it without further prompts.");
            AutoAcceptAskFollowupQuestionCheckBox.Content = L("autoAccept.tool.askFollowupQuestion", "Ask follow-up question");
            AutoAcceptAskFollowupQuestionCheckBox.ToolTip = L("autoAccept.tooltip.askFollowupQuestion", "The AI pauses to ask you a clarifying question before continuing. Generally safe.");
            AutoAcceptAttemptCompletionCheckBox.Content = L("autoAccept.tool.attemptCompletion", "Mark task as complete");
            AutoAcceptAttemptCompletionCheckBox.ToolTip = L("autoAccept.tooltip.attemptCompletion", "The AI signals it has finished the current task and presents a summary. Safe.");
            AutoAcceptSkillCheckBox.Content = L("autoAccept.tool.skill", "Run skill");
            AutoAcceptSkillCheckBox.ToolTip = L("autoAccept.tooltip.skill", "Executes a built-in skill or macro. Risk depends on what the skill does.");
            AutoAcceptSwitchModeCheckBox.Content = L("autoAccept.tool.switchMode", "Switch mode");
            AutoAcceptSwitchModeCheckBox.ToolTip = L("autoAccept.tooltip.switchMode", "Changes the AI's operating mode (e.g. from chat to coding assistant). Generally safe.");
            AutoAcceptSwitchModelCheckBox.Content = L("autoAccept.tool.switchModel", "Switch model");
            AutoAcceptSwitchModelCheckBox.ToolTip = L("autoAccept.tooltip.switchModel", "Switches to a different AI model or provider mid-conversation. Generally safe.");
            AutoAcceptUpdateTodoListCheckBox.Content = L("autoAccept.tool.updateTodoList", "Update to-do list");
            AutoAcceptUpdateTodoListCheckBox.ToolTip = L("autoAccept.tooltip.updateTodoList", "Adds, updates, or checks off items in the task to-do list. Safe.");
            AutoAcceptShowImageCheckBox.Content = L("autoAccept.tool.showImage", "Show image");
            AutoAcceptShowImageCheckBox.ToolTip = L("autoAccept.tooltip.showImage", "Displays an image from a file path or URL in the chat. Safe.");
            AutoAcceptMouseToolsCheckBox.Content = L("autoAccept.tool.mouseTools", "Mouse tools (move, click, scroll)");
            AutoAcceptMouseToolsCheckBox.ToolTip = L("autoAccept.tooltip.mouseTools", "Moves the cursor and clicks anywhere on screen. High risk \u2014 can interact with any application or UI element without you seeing it first.");
            AutoAcceptKeyboardToolsCheckBox.Content = L("autoAccept.tool.keyboardTools", "Keyboard tools (type text, key combos)");
            AutoAcceptKeyboardToolsCheckBox.ToolTip = L("autoAccept.tooltip.keyboardTools", "Types text or presses key combinations into the focused window. High risk \u2014 can send input to any application.");

            // MCP Connections
            McpServersTitle.Text = L("mcp.servers", "MCP Servers");
            McpServersDescription.Text = L("mcp.serversDescription", "Extend the AI with custom tools by connecting MCP servers. Changes take effect immediately.");
            McpTipText.Text = L("mcp.tip", "Most servers require Node.js (npx). If a server fails to start, make sure Node.js is installed \u2014 nodejs.org");
            McpCatalogTitle.Text = L("mcp.discover", "Discover");
            McpInstalledServersTitle.Text = L("mcp.installedServers", "Installed Servers");
            AddMcpBtn.Content = L("mcp.addCustom", "+ Custom");
            McpNameLabel.Text = L("mcp.nameLabel", "Name");
            McpCommandLabel.Text = L("mcp.commandLabel", "Command");
            McpArgsLabel.Text = L("mcp.argsLabel", "Arguments");
            McpWorkDirLabel.Text = L("mcp.workDirLabel", "Working directory (optional)");
            McpEnvVarsLabel.Text = L("mcp.envVarsLabel", "Environment variables \u2014 one per line:  KEY=value");

            AutoAcceptProfileLabel.Text = L("settings.profile", "Profile");
            ApplyAutoAcceptProfileButton.Content = L("settings.apply", "Apply");
            SaveAutoAcceptProfileButton.Content = L("settings.saveAs", "Save as");
            DeleteAutoAcceptProfileButton.Content = L("settings.delete", "Delete");

            if (_selectedProvider == null)
            {
                EditPanelTitle.Text = L("settings.selectProvider", "Select a provider");
            }

            if (_selectedProvider != null)
            {
                var meta = ProviderFactory.GetMetadata(_selectedProvider.Type);
                ApplyProviderMetadata(meta, hasKey: !string.IsNullOrEmpty(_selectedProvider.ApiKey));
            }
        }
    }
}
