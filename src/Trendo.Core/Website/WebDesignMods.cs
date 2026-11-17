using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>A4 im Studio „Webseite bauen“: Mods bringen Vorlagen (Bausteingruppen), Aktionen (benannte Abläufe aus vorhandenen A1-Aktionen) oder Skills (Fachtexte für die KI) mit.
/// Mods enthalten nur Daten, keinen ausführbaren Code. Jede Mod hat eine Beschreibung mit Rechten und Lizenz (trendo-webmod.json), wird nur nach Zustimmung installiert
/// und liegt danach nur im Projektordner (mods/&lt;id&gt;/). Die KI sieht Mod-Aktionen als weitere A1-Aktionen; ausgeführt werden sie als die vorhandenen, geprüften Aktionen.</summary>
public static partial class WebDesignMods
{
    public const string ManifestName = "trendo-webmod.json", Folder = "mods";
    public const int MaxFiles = 200, MaxSkillChars = 4000, MaxSteps = 60;
    public const long MaxBytes = 20 * 1024 * 1024, MaxFileBytes = 5 * 1024 * 1024;
    public static readonly string[] Rights = ["vorlagen", "aktionen", "skills"];
    public static readonly Dictionary<string, string> RightNames = new() { ["vorlagen"] = "bringt Vorlagen (Bausteingruppen) mit", ["aktionen"] = "bringt Aktionen für die KI und dich mit", ["skills"] = "gibt der KI Fachwissen mit" };
    /// <summary>Freie Lizenzen, unter denen Mods weitergegeben werden dürfen.</summary>
    public static readonly HashSet<string> Licenses = ["MIT", "Apache-2.0", "BSD-2-Clause", "BSD-3-Clause", "ISC", "MPL-2.0", "CC0-1.0", "CC-BY-4.0", "Unlicense", "0BSD"];
    static readonly HashSet<string> DataTypes = [".json", ".md", ".txt"];
    static readonly HashSet<string> ImageTypes = [".png", ".jpg", ".jpeg", ".webp"];

    [GeneratedRegex("^[a-z0-9]+(?:[.-][a-z0-9]+){0,5}$")] private static partial Regex IdPattern();
    [GeneratedRegex("^[a-z][a-z0-9_]{0,29}$")] private static partial Regex NamePattern();
    [GeneratedRegex(@"^\d+\.\d+(\.\d+)?$")] private static partial Regex VersionPattern();

    public sealed record Vorlage(string Name, string Beschreibung, JsonArray Inhalt);
    public sealed record Aktion(string Name, string Beschreibung, IReadOnlyList<string> Parameter, JsonArray Schritte);
    public sealed record Mod(string Id, string Name, string Version, string Beschreibung, string Lizenz, string Autor, IReadOnlyList<string> Rechte,
        IReadOnlyList<Vorlage> Vorlagen, IReadOnlyList<Aktion> Aktionen, IReadOnlyDictionary<string, string> Skills, IReadOnlyList<string> Dateien, long Bytes);
    /// <param name="Extracted">true nur für den eigenen Entpack-Ordner einer ZIP-Datei; nur der darf nach der Installation weg.</param>
    public sealed record Prepared(Mod? Mod, string? Error, string Fingerprint, string? Folder, bool Extracted = false);

    static string Str(JsonNode? n) => n is JsonValue v && v.TryGetValue(out string? s) ? s : "";

    // ---------- Prüfen ----------

