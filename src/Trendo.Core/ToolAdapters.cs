using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Alle Studios, Runde 2 – SYS-10 „Texte & Tabellen“: Dokumente mit typst (Apache-2.0, typst 0.15.1) als PDF/PNG setzen.
/// typst läuft als eigener Prozess (Argumente als Liste), <c>--root</c> begrenzt den Dateizugriff auf den Projektordner.</summary>
public static class TypstDoc
{
    public static readonly string[] Outputs = [".pdf", ".png", ".svg"];

    public static string? Check(string input, string output, string root)
    {
        if (string.IsNullOrWhiteSpace(input) || !File.Exists(input)) return "Die typst-Datei gibt es nicht.";
        if (!input.EndsWith(".typ", StringComparison.OrdinalIgnoreCase)) return "Eingabe muss eine .typ-Datei sein.";
        if (!Outputs.Contains(Path.GetExtension(output).ToLowerInvariant())) return "Ausgabe muss PDF, PNG oder SVG sein.";
        var r = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(input).StartsWith(r, StringComparison.OrdinalIgnoreCase) || !Path.GetFullPath(output).StartsWith(r, StringComparison.OrdinalIgnoreCase))
            return "Eingabe und Ausgabe müssen im Projektordner liegen.";
        return null;
    }

    /// <summary>typst-Argumente: <c>compile --root &lt;projekt&gt; ein aus</c>; bei PNG mit Auflösung (bei mehreren Seiten Muster <c>{p}</c> nötig – Trendo setzt dann nur Seite 1).</summary>
    public static IReadOnlyList<string> Args(string input, string output, string root, int ppi = 144)
    {
        var a = new List<string> { "compile", "--root", root, input, output };
        if (output.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) a.AddRange(["--ppi", Math.Clamp(ppi, 36, 600).ToString(CultureInfo.InvariantCulture), "--pages", "1"]);
        return a;
    }

    public static (bool Ok, string Message) Compile(string exe, string input, string output, string root, int timeoutMs = 60000,
        Func<string, IReadOnlyList<string>, int, Cli.Result>? run = null)
    {
        if (Check(input, output, root) is { } why) return (false, why);
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe)) return (false, "typst ist noch nicht eingerichtet – mit einem Klick (kostenlos, Apache-2.0).");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        var r = (run ?? ((e, a, t) => Cli.Run(e, a, timeoutMs: t)))(exe, Args(input, output, root), timeoutMs);
        if (!r.Ok) return (false, "typst meldet: " + FirstError(r.Err.Length > 0 ? r.Err : r.Out));
        return File.Exists(output) && new FileInfo(output).Length > 0 ? (true, output) : (false, "typst hat keine Datei geschrieben.");
    }

    static string FirstError(string s) => s.Replace("\r", "").Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("error", StringComparison.OrdinalIgnoreCase)) ?? s.Trim().Split('\n')[0];

    /// <summary>Text sicher in typst-Markup setzen: Zeichen mit Sonderbedeutung werden mit Backslash geschützt (keine Befehlseinschleusung über <c>#</c>).</summary>
    public static string Escape(string s)
    {
        var sb = new StringBuilder(s.Length + 8);
        foreach (var ch in s.Replace("\r", ""))
        {
            if ("\\#*_`$<>@[]=-+/~\"'".Contains(ch)) sb.Append('\\');
            sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>Brief bzw. Rechnung als typst-Quelle: Titel, Anrede/Text (Absätze) und optionale Posten (Menge, Artikel, Einzelpreis) mit Summe.</summary>
    public static string Letter(string title, string body, IEnumerable<(decimal Qty, string Item, decimal Price)>? rows = null)
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        var sb = new StringBuilder("#set page(paper: \"a4\", margin: 2.5cm)\n#set text(lang: \"de\", size: 11pt)\n#set par(justify: true)\n\n");
        sb.Append("= ").Append(Escape(title.Trim())).Append("\n\n");
        foreach (var p in body.Replace("\r", "").Split("\n\n")) if (p.Trim().Length > 0) sb.Append(Escape(p.Trim()).Replace("\n", " \\\n")).Append("\n\n");
        var list = rows?.ToList() ?? [];
        if (list.Count > 0)
        {
            sb.Append("#table(columns: (auto, 1fr, auto, auto), align: (right, left, right, right),\n  [*Menge*], [*Artikel*], [*Einzelpreis*], [*Summe*],\n");
            foreach (var (q, item, price) in list)
                sb.Append($"  [{q.ToString("0.##", de)}], [{Escape(item)}], [{Escape(price.ToString("N2", de))} €], [{Escape((q * price).ToString("N2", de))} €],\n");
            sb.Append(")\n\n#align(right)[*Gesamt: ").Append(Escape(list.Sum(r => r.Qty * r.Price).ToString("N2", de))).Append(" €*]\n");
        }
        return sb.ToString();
    }
}

/// <summary>Alle Studios, Runde 2 – SPEC-15 „Live streamen“: eigener Sendeserver mit mediamtx (MIT, 1.21.3).
/// Standard nur auf diesem Rechner (127.0.0.1); im Heimnetz nur auf ausdrücklichen Wunsch. Kein API-/Metrik-Zugang, kein WebRTC.</summary>
public static class LiveServer
{
    static readonly Regex PathRx = new("^[a-z0-9][a-z0-9_-]{0,39}$", RegexOptions.Compiled);
    public static bool ValidPath(string p) => PathRx.IsMatch(p ?? "");
    public static bool ValidPort(int p) => p is >= 1024 and <= 65535;

    /// <summary>Konfiguration: RTSP (für OBS/ffmpeg) und HLS (zum Ansehen im Browser/Trendo-Player).</summary>
    public static string Config(int rtspPort = 8554, int hlsPort = 8888, bool lan = false)
    {
        if (!ValidPort(rtspPort) || !ValidPort(hlsPort) || rtspPort == hlsPort) throw new ArgumentException("Ports müssen zwischen 1024 und 65535 liegen und verschieden sein.");
        var host = lan ? "" : "127.0.0.1";
        return $"""
            logLevel: info
            api: no
            metrics: no
            pprof: no
            playback: no
            rtsp: yes
            rtspTransports: [tcp]
            rtspAddress: {host}:{rtspPort}
            rtmp: no
            hls: yes
            hlsAddress: {host}:{hlsPort}
            hlsAlwaysRemux: yes
            webrtc: no
            srt: no
            paths:
              all_others:

            """.Replace("\r", "");
    }

    public static string PublishUrl(string path, int rtspPort = 8554) => ValidPath(path) ? $"rtsp://127.0.0.1:{rtspPort}/{path}" : throw new ArgumentException("Sendungsname: nur a–z, 0–9, - und _ (bis 40 Zeichen).");
    public static string WatchUrl(string path, int hlsPort = 8888) => ValidPath(path) ? $"http://127.0.0.1:{hlsPort}/{path}/index.m3u8" : throw new ArgumentException("Sendungsname: nur a–z, 0–9, - und _ (bis 40 Zeichen).");

    /// <summary>Schreibt die Konfiguration in den Arbeitsordner und liefert die Startargumente (nur die Konfigurationsdatei).</summary>
    public static IReadOnlyList<string> Prepare(string workDir, int rtspPort = 8554, int hlsPort = 8888, bool lan = false)
    {
        Directory.CreateDirectory(workDir); var cfg = Path.Combine(workDir, "mediamtx.yml");
        File.WriteAllText(cfg, Config(rtspPort, hlsPort, lan)); return [cfg];
    }
}

/// <summary>Alle Studios, Runde 2 – STUDIO-TV-01 „TV & Live“: Livestreams der Öffentlich-Rechtlichen mit streamlink (BSD-2, 8.6.3) auflösen.
/// Nur freigegebene Seiten (ARD/Das Erste, arte – im echten Lauf am 08.11.2026 geprüft); kein Login, keine Cookies, kein DRM, keine Aufnahme.</summary>
public static class LiveResolve
{
    public static readonly (string Name, string Url)[] Channels =
    [
        ("Das Erste", "https://www.daserste.de/live/index.html"),
        ("arte", "https://www.arte.tv/de/live/"),
    ];
    static readonly string[] Hosts = ["daserste.de", "ardmediathek.de", "arte.tv"];

    public static bool Allowed(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == "https" && u.UserInfo.Length == 0 && u.IsDefaultPort &&
        Hosts.Any(h => u.Host.Equals(h, StringComparison.OrdinalIgnoreCase) || u.Host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<string> InfoArgs(string url) => ["--no-config", "--no-plugin-sideloading", "--json", url];
    public static IReadOnlyList<string> UrlArgs(string url, string quality) => ["--no-config", "--no-plugin-sideloading", "--stream-url", url, quality];
    static readonly Regex QualityRx = new("^[a-z0-9_]{1,20}$", RegexOptions.Compiled);

    /// <summary>JSON von <c>streamlink --json</c>: Titel und Qualitäten (best/worst ans Ende).</summary>
    public static (string Title, IReadOnlyList<string> Qualities, string? Error) Parse(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json); var r = d.RootElement;
            if (r.TryGetProperty("error", out var e)) return ("", [], e.GetString());
            var title = r.TryGetProperty("metadata", out var m) && m.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString()! : "";
            var q = r.TryGetProperty("streams", out var s) && s.ValueKind == JsonValueKind.Object ? s.EnumerateObject().Select(p => p.Name).ToList() : [];
            return (title, q.Where(x => x is not ("best" or "worst")).Concat(q.Where(x => x is "best" or "worst")).ToList(), q.Count == 0 ? "Keine Streams gefunden." : null);
        }
        catch (JsonException) { return ("", [], "Antwort von streamlink nicht lesbar."); }
    }

    public static (bool Ok, string Title, IReadOnlyList<string> Qualities, string Message) Info(string exe, string url, int timeoutMs = 45000, Func<string, IReadOnlyList<string>, int, Cli.Result>? run = null)
    {
        if (!Allowed(url)) return (false, "", [], "Nur freigegebene Sender (ARD/Das Erste, arte).");
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe)) return (false, "", [], "streamlink ist noch nicht eingerichtet.");
        var r = (run ?? ((e, a, t) => Cli.Run(e, a, timeoutMs: t)))(exe, InfoArgs(url), timeoutMs);
        var (title, q, err) = Parse(r.Out);
        return err is null ? (true, title, q, $"{title}: {q.Count} Qualitäten") : (false, "", [], err);
    }

    /// <summary>Direkte Stream-Adresse (HLS) für den Trendo-Player.</summary>
    public static (bool Ok, string Message) StreamUrl(string exe, string url, string quality = "best", int timeoutMs = 45000, Func<string, IReadOnlyList<string>, int, Cli.Result>? run = null)
    {
        if (!Allowed(url)) return (false, "Nur freigegebene Sender (ARD/Das Erste, arte).");
        if (!QualityRx.IsMatch(quality)) return (false, "Unbekannte Qualität.");
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe)) return (false, "streamlink ist noch nicht eingerichtet.");
        var r = (run ?? ((e, a, t) => Cli.Run(e, a, timeoutMs: t)))(exe, UrlArgs(url, quality), timeoutMs);
        var line = r.Out.Trim().Split('\n').LastOrDefault()?.Trim() ?? "";
        return r.Ok && Uri.TryCreate(line, UriKind.Absolute, out var u) && u.Scheme is "https" or "http" ? (true, line) : (false, "Stream nicht verfügbar: " + (r.Err.Length > 0 ? r.Err : r.Out).Trim().Split('\n')[0]);
    }
}
