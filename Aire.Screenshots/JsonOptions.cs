using System.Text.Json;

namespace Aire.Screenshots;

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}
