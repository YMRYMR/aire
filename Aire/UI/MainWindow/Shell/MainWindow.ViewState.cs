using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Aire.Services;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;

namespace Aire
{
    public partial class MainWindow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private ObservableCollection<ChatMessage> _messages = new();
        public ObservableCollection<ChatMessage> Messages
        {
            get => _messages;
            set { _messages = value; OnPropertyChanged(); }
        }

        private bool _isThinking;
        public bool IsThinking
        {
            get => _isThinking;
            set
            {
                _isThinking = value;
                OnPropertyChanged();
                ProgressOverlay.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                StopAiButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private TrayIconService? _trayServiceProperty;
        public TrayIconService? TrayService
        {
            get => _trayServiceProperty;
            set
            {
                if (_trayServiceProperty != null)
                    _trayServiceProperty.AttachedToTrayChanged -= OnAttachedToTrayChanged;
                _trayServiceProperty = value;
                if (_trayServiceProperty != null)
                {
                    _trayServiceProperty.AttachedToTrayChanged += OnAttachedToTrayChanged;
                    _trayServiceProperty.IsAttachedToTray = _isAttached;
                }
                UpdateTopmost();
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
