using System;
using System.IO;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        // Window singleton / open API
        public static SettingsWindow? Current { get; private set; }

        /// <summary>
        /// Fired when any code wants to open Settings, optionally navigating
        /// to a specific tab. App.xaml.cs subscribes and handles this.
        /// </summary>
        public static event Action<string?>? OpenRequested;

        /// <summary>Request the Settings window to open, optionally jumping to a tab.</summary>
        /// <param name="tab">One of: providers, appearance, voice, context, auto-accept, connections.</param>
        public static void RequestOpen(string? tab = null) => OpenRequested?.Invoke(tab);

        /// <summary>
        /// In-memory snapshot of the last-saved auto-accept JSON.
        /// MainWindow reads this to avoid a cross-connection database round-trip on every tool call.
        /// </summary>
        public static string? AutoAcceptJsonCache { get; private set; }

        /// <summary>Called by MainWindow at startup to warm the cache from the DB.</summary>
        public static void SetAutoAcceptCache(string json) => AutoAcceptJsonCache = json;

        internal static readonly string StatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aire", "settingsstate.json");
    }
}
