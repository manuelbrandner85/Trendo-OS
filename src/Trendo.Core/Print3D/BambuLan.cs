using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Print3D;

/// <summary>N24: Bambu-Lab-Drucker im Heimnetz (LAN-Modus). Status und Befehle über MQTT (TLS, Port 8883, Benutzer „bblp“, Zugangscode vom
/// Druckerbildschirm), Dateien über FTPS (Port 990), Kamerabild (A1/P1: Port 6000; X1: RTSPS über FFmpeg). Keine Cloud, kein Konto.
/// Das Zertifikat des Druckers ist selbst ausgestellt: Trendo merkt sich beim ersten Verbinden seinen Fingerabdruck und lehnt später andere ab.</summary>
public sealed record BambuPrinter(string Name, string Ip, string Serial)
{
    public string SecretKey => $"bambu-zugang-{Serial}";
}

/// <summary>Druckerzustand aus den Statusmeldungen (Felder wie in der Bambu-Firmware).</summary>
public sealed record BambuStatus
{
    public string State { get; init; } = "";          // IDLE, PREPARE, RUNNING, PAUSE, FINISH, FAILED
    public int Percent { get; init; }
    public int RemainingMin { get; init; }
    public int Layer { get; init; }
    public int TotalLayers { get; init; }
    public double Nozzle { get; init; }
    public double NozzleTarget { get; init; }
    public double Bed { get; init; }
    public double BedTarget { get; init; }
    public string File { get; init; } = "";
    public int ErrorCode { get; init; }

    public string StateText => State switch
    {
        "IDLE" => "Bereit", "PREPARE" => "Bereitet vor", "RUNNING" => "Druckt", "PAUSE" => "Pausiert", "FINISH" => "Fertig", "FAILED" => "Fehlgeschlagen", "SLICING" => "Bereitet Datei vor",
        "" => "Unbekannt", _ => State,
    };

    /// <summary>Teilmeldungen ergänzen den bisherigen Stand (der Drucker schickt oft nur geänderte Felder).</summary>
    public BambuStatus Merge(string json)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(json); } catch (JsonException) { return this; }
        if (root?["print"] is not JsonObject p) return this;
        int I(string k, int d) => p[k] is JsonValue v && (v.TryGetValue<int>(out var i) || (v.TryGetValue<double>(out var dd) && (i = (int)dd) == i) || (v.TryGetValue<string>(out var s) && int.TryParse(s, out i))) ? i : d;
        double D(string k, double d) => p[k] is JsonValue v && (v.TryGetValue<double>(out var x) || (v.TryGetValue<string>(out var s) && double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x))) ? x : d;
        string S(string k, string d) => p[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : d;
        return this with
        {
            State = S("gcode_state", State), Percent = I("mc_percent", Percent), RemainingMin = I("mc_remaining_time", RemainingMin),
            Layer = I("layer_num", Layer), TotalLayers = I("total_layer_num", TotalLayers),
            Nozzle = D("nozzle_temper", Nozzle), NozzleTarget = D("nozzle_target_temper", NozzleTarget), Bed = D("bed_temper", Bed), BedTarget = D("bed_target_temper", BedTarget),
            File = S("subtask_name", S("gcode_file", File)), ErrorCode = I("print_error", ErrorCode),
        };
    }
}

/// <summary>Kleinster MQTT-3.1.1-Teil, den Trendo braucht: Verbinden, Abonnieren, Senden, Empfangen, Ping.</summary>
public static class Mqtt
{
    static void Len(List<byte> b, int n) { do { var d = (byte)(n % 128); n /= 128; if (n > 0) d |= 128; b.Add(d); } while (n > 0); }
    static void Str(List<byte> b, string s) { var x = Encoding.UTF8.GetBytes(s); b.Add((byte)(x.Length >> 8)); b.Add((byte)x.Length); b.AddRange(x); }
    static byte[] Packet(byte head, List<byte> body) { var b = new List<byte> { head }; Len(b, body.Count); b.AddRange(body); return [.. b]; }

    public static byte[] Connect(string clientId, string user, string password, int keepAlive = 30)
    {
        var v = new List<byte>(); Str(v, "MQTT"); v.Add(4); v.Add(0b1100_0010); v.Add((byte)(keepAlive >> 8)); v.Add((byte)keepAlive);
        Str(v, clientId); Str(v, user); Str(v, password);
        return Packet(0x10, v);
    }

