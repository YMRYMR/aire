using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Aire.Data
{
    /// <summary>
    /// Lightweight view model used by the conversation history sidebar.
    /// </summary>
    public class ConversationSummary : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public string ProviderColor { get; set; } = "#888888";
        public string AssistantModeKey { get; set; } = "general";
        public string AssistantModeDisplayName
            => AssistantModeKey switch
            {
                "developer" => "Developer",
                "creative-writer" => "Creative writer",
                "architect" => "Architect",
                "teacher" => "Teacher",
                "security" => "Security",
                "scientist" => "Scientist",
                "psicologist" => "Psicologist",
                "philosopher" => "Philosopher",
                _ => "General"
            };

        public string RelativeDate
        {
            get
            {
                var today = DateTime.Today;
                var days = (today - UpdatedAt.Date).TotalDays;
                return days < 1 ? "Today"
                    : days < 2 ? "Yesterday"
                    : days < 7 ? UpdatedAt.ToString("dddd")
                    : UpdatedAt.Year == today.Year ? UpdatedAt.ToString("MMM d")
                    : UpdatedAt.ToString("MMM d, yyyy");
            }
        }

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                _isEditing = value;
                Notify();
            }
        }

        private string _editingTitle = string.Empty;
        public string EditingTitle
        {
            get => _editingTitle;
            set
            {
                _editingTitle = value;
                Notify();
            }
        }

        private void Notify([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
