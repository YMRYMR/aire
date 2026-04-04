using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Svg;
using System.Drawing.Imaging;

namespace Aire
{
    public partial class MainWindow
    {
        private static readonly HttpClient _chatImageHttpClient = new();

        private static ImageSource? LoadChatImageSource(string imageReference)
        {
            if (string.IsNullOrWhiteSpace(imageReference))
                return null;

            try
            {
                if (IsSvgReference(imageReference))
                    return LoadSvgImageSource(imageReference);

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

        private static ObservableCollection<System.Windows.Media.ImageSource>? LoadChatImageSources(System.Collections.Generic.IEnumerable<string>? imageReferences)
        {
            if (imageReferences == null)
                return null;

            var images = new ObservableCollection<System.Windows.Media.ImageSource>();
            foreach (var imageReference in imageReferences)
            {
                var bitmap = LoadChatImageSource(imageReference);
                if (bitmap != null)
                    images.Add(bitmap);
            }

            return images.Count == 0 ? null : images;
        }

        private static string AppendImageFallbackLinks(string text, System.Collections.Generic.IEnumerable<string>? imageReferences)
        {
            if (imageReferences == null)
                return text;

            var links = imageReferences
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(ToClickableImageReference)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (links.Count == 0)
                return text;

            var suffix = string.Join(Environment.NewLine, links.Select(link => $"Open image: {link}"));
            return string.IsNullOrWhiteSpace(text) ? suffix : $"{text}{Environment.NewLine}{Environment.NewLine}{suffix}";
        }

        private static bool IsSvgReference(string imageReference)
        {
            if (string.IsNullOrWhiteSpace(imageReference))
                return false;

            if (imageReference.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                return true;

            return Uri.TryCreate(imageReference, UriKind.Absolute, out var absolute)
                   && absolute.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
        }

        private static ImageSource? LoadSvgImageSource(string imageReference)
        {
            try
            {
                using var stream = OpenSvgStream(imageReference);
                if (stream == null)
                    return null;

                var document = SvgDocument.Open<SvgDocument>(stream);
                using var bitmap = document.Draw();
                using var memory = new MemoryStream();
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;

                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = memory;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        private static Stream? OpenSvgStream(string imageReference)
        {
            if (Uri.TryCreate(imageReference, UriKind.Absolute, out var uri))
            {
                if (uri.IsFile && File.Exists(uri.LocalPath))
                    return File.OpenRead(uri.LocalPath);

                if (uri.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    var bytes = _chatImageHttpClient.GetByteArrayAsync(uri).GetAwaiter().GetResult();
                    return new MemoryStream(bytes, writable: false);
                }
            }

            return File.Exists(imageReference) ? File.OpenRead(imageReference) : null;
        }

        private static string ToClickableImageReference(string imageReference)
        {
            if (Uri.TryCreate(imageReference, UriKind.Absolute, out var absolute))
                return absolute.IsFile ? absolute.AbsoluteUri : imageReference;

            return File.Exists(imageReference)
                ? new Uri(Path.GetFullPath(imageReference), UriKind.Absolute).AbsoluteUri
                : imageReference;
        }
    }
}
