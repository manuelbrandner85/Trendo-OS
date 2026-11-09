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

    /// <summary><paramref name="errors"/>: sammelt Skriptfehler, Konsolen-Fehler und nicht gefundene Dateien der Seite („echt testen“).</summary>
    /// <param name="domPath">Falls gesetzt: Die fertig gezeichnete Seite wird dort als HTML abgelegt, und zu breiter Inhalt landet in <paramref name="errors"/> (Prüfung des Exports, A6).</param>
    public static async Task<WebsitePreviewResult> CaptureAsync(string projectDir, int width, int height, string pngPath, string? query = null, CancellationToken ct = default, List<string>? errors = null, string? domPath = null)
    {
        var index = Path.Combine(projectDir, "index.html");
        if (!File.Exists(index)) return new(false, "", "Die Seite hat keine index.html.");
        var browser = FindBrowser();
        if (browser is null) return new(false, "", "Kein Chrome oder Chromium. Die Bildvorschau geht hier nicht. Öffne die index.html im Browser.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(pngPath))!);
        using var host = WebsiteHost.Start(projectDir);
        var url = host.Origin + "/index.html" + (string.IsNullOrWhiteSpace(query) ? "" : "?" + query.TrimStart('?'));
        await ShotAsync(browser, url, width, height, pngPath, ct, errors, domPath);
        return File.Exists(pngPath) ? new(true, pngPath, width < 700 ? "Handy" : "Desktop") : new(false, "", "Die Vorschau hat kein Bild geschrieben.");
    }

    static async Task ShotAsync(string browser, string url, int width, int height, string pngPath, CancellationToken ct, List<string>? errors = null, string? domPath = null)
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
        using var job = new ProcessJob(); // Chrome-Hilfsprozesse enden mit dem Job – Kill(true) erwischt nicht alle
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Chrome ließ sich nicht starten.");
        job.Add(process);
        _ = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        ClientWebSocket? socket = null;
        // ReceiveAsync und GetStringAsync lassen den Abbruch auf diesem Chrome oft liegen. Dann bleibt der Prozess stehen.
        using var wake = timeout.Token.Register(() => Cut(socket, http, process));
        try
        {
            var work = TakeShot();
            var done = await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(45))); // Prüfung 04.11.2026: unter Last (oder auf langsamen PCs) reichten 27 s nicht
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
            job.Dispose(); // beendet alle übrigen Chrome-Prozesse, danach ist das Profil frei
            try { process.WaitForExit(3000); } catch (InvalidOperationException) { }
            try { Directory.Delete(profile, true); } catch { /* Chrome hält die Datei manchmal noch */ }
        }

        async Task TakeShot()
        {
            var wsUrl = await PageSocket(http, port, timeout.Token);
            socket = new ClientWebSocket();
            await socket.ConnectAsync(new Uri(wsUrl), timeout.Token);
            var cdp = new Cdp(socket) { OnEvent = errors is null ? null : e => Collect(e, errors) };
            await cdp.Call("Page.enable", new { }, timeout.Token);
            if (errors is not null) { await cdp.Call("Runtime.enable", new { }, timeout.Token); await cdp.Call("Log.enable", new { }, timeout.Token); }
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
            await Task.Delay(errors is null ? 400 : 1200, timeout.Token); // beim echten Test etwas länger laufen lassen, damit späte Fehler auffallen
            if (errors is not null) await cdp.Call("Runtime.evaluate", new { expression = "1" }, timeout.Token); // ausstehende Meldungen abholen
            if (domPath is not null)
            {
                var dom = await cdp.Call("Runtime.evaluate", new { expression = "document.documentElement.outerHTML", returnByValue = true }, timeout.Token);
                await File.WriteAllTextAsync(domPath, "<!doctype html>\n" + ResultValue(dom), timeout.Token);
                var over = await cdp.Call("Runtime.evaluate", new { expression = "Math.max(document.documentElement.scrollWidth, document.body ? document.body.scrollWidth : 0) - window.innerWidth", returnByValue = true }, timeout.Token);
                if (errors is not null && double.TryParse(ResultValue(over), System.Globalization.CultureInfo.InvariantCulture, out var extra) && extra > 1)
                    lock (errors) errors.Add($"Waagerechtes Scrollen bei {width} Pixeln Breite ({extra:0} Pixel zu breit).");
            }
            var shot = await cdp.Call("Page.captureScreenshot", new { format = "png" }, timeout.Token);
            var data = ResultString(shot, "data");
            if (string.IsNullOrWhiteSpace(data)) throw new InvalidOperationException("Chrome hat kein Bild geliefert.");
            await File.WriteAllBytesAsync(pngPath, Convert.FromBase64String(data), timeout.Token);
        }
    }

    /// <summary>Fehlermeldung der Seite in verständlicher Form merken (ohne Adresse des lokalen Servers).</summary>
    static void Collect(JsonElement e, List<string> errors)
    {
        var method = e.TryGetProperty("method", out var m) ? m.GetString() : null;
        if (!e.TryGetProperty("params", out var p)) return;
        string? text = null;
        if (method == "Runtime.exceptionThrown" && p.TryGetProperty("exceptionDetails", out var d))
        {
            var msg = d.TryGetProperty("exception", out var ex) && ex.TryGetProperty("description", out var desc) ? desc.GetString() : d.TryGetProperty("text", out var t) ? t.GetString() : "Fehler";
            var line = d.TryGetProperty("lineNumber", out var ln) ? ln.GetInt32() + 1 : 0;
            var src = d.TryGetProperty("url", out var u) ? System.IO.Path.GetFileName(new Uri(u.GetString() ?? "x:/", UriKind.Absolute).AbsolutePath) : "";
            text = $"Skriptfehler{(src.Length > 0 ? " in " + src : "")}{(line > 0 ? " Zeile " + line : "")}: {msg?.Split('\n')[0]}";
        }
        else if (method == "Runtime.consoleAPICalled" && p.TryGetProperty("type", out var ty) && ty.GetString() == "error" && p.TryGetProperty("args", out var args))
            text = "Konsole: " + string.Join(" ", args.EnumerateArray().Select(a => a.TryGetProperty("value", out var v) ? v.ToString() : a.TryGetProperty("description", out var ds) ? ds.GetString() : ""));
        else if (method == "Log.entryAdded" && p.TryGetProperty("entry", out var en) && en.TryGetProperty("level", out var lv) && lv.GetString() == "error")
        {
            var raw = en.TryGetProperty("text", out var tx) ? tx.GetString() ?? "Fehler" : "Fehler";
            var path = en.TryGetProperty("url", out var eu) && eu.GetString() is { Length: > 0 } uu && Uri.TryCreate(uu, UriKind.Absolute, out var lu) ? lu.AbsolutePath.TrimStart('/') : "";
            if (path.Equals("favicon.ico", StringComparison.OrdinalIgnoreCase)) return; // fehlendes Seitensymbol fragt der Browser von selbst an – kein Fehler der Seite
            text = raw.Contains("404") && path.Length > 0 ? $"Datei nicht gefunden: {path} (die Seite verlinkt sie, sie liegt aber nicht im Projekt)" : raw + (path.Length > 0 ? $" ({path})" : "");
        }
        if (text is null) return;
        text = System.Text.RegularExpressions.Regex.Replace(text, @"https?://127\.0\.0\.1:\d+/", "");
        if (text.Length > 300) text = text[..300] + "…";
        lock (errors) if (!errors.Contains(text)) errors.Add(text);
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
        /// <summary>Ereignisse ohne Antwort-Nummer (Konsole, Fehler) – nur wenn jemand zuhört.</summary>
        public Action<JsonElement>? OnEvent { get; init; }
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
                if (!doc.RootElement.TryGetProperty("id", out var got) || got.GetInt32() != id) { if (OnEvent is { } on && doc.RootElement.TryGetProperty("method", out _)) on(doc.RootElement); doc.Dispose(); continue; }
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
                var finished = await Task.WhenAny(receive, Task.Delay(TimeSpan.FromSeconds(25)));
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
