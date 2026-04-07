using System.Linq;
using System.Windows.Controls;
using Aire.UI;
using Xunit;

namespace Aire.Tests.UI
{
    public class ProviderChoiceVisibilityTests : TestBase
    {
        [Fact]
        public void PruneHiddenChoices_RemovesHiddenProviderItems()
        {
            RunOnStaThread(delegate
            {
                EnsureApplication();

                var comboBox = new ComboBox();
                comboBox.Items.Add(new ComboBoxItem { Tag = "OpenAI", Content = "OpenAI" });
                comboBox.Items.Add(new ComboBoxItem { Tag = "ClaudeWeb", Content = "Claude.ai" });
                comboBox.Items.Add(new ComboBoxItem { Tag = "ClaudeCode", Content = "Claude Code" });

                ProviderChoiceVisibility.PruneHiddenChoices(comboBox);

                Assert.DoesNotContain(comboBox.Items.Cast<ComboBoxItem>(), item => (string?)item.Tag == "ClaudeWeb");
                Assert.Contains(comboBox.Items.Cast<ComboBoxItem>(), item => (string?)item.Tag == "OpenAI");
                Assert.Contains(comboBox.Items.Cast<ComboBoxItem>(), item => (string?)item.Tag == "ClaudeCode");
            });
        }
    }
}
