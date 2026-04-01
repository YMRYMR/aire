using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using Aire.UI.Controls;
using ProviderChatMessage = Aire.Providers.ChatMessage;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfSolidBrush = System.Windows.Media.SolidColorBrush;

namespace Aire.UI
{
    public partial class OnboardingWindow
    {
        private void BindStandardModels(IReadOnlyList<ModelDefinition> models, string? preferredModelId = null)
        {
            var previous = preferredModelId
                ?? ModelCombo.SelectedValue as string
                ?? (ModelCombo.SelectedItem as ModelDefinition)?.Id
                ?? ModelCombo.Text;

            _standardModelViewSource.Source = models;
            ModelCombo.ItemsSource = _standardModelViewSource.View;
            ModelCombo.DisplayMemberPath = "DisplayName";
            ModelCombo.SelectedValuePath = "Id";

            if (models.Count == 0)
                return;

            var match = !string.IsNullOrWhiteSpace(previous)
                ? models.FirstOrDefault(m =>
                    string.Equals(m.Id, previous, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(m.DisplayName, previous, StringComparison.OrdinalIgnoreCase))
                : null;

            _suppressStandardModelFilter = true;
            if (match != null)
            {
                ModelCombo.SelectedItem = match;
                ModelCombo.Text = match.DisplayName;
            }
            else
            {
                ModelCombo.SelectedItem = models[0];
                ModelCombo.Text = models[0].DisplayName;
            }
            _suppressStandardModelFilter = false;
        }

        private void OnStandardModelComboTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            EditableComboBoxFilterHelper.ApplyFilter(
                ModelCombo,
                _suppressStandardModelFilter,
                ref _preStandardModelFilterSelection,
                model => model.DisplayName,
                model => model.Id);
        }

        private void StandardModelCombo_DropDownClosed(object? sender, EventArgs e)
        {
            EditableComboBoxFilterHelper.ResetFilter(
                ModelCombo,
                ref _suppressStandardModelFilter,
                ref _preStandardModelFilterSelection,
                model => model.DisplayName);
        }

        internal void GoToStep(int step)
        {
            _step = step;
            Step1Panel.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2Panel.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3Panel.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
            Step4Panel.Visibility = step == 4 ? Visibility.Visible : Visibility.Collapsed;

            UpdateDots();
            SizeToContent = SizeToContent.Height;
        }

        private void UpdateDots()
        {
            var active = (WpfBrush)FindResource("PrimaryBrush");
            var inactive = (WpfBrush)FindResource("Surface3Brush");
            Dot1.Fill = _step == 1 ? active : inactive;
            Dot2.Fill = _step == 2 ? active : inactive;
            Dot3.Fill = _step == 3 ? active : inactive;
            Dot4.Fill = _step == 4 ? active : inactive;
        }

        internal void Step1Next_Click(object sender, RoutedEventArgs e) => GoToStep(2);

        private void Step2Back_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => GoToStep(1);

        internal void PickProvider_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn) return;
            var type = btn.Tag as string ?? "OpenAI";

            foreach (WpfComboBoxItem item in ProviderTypeCombo.Items)
            {
                if ((item.Tag as string) == type)
                {
                    ProviderTypeCombo.SelectedItem = item;
                    break;
                }
            }

            GoToStep(3);

            if (type != "Ollama" && type != "ClaudeWeb")
                ApiKeyBox.Focus();
            else
                ProviderNameBox.Focus();
        }

        private void PickCustomProvider_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => GoToStep(3);

        internal void Step3Back_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            CancelOllamaOps();
            GoToStep(2);
        }

        private void Step4Back_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => GoToStep(3);

        internal void ProviderTypeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ProviderTypeCombo.SelectedItem is not WpfComboBoxItem item) return;

            CancelOllamaOps();

            var type = item.Tag as string ?? "OpenAI";

            ProviderNameBox.Text = ProviderDisplayName(type);

            bool isOllama = type == "Ollama";
            bool isClaudeWeb = type == "ClaudeWeb";
            bool isCodex = type == "Codex";
            bool needsKey = !isOllama && !isClaudeWeb && !isCodex;

            ApiKeyPanel.Visibility = needsKey ? Visibility.Visible : Visibility.Collapsed;
            BaseUrlPanel.Visibility = type is "OpenAI" or "Zai" ? Visibility.Visible : Visibility.Collapsed;
            StandardModelPanel.Visibility = isOllama ? Visibility.Collapsed : Visibility.Visible;
            TestGrid.Visibility = isOllama ? Visibility.Collapsed : Visibility.Visible;
            OllamaModelPicker.Visibility = isOllama ? Visibility.Visible : Visibility.Collapsed;
            CodexSetupPanel.Visibility = isCodex ? Visibility.Visible : Visibility.Collapsed;

            LabelApiKey.Visibility = needsKey ? Visibility.Visible : Visibility.Collapsed;
            ApiKeyBox.Visibility = needsKey ? Visibility.Visible : Visibility.Collapsed;
            ApiKeyLink.Visibility = needsKey ? ApiKeyLink.Visibility : Visibility.Collapsed;
            AnthropicLoginPanel.Visibility = isClaudeWeb ? Visibility.Visible : Visibility.Collapsed;
            if (isClaudeWeb)
                RefreshClaudeSessionStatus();

            if (!isOllama)
            {
                if (isCodex)
                    RefreshCodexInstallState();

                if (needsKey)
                    RefreshApiKeyLink();
                else
                    ApiKeyLink.Visibility = Visibility.Collapsed;
                UpdateVisitProviderButton();
                var models = ModelCatalog.GetDefaults(type == "ClaudeWeb" ? "Anthropic" : type);
                BindStandardModels(models);
                ClearTestResult();
            }
            else
            {
                VisitProviderButton.Visibility = Visibility.Collapsed;
                _ = OllamaModelPicker.CheckAsync();
            }
        }

        private void ApiKeyLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ApiKeyLink.Tag is string url && !string.IsNullOrEmpty(url))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
        }

        private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _modelFetchCts?.Cancel();
            _modelFetchCts = new CancellationTokenSource();
            var ct = _modelFetchCts.Token;
            _ = FetchModelsWithDebounceAsync(ct);
        }

    }
}
