using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using System.Windows;
using Aire.UI;
using Xunit;

namespace Aire.Tests.UI;

public class OnboardingWindowProviderCardsTests : TestBase
{
    [Fact]
    public void OnboardingWindow_BuildProviderCards_PopulatesProviderButtons()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var window = new OnboardingWindow();
            try
            {
                var buildProviderCards = typeof(OnboardingWindow).GetMethod(
                    "BuildProviderCards",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(buildProviderCards);

                var providerCardGrid = window.FindName("ProviderCardGrid")!;
                var children = (UIElementCollection)providerCardGrid.GetType()
                    .GetProperty("Children", BindingFlags.Instance | BindingFlags.Public)!
                    .GetValue(providerCardGrid)!;
                children.Clear();

                buildProviderCards!.Invoke(window, null);

                var buttons = children.OfType<Button>().ToList();
                Assert.NotEmpty(buttons);
                Assert.Contains(buttons, button => button.Tag as string == "OpenAI");
                Assert.Contains(buttons, button => button.Tag as string == "Mistral");
                Assert.Contains(buttons, button => button.Tag as string == "Ollama");
                Assert.Contains(buttons, button => button.Tag as string == "Mistral");
            }
            finally
            {
                window.Close();
            }
        });
    }
}
