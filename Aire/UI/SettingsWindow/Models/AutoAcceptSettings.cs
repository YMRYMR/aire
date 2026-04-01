using System.Collections.Generic;

namespace Aire.UI.Settings.Models
{
    internal sealed class AutoAcceptSettings
    {
        public bool Enabled { get; set; }
        public List<string> AllowedTools { get; set; } = new();
        public bool AllowMouseTools { get; set; }
        public bool AllowKeyboardTools { get; set; }
    }
}
