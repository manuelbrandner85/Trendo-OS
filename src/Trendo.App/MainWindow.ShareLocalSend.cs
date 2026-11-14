using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 9 – SYS-06: „Ans Handy senden“ über das offene LocalSend-Protokoll. Handys mit der LocalSend-App im selben Netz finden,
/// Dateien senden (das Handy fragt selbst nach, ob es annimmt). Nur eigenes Netz, Trendo öffnet dafür keinen Server.</summary>
public partial class MainWindow
{
    public List<LocalSend.Peer> LsPeers { get; private set; } = [];
    string _lsMsg = "", _lsFile = ""; public string LocalSendMessage => _lsMsg;
    static readonly string LsFingerprint = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    /// <summary>Handys suchen: ankündigen und einige Sekunden auf Antworten hören (öffentlich für Tests, Gruppe/Port dort änderbar).</summary>
    public async Task LocalSendDiscover(int seconds = 4, string group = LocalSend.MulticastAddress, int port = LocalSend.Port)
    {
        _lsMsg = "Suche Handys im Netz …"; Show("teilen");
        var found = new Dictionary<string, LocalSend.Peer>();
        try
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            udp.JoinMulticastGroup(IPAddress.Parse(group));
            var msg = Encoding.UTF8.GetBytes(LocalSend.Announcement(LsFingerprint));
            await udp.SendAsync(msg, msg.Length, new IPEndPoint(IPAddress.Parse(group), port));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 15)));
            try
            {
                while (true)
                {
                    var r = await udp.ReceiveAsync(cts.Token);
                    if (LocalSend.ParsePeer(Encoding.UTF8.GetString(r.Buffer), r.RemoteEndPoint.Address, LsFingerprint) is { } p) found[p.Fingerprint] = p;
                }
            }
            catch (OperationCanceledException) { }
            _lsMsg = found.Count == 0 ? "Kein Handy gefunden. Ist LocalSend dort geöffnet und im selben WLAN?" : $"{found.Count} Gerät(e) gefunden.";
        }
        catch (SocketException e) { _lsMsg = "⚠ Netzwerksuche ging nicht: " + e.Message; }
        LsPeers = [.. found.Values.OrderBy(p => p.Alias)];
        Show("teilen");
    }

    /// <summary>Dateien an ein Gerät senden (öffentlich für Tests). Rückgabe: übertragene Dateien.</summary>
    public async Task<int> LocalSendSend(LocalSend.Peer peer, IReadOnlyList<string> paths, string? pin = null)
    {
        var missing = paths.FirstOrDefault(p => !File.Exists(p));
        if (paths.Count == 0 || missing is not null) { _lsMsg = "⚠ Diese Datei gibt es nicht."; Show("teilen"); return 0; }
        if (!IPAddress.TryParse(peer.Address, out var ip) || !LocalSend.IsLocal(ip)) { _lsMsg = "⚠ Senden nur innerhalb des eigenen Netzes."; Show("teilen"); return 0; }
        _lsMsg = $"Warte auf Bestätigung am Gerät „{peer.Alias}“ …"; Show("teilen");
        using var handler = new HttpClientHandler();
        if (peer.Protocol == "https") handler.ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert is not null && LocalSend.CertificateMatches(cert.RawData, peer.Fingerprint); // selbst signiert: gegen den angekündigten Fingerabdruck prüfen
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var sent = 0; string? session = null;
        try
        {
            var files = new List<LocalSend.SendFile>();
            foreach (var p in paths)
            {
                await using var s = File.OpenRead(p);
                files.Add(new(Guid.NewGuid().ToString("N"), p, Path.GetFileName(p), s.Length, Convert.ToHexStringLower(await SHA256.HashDataAsync(s)), LocalSend.FileType(p)));
            }
            using var prep = await http.PostAsync(LocalSend.PrepareUrl(peer, pin), new StringContent(LocalSend.PrepareBody(LsFingerprint, files), Encoding.UTF8, "application/json"));
            if ((int)prep.StatusCode == 204 || !prep.IsSuccessStatusCode) { _lsMsg = ((int)prep.StatusCode == 204 ? "• " : "⚠ ") + LocalSend.Error((int)prep.StatusCode); Show("teilen"); return 0; }
            var (sid, tokens) = LocalSend.ParsePrepare(await prep.Content.ReadAsStringAsync()); session = sid;
            foreach (var f in files)
            {
                if (!tokens.TryGetValue(f.Id, out var token)) continue; // am Handy abgewählt
                _lsMsg = $"Sende {f.FileName} … ({sent + 1} von {tokens.Count})"; Show("teilen");
                await using var body = File.OpenRead(f.Path);
                using var up = await http.PostAsync(LocalSend.UploadUrl(peer, sid, f.Id, token), new StreamContent(body));
                if (!up.IsSuccessStatusCode) throw new InvalidOperationException(LocalSend.Error((int)up.StatusCode));
                sent++;
            }
            session = null;
            _lsMsg = $"✓ {sent} Datei(en) an „{peer.Alias}“ gesendet.";
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidOperationException or FormatException or System.Text.Json.JsonException or TaskCanceledException)
        {
            _lsMsg = "⚠ Senden ging nicht: " + e.Message;
            if (session is not null) try { using var c = await http.PostAsync(LocalSend.CancelUrl(peer, session), null); } catch (HttpRequestException) { } // Sitzung am Handy freigeben
        }
        _core.Audit.Append(Principal.LocalOwner.Name, "share.localsend", $"{sent} Datei(en)");
        Show("teilen"); return sent;
    }

    Control ShareLocalSendCard()
    {
        var file = new TextBox { Name = "ls-file", Text = _lsFile, PlaceholderText = "Datei (mehrere mit ;)", Width = 380 };
        file.TextChanged += (_, _) => _lsFile = file.Text ?? "";
        var pin = new TextBox { Name = "ls-pin", PlaceholderText = "PIN (falls verlangt)", Width = 140 };
        var body = new StackPanel { Spacing = 6, Children = { new WrapPanel { Children = { MSp(file), MSp(pin), MSp(Act("ls-find", "Handys suchen", () => _ = LocalSendDiscover(), true)) } } } };
        var i = 0;
        foreach (var p in LsPeers)
        {
            var peer = p;
            body.Children.Add(Row(P(p.Label, Text, 12.5), Act($"ls-send-{i++}", "Senden", () => _ = LocalSendSend(peer, (file.Text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.Trim('"')).ToList(), pin.Text))));
        }
        if (_lsMsg.Length > 0) body.Children.Add(new TextBlock { Name = "ls-msg", Text = _lsMsg, TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = _lsMsg.StartsWith('⚠') ? Brush.Parse("#F97316") : Text });
        return ToolCard("ls-card", Mdi.Wifi, "#93C5FD", "#1D4ED8", "Ans Handy senden (LocalSend)", "Schickt Dateien direkt an ein Handy im selben WLAN – dort muss die kostenlose App LocalSend geöffnet sein. Das Handy fragt, ob es annimmt.", body);
    }
}
