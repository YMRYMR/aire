using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using Aire.Services;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using ImageSource = System.Windows.Media.ImageSource;

namespace Aire.UI.MainWindow.Models;

public class ChatMessage : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

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

    public TaskCompletionSource<bool>? ApprovalTcs { get; set; }

    private ToolCallRequest? _pendingToolCall;
    public ToolCallRequest? PendingToolCall
    {
        get => _pendingToolCall;
        set { _pendingToolCall = value; OnPropertyChanged(); }
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
        set { _toolCallStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasToolCallStatus)); }
    }
    public bool HasToolCallStatus => !string.IsNullOrEmpty(_toolCallStatus);

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
