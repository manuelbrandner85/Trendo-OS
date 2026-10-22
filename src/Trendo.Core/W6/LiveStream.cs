using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Trendo.Core;

public enum VideoKind { Screen, Window, Camera }
public enum LivePlatform { YouTube, Twitch, Custom }
public enum LiveMode { Idle, Preview, Recording, Broadcasting }
public enum LiveOutput { Preview, Record, Broadcast }
public enum CaptureOs { Windows, Linux, Mac }

/// <summary>Ein Fenster, das der Nutzer für die Aufnahme auswählt. ByTitle: Windows greift über den Fenstertitel, sonst über Position und Größe.</summary>
public sealed record WindowGrab(string Title, int X, int Y, int Width, int Height, bool ByTitle)
{
    public override string ToString() => ByTitle ? Title : $"{Title} ({Width}×{Height})";
}

public sealed record DeviceItem(string Id, string Label)
{
    public override string ToString() => Label;
}

public sealed record CaptureDevices(IReadOnlyList<DeviceItem> Cameras, IReadOnlyList<DeviceItem> Microphones);

/// <summary>Was ffmpeg aufnehmen soll. Das Betriebssystem steht im Wert, damit die Befehle unabhängig vom Testrechner festliegen.</summary>
public sealed record CaptureChoice
{
    public required CaptureOs Os { get; init; }
    public required VideoKind Video { get; init; }
    public string Device { get; init; } = "";
    public bool ByTitle { get; init; }
    public int WinX { get; init; }
    public int WinY { get; init; }
    public int WinW { get; init; }
    public int WinH { get; init; }
    public bool Microphone { get; init; }
    public string? Mic { get; init; }
    public int Fps { get; init; } = 30;
    public int Width { get; init; } = 1280;
    public int Height { get; init; } = 720;
    public int VideoKbps { get; init; } = 2500;
    public int AudioKbps { get; init; } = 160;
    public string Display { get; init; } = ":0";
}

public sealed record CaptureRates(int W, int H, int Fps, int VideoKbps, int AudioKbps);

/// <summary>Stream-Schlüssel liegen nur im Secret-Store. Sie kommen nie ins Audit und nie in eine Datei des Studios.</summary>
public static class StreamKeys
{
    public static string Slot(LivePlatform p) => "stream-key:" + p switch
    {
        LivePlatform.YouTube => "youtube",
        LivePlatform.Twitch => "twitch",
        LivePlatform.Custom => "custom",
        _ => throw new ArgumentOutOfRangeException(nameof(p))
    };

    public static void Save(ISecretStore store, LivePlatform p, string key)
    {
        var k = (key ?? "").Trim();
        if (k.Length is < 4 or > 300) throw new ArgumentException("Der Stream-Schlüssel muss zwischen 4 und 300 Zeichen lang sein.");
        if (k.Any(c => char.IsWhiteSpace(c) || c is '/' or '?' or '#' or '\\' or '"' or '\''))
            throw new ArgumentException("Der Stream-Schlüssel enthält Zeichen, die nicht hinein dürfen.");
        store.Set(Slot(p), k);
    }

    public static string Require(ISecretStore store, LivePlatform p) =>
        store.Get(Slot(p)) is { Length: > 0 } k ? k : throw new InvalidOperationException("Bitte zuerst den Stream-Schlüssel speichern.");

    public static bool Saved(ISecretStore store, LivePlatform p) => !string.IsNullOrEmpty(store.Get(Slot(p)));
}

/// <summary>Offizielle RTMP-Adressen. Der Schlüssel wird nur an die Adresse gehängt und nirgends protokolliert.</summary>
public static class Ingest
{
    public const string YouTube = "rtmp://a.rtmp.youtube.com/live2";
    public const string Twitch = "rtmp://live.twitch.tv/app";

    public static string Target(LivePlatform platform, string? customBase) => platform switch
    {
        LivePlatform.YouTube => "youtube",
        LivePlatform.Twitch => "twitch",
        LivePlatform.Custom => "rtmp:" + ParseBase(customBase).Host,
        _ => throw new ArgumentOutOfRangeException(nameof(platform))
    };

