using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Stufe 3 im Studio „Webseite bauen“: mehrere Seiten je Projekt, Sprachen (Z5), Weiterleitungen und Website-Adresse (W3), Übergang zwischen Seiten (W4).
/// Jede Seite ist eine eigene Editor-Datei: die Startseite bleibt <c>seite.trendo.json</c>, weitere Seiten liegen unter <c>seiten/</c>, Übersetzungen unter <c>sprachen/&lt;code&gt;/</c>.
/// Gestaltungswerte und Komponenten liegen schon projektweit und gelten damit für alle Seiten. Die „aktive“ Seite bestimmt, was Editor, KI und Stände bearbeiten.</summary>
public static partial class WebDesignSite
{
    public const string SiteFile = "website.trendo.json", PagesDir = "seiten", LangDir = "sprachen", Index = "index", PageExt = ".trendo.json";
    public const int MaxPages = 200;
    public static readonly string[] Transitions = ["keiner", "einblenden", "schieben"];
    /// <summary>Namen, die der Export selbst braucht; Seiten und Sammlungen dürfen so nicht heißen.</summary>
    public static readonly HashSet<string> Reserved = ["assets", "bilder", "medien", "daten", "sitemap", "robots", "namensnennung", "seiten", "sprachen", "veroeffentlichen", "404"];

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")] private static partial Regex SlugPattern();
    [GeneratedRegex("^[a-z]{2}(-[a-z]{2})?$")] private static partial Regex LangPattern();

    public sealed record Weiterleitung(string Von, string Nach);
    public sealed record Settings(string Basis, string Standardsprache, IReadOnlyList<string> Sprachen, string Uebergang, IReadOnlyList<Weiterleitung> Weiterleitungen,
        string Aktiv, string AktivSprache, IReadOnlyList<string> Reihenfolge);
    public sealed record PageKey(string Adresse, string Sprache);

