using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Aire.Tests
{
    public abstract class TestBase
    {
        protected static void EnsureApplication()
        {
            if (Application.Current == null)
                new Application().ShutdownMode = ShutdownMode.OnExplicitShutdown;

            if (!Application.Current.Resources.MergedDictionaries.Any(
                    d => d.Source?.OriginalString.Contains("AppTheme.xaml", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Aire;component/UI/AppTheme.xaml", UriKind.Absolute)
                });
            }
        }

        protected static void RunOnStaThread(Action action)
        {
            Exception? error = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    try { Application.Current?.Shutdown(); } catch { }
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (error != null)
                throw new Exception($"STA thread failed: {error.Message}\n{error.StackTrace}", error);
        }

        protected static void RunOnStaThread(Func<Task> action)
        {
            RunOnStaThread(() =>
            {
                var task = action();
                if (task.IsCompleted)
                {
                    task.GetAwaiter().GetResult();
                    return;
                }

                var frame = new DispatcherFrame();
                task.ContinueWith(
                    _ => frame.Continue = false,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                Dispatcher.PushFrame(frame);
                task.GetAwaiter().GetResult();
            });
        }

        protected static void SelectComboTag(ComboBox comboBox, string tag)
        {
            foreach (ComboBoxItem item in comboBox.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Tag?.ToString(), tag, StringComparison.Ordinal))
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
            throw new InvalidOperationException($"No ComboBoxItem with tag '{tag}' was found.");
        }

        protected static JsonElement JsonElementFor(object value)
            => JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement.Clone();

        protected static void PumpDispatcherUntil(Func<bool> condition, int timeoutMs = 5000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (!condition())
            {
                if (DateTime.UtcNow >= deadline)
                    throw new TimeoutException("Timed out waiting for dispatcher condition.");
                Application.Current?.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                Thread.Sleep(15);
            }
        }
    }
}