    public static Uri ParseBase(string? url)
    {
        var t = (url ?? "").Trim();
        if (!Uri.TryCreate(t, UriKind.Absolute, out var u) || u.Scheme is not ("rtmp" or "rtmps"))
            throw new ArgumentException("Die Server-Adresse muss mit rtmp:// oder rtmps:// beginnen.");
        if (string.IsNullOrEmpty(u.Host)) throw new ArgumentException("Der Server-Adresse fehlt der Rechnername.");
        if (!string.IsNullOrEmpty(u.UserInfo)) throw new ArgumentException("Die Server-Adresse darf keinen Schlüssel enthalten. Den Schlüssel trägst du extra ein.");
        return u;
    }

    public static string Url(LivePlatform platform, string? customBase, string key)
    {
        if (key.Any(c => char.IsWhiteSpace(c) || c is '/' or '?' or '#' or '\\'))
            throw new ArgumentException("Der Stream-Schlüssel enthält Zeichen, die nicht hinein dürfen.");
        var basis = platform switch
        {
            LivePlatform.YouTube => YouTube,
            LivePlatform.Twitch => Twitch,
            LivePlatform.Custom => ParseBase(customBase).GetLeftPart(UriPartial.Path).TrimEnd('/'),
            _ => throw new ArgumentOutOfRangeException(nameof(platform))
        };
        return basis + "/" + key;
    }

    public static string Redact(string text, string? secret)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(secret) || secret.Length < 4) return text;
        return text.Replace(secret, "••••", StringComparison.Ordinal);
    }
}

public static class LiveFfmpeg
{
    public static IReadOnlyList<string> Qualities(LivePlatform p) => p == LivePlatform.Twitch
        ? ["720p30", "720p60", "1080p30", "1080p60"]
        : ["720p30", "720p60", "1080p30", "1080p60", "1440p30", "2160p30"];

    public static CaptureRates Rates(LivePlatform platform, string quality)
    {
        var name = platform == LivePlatform.Twitch ? "Twitch" : "YouTube";
        var plan = BroadcastDesk.PlanStream(name, quality, 10000);
        var q = quality.Trim().ToLowerInvariant().Replace(" ", "");
        var fps = q.Contains("60", StringComparison.Ordinal) ? 60 : 30;
        var (w, h) = q switch
        {
            _ when q.StartsWith("2160", StringComparison.Ordinal) => (3840, 2160),
            _ when q.StartsWith("1440", StringComparison.Ordinal) => (2560, 1440),
            _ when q.StartsWith("1080", StringComparison.Ordinal) => (1920, 1080),
            _ when q.StartsWith("720", StringComparison.Ordinal) => (1280, 720),
            _ => throw new ArgumentException("Qualität bitte wie „720p30“ oder „1080p60“.")
        };
        return new(w, h, fps, plan.VideoKbps, plan.AudioKbps);
    }

