using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace Aire
{
    public partial class MainWindow
    {
        private static BitmapImage? LoadChatImageSource(string imageReference)
        {
            if (string.IsNullOrWhiteSpace(imageReference))
                return null;

            try
            {
                Uri uri;
                if (Uri.TryCreate(imageReference, UriKind.Absolute, out var absolute))
                {
                    uri = absolute;
                }
                else if (File.Exists(imageReference))
                {
                    uri = new Uri(imageReference, UriKind.Absolute);
                }
                else
                {
                    return null;
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = uri;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}
