using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Providers;

public class OpenRouterProviderTests
{
    [Fact]
    public async Task GetTokenUsageAsync_ParsesKeyUsagePayload()
    {
        using var server = new SimpleJsonServer((method, path) =>
            method == "GET" && path == "/v1/key"
                ? SimpleJsonServer.Json(200, """{"usage":123,"limit_remaining":77,"reset_date":"2026-04-30T00:00:00Z"}""")
                : SimpleJsonServer.Json(404, """{"error":"missing"}"""));

        var provider = new OpenRouterProvider();
        provider.Initialize(new ProviderConfig
        {
            ApiKey = "or-test",
            BaseUrl = server.BaseUrl,
            Model = "openai/gpt-4o-mini"
        });

        var usage = await provider.GetTokenUsageAsync(CancellationToken.None);

        Assert.NotNull(usage);
        Assert.Equal(123L, usage!.Used);
        Assert.Equal(200L, usage.Limit);
        Assert.Equal("credits", usage.Unit);
        Assert.Equal(new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc), usage.ResetDate?.ToUniversalTime());
    }

    private sealed class SimpleJsonServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Func<string, string, Response> _handler;
        private readonly Task _serveLoop;

        public SimpleJsonServer(Func<string, string, Response> handler)
        {
            _handler = handler;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            BaseUrl = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
            _serveLoop = Task.Run(ServeAsync);
        }

        public string BaseUrl { get; }

        public static Response Json(int statusCode, string json) =>
            new(statusCode, "application/json", Encoding.UTF8.GetBytes(json));

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
                    var response = _handler(parts[0], parts[1]);

                    while (!string.IsNullOrEmpty(await reader.ReadLineAsync()))
                    {
                    }

                    var header = $"HTTP/1.1 {response.StatusCode} {(response.StatusCode == 200 ? "OK" : "Error")}\r\n" +
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
}
