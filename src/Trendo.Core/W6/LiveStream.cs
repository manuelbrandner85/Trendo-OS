using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core;

public enum StreamKind { Bildschirm, Fenster, Kamera, Mikrofon }

/// <summary>Eine aufnehmbare Quelle. Device ist der technische Name für FFmpeg, Size nur bei Bildschirm und Fenster.</summary>
public sealed record StreamSource(string Id, StreamKind Kind, string Label, string Device, string? Size);

/// <summary>Was die Umgebung wirklich gemeldet hat. Leer heißt: nichts gefunden, nicht „irgendetwas annehmen“.</summary>
public sealed class CaptureFacts
{
    public bool Windows { get; init; }
    public string? Display { get; init; }
    public IReadOnlyList<(string Path, string Name)> Videos { get; init; } = [];
    public string? Xrandr { get; init; }
    public string? Wmctrl { get; init; }
    public IReadOnlyDictionary<string, string> WindowInfo { get; init; } = new Dictionary<string, string>();
    public string? Pulse { get; init; }
    public string? DirectShow { get; init; }
}

public enum LiveOut { Vorschau, Aufnahme, Sendung }

/// <summary>SPEC-15: echte Quellen, FFmpeg-Befehl und Freigabe. Startet selbst nichts.</summary>
public static partial class LiveStream
{
    public const string Action = "live.start";
    public static readonly (string Name, string Server)[] Platforms =
    [
        ("YouTube", "rtmp://a.rtmp.youtube.com/live2"),
        ("Twitch", "rtmp://live.twitch.tv/app"),
        ("Eigener Server", "rtmp://"),
    ];

