using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Controls;
using WpfPen = System.Windows.Media.Pen;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace Aire.UI
{
    internal sealed class TextProofingAdorner : Adorner
    {
        private static readonly Regex WordRegex = new(@"[\p{L}][\p{L}\p{Mn}\p{Pd}'’]*", RegexOptions.Compiled);
        private static readonly HashSet<string> IgnoreWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "md", "txt", "pdf", "png", "jpg", "jpeg", "gif", "json", "xml", "html", "css", "js", "ts", "cs", "py", "rb",
            "rs", "c", "cpp", "sh", "sql", "zip", "tar", "gz", "rar", "doc", "docx", "xls", "xlsx", "ppt", "pptx"
        };

        private readonly WpfTextBox _textBox;
        private IReadOnlyList<(int Start, int Length)> _misspellings = Array.Empty<(int Start, int Length)>();
        private string _uiLanguageCode = "en";
        private bool _refreshPending;

        public TextProofingAdorner(WpfTextBox adornedElement) : base(adornedElement)
        {
            _textBox = adornedElement;
            IsHitTestVisible = false;
        }

        public void SetLanguage(string uiLanguageCode)
        {
            _uiLanguageCode = uiLanguageCode;
            RequestRefresh();
        }

        public void RequestRefresh()
        {
            if (_refreshPending)
                return;

            _refreshPending = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _refreshPending = false;
                RecomputeMisspellings();
                InvalidateVisual();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (_misspellings.Count == 0)
                return;

            var pen = new WpfPen(new SolidColorBrush(WpfColor.FromRgb(0xD7, 0x3A, 0x49)), 1.1);
            pen.Freeze();

            foreach (var (start, length) in _misspellings)
            {
                DrawUnderline(drawingContext, pen, start, length);
            }
        }

        private void RecomputeMisspellings()
        {
            var text = _textBox.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                _misspellings = Array.Empty<(int, int)>();
                return;
            }

            var spellChecker = TextProofingService.GetChecker(_uiLanguageCode);
            var misses = new List<(int Start, int Length)>();

            foreach (Match match in WordRegex.Matches(text))
            {
                var word = match.Value;
                if (word.Length <= 1 || IgnoreWords.Contains(word))
                    continue;

                if (!spellChecker.IsWordCorrect(word))
                    misses.Add((match.Index, match.Length));
            }

            _misspellings = misses;
        }

        private void DrawUnderline(DrawingContext drawingContext, WpfPen pen, int startIndex, int length)
        {
            var textLength = _textBox.Text?.Length ?? 0;
            if (textLength <= 0)
                return;

            if (startIndex < 0 || startIndex >= textLength)
                return;

            var safeLength = Math.Min(length, textLength - startIndex);
            if (safeLength <= 0)
                return;

            var startRect = _textBox.GetRectFromCharacterIndex(startIndex, trailingEdge: false);
            var endRect = _textBox.GetRectFromCharacterIndex(startIndex + Math.Max(0, safeLength - 1), trailingEdge: true);

            if (startRect.IsEmpty || endRect.IsEmpty)
                return;

            // Split across wrapped lines if necessary.
            var currentLineTop = double.NaN;
            var lineStartX = startRect.Left;
            var lineEndX = endRect.Right;

            for (var i = 0; i < safeLength; i++)
            {
                var rect = _textBox.GetRectFromCharacterIndex(startIndex + i, trailingEdge: i == safeLength - 1);
                if (rect.IsEmpty)
                    continue;

                if (double.IsNaN(currentLineTop))
                {
                    currentLineTop = rect.Top;
                    lineStartX = rect.Left;
                    lineEndX = rect.Right;
                    continue;
                }

                if (Math.Abs(rect.Top - currentLineTop) > 1.0)
                {
                    DrawLine(drawingContext, pen, lineStartX, lineEndX, currentLineTop);
                    currentLineTop = rect.Top;
                    lineStartX = rect.Left;
                    lineEndX = rect.Right;
                }
                else
                {
                    lineEndX = rect.Right;
                }
            }

            if (!double.IsNaN(currentLineTop))
                DrawLine(drawingContext, pen, lineStartX, lineEndX, currentLineTop);
        }

        private static void DrawLine(DrawingContext drawingContext, WpfPen pen, double startX, double endX, double top)
        {
            if (endX <= startX)
                return;

            var y = top + 15;
            drawingContext.DrawLine(pen, new WpfPoint(startX, y), new WpfPoint(endX, y));
        }
    }
}
