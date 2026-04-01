using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aire.Services.Email;

namespace Aire.UI.Settings.Models
{
    internal sealed class EmailAccountViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void Notify([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public EmailAccount Model { get; }

        public EmailAccountViewModel(EmailAccount model)
        {
            Model = model;
            StatusColor = "#888888";
        }

        public string DisplayName => Model.DisplayName;
        public string ProviderText => Model.Provider.ToString();

        private string _statusColor = "#888888";
        public string StatusColor
        {
            get => _statusColor;
            set
            {
                _statusColor = value;
                Notify();
            }
        }
    }
}
