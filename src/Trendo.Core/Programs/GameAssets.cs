using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Trendo.Core.Bibliothek;

namespace Trendo.Core.Programs;

/// <summary>Freie Spiele-Assets (Freigabe 05.11.2026, G1–G3): Godot Asset Library über ihre offizielle Schnittstelle; Kenney, Quaternius, OpenGameArt und
/// itch.io über den Browser und den Download-Ordner (kein Abgreifen). Jedes Asset steht mit Quelle, Lizenz und Urheber in der Asset-Liste des Spiels;
/// daraus entstehen der Abspann (CREDITS.txt) und die Lizenzprüfung vor dem Veröffentlichen.</summary>
public static class GameAssets
{
    public sealed record Site(string Name, string Url, string License, string Hint);
    /// <summary>Seiten ohne Schnittstelle (Download-Übernahme).</summary>
    public static readonly Site[] Sites =
    [
        new("Kenney", "https://kenney.nl/assets?search=", "CC0 1.0", "Tausende Spielfiguren, Kacheln, Töne – alles CC0"),
        new("Quaternius", "https://quaternius.com/", "CC0 1.0", "Stilisierte 3D-Figuren und Umgebungen – CC0"),
        new("OpenGameArt", "https://opengameart.org/art-search?keys=", "", "Grafik, Musik, Geräusche – Lizenz je Asset prüfen"),
        new("itch.io", "https://itch.io/game-assets/free/tag-", "", "Freie Asset-Pakete von Künstlern – Lizenz je Paket prüfen"),
    ];

    /// <summary>Verwendungen im Spiel.</summary>
    public static readonly (string Id, string Label)[] Uses =
    [
        ("figur", "Figur-Bild"), ("anim-stehen", "Animation Stehen"), ("anim-laufen", "Animation Laufen"), ("anim-springen", "Animation Springen"),
        ("anim-fallen", "Animation Fallen"), ("anim-treffen", "Animation Getroffen"), ("hintergrund", "Hintergrundbild"),
        ("ton-springen", "Ton Springen"), ("ton-muenze", "Ton Münze"), ("ton-treffer", "Ton Treffer"), ("ton-ziel", "Ton Ziel"), ("ton-musik", "Musik"), ("datei", "Nur in der Liste (z. B. 3D-Modell)"),
    ];

    public static bool IsImage(string f) => Path.GetExtension(f).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif";
    public static bool IsSound(string f) => Path.GetExtension(f).ToLowerInvariant() is ".wav" or ".ogg" or ".mp3" or ".m4a";
    /// <summary>Passt die Datei zur Verwendung?</summary>
    public static bool Fits(string use, string file) => use.StartsWith("ton-", StringComparison.Ordinal) ? IsSound(file) : use is "figur" or "hintergrund" || use.StartsWith("anim-", StringComparison.Ordinal) ? IsImage(file) : true;

    /// <summary>Bildstreifen: Anzahl Bilder aus dem Seitenverhältnis (gleich große, quadratische Einzelbilder nebeneinander).</summary>
    public static int FramesOf(int width, int height) => height <= 0 ? 1 : Math.Clamp((int)Math.Round(width / (double)height), 1, 32);

    /// <summary>Bildgröße direkt aus dem Dateikopf (PNG, GIF) – ohne Bildbibliothek; null bei anderen Formaten.</summary>
    public static (int W, int H)? ImageSize(string file)
    {
        try
        {
            using var f = File.OpenRead(file); var h = new byte[26]; if (f.Read(h, 0, h.Length) < 24) return null;
            if (h[0] == 0x89 && h[1] == (byte)'P' && h[2] == (byte)'N' && h[3] == (byte)'G') return ((h[16] << 24) | (h[17] << 16) | (h[18] << 8) | h[19], (h[20] << 24) | (h[21] << 16) | (h[22] << 8) | h[23]);
            if (h[0] == (byte)'G' && h[1] == (byte)'I' && h[2] == (byte)'F') return (h[6] | (h[7] << 8), h[8] | (h[9] << 8));
        }
        catch (IOException) { }
        return null;
    }

    // ───────────── Godot Asset Library ─────────────
    public sealed record GodotHit(string Id, string Title, string Author, string License, string Category, string IconUrl);

    public static async Task<List<GodotHit>> SearchGodot(IWebFetch http, string query, CancellationToken ct = default)
    {
        var url = $"https://godotengine.org/asset-library/api/asset?filter={Uri.EscapeDataString(query.Trim())}&godot_version=4.3&max_results=30";
        using var d = JsonDocument.Parse((await http.GetAsync(url, ct)).EnsureOk().Text);
        var res = new List<GodotHit>();
        if (d.RootElement.TryGetProperty("result", out var r))
            foreach (var a in r.EnumerateArray())
                res.Add(new(S(a, "asset_id"), S(a, "title"), S(a, "author"), S(a, "cost"), S(a, "category"), S(a, "icon_url")));
        return res;
    }

