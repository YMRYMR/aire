using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using Aire.Data;
using Aire.Services;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using ImageSource = System.Windows.Media.ImageSource;

namespace Aire.UI.MainWindow.Models;

public class ChatMessage : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Database row id for this message (0 if not persisted or synthetic like Date separators).</summary>
    public int DbMessageId { get; set; }

    public string Sender { get; set; } = string.Empty;

    private string _text = string.Empty;
    public string Text
    {
        get => _text;
        set { _text = value; OnPropertyChanged(); }
    }

    public string Timestamp { get; set; } = string.Empty;
    public DateTime? MessageDate { get; set; }
    public Brush BackgroundBrush { get; set; } = Brushes.Transparent;
    public Brush SenderForeground { get; set; } = Brushes.White;

    private bool _isOrchestratorNarrative;
    public bool IsOrchestratorNarrative
    {
        get => _isOrchestratorNarrative;
        set { _isOrchestratorNarrative = value; OnPropertyChanged(); }
    }

    public TaskCompletionSource<bool>? ApprovalTcs { get; set; }

    private ToolCallRequest? _pendingToolCall;
    public ToolCallRequest? PendingToolCall
    {
        get => _pendingToolCall;
        set
        {
            _pendingToolCall = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDeniedToolCall));
            OnPropertyChanged(nameof(DeniedToolCallText));
            OnPropertyChanged(nameof(DeniedToolCallActionText));
        }
    }

    private bool _isApprovalPending;
    public bool IsApprovalPending
    {
        get => _isApprovalPending;
        set { _isApprovalPending = value; OnPropertyChanged(); }
    }

    private string? _toolCallStatus;
    public string? ToolCallStatus
    {
        get => _toolCallStatus;
        set
        {
            _toolCallStatus = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasToolCallStatus));
            OnPropertyChanged(nameof(IsDeniedToolCall));
            OnPropertyChanged(nameof(DeniedToolCallText));
            OnPropertyChanged(nameof(DeniedToolCallActionText));
        }
    }
    public bool HasToolCallStatus => !string.IsNullOrEmpty(_toolCallStatus);

    public bool IsDeniedToolCall
        => !string.IsNullOrWhiteSpace(_toolCallStatus)
           && _toolCallStatus.StartsWith("✗", StringComparison.Ordinal);

    public string DeniedToolCallText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_toolCallStatus))
                return string.Empty;

            var action = DeniedToolCallActionText;
            if (string.IsNullOrWhiteSpace(action))
                return _toolCallStatus;

            return $"{_toolCallStatus}\n{action}";
        }
    }

    private string? _deniedToolCallActionText;
    public string DeniedToolCallActionText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_deniedToolCallActionText))
                return _deniedToolCallActionText;

            if (!string.IsNullOrWhiteSpace(PendingToolCall?.Description))
                return PendingToolCall!.Description;

            if (!string.IsNullOrWhiteSpace(_toolCallStatus))
            {
                var split = SplitDeniedStatus(_toolCallStatus);
                if (!string.IsNullOrWhiteSpace(split.ActionText))
                    return split.ActionText;
            }

            return DescribeDeniedTool(PendingToolCall?.Tool);
        }
        set
        {
            _deniedToolCallActionText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DeniedToolCallText));
        }
    }

    public static (string StatusText, string ActionText) SplitDeniedStatus(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (string.Empty, string.Empty);

        var firstBreak = text.IndexOf('\n');
        if (firstBreak < 0)
            return (text.Trim(), string.Empty);

        var status = text[..firstBreak].Trim();
        var action = text[(firstBreak + 1)..].Trim();
        if (action.StartsWith("Action:", StringComparison.OrdinalIgnoreCase))
            action = action["Action:".Length..].Trim();

        return (status, action);
    }

    private static string DescribeDeniedTool(string? tool)
        => tool switch
        {
            "execute_command" => "Run a command",
            "read_command_output" => "Read command output",
            "open_url" => "Open a webpage",
            "open_browser_tab" => "Open a webpage",
            "list_directory" => "List a folder",
            "list_files" => "List files",
            "read_file" => "Read a file",
            "write_file" => "Write to a file",
            "write_to_file" => "Write to a file",
            "apply_diff" => "Apply a code change",
            "create_directory" => "Create a folder",
            "delete_file" => "Delete a file",
            "move_file" => "Move a file",
            "search_files" => "Search files",
            "new_task" => "Create a new task",
            "request_context" => "Request more context",
            "attempt_completion" => "Mark the task complete",
            "ask_followup_question" => "Ask a follow-up question",
            "skill" => "Run a skill",
            "switch_mode" => "Switch mode",
            "update_todo_list" => "Update the to-do list",
            "begin_mouse_session" => "Start mouse control",
            "end_mouse_session" => "Stop mouse control",
            "take_screenshot" => "Take a screenshot",
            "mouse_move" => "Move the mouse",
            "mouse_click" => "Click the mouse",
            "mouse_double_click" => "Double-click the mouse",
            "mouse_drag" => "Drag with the mouse",
            "type_text" => "Type text",
            "key_press" => "Press a key",
            "switch_browser_tab" => "Switch browser tabs",
            "close_browser_tab" => "Close a browser tab",
            "get_browser_html" => "Read page HTML",
            "execute_browser_script" => "Run a browser script",
            "get_browser_cookies" => "Read browser cookies",
            "get_clipboard" => "Read the clipboard",
            "set_clipboard" => "Copy to the clipboard",
            "show_notification" => "Show a desktop notification",
            "get_system_info" => "Get system info",
            "get_running_processes" => "List running processes",
            "get_active_window" => "Get the active window",
            "get_selected_text" => "Read selected text",
            "open_file" => "Open a file",
            "remember" => "Remember something",
            "recall" => "Recall something",
            "set_reminder" => "Set a reminder",
            "http_request" => "Send an HTTP request",
            "mouse_scroll" => "Scroll the mouse wheel",
            "search_file_content" => "Search file contents",
            "show_image" => "Show an image",
            "read_emails" => "Read emails",
            "send_email" => "Send an email",
            "search_emails" => "Search emails",
            "reply_to_email" => "Reply to an email",
            _ => string.IsNullOrWhiteSpace(tool) ? string.Empty : tool.Replace('_', ' ')
        };

    private bool _isSearchMatch;
    public bool IsSearchMatch
    {
        get => _isSearchMatch;
        set { _isSearchMatch = value; OnPropertyChanged(); }
    }

    private ImageSource? _screenshotImage;
    public ImageSource? ScreenshotImage
    {
        get => _screenshotImage;
        set { _screenshotImage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasScreenshot)); }
    }
    public bool HasScreenshot => _screenshotImage != null;

    private ImageSource? _attachedImage;
    public ImageSource? AttachedImage
    {
        get => _attachedImage;
        set { _attachedImage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasAttachedImage)); }
    }
    public bool HasAttachedImage => _attachedImage != null;

    private ObservableCollection<MessageAttachment>? _fileAttachments;
    public ObservableCollection<MessageAttachment>? FileAttachments
    {
        get => _fileAttachments;
        set { _fileAttachments = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasFileAttachments)); }
    }
    public bool HasFileAttachments => _fileAttachments?.Count > 0;

    private ObservableCollection<ImageSource>? _inlineImages;
    public ObservableCollection<ImageSource>? InlineImages
    {
        get => _inlineImages;
        set { _inlineImages = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasInlineImages)); }
    }
    public bool HasInlineImages => _inlineImages?.Count > 0;

    private System.Collections.ObjectModel.ObservableCollection<TodoItem>? _todoItems;
    public System.Collections.ObjectModel.ObservableCollection<TodoItem>? TodoItems
    {
        get => _todoItems;
        set { _todoItems = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasTodoItems)); }
    }
    public bool HasTodoItems => _todoItems?.Count > 0;

    private string? _followUpQuestion;
    public string? FollowUpQuestion
    {
        get => _followUpQuestion;
        set { _followUpQuestion = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasFollowUpQuestion)); }
    }

    private List<string>? _followUpOptions;
    public List<string>? FollowUpOptions
    {
        get => _followUpOptions;
        set { _followUpOptions = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasFollowUpOptions)); }
    }

    public bool HasFollowUpQuestion => !string.IsNullOrEmpty(_followUpQuestion);
    public bool HasFollowUpOptions => _followUpOptions?.Count > 0;

    private bool _answerSubmitted;
    public bool AnswerSubmitted
    {
        get => _answerSubmitted;
        set { _answerSubmitted = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowAnswerButtons)); }
    }
    public bool ShowAnswerButtons => HasFollowUpOptions && !_answerSubmitted;

    public TaskCompletionSource<string>? AnswerTcs { get; set; }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
