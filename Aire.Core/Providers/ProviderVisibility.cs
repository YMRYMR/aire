using System;

namespace Aire.Providers
{
    /// <summary>
    /// Central visibility gate for provider variants that should only surface in debug builds.
    /// </summary>
    public static class ProviderVisibility
    {
        public static bool ShowClaudeWebProvider
        {
            get
            {
#if DEBUG
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsHiddenFromRelease(string type)
            => string.Equals(ProviderIdentityCatalog.NormalizeType(type), "ClaudeWeb", StringComparison.OrdinalIgnoreCase);
    }
}
