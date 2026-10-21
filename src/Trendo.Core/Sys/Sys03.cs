using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core.Sys;

/// <summary>SYS-03 (Katalog freigegeben 21.10.2026): Browser & Downloads. Core rechnet und prüft nur – alles mit Netz läuft in der App (QA-019).</summary>
public static partial class Web
{
    // P06 Prüfsumme
    public static string? AlgoFor(string expected) => Regex.Replace(expected, @"\s", "").Length switch { 32 => "MD5", 40 => "SHA-1", 64 => "SHA-256", 128 => "SHA-512", _ => null };
    public static (bool Match, string Algo, string Actual) VerifyHash(string file, string expected)
    {
        var e = Regex.Replace(expected, @"\s", "").ToLowerInvariant();
        var algo = AlgoFor(e) ?? throw new ArgumentException("Das sieht nicht wie eine Prüfsumme aus (MD5, SHA-1, SHA-256 oder SHA-512).");
        using var s = File.OpenRead(file);
        var h = algo switch { "MD5" => MD5.HashData(s), "SHA-1" => SHA1.HashData(s), "SHA-512" => SHA512.HashData(s), _ => SHA256.HashData(s) };
        var a = Convert.ToHexString(h).ToLowerInvariant(); return (a == e, algo, a);
    }

