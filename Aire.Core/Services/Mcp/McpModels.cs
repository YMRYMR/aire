using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aire.Services.Mcp
{
    public class McpServerConfig
    {
        public int    Id               { get; set; }
        public string Name             { get; set; } = string.Empty;
        public string Command          { get; set; } = string.Empty;
        public string Arguments        { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public Dictionary<string, string> EnvVars { get; set; } = new();
        public bool   IsEnabled        { get; set; } = true;
        public int    SortOrder        { get; set; }
    }

    public class McpToolDefinition
    {
        public string      ServerName   { get; set; } = string.Empty;
        public string      Name         { get; set; } = string.Empty;
        public string      Description  { get; set; } = string.Empty;
        public JsonElement InputSchema  { get; set; }
    }

    public class McpCallResult
    {
        public bool   IsError { get; set; }
        public string Text    { get; set; } = string.Empty;
    }

    // ── Wire types ────────────────────────────────────────────────────────────

    internal class McpRpcRequest
    {
        [JsonPropertyName("jsonrpc")] public string  Jsonrpc { get; set; } = "2.0";
        [JsonPropertyName("id")]      public int?    Id      { get; set; }
        [JsonPropertyName("method")]  public string  Method  { get; set; } = string.Empty;
        [JsonPropertyName("params")]  public object? Params  { get; set; }
    }

    internal class McpRpcResponse
    {
        [JsonPropertyName("jsonrpc")] public string       Jsonrpc { get; set; } = "2.0";
        [JsonPropertyName("id")]      public int?         Id      { get; set; }
        [JsonPropertyName("result")]  public JsonElement? Result  { get; set; }
        [JsonPropertyName("error")]   public McpRpcError? Error   { get; set; }
    }

    internal class McpRpcError
    {
        [JsonPropertyName("code")]    public int    Code    { get; set; }
        [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
    }
}
