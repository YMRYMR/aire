using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Aire.AppLayer.Providers;
using Aire.Data;
using Aire.Domain.Providers;
using Aire.Providers;
using Aire.Services.Providers;
using ProviderChatMessage = Aire.Providers.ChatMessage;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfSolidBrush = System.Windows.Media.SolidColorBrush;

namespace Aire.UI
{
    public partial class OnboardingWindow
    {
        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            _testCts?.Cancel();
            _testCts = new CancellationTokenSource();
            var ct = _testCts.Token;

            SetTestResult(S("onboarding.testing", "Testing…"), null);
            TestButton.IsEnabled = false;

            try
            {
                var provider = BuildProviderFromForm();
                if (provider == null)
                {
                    SetTestResult(S("onboarding.fillKeyFirst", "Fill in the API Key first."), false);
                    return;
                }

                var connectionTestService = new ProviderConnectionTestApplicationService();
                var response = await connectionTestService.RunAsync(provider, ct);

                if (ct.IsCancellationRequested)
                    return;

                SetTestResult(
                    response.Success
                        ? S("onboarding.testOk", "Connected!")
                        : $"{S("onboarding.testFail", "Failed")}: {response.Message}",
                    response.Success);
            }
            catch (OperationCanceledException)
            {
            }
                catch
            {
                if (!ct.IsCancellationRequested)
                    SetTestResult($"{S("onboarding.testFail", "Failed")}: provider test failed.", false);
            }
            finally
            {
                TestButton.IsEnabled = true;
            }
        }

        internal void SetTestResult(string message, bool? success)
        {
            TestResultText.Text = message;
            TestResultText.Foreground = success switch
            {
                true => new WpfSolidBrush(WpfColor.FromRgb(0x4C, 0xAF, 0x50)),
                false => new WpfSolidBrush(WpfColor.FromRgb(0xE5, 0x53, 0x4B)),
                null => (WpfBrush)FindResource("TextSecondaryBrush"),
            };
        }

        internal void ClearTestResult() => TestResultText.Text = string.Empty;

        internal IAiProvider? BuildProviderFromForm()
        {
            if (ProviderTypeCombo.SelectedItem is not WpfComboBoxItem typeItem)
                return null;

            var type = typeItem.Tag as string ?? "OpenAI";
            var model = ModelCombo.SelectedValue as string
                ?? (ModelCombo.SelectedItem as ModelDefinition)?.Id
                ?? ModelCombo.Text.Trim();
            var workflow = new ProviderSetupApplicationService();
            return workflow.BuildRuntimeProvider(new ProviderRuntimeRequest(
                type,
                ApiKeyBox.Password,
                BaseUrlBox.Text,
                model,
                type == "ClaudeWeb" && (_claudeSessionActive || ClaudeAiSession.Instance.IsReady)));
        }
    }
}
