using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 9 – SYS-06: LocalSend-Sendeweg über echte Verbindungen gegen einen Empfänger, der genau die Protokollbeschreibung umsetzt (HTTP, Token, SHA-256).</summary>
public class LocalSend_Ui
{
    /// <summary>Empfänger nach Protokoll v2: prepare-upload (Token je Datei, PIN, Ablehnen), upload (Token prüfen, sha256 → 422), cancel.</summary>
    sealed class Receiver : IDisposable
    {
        readonly HttpListener _l = new(); readonly CancellationTokenSource _cts = new();
        public int Port { get; }
        public Dictionary<string, byte[]> Received { get; } = []; public List<string> Paths { get; } = [];
        public string? RequirePin, DeclineFile; public bool Reject, BreakChecksum;
        Dictionary<string, (string Name, string Sha, string Token)> _files = [];
        public Receiver()
        {
            for (var tries = 0; ; tries++)
            {
                var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start(); Port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
                _l.Prefixes.Clear(); _l.Prefixes.Add($"http://127.0.0.1:{Port}/");
                try { _l.Start(); break; } catch (HttpListenerException) when (tries < 5) { }
            }
            _ = Task.Run(Loop);
        }
        async Task Loop()
        {
            while (!_cts.IsCancellationRequested)
            {
                HttpListenerContext c; try { c = await _l.GetContextAsync(); } catch (Exception) { return; }
                try { await Handle(c); } finally { c.Response.Close(); }
            }
        }
        async Task Handle(HttpListenerContext c)
        {
            Paths.Add(c.Request.Url!.AbsolutePath); var q = c.Request.QueryString;
            using var ms = new MemoryStream(); await c.Request.InputStream.CopyToAsync(ms); var body = ms.ToArray();
            switch (c.Request.Url.AbsolutePath)
            {
                case "/api/localsend/v2/prepare-upload":
                    if (Reject) { c.Response.StatusCode = 403; return; }
                    if (RequirePin is not null && q["pin"] != RequirePin) { c.Response.StatusCode = 401; return; }
                    var o = JsonNode.Parse(body)!; Assert.NotNull(o["info"]!["alias"]);
                    _files = []; var map = new JsonObject();
                    foreach (var (id, f) in o["files"]!.AsObject())
                    {
                        var name = f!["fileName"]!.GetValue<string>(); if (name == DeclineFile) continue;
                        var tok = Guid.NewGuid().ToString("N"); _files[id] = (name, f["sha256"]!.GetValue<string>(), tok); map[id] = tok;
                    }
                    var res = Encoding.UTF8.GetBytes(new JsonObject { ["sessionId"] = "sitzung-1", ["files"] = map }.ToJsonString());
                    c.Response.ContentType = "application/json"; await c.Response.OutputStream.WriteAsync(res);
                    return;
                case "/api/localsend/v2/upload":
                    if (q["sessionId"] != "sitzung-1" || !_files.TryGetValue(q["fileId"] ?? "", out var e) || e.Token != q["token"]) { c.Response.StatusCode = 403; return; }
                    var sha = Convert.ToHexStringLower(SHA256.HashData(body));
                    if (BreakChecksum || sha != e.Sha) { c.Response.StatusCode = 422; return; }
                    Received[e.Name] = body; return;
                case "/api/localsend/v2/cancel": return;
                default: c.Response.StatusCode = 404; return;
            }
        }
        public void Dispose() { _cts.Cancel(); try { _l.Stop(); _l.Close(); } catch (ObjectDisposedException) { } }
    }

    static LocalSend.Peer Peer(int port) => new("Testhandy", "127.0.0.1", port, "http", "FP", "Pixel", "mobile");