    // P07 Download fortsetzen: Zustand neben der .part-Datei
    public sealed record DlState(string Url, string Target, long Total, string? ETag);
    public static string PartFile(string target) => target + ".part";
    public static string StateFile(string target) => target + ".trendo-download.json";
    public static long ResumeFrom(string target, string url)
    {
        var st = LoadState(target);
        return st is not null && st.Url == url && File.Exists(PartFile(target)) ? new FileInfo(PartFile(target)).Length : 0;
    }
    public static DlState? LoadState(string target) { try { return File.Exists(StateFile(target)) ? JsonSerializer.Deserialize<DlState>(File.ReadAllText(StateFile(target))) : null; } catch (JsonException) { return null; } }
    public static void SaveState(DlState s) => File.WriteAllText(StateFile(s.Target), JsonSerializer.Serialize(s));
    public static void Finish(string target) { File.Move(PartFile(target), target, true); File.Delete(StateFile(target)); }
    public static string SafeFileName(Uri u)
    {
        var n = Uri.UnescapeDataString(Path.GetFileName(u.AbsolutePath)); n = string.Concat(n.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
        return n.Length == 0 ? "download.bin" : n;
    }

    // P09 Lesemodus / P22 Offline-Kopie
    [GeneratedRegex(@"<(script|style|noscript|iframe|object|embed|template)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)] private static partial Regex Active();
    [GeneratedRegex(@"\son\w+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase)] private static partial Regex Handlers();
    public static string Title(string html) => WebUtility.HtmlDecode(Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[1].Value.Trim());
    /// <summary>Text eines Artikels: bevorzugt &lt;article&gt;/&lt;main&gt;, sonst alle Absätze. Skripte werden entfernt und nie ausgeführt.</summary>
    public static string ReaderText(string html)
    {
        html = Active().Replace(html, " ");
        var scope = Regex.Match(html, @"<(article|main)\b[^>]*>(.*?)</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline) is { Success: true } m ? m.Groups[2].Value : html;
        var parts = Regex.Matches(scope, @"<(h[1-3]|p|li)\b[^>]*>(.*?)</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)
            .Select(x => (Tag: x.Groups[1].Value.ToLowerInvariant(), Text: Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(x.Groups[2].Value, "<[^>]+>", "")), @"\s+", " ").Trim()))
            .Where(x => x.Text.Length > 0).Select(x => x.Tag.StartsWith('h') ? "## " + x.Text : x.Tag == "li" ? "• " + x.Text : x.Text);
        return string.Join("\n\n", parts);
    }
    /// <summary>Offline-Kopie: Skripte und Ereignis-Attribute raus, &lt;base&gt; für Bilder/CSS, Herkunft als Kommentar.</summary>
    public static string OfflineCopy(string html, Uri source)
    {
        var h = Handlers().Replace(Active().Replace(html, ""), "");
        var b = $"<base href=\"{WebUtility.HtmlEncode(source.AbsoluteUri)}\"><meta http-equiv=\"Content-Security-Policy\" content=\"script-src 'none'\">";
        h = Regex.IsMatch(h, "<head[^>]*>", RegexOptions.IgnoreCase) ? Regex.Replace(h, "<head[^>]*>", m => m.Value + b, RegexOptions.IgnoreCase) : b + h;
        return $"<!-- Offline-Kopie von {source.AbsoluteUri}, gespeichert {DateTime.Now:dd.MM.yyyy HH:mm} mit Trendo OS -->\n" + h;
    }

    // P10 Tor, P11 Privates Fenster, P12 Standardbrowser, P25 Verlauf
    public const string TorOfficial = "https://www.torproject.org/de/download/";
    public static readonly string[] TorPaths = [@"%USERPROFILE%\Desktop\Tor Browser\Browser\firefox.exe", @"%LOCALAPPDATA%\Tor Browser\Browser\firefox.exe", @"%ProgramFiles%\Tor Browser\Browser\firefox.exe"];
    public static string? PrivateArg(string browserId) => browserId switch { "chrome" or "brave" or "chromium" => "--incognito", "edge" => "--inprivate", "firefox" => "--private-window", "opera" => "--private", _ => null };
    public static string BrowserForProgId(string? progId) => progId switch
    {
        null or "" => "nicht festgelegt",
        var p when p.StartsWith("ChromeHTML") => "Google Chrome", var p when p.StartsWith("MSEdgeHTM") => "Microsoft Edge", var p when p.StartsWith("FirefoxURL") => "Mozilla Firefox",
        var p when p.StartsWith("BraveHTML") => "Brave", var p when p.StartsWith("OperaStable") => "Opera", var p => p,
    };
    public static readonly (string Id, string Name, string[] Steps)[] ClearHistory =
    [
        ("chrome", "Google Chrome", ["Chrome öffnen", "Strg + Umschalt + Entf drücken", "Zeitraum „Gesamte Zeit“ wählen", "Häkchen bei Browserverlauf (und ggf. Cookies) setzen", "„Daten löschen“ klicken"]),
        ("edge", "Microsoft Edge", ["Edge öffnen", "Strg + Umschalt + Entf drücken", "Zeitraum „Gesamte Zeit“ wählen", "Browserverlauf ankreuzen", "„Jetzt löschen“ klicken"]),
        ("firefox", "Mozilla Firefox", ["Firefox öffnen", "Strg + Umschalt + Entf drücken", "Zeitraum „Alles“ wählen", "Chronik ankreuzen", "„Löschen“ bzw. „OK“ klicken"]),
        ("brave", "Brave", ["Brave öffnen", "Strg + Umschalt + Entf drücken", "Zeitraum „Gesamte Zeit“ wählen", "Browserverlauf ankreuzen", "„Daten löschen“ klicken"]),
    ];

    // P13 Suchmaschine
    public static readonly (string Id, string Name, string Url)[] Engines = [("duckduckgo", "DuckDuckGo", "https://duckduckgo.com/?q="), ("startpage", "Startpage", "https://www.startpage.com/do/search?q="), ("google", "Google", "https://www.google.com/search?q=")];
    public static string EngineFile(string dataRoot) => Path.Combine(dataRoot, "netz", "suchmaschine.txt");
    public static string Engine(string dataRoot) { var f = EngineFile(dataRoot); var id = File.Exists(f) ? File.ReadAllText(f).Trim() : ""; return Engines.Any(e => e.Id == id) ? id : "duckduckgo"; }
    public static void SetEngine(string dataRoot, string id) { if (!Engines.Any(e => e.Id == id)) throw new ArgumentException("Unbekannte Suchmaschine."); Directory.CreateDirectory(Path.GetDirectoryName(EngineFile(dataRoot))!); File.WriteAllText(EngineFile(dataRoot), id); }
    public static string SearchUrl(string dataRoot) => Engines.First(e => e.Id == Engine(dataRoot)).Url;

    // P14 Seite als PDF (Chrome/Edge ohne Fenster)
    public static List<string> PrintToPdfArgs(Uri url, string pdf) => ["--headless=new", "--disable-gpu", "--no-pdf-header-footer", "--no-first-run", "--user-data-dir=" + Path.Combine(Path.GetTempPath(), "trendo-pdf-profil"), "--print-to-pdf=" + pdf, url.AbsoluteUri];
    /// <summary>HTML für den PDF-Druck: Offline-Kopie ohne Skripte, Papierformat A4 erzwungen.</summary>
    public static string PrintableA4(string html, Uri source) => OfflineCopy(html, source).Replace("<base href=", "<style>@page { size: A4; margin: 15mm }</style><base href=", StringComparison.Ordinal);
    public static bool IsA4(string pdfinfoOutput) => Regex.Match(pdfinfoOutput, @"Page size:\s+([\d.]+) x ([\d.]+) pts") is { Success: true } m && Math.Abs(double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) - 595) < 3 && Math.Abs(double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) - 842) < 3;

    // P15 Link-Prüfung, P16 Phishing
    public static readonly string[] Shorteners = ["bit.ly", "tinyurl.com", "t.co", "goo.gl", "ow.ly", "is.gd", "buff.ly", "rebrand.ly", "cutt.ly", "shorturl.at", "t.ly"];
    public static bool IsShort(Uri u) => Shorteners.Contains(u.Host.ToLowerInvariant());
    static readonly string[] Brands = ["paypal", "amazon", "sparkasse", "volksbank", "commerzbank", "deutsche-bank", "dkb", "ing", "postbank", "dhl", "microsoft", "apple", "google", "netflix", "ebay", "facebook", "instagram", "whatsapp", "elster", "telekom", "vodafone"];
    static int Lev(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i; for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++) for (var j = 1; j <= b.Length; j++) d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }
    /// <summary>Warnzeichen für eine Adresse (leer = nichts Auffälliges; keine Garantie).</summary>
    public static List<string> PhishingHints(string url)
    {
        var w = new List<string>();
        if (!Uri.TryCreate(url.Contains("://") ? url : "https://" + url, UriKind.Absolute, out var u)) return ["Das ist keine gültige Adresse."];
        var host = u.IdnHost.ToLowerInvariant();
        if (u.Scheme == "http") w.Add("Unverschlüsselt (http): Hier nie Passwörter oder Bankdaten eingeben.");
        if (host.Split('.').Any(p => p.StartsWith("xn--"))) w.Add($"Enthält Sonderzeichen-Domain (Punycode): eigentlich „{u.Host}“ – kann echte Seiten nachahmen.");
        if (u.Host.Any(c => c > 127) && u.Host.Any(c => c < 128 && char.IsLetter(c))) w.Add("Mischt lateinische und fremde Schriftzeichen.");
        if (IPAddress.TryParse(u.Host.Trim('[', ']'), out _)) w.Add("Zahlen statt Namen (IP-Adresse) – bei Banken oder Shops unüblich.");
        if (u.UserInfo.Length > 0) w.Add($"Enthält „@“: Die Seite ist in Wahrheit {u.Host}, nicht {u.UserInfo}.");
        if (host.Split('.').Length > 4) w.Add("Sehr viele Unterdomains – prüfe genau, wie die Adresse endet.");
        if (IsShort(u)) w.Add("Kurzlink: Das echte Ziel ist nicht sichtbar.");
        var parts = host.Split('.'); var main = parts.Length >= 2 ? parts[^2] : host;
        foreach (var b in Brands)
        {
            if (main != b && Lev(main, b) is > 0 and <= 2 && main.Length >= 4) w.Add($"Ähnelt „{b}“, ist es aber nicht ({host}).");
            else if (main != b && host.Contains(b) && !host.EndsWith(b + "." + parts[^1])) w.Add($"Enthält „{b}“, gehört aber zu {main}.{parts[^1]}.");
        }
        return w.Distinct().ToList();
    }
    public static string Explain(Uri u) => $"Ziel: {u.Host}{(u.AbsolutePath.Length > 1 ? u.AbsolutePath : "")} · {(u.Scheme == "https" ? "verschlüsselt" : "unverschlüsselt")}";

    // P17 Formular-Schutz (nur Hinweise)
    [GeneratedRegex(@"\b[A-Z]{2}\d{2}(?: ?[A-Z0-9]{4}){3,7}(?: ?[A-Z0-9]{1,3})?\b")] private static partial Regex IbanRx();
    [GeneratedRegex(@"\b(?:\d[ -]?){13,19}\b")] private static partial Regex CardRx();
    static bool Luhn(string digits) { var s = 0; var alt = false; for (var i = digits.Length - 1; i >= 0; i--) { var n = digits[i] - '0'; if (alt) { n *= 2; if (n > 9) n -= 9; } s += n; alt = !alt; } return s % 10 == 0; }
    public static List<string> FormHints(string url, string text)
    {
        var w = new List<string>();
        var http = url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        if (IbanRx().IsMatch(text.ToUpperInvariant())) w.Add("Enthält eine IBAN.");
        if (CardRx().Matches(text).Any(m => { var d = new string(m.Value.Where(char.IsDigit).ToArray()); return d.Length is >= 13 and <= 19 && Luhn(d); })) w.Add("Enthält eine Kartennummer.");
        if (Regex.IsMatch(text, @"passwor|kennwort|pin\b|tan\b", RegexOptions.IgnoreCase)) w.Add("Enthält ein Passwort, eine PIN oder TAN – echte Banken fragen nie per Formular oder Mail danach.");
        if (Regex.IsMatch(text, @"\b\d{11}\b")) w.Add("Enthält eine 11-stellige Nummer (z. B. Steuer-ID).");
        if (w.Count > 0 && http) w.Insert(0, "Achtung: Die Seite ist unverschlüsselt (http).");
        foreach (var h in PhishingHints(url).Where(h => !h.StartsWith("Unverschlüsselt"))) w.Add(h);
        return w;
    }

    // P18 Downloads sortieren
    public static string TypeFolder(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".pdf" or ".doc" or ".docx" or ".odt" or ".txt" or ".rtf" or ".xlsx" or ".xls" or ".ods" or ".csv" or ".pptx" => "Dokumente",
        ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg" or ".heic" => "Bilder",
        ".mp3" or ".wav" or ".flac" or ".ogg" or ".m4a" => "Musik", ".mp4" or ".mkv" or ".mov" or ".avi" or ".webm" => "Videos",
        ".zip" or ".7z" or ".rar" or ".tar" or ".gz" => "Archive", ".exe" or ".msi" or ".msix" or ".bat" or ".cmd" or ".ps1" => "Programme", _ => "Sonstiges",
    };
    public static List<Move> SortPlan(string folder) => Directory.GetFiles(folder).Where(f => !Browsers.Unfinished(f)).OrderBy(f => f)
        .Select(f => new Move(f, Path.Combine(folder, TypeFolder(f), Path.GetFileName(f)))).Where(m => !File.Exists(m.To)).ToList();

    // P19 Quarantäne
    public sealed record QItem(string Id, string Original, string Stored, DateTime At, string Sha256);
    public static string QDir(string dataRoot) => Path.Combine(dataRoot, "quarantaene");
    /// <summary>Verschiebt in den Quarantäne-Ordner mit Endung „.gesperrt“ – so startet Windows die Datei nicht per Doppelklick.</summary>
    public static QItem Quarantine(string dataRoot, string file, AuditLog? audit = null)
    {
        var d = QDir(dataRoot); Directory.CreateDirectory(d);
        var id = Guid.NewGuid().ToString("N")[..10]; var stored = Path.Combine(d, id + "-" + Path.GetFileName(file) + ".gesperrt");
        var sha = J.Sha(file); File.Move(file, stored);
        var q = new QItem(id, Path.GetFullPath(file), stored, DateTime.Now, sha);
        var l = J.Load<QItem>(Path.Combine(d, "liste.json")); l.Add(q); J.Save(Path.Combine(d, "liste.json"), l);
        audit?.Append("app", "quarantine.add", $"{Path.GetFileName(file)} sha256={sha}"); return q;
    }
    public static List<QItem> QuarantineList(string dataRoot) => J.Load<QItem>(Path.Combine(QDir(dataRoot), "liste.json"));
    public static void Restore(string dataRoot, string id)
    {
        var f = Path.Combine(QDir(dataRoot), "liste.json"); var l = J.Load<QItem>(f); var q = l.Single(x => x.Id == id);
        if (File.Exists(q.Original)) throw new IOException("Am alten Ort liegt schon eine Datei mit dem Namen.");
        File.Move(q.Stored, q.Original); l.Remove(q); J.Save(f, l);
    }

    // P20 Defender
    public const string MpCmdRun = @"%ProgramFiles%\Windows Defender\MpCmdRun.exe";
    public static List<string> DefenderArgs(string file) => ["-Scan", "-ScanType", "3", "-File", file, "-DisableRemediation"];
    public static string DefenderResult(int exit, string output) => exit switch
    {
        0 => "Keine Bedrohung gefunden.", 2 => "Achtung: Defender hat eine Bedrohung gefunden. Datei nicht öffnen – am besten in die Quarantäne.",
        _ => output.Contains("Admin", StringComparison.OrdinalIgnoreCase) ? "Defender verlangt Administratorrechte – Prüfung nicht möglich." : $"Prüfung nicht möglich (Code {exit}).",
    };

    // P21 Webseiten-Notiz
    public static string WebNote(Uri u, string note) => $"🔗 {u.AbsoluteUri}\n{note.Trim()}";

    // P24 Proxy
    public static string ProxyInfo(bool? enabled, string? server, string? autoConfig, string? envProxy)
    {
        var l = new List<string>();
        if (enabled == true && !string.IsNullOrWhiteSpace(server)) l.Add("Windows-Proxy: " + server);
        if (!string.IsNullOrWhiteSpace(autoConfig)) l.Add("Automatische Konfiguration: " + autoConfig);
        if (!string.IsNullOrWhiteSpace(envProxy)) l.Add("Umgebungsvariable: " + envProxy);
        return l.Count == 0 ? "Kein Proxy eingerichtet – Verbindung geht direkt ins Internet." : string.Join("\n", l);
    }
}

// P08 Lesezeichen
public sealed record Bookmark(string Title, string Url, string Folder);
public static partial class Bookmarks
{
    public static string FileIn(string dataRoot) => Path.Combine(dataRoot, "netz", "lesezeichen.json");
    public static List<Bookmark> Load(string dataRoot) => J.Load<Bookmark>(FileIn(dataRoot));
    public static void Save(string dataRoot, List<Bookmark> l) => J.Save(FileIn(dataRoot), l);
    public static void Add(string dataRoot, Bookmark b)
    {
        if (!Uri.TryCreate(b.Url, UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https")) throw new ArgumentException("Nur http- und https-Adressen.");
        var l = Load(dataRoot); l.RemoveAll(x => x.Url == b.Url); l.Add(b); Save(dataRoot, l);
    }
    /// <summary>Netscape-Lesezeichen-HTML (Export aus Chrome, Edge, Firefox) lesen – mit Ordnern.</summary>
    public static List<Bookmark> ImportHtml(string html)
    {
        var res = new List<Bookmark>(); var stack = new Stack<string>(); string? pending = null;
        foreach (Match m in Regex.Matches(html, @"<H3[^>]*>(.*?)</H3>|<A\s+[^>]*HREF=""([^""]*)""[^>]*>(.*?)</A>|<DL>|</DL>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var v = m.Value;
            if (v.StartsWith("<H3", StringComparison.OrdinalIgnoreCase)) pending = WebUtility.HtmlDecode(m.Groups[1].Value.Trim());
            else if (v.Equals("<DL>", StringComparison.OrdinalIgnoreCase)) { stack.Push(pending ?? ""); pending = null; }
            else if (v.Equals("</DL>", StringComparison.OrdinalIgnoreCase)) { if (stack.Count > 0) stack.Pop(); }
            else
            {
                var url = WebUtility.HtmlDecode(m.Groups[2].Value);
                if (Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme is "http" or "https")
                    res.Add(new(WebUtility.HtmlDecode(Regex.Replace(m.Groups[3].Value, "<[^>]+>", "").Trim()), url, string.Join("/", stack.Reverse().Where(s => s.Length > 0))));
            }
        }
        return res;
    }
    public static string ExportHtml(IEnumerable<Bookmark> list)
    {
        var sb = new StringBuilder("<!DOCTYPE NETSCAPE-Bookmark-file-1>\n<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">\n<TITLE>Bookmarks</TITLE>\n<H1>Bookmarks</H1>\n<DL><p>\n");
        foreach (var g in list.GroupBy(b => b.Folder))
        {
            var depth = 0;
            foreach (var f in g.Key.Split('/', StringSplitOptions.RemoveEmptyEntries)) { sb.Append($"<DT><H3>{WebUtility.HtmlEncode(f)}</H3>\n<DL><p>\n"); depth++; }
            foreach (var b in g) sb.Append($"<DT><A HREF=\"{WebUtility.HtmlEncode(b.Url)}\">{WebUtility.HtmlEncode(b.Title)}</A>\n");
            for (var i = 0; i < depth; i++) sb.Append("</DL><p>\n");
        }
        return sb.Append("</DL><p>\n").ToString();
    }
}
