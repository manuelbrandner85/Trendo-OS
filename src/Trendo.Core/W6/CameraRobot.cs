using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Eine Webcam oder eine IP-Kamera. Address ist das Gerät oder die RTSP-Adresse.</summary>
public sealed record KnownCamera(string Id, string Kind, string Name, string Address);

public sealed class CameraFacts
{
    public bool Windows { get; init; }
    public IReadOnlyList<(string Path, string Name)> Videos { get; init; } = [];
    public string? DirectShow { get; init; }
}

/// <summary>CORE-09: Kameras finden, Schnappschuss und Aufnahme als FFmpeg-Befehl. Startet selbst nichts.</summary>
public static partial class CameraCatalog
{
    public static IReadOnlyList<KnownCamera> Local(CameraFacts facts)
    {
        var list = new List<KnownCamera>();
        if (facts.Windows)
        {
            foreach (var cam in ParseDirectShow(facts.DirectShow ?? "")) list.Add(cam);
        }
        else
        {
            foreach (var v in facts.Videos)
            {
                var name = string.IsNullOrWhiteSpace(v.Name) ? v.Path : v.Name.Trim();
                list.Add(new("webcam:" + v.Path, "webcam", name, v.Path));
            }
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return list.Where(c => seen.Add(c.Id)).ToList();
    }

    public static IReadOnlyList<KnownCamera> ParseDirectShow(string text)
    {
        var list = new List<KnownCamera>();
        var video = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Contains("video devices", StringComparison.OrdinalIgnoreCase)) { video = true; continue; }
            if (line.Contains("audio devices", StringComparison.OrdinalIgnoreCase)) { video = false; continue; }
            if (!video || line.Contains("Alternative name", StringComparison.OrdinalIgnoreCase)) continue;
            var q1 = line.IndexOf('"');
            var q2 = q1 >= 0 ? line.IndexOf('"', q1 + 1) : -1;
            if (q1 < 0 || q2 <= q1) continue;
            if (!line.Contains("(video)", StringComparison.OrdinalIgnoreCase) && !line.Contains("video devices", StringComparison.OrdinalIgnoreCase) && !video) continue;
            var name = line[(q1 + 1)..q2].Trim();
            if (name.Length == 0) continue;
            list.Add(new("webcam:" + name, "webcam", name, name));
        }
        return list;
    }

    public static KnownCamera Rtsp(string name, string url)
    {
        var n = name.Trim();
        if (n.Length < 2) throw new ArgumentException("Bitte der Kamera einen Namen geben.");
        var u = url.Trim();
        if (!u.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase) && !u.StartsWith("rtsps://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Die Adresse muss mit rtsp:// oder rtsps:// beginnen.");
        if (!Uri.TryCreate(u, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
            throw new ArgumentException("Die RTSP-Adresse ist nicht vollständig.");
        return new("rtsp:" + u, "rtsp", n, u);
    }

    public static string Redact(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || string.IsNullOrEmpty(u.UserInfo)) return url;
        return u.Scheme + "://…@" + u.Host + (u.IsDefaultPort ? "" : ":" + u.Port) + u.PathAndQuery;
    }

    public static string[] Snapshot(KnownCamera cam, string png, bool windows) => Output(cam, png, windows, 0, oneFrame: true);

    public static string[] Record(KnownCamera cam, string file, int seconds, bool windows)
    {
        if (seconds < 0 || seconds > 3600) throw new ArgumentException("Die Dauer muss zwischen 0 und 3600 Sekunden liegen. 0 heißt: bis du stoppst.");
        return Output(cam, file, windows, seconds, oneFrame: false);
    }

    private static string[] Output(KnownCamera cam, string file, bool windows, int seconds, bool oneFrame)
    {
        var a = new List<string> { "-hide_banner", "-loglevel", "error", "-y" };
        if (cam.Kind == "webcam" && windows) a.AddRange(["-f", "dshow", "-i", "video=" + cam.Address]);
        else if (cam.Kind == "webcam") a.AddRange(["-f", "v4l2", "-framerate", "30", "-i", cam.Address]);
        else a.AddRange(["-rtsp_transport", "tcp", "-i", cam.Address]);
        if (oneFrame) a.AddRange(["-frames:v", "1", file]);
        else
        {
            if (seconds > 0) a.AddRange(["-t", seconds.ToString(CultureInfo.InvariantCulture)]);
            a.AddRange(["-c:v", "libx264", "-preset", "veryfast", "-pix_fmt", "yuv420p", file]);
        }
        return a.ToArray();
    }
}

