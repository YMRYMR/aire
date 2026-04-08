using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using Aire.UI;
using Xunit;

namespace Aire.Tests.UI;

public class InitializationWindowTests : TestBase
{
    [Fact]
    public void InitializationWindow_AppendStatus_DeduplicatesAndTrimsHistory()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var window = new InitializationWindow();
            try
            {
                var appendStatus = typeof(InitializationWindow).GetMethod(
                    "AppendStatus",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(appendStatus);

                for (int i = 1; i <= 7; i++)
                {
                    appendStatus!.Invoke(window, [ $"Step {i}" ]);
                }

                appendStatus.Invoke(window, [ "Step 7" ]);

                var itemsControl = (ItemsControl)window.FindName("ActionItemsControl")!;
                var entries = ((IEnumerable<string>)itemsControl.ItemsSource!).ToList();

                Assert.Equal(6, entries.Count);
                Assert.Equal("Step 2", entries[0]);
                Assert.Equal("Step 7", entries[^1]);
                Assert.Equal("Step 7", ((TextBlock)window.FindName("StatusText")!).Text);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
