using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trendo.Core;

namespace Trendo.Api;

/// <summary>Örtlicher Zugang für Claude Code zu den Trendo-Werkzeugen. Lauscht nur auf 127.0.0.1, gilt für einen Lauf und verlangt ein Zufalls-Kennzeichen, das nur in der Konfiguration dieses Laufs steht.</summary>
public sealed class ClaudeBridgeHost : IAsyncDisposable
{
    readonly WebApplication _app;
    public int Port { get; }
    public string Token { get; }

    ClaudeBridgeHost(WebApplication app, int port, string token) { _app = app; Port = port; Token = token; }

    public static async Task<ClaudeBridgeHost> StartAsync(TrendoMcp mcp)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expected = Encoding.UTF8.GetBytes("Bearer " + token);
        var b = WebApplication.CreateSlimBuilder();
        b.Logging.ClearProviders();
        b.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0));
        var app = b.Build();
        app.MapPost("/mcp", async (HttpContext c) =>
        {
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(c.Request.Headers.Authorization.ToString()), expected)) { c.Response.StatusCode = 401; return; }
            JsonNode? request;
            try { request = await JsonNode.ParseAsync(c.Request.Body, cancellationToken: c.RequestAborted); }
            catch (JsonException) { c.Response.StatusCode = 400; return; }
            JsonObject? answer;
            try { answer = await mcp.HandleAsync(request); }
            catch (Exception e) when (e is not OperationCanceledException) // ein Werkzeugfehler soll Claude erreichen und nicht als leere Serverantwort enden
            { answer = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = request?["id"]?.DeepClone(), ["error"] = new JsonObject { ["code"] = -32603, ["message"] = e.GetType().Name + ": " + e.Message } }; }
            if (answer is null) { c.Response.StatusCode = 202; return; }
            c.Response.ContentType = "application/json";
            await c.Response.WriteAsync(answer.ToJsonString(), c.RequestAborted);
        });
        app.MapGet("/mcp", (HttpContext c) => { c.Response.StatusCode = 405; return Task.CompletedTask; });
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return new ClaudeBridgeHost(app, new Uri(address).Port, token);
    }

    /// <summary>Konfiguration für „claude --mcp-config“.</summary>
    public string ConfigJson() => new JsonObject
    {
        ["mcpServers"] = new JsonObject
        {
            [TrendoMcp.ServerName] = new JsonObject
            {
                ["type"] = "http",
                ["url"] = $"http://127.0.0.1:{Port}/mcp",
                ["headers"] = new JsonObject { ["Authorization"] = "Bearer " + Token },
            },
        },
    }.ToJsonString();

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
