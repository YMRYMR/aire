using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace Aire.UI
{
    internal static class TextProofingManager
    {
        private static readonly Dictionary<WpfTextBox, TextProofingAdorner> Adorners = new();

        public static void AttachOrUpdate(WpfTextBox textBox, string uiLanguageCode)
        {
            if (textBox == null)
                return;

            if (Adorners.TryGetValue(textBox, out var existing))
            {
                existing.SetLanguage(uiLanguageCode);
                existing.RequestRefresh();
                return;
            }

            void attachWhenReady(object? sender, RoutedEventArgs e)
            {
                textBox.Loaded -= attachWhenReady;
                AttachCore(textBox, uiLanguageCode);
            }

            if (textBox.IsLoaded)
                AttachCore(textBox, uiLanguageCode);
            else
                textBox.Loaded += attachWhenReady;
        }

        private static void AttachCore(WpfTextBox textBox, string uiLanguageCode)
        {
            if (Adorners.ContainsKey(textBox))
                return;

            var layer = AdornerLayer.GetAdornerLayer(textBox);
            if (layer == null)
                return;

            var adorner = new TextProofingAdorner(textBox);
            adorner.SetLanguage(uiLanguageCode);
            Adorners[textBox] = adorner;
            layer.Add(adorner);

            textBox.TextChanged += (_, _) => adorner.RequestRefresh();
            textBox.SizeChanged += (_, _) => adorner.RequestRefresh();
            textBox.Unloaded += (_, _) =>
            {
                if (Adorners.Remove(textBox))
                    layer.Remove(adorner);
            };
        }
    }
}