    public static byte[] Subscribe(ushort id, string topic) { var v = new List<byte> { (byte)(id >> 8), (byte)id }; Str(v, topic); v.Add(0); return Packet(0x82, v); }
    public static byte[] Publish(string topic, string payload) { var v = new List<byte>(); Str(v, topic); v.AddRange(Encoding.UTF8.GetBytes(payload)); return Packet(0x30, v); }
    public static byte[] Ping() => [0xC0, 0x00];
    public static byte[] Disconnect() => [0xE0, 0x00];

    /// <summary>Ein Paket lesen: Art und Inhalt.</summary>
    public static async Task<(byte Type, byte[] Body)?> ReadAsync(Stream s, CancellationToken ct)
    {
        var h = new byte[1];
        if (await s.ReadAsync(h, ct) == 0) return null;
        int len = 0, mul = 1;
        for (var i = 0; i < 4; i++)
        {
            var b = new byte[1]; if (await s.ReadAsync(b, ct) == 0) return null;
            len += (b[0] & 127) * mul; mul *= 128; if ((b[0] & 128) == 0) break;
        }
        var body = new byte[len]; var got = 0;
        while (got < len) { var n = await s.ReadAsync(body.AsMemory(got, len - got), ct); if (n == 0) return null; got += n; }
        return ((byte)(h[0] >> 4), body);
    }

    /// <summary>Inhalt einer PUBLISH-Nachricht (QoS 0): Thema und Text.</summary>
    public static (string Topic, string Payload) ParsePublish(byte[] body, byte flags = 0)
    {
        var tl = (body[0] << 8) | body[1]; var topic = Encoding.UTF8.GetString(body, 2, tl);
        var start = 2 + tl + (((flags >> 1) & 3) > 0 ? 2 : 0);
        return (topic, Encoding.UTF8.GetString(body, start, body.Length - start));
    }
}

/// <summary>Verbindung zu einem Drucker. Der Stream kommt von außen (TLS zum Drucker oder in Tests eine lokale Verbindung).</summary>
public sealed class BambuClient(BambuPrinter printer, string accessCode) : IAsyncDisposable
{
    Stream? _s; CancellationTokenSource? _cts; Task? _loop; readonly SemaphoreSlim _send = new(1, 1);
    public BambuStatus Status { get; private set; } = new();
    public event Action<BambuStatus>? Changed;
    public bool Connected => _s is not null;
    int _seq;

    public string ReportTopic => $"device/{printer.Serial}/report";
    public string RequestTopic => $"device/{printer.Serial}/request";

