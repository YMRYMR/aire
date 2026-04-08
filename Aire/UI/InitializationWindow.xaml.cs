using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Aire.Services;

namespace Aire.UI
{
    /// <summary>
    /// Splash window shown at app launch while slow one-time data is loaded into
    /// <see cref="AppStartupCache"/>. The window closes itself when loading finishes.
    /// </summary>
    public partial class InitializationWindow : Window
    {
        private readonly ObservableCollection<string> _startupActions = new();

        public InitializationWindow()
        {
            InitializeComponent();
            ActionItemsControl.ItemsSource = _startupActions;
        }

        private void AppendStatus(string text)
        {
            if (_startupActions.Count == 0 || !string.Equals(_startupActions[^1], text, StringComparison.Ordinal))
            {
                _startupActions.Add(text);
                while (_startupActions.Count > 6)
                {
                    _startupActions.RemoveAt(0);
                }
            }

            StatusText.Text = text;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ActionItemsControl.UpdateLayout();
            }), DispatcherPriority.Background);
        }

        /// <summary>
        /// Shows the window, performs all startup loading tasks, populates
        /// <see cref="AppStartupCache"/>, then closes.
        /// Call with <c>await</c> from <see cref="App.OnStartup"/>.
        /// </summary>
        public async Task RunAndCloseAsync(
            Func<IProgress<string>, Task>? startupWork = null,
            Func<Task>? beforeClose = null)
        {
            Show();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

            try
            {
                var progress = new Progress<string>(AppendStatus);
                await LoadAsync(progress);
                if (startupWork != null)
                {
                    await startupWork(progress);
                }

                if (beforeClose != null)
                {
                    await beforeClose();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("InitializationWindow", "Startup loading failed", ex);
                AppendStatus("Startup warning.");
            }
            finally
            {
                Close();
            }
        }
    }
}
