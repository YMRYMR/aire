using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Button = System.Windows.Controls.Button;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
using TextChangedEventArgs = System.Windows.Controls.TextChangedEventArgs;
using Aire.Services;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private readonly PromptTemplateService _templateService = new();
        private bool _suppressTemplateEvents;
        private int _selectedTemplateIndex = -1;

        internal void LoadTemplates()
        {
            _templateService.Load();
            TemplatesListView.ItemsSource = _templateService.Templates.ToList();
            SetEditFieldsEnabled(true);
            TemplateSaveButton.IsEnabled = true;
        }

        private void AddTemplate_Click(object sender, RoutedEventArgs e)
        {
            var L = LocalizationService.S;
            var template = new PromptTemplate
            {
                Name = L("settings.templates.newTemplate", "New Template"),
                Prefix = string.Empty,
                Shortcut = null,
                Template = null
            };
            _templateService.Add(template);
            RefreshTemplatesList();
            TemplatesListView.SelectedIndex = _templateService.Templates.Count - 1;
        }

        private void TemplatesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var L = LocalizationService.S;
            if (TemplatesListView.SelectedItem is not PromptTemplate template)
            {
                _selectedTemplateIndex = -1;
                TemplateEditPanelTitle.Text = L("settings.templates.createTitle", "Create a template");
                SetEditFieldsEnabled(true);
                TemplateSaveButton.IsEnabled = true;
                _suppressTemplateEvents = true;
                TemplateNameTextBox.Text = string.Empty;
                TemplatePrefixTextBox.Text = string.Empty;
                TemplateShortcutTextBox.Text = string.Empty;
                TemplateTemplateTextBox.Text = string.Empty;
                TemplateSampleTextBox.Text = L("settings.templates.samplePlaceholder", "Paste a short example here to test the template.");
                _suppressTemplateEvents = false;
                TemplatePreviewText.Text = L("settings.templates.previewEmpty", "Select a template to see a preview");
                UpdateTemplatePreview();
                return;
            }

            _selectedTemplateIndex = TemplatesListView.SelectedIndex;
            TemplateEditPanelTitle.Text = template.Name;
            SetEditFieldsEnabled(true);
            TemplateSaveButton.IsEnabled = true;

            _suppressTemplateEvents = true;
            TemplateNameTextBox.Text = template.Name ?? string.Empty;
            TemplatePrefixTextBox.Text = template.Prefix ?? string.Empty;
            TemplateShortcutTextBox.Text = template.Shortcut ?? string.Empty;
            TemplateTemplateTextBox.Text = template.Template ?? string.Empty;
            TemplateSampleTextBox.Text = L("settings.templates.samplePlaceholder", "Paste a short example here to test the template.");
            _suppressTemplateEvents = false;

            UpdateTemplatePreview();
        }

        private void ApplyStarterTemplate(string name, string shortcut, string template, string sampleText)
        {
            var starter = new PromptTemplate
            {
                Name = name,
                Prefix = string.Empty,
                Shortcut = shortcut,
                Template = template
            };

            if (_selectedTemplateIndex < 0)
            {
                _templateService.Add(starter);
                RefreshTemplatesList();
                TemplatesListView.SelectedIndex = _templateService.Templates.Count - 1;
            }
            else
            {
                _templateService.Update(_selectedTemplateIndex, starter);
                RefreshTemplatesList();
                TemplatesListView.SelectedIndex = _selectedTemplateIndex;
            }

            TemplateSampleTextBox.Text = sampleText;
            TemplateSaveButton.IsEnabled = true;
            UpdateTemplatePreview();
            TemplateTemplateTextBox.Focus();
            TemplateTemplateTextBox.CaretIndex = TemplateTemplateTextBox.Text.Length;
        }

        private void ExplainRecipe_Click(object sender, RoutedEventArgs e)
        {
            var L = LocalizationService.S;
            ApplyStarterTemplate(
                L("settings.templates.recipeExplainName", "Explain code"),
                L("settings.templates.recipeExplainShortcut", "/explain"),
                L("settings.templates.recipeExplainBody", "Please explain this code in simple words:\n\n{{code}}"),
                "public static int Add(int a, int b) => a + b;");
        }

        private void FixRecipe_Click(object sender, RoutedEventArgs e)
        {
            var L = LocalizationService.S;
            ApplyStarterTemplate(
                L("settings.templates.recipeFixName", "Fix mistakes"),
                L("settings.templates.recipeFixShortcut", "/fix"),
                L("settings.templates.recipeFixBody", "Find and fix any problems in this code:\n\n{{code}}"),
                "var total = price + tax;");
        }

        private void SummarizeRecipe_Click(object sender, RoutedEventArgs e)
        {
            var L = LocalizationService.S;
            ApplyStarterTemplate(
                L("settings.templates.recipeSummarizeName", "Summarize text"),
                L("settings.templates.recipeSummarizeShortcut", "/summarize"),
                L("settings.templates.recipeSummarizeBody", "Summarize this text in a short, clear way:\n\n{{text}}"),
                L("settings.templates.sampleText", "This is a short note that needs to be explained or summarized."));
        }

        private void ReviewRecipe_Click(object sender, RoutedEventArgs e)
        {
            var L = LocalizationService.S;
            ApplyStarterTemplate(
                L("settings.templates.recipeReviewName", "Review selection"),
                L("settings.templates.recipeReviewShortcut", "/review"),
                L("settings.templates.recipeReviewBody", "Review the selected text and tell me what is good and what can be improved:\n\n{{selection}}"),
                L("settings.templates.sampleReviewText", "Here is some text that I selected and want reviewed."));
        }

        private void DeleteTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not PromptTemplate template) return;
            _templateService.Remove(template);
            RefreshTemplatesList();
            TemplatesListView.SelectedIndex = -1;
        }

        private void SaveTemplate_Click(object sender, RoutedEventArgs e)
        {
            var name = TemplateNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                TemplateNameTextBox.Focus();
                return;
            }

            var updated = new PromptTemplate
            {
                Name = name,
                Prefix = TemplatePrefixTextBox.Text,
                Shortcut = string.IsNullOrWhiteSpace(TemplateShortcutTextBox.Text) ? null : TemplateShortcutTextBox.Text.Trim(),
                Template = string.IsNullOrWhiteSpace(TemplateTemplateTextBox.Text) ? null : TemplateTemplateTextBox.Text
            };

            if (_selectedTemplateIndex < 0 || _selectedTemplateIndex >= _templateService.Templates.Count)
            {
                _templateService.Add(updated);
                _selectedTemplateIndex = _templateService.Templates.Count - 1;
            }
            else
            {
                _templateService.Update(_selectedTemplateIndex, updated);
            }

            RefreshTemplatesList();
            TemplatesListView.SelectedIndex = _selectedTemplateIndex;
        }

        private void TemplateField_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTemplateEvents) return;
            UpdateTemplatePreview();
        }

        private void UpdateTemplatePreview()
        {
            var prefix = TemplatePrefixTextBox.Text;
            var template = TemplateTemplateTextBox.Text;
            var sample = TemplateSampleTextBox.Text;

            if (string.IsNullOrWhiteSpace(prefix) && string.IsNullOrWhiteSpace(template))
            {
                TemplatePreviewText.Text = LocalizationService.S("settings.templates.previewEmptyValue", "(empty)");
                return;
            }

            var sampleText = string.IsNullOrWhiteSpace(sample)
                ? "sample text"
                : sample;

            var temp = new PromptTemplate
            {
                Name = TemplateNameTextBox.Text,
                Prefix = prefix,
                Template = string.IsNullOrWhiteSpace(template) ? null : template
            };
            TemplatePreviewText.Text = temp.Resolve(new Dictionary<string, string>
            {
                ["code"] = sampleText,
                ["text"] = sampleText,
                ["selection"] = sampleText,
                ["clipboard"] = sampleText
            });
        }

        private void RefreshTemplatesList()
        {
            var selected = _selectedTemplateIndex;
            TemplatesListView.ItemsSource = null;
            TemplatesListView.ItemsSource = _templateService.Templates.ToList();
            if (selected >= 0 && selected < _templateService.Templates.Count)
                TemplatesListView.SelectedIndex = selected;
        }

        private void SetEditFieldsEnabled(bool enabled)
        {
            TemplateNameTextBox.IsEnabled = enabled;
            TemplatePrefixTextBox.IsEnabled = enabled;
            TemplateShortcutTextBox.IsEnabled = enabled;
            TemplateTemplateTextBox.IsEnabled = enabled;
            TemplateSampleTextBox.IsEnabled = enabled;
        }
    }
}
