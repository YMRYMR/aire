using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Controls;
using Aire.UI;
using Xunit;

namespace Aire.Tests.UI
{
    public class ImageViewerWindowTests : TestBase
    {
        [Fact]
        public void ImageViewerWindow_ZoomAndFitHelpers_UpdateTransform()
        {
            RunOnStaThread(delegate
            {
                EnsureApplication();
                WriteableBitmap source = new WriteableBitmap(10, 20, 96, 96, PixelFormats.Bgra32, null);
                ImageViewerWindow window = new ImageViewerWindow(source);
                
                window.FitToWindow(200, 100);
                window.ZoomAt(new Point(50, 25), 1.2);
                window.ShowActualSize();
                window.ApplyTransform();
                
                // ZoomLabel is internal in UI
                Assert.False(string.IsNullOrWhiteSpace(window.ZoomLabel.Text));
                
                window.Close();
            });
        }
    }
}