    public static List<string> Args(LiveOutput output, CaptureChoice c, string previewPath, string? mediaPath, string? rtmpUrl)
    {
        if (string.IsNullOrWhiteSpace(previewPath)) throw new ArgumentException("Die Vorschau braucht einen Dateipfad.");
        if (c.Fps is < 1 or > 60) throw new ArgumentException("Die Bildrate muss zwischen 1 und 60 liegen.");
        if (c.Width < 16 || c.Height < 16) throw new ArgumentException("Die Auflösung ist zu klein.");
        if (c.VideoKbps is < 100 or > 80000) throw new ArgumentException("Die Bitrate liegt außerhalb des sinnvollen Bereichs.");
        var a = new List<string> { "-y", "-hide_banner" };
        AddVideo(a, c);
        int? audio = null;
        if (c.Microphone)
        {
            audio = 1;
            AddMic(a, c);
        }
        else if (output == LiveOutput.Broadcast)
        {
            audio = 1;
            a.AddRange(["-f", "lavfi", "-i", "anullsrc=channel_layout=stereo:sample_rate=44100"]);
        }
        if (output == LiveOutput.Preview)
        {
            a.AddRange(["-vf", "fps=2,scale=640:-2", "-an", "-q:v", "5", "-f", "image2", "-update", "1", previewPath]);
            return a;
        }
        var scale = c.Width.ToString(CultureInfo.InvariantCulture) + ":" + c.Height.ToString(CultureInfo.InvariantCulture);
        a.Add("-filter_complex");
        a.Add($"[0:v]scale={scale}:force_original_aspect_ratio=decrease,pad={scale}:(ow-iw)/2:(oh-ih)/2,split=2[vout][p];[p]fps=2,scale=640:-2[prev]");
        a.AddRange(["-map", "[prev]", "-an", "-q:v", "5", "-f", "image2", "-update", "1", previewPath]);
        void Encode(bool live)
        {
            a.AddRange(["-map", "[vout]"]);
            if (audio is int i) a.AddRange(["-map", i.ToString(CultureInfo.InvariantCulture) + ":a"]);
            a.AddRange(["-c:v", "libx264", "-preset", "veryfast"]);
            if (live) a.AddRange(["-tune", "zerolatency"]);
            a.AddRange(["-pix_fmt", "yuv420p", "-b:v", c.VideoKbps.ToString(CultureInfo.InvariantCulture) + "k", "-maxrate", c.VideoKbps.ToString(CultureInfo.InvariantCulture) + "k", "-bufsize", (c.VideoKbps * 2).ToString(CultureInfo.InvariantCulture) + "k", "-g", (c.Fps * 2).ToString(CultureInfo.InvariantCulture)]);
            if (audio is not null) a.AddRange(["-c:a", "aac", "-b:a", c.AudioKbps.ToString(CultureInfo.InvariantCulture) + "k", "-ar", "44100"]);
        }
        if (output == LiveOutput.Broadcast)
        {
            if (string.IsNullOrWhiteSpace(rtmpUrl) || !rtmpUrl.StartsWith("rtmp", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Dem Stream fehlt die Zieladresse.");
            Encode(true);
            a.AddRange(["-f", "flv", rtmpUrl]);
            if (!string.IsNullOrWhiteSpace(mediaPath)) { Encode(false); a.AddRange(["-f", "mp4", "-movflags", "frag_keyframe+empty_moov", mediaPath]); }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(mediaPath)) throw new ArgumentException("Die Aufnahme braucht einen Dateipfad.");
            Encode(false);
            a.AddRange(["-f", "mp4", "-movflags", "frag_keyframe+empty_moov", mediaPath]);
        }
        return a;
    }

    private static void Queue(List<string> a, CaptureOs os) { if (os == CaptureOs.Windows) a.AddRange(["-thread_queue_size", "1024"]); }

    private static string ScreenId(string display)
    {
        var d = string.IsNullOrWhiteSpace(display) ? ":0" : display.Trim();
        return d.Contains('.', StringComparison.Ordinal) ? d : d + ".0";
    }

    private static string Clean(string s, string what)
    {
        var t = s.Trim();
        if (t.Length is 0 or > 300 || t.Any(c => c is '\r' or '\n' or '\0')) throw new ArgumentException(what + " ist ungültig.");
        return t;
    }

    private static void AddVideo(List<string> a, CaptureChoice c)
    {
        var fps = c.Fps.ToString(CultureInfo.InvariantCulture);
        switch (c.Video)
        {
            case VideoKind.Screen:
                Queue(a, c.Os);
                if (c.Os == CaptureOs.Windows) a.AddRange(["-f", "gdigrab", "-framerate", fps, "-i", "desktop"]);
                else if (c.Os == CaptureOs.Mac) a.AddRange(["-f", "avfoundation", "-framerate", fps, "-i", "Capture screen 0:none"]);
                else a.AddRange(["-f", "x11grab", "-framerate", fps, "-i", ScreenId(c.Display)]);
                break;
            case VideoKind.Window:
                if (c.Os == CaptureOs.Mac) throw new ArgumentException("Ein einzelnes Fenster aufnehmen geht auf diesem System nicht. Nimm den Bildschirm oder die Kamera.");
                Queue(a, c.Os);
                if (c.Os == CaptureOs.Windows)
                    a.AddRange(["-f", "gdigrab", "-framerate", fps, "-i", "title=" + Clean(c.Device, "Der Fenstertitel")]);
                else if (c.WinW < 16 || c.WinH < 16)
                    throw new ArgumentException("Bitte das Fenster erneut suchen. Ohne Position kann es nicht ins Bild.");
                else
                    a.AddRange(["-f", "x11grab", "-framerate", fps, "-video_size", c.WinW.ToString(CultureInfo.InvariantCulture) + "x" + c.WinH.ToString(CultureInfo.InvariantCulture), "-i", ScreenId(c.Display) + "+" + c.WinX.ToString(CultureInfo.InvariantCulture) + "," + c.WinY.ToString(CultureInfo.InvariantCulture)]);
                break;
            case VideoKind.Camera:
                Queue(a, c.Os);
                var dev = Clean(c.Device, "Die Kamera");
                if (c.Os == CaptureOs.Windows) a.AddRange(["-f", "dshow", "-framerate", fps, "-i", "video=" + dev]);
                else if (c.Os == CaptureOs.Mac) a.AddRange(["-f", "avfoundation", "-framerate", fps, "-i", dev + ":none"]);
                else a.AddRange(["-f", "v4l2", "-framerate", fps, "-i", dev]);
                break;
            default: throw new ArgumentOutOfRangeException();
        }
    }

    private static void AddMic(List<string> a, CaptureChoice c)
    {
        var mic = string.IsNullOrWhiteSpace(c.Mic) ? c.Os == CaptureOs.Linux ? "default" : throw new ArgumentException("Bitte ein Mikrofon auswählen.") : Clean(c.Mic, "Das Mikrofon");
        Queue(a, c.Os);
        if (c.Os == CaptureOs.Windows) a.AddRange(["-f", "dshow", "-i", "audio=" + mic]);
        else if (c.Os == CaptureOs.Mac) a.AddRange(["-f", "avfoundation", "-i", ":" + mic]);
        else a.AddRange(["-f", "pulse", "-i", mic]);
    }
}

/// <summary>Liest die Gerätelisten, die ffmpeg und die üblichen Linux-Werkzeuge ausgeben.</summary>
public static class LiveDevices
{
    public static (List<DeviceItem> Cameras, List<DeviceItem> Mics) ParseDshow(string text)
    {
        var cams = new List<DeviceItem>(); var mics = new List<DeviceItem>();
        string? mode = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Contains("video devices", StringComparison.OrdinalIgnoreCase)) { mode = "video"; continue; }
            if (line.Contains("audio devices", StringComparison.OrdinalIgnoreCase)) { mode = "audio"; continue; }
            if (line.Contains("Alternative", StringComparison.OrdinalIgnoreCase)) continue;
            var q1 = line.IndexOf('"'); var q2 = q1 < 0 ? -1 : line.IndexOf('"', q1 + 1);
            if (q1 < 0 || q2 < 0) continue;
            var name = line[(q1 + 1)..q2].Trim();
            if (name.Length == 0) continue;
            var kind = line.Contains("(video)", StringComparison.OrdinalIgnoreCase) ? "video" : line.Contains("(audio)", StringComparison.OrdinalIgnoreCase) ? "audio" : mode;
            if (kind == "video") cams.Add(new(name, name));
            else if (kind == "audio") mics.Add(new(name, name));
        }
        return (cams, mics);
    }

    public static List<DeviceItem> ParsePulse(string text)
    {
        var list = new List<DeviceItem>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.Contains('[', StringComparison.Ordinal) || !line.EndsWith(']')) continue;
            var id = line[..line.IndexOf('[')].Trim();
            var label = line[(line.IndexOf('[') + 1)..^1].Trim();
            if (id.Length == 0 || id.Contains("monitor", StringComparison.OrdinalIgnoreCase) || label.Contains("monitor", StringComparison.OrdinalIgnoreCase)) continue;
            if (id.Contains(' ')) continue;
            list.Add(new(id, label.Length == 0 ? id : label));
        }
        return list;
    }

    public static List<DeviceItem> ParsePactl(string text)
    {
        var list = new List<DeviceItem>();
        foreach (var raw in text.Split('\n'))
        {
            var p = raw.Split('\t');
            if (p.Length < 2) continue;
            var id = p[1].Trim();
            if (id.Length == 0 || !id.Contains("input", StringComparison.OrdinalIgnoreCase) || id.Contains("monitor", StringComparison.OrdinalIgnoreCase)) continue;
            list.Add(new(id, id));
        }
        return list;
    }

    public static (List<DeviceItem> Cameras, List<DeviceItem> Mics) ParseAvfoundation(string text)
    {
        var cams = new List<DeviceItem>(); var mics = new List<DeviceItem>();
        string? mode = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Contains("video devices", StringComparison.OrdinalIgnoreCase)) { mode = "video"; continue; }
            if (line.Contains("audio devices", StringComparison.OrdinalIgnoreCase)) { mode = "audio"; continue; }
            var b1 = line.LastIndexOf('['); var b2 = line.LastIndexOf(']');
            if (b1 < 0 || b2 < b1 || mode is null) continue;
            var id = line[(b1 + 1)..b2].Trim();
            var label = line[(b2 + 1)..].Trim().Trim('"');
            if (!int.TryParse(id, out _)) continue;
            var item = new DeviceItem(id, label.Length == 0 ? id : id + " – " + label);
            if (mode == "video") cams.Add(item); else mics.Add(item);
        }
        return (cams, mics);
    }

    public static List<WindowGrab> ParseWmctrl(string text)
    {
        var list = new List<WindowGrab>();
        foreach (var raw in text.Split('\n'))
        {
            var p = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (p.Length < 8 || !p[0].StartsWith("0x", StringComparison.OrdinalIgnoreCase)) continue;
            if (!int.TryParse(p[2], out var x) || !int.TryParse(p[3], out var y) || !int.TryParse(p[4], out var w) || !int.TryParse(p[5], out var h)) continue;
            if (w < 20 || h < 20) continue;
            var title = string.Join(' ', p.Skip(7)).Trim();
            if (title.Length == 0) continue;
            list.Add(new(title, x, y, w, h, false));
        }
        return list;
    }

    public static List<WindowGrab> ParseXwininfo(string text)
    {
        var list = new List<WindowGrab>();
        foreach (var raw in text.Split('\n'))
        {
            var q1 = raw.IndexOf('"'); var q2 = q1 < 0 ? -1 : raw.IndexOf('"', q1 + 1);
            if (q1 < 0 || q2 < 0) continue;
            var title = raw[(q1 + 1)..q2].Trim();
            var rest = raw[(q2 + 1)..];
            var at = rest.IndexOf(')');
            var geom = at < 0 ? rest : rest[(at + 1)..];
            var parts = geom.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var token = parts.FirstOrDefault(s => s.Contains('x') && s.Contains('+'));
            if (token is null || title.Length == 0) continue;
            var xp = token.Split('x', '+');
            if (xp.Length < 4 || !int.TryParse(xp[0], out var w) || !int.TryParse(xp[1], out var h) || !int.TryParse(xp[2], out var x) || !int.TryParse(xp[3], out var y)) continue;
            if (w < 20 || h < 20) continue;
            list.Add(new(title, x, y, w, h, false));
        }
        return list;
    }
}

