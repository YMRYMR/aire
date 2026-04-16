using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Aire.UI
{
    /// <summary>
    /// Shows a rotating placeholder message inside an empty text box.
    /// </summary>
    internal sealed class RotatingWatermarkHelper : IDisposable
    {
        private readonly System.Windows.Controls.TextBox _textBox;
        private readonly System.Windows.Controls.TextBlock _watermark;
        private readonly IReadOnlyList<string> _examples;
        private readonly DispatcherTimer _timer;
        private int _exampleIndex;
        private bool _isDisposed;

        public RotatingWatermarkHelper(System.Windows.Controls.TextBox textBox, System.Windows.Controls.TextBlock watermark, IEnumerable<string> examples, TimeSpan? interval = null)
        {
            _textBox = textBox;
            _watermark = watermark;
            _examples = examples
                .Where(example => !string.IsNullOrWhiteSpace(example))
                .Select(example => example.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (_examples.Count == 0)
                throw new ArgumentException("At least one watermark example is required.", nameof(examples));

            _timer = new DispatcherTimer
            {
                Interval = interval ?? TimeSpan.FromSeconds(3.5)
            };
            _timer.Tick += Timer_Tick;

            _textBox.TextChanged += TextBox_StateChanged;
            _textBox.GotKeyboardFocus += TextBox_StateChanged;
            _textBox.LostKeyboardFocus += TextBox_StateChanged;
            _textBox.Loaded += TextBox_StateChanged;

            UpdateWatermark();
            _timer.Start();
        }

        private void TextBox_StateChanged(object? sender, RoutedEventArgs e)
            => UpdateWatermark();

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!ShouldShowWatermark())
                return;

            _exampleIndex = (_exampleIndex + 1) % _examples.Count;
            _watermark.Text = _examples[_exampleIndex];
        }

        private bool ShouldShowWatermark()
            => string.IsNullOrWhiteSpace(_textBox.Text);

        private void UpdateWatermark()
        {
            if (ShouldShowWatermark())
            {
                _watermark.Visibility = Visibility.Visible;
                _watermark.Text = _examples[_exampleIndex];
            }
            else
            {
                _watermark.Visibility = Visibility.Collapsed;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
            _textBox.TextChanged -= TextBox_StateChanged;
            _textBox.GotKeyboardFocus -= TextBox_StateChanged;
            _textBox.LostKeyboardFocus -= TextBox_StateChanged;
            _textBox.Loaded -= TextBox_StateChanged;
        }
    }
}