    /// <summary>Gültige Seitenadresse: Kleinbuchstaben, Ziffern und Bindestriche, keine reservierten Namen und keine Sprachcodes.</summary>
    public static string? CheckSlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return "Bitte eine Adresse angeben, z. B. „ueber-uns“.";
        if (slug.Length > 60) return "Die Adresse ist zu lang (höchstens 60 Zeichen).";
        if (!SlugPattern().IsMatch(slug)) return "Adresse nur aus Kleinbuchstaben, Ziffern und Bindestrichen, z. B. „ueber-uns“.";
        if (Reserved.Contains(slug)) return $"„{slug}“ braucht der Export selbst. Bitte eine andere Adresse.";
        if (LangPattern().IsMatch(slug)) return $"„{slug}“ sieht aus wie ein Sprachcode und ist für Sprachen reserviert.";
        return null;
    }

    /// <summary>Aus einem Titel eine Adresse machen (ä → ae, Leerzeichen → Bindestrich).</summary>
    public static string SlugOf(string text)
    {
        var s = text.Trim().ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
        s = Regex.Replace(s, "[^a-z0-9]+", "-").Trim('-');
        return s.Length > 60 ? s[..60].Trim('-') : s;
    }

    public static bool IsLanguage(string? code) => code is not null && LangPattern().IsMatch(code);

    // ---------- Einstellungen ----------

    static JsonObject ReadRaw(string dir)
    {
        var f = Path.Combine(dir, SiteFile);
        try { if (File.Exists(f) && JsonNode.Parse(File.ReadAllText(f)) is JsonObject o) return o; }
        catch (Exception e) when (e is JsonException or IOException or ArgumentException) { }
        return new JsonObject();
    }

    static void WriteRaw(string dir, JsonObject o)
    {
        Directory.CreateDirectory(dir);
        var f = Path.Combine(dir, SiteFile);
        File.WriteAllText(f + ".neu", o.ToJsonString(new JsonSerializerOptions { WriteIndented = true })); File.Move(f + ".neu", f, true);
    }

    static string Str(JsonNode? n, string d = "") => n is JsonValue v && v.TryGetValue(out string? s) ? s : d;

    /// <summary>Einstellungen der Website; Unbekanntes oder Ungültiges fällt auf sichere Werte zurück.</summary>
    public static Settings Load(string dir)
    {
        var o = ReadRaw(dir);
        var std = Str(o["standardsprache"], "de"); if (!IsLanguage(std)) std = "de";
        var langs = new List<string> { std };
        if (o["sprachen"] is JsonArray la) foreach (var l in la) if (Str(l) is { } c && IsLanguage(c) && !langs.Contains(c) && langs.Count < 12) langs.Add(c);
        var basis = Str(o["basis"]); if (CheckBase(basis) is not null) basis = "";
        var ueb = Str(o["uebergang"], "keiner"); if (!Transitions.Contains(ueb)) ueb = "keiner";
        var redirects = new List<Weiterleitung>();
        if (o["weiterleitungen"] is JsonArray ra)
            foreach (var r in ra.OfType<JsonObject>()) if (NormalizeFrom(Str(r["von"])) is { } von && NormalizeTarget(Str(r["nach"])) is { } nach) redirects.Add(new(von, nach));
        var order = o["reihenfolge"] is JsonArray oa ? oa.Select(x => Str(x)).Where(x => x == Index || CheckSlug(x) is null).Distinct().ToList() : [];
        var aktivSprache = Str(o["aktivSprache"], std); if (!langs.Contains(aktivSprache)) aktivSprache = std;
        var aktiv = Str(o["aktiv"], Index); if (aktiv != Index && (CheckSlug(aktiv) is not null || !File.Exists(FileOf(dir, aktiv, std, std)))) aktiv = Index;
        return new(basis, std, langs, ueb, redirects, aktiv, aktivSprache, order);
    }

    /// <summary>Website-Adresse für Sitemap, kanonische Adressen und hreflang: nur https, ohne Pfadrest-Tricks.</summary>
    public static string? CheckBase(string? basis)
    {
        if (string.IsNullOrEmpty(basis)) return null;
        if (!Uri.TryCreate(basis, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(u.Query) || !string.IsNullOrEmpty(u.Fragment) || u.UserInfo.Length > 0 || basis.Any(char.IsWhiteSpace))
            return "Die Adresse der Website bitte vollständig mit https://, z. B. https://www.beispiel.de";
        return null;
    }

    public static string? SetBase(string dir, string basis)
    {
        var b = basis.Trim().TrimEnd('/');
        if (CheckBase(b) is { } err) return err;
        var o = ReadRaw(dir); o["basis"] = b; WriteRaw(dir, o); return null;
    }

    public static string? SetTransition(string dir, string uebergang)
    {
        if (!Transitions.Contains(uebergang)) return "Unbekannter Übergang.";
        var o = ReadRaw(dir); o["uebergang"] = uebergang; WriteRaw(dir, o); return null;
    }

    // ---------- Dateien ----------

    /// <summary>Datei einer Seite in einer Sprache.</summary>
    public static string FileOf(string dir, string adresse, string sprache, string standard) =>
        sprache == standard
            ? (adresse == Index ? Path.Combine(dir, WebDesign.PageFile) : Path.Combine(dir, PagesDir, adresse + PageExt))
            : Path.Combine(dir, LangDir, sprache, adresse + PageExt);

    public static string FileOf(string dir, PageKey k) => FileOf(dir, k.Adresse, k.Sprache, Load(dir).Standardsprache);

    /// <summary>Datei, die Editor, KI und Stände gerade bearbeiten.</summary>
    public static string ActiveFile(string dir)
    {
        var s = Load(dir);
        return FileOf(dir, s.Aktiv, s.AktivSprache, s.Standardsprache);
    }

    public static PageKey Active(string dir) { var s = Load(dir); return new(s.Aktiv, s.AktivSprache); }

    /// <summary>Alle Seiten der Standardsprache: Startseite zuerst, dann in der gespeicherten Reihenfolge, dann der Rest nach Name.</summary>
    public static IReadOnlyList<string> Pages(string dir)
    {
        var s = Load(dir);
        var found = new List<string>();
        var pd = Path.Combine(dir, PagesDir);
        if (Directory.Exists(pd))
            foreach (var f in Directory.GetFiles(pd, "*" + PageExt))
            {
                var slug = Path.GetFileName(f)[..^PageExt.Length];
                if (CheckSlug(slug) is null) found.Add(slug);
            }
        var list = new List<string> { Index };
        list.AddRange(s.Reihenfolge.Where(found.Contains));
        list.AddRange(found.Where(x => !list.Contains(x)).OrderBy(x => x, StringComparer.Ordinal));
        return list.Take(MaxPages).ToList();
    }

    public static bool HasTranslation(string dir, string adresse, string sprache)
    {
        var s = Load(dir);
        return sprache == s.Standardsprache || File.Exists(FileOf(dir, adresse, sprache, s.Standardsprache));
    }

    /// <summary>Titel einer Seite aus ihren Daten.</summary>
    public static string TitleOf(string dir, string adresse, string? sprache = null)
    {
        var s = Load(dir);
        var f = FileOf(dir, adresse, sprache ?? s.Standardsprache, s.Standardsprache);
        try { return File.Exists(f) && JsonNode.Parse(File.ReadAllText(f))?["root"]?["props"]?["title"]?.ToString() is { Length: > 0 } t ? t : adresse; }
        catch (Exception e) when (e is JsonException or IOException or ArgumentException) { return adresse; }
    }

    // ---------- Seiten bearbeiten ----------

    /// <summary>Seite zum Bearbeiten wählen. Eine fehlende Übersetzung entsteht dabei als Kopie der Seite in der Standardsprache.</summary>
    public static string? Activate(string dir, string adresse, string? sprache = null)
    {
        var s = Load(dir);
        var lang = sprache ?? s.Standardsprache;
        if (!s.Sprachen.Contains(lang)) return "Diese Sprache gibt es im Projekt nicht.";
        if (adresse != Index && (CheckSlug(adresse) is not null || !File.Exists(FileOf(dir, adresse, s.Standardsprache, s.Standardsprache)))) return "Diese Seite gibt es nicht.";
        var target = FileOf(dir, adresse, lang, s.Standardsprache);
        if (!File.Exists(target))
        {
            var src = FileOf(dir, adresse, s.Standardsprache, s.Standardsprache);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            // Z5: Die Kopie gilt als nicht übersetzt, solange ihre Texte denen der Standardsprache gleichen (WebDesignI18n.Missing)
            File.WriteAllText(target, File.Exists(src) ? File.ReadAllText(src) : WebDesign.Empty(adresse));
        }
        var raw = ReadRaw(dir); raw["aktiv"] = adresse; raw["aktivSprache"] = lang; WriteRaw(dir, raw);
        return null;
    }

    /// <summary>Neue Seite anlegen (leer oder als Kopie einer vorhandenen) und zum Bearbeiten wählen.</summary>
    public static string? Create(string dir, string adresse, string titel, string? kopieVon = null)
    {
        if (CheckSlug(adresse) is { } err) return err;
        var s = Load(dir);
        if (Pages(dir).Count >= MaxPages) return $"Höchstens {MaxPages} Seiten je Projekt.";
        if (Pages(dir).Contains(adresse)) return $"Die Adresse „{adresse}“ gibt es schon.";
        if (WebDesignCms.Load(dir).Any(c => c.Id == adresse)) return $"„{adresse}“ ist schon der Name einer Sammlung.";
        var f = FileOf(dir, adresse, s.Standardsprache, s.Standardsprache);
        string content;
        if (kopieVon is not null)
        {
            if (!Pages(dir).Contains(kopieVon)) return "Die Vorlage-Seite gibt es nicht.";
            var src = FileOf(dir, kopieVon, s.Standardsprache, s.Standardsprache);
            content = File.Exists(src) && WebDesign.Validate(File.ReadAllText(src)).Json is { } ok ? ok : WebDesign.Empty(titel);
            var o = JsonNode.Parse(content)!.AsObject();
            if (o["root"]?["props"] is JsonObject rp) { rp["title"] = titel.Trim(); rp.Remove("vorlage"); }
            content = o.ToJsonString();
        }
        else content = WebDesign.Empty(titel.Trim().Length > 0 ? titel.Trim() : adresse);
        Directory.CreateDirectory(Path.GetDirectoryName(f)!);
        File.WriteAllText(f, content);
        var raw = ReadRaw(dir);
        var order = raw["reihenfolge"] as JsonArray ?? new JsonArray();
        order.Add(adresse); raw["reihenfolge"] = order; raw["aktiv"] = adresse; raw["aktivSprache"] = s.Standardsprache;
        WriteRaw(dir, raw);
        return null;
    }

    /// <summary>Seite mit allen Übersetzungen löschen. Die Startseite bleibt immer. Die Dateien wandern in den Papierkorb-Ordner des Projekts.</summary>
    public static string? Delete(string dir, string adresse)
    {
        if (adresse == Index) return "Die Startseite lässt sich nicht löschen.";
        if (!Pages(dir).Contains(adresse)) return "Diese Seite gibt es nicht.";
        var s = Load(dir);
        var bin = Path.Combine(dir, ".trendo-papierkorb", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + adresse);
        Directory.CreateDirectory(bin);
        foreach (var lang in s.Sprachen)
        {
            var f = FileOf(dir, adresse, lang, s.Standardsprache);
            if (File.Exists(f)) File.Move(f, Path.Combine(bin, lang + PageExt));
        }
        var raw = ReadRaw(dir);
        if (raw["reihenfolge"] is JsonArray order) foreach (var x in order.Where(x => Str(x) == adresse).ToList()) order.Remove(x);
        if (Str(raw["aktiv"]) == adresse) { raw["aktiv"] = Index; raw["aktivSprache"] = s.Standardsprache; }
        WriteRaw(dir, raw);
        return null;
    }

    /// <summary>Reihenfolge ändern: Seite eins nach oben oder unten (die Startseite bleibt vorn).</summary>
    public static void Move(string dir, string adresse, int richtung)
    {
        var pages = Pages(dir).Where(p => p != Index).ToList();
        var i = pages.IndexOf(adresse); var j = i + Math.Sign(richtung);
        if (i < 0 || j < 0 || j >= pages.Count) return;
        (pages[i], pages[j]) = (pages[j], pages[i]);
        var raw = ReadRaw(dir); raw["reihenfolge"] = new JsonArray(pages.Select(p => (JsonNode)p).ToArray()); WriteRaw(dir, raw);
    }

    // ---------- Sprachen (Z5) ----------

    public static string? AddLanguage(string dir, string code)
    {
        var c = code.Trim().ToLowerInvariant();
        if (!IsLanguage(c)) return "Sprachcode bitte wie „en“ oder „fr“ (zwei Buchstaben, optional „en-gb“).";
        var s = Load(dir);
        if (s.Sprachen.Contains(c)) return "Diese Sprache gibt es schon.";
        if (s.Sprachen.Count >= 12) return "Höchstens 12 Sprachen je Projekt.";
        var raw = ReadRaw(dir);
        raw["standardsprache"] = s.Standardsprache;
        raw["sprachen"] = new JsonArray(s.Sprachen.Append(c).Select(x => (JsonNode)x).ToArray());
        WriteRaw(dir, raw);
        return null;
    }

    public static string? RemoveLanguage(string dir, string code)
    {
        var s = Load(dir);
        if (code == s.Standardsprache) return "Die Standardsprache bleibt.";
        if (!s.Sprachen.Contains(code)) return "Diese Sprache gibt es nicht.";
        var langDir = Path.Combine(dir, LangDir, code);
        if (Directory.Exists(langDir))
        {
            var bin = Path.Combine(dir, ".trendo-papierkorb", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-sprache-" + code);
            Directory.CreateDirectory(Path.GetDirectoryName(bin)!);
            Directory.Move(langDir, bin);
        }
        var raw = ReadRaw(dir);
        raw["sprachen"] = new JsonArray(s.Sprachen.Where(x => x != code).Select(x => (JsonNode)x).ToArray());
        if (Str(raw["aktivSprache"]) == code) raw["aktivSprache"] = s.Standardsprache;
        WriteRaw(dir, raw);
        return null;
    }

    // ---------- Weiterleitungen (W3) ----------

    /// <summary>Quelle einer Weiterleitung: eine alte Adresse dieser Website, z. B. „/alt“ oder „alt.html“.</summary>
    public static string? NormalizeFrom(string von)
    {
        var v = von.Trim().TrimStart('/');
        if (v.EndsWith(".html", StringComparison.Ordinal)) v = v[..^5];
        var parts = v.Split('/');
        if (parts.Length is < 1 or > 2 || parts.Any(p => !SlugPattern().IsMatch(p))) return null;
        return string.Join('/', parts);
    }

    /// <summary>Ziel einer Weiterleitung: eine Seite dieser Website oder eine https-Adresse.</summary>
    public static string? NormalizeTarget(string nach)
    {
        var n = nach.Trim();
        if (n.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return Uri.TryCreate(n, UriKind.Absolute, out var u) && u.UserInfo.Length == 0 && !n.Any(char.IsWhiteSpace) ? n : null;
        if (n is "/" or "" or "index" or "index.html") return Index;
        return NormalizeFrom(n);
    }

    public static string? AddRedirect(string dir, string von, string nach)
    {
        if (NormalizeFrom(von) is not { } v) return "Die alte Adresse bitte wie „/alt“ oder „alt.html“ (Kleinbuchstaben, Ziffern, Bindestriche).";
        if (NormalizeTarget(nach) is not { } n) return "Das Ziel bitte als Seite dieser Website („ueber-uns“) oder als https-Adresse.";
        if (v == n) return "Eine Weiterleitung auf sich selbst geht nicht.";
        var s = Load(dir);
        if (s.Weiterleitungen.Any(r => r.Von == v)) return $"Für „/{v}“ gibt es schon eine Weiterleitung.";
        if (Pages(dir).Contains(v)) return $"„/{v}“ ist eine bestehende Seite. Eine Weiterleitung würde sie überdecken.";
        var raw = ReadRaw(dir);
        var arr = raw["weiterleitungen"] as JsonArray ?? new JsonArray();
        arr.Add(new JsonObject { ["von"] = v, ["nach"] = n }); raw["weiterleitungen"] = arr; WriteRaw(dir, raw);
        return null;
    }

    public static void RemoveRedirect(string dir, string von)
    {
        var raw = ReadRaw(dir);
        if (raw["weiterleitungen"] is JsonArray arr) foreach (var x in arr.Where(x => NormalizeFrom(Str(x?["von"])) == von).ToList()) arr.Remove(x);
        WriteRaw(dir, raw);
    }
}