/// <summary>Sucht ffmpeg über die bestehende Werkzeugprüfung und danach über den Programmpfad. Installiert nichts.</summary>
public static class FfmpegReady
{
    public static async Task<ToolCheck> Resolve(DependencyManager deps, Func<string, string?>? find = null, CancellationToken ct = default)
    {
        var comp = DependencyCatalog.Get("ffmpeg");
        var check = await deps.CheckAsync(comp, ct);
        if (check.Ok && !string.IsNullOrEmpty(check.Path)) return check;
        var locate = find ?? Cli.Find;
        var found = locate("ffmpeg") ?? locate("ffmpeg.exe");
        if (found is null)
            return check.State == ToolState.Broken ? check : new ToolCheck(comp, ToolState.Missing, null, null, "nicht gefunden");
        try
        {
            var (exit, output) = await deps.Runner.RunAsync(found, "-version", null, ct);
            if (exit != 0) return new ToolCheck(comp, ToolState.Broken, found, null, $"Start fehlgeschlagen (Exit {exit})");
            var first = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
            deps.Integrate("ffmpeg", found);
            return new ToolCheck(comp, ToolState.Working, found, first.Length > 80 ? first[..80] : first, "funktioniert");
        }
        catch (Exception ex) { return new ToolCheck(comp, ToolState.Broken, found, null, ex.GetType().Name); }
    }
}

