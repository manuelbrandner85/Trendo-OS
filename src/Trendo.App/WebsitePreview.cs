using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

// Liegt im App-Projekt, nicht in Trendo.Core: die lokale Vorschau braucht den Loopback-Host und Chrome.
// QA-019 verbietet System.Net.Http und System.Net.Sockets in der Kernbibliothek.

public sealed record WebsitePreviewResult(bool Ok, string Path, string Note);

/// <summary>Bildvorschau über einen lokalen Chrome oder Chromium. Kein CDN, kein WebView im Fenster.</summary>
public static class WebsitePreview
{
    public static string? FindBrowser()
    {
        foreach (var name in new[] { "google-chrome", "google-chrome-stable", "chromium", "chromium-browser", "msedge", "microsoft-edge" })
            if (Cli.Find(name) is { } path) return path;
        if (!OperatingSystem.IsWindows()) return null;
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };
        foreach (var root in roots)
        {
            foreach (var rel in new[]
            {
                Path.Combine("Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine("Microsoft", "Edge", "Application", "msedge.exe")
            })
            {
                var full = Path.Combine(root, rel);
                if (File.Exists(full)) return full;
            }
        }
        return null;
    }

    public static async Task<WebsitePreviewResult> CaptureAsync(string projectDir, int width, int height, string pngPath, string? query = null, CancellationToken ct = default)
    {
        var index = Path.Combine(projectDir, "index.html");
        if (!File.Exists(index)) return new(false, "", "Die Seite hat keine index.html.");
        var browser = FindBrowser();
        if (browser is null) return new(false, "", "Kein Chrome oder Chromium. Die Bildvorschau geht hier nicht. Öffne die index.html im Browser.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(pngPath))!);
        using var host = WebsiteHost.Start(projectDir);
        var url = host.Origin + "/index.html" + (string.IsNullOrWhiteSpace(query) ? "" : "?" + query.TrimStart('?'));
        await ShotAsync(browser, url, width, height, pngPath, ct);
        return File.Exists(pngPath) ? new(true, pngPath, width < 700 ? "Handy" : "Desktop") : new(false, "", "Die Vorschau hat kein Bild geschrieben.");
    }

    static async Task ShotAsync(string browser, string url, int width, int height, string pngPath, CancellationToken ct)
    {
        var profile = Path.Combine(Path.GetTempPath(), "trendo-chrome-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profile);
        var port = FreePort();
        var psi = new ProcessStartInfo(browser)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in new[]
        {
            "--headless=new", "--no-sandbox", "--disable-dev-shm-usage", "--hide-scrollbars", "--no-first-run",
            "--disable-extensions", "--disable-background-networking", "--remote-allow-origins=*",
            "--remote-debugging-port=" + port, "--user-data-dir=" + profile, "--window-size=" + width + "," + height,
            "--use-gl=angle", "--use-angle=swiftshader", "--enable-unsafe-swiftshader", "about:blank"
        }) psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Chrome ließ sich nicht starten.");
        _ = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        ClientWebSocket? socket = null;
        // ReceiveAsync und GetStringAsync lassen den Abbruch auf diesem Chrome oft liegen. Dann bleibt der Prozess stehen.
        using var wake = timeout.Token.Register(() => Cut(socket, http, process));
        try
        {
            var work = TakeShot();
            var done = await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(27)));
            if (done != work)
            {
                Cut(socket, http, process);
                var again = await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(2)));
                if (again != work) throw new TimeoutException("Die Vorschau hat nicht rechtzeitig geantwortet.");
            }
            await work;
        }
        finally
        {
            Cut(socket, http, process);
            try { socket?.Dispose(); } catch { /* schon zu */ }
            try { Directory.Delete(profile, true); } catch { /* Chrome hält die Datei manchmal noch */ }
        }

        async Task TakeShot()
        {
            var wsUrl = await PageSocket(http, port, timeout.Token);
            socket = new ClientWebSocket();
            await socket.ConnectAsync(new Uri(wsUrl), timeout.Token);
            var cdp = new Cdp(socket);
            await cdp.Call("Page.enable", new { }, timeout.Token);
            await cdp.Call("Emulation.setDeviceMetricsOverride", new { width, height, deviceScaleFactor = 1, mobile = width < 700 }, timeout.Token);
            await cdp.Call("Page.navigate", new { url }, timeout.Token);
            var until = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < until)
            {
                timeout.Token.ThrowIfCancellationRequested();
                var state = await cdp.Call("Runtime.evaluate", new { expression = "document.documentElement && document.documentElement.dataset.fertig || ''", returnByValue = true }, timeout.Token);
                var value = ResultValue(state);
                if (value == "1") break;
                await Task.Delay(120, timeout.Token);
            }
            await Task.Delay(400, timeout.Token);
            var shot = await cdp.Call("Page.captureScreenshot", new { format = "png" }, timeout.Token);
            var data = ResultString(shot, "data");
            if (string.IsNullOrWhiteSpace(data)) throw new InvalidOperationException("Chrome hat kein Bild geliefert.");
            await File.WriteAllBytesAsync(pngPath, Convert.FromBase64String(data), timeout.Token);
        }
    }

    static void Cut(ClientWebSocket? socket, HttpClient http, Process process)
    {
        try { socket?.Abort(); } catch { /* noch nicht verbunden */ }
        try { http.CancelPendingRequests(); } catch { /* schon zu */ }
        try { if (!process.HasExited) process.Kill(true); } catch { /* schon zu */ }
    }

    static async Task<string> PageSocket(HttpClient http, int port, CancellationToken ct)
    {
        for (var i = 0; i < 40; i++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var json = await http.GetStringAsync("http://127.0.0.1:" + port + "/json/list", ct);
                using var doc = JsonDocument.Parse(json);
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    var type = item.TryGetProperty("type", out var t) ? t.GetString() : "";
                    if (type != "page") continue;
                    if (item.TryGetProperty("webSocketDebuggerUrl", out var url) && url.GetString() is { Length: > 0 } ws)
                        return ws;
                }
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            await Task.Delay(150, ct);
        }
        throw new InvalidOperationException("Chrome hat keinen Vorschau-Kanal geöffnet.");
    }

    static string ResultValue(JsonDocument doc)
    {
        if (!doc.RootElement.TryGetProperty("result", out var result)) return "";
        if (!result.TryGetProperty("result", out var inner)) return "";
        if (!inner.TryGetProperty("value", out var value)) return "";
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
    }

    static string ResultString(JsonDocument doc, string name)
    {
        if (!doc.RootElement.TryGetProperty("result", out var result)) return "";
        return result.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";
    }

    public static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    sealed class Cdp(ClientWebSocket socket)
    {
        int _id;
        public async Task<JsonDocument> Call(string method, object args, CancellationToken ct)
        {
            var id = ++_id;
            var payload = JsonSerializer.Serialize(new { id, method, @params = args });
            var bytes = Encoding.UTF8.GetBytes(payload);
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
            while (true)
            {
                var text = await Read(ct);
                var doc = JsonDocument.Parse(text);
                if (!doc.RootElement.TryGetProperty("id", out var got) || got.GetInt32() != id) { doc.Dispose(); continue; }
                if (doc.RootElement.TryGetProperty("error", out var error))
                {
                    var message = error.TryGetProperty("message", out var m) ? m.GetString() : "unbekannt";
                    doc.Dispose();
                    throw new InvalidOperationException("Chrome hat „" + method + "“ abgelehnt: " + message);
                }
                return doc;
            }
        }

        async Task<string> Read(CancellationToken ct)
        {
            using var ms = new MemoryStream();
            var buf = new byte[64 * 1024];
            while (true)
            {
                var receive = socket.ReceiveAsync(buf, ct);
                var finished = await Task.WhenAny(receive, Task.Delay(TimeSpan.FromSeconds(12)));
                if (finished != receive)
                {
                    try { socket.Abort(); } catch { /* schon zu */ }
                    throw new TimeoutException("Die Vorschau hat nicht geantwortet.");
                }
                var part = await receive;
                if (part.MessageType == WebSocketMessageType.Close) throw new InvalidOperationException("Die Vorschau wurde unterbrochen.");
                ms.Write(buf, 0, part.Count);
                if (part.EndOfMessage) break;
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }
}

/// <summary>Liefert nur den Projektordner aus, auf 127.0.0.1. Kein Zugriff auf den Rest des PCs.</summary>
public sealed class WebsiteHost : IDisposable
{
    public string Origin { get; }
    readonly HttpListener _listener = new();
    readonly CancellationTokenSource _stop = new();
    readonly string _root;
    readonly Task _loop;

    WebsiteHost(string root, int port)
    {
        _root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Origin = "http://127.0.0.1:" + port;
        _listener.Prefixes.Add(Origin + "/");
        _listener.Start();
        _loop = Task.Run(Serve);
    }

    public static WebsiteHost Start(string root)
    {
        Exception? last = null;
        for (var i = 0; i < 5; i++)
        {
            try { return new WebsiteHost(root, WebsitePreview.FreePort()); }
            catch (HttpListenerException ex) { last = ex; }
        }
        throw new InvalidOperationException("Die lokale Vorschau konnte nicht starten.", last);
    }

    async Task Serve()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { break; }
            _ = Task.Run(() => Answer(ctx));
        }
    }

    void Answer(HttpListenerContext ctx)
    {
        try
        {
            if (ctx.Request.HttpMethod is not ("GET" or "HEAD")) { ctx.Response.StatusCode = 405; ctx.Response.Close(); return; }
            var rel = Uri.UnescapeDataString(ctx.Request.Url?.AbsolutePath ?? "/");
            if (rel.Contains('\\') || rel.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..")) { ctx.Response.StatusCode = 403; ctx.Response.Close(); return; }
            if (rel == "/") rel = "/index.html";
            var full = Path.GetFullPath(Path.Combine(_root, rel.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(_root, StringComparison.Ordinal) || !File.Exists(full)) { ctx.Response.StatusCode = 404; ctx.Response.Close(); return; }
            var bytes = File.ReadAllBytes(full);
            ctx.Response.ContentType = Mime(full);
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.Headers["Cache-Control"] = "no-store";
            if (ctx.Request.HttpMethod != "HEAD") ctx.Response.OutputStream.Write(bytes);
            ctx.Response.Close();
        }
        catch { try { ctx.Response.Abort(); } catch { /* Verbindung schon zu */ } }
    }

    static string Mime(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".js" => "text/javascript; charset=utf-8",
        ".json" => "application/json",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        ".glb" => "model/gltf-binary",
        ".gltf" => "model/gltf+json",
        ".txt" or ".md" => "text/plain; charset=utf-8",
        _ => "application/octet-stream"
    };

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Close();
        try { _loop.Wait(500); } catch { /* beenden reicht */ }
    }
}
