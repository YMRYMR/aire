using System;
using System.Windows;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services
{
    public class TrayIconServiceTests : TestBase
    {
        [Fact]
        public void TrayIconService_BasicLifecycleAndEvents_Work()
        {
            RunOnStaThread(delegate
            {
                EnsureApplication();
                Window window = new Window();
                using TrayIconService tray = new TrayIconService(window);
                
                int raised = 0;
                tray.AttachedToTrayChanged += (s, e) => raised++;
                
                tray.IsAttachedToTray = false;
                tray.IsAttachedToTray = true;
                
                tray.SetToolTip("Aire test");
                tray.ToggleMainWindow();
                tray.ToggleMainWindow();
                tray.DetachFromTray();
                
                Assert.True(raised >= 2);
                window.Close();
            });
        }
    }
}
