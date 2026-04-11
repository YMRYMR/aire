using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Aire.Tests.Infrastructure;

/// <summary>
/// Lightweight TCP-based HTTP server for unit tests.
/// Accepts a handler function that receives (method, path, body) and returns a <see cref="Response"/>.
/// </summary>
public sealed class SimpleJsonServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly Func<string, string, string, Response> _handler;
    private readonly Task _serveLoop;

    /// <summary>
    /// Creates a server with a handler that receives (method, path, body).
    /// </summary>
    public SimpleJsonServer(Func<string, string, string, Response> handler)
    {
        _handler = handler;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        BaseUrl = $"http://127.0.0.1:{port}";
        _serveLoop = Task.Run(ServeAsync);
    }

    /// <summary>
    /// Creates a server with a handler that receives (method, path) only.
    /// The request body is consumed but not forwarded to the handler.
    /// </summary>
    public SimpleJsonServer(Func<string, string, Response> handler)
        : this((method, path, _) => handler(method, path))
    {
    }

    public string BaseUrl { get; }

    /// <summary>
    /// Captured request bodies from all incoming requests.
    /// </summary>
    public List<string> RequestBodies { get; } = [];

    // --- Static response helpers ---

    public static Response Json(int statusCode, string json) =>
        new(statusCode, "application/json", Encoding.UTF8.GetBytes(json));

    public static Response Text(int statusCode, string text) =>
        new(statusCode, "text/plain", Encoding.UTF8.GetBytes(text));

    public static Response Sse(int statusCode, IEnumerable<string> lines) =>
        new(statusCode, "text/event-stream", Encoding.UTF8.GetBytes(string.Join("\n", lines) + "\n\n"));

    public static Response Lines(int statusCode, IEnumerable<string> lines) =>
        new(statusCode, "application/x-ndjson", Encoding.UTF8.GetBytes(string.Join("\n", lines) + "\n"));

    // --- Server loop ---

    private async Task ServeAsync()
    {
        try
        {
            while (true)
            {
                using var client = await _listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, leaveOpen: true);

                var requestLine = await reader.ReadLineAsync();
                if (requestLine == null)
                    continue;

                var parts = requestLine.Split(' ');
                var method = parts[0];
                var path = parts[1];
                var contentLength = 0;

                string? line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                {
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        contentLength = int.Parse(line.AsSpan(15).Trim());
                }

                string body = string.Empty;
                if (contentLength > 0)
                {
                    var buffer = new char[contentLength];
                    var read = 0;
                    while (read < contentLength)
                        read += await reader.ReadAsync(buffer, read, contentLength - read);
                    body = new string(buffer);
                }

                RequestBodies.Add(body);

                var response = _handler(method, path, body);
                var statusText = response.StatusCode == 200 ? "OK" : "Error";
                var header =
                    $"HTTP/1.1 {response.StatusCode} {statusText}\r\n" +
                    $"Content-Type: {response.ContentType}\r\n" +
                    $"Content-Length: {response.Body.Length}\r\n" +
                    "Connection: close\r\n\r\n";

                await stream.WriteAsync(Encoding.ASCII.GetBytes(header));
                await stream.WriteAsync(response.Body);
                await stream.FlushAsync();
            }
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        try { _listener.Stop(); } catch { }
        try { _serveLoop.Wait(1000); } catch { }
    }

    public sealed record Response(int StatusCode, string ContentType, byte[] Body);
}