    [AvaloniaFact(DisplayName = "LocalSend echt über HTTP: Dateien kommen Byte für Byte an (Token, SHA-256); abgewählte Datei entfällt; Ablehnung, PIN und Prüfsummenfehler verständlich und mit cancel")]
    public async Task Send()
    {
        var (w, _, root) = Ui.Open(); var dir = Path.Combine(root, "senden"); Directory.CreateDirectory(dir);
        var a = Path.Combine(dir, "urlaub bild.jpg"); var data = new byte[300_000]; new Random(3).NextBytes(data); File.WriteAllBytes(a, data);
        var b = Path.Combine(dir, "notiz.txt"); File.WriteAllText(b, "Größe äöü");
        using var r = new Receiver();
        try
        {
            w.Show("teilen"); Assert.NotNull(w.Find<Button>("ls-find"));
            Assert.Equal(2, await w.LocalSendSend(Peer(r.Port), [a, b]));
            Assert.Equal("✓ 2 Datei(en) an „Testhandy“ gesendet.", w.LocalSendMessage);
            Assert.Equal(data, r.Received["urlaub bild.jpg"]); Assert.Equal(File.ReadAllBytes(b), r.Received["notiz.txt"]);
            Assert.Equal("/api/localsend/v2/prepare-upload", r.Paths[0]);

            r.Received.Clear(); r.DeclineFile = "notiz.txt";
            Assert.Equal(1, await w.LocalSendSend(Peer(r.Port), [a, b])); Assert.Single(r.Received); r.DeclineFile = null;

            r.Reject = true; Assert.Equal(0, await w.LocalSendSend(Peer(r.Port), [a])); Assert.Contains("abgelehnt", w.LocalSendMessage); r.Reject = false;

            r.RequirePin = "4711";
            Assert.Equal(0, await w.LocalSendSend(Peer(r.Port), [a])); Assert.Contains("PIN", w.LocalSendMessage);
            Assert.Equal(1, await w.LocalSendSend(Peer(r.Port), [a], "4711")); r.RequirePin = null;

            r.BreakChecksum = true; r.Paths.Clear();
            Assert.Equal(0, await w.LocalSendSend(Peer(r.Port), [a])); Assert.Contains("Prüfsumme", w.LocalSendMessage); Assert.Contains("/api/localsend/v2/cancel", r.Paths); r.BreakChecksum = false;

            Assert.Equal(0, await w.LocalSendSend(Peer(r.Port), [Path.Combine(dir, "weg.bin")])); Assert.Contains("gibt es nicht", w.LocalSendMessage);
            Assert.Equal(0, await w.LocalSendSend(new("Fremd", "8.8.8.8", 53317, "http", "FP", "", ""), [a])); Assert.Contains("eigenen Netzes", w.LocalSendMessage);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "LocalSend echt über UDP: Antwort eines Geräts erscheint in der Liste mit Senden-Knopf; ohne Antwort verständlicher Hinweis")]
    public async Task Discover()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var port = Free();
            var find = w.LocalSendDiscover(2, LocalSend.MulticastAddress, port);
            using (var udp = new UdpClient(AddressFamily.InterNetwork))
            {
                await Task.Delay(300);
                var msg = Encoding.UTF8.GetBytes("""{"alias":"Nice Orange","version":"2.0","deviceModel":"Samsung","deviceType":"mobile","fingerprint":"ABC","port":53317,"protocol":"https","announce":false}""");
                await udp.SendAsync(msg, msg.Length, new IPEndPoint(IPAddress.Loopback, port));
            }
            await find;
            Assert.Equal("Nice Orange (Samsung)", Assert.Single(w.LsPeers).Label);
            Assert.NotNull(w.Find<Button>("ls-send-0")); Assert.Equal("1 Gerät(e) gefunden.", w.LocalSendMessage);
            await w.LocalSendDiscover(1, LocalSend.MulticastAddress, Free());
            Assert.Empty(w.LsPeers); Assert.StartsWith("Kein Handy gefunden", w.LocalSendMessage);
        }
        finally { w.Close(); }
    }

    static int Free() { var u = new UdpClient(0); var p = ((IPEndPoint)u.Client.LocalEndPoint!).Port; u.Close(); return p; }
}
