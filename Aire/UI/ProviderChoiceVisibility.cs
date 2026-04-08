using System;
using System.Windows.Controls;
using Aire.Providers;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace Aire.UI
{
    internal static class ProviderChoiceVisibility
    {
        internal static void PruneHiddenChoices(WpfComboBox comboBox)
            => PruneHiddenChoices(comboBox, ProviderVisibility.ShowClaudeWebProvider);

        internal static void PruneHiddenChoices(WpfComboBox comboBox, bool showClaudeWebProvider)
        {
            if (comboBox == null)
                throw new ArgumentNullException(nameof(comboBox));

            if (showClaudeWebProvider)
                return;

            string? selectedTag = (comboBox.SelectedItem as WpfComboBoxItem)?.Tag as string;
            bool selectedTagHidden = !string.IsNullOrWhiteSpace(selectedTag) && ProviderVisibility.IsHiddenFromRelease(selectedTag);
            bool removedSelectedTag = false;

            for (int index = comboBox.Items.Count - 1; index >= 0; index--)
            {
                if (comboBox.Items[index] is WpfComboBoxItem item &&
                    item.Tag is string itemTag &&
                    ProviderVisibility.IsHiddenFromRelease(itemTag))
                {
                    removedSelectedTag |= selectedTagHidden &&
                        string.Equals(itemTag, selectedTag, StringComparison.OrdinalIgnoreCase);
                    comboBox.Items.RemoveAt(index);
                }
            }

            if (removedSelectedTag)
            {
                comboBox.SelectedIndex = -1;
            }
        }
    }
}