    /// <summary>Mod aus einem Ordner prüfen (nur lesen). Abgelehnt wird: ohne Beschreibung, Rechte oder freie Lizenz; andere Dateien als Daten und Bilder;
    /// Verknüpfungen; Inhalte ohne passendes Recht; Vorlagen, die nicht gültig sind; Aktionen, die etwas anderes als vorhandene Aktionen ausführen.</summary>
    public static (Mod? Mod, string? Error) Check(string folder)
    {
        if (!Directory.Exists(folder)) return (null, "Diesen Ordner gibt es nicht.");
        if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) return (null, "Verknüpfte Ordner werden nicht installiert.");
        var mf = Path.Combine(folder, ManifestName);
        if (!File.Exists(mf)) return (null, $"Keine Mod: {ManifestName} fehlt.");
        JsonObject m;
        try { m = JsonNode.Parse(File.ReadAllText(mf)) as JsonObject ?? throw new JsonException(); }
        catch (Exception e) when (e is JsonException or ArgumentException) { return (null, $"{ManifestName} ist kein gültiges JSON."); }
        var id = Str(m["id"]); var name = Str(m["name"]).Trim(); var version = Str(m["version"]); var desc = Str(m["beschreibung"]).Trim(); var lic = Str(m["lizenz"]); var author = Str(m["autor"]).Trim();
        if (!IdPattern().IsMatch(id) || id.Length > 60) return (null, "Die Mod braucht eine id aus Kleinbuchstaben, Ziffern, Punkten und Bindestrichen.");
        if (name.Length is 0 or > 60) return (null, "Die Mod braucht einen Namen.");
        if (!VersionPattern().IsMatch(version)) return (null, "Die Mod braucht eine Version wie 1.0.0.");
        if (desc.Length < 10 || desc.Length > 1000) return (null, "Die Mod braucht eine Beschreibung (10 bis 1000 Zeichen).");
        if (!Licenses.Contains(lic)) return (null, $"Lizenz fehlt oder ist nicht frei. Erlaubt: {string.Join(", ", Licenses.Order())}.");
        if (m["rechte"] is not JsonArray ra || ra.Count == 0) return (null, "Die Mod muss sagen, welche Rechte sie braucht (rechte: vorlagen, aktionen, skills).");
        var rights = ra.Select(x => Str(x)).Distinct().ToList();
        if (rights.FirstOrDefault(r => !Rights.Contains(r)) is { } bad) return (null, $"Unbekanntes Recht „{bad}“. Möglich: {string.Join(", ", Rights)}.");