    public static IReadOnlyList<StreamSource> Read(CaptureFacts f)
    {
        var list = new List<StreamSource>();
        if (f.Windows)
        {
            list.Add(new("screen:desktop", StreamKind.Bildschirm, "Bildschirm", "desktop", null));
            list.AddRange(ParseDirectShow(f.DirectShow ?? ""));
            foreach (var w in ParseWindows(f.Wmctrl ?? "", windows: true)) list.Add(w);
        }
        else
        {
            foreach (var line in ParseXrandr(f.Xrandr ?? "", string.IsNullOrWhiteSpace(f.Display) ? ":0.0" : f.Display!)) list.Add(line);
            foreach (var v in f.Videos)
            {
                var name = string.IsNullOrWhiteSpace(v.Name) ? v.Path : v.Name.Trim();
                list.Add(new("cam:" + v.Path, StreamKind.Kamera, name, v.Path, null));
            }
            list.AddRange(ParsePulse(f.Pulse ?? ""));
            foreach (var w in ParseWindows(f.Wmctrl ?? "", windows: false))
            {
                if (!f.WindowInfo.TryGetValue(w.Device, out var info)) continue;
                var grab = ParseXwininfo(info, string.IsNullOrWhiteSpace(f.Display) ? ":0.0" : f.Display!);
                if (grab is null) continue;
                list.Add(w with { Device = grab.Value.Device, Size = grab.Value.Size });
            }
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return list.Where(s => seen.Add(s.Id)).ToList();
    }

    public static IReadOnlyList<StreamSource> ParseDirectShow(string text)
    {
        var list = new List<StreamSource>();
        var mode = "";
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Contains("video devices", StringComparison.OrdinalIgnoreCase)) { mode = "video"; continue; }
            if (line.Contains("audio devices", StringComparison.OrdinalIgnoreCase)) { mode = "audio"; continue; }
            if (line.Contains("Alternative name", StringComparison.OrdinalIgnoreCase)) continue;
            var q1 = line.IndexOf('"');
            var q2 = q1 >= 0 ? line.IndexOf('"', q1 + 1) : -1;
            if (q1 < 0 || q2 <= q1) continue;
            var name = line[(q1 + 1)..q2].Trim();
            if (name.Length == 0) continue;
            var kind = line.Contains("(video)", StringComparison.OrdinalIgnoreCase) ? "video"
                : line.Contains("(audio)", StringComparison.OrdinalIgnoreCase) ? "audio" : mode;
            if (kind == "video") list.Add(new("cam:" + name, StreamKind.Kamera, name, name, null));
            else if (kind == "audio") list.Add(new("mic:" + name, StreamKind.Mikrofon, name, name, null));
        }
        return list;
    }

    public static IReadOnlyList<StreamSource> ParseXrandr(string text, string display)
    {
        var list = new List<StreamSource>();
        foreach (var raw in text.Split('\n'))
        {
            var m = Xr().Match(raw.Trim());
            if (!m.Success) continue;
            var w = m.Groups[2].Value;
            var h = m.Groups[3].Value;
            var x = m.Groups[4].Value;
            var y = m.Groups[5].Value;
            var dev = x == "0" && y == "0" ? display : $"{display}+{x},{y}";
            list.Add(new("screen:" + m.Groups[1].Value, StreamKind.Bildschirm, "Bildschirm " + m.Groups[1].Value, dev, $"{w}x{h}"));
        }
        return list;
    }

    public static IReadOnlyList<StreamSource> ParsePulse(string text)
    {
        var list = new List<StreamSource>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("Name", StringComparison.OrdinalIgnoreCase)) continue;
            var p = line.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (p.Length < 2 || !int.TryParse(p[0], out _)) continue;
            var name = p[1];
            var monitor = name.Contains(".monitor", StringComparison.OrdinalIgnoreCase);
            var label = monitor ? "Ton vom PC" : "Mikrofon";
            list.Add(new("mic:" + name, StreamKind.Mikrofon, label, name, null));
        }
        return list;
    }

    /// <summary>wmctrl -l: „0xId  Desktop  Host  Titel“. Unter Windows ist Device der Titel, unter Linux die Fenster-Id.</summary>
    public static IReadOnlyList<StreamSource> ParseWindows(string text, bool windows)
    {
        var list = new List<StreamSource>();
        foreach (var raw in text.Split('\n'))
        {
            var m = Wm().Match(raw.Trim());
            if (!m.Success) continue;
            var title = m.Groups[2].Value.Trim();
            if (title.Length == 0) continue;
            var dev = windows ? title : m.Groups[1].Value;
            list.Add(new("win:" + dev, StreamKind.Fenster, title, dev, null));
        }
        return list;
    }

    public static (string Device, string Size)? ParseXwininfo(string text, string display)
    {
        int? w = null, h = null, x = null, y = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("Width:", StringComparison.Ordinal)) w = Num(line);
            else if (line.StartsWith("Height:", StringComparison.Ordinal)) h = Num(line);
            else if (line.StartsWith("Absolute upper-left X:", StringComparison.Ordinal)) x = Num(line);
            else if (line.StartsWith("Absolute upper-left Y:", StringComparison.Ordinal)) y = Num(line);
        }
        if (w is null or < 2 || h is null or < 2 || x is null || y is null) return null;
        var dev = x == 0 && y == 0 ? display : $"{display}+{x},{y}";
        return (dev, $"{w}x{h}");
    }

    public static StreamSource Window(string title)
    {
        var t = title.Trim();
        if (t.Length < 2) throw new ArgumentException("Bitte den Fenstertitel eintragen, so wie er in der Titelleiste steht.");
        return new("win:" + t, StreamKind.Fenster, t, t, null);
    }

    public static string PublishUrl(string server, string key)
    {
        var s = server.Trim();
        if (!s.StartsWith("rtmp://", StringComparison.OrdinalIgnoreCase) && !s.StartsWith("rtmps://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Der Server muss mit rtmp:// oder rtmps:// beginnen.");
        var k = key.Trim();
        if (k.Length < 4) throw new ArgumentException("Bitte den Stream-Schlüssel eintragen. Den bekommst du bei der Plattform.");
        if (k.Any(c => char.IsWhiteSpace(c) || c is '"' or '\'' or '\\' or '&'))
            throw new ArgumentException("Der Stream-Schlüssel enthält Zeichen, die hier nicht erlaubt sind.");
        return s.TrimEnd('/') + "/" + k;
    }

    public static string Target(string via, string platform, string server, IEnumerable<string> sourceIds, string key)
    {
        var ids = string.Join(",", sourceIds.OrderBy(x => x, StringComparer.Ordinal));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key.Trim())))[..16];
        return $"{via}|{platform.Trim()}|{server.Trim()}|{ids}|{hash}";
    }

    public static ApprovalRequest Ask(ApprovalGate gate, Principal who, string via, string platform, string server, IReadOnlyList<string> sourceIds, string key)
    {
        if (via is not ("ffmpeg" or "obs")) throw new ArgumentException("Die Sendung läuft über FFmpeg oder OBS.");
        if (sourceIds.Count == 0) throw new InvalidOperationException("Bitte zuerst eine Quelle wählen.");
        _ = PublishUrl(server, key);
        var tool = via == "obs" ? "OBS" : "FFmpeg";
        return gate.Request(who, Action, $"Live-Sendung auf {platform.Trim()} über {tool} starten. Der Schlüssel steht nicht im Protokoll.", Target(via, platform, server, sourceIds, key), TimeSpan.FromMinutes(10));
    }

    /// <summary>Startet die übergebene Aktion nur innerhalb des Freigabe-Gates. Ohne Freigabe passiert nichts.</summary>
    public static void Start(ApprovalGate gate, string approvalId, string via, string platform, string server, IReadOnlyList<string> sourceIds, string key, Action start) =>
        gate.Execute(approvalId, Action, Target(via, platform, server, sourceIds, key), () => { start(); return true; });

    public static string OutputExt(IReadOnlyList<StreamSource> sources, LiveOut kind) => kind switch
    {
        LiveOut.Vorschau when sources.Any(s => s.Kind != StreamKind.Mikrofon) => ".png",
        LiveOut.Vorschau => ".wav",
        LiveOut.Aufnahme when sources.All(s => s.Kind == StreamKind.Mikrofon) => ".m4a",
        LiveOut.Aufnahme => ".mp4",
        _ => ".flv",
    };

    public static string[] Args(IReadOnlyList<StreamSource> sources, LiveOut kind, string output, bool windows, string? publishUrl = null)
    {
        if (sources.Count == 0) throw new InvalidOperationException("Bitte zuerst eine Quelle wählen.");
        if (kind == LiveOut.Sendung)
        {
            if (string.IsNullOrWhiteSpace(publishUrl)) throw new ArgumentException("Zum Senden fehlt die Adresse.");
        }
        else if (publishUrl is not null) throw new ArgumentException("Vorschau und Aufnahme gehen nicht ins Netz.");
        var videos = sources.Where(s => s.Kind != StreamKind.Mikrofon).ToList();
        var audio = sources.FirstOrDefault(s => s.Kind == StreamKind.Mikrofon);
        if (videos.Count > 4) throw new InvalidOperationException("Höchstens vier Bildquellen gleichzeitig.");
        var a = new List<string> { "-hide_banner", "-loglevel", "error", "-y" };
        foreach (var s in videos) AddInput(a, s, windows);
        if (audio is not null) AddInput(a, audio, windows);
        if (videos.Count >= 2)
        {
            var bits = new List<string>();
            for (var i = 0; i < videos.Count; i++) bits.Add($"[{i}:v]scale=-2:720,setsar=1[v{i}]");
            var stack = string.Concat(Enumerable.Range(0, videos.Count).Select(i => $"[v{i}]"));
            bits.Add($"{stack}hstack=inputs={videos.Count}[vout]");
            a.AddRange(["-filter_complex", string.Join(';', bits), "-map", "[vout]"]);
            if (audio is not null) a.AddRange(["-map", videos.Count + ":a"]);
        }
        else if (videos.Count == 1 && audio is not null) a.AddRange(["-map", "0:v:0", "-map", "1:a:0"]);
        if (videos.Count > 0 && kind != LiveOut.Vorschau)
            a.AddRange(["-c:v", "libx264", "-preset", "veryfast", "-pix_fmt", "yuv420p"]);
        if (audio is not null && kind != LiveOut.Vorschau)
            a.AddRange(["-c:a", "aac", "-b:a", "160k"]);
        switch (kind)
        {
            case LiveOut.Vorschau when videos.Count > 0:
                a.AddRange(["-frames:v", "1", output]);
                break;
            case LiveOut.Vorschau:
                a.AddRange(["-t", "1", "-c:a", "pcm_s16le", output]);
                break;
            case LiveOut.Aufnahme:
                if (output.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)) a.AddRange(["-movflags", "+faststart"]);
                a.Add(output);
                break;
            default:
                a.AddRange(["-f", "flv", publishUrl!]);
                break;
        }
        return a.ToArray();
    }

    private static void AddInput(List<string> a, StreamSource s, bool windows)
    {
        switch (s.Kind)
        {
            case StreamKind.Bildschirm when windows:
                a.AddRange(["-f", "gdigrab", "-framerate", "30", "-i", s.Device]);
                break;
            case StreamKind.Bildschirm:
                a.AddRange(["-f", "x11grab", "-framerate", "30", "-video_size", s.Size ?? throw new InvalidOperationException($"Für „{s.Label}“ fehlt die gemessene Größe."), "-i", s.Device]);
                break;
            case StreamKind.Fenster when windows:
                a.AddRange(["-f", "gdigrab", "-framerate", "30", "-i", "title=" + s.Device]);
                break;
            case StreamKind.Fenster:
                if (string.IsNullOrWhiteSpace(s.Size)) throw new InvalidOperationException($"Für das Fenster „{s.Label}“ fehlt die Größe. Wähle den Bildschirm oder such die Quellen neu.");
                a.AddRange(["-f", "x11grab", "-framerate", "30", "-video_size", s.Size, "-i", s.Device]);
                break;
            case StreamKind.Kamera when windows:
                a.AddRange(["-f", "dshow", "-framerate", "30", "-i", "video=" + s.Device]);
                break;
            case StreamKind.Kamera:
                a.AddRange(["-f", "v4l2", "-framerate", "30", "-i", s.Device]);
                break;
            case StreamKind.Mikrofon when windows:
                a.AddRange(["-f", "dshow", "-i", "audio=" + s.Device]);
                break;
            default:
                a.AddRange(["-f", "pulse", "-i", s.Device]);
                break;
        }
    }

    public static string? NewestService(string configRoot)
    {
        var dir = Path.Combine(configRoot, "basic", "profiles");
        if (!Directory.Exists(dir)) return null;
        return Directory.EnumerateFiles(dir, "service.json", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }

    /// <summary>Schreibt Server und Schlüssel in das OBS-Profil. Andere Einstellungen bleiben.</summary>
    public static void WriteService(string serviceJson, string server, string key)
    {
        _ = PublishUrl(server, key);
        JsonNode root;
        try { root = File.Exists(serviceJson) ? JsonNode.Parse(File.ReadAllText(serviceJson)) ?? new JsonObject() : new JsonObject(); }
        catch (System.Text.Json.JsonException) { throw new InvalidOperationException("Die OBS-Dienstdatei ist kein gültiges JSON. Es wurde nichts geändert."); }
        if (root is not JsonObject obj) throw new InvalidOperationException("Die OBS-Dienstdatei hat ein unbekanntes Format. Es wurde nichts geändert.");
        obj["type"] = "rtmp_custom";
        var settings = obj["settings"] as JsonObject ?? new JsonObject();
        settings["server"] = server.Trim();
        settings["key"] = key.Trim();
        obj["settings"] = settings;
        File.WriteAllText(serviceJson, obj.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private static int Num(string line)
    {
        var cut = line[(line.LastIndexOf(':') + 1)..].Trim();
        return int.Parse(cut, System.Globalization.CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"^(\S+)\s+connected(?:\s+primary)?\s+(\d+)x(\d+)\+(-?\d+)\+(-?\d+)")]
    private static partial Regex Xr();

    [GeneratedRegex(@"^(0x[0-9a-fA-F]+)\s+\S+\s+\S+\s+(.+)$")]
    private static partial Regex Wm();
}
