using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>T-0718 (Audit C4): Eine Website aus Trendo veröffentlichen geht nur über das Freigabe-Tor (<see cref="ApprovalGate"/>).
/// Jeder Weg hat eine eigene Wirkung (Aktion). Das Ziel der Freigabe ist „Ort#Fingerabdruck der gezeigten Dateien“ – die Freigabe gilt also genau für
/// diese Dateien an genau diesem Ort, genau einmal. Ändert sich der Export, passt der Fingerabdruck nicht mehr und es braucht eine neue Freigabe.
/// Ordner und ZIP schreibt der Kern selbst; Netzwege (eigener Server über WebDAV, Netlify) führt die App aus – der Kern bleibt ohne Netz-Bibliothek (QA-019).
/// Danach meldet Trendo Ort, Anzahl Dateien und das Prüfergebnis (Links und Dateien), bei Fehlern mit „Was tun“.</summary>
public static class WebPublishing
{
    public const string ActionOrdner = "web.veroeffentlichen.ordner";
    public const string ActionZip = "web.veroeffentlichen.zip";
    public const string ActionServer = "web.veroeffentlichen.server";
    public const string ActionNetlify = "web.veroeffentlichen.netlify";
    /// <summary>Merkzettel im Zielordner: welche Dateien Trendo dort zuletzt veröffentlicht hat (nur diese werden beim nächsten Mal ersetzt oder entfernt).</summary>
    public const string Marker = ".trendo-veroeffentlicht.json";

    public enum Weg { Ordner, Zip, Server, Netlify }

    public static string ActionOf(Weg w) => w switch
    {
        Weg.Ordner => ActionOrdner, Weg.Zip => ActionZip, Weg.Server => ActionServer, _ => ActionNetlify,
    };

    /// <summary>Ziel der Freigabe: Ort und Fingerabdruck der gezeigten Dateien.</summary>
    public static string Target(string where, string fingerprint) => where + "#" + fingerprint;

    /// <summary>Inhalt der Rückfrage im Tor: Wirkung, Ziel, Dateien, Größe. Preview = vollständige Dateiliste.</summary>
    public sealed record Rueckfrage(string Action, string Target, string Title, string Text, string Yes, string Preview);

    public static Rueckfrage Describe(Weg weg, string where, WebDesignPublish.Plan plan) => Describe(weg, where, plan.Files, plan.Fingerprint);

    public static Rueckfrage Describe(Weg weg, string where, IReadOnlyList<WebDesignPublish.FileItem> files, string fingerprint)
    {
        var bytes = files.Sum(f => f.Bytes);
        var (title, wirkung, yes) = weg switch
        {
            Weg.Ordner => ("Website in einen Ordner veröffentlichen?", "Trendo kopiert die geprüfte Website in diesen Ordner. Dateien einer früheren Veröffentlichung von Trendo dort werden ersetzt, andere Dateien bleiben unberührt. Es geht nichts ins Internet.", "Veröffentlichen"),
            Weg.Zip => ("Website als Paket (ZIP) speichern?", "Trendo packt genau diese Dateien in eine ZIP-Datei auf diesem PC. Es geht nichts ins Internet.", "Paket speichern"),
            Weg.Server => ("Website auf deinen Server hochladen?", "Trendo lädt die Website mit deinem Benutzernamen per WebDAV auf deinen Server. " + (IsHomeNetwork(where) ? "Der Server liegt in deinem Heimnetz bzw. auf diesem PC." : "Der Server liegt im Internet – die Seite ist danach für alle erreichbar, die die Adresse kennen."), "Hochladen"),
            _ => ("Website auf Netlify veröffentlichen?", "Trendo lädt die Website mit deinem eigenen Netlify-Konto hoch. Sie ist danach öffentlich im Internet erreichbar.", "Veröffentlichen"),
        };
        var text = $"Wirkung: {wirkung}\nZiel: {where}\nDateien: {files.Count} · Größe: {Kb(bytes)}\n\nFreigegeben werden genau diese Dateien. Ändert sich vorher etwas, fragt Trendo neu.";
        var preview = string.Join("\n", files.Select(f => $"• {f.Rel} ({Kb(f.Bytes)})"));
        return new(ActionOf(weg), Target(where, fingerprint), title, text, yes, preview);
    }

    internal static string Kb(long bytes) => $"{bytes / 1024.0:0.#} KB";