        // Dateien: nur Daten und Bilder, keine Verknüpfungen, nur in bekannten Unterordnern
        var files = new List<string>(); long bytes = 0;
        foreach (var f in Directory.EnumerateFileSystemEntries(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 }))
        {
            if ((File.GetAttributes(f) & FileAttributes.ReparsePoint) != 0) return (null, $"Verknüpfung „{Path.GetRelativePath(folder, f)}“ ist in einer Mod nicht erlaubt.");
            if (Directory.Exists(f)) continue;
            var rel = Path.GetRelativePath(folder, f).Replace(Path.DirectorySeparatorChar, '/');
            var ext = Path.GetExtension(f).ToLowerInvariant();
            var top = rel.Contains('/') ? rel[..rel.IndexOf('/')] : "";
            if (rel == ManifestName || rel is "LICENSE" or "LICENSE.txt" or "LIZENZ.txt" or "README.md") { }
            else if (top is "vorlagen" or "aktionen" && ext == ".json" && rel.Count(c => c == '/') == 1) { }
            else if (top == "skills" && ext is ".md" or ".txt" && rel.Count(c => c == '/') == 1) { }
            else if (top == "bilder" && ImageTypes.Contains(ext)) { }
            else return (null, $"„{rel}“ ist in einer Mod nicht erlaubt. Mods enthalten nur Daten (JSON, Text) und Bilder, keinen Programmcode.");
            if (Path.GetFileName(f).StartsWith('.')) return (null, $"Versteckte Datei „{rel}“ ist nicht erlaubt.");
            var len = new FileInfo(f).Length; bytes += len;
            if (len > MaxFileBytes) return (null, $"„{rel}“ ist zu groß (über 5 MB).");
            files.Add(rel);
        }
        if (files.Count > MaxFiles) return (null, $"Zu viele Dateien (höchstens {MaxFiles}).");
        if (bytes > MaxBytes) return (null, "Die Mod ist zu groß (über 20 MB).");
        foreach (var (dirName, right) in new[] { ("vorlagen", "vorlagen"), ("aktionen", "aktionen"), ("skills", "skills") })
            if (files.Any(f => f.StartsWith(dirName + "/", StringComparison.Ordinal)) && !rights.Contains(right)) return (null, $"Die Mod bringt {dirName} mit, nennt aber das Recht „{right}“ nicht.");

        var vorlagen = new List<Vorlage>(); var aktionen = new List<Aktion>(); var skills = new Dictionary<string, string>();
        foreach (var f in files.Where(f => f.StartsWith("vorlagen/", StringComparison.Ordinal)))
        {
            var (v, err) = ReadTemplate(folder, f, id); if (v is null) return (null, $"{f}: {err}");
            vorlagen.Add(v);
        }
        foreach (var f in files.Where(f => f.StartsWith("aktionen/", StringComparison.Ordinal)))
        {
            var (a, err) = ReadAction(folder, f); if (a is null) return (null, $"{f}: {err}");
            if (aktionen.Any(x => x.Name == a.Name)) return (null, $"Aktion „{a.Name}“ steht doppelt.");
            aktionen.Add(a);
        }
        foreach (var f in files.Where(f => f.StartsWith("skills/", StringComparison.Ordinal)))
        {
            var text = File.ReadAllText(Path.Combine(folder, f.Replace('/', Path.DirectorySeparatorChar)));
            if (text.Length > MaxSkillChars) return (null, $"{f}: Skill ist zu lang (höchstens {MaxSkillChars} Zeichen).");
            skills[Path.GetFileNameWithoutExtension(f)] = text;
        }
        if (vorlagen.Count + aktionen.Count + skills.Count == 0) return (null, "Die Mod bringt nichts mit (keine Vorlagen, Aktionen oder Skills).");
        return (new(id, name, version, desc, lic, author, rights, vorlagen, aktionen, skills, files, bytes), null);
    }

    static (Vorlage? V, string? Error) ReadTemplate(string folder, string rel, string modId)
    {
        try
        {
            if (JsonNode.Parse(File.ReadAllText(Path.Combine(folder, rel.Replace('/', Path.DirectorySeparatorChar)))) is not JsonObject o) return (null, "kein JSON-Objekt.");
            var name = Str(o["name"]).Trim(); if (name.Length is 0 or > 60) return (null, "Vorlage ohne Namen.");
            if (o["inhalt"] is not JsonArray inhalt || inhalt.Count == 0) return (null, "Vorlage ohne Inhalt.");
            // Bilder der Vorlage müssen in der Mod liegen; im Projekt heißen sie dann mods/<id>/bilder/…
            string? bad = null;
            void Walk(JsonNode? n)
            {
                switch (n)
                {
                    case JsonObject x:
                        foreach (var key in x.Select(kv => kv.Key).ToList())
                            if (key is "src" or "bild" && x[key] is JsonValue v && v.TryGetValue(out string? s) && s.Length > 0)
                            {
                                var clean = s.Replace('\\', '/');
                                if (!clean.StartsWith("bilder/", StringComparison.Ordinal) || clean.Contains("..") || !File.Exists(Path.Combine(folder, clean.Replace('/', Path.DirectorySeparatorChar)))) { bad = s; continue; }
                                x[key] = $"{Folder}/{modId}/{clean}";
                            }
                            else Walk(x[key]);
                        break;
                    case JsonArray a: foreach (var y in a) Walk(y); break;
                }
            }
            var copy = inhalt.DeepClone().AsArray(); Walk(copy);
            if (bad is not null) return (null, $"Bild „{bad}“ liegt nicht im Ordner bilder/ der Mod.");
            var page = new JsonObject { ["root"] = new JsonObject { ["props"] = new JsonObject() }, ["content"] = copy, ["zones"] = new JsonObject() };
            if (WebDesign.Validate(page.ToJsonString()) is { Json: null, Error: var e }) return (null, e);
            if (BlockTypes(copy).Any(t => !WebDesignActions.ComponentNames.Contains(t) || t is "Html" or "Komponente")) return (null, "Vorlage nutzt einen unbekannten oder nicht erlaubten Baustein.");
            return (new(name, Str(o["beschreibung"]).Trim(), copy), null);
        }
        catch (Exception e) when (e is JsonException or ArgumentException) { return (null, "kein gültiges JSON."); }
    }

    /// <summary>Typen aller Bausteine in jeder Tiefe (auch in Slots von Bereichen und Spalten).</summary>
    static IEnumerable<string> BlockTypes(JsonNode? n)
    {
        switch (n)
        {
            case JsonArray a: foreach (var x in a) foreach (var t in BlockTypes(x)) yield return t; break;
            case JsonObject o:
                if (o.ContainsKey("type") || o["props"] is JsonObject) yield return Str(o["type"]);
                foreach (var kv in o) if (kv.Key != "type") foreach (var t in BlockTypes(kv.Value)) yield return t;
                break;
        }
    }

    static (Aktion? A, string? Error) ReadAction(string folder, string rel)
    {
        try
        {
            if (JsonNode.Parse(File.ReadAllText(Path.Combine(folder, rel.Replace('/', Path.DirectorySeparatorChar)))) is not JsonObject o) return (null, "kein JSON-Objekt.");
            var name = Str(o["name"]); if (!NamePattern().IsMatch(name)) return (null, "Aktion braucht einen Namen aus Kleinbuchstaben, Ziffern und _.");
            var desc = Str(o["beschreibung"]).Trim(); if (desc.Length < 5) return (null, "Aktion braucht eine Beschreibung.");
            var pars = (o["parameter"] as JsonArray ?? []).Select(x => Str(x)).ToList();
            if (pars.Any(p => !NamePattern().IsMatch(p)) || pars.Count > 10) return (null, "Parameter nur als kurze Namen (höchstens 10).");
            if (o["schritte"] is not JsonArray steps || steps.Count is 0 or > MaxSteps) return (null, $"Aktion braucht 1 bis {MaxSteps} Schritte.");
            var known = WebDesignActions.Catalog.Select(a => a.Name).ToHashSet();
            foreach (var s in steps)
            {
                var an = Str(s?["aktion"]);
                if (!known.Contains(an)) return (null, $"Schritt „{an}“ ist keine vorhandene Aktion. Mods führen nur die geprüften Aktionen von Trendo aus.");
            }
            return (new(name, desc, pars, steps.DeepClone().AsArray()), null);
        }
        catch (Exception e) when (e is JsonException or ArgumentException) { return (null, "kein gültiges JSON."); }
    }

    // ---------- Vorbereiten und Installieren ----------

    /// <summary>Quelle (Ordner oder .zip auf diesem PC) prüfen und für die Zustimmung zusammenfassen. Eine Internet-Adresse wird abgelehnt.</summary>
    public static Prepared Prepare(string source)
    {
        var p = (source ?? "").Trim().Trim('"');
        if (p.Contains("://", StringComparison.Ordinal) || p.StartsWith(@"\\", StringComparison.Ordinal) || p.StartsWith("//", StringComparison.Ordinal)) return new(null, "Mods nur aus einem Ordner oder einer ZIP-Datei auf diesem PC, nicht aus dem Internet.", "", null);
        if (!Path.IsPathFullyQualified(p)) return new(null, "Bitte den vollständigen Pfad angeben.", "", null);
        if (WebDesignImport.LocalPathError(p) is { } lerr) return new(null, lerr, "", null);
        string folder;
        if (File.Exists(p) && Path.GetExtension(p).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            folder = Path.Combine(Path.GetTempPath(), "trendo-mod-" + Guid.NewGuid().ToString("N"));
            if (Unzip(p, folder) is { } zerr) { Cleanup(new(null, null, "", folder, true)); return new(null, zerr, "", null); }
            // „Senden an › ZIP-komprimierter Ordner“ legt alles in einen Hauptordner: den als Mod-Wurzel nehmen
            if (!File.Exists(Path.Combine(folder, ManifestName)) && Directory.GetFiles(folder).Length == 0 && Directory.GetDirectories(folder) is [var only])
            {
                var flat = folder + "-x";
                try { Directory.Move(only, flat); Directory.Delete(folder); Directory.Move(flat, folder); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    foreach (var d in new[] { folder, flat }) try { if (Directory.Exists(d)) Directory.Delete(d, true); } catch (Exception e2) when (e2 is IOException or UnauthorizedAccessException) { }
                    return new(null, "Die ZIP-Datei ließ sich nicht entpacken (Dateien gesperrt, z. B. vom Virenschutz). Bitte noch einmal versuchen.", "", null);
                }
            }
        }
        else if (Directory.Exists(p)) folder = p;
        else return new(null, "Bitte einen Mod-Ordner oder eine ZIP-Datei wählen.", "", null);
        var (mod, err) = Check(folder);
        var extracted = folder != p;
        return mod is null ? new(null, err, "", folder, extracted) : new(mod, null, Fingerprint(folder, mod), folder, extracted);
    }

    /// <summary>ZIP entpacken: kein Weg nach oben, keine absoluten Pfade, Größen- und Anzahlgrenzen.</summary>
    static string? Unzip(string zip, string target)
    {
        try
        {
            using var z = ZipFile.OpenRead(zip);
            if (z.Entries.Count > MaxFiles * 2) return "Die ZIP-Datei hat zu viele Einträge.";
            long total = 0;
            var root = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (var e in z.Entries)
            {
                if (e.FullName.EndsWith('/')) continue;
                var dest = Path.GetFullPath(Path.Combine(target, e.FullName.Replace('/', Path.DirectorySeparatorChar)));
                if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase) || e.FullName.Contains(':') || Path.IsPathRooted(e.FullName)) return $"„{e.FullName}“ zeigt aus der Mod heraus und wird nicht entpackt.";
                total += e.Length; if (e.Length > MaxFileBytes || total > MaxBytes) return "Die ZIP-Datei ist zu groß.";
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                e.ExtractToFile(dest, false);
            }
            return null;
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException) { return "Die ZIP-Datei ließ sich nicht lesen: " + e.Message; }
    }

    static string Fingerprint(string folder, Mod mod)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var f in mod.Dateien.Order(StringComparer.Ordinal)) { sha.AppendData(Encoding.UTF8.GetBytes(f + "\n")); sha.AppendData(SHA256.HashData(File.ReadAllBytes(Path.Combine(folder, f.Replace('/', Path.DirectorySeparatorChar))))); }
        return Convert.ToHexString(sha.GetHashAndReset());
    }

    /// <summary>Installieren nur mit Zustimmung für genau diese geprüfte Mod: die Dateien kommen nach mods/&lt;id&gt;/ im Projekt, nirgends sonst hin.</summary>
    public static string? Install(string projectDir, Prepared prepared, string? consentedFingerprint)
    {
        if (prepared.Mod is not { } mod || prepared.Folder is null) return prepared.Error ?? "Erst prüfen.";
        if (consentedFingerprint != prepared.Fingerprint) return "Nicht installiert: Für genau diese Mod fehlt deine Zustimmung.";
        var (again, err) = Check(prepared.Folder);
        if (again is null) return err;
        if (Fingerprint(prepared.Folder, again) != prepared.Fingerprint) return "Nicht installiert: Die Mod hat sich nach der Prüfung geändert. Bitte neu prüfen.";
        var target = Path.Combine(projectDir, Folder, mod.Id);
        var full = Path.GetFullPath(target);
        if (!full.StartsWith(Path.GetFullPath(Path.Combine(projectDir, Folder)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return "Ungültige Mod-id.";
        var source = Path.GetFullPath(prepared.Folder).TrimEnd(Path.DirectorySeparatorChar);
        if (source.Equals(full, StringComparison.OrdinalIgnoreCase) || source.StartsWith(full + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || full.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return "Diese Mod liegt schon im Projekt. Zum Erneuern bitte die neue Fassung aus einem anderen Ordner oder einer ZIP-Datei wählen.";
        // erst vollständig neben das Ziel kopieren, dann austauschen: Bei einem Fehler bleibt die alte Fassung erhalten
        var fresh = Path.Combine(projectDir, Folder, ".neu-" + mod.Id); var old = Path.Combine(projectDir, Folder, ".alt-" + mod.Id);
        try
        {
            foreach (var d in new[] { fresh, old }) if (Directory.Exists(d)) Directory.Delete(d, true);
            foreach (var f in mod.Dateien)
            {
                var dest = Path.Combine(fresh, f.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(Path.Combine(prepared.Folder, f.Replace('/', Path.DirectorySeparatorChar)), dest);
            }
            if (Directory.Exists(target)) Directory.Move(target, old);
            Directory.Move(fresh, target);
            if (Directory.Exists(old)) try { Directory.Delete(old, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            try { if (Directory.Exists(fresh)) Directory.Delete(fresh, true); if (!Directory.Exists(target) && Directory.Exists(old)) Directory.Move(old, target); } catch (Exception e2) when (e2 is IOException or UnauthorizedAccessException) { }
            return "Die Mod ließ sich nicht installieren (Datei gesperrt oder kein Schreibrecht). Die bisherige Fassung bleibt erhalten.";
        }
        return null;
    }

    public static string? Uninstall(string projectDir, string id)
    {
        if (!IdPattern().IsMatch(id)) return "Ungültige Mod-id.";
        var target = Path.Combine(projectDir, Folder, id);
        if (!Directory.Exists(target)) return "Diese Mod ist nicht installiert.";
        Directory.Delete(target, true); return null;
    }

    /// <summary>Installierte Mods des Projekts (jede wird beim Lesen neu geprüft; Ungültiges wird ausgelassen).</summary>
    public static IReadOnlyList<Mod> Installed(string projectDir)
    {
        var d = Path.Combine(projectDir, Folder);
        if (!Directory.Exists(d)) return [];
        var list = new List<Mod>();
        foreach (var sub in Directory.GetDirectories(d).Order(StringComparer.Ordinal))
            if (Check(sub) is { Mod: { } m } && m.Id == Path.GetFileName(sub)) list.Add(m);
        return list;
    }

    /// <summary>Entpack-Ordner einer ZIP-Datei aufräumen. Ein Mod-Ordner der Person wird nie gelöscht.</summary>
    public static void Cleanup(Prepared? p) { if (p is { Extracted: true, Folder: { } f } && Path.GetFileName(f).StartsWith("trendo-mod-", StringComparison.Ordinal) && f.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase)) TempDir.Delete(f); }

    // ---------- Nutzen ----------

    /// <summary>Vorlage einer Mod an die Seite anhängen. Bausteine bekommen neue ids, damit nichts mit der Seite kollidiert.</summary>
    public static (string? Json, string? Error) InsertTemplate(string pageJson, Mod mod, string templateName)
    {
        var t = mod.Vorlagen.FirstOrDefault(v => v.Name == templateName); if (t is null) return (null, "Diese Vorlage gibt es nicht.");
        if (JsonNode.Parse(pageJson) is not JsonObject doc || doc["content"] is not JsonArray content) return (null, "Die Seite lässt sich nicht lesen.");
        var n = 0; var prefix = mod.Id.Replace('.', '-') + "-" + Guid.NewGuid().ToString("N")[..4];
        void Renew(JsonNode? x)
        {
            switch (x)
            {
                case JsonObject o:
                    if (o["type"] is not null && o["props"] is JsonObject p) p["id"] = $"{prefix}-{++n}";
                    foreach (var kv in o.ToList()) Renew(kv.Value);
                    break;
                case JsonArray a: foreach (var y in a) Renew(y); break;
            }
        }
        var blocks = t.Inhalt.DeepClone().AsArray(); Renew(blocks);
        foreach (var b in blocks.ToList()) { blocks.Remove(b); content.Add(b); }
        return WebDesign.Validate(doc.ToJsonString());
    }

    /// <summary>Mod-Aktionen für die KI beschreiben: Name „mod:id/name“, Zweck und Parameter.</summary>
    public static string CatalogText(IReadOnlyList<Mod> mods) => string.Join("\n", mods.SelectMany(m => m.Aktionen.Select(a =>
        $"- mod:{m.Id}/{a.Name} (aus der Mod „{m.Name}“): {a.Beschreibung} Angaben: {(a.Parameter.Count == 0 ? "keine" : string.Join(", ", a.Parameter))} (in \"werte\", Text oder Zahl; Ja/Nein als \"ja\"/\"nein\")."))) ;

    /// <summary>Skills der Mods für die KI, gekürzt auf <paramref name="maxChars"/>.</summary>
    public static string SkillsText(IReadOnlyList<Mod> mods, int maxChars)
    {
        var text = string.Join("\n\n", mods.SelectMany(m => m.Skills.Select(s => $"[{m.Name} › {s.Key}]\n{s.Value.Trim()}")));
        return text.Length > maxChars ? text[..maxChars] : text;
    }

    /// <summary>Plan der KI (oder eines Menschen): Mod-Aktionen „mod:id/name“ durch ihre Schritte ersetzen. Werte werden nur als Text in „{{parameter}}“ eingesetzt.
    /// Danach prüft <see cref="WebDesignActions.Apply(string, string, WebPlanScope?)"/> jeden Schritt wie jede andere Aktion; eine unbekannte Mod-Aktion wird abgelehnt.</summary>
    public static (string? Plan, string? Error) Expand(string? answer, IReadOnlyList<Mod> mods)
    {
        var (plan, err) = WebDesignActions.Parse(answer);
        if (plan is null) return (answer, null); // Fehler meldet Apply wie bisher
        if (plan["aktionen"] is not JsonArray list || !list.Any(a => Str(a?["aktion"]).StartsWith("mod:", StringComparison.Ordinal))) return (plan.ToJsonString(), null);
        var result = new JsonArray();
        foreach (var step in list)
        {
            var name = Str(step?["aktion"]);
            if (!name.StartsWith("mod:", StringComparison.Ordinal)) { result.Add(step!.DeepClone()); continue; }
            var parts = name[4..].Split('/', 2);
            var mod = mods.FirstOrDefault(m => m.Id == parts[0]);
            var act = parts.Length == 2 ? mod?.Aktionen.FirstOrDefault(a => a.Name == parts[1]) : null;
            if (act is null) return (null, $"Die Mod-Aktion „{name}“ gibt es in diesem Projekt nicht.");
            var werte = step?["werte"] as JsonObject ?? new JsonObject();
            // Zahlen und Ja/Nein kommen als Text in die Schritte; Listen oder Objekte passen in keinen Platzhalter
            if (act.Parameter.FirstOrDefault(p => werte[p] is JsonArray or JsonObject) is { } bad) return (null, $"Die Angabe „{bad}“ für „{name}“ muss Text oder eine Zahl sein.");
            var map = act.Parameter.ToDictionary(p => p, p => ValueText(werte[p]));
            foreach (var s in act.Schritte)
            {
                var copy = s!.DeepClone();
                Fill(copy, map);
                result.Add(copy);
            }
            if (result.Count > 200) return (null, "Der Plan ist mit den Mod-Aktionen zu lang.");
        }
        return (new JsonObject { ["aktionen"] = result }.ToJsonString(), null);
    }

    /// <summary>Wert einer Mod-Angabe als Text: Text bleibt, Zahlen ohne Ländereinstellung (3.5 statt 3,5), Ja/Nein als „ja“/„nein“ wie in allen Bausteinen.</summary>
    static string ValueText(JsonNode? n) => n switch
    {
        JsonValue v when v.TryGetValue(out string? s) => s,
        JsonValue v when v.TryGetValue(out bool b) => b ? "ja" : "nein",
        JsonValue v when v.TryGetValue(out decimal d) => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
        JsonValue v when v.TryGetValue(out double x) => x.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        _ => "",
    };

    static void Fill(JsonNode? n, IReadOnlyDictionary<string, string> map)
    {
        switch (n)
        {
            case JsonObject o:
                foreach (var key in o.Select(kv => kv.Key).ToList())
                    if (o[key] is JsonValue v && v.TryGetValue(out string? s) && s.Contains("{{")) o[key] = Regex.Replace(s, @"\{\{\s*([a-z][a-z0-9_]{0,29})\s*\}\}", m => map.TryGetValue(m.Groups[1].Value, out var x) ? x : "");
                    else Fill(o[key], map);
                break;
            case JsonArray a: foreach (var x in a) Fill(x, map); break;
        }
    }
}
