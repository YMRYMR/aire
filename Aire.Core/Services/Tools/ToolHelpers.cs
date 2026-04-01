using System.Text.Json;

namespace Aire.Services.Tools
{
    /// <summary>
    /// Shared parameter-extraction helpers used by tool services.
    /// </summary>
    public static class ToolHelpers
    {
        public static string GetString(ToolCallRequest request, string name, string fallback = "")
        {
            if (request.Parameters.ValueKind == JsonValueKind.Undefined)
                return fallback;

            if (request.Parameters.TryGetProperty(name, out var element))
            {
                if (element.ValueKind == JsonValueKind.String)
                    return element.GetString() ?? fallback;

                return element.ToString();
            }

            return fallback;
        }

        public static int GetInt(ToolCallRequest request, string name, int fallback = 0)
        {
            if (request.Parameters.ValueKind == JsonValueKind.Undefined)
                return fallback;

            if (request.Parameters.TryGetProperty(name, out var element))
            {
                if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
                    return number;

                if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out var stringNumber))
                    return stringNumber;
            }

            return fallback;
        }

        public static int GetIndexParam(ToolCallRequest request, int fallback = -1)
        {
            var value = GetInt(request, "index", int.MinValue);
            if (value != int.MinValue)
                return value;

            value = GetInt(request, "tab_index", int.MinValue);
            return value != int.MinValue ? value : fallback;
        }
    }
}
