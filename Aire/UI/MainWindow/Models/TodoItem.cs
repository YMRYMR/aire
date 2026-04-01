using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Aire.UI.MainWindow.Models;

public class TodoItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    private string _status = "pending";
    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusIcon));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(IsCompleted));
        }
    }

    public string StatusIcon => Status switch
    {
        "completed" => "✓",
        "in_progress" => "◌",
        "blocked" => "✗",
        _ => "○",
    };

    public string StatusColor => Status switch
    {
        "completed" => "#5A9A5A",
        "in_progress" => "#C8A040",
        "blocked" => "#9A4444",
        _ => "#707070",
    };

    public bool IsCompleted => Status == "completed";

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