    /// <summary>Godot-Asset laden (ZIP von der offiziellen Download-Adresse), entpacken (ohne Pfad-Ausbruch); liefert den Ordner.</summary>
    public static async Task<string> DownloadGodot(IWebFetch http, GodotHit hit, string targetRoot, CancellationToken ct = default)
    {
        using var d = JsonDocument.Parse((await http.GetAsync($"https://godotengine.org/asset-library/api/asset/{Uri.EscapeDataString(hit.Id)}", ct)).EnsureOk().Text);
        var dl = S(d.RootElement, "download_url");
        if (!Uri.TryCreate(dl, UriKind.Absolute, out var u) || u.Scheme != "https" || !(u.Host is "github.com" or "codeload.github.com" or "gitlab.com" || u.Host.EndsWith(".github.com") || u.Host.EndsWith(".githubusercontent.com")))
            throw new InvalidDataException("Die Download-Adresse liegt nicht bei GitHub/GitLab – nicht geladen.");
        var dir = Path.Combine(targetRoot, "godot-" + new string(hit.Id.Where(char.IsLetterOrDigit).ToArray())); Directory.CreateDirectory(dir);
        var zip = Path.Combine(dir, "paket.zip"); await File.WriteAllBytesAsync(zip, (await http.GetAsync(u.AbsoluteUri, ct)).EnsureOk().Body, ct);
        Unzip(zip, dir); File.Delete(zip);
        return dir;
    }

    public static void Unzip(string zip, string dir)
    {
        var full = Path.GetFullPath(dir) + Path.DirectorySeparatorChar;
        using var z = ZipFile.OpenRead(zip);
        foreach (var e in z.Entries.Where(e => e.Name.Length > 0))
        {
            var target = Path.GetFullPath(Path.Combine(dir, e.FullName)); if (!target.StartsWith(full, StringComparison.OrdinalIgnoreCase)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!); e.ExtractToFile(target, true);
        }
    }

    // ───────────── Asset-Liste des Spiels ─────────────
    /// <summary>Asset ins Spiel aufnehmen: Kopie in den Projekt-Asset-Ordner, Eintrag mit Quelle, Lizenz, Urheber und Verwendung.</summary>
    public static JsonObject Add(JsonObject doc, string assetDir, string file, string source, string license, string author, string url, string use)
    {
        if (!File.Exists(file)) throw new FileNotFoundException("Datei nicht gefunden.", file);
        if (!Uses.Any(u => u.Id == use)) throw new ArgumentException("Unbekannte Verwendung.");
        if (!Fits(use, file)) throw new ArgumentException(use.StartsWith("ton-", StringComparison.Ordinal) ? "Für Töne bitte eine Tondatei (WAV, OGG, MP3)." : "Dafür bitte ein Bild (PNG, JPG, WebP, GIF).");
        Directory.CreateDirectory(assetDir);
        var name = Path.GetFileName(file); var target = Path.Combine(assetDir, name);
        for (var i = 2; File.Exists(target) && !SameFile(target, file); i++) target = Path.Combine(assetDir, $"{Path.GetFileNameWithoutExtension(name)}-{i}{Path.GetExtension(name)}");
        if (!SameFile(target, file)) File.Copy(file, target, true);
        var a = doc["assets"] as JsonArray ?? [];
        if (a.Count >= 500) throw new InvalidOperationException("Höchstens 500 Assets je Spiel.");
        var entry = new JsonObject { ["name"] = Path.GetFileNameWithoutExtension(name), ["file"] = target, ["source"] = source, ["license"] = license, ["author"] = author, ["url"] = url, ["use"] = use };
        a.Add(entry); doc["assets"] = a;
        return entry;
    }
    static bool SameFile(string a, string b) => File.Exists(a) && string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    static string S(JsonElement e, string k) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) ? v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.GetRawText() : "";
    static string S(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";

    /// <summary>Abspann: alle Assets, die genannt werden müssen (und freiwillig die CC0-Quellen).</summary>
    public static string Credits(JsonObject doc)
    {
        var list = (doc["assets"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        if (list.Count == 0) return "";
        var sb = new System.Text.StringBuilder($"{S(doc["title"])} – Mitwirkende und Lizenzen\n\n");
        foreach (var g in list.GroupBy(a => S(a["source"])))
        {
            sb.Append(g.Key).Append('\n');
            foreach (var a in g) sb.Append($"  „{S(a["name"])}“ von {(S(a["author"]) is { Length: > 0 } au ? au : "unbekannt")} – {S(a["license"])}" + (S(a["url"]) is { Length: > 0 } u ? $" – {u}" : "") + "\n");
        }
        return sb.ToString();
    }

    /// <summary>G3: Lizenzprüfung vor dem Veröffentlichen – nicht kommerziell nutzbare Assets, fehlende Urheber bei Namensnennungspflicht, unbekannte Lizenzen.</summary>
    public static List<string> Check(JsonObject doc, bool commercial)
    {
        var w = new List<string>();
        foreach (var a in (doc["assets"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var lic = S(a["license"]); var name = S(a["name"]);
            if (lic.Length == 0) { w.Add($"„{name}“: Lizenz fehlt – bitte von der Seite übernehmen."); continue; }
            if (commercial && !FreeModels.Commercial(lic) && !lic.Contains("MIT", StringComparison.OrdinalIgnoreCase)) w.Add($"„{name}“ ({lic}) darf nicht in einem verkauften Spiel stecken – ersetzen oder kostenlos veröffentlichen.");
            if (FreeModels.NeedsCredit(lic) && S(a["author"]).Length == 0) w.Add($"„{name}“ ({lic}) braucht eine Namensnennung, aber der Urheber fehlt.");
            if (S(a["file"]) is { Length: > 0 } f && !File.Exists(f)) w.Add($"„{name}“: Datei fehlt ({Path.GetFileName(f)}).");
        }
        return w;
    }
}
