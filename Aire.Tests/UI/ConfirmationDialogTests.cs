using System.Windows;
using Aire.UI;
using Xunit;

namespace Aire.Tests.UI
{
    public class ConfirmationDialogTests : TestBase
    {
        [Fact]
        public void ConfirmationDialog_ButtonHandlersSetResult()
        {
            RunOnStaThread(delegate
            {
                EnsureApplication();
                
                // Test Yes button
                ConfirmationDialog dialog = new ConfirmationDialog();
                dialog.YesButton_Click(dialog, new RoutedEventArgs());
                Assert.True(dialog.Result);
                
                // Test No button
                dialog = new ConfirmationDialog();
                dialog.NoButton_Click(dialog, new RoutedEventArgs());
                Assert.False(dialog.Result);
                
                dialog.Close();
            });
        }
    }
}