    /// <summary>TLS-Verbindung mit Fingerabdruck-Prüfung (erstes Mal merken, danach vergleichen).</summary>
    public static async Task<Stream> OpenTls(string host, int port, Func<string?> knownThumb, Action<string> rememberThumb, CancellationToken ct)
    {
        var tcp = new TcpClient(); await tcp.ConnectAsync(host, port, ct);
        var ssl = new SslStream(tcp.GetStream(), false, (_, cert, _, _) =>
        {
            if (cert is null) return false;
            var thumb = new X509Certificate2(cert).GetCertHashString(System.Security.Cryptography.HashAlgorithmName.SHA256);
            var known = knownThumb();
            if (known is null) { rememberThumb(thumb); return true; }
            return string.Equals(known, thumb, StringComparison.OrdinalIgnoreCase);
        });
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = host }, ct);
        return ssl;
    }

    public async Task ConnectAsync(Stream stream, CancellationToken ct = default)
    {
        _s = stream;
        await Send(Mqtt.Connect("trendo-" + Guid.NewGuid().ToString("N")[..8], "bblp", accessCode), ct);
        var ack = await Mqtt.ReadAsync(_s, ct);
        if (ack is not { Type: 2 } a || a.Body.Length < 2 || a.Body[1] != 0)
            throw new InvalidOperationException(ack is { Type: 2 } bad && bad.Body.Length >= 2 && bad.Body[1] is 4 or 5 ? "Der Zugangscode stimmt nicht (am Drucker unter Einstellungen → Netzwerk nachsehen)." : "Der Drucker hat die Verbindung abgelehnt.");
        await Send(Mqtt.Subscribe(1, ReportTopic), ct);
        await RequestAll(ct);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _loop = Task.Run(() => Loop(_cts.Token), _cts.Token);
    }

    async Task Loop(CancellationToken ct)
    {
        var lastPing = DateTime.UtcNow;
        try
        {
            while (!ct.IsCancellationRequested && _s is not null)
            {
                var readTask = Mqtt.ReadAsync(_s, ct);
                while (!readTask.IsCompleted)
                {
                    await Task.WhenAny(readTask, Task.Delay(1000, ct));
                    if (DateTime.UtcNow - lastPing > TimeSpan.FromSeconds(20)) { await Send(Mqtt.Ping(), ct); lastPing = DateTime.UtcNow; }
                }
                var p = await readTask; if (p is null) break;
                if (p.Value.Type == 3)
                {
                    var (topic, payload) = Mqtt.ParsePublish(p.Value.Body);
                    if (topic == ReportTopic) { Status = Status.Merge(payload); Changed?.Invoke(Status); }
                }
            }
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException) { }
    }

    async Task Send(byte[] data, CancellationToken ct)
    {
        await _send.WaitAsync(ct);
        try { await _s!.WriteAsync(data, ct); await _s.FlushAsync(ct); } finally { _send.Release(); }
    }

    string Seq() => Interlocked.Increment(ref _seq).ToString(System.Globalization.CultureInfo.InvariantCulture);

    public Task RequestAll(CancellationToken ct = default) => Send(Mqtt.Publish(RequestTopic, $"{{\"pushing\":{{\"sequence_id\":\"{Seq()}\",\"command\":\"pushall\"}}}}"), ct);

    /// <summary>Druckbefehle: pause, resume, stop.</summary>
    public Task Command(string cmd, CancellationToken ct = default) =>
        cmd is "pause" or "resume" or "stop" ? Send(Mqtt.Publish(RequestTopic, $"{{\"print\":{{\"sequence_id\":\"{Seq()}\",\"command\":\"{cmd}\",\"param\":\"\"}}}}"), ct) : throw new ArgumentException("Unbekannter Befehl.", nameof(cmd));

    /// <summary>Druckauftrag für eine schon hochgeladene, in Bambu Studio vorbereitete Datei (.gcode.3mf) starten.
    /// Nur nach ausdrücklicher Bestätigung in der Oberfläche aufrufen – der Drucker heizt auf und bewegt sich.</summary>
    public Task StartPrint(string remoteFile, int plate = 1, bool useAms = false, CancellationToken ct = default) => Send(Mqtt.Publish(RequestTopic, StartPayload(remoteFile, plate, useAms, Seq())), ct);

    public static string StartPayload(string remoteFile, int plate, bool useAms, string seq)
    {
        var name = Path.GetFileName(remoteFile);
        var o = new JsonObject
        {
            ["print"] = new JsonObject
            {
                ["sequence_id"] = seq, ["command"] = "project_file", ["param"] = $"Metadata/plate_{plate}.gcode", ["project_id"] = "0", ["profile_id"] = "0", ["task_id"] = "0", ["subtask_id"] = "0",
                ["subtask_name"] = name.Replace(".gcode.3mf", "", StringComparison.OrdinalIgnoreCase).Replace(".3mf", "", StringComparison.OrdinalIgnoreCase),
                ["url"] = $"file:///sdcard/{name}", ["timelapse"] = false, ["bed_type"] = "auto", ["bed_levelling"] = true, ["flow_cali"] = false, ["vibration_cali"] = false, ["layer_inspect"] = false, ["use_ams"] = useAms,
            },
        };
        return o.ToJsonString();
    }

    public async ValueTask DisposeAsync()
    {
        try { if (_s is not null) await Send(Mqtt.Disconnect(), CancellationToken.None); } catch (Exception e) when (e is IOException or ObjectDisposedException) { }
        _cts?.Cancel();
        if (_loop is not null) try { await _loop; } catch (OperationCanceledException) { }
        _s?.Dispose(); _s = null;
    }
}

