using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Aire.Services;
using Aire.UI;
using Xunit;

namespace Aire.Tests.UI
{
    public class HelpWindowTests : TestBase
    {
        [Fact]
        public void HelpWindow_StateHelpersAndSearchMatch_Work()
        {
            RunOnStaThread(delegate
            {
                EnsureApplication();
                HelpWindow helpWindow = new HelpWindow();
                string path = HelpWindow.StatePath;
                string originalState = File.Exists(path) ? File.ReadAllText(path) : null;

                try
                {
                    helpWindow.Width = 640;
                    helpWindow.Height = 480;
                    helpWindow.Left = 20;
                    helpWindow.Top = 30;
                    
                    helpWindow.SaveWindowState();
                    
                    helpWindow.Width = 200;
                    helpWindow.Height = 200;
                    helpWindow.LoadWindowState();
                    
                    Assert.True(helpWindow.Width >= 200);
                    Assert.True(helpWindow.Height >= 200);

                    var section = new HelpSection("text", "Install tools", null, "Use the settings panel", "Quickstart", null, null);
                    Assert.True(HelpWindow.SectionMatchesQuery(section, "install"));
                    Assert.False(HelpWindow.SectionMatchesQuery(section, "missing"));

                    StackPanel panel = new StackPanel();
                    helpWindow.RenderSection(section, panel);
                    Assert.NotEmpty(panel.Children);
                }
                finally
                {
                    try
                    {
                        if (originalState == null) { if (File.Exists(path)) File.Delete(path); }
                        else File.WriteAllText(path, originalState);
                    }
                    catch { }
                    helpWindow.Close();
                }
            });
        }
    }
}