public interface ICaptureProcess
{
    bool Running { get; }
    string Tail { get; }
    void Stop();
}

public interface ICaptureProcessFactory
{
    ICaptureProcess Start(string ffmpeg, IReadOnlyList<string> arguments);
}

/// <summary>Startet ffmpeg ohne Shell. Stopp schreibt „q“ und beendet den Prozess, falls er nicht von allein aufhört.</summary>
public sealed class FfmpegCaptureFactory : ICaptureProcessFactory
{
    public ICaptureProcess Start(string ffmpeg, IReadOnlyList<string> arguments)
    {
        var exe = File.Exists(ffmpeg) ? ffmpeg : Cli.Find(ffmpeg) ?? throw new FileNotFoundException("FFmpeg wurde nicht gefunden.", ffmpeg);
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path.GetTempPath() };
        foreach (var arg in arguments) psi.ArgumentList.Add(arg);
        Process proc;
        try { proc = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg hat nicht gestartet."); }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex) { throw new InvalidOperationException("FFmpeg hat nicht gestartet: " + ex.Message); }
        return new Capture(proc);
    }

    private sealed class Capture : ICaptureProcess
    {
        private readonly Process _proc;
        private readonly StringBuilder _err = new();
        public Capture(Process proc)
        {
            _proc = proc;
            proc.ErrorDataReceived += (_, e) => { if (e.Data is null) return; lock (_err) { _err.AppendLine(e.Data); if (_err.Length > 4000) _err.Remove(0, _err.Length - 2000); } };
            proc.OutputDataReceived += (_, _) => { };
            try { proc.BeginErrorReadLine(); proc.BeginOutputReadLine(); } catch { /* schon zu Ende */ }
        }
        public bool Running => !_proc.HasExited;
        public string Tail { get { lock (_err) { var s = _err.ToString(); return s.Length <= 600 ? s : s[^600..]; } } }
        public void Stop()
        {
            if (_proc.HasExited) return;
            try { _proc.StandardInput.WriteLine("q"); _proc.StandardInput.Flush(); } catch { /* Eingabe schon zu */ }
            if (!_proc.WaitForExit(3000)) { try { _proc.Kill(entireProcessTree: true); } catch { /* schon weg */ } }
        }
    }
}