/// <summary>Datei auf die Speicherkarte des Druckers laden (FTPS mit implizitem TLS, Port 990, passiver Modus).</summary>
public static class BambuFtp
{
    public static async Task UploadAsync(string host, string accessCode, string localFile, Func<string?> knownThumb, Action<string> rememberThumb, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        await using var ctrl = await BambuClient.OpenTls(host, 990, knownThumb, rememberThumb, ct);
        var r = new StreamReader(ctrl, Encoding.ASCII); var w = new StreamWriter(ctrl, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
        async Task<string> Reply() { string? line; var last = ""; do { line = await r.ReadLineAsync(ct) ?? throw new IOException("Drucker hat die Verbindung beendet."); last = line; } while (line.Length < 4 || line[3] == '-'); return last; }
        async Task<string> Cmd(string c, params string[] ok) { await w.WriteLineAsync(c); var rep = await Reply(); if (!ok.Any(o => rep.StartsWith(o, StringComparison.Ordinal))) throw new IOException($"Drucker antwortet auf {c.Split(' ')[0]}: {rep}"); return rep; }
        await Reply();
        await Cmd("USER bblp", "331", "230");
        await Cmd($"PASS {accessCode}", "230");
        await Cmd("PBSZ 0", "200"); await Cmd("PROT P", "200"); await Cmd("TYPE I", "200");
        var pasv = await Cmd("PASV", "227");
        var nums = System.Text.RegularExpressions.Regex.Match(pasv, @"\((\d+),(\d+),(\d+),(\d+),(\d+),(\d+)\)").Groups.Values.Skip(1).Select(g => int.Parse(g.Value)).ToArray();
        var port = nums[4] * 256 + nums[5];
        await w.WriteLineAsync($"STOR {Path.GetFileName(localFile)}");
        await using (var data = await BambuClient.OpenTls(host, port, knownThumb, _ => { }, ct))
        {
            var rep = await Reply(); if (!rep.StartsWith('1')) throw new IOException("Hochladen abgelehnt: " + rep);
            await using var f = File.OpenRead(localFile); var buf = new byte[81920]; long done = 0; int n;
            while ((n = await f.ReadAsync(buf, ct)) > 0) { await data.WriteAsync(buf.AsMemory(0, n), ct); done += n; progress?.Report((double)done / Math.Max(1, f.Length)); }
        }
        var fin = await Reply(); if (!fin.StartsWith("226", StringComparison.Ordinal)) throw new IOException("Hochladen nicht bestätigt: " + fin);
        try { await w.WriteLineAsync("QUIT"); } catch (IOException) { }
    }
}

/// <summary>Kamerabild der Drucker A1 und P1 (JPEG über TLS, Port 6000).</summary>
public static class BambuCamera
{
    public static byte[] AuthPacket(string accessCode)
    {
        var b = new byte[80];
        BitConverter.GetBytes(0x40).CopyTo(b, 0); BitConverter.GetBytes(0x3000).CopyTo(b, 4);
        Encoding.ASCII.GetBytes("bblp").CopyTo(b, 16);
        var code = Encoding.ASCII.GetBytes(accessCode); Array.Copy(code, 0, b, 48, Math.Min(32, code.Length));
        return b;
    }

    public static async Task<byte[]> SnapshotAsync(string host, string accessCode, Func<string?> knownThumb, Action<string> rememberThumb, CancellationToken ct = default)
    {
        await using var s = await BambuClient.OpenTls(host, 6000, knownThumb, rememberThumb, ct);
        await s.WriteAsync(AuthPacket(accessCode), ct);
        var head = new byte[16]; await ReadExact(s, head, ct);
        var size = BitConverter.ToInt32(head, 0); if (size is <= 0 or > 10_000_000) throw new IOException("Unerwartete Antwort der Kamera.");
        var img = new byte[size]; await ReadExact(s, img, ct);
        if (img.Length < 4 || img[0] != 0xFF || img[1] != 0xD8) throw new IOException("Die Kamera hat kein Bild geschickt.");
        return img;
    }

    static async Task ReadExact(Stream s, byte[] buf, CancellationToken ct) { var got = 0; while (got < buf.Length) { var n = await s.ReadAsync(buf.AsMemory(got), ct); if (n == 0) throw new IOException("Kamera-Verbindung beendet."); got += n; } }

    /// <summary>X1-Drucker: Kamerabild über RTSPS mit FFmpeg (ein Einzelbild).</summary>
    public static IReadOnlyList<string> RtspArgs(string host, string accessCode, string outFile) =>
        ["-y", "-rtsp_transport", "tcp", "-i", $"rtsps://bblp:{accessCode}@{host}:322/streaming/live/1", "-frames:v", "1", outFile];
}
