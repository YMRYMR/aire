using System;
using System.Windows.Data;
using System.Windows.Threading;
using System.Windows.Input;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace Aire.UI.Controls
{
    /// <summary>
    /// Shared editable-combobox behavior for type-to-filter pickers used in onboarding and settings.
    /// </summary>
    internal static class EditableComboBoxFilterHelper
    {
        /// <summary>
        /// Forwards typed characters into the editable textbox while the dropdown is open.
        /// </summary>
        /// <param name="comboBox">Editable combo box receiving preview input.</param>
        /// <param name="e">Current text-input event.</param>
        public static void HandlePreviewTextInput(WpfComboBox comboBox, TextCompositionEventArgs e)
        {
            if (!comboBox.IsEditable || !comboBox.IsDropDownOpen || string.IsNullOrEmpty(e.Text))
                return;

            var textBox = GetEditableTextBox(comboBox);
            if (textBox == null)
                return;

            textBox.Focus();
            ReplaceSelection(textBox, e.Text);
            e.Handled = true;
        }

        /// <summary>
        /// Handles destructive keys like Backspace and Delete while the dropdown is open.
        /// </summary>
        /// <param name="comboBox">Editable combo box receiving preview key input.</param>
        /// <param name="e">Current key event.</param>
        public static void HandlePreviewKeyDown(WpfComboBox comboBox, System.Windows.Input.KeyEventArgs e)
        {
            if (!comboBox.IsEditable || !comboBox.IsDropDownOpen)
                return;

            var textBox = GetEditableTextBox(comboBox);
            if (textBox == null)
                return;

            switch (e.Key)
            {
                case Key.Back:
                    textBox.Focus();
                    DeletePrevious(textBox);
                    e.Handled = true;
                    break;
                case Key.Delete:
                    textBox.Focus();
                    DeleteNext(textBox);
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// Focuses the editable textbox created by the combo box template.
        /// </summary>
        /// <param name="comboBox">Editable combo box whose textbox should receive focus.</param>
        /// <param name="selectAll">Whether the current text should be fully selected after focus.</param>
        public static void FocusEditableTextBox(WpfComboBox comboBox, bool selectAll = true)
        {
            comboBox.Dispatcher.BeginInvoke((Action)(() =>
            {
                var textBox = GetEditableTextBox(comboBox);
                if (textBox == null)
                    return;

                textBox.Focus();
                if (selectAll)
                    textBox.SelectAll();
            }), DispatcherPriority.Background);
        }

        /// <summary>
        /// Applies a live filter to the combo-box view using one or two candidate text fields.
        /// </summary>
        /// <typeparam name="T">Item type held by the combo box.</typeparam>
        /// <param name="comboBox">Editable combo box being filtered.</param>
        /// <param name="suppressFilter">Whether filtering is temporarily suppressed by the caller.</param>
        /// <param name="previousSelection">Last selected item, retained so it can be restored after filtering.</param>
        /// <param name="primaryText">Primary text selector used for matching typed input.</param>
        /// <param name="secondaryText">Optional secondary text selector used for matching typed input.</param>
        public static void ApplyFilter<T>(
            WpfComboBox comboBox,
            bool suppressFilter,
            ref T? previousSelection,
            Func<T, string?> primaryText,
            Func<T, string?>? secondaryText = null)
            where T : class
        {
            if (suppressFilter || comboBox.ItemsSource == null || !comboBox.IsDropDownOpen)
                return;

            var typed = comboBox.Text;
            var view = CollectionViewSource.GetDefaultView(comboBox.ItemsSource);

            if (string.IsNullOrWhiteSpace(typed))
            {
                view.Filter = null;
            }
            else
            {
                previousSelection ??= comboBox.SelectedItem as T;
                view.Filter = obj =>
                {
                    if (obj is not T item)
                        return false;

                    return Contains(primaryText(item), typed)
                        || Contains(secondaryText?.Invoke(item), typed);
                };
            }

            comboBox.IsDropDownOpen = true;
        }

        /// <summary>
        /// Removes any active filter and restores the previous selection when appropriate.
        /// </summary>
        /// <typeparam name="T">Item type held by the combo box.</typeparam>
        /// <param name="comboBox">Editable combo box whose view should be reset.</param>
        /// <param name="suppressFilter">Whether filtering is temporarily suppressed by the caller.</param>
        /// <param name="previousSelection">Last selected item recorded before filtering began.</param>
        /// <param name="restoreText">Text projection used when restoring the previous selection.</param>
        public static void ResetFilter<T>(
            WpfComboBox comboBox,
            ref bool suppressFilter,
            ref T? previousSelection,
            Func<T, string> restoreText)
            where T : class
        {
            if (comboBox.ItemsSource == null)
                return;

            var view = CollectionViewSource.GetDefaultView(comboBox.ItemsSource);
            if (view.Filter == null)
                return;

            suppressFilter = true;
            view.Filter = null;
            suppressFilter = false;

            if (comboBox.SelectedItem == null && previousSelection != null)
            {
                suppressFilter = true;
                comboBox.SelectedItem = previousSelection;
                comboBox.Text = restoreText(previousSelection);
                suppressFilter = false;
            }

            previousSelection = null;
        }

        /// <summary>
        /// Case-insensitive substring matcher used by the live filter.
        /// </summary>
        private static bool Contains(string? candidate, string typed)
            => !string.IsNullOrWhiteSpace(candidate)
            && candidate.Contains(typed, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Looks up the editable textbox generated by the combo box control template.
        /// </summary>
        private static WpfTextBox? GetEditableTextBox(WpfComboBox comboBox)
            => comboBox.Template?.FindName("PART_EditableTextBox", comboBox) as WpfTextBox;

        /// <summary>
        /// Replaces the current textbox selection with new text and moves the caret after the inserted content.
        /// </summary>
        private static void ReplaceSelection(WpfTextBox textBox, string text)
        {
            var start = textBox.SelectionStart;
            var length = textBox.SelectionLength;
            var current = textBox.Text ?? string.Empty;

            textBox.Text = current.Remove(start, length).Insert(start, text);
            textBox.CaretIndex = start + text.Length;
            textBox.SelectionLength = 0;
        }

        /// <summary>
        /// Deletes the current selection or the character immediately before the caret.
        /// </summary>
        private static void DeletePrevious(WpfTextBox textBox)
        {
            if (textBox.SelectionLength > 0)
            {
                ReplaceSelection(textBox, string.Empty);
                return;
            }

            if (textBox.CaretIndex <= 0)
                return;

            var start = textBox.CaretIndex - 1;
            textBox.Text = textBox.Text.Remove(start, 1);
            textBox.CaretIndex = start;
        }

        /// <summary>
        /// Deletes the current selection or the character immediately after the caret.
        /// </summary>
        private static void DeleteNext(WpfTextBox textBox)
        {
            if (textBox.SelectionLength > 0)
            {
                ReplaceSelection(textBox, string.Empty);
                return;
            }

            if (textBox.CaretIndex >= textBox.Text.Length)
                return;

            textBox.Text = textBox.Text.Remove(textBox.CaretIndex, 1);
        }
    }
}