/// <summary>Eine Aufnahme oder ein Stream. Der Livestream startet nur über <see cref="LiveBroadcast.Go"/>.</summary>
public sealed class LiveSession(ICaptureProcessFactory factory)
{
    private ICaptureProcess? _proc;
    private string? _secret;
    public LiveMode Mode { get; private set; } = LiveMode.Idle;
    public string Status { get; private set; } = "Bereit.";
    public string? PreviewFile { get; private set; }
    public string? OutputFile { get; private set; }
    public IReadOnlyList<string> LastArguments { get; private set; } = [];
    public bool Exited => _proc is { Running: false };

    public string StatusLine() => Ingest.Redact(Status, _secret);

    public void StartPreview(string ffmpeg, CaptureChoice choice, string previewPath) =>
        Run(LiveMode.Preview, ffmpeg, LiveFfmpeg.Args(LiveOutput.Preview, choice, previewPath, null, null), previewPath, null, null, "Vorschau läuft. Mit Stopp beenden.");

    public void StartRecording(string ffmpeg, CaptureChoice choice, string previewPath, string file) =>
        Run(LiveMode.Recording, ffmpeg, LiveFfmpeg.Args(LiveOutput.Record, choice, previewPath, file, null), previewPath, file, null, "Aufnahme läuft: " + Path.GetFileName(file));