    /// <summary>Rückmeldung nach dem Veröffentlichen. <see cref="Geprueft"/>: Zahl der geprüften Links/Dateien; <see cref="Befunde"/>: was nicht stimmt.</summary>
    public sealed record Ergebnis(bool Ok, string Wo, int Dateien, long Bytes, int Geprueft, IReadOnlyList<string> Befunde, string Meldung, string? WasTun = null)
    {
        public string Text => Meldung
            + (Befunde.Count > 0 ? "\n" + string.Join("\n", Befunde.Take(8).Select(b => "✗ " + b)) + (Befunde.Count > 8 ? $"\n… und {Befunde.Count - 8} weitere" : "") : "")
            + (WasTun is { Length: > 0 } t ? "\nWas tun: " + t : "");
    }

    /// <summary>Verständliche Fehlermeldung mit „Was tun“ – es wurde nichts (oder nicht alles) veröffentlicht.</summary>
    public static Ergebnis Fehler(string meldung, string wasTun, string wo = "") => new(false, wo, 0, 0, 0, [], "Nicht veröffentlicht: " + meldung, wasTun);

    // ───────────── Link- und Dateiprüfung ─────────────

    static readonly Regex AttrRef = new("\\b(?:href|src|poster)\\s*=\\s*['\"]([^'\"]*)['\"]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex SrcSet = new("\\bsrcset\\s*=\\s*['\"]([^'\"]*)['\"]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex CssUrl = new("url\\(\\s*['\"]?([^'\")]+)['\"]?\\s*\\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Alle Verweise einer HTML- oder CSS-Datei (href, src, poster, srcset, CSS url()).</summary>
    public static IEnumerable<string> Refs(string text, bool css)
    {
        if (!css)
        {
            foreach (Match m in AttrRef.Matches(text)) yield return WebUtility.HtmlDecode(m.Groups[1].Value.Trim());
            foreach (Match m in SrcSet.Matches(text))
                foreach (var part in m.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    yield return WebUtility.HtmlDecode(part.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]);
        }
        foreach (Match m in CssUrl.Matches(text)) yield return m.Groups[1].Value.Trim();
    }

    /// <summary>Verweis, der im Ordner liegen muss? (nicht: Netz, Anker, Mail, Telefon, Daten-URL)</summary>
    public static bool IsLocal(string v) => v.Length > 0 && !v.StartsWith('#') && !v.StartsWith("//", StringComparison.Ordinal)
        && !Regex.IsMatch(v, "^[a-zA-Z][a-zA-Z0-9+.-]*:");

    /// <summary>Prüft im veröffentlichten Ordner jede HTML- und CSS-Datei: Jeder lokale Verweis muss als Datei im Ordner liegen (Ordner-Links → index.html).
    /// Liefert die Zahl der geprüften Verweise und die Befunde. Netz-Verweise werden nicht geöffnet.</summary>
    public static (int Geprueft, IReadOnlyList<string> Befunde) CheckLinks(string dir)
    {
        var root = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var befunde = new List<string>(); var n = 0;
        if (!File.Exists(Path.Combine(root, "index.html"))) befunde.Add("Die Startseite index.html fehlt.");
        var files = Directory.Exists(root) ? Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint })
            .Where(f => Path.GetExtension(f).ToLowerInvariant() is ".html" or ".htm" or ".css")
            .Where(f => !Path.GetRelativePath(root, f).Replace('\\', '/').Split('/').Any(p => p.StartsWith('.')))
            .OrderBy(f => f, StringComparer.Ordinal).ToList() : [];
        foreach (var f in files)
        {
            var rel = Path.GetRelativePath(root, f).Replace('\\', '/');
            var css = f.EndsWith(".css", StringComparison.OrdinalIgnoreCase);
            foreach (var raw in Refs(File.ReadAllText(f), css).Distinct())
            {
                if (!IsLocal(raw)) continue;
                n++;
                var v = raw.Split('#', '?')[0];
                if (v.Length == 0) continue;
                string target;
                try { target = Path.GetFullPath(v.StartsWith('/') ? Path.Combine(root, Uri.UnescapeDataString(v.TrimStart('/'))) : Path.Combine(Path.GetDirectoryName(f)!, Uri.UnescapeDataString(v))); }
                catch (ArgumentException) { befunde.Add($"{rel}: „{raw}“ ist kein gültiger Verweis."); continue; }
                if (!target.StartsWith(root, StringComparison.Ordinal)) { befunde.Add($"{rel}: „{raw}“ zeigt aus der Website heraus."); continue; }
                if (v.EndsWith('/') || Directory.Exists(target)) target = Path.Combine(target, "index.html");
                if (!File.Exists(target)) befunde.Add($"{rel}: „{raw}“ fehlt im veröffentlichten Ordner.");
            }
        }
        return (n, befunde);
    }

    // ───────────── Dateien genau so lesen, wie sie freigegeben wurden ─────────────

    /// <summary>Liest genau die Dateien des Plans und prüft den Fingerabdruck über dieselben Bytes (kein zweiter Durchlauf, kein Wettlauf).
    /// Fehler: Datei fehlt, ist eine Verknüpfung oder hat sich seit der Prüfung geändert.</summary>
    public static (IReadOnlyList<(string Rel, byte[] Bytes)>? Files, string? Error) ReadExactly(string sourceDir, WebDesignPublish.Plan plan)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var list = new List<(string, byte[])>();
        foreach (var f in plan.Files)
        {
            var full = Path.Combine(sourceDir, f.Rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full)) return (null, $"„{f.Rel}“ fehlt inzwischen im Export.");
            if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) return (null, $"„{f.Rel}“ ist eine Verknüpfung und wird nicht veröffentlicht.");
            var bytes = File.ReadAllBytes(full);
            sha.AppendData(Encoding.UTF8.GetBytes(f.Rel + "\n")); sha.AppendData(SHA256.HashData(bytes));
            list.Add((f.Rel, bytes));
        }
        return Convert.ToHexString(sha.GetHashAndReset()) == plan.Fingerprint && plan.Files.Count > 0
            ? (list, null) : (null, "Der Export hat sich nach der Prüfung geändert.");
    }

    const string NeuPruefen = "„Prüfen und Dateiliste zeigen“ wählen und noch einmal veröffentlichen.";

    // ───────────── Weg 1: Ordner ─────────────

    /// <summary>Vorab-Prüfung des Zielordners (vor der Rückfrage): nicht im Export selbst, nicht über dem Export, und entweder leer oder ein Ordner,
    /// in den Trendo schon veröffentlicht hat (Merkzettel). Fremde Dateien werden nie überschrieben.</summary>
    public static Ergebnis? FolderError(string sourceDir, string dest)
    {
        if (string.IsNullOrWhiteSpace(dest)) return Fehler("Kein Zielordner gewählt.", "Einen Ordner wählen, zum Beispiel einen neuen, leeren Ordner auf dem Desktop oder einem USB-Stick.");
        string src, dst;
        try { src = Path.GetFullPath(sourceDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar; dst = Path.GetFullPath(dest).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar; }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return Fehler("Der Zielordner ist ungültig.", "Einen anderen Ordner wählen.", dest); }
        if (dst.StartsWith(src, StringComparison.OrdinalIgnoreCase) || src.StartsWith(dst, StringComparison.OrdinalIgnoreCase))
            return Fehler("Der Zielordner liegt im Projekt oder das Projekt liegt in ihm.", "Einen Ordner außerhalb des Projekts wählen, zum Beispiel einen neuen Ordner „Meine Website“.", dest);
        if (File.Exists(dest)) return Fehler("Am Ziel liegt eine Datei, kein Ordner.", "Einen Ordner wählen.", dest);
        if (Directory.Exists(dest) && !File.Exists(Path.Combine(dest, Marker)) && Directory.EnumerateFileSystemEntries(dest).Any())
            return Fehler("Der Ordner ist nicht leer – Trendo überschreibt keine fremden Dateien.", "Einen leeren Ordner wählen oder den Ordner, in den Trendo schon einmal veröffentlicht hat.", dest);
        return null;
    }

    sealed record MarkerFile(int Version, string Fingerprint, DateTimeOffset At, List<string> Files);

    /// <summary>Veröffentlicht in einen Ordner – nur mit der Freigabe <paramref name="approvalId"/> für genau diesen Ordner und diese Dateien (einmalig, im Protokoll).
    /// Jede Datei wird atomar geschrieben; Dateien der letzten Trendo-Veröffentlichung, die nicht mehr dazugehören, werden entfernt. Danach Link- und Dateiprüfung.</summary>
    public static Ergebnis ToFolder(ApprovalGate gate, string approvalId, string sourceDir, WebDesignPublish.Plan plan, string dest) =>
        gate.Execute(approvalId, ActionOrdner, Target(dest, plan.Fingerprint), () =>
        {
            if (FolderError(sourceDir, dest) is { } fe) return fe;
            var (files, err) = ReadExactly(sourceDir, plan);
            if (files is null) return Fehler(err!, NeuPruefen, dest);
            try
            {
                Directory.CreateDirectory(dest);
                var markerPath = Path.Combine(dest, Marker);
                var old = File.Exists(markerPath) ? TryRead(markerPath)?.Files ?? [] : [];
                foreach (var (rel, bytes) in files) AtomicFile.WriteAllBytes(Path.Combine(dest, rel.Replace('/', Path.DirectorySeparatorChar)), bytes);
                var root = Path.GetFullPath(dest).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                foreach (var gone in old.Except(files.Select(f => f.Rel), StringComparer.Ordinal))
                {
                    var p = Path.GetFullPath(Path.Combine(dest, gone.Replace('/', Path.DirectorySeparatorChar)));
                    if (p.StartsWith(root, StringComparison.Ordinal) && File.Exists(p)) File.Delete(p);
                }
                AtomicFile.WriteAllText(markerPath, JsonSerializer.Serialize(new MarkerFile(1, plan.Fingerprint, DateTimeOffset.Now, files.Select(f => f.Rel).ToList())));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return Fehler("Der Ordner ließ sich nicht beschreiben (" + e.Message + ").", "Prüfen, ob der Ordner schreibgeschützt ist oder der Datenträger voll ist, oder einen anderen Ordner wählen.", dest);
            }
            var (n, befunde) = CheckLinks(dest);
            return Bericht(dest, plan, n, befunde, $"Veröffentlicht in {dest}");
        });

    static MarkerFile? TryRead(string f) { try { return JsonSerializer.Deserialize<MarkerFile>(File.ReadAllText(f)); } catch (JsonException) { return null; } }

    /// <summary>Gemeinsame Rückmeldung: Ort, Dateien, Größe, Prüfergebnis.</summary>
    public static Ergebnis Bericht(string wo, WebDesignPublish.Plan plan, int geprueft, IReadOnlyList<string> befunde, string kopf, string? extra = null)
    {
        var ok = befunde.Count == 0;
        var pruef = ok ? $"Prüfung: {geprueft} Links und Dateien geprüft – alles gefunden." : $"Prüfung: {geprueft} Links und Dateien geprüft – {befunde.Count} Problem(e):";
        return new(ok, wo, plan.Files.Count, plan.Bytes, geprueft, befunde,
            $"{kopf}: {plan.Files.Count} Dateien, {Kb(plan.Bytes)}." + (extra is null ? "" : "\n" + extra) + "\n" + pruef,
            ok ? null : "Den Fehler in Trendo beheben, neu exportieren und noch einmal veröffentlichen.");
    }

    // ───────────── Weg 2: Paket (ZIP) ─────────────

    /// <summary>ZIP genau aus den freigegebenen Dateien in <paramref name="zipDir"/> – nur mit Freigabe. Danach wird das Paket wieder geöffnet und gegen die Dateiliste geprüft.</summary>
    public static Ergebnis ToZip(ApprovalGate gate, string approvalId, string sourceDir, WebDesignPublish.Plan plan, string zipDir) =>
        gate.Execute(approvalId, ActionZip, Target(zipDir, plan.Fingerprint), () =>
        {
            var (files, err) = ReadExactly(sourceDir, plan);
            if (files is null) return Fehler(err!, NeuPruefen, zipDir);
            var zip = Path.Combine(zipDir, $"website-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
            for (var k = 2; File.Exists(zip); k++) zip = Path.Combine(zipDir, $"website-{DateTime.Now:yyyyMMdd-HHmmss}-{k}.zip");
            try
            {
                using var ms = new MemoryStream();
                using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true))
                    foreach (var (rel, bytes) in files) { using var s = z.CreateEntry(rel, CompressionLevel.Optimal).Open(); s.Write(bytes); }
                AtomicFile.WriteAllBytes(zip, ms.ToArray());
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { return Fehler("Das Paket ließ sich nicht speichern (" + e.Message + ").", "Platz auf dem Datenträger prüfen und noch einmal versuchen.", zipDir); }
            // Nachprüfung: Paket öffnen, jede Datei mit dem freigegebenen Inhalt vergleichen
            var befunde = new List<string>();
            using (var z = ZipFile.OpenRead(zip))
            {
                var want = files.ToDictionary(f => f.Rel, f => f.Bytes, StringComparer.Ordinal);
                foreach (var e in z.Entries)
                {
                    using var s = e.Open(); using var m = new MemoryStream(); s.CopyTo(m);
                    if (!want.Remove(e.FullName, out var b) || !b.AsSpan().SequenceEqual(m.ToArray())) befunde.Add($"Paket: „{e.FullName}“ weicht ab.");
                }
                befunde.AddRange(want.Keys.Select(k => $"Paket: „{k}“ fehlt."));
            }
            var (n, linkBefunde) = CheckLinks(sourceDir);
            befunde.AddRange(linkBefunde);
            return Bericht(zip, plan, n + plan.Files.Count, befunde, $"Paket gespeichert: {zip}", "Es wurde nichts hochgeladen. Das Paket kannst du bei jedem Webhoster hochladen.");
        });

    // ───────────── Weg 3: eigener Server (WebDAV) – Netzarbeit in der App ─────────────

    /// <summary>Server-Adresse prüfen: http/https, ohne Benutzername/Passwort in der Adresse. Unverschlüsselt (http) nur auf diesem PC oder im Heimnetz,
    /// sonst nur https – damit das Passwort nie ungeschützt durchs Internet geht. Liefert die Adresse mit „/“ am Ende.</summary>
    public static (Uri? Url, string? Error) ServerAddress(string? text)
    {
        var t = (text ?? "").Trim();
        if (t.Length == 0) return (null, "Bitte die Adresse deines Servers eintragen, zum Beispiel https://mein-server.de/webdav/ oder http://192.168.1.20/site/.");
        if (!Uri.TryCreate(t, UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https") || string.IsNullOrEmpty(u.Host))
            return (null, "Die Adresse muss mit https:// (oder im Heimnetz http://) beginnen.");
        if (u.UserInfo.Length > 0) return (null, "Benutzername und Passwort bitte nicht in die Adresse schreiben, sondern in die Felder darunter.");
        if (u.Query.Length > 0 || u.Fragment.Length > 0) return (null, "Die Adresse darf kein „?“ oder „#“ enthalten.");
        if (u.Scheme == "http" && !IsHomeNetwork(u.ToString())) return (null, "Unverschlüsselt (http://) geht nur im Heimnetz. Für einen Server im Internet bitte https:// verwenden.");
        var b = new UriBuilder(u); if (!b.Path.EndsWith('/')) b.Path += "/";
        return (b.Uri, null);
    }

    /// <summary>Liegt die Adresse auf diesem PC oder im Heimnetz (127.0.0.1, localhost, 10/8, 172.16/12, 192.168/16)?</summary>
    public static bool IsHomeNetwork(string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var u)) return false;
        if (u.IsLoopback) return true;
        return IPAddress.TryParse(u.Host.Trim('[', ']'), out var ip) && (IPAddress.IsLoopback(ip) || FolderShare.IsPrivateLan(ip));
    }

    /// <summary>Ordner, die auf dem Server der Reihe nach angelegt werden müssen (oberste zuerst).</summary>
    public static IReadOnlyList<string> ServerFolders(IEnumerable<string> rels)
    {
        var res = new List<string>();
        foreach (var r in rels)
        {
            var parts = r.Split('/');
            for (var i = 1; i < parts.Length; i++) { var d = string.Join('/', parts.Take(i)) + "/"; if (!res.Contains(d)) res.Add(d); }
        }
        return res;
    }

    /// <summary>Adresse einer Datei/eines Ordners auf dem Server (jeder Namensteil sauber kodiert).</summary>
    public static Uri ServerUrl(Uri baseUrl, string rel) =>
        new(baseUrl, string.Join('/', rel.Split('/').Select(Uri.EscapeDataString)));

    /// <summary>Verständliche Meldung zu einer Antwort des Servers.</summary>
    public static (string Meldung, string WasTun) ServerFehler(int status) => status switch
    {
        401 or 403 => ($"Der Server hat Benutzername oder Passwort abgelehnt (Fehler {status}).", "Benutzername und Passwort prüfen. Bei manchen Anbietern braucht WebDAV ein eigenes App-Passwort."),
        404 or 409 => ($"Den Ordner gibt es auf dem Server nicht (Fehler {status}).", "Die Adresse prüfen – sie muss auf einen vorhandenen WebDAV-Ordner zeigen."),
        405 or 501 => ($"Der Server nimmt unter dieser Adresse keine Dateien an (Fehler {status}).", "Prüfen, ob WebDAV auf dem Server eingeschaltet ist und die Adresse auf den WebDAV-Ordner zeigt."),
        507 => ("Auf dem Server ist kein Platz mehr (Fehler 507).", "Platz auf dem Server schaffen und noch einmal hochladen."),
        _ => ($"Der Server meldet Fehler {status}.", "Später noch einmal versuchen oder beim Anbieter des Servers nachfragen."),
    };

    public const string NichtErreichbarWasTun = "Prüfen, ob der Server eingeschaltet und die Adresse richtig ist (im Heimnetz: ist der PC im selben Netz?).";
}