public sealed class CameraBook
{
    public List<KnownCamera> Saved { get; set; } = [];

    public static CameraBook Load(string path)
    {
        if (!File.Exists(path)) return new();
        try { return JsonSerializer.Deserialize<CameraBook>(File.ReadAllText(path)) ?? new(); }
        catch (JsonException) { return new(); }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

/// <summary>ONVIF: Suchpaket, SOAP und das Lesen der RTSP-Adresse. Sendet selbst nichts.</summary>
public static partial class Onvif
{
    public static byte[] Probe()
    {
        var id = "uuid:" + Guid.NewGuid();
        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <e:Envelope xmlns:e="http://www.w3.org/2003/05/soap-envelope" xmlns:w="http://schemas.xmlsoap.org/ws/2004/08/addressing" xmlns:d="http://schemas.xmlsoap.org/ws/2005/04/discovery" xmlns:dn="http://www.onvif.org/ver10/network/wsdl">
              <e:Header>
                <w:MessageID>{id}</w:MessageID>
                <w:To>urn:schemas-xmlsoap-org:ws:2005:04:discovery</w:To>
                <w:Action>http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe</w:Action>
              </e:Header>
              <e:Body><d:Probe><d:Types>dn:NetworkVideoTransmitter</d:Types></d:Probe></e:Body>
            </e:Envelope>
            """;
        return Encoding.UTF8.GetBytes(xml);
    }

    public static IReadOnlyList<string> XAddrs(string xml)
    {
        var list = new List<string>();
        foreach (Match m in Xaddr().Matches(xml))
            foreach (var part in m.Groups[1].Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                if (part.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || part.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    list.Add(part.Trim());
        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string DeviceService(string host)
    {
        var h = host.Trim();
        if (h.Length == 0) throw new ArgumentException("Bitte die Adresse der Kamera eintragen.");
        if (h.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || h.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(h, UriKind.Absolute, out var u) || u.Host.Length == 0) throw new ArgumentException("Die Adresse ist nicht vollständig.");
            return h;
        }
        if (h.Contains("://", StringComparison.Ordinal)) throw new ArgumentException("Für ONVIF brauche ich eine IP-Adresse oder http://…");
        return "http://" + h.TrimEnd('/') + "/onvif/device_service";
    }

    public static async Task<string> Resolve(Func<string, string, CancellationToken, Task<string>> post, string host, string? user, string? password, CancellationToken ct = default)
    {
        var device = DeviceService(host);
        var caps = await post(device, Envelope("tds", "http://www.onvif.org/ver10/device/wsdl", "GetCapabilities", "<tds:Category>All</tds:Category>", user, password), ct);
        var media = MediaXAddr(caps) ?? throw new InvalidOperationException("Die Kamera nennt keinen Medien-Dienst.");
        var profiles = await post(media, Envelope("trt", "http://www.onvif.org/ver10/media/wsdl", "GetProfiles", "", user, password), ct);
        var token = ProfileToken(profiles) ?? throw new InvalidOperationException("Die Kamera hat kein Profil.");
        var uriXml = await post(media, Envelope("trt", "http://www.onvif.org/ver10/media/wsdl", "GetStreamUri", StreamBody(token), user, password), ct);
        return StreamUri(uriXml) ?? throw new InvalidOperationException("Die Kamera hat keine RTSP-Adresse geliefert.");
    }

    public static string? MediaXAddr(string xml)
    {
        var i = xml.IndexOf("Media", StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        var m = Regex.Match(xml[i..], @"XAddr>\s*([^<]+)\s*<", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    public static string? ProfileToken(string xml)
    {
        var m = Regex.Match(xml, @"Profiles\b[^>]*\btoken\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    public static string? StreamUri(string xml)
    {
        var m = Regex.Match(xml, @">\s*(rtsps?://[^<\s]+)\s*<", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    public static string Envelope(string prefix, string ns, string action, string inner, string? user, string? password)
    {
        var header = string.IsNullOrEmpty(user) ? "" : Security(user, password ?? "");
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:{prefix}="{ns}" xmlns:tt="http://www.onvif.org/ver10/schema">
              <s:Header>{header}</s:Header>
              <s:Body><{prefix}:{action}>{inner}</{prefix}:{action}></s:Body>
            </s:Envelope>
            """;
    }

    private static string StreamBody(string token) =>
        "<trt:StreamSetup><tt:Stream>RTP-Unicast</tt:Stream><tt:Transport><tt:Protocol>RTSP</tt:Protocol></tt:Transport></trt:StreamSetup><trt:ProfileToken>" + Esc(token) + "</trt:ProfileToken>";

    private static string Security(string user, string password) =>
        "<wsse:Security xmlns:wsse=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd\"><wsse:UsernameToken><wsse:Username>" + Esc(user) + "</wsse:Username><wsse:Password>" + Esc(password) + "</wsse:Password></wsse:UsernameToken></wsse:Security>";

    private static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    [GeneratedRegex(@"XAddrs>\s*([^<]+)\s*<", RegexOptions.IgnoreCase)]
    private static partial Regex Xaddr();
}

public sealed record MotionOrder(string Device, double Base, double Shoulder, double Elbow, double Seconds, string Gripper)
{
    public string Line
    {
        get
        {
            var name = Device.Trim();
            if (name.Length == 0 || name.Any(char.IsWhiteSpace)) throw new ArgumentException("Der Gerätename darf keine Leerzeichen haben.");
            if (Seconds <= 0 || Seconds > 60) throw new ArgumentException("Die Dauer muss größer als 0 und höchstens 60 Sekunden sein.");
            if (Gripper is not ("halten" or "greifen" or "loslassen")) throw new ArgumentException("Greifer: halten, greifen oder loslassen.");
            foreach (var a in new[] { Base, Shoulder, Elbow })
                if (a is < -360 or > 360 || double.IsNaN(a)) throw new ArgumentException("Ein Winkel muss zwischen −360 und 360 Grad liegen.");
            return string.Create(CultureInfo.InvariantCulture, $"MOVE {name} {Base:0.###} {Shoulder:0.###} {Elbow:0.###} {Seconds:0.###} {Gripper}");
        }
    }

    public string Target => Line;
}

/// <summary>Bewegung: Simulation und Probelauf senden nichts. Echt nur im Freigabe-Gate, und nie bei Not-Aus.</summary>
public sealed class RobotLink
{
    public const string Action = "robot.move";
    public bool Emergency { get; private set; }
    public List<string> Journal { get; } = [];

    public void Estop()
    {
        Emergency = true;
        Journal.Add("NOT-AUS. Es bewegt sich nichts.");
    }

    public void Release()
    {
        Emergency = false;
        Journal.Add("Not-Aus gelöst. Eine echte Bewegung braucht wieder eine Freigabe.");
    }

    public string Simulate(ArmConfig arm, double x, double y, double z)
    {
        var j = RobotArm.Inverse(arm, x, y, z);
        var (fx, fy, fz) = RobotArm.Forward(arm, j);
        var line = $"Simulation: Sockel {N(j.Base)}°, Schulter {N(j.Shoulder)}°, Ellbogen {N(j.Elbow)}°. Der Greifer wäre bei {N(fx)} / {N(fy)} / {N(fz)} mm. Es wurde nichts gesendet.";
        Journal.Add(line);
        return line;
    }

    public string Dry(MotionOrder order)
    {
        var line = "Probelauf: " + order.Line + ". Es wurde nichts gesendet.";
        Journal.Add(line);
        return line;
    }

    public ApprovalRequest Ask(ApprovalGate gate, Principal who, MotionOrder order) =>
        gate.Request(who, Action, "Echte Bewegung am Gerät " + order.Device.Trim(), order.Target, TimeSpan.FromMinutes(5));

    public string Send(ApprovalGate gate, string approvalId, MotionOrder order, Action<string> send)
    {
        if (Emergency) throw new InvalidOperationException("Not-Aus ist aktiv. Es wurde nichts gesendet.");
        return gate.Execute(approvalId, Action, order.Target, () =>
        {
            if (Emergency) throw new InvalidOperationException("Not-Aus ist aktiv. Es wurde nichts gesendet.");
            send(order.Line);
            var line = "Gesendet: " + order.Line;
            Journal.Add(line);
            return line;
        });
    }

    private static string N(double v) => v.ToString("0.0", CultureInfo.GetCultureInfo("de-DE"));
}

public static class RobotWire
{
    public static void Send(string port, string line)
    {
        if (string.IsNullOrWhiteSpace(port)) throw new InvalidOperationException("Bitte den Anschluss eintragen, zum Beispiel COM3 oder /dev/ttyUSB0.");
        var p = port.Trim();
        if (!File.Exists(p) && !p.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
            throw new IOException($"Der Anschluss „{p}“ ist nicht da. Es wurde nichts gesendet.");
        var path = p.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ? @"\\.\" + p : p;
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        fs.Write(Encoding.ASCII.GetBytes(line.Trim() + "\n"));
    }
}
