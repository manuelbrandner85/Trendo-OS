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
        // „8. November“ oder „3. Mahnung“ am Zeilen- bzw. Zellenanfang wäre sonst eine nummerierte Liste
        return Regex.Replace(sb.ToString(), @"(?m)^(\s*\d+)\.", "$1\\.");
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

    /// <summary>Geschäftsbrief nach DIN 5008 (Alle Studios, Runde 6; Konzept typst-letter-pro, eigener Code, offline ohne typst-Pakete).</summary>
    public sealed record DinLetter(string Sender, string Recipient, string Subject, string Body, DateOnly Date,
        bool FormA = false, string YourRef = "", string OurRef = "", string Closing = "Mit freundlichen Grüßen", string Signer = "", string Footer = "");

    /// <summary>Maße DIN 5008 (Millimeter von oben): Anschriftfeld, Informationsblock, Betreff, Falzmarken; Lochmarke 148,5.</summary>
    public static (double Address, double Info, double Subject, double Fold1, double Fold2) DinGeometry(bool formA) =>
        formA ? (27, 32, 80.46, 87, 192) : (45, 50, 98.46, 105, 210);
    public const double DinLeft = 25, DinRight = 20, DinTop = 27, DinAddressLeft = 20, DinAddressWidth = 85, DinInfoLeft = 125, DinPunch = 148.5;

    /// <summary>Pflichtangaben prüfen – leere Liste = in Ordnung.</summary>
    public static IReadOnlyList<string> DinCheck(DinLetter l)
    {
        var e = new List<string>();
        static int Lines(string s) => s.Replace("\r", "").Split('\n').Count(x => x.Trim().Length > 0);
        if (Lines(l.Sender) == 0) e.Add("Absender fehlt.");
        if (Lines(l.Recipient) == 0) e.Add("Empfänger fehlt.");
        else if (Lines(l.Recipient) > 6) e.Add("Die Anschrift hat höchstens 6 Zeilen (DIN 5008).");
        if (l.Subject.Trim().Length == 0) e.Add("Betreff fehlt.");
        if (l.Body.Trim().Length == 0) e.Add("Brieftext fehlt.");
        return e;
    }

    static string Mm(double v) => v.ToString("0.##", CultureInfo.InvariantCulture) + "mm";
    static string Block(string s) => string.Join(" \\\n", s.Replace("\r", "").Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).Select(Escape));

    /// <summary>typst-Quelle für einen DIN-5008-Brief (Form A oder B): Briefkopf, Rücksendeangabe, Anschriftfeld 85 × 45 mm, Informationsblock,
    /// Betreff, Text, Gruß, Falz- und Lochmarken; Fußzeile und ab Seite 2 „Seite x von y“.</summary>
    public static string Din5008(DinLetter l)
    {
        if (DinCheck(l) is { Count: > 0 } errors) throw new ArgumentException(string.Join(" ", errors));
        var g = DinGeometry(l.FormA); var de = CultureInfo.GetCultureInfo("de-DE");
        var senderLines = l.Sender.Replace("\r", "").Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        var back = Escape(string.Join(" · ", senderLines));
        var sb = new StringBuilder();
        sb.Append("// DIN 5008 Form ").Append(l.FormA ? "A" : "B").Append(" – erzeugt von Trendo, frei bearbeitbar\n");
        sb.Append("#set page(paper: \"a4\", margin: (left: ").Append(Mm(DinLeft)).Append(", right: ").Append(Mm(DinRight)).Append(", top: ").Append(Mm(DinTop)).Append(", bottom: 28mm),\n");
        sb.Append("  background: context { if here().page() == 1 {\n");
        sb.Append("    place(top + left, dx: 4mm, dy: ").Append(Mm(g.Fold1)).Append(", line(length: 5mm, stroke: 0.3pt + luma(110)))\n");
        sb.Append("    place(top + left, dx: 4mm, dy: ").Append(Mm(DinPunch)).Append(", line(length: 8mm, stroke: 0.3pt + luma(110)))\n");
        sb.Append("    place(top + left, dx: 4mm, dy: ").Append(Mm(g.Fold2)).Append(", line(length: 5mm, stroke: 0.3pt + luma(110)))\n  } },\n");
        sb.Append("  footer: context { set text(size: 7.5pt, fill: luma(90)); let n = counter(page).final().first()\n");
        sb.Append("    grid(columns: (1fr, auto), [").Append(Block(l.Footer)).Append("], if n > 1 [Seite #counter(page).display() von #n] else []) })\n");
        sb.Append("#set text(lang: \"de\", region: \"DE\", size: 11pt, font: (\"Inter\", \"Arial\", \"Liberation Sans\", \"DejaVu Sans\", \"Libertinus Serif\"))\n");
        sb.Append("#set par(justify: false, leading: 0.62em, spacing: 1.2em)\n\n");
        // Briefkopf rechts oben
        sb.Append("#place(top + right, dy: ").Append(Mm((l.FormA ? 8 : 14) - DinTop)).Append(", align(right, text(size: 9pt)[*").Append(Escape(senderLines[0])).Append("*");
        if (senderLines.Count > 1) sb.Append(" \\\n").Append(string.Join(" \\\n", senderLines.Skip(1).Select(Escape)));
        sb.Append("]))\n");
        // Anschriftfeld 85 × 45 mm: Rücksendeangabe in der Zusatz- und Vermerkzone (17,7 mm), darunter die Anschriftzone (27,3 mm)
        sb.Append("#place(top + left, dx: ").Append(Mm(DinAddressLeft - DinLeft)).Append(", dy: ").Append(Mm(g.Address - DinTop)).Append(", block(width: ").Append(Mm(DinAddressWidth)).Append(", height: 45mm)[\n");
        sb.Append("  #place(top + left, dx: 5mm, dy: 12.2mm, text(size: 7pt, fill: luma(70))[").Append(back).Append("])\n");
        sb.Append("  #place(top + left, dx: 5mm, dy: 17.7mm, [#block(width: 75mm, height: 27.3mm, text(size: 10pt)[").Append(Block(l.Recipient)).Append("]) <din-anschrift>])\n])\n");
        // Informationsblock
        sb.Append("#place(top + left, dx: ").Append(Mm(DinInfoLeft - DinLeft)).Append(", dy: ").Append(Mm(g.Info - DinTop)).Append(", [#block(width: 75mm, text(size: 9pt, grid(columns: (28mm, 1fr), row-gutter: 1.4mm,\n");
        if (l.YourRef.Trim().Length > 0) sb.Append("  [Ihr Zeichen:], [").Append(Escape(l.YourRef.Trim())).Append("],\n");
        if (l.OurRef.Trim().Length > 0) sb.Append("  [Unser Zeichen:], [").Append(Escape(l.OurRef.Trim())).Append("],\n");
        sb.Append("  [Datum:], [").Append(Escape(l.Date.ToString("d. MMMM yyyy", de))).Append("],\n))) <din-info>])\n\n");
        // Betreff und Text
        sb.Append("#v(").Append(Mm(g.Subject - DinTop)).Append(")\n#block[*").Append(Escape(l.Subject.Trim())).Append("*] <din-betreff>\n\n#v(0.6em)\n");
        foreach (var p in l.Body.Replace("\r", "").Split("\n\n")) if (p.Trim().Length > 0) sb.Append(Block(p)).Append("\n\n");
        sb.Append(Escape(l.Closing.Trim())).Append("\n\n#v(12mm)\n").Append(Escape((l.Signer.Trim().Length > 0 ? l.Signer : senderLines[0]).Trim())).Append('\n');
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