    /// <summary>Nur aus <see cref="LiveBroadcast.Go"/>, und das nur innerhalb von ApprovalGate.Execute.</summary>
    internal void BeginBroadcast(string ffmpeg, CaptureChoice choice, string previewPath, string rtmpUrl, string? copyFile, string secret)
    {
        _secret = secret;
        try
        {
            Run(LiveMode.Broadcasting, ffmpeg, LiveFfmpeg.Args(LiveOutput.Broadcast, choice, previewPath, copyFile, rtmpUrl), previewPath, copyFile, secret, "Live. Mit Stopp beenden.");
        }
        catch { _secret = null; throw; }
    }

    private void Run(LiveMode mode, string ffmpeg, List<string> args, string preview, string? file, string? secret, string status)
    {
        if (Mode != LiveMode.Idle) throw new InvalidOperationException("Es läuft schon eine Aufnahme oder ein Stream. Bitte zuerst auf Stopp tippen.");
        var dir = Path.GetDirectoryName(preview);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        if (file is not null) Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        ICaptureProcess proc;
        try { proc = factory.Start(ffmpeg, args); }
        catch { Mode = LiveMode.Idle; throw; }
        _proc = proc;
        _secret = secret;
        Mode = mode;
        LastArguments = args;
        PreviewFile = preview;
        if (file is not null) OutputFile = file;
        Status = status;
    }

    public void NoteExit()
    {
        if (_proc is null || Mode == LiveMode.Idle || _proc.Running) return;
        var tail = Ingest.Redact(_proc.Tail, _secret).Trim();
        if (tail.Length > 400) tail = tail[^400..];
        _proc = null;
        _secret = null;
        Mode = LiveMode.Idle;
        Status = tail.Length == 0 ? "Beendet." : "FFmpeg hat aufgehört. " + tail;
    }

    public void Stop()
    {
        var p = _proc;
        if (p is null) { Status = "Es läuft nichts."; return; }
        try { p.Stop(); } catch { /* Stopp darf die Oberfläche nicht abbrechen */ }
        _proc = null;
        _secret = null;
        Mode = LiveMode.Idle;
        Status = "Gestoppt.";
    }
}

/// <summary>Livestream nur nach Freigabe. Ohne freigegebene, einmalige Anfrage startet nichts, und nichts startet von allein.</summary>
public static class LiveBroadcast
{
    public const string Action = "live.start";

    public static string Where(LivePlatform platform, string? customBase) => platform switch
    {
        LivePlatform.YouTube => "YouTube",
        LivePlatform.Twitch => "Twitch",
        LivePlatform.Custom => "den Server " + Ingest.ParseBase(customBase).Host,
        _ => "das Ziel"
    };

    public static ApprovalRequest Ask(ApprovalGate gate, Principal by, LivePlatform platform, string? customBase, string sourceLabel) =>
        gate.Request(by, Action, $"Livestream zu {Where(platform, customBase)} starten. Quelle: {sourceLabel}. Es startet nicht von allein.", Ingest.Target(platform, customBase));

    public static void Go(ApprovalGate gate, string approvalId, LiveSession session, LivePlatform platform, string? customBase, ISecretStore secrets, string ffmpeg, CaptureChoice choice, string previewPath, string? copyFile, bool offline)
    {
        if (offline) throw new OfflineException("Offline-Modus: Ein Livestream braucht eine Verbindung. Aufnehmen auf diesem PC geht weiter.");
        if (session.Mode != LiveMode.Idle) throw new InvalidOperationException("Es läuft schon eine Aufnahme oder ein Stream. Bitte zuerst auf Stopp tippen.");
        var key = StreamKeys.Require(secrets, platform);
        var url = Ingest.Url(platform, customBase, key);
        var target = Ingest.Target(platform, customBase);
        gate.Execute(approvalId, Action, target, () =>
        {
            session.BeginBroadcast(ffmpeg, choice, previewPath, url, copyFile, key);
            return true;
        });
    }
}
