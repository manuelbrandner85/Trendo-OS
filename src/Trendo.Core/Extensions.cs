using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>X-04: Erweiterungen – Daten (Vorlagen, Listen, Links, Texte) und seit T-0766 Funktions-Erweiterungen („modul“): ein WebAssembly-Modul,
/// das nur in der Sandbox (<see cref="ExtensionSandbox"/>) läuft – nie als .NET-Code im Trendo-Prozess. Installieren aus Ordner oder ZIP, prüfen, ein-/ausschalten, entfernen.</summary>
public sealed record ExtManifest(string Id, string Name, string Version, string Author, string Description, string Kind, string[] Permissions, string Entry, string License,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] ExtCommand[]? Commands = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? ModuleSha256 = null);
/// <summary>T-0766: Befehl einer Funktions-Erweiterung – ein Knopf im Studio „Projekte“: nimmt eine Projektdatei mit einer der Endungen <paramref name="Input"/>
/// und legt das Ergebnis als neue Datei mit <paramref name="Output"/> ins offene Projekt.</summary>
public sealed record ExtCommand(string Id, string Label, string[] Input, string Output);
public sealed record InstalledExt(ExtManifest Manifest, string Dir, bool Enabled, string Sha256);

public static partial class Extensions
{
    public const string ManifestFile = "trendo-extension.json";
    /// <summary>seite = Baukasten-Vorlage (JSON), vorhaben = Schrittliste (Markdown), links = Linkliste (JSON), text = Textbausteine (Markdown),
    /// modul = Funktions-Erweiterung (WebAssembly in der Sandbox, T-0766).</summary>
    public static readonly string[] Kinds = ["seite", "vorhaben", "links", "text", ModuleKind];
    public const string ModuleKind = "modul";
    /// <summary>Endungen, die ein Befehl als Ergebnis anlegen darf – nur Text, nie ausführbare Dateien.</summary>
    public static readonly string[] OutputExt = [".md", ".txt", ".csv", ".tsv", ".json"];
    public static readonly string[] Permissions = ["dateien-lesen", "internet-links"];
    static readonly string[] AllowedExt = [".json", ".md", ".txt", ".png", ".jpg", ".jpeg", ".svg", ".webp", ".css"];

    [GeneratedRegex(@"^[a-z0-9][a-z0-9\-\.]{2,63}$")] private static partial Regex IdRx();
    [GeneratedRegex(@"^\d+\.\d+\.\d+$")] private static partial Regex VerRx();

    public static (ExtManifest? M, List<string> Errors) Validate(string dir)
    {
        var err = new List<string>();
        var mf = Path.Combine(dir, ManifestFile);
        if (!File.Exists(mf)) return (null, ["Keine Datei " + ManifestFile + " gefunden."]);
        ExtManifest? m;
        try { m = JsonSerializer.Deserialize<ExtManifest>(File.ReadAllText(mf), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException e) { return (null, ["Beschreibung ist kein gültiges JSON: " + e.Message]); }
        if (m is null) return (null, ["Beschreibung ist leer."]);
        if (m.Id is null || !IdRx().IsMatch(m.Id)) err.Add("Kennung ungültig (nur a–z, 0–9, - und .).");
        if (string.IsNullOrWhiteSpace(m.Name)) err.Add("Name fehlt.");
        if (m.Version is null || !VerRx().IsMatch(m.Version)) err.Add("Version muss wie 1.0.0 aussehen.");
        if (!Kinds.Contains(m.Kind)) err.Add("Unbekannte Art: " + m.Kind);
        var module = m.Kind == ModuleKind;
        foreach (var p in m.Permissions ?? [])
            if (!(module ? ExtensionRights.All.Contains(p) : Permissions.Contains(p)))
                err.Add(p is "netz" or "internet" or "netzwerk" ? "Nicht erlaubtes Recht: " + p + " – Internet gibt es für Erweiterungen nicht." : "Nicht erlaubtes Recht: " + p);
        if (m.Entry is null || !FreeAssets.SafeRel(m.Entry) || !File.Exists(Path.Combine(dir, m.Entry))) err.Add("Einstiegsdatei fehlt.");
        if (module) err.AddRange(ValidateModule(dir, m));
        else if (m.Commands is { Length: > 0 }) err.Add("Befehle gibt es nur bei Funktions-Erweiterungen (Art „modul“).");
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(f).ToLowerInvariant();
            var rel = Path.GetRelativePath(dir, f).Replace('\\', '/');
            // T-0766: bei „modul“ ist genau die Einstiegsdatei (.wasm) erlaubt und WebAssembly-Quelltext (.wat) zum Nachlesen – sonst wie bisher nur Daten
            var moduleFile = module && (ext == ".wat" || (ext == ".wasm" && rel == m.Entry?.Replace('\\', '/')));
            if (Path.GetFileName(f) is not (ManifestFile or ".aus") && !AllowedExt.Contains(ext) && !moduleFile) err.Add($"Datei nicht erlaubt (kein Programmcode): {Path.GetRelativePath(dir, f)}");
            if (new FileInfo(f).LinkTarget is not null) err.Add("Verknüpfungen sind nicht erlaubt: " + Path.GetFileName(f));
        }
        if (err.Count == 0 && m.Kind is "seite" or "links")
            try { JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, m.Entry!))).Dispose(); } catch (JsonException) { err.Add("Einstiegsdatei ist kein gültiges JSON."); }
        if (TouchesBrand(dir)) err.Add("Name und Logo von Trendo OS lassen sich nicht ändern – alles andere darfst du frei erweitern.");

        return (m, err);
    }

    [GeneratedRegex(@"^[a-z0-9][a-z0-9\-]{0,31}$")] private static partial Regex CmdRx();
    [GeneratedRegex(@"^\.[a-z0-9]{1,8}$")] private static partial Regex ExtRx();

    /// <summary>T-0766: Prüfregeln für Funktions-Erweiterungen (ohne das Modul auszuführen; die Sandbox prüft Importe beim Installieren und vor jedem Lauf).</summary>
    static IEnumerable<string> ValidateModule(string dir, ExtManifest m)
    {
        if (m.Entry is null || !m.Entry.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase)) { yield return "Einstiegsdatei einer Funktions-Erweiterung muss ein WebAssembly-Modul (.wasm) sein."; yield break; }
        var wasm = Path.Combine(dir, m.Entry);
        if (!File.Exists(wasm)) yield break;
        if (m.ModuleSha256 is null || m.ModuleSha256.Length != 64) yield return "Prüfsumme des Moduls (ModuleSha256) fehlt.";
        else if (!string.Equals(ExtensionCatalog.FileSha256(wasm), m.ModuleSha256, StringComparison.OrdinalIgnoreCase)) yield return "Prüfsumme des Moduls stimmt nicht – die Datei wurde verändert. Nicht installiert.";
        if (m.Commands is not { Length: > 0 }) { yield return "Eine Funktions-Erweiterung braucht mindestens einen Befehl."; yield break; }
        var perms = m.Permissions ?? [];
        if (!perms.Contains(ExtensionRights.Command)) yield return "Für Befehle fehlt das Recht „befehl“.";
        var ids = new HashSet<string>();
        foreach (var c in m.Commands)
        {
            if (c is null) { yield return "Leerer Befehl."; continue; }
            if (c.Id is null || !CmdRx().IsMatch(c.Id) || !ids.Add(c.Id)) yield return "Befehl hat keine gültige, eindeutige Kennung.";
            if (string.IsNullOrWhiteSpace(c.Label) || c.Label.Length > 60) yield return "Befehl braucht einen kurzen Namen (höchstens 60 Zeichen).";
            if (c.Input is not { Length: > 0 } || c.Input.Any(x => x is null || !ExtRx().IsMatch(x))) yield return $"Befehl „{c.Label}“: Eingabe-Endungen fehlen (z. B. .csv).";
            else if (!perms.Contains(ExtensionRights.InputFile)) yield return "Für die Eingabedatei fehlt das Recht „eingabe-datei“.";
            if (c.Output is null || !OutputExt.Contains(c.Output)) yield return $"Befehl „{c.Label}“: Ergebnis-Endung nicht erlaubt (nur {string.Join(", ", OutputExt)}).";
            else if (!perms.Contains(ExtensionRights.ProjectWrite)) yield return "Für das Ergebnis im Projekt fehlt das Recht „projekt-schreiben“.";
        }
    }

    /// <summary>T-0766: Versionen vergleichen (1.2.10 &gt; 1.2.9). Ungültige Teile gelten als 0.</summary>
    public static int CompareVersion(string a, string b)
    {
        static int[] P(string v) => (v ?? "").Split('.').Select(x => int.TryParse(x, out var n) ? n : 0).Concat([0, 0, 0]).Take(3).ToArray();
        var (x, y) = (P(a), P(b));
        for (var i = 0; i < 3; i++) if (x[i] != y[i]) return x[i].CompareTo(y[i]);
        return 0;
    }

    /// <summary>Wunsch 02.11.2026: Jeder darf Trendo erweitern, nur Name und Logo bleiben fest. Erweiterungen, die Produktname, Marke oder Logo setzen wollen, werden abgelehnt.</summary>
    public static bool TouchesBrand(string dir)
    {
        foreach (var f in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                using var d = JsonDocument.Parse(File.ReadAllText(f));
                if (HasBrandKey(d.RootElement)) return true;
            }
            catch (JsonException) { }
        }
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            if (Path.GetFileNameWithoutExtension(f).ToLowerInvariant() is "trendo-logo" or "logo-trendo" or "trendo-emblem" or "trendo") return true;
        return false;
        static bool HasBrandKey(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.Object => e.EnumerateObject().Any(p => p.Name.ToLowerInvariant() is "productname" or "systemname" or "brand" or "brandname" or "osname" or "logo" or "applogo" || HasBrandKey(p.Value)),
            JsonValueKind.Array => e.EnumerateArray().Any(HasBrandKey),
            _ => false,
        };
    }

    public static string Hash(string dir)
    {
        using var sha = SHA256.Create();
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Where(f => Path.GetFileName(f) != ".aus").OrderBy(f => Path.GetRelativePath(dir, f), StringComparer.Ordinal))
        {
            var name = System.Text.Encoding.UTF8.GetBytes(Path.GetRelativePath(dir, f).Replace('\\', '/'));
            sha.TransformBlock(name, 0, name.Length, null, 0);
            var b = File.ReadAllBytes(f); sha.TransformBlock(b, 0, b.Length, null, 0);
        }
        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }
}

/// <summary>X-04, T-0241: Mit <paramref name="gate"/> brauchen Installieren und Entfernen eine Freigabe (ApprovalGate, Not-Stopp sperrt);
/// Ziel der Freigabe ist die Prüfsumme der Quelle bzw. die Kennung – wird die Datei danach verändert, wird nichts installiert.</summary>
public sealed partial class ExtensionStore(string dataRoot, AuditLog? audit = null, ApprovalGate? gate = null)
{
    public string Root { get; } = Path.Combine(dataRoot, "erweiterungen");
    public string DataRoot => dataRoot;
    AuditLog? Audit => audit;
    ApprovalGate? Gate => gate;
    public const string InstallAction = "ext.install";
    public const string RemoveAction = "ext.remove";
    // Grenzen gegen „ZIP-Bomben“ und unsaubere Pakete
    public const int MaxEntries = 2000;
    public const long MaxBytes = 50L << 20;
    public const double MaxRatio = 100;

    /// <summary>Prüfsumme einer Quelle (ZIP-Datei oder Ordner) – Ziel der Freigabe.</summary>
    public static string SourceHash(string source) =>
        File.Exists(source) ? ExtensionCatalog.FileSha256(source) : Directory.Exists(source) ? Extensions.Hash(source) : "";

    /// <summary>Installiert aus Ordner oder .zip – mit Freigabe, wenn ein ApprovalGate gesetzt ist.</summary>
    public (InstalledExt? Ext, List<string> Errors) Install(string source, string? approvalId = null)
    {
        if (gate is null) return InstallCore(source);
        if (approvalId is null) return (null, ["Installieren braucht deine Freigabe."]);
        try { return gate.Execute(approvalId, InstallAction, SourceHash(source), () => InstallCore(source)); }
        catch (ApprovalException e) { return (null, [e.Message]); }
    }

    /// <summary>Installiert aus Ordner oder .zip. ZIP-Einträge, die aus dem Ordner ausbrechen, werden abgewiesen.
    /// Der Austausch ist atomar: neue Fassung erst vollständig daneben, dann umbenennen; die alte bleibt bis zum Erfolg erhalten.</summary>
    (InstalledExt? Ext, List<string> Errors) InstallCore(string source)
    {
        var tmp = Path.Combine(Path.GetTempPath(), "trendo-ext-" + Guid.NewGuid().ToString("N"));
        try
        {
            if (File.Exists(source) && source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(tmp);
                using var z = ZipFile.OpenRead(source);
                if (z.Entries.Count > MaxEntries) return (null, [$"Erweiterung hat zu viele Dateien (mehr als {MaxEntries})."]);
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                long total = 0;
                foreach (var e in z.Entries)
                {
                    if (e.FullName.EndsWith('/')) continue;
                    if (!FreeAssets.SafeRel(e.FullName)) return (null, ["Unsicherer Pfad im ZIP: " + e.FullName]);
                    if (!seen.Add(e.FullName.Replace('\\', '/'))) return (null, ["Doppelte Datei im ZIP: " + e.FullName]);
                    if ((total += e.Length) > MaxBytes) return (null, ["Erweiterung ist größer als 50 MB."]);
                    if (e.Length > 1 << 20 && e.Length > MaxRatio * Math.Max(1, e.CompressedLength)) return (null, ["Ungewöhnlich stark gepackte Datei im ZIP (möglicherweise schädlich): " + e.FullName]);
                    var t = Path.Combine(tmp, e.FullName); Directory.CreateDirectory(Path.GetDirectoryName(t)!); e.ExtractToFile(t);
                }
                var inner = Directory.GetDirectories(tmp); if (!File.Exists(Path.Combine(tmp, Extensions.ManifestFile)) && inner.Length == 1) source = inner[0]; else source = tmp;
            }
            if (!Directory.Exists(source)) return (null, ["Ordner oder ZIP nicht gefunden."]);
            var (m, err) = Extensions.Validate(source);
            if (m is null || err.Count > 0) return (null, err);
            var dst = Path.Combine(Root, m.Id);
            if (Get(m.Id) is { } old && !string.Equals(old.Manifest.Author, m.Author, StringComparison.Ordinal))
                return (null, [$"Es gibt schon eine Erweiterung „{old.Manifest.Name}“ mit derselben Kennung von „{old.Manifest.Author}“. Erst diese entfernen."]);
            // T-0766: Aktualisieren nur nach vorn – eine ältere Fassung ersetzt nie eine neuere
            if (Get(m.Id) is { } cur && Extensions.CompareVersion(m.Version, cur.Manifest.Version) < 0)
                return (null, [$"Installiert ist schon die neuere Version {cur.Manifest.Version} – {m.Version} wird nicht darübergelegt."]);
            // T-0766: Modul in der Sandbox-Prüfung (gültiges WebAssembly, nur erlaubte Importe, kein Netz) – ohne es auszuführen
            if (m.Kind == Extensions.ModuleKind && ExtensionSandbox.Inspect(File.ReadAllBytes(Path.Combine(source, m.Entry))) is { Count: > 0 } bad) return (null, bad);
            Directory.CreateDirectory(Root);
            var stage = Path.Combine(Root, ".neu-" + Guid.NewGuid().ToString("N")[..8]);
            var backup = Path.Combine(Root, ".alt-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                CopyDir(source, stage);
                if (Directory.Exists(dst)) Directory.Move(dst, backup);
                try { Directory.Move(stage, dst); }
                catch (Exception) { if (Directory.Exists(backup) && !Directory.Exists(dst)) Directory.Move(backup, dst); throw; }
                if (Directory.Exists(backup)) Directory.Delete(backup, true);
            }
            finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
            audit?.Append("app", "ext.install", $"{m.Id} {m.Version}");
            return (Get(m.Id), []);
        }
        finally { TempDir.Delete(tmp); }
    }

    static void CopyDir(string s, string d)
    {
        Directory.CreateDirectory(d);
        foreach (var f in Directory.GetFiles(s)) File.Copy(f, Path.Combine(d, Path.GetFileName(f)));
        foreach (var sub in Directory.GetDirectories(s)) CopyDir(sub, Path.Combine(d, Path.GetFileName(sub)));
    }

    public InstalledExt? Get(string id)
    {
        var d = Path.Combine(Root, id); if (!Directory.Exists(d)) return null;
        var (m, err) = Extensions.Validate(d); if (m is null || err.Count > 0) return null;
        return new(m, d, !File.Exists(Path.Combine(d, ".aus")), Extensions.Hash(d));
    }

    public IReadOnlyList<InstalledExt> All() => Directory.Exists(Root) ? Directory.GetDirectories(Root).Where(d => !Path.GetFileName(d).StartsWith('.')).Select(d => Get(Path.GetFileName(d))).OfType<InstalledExt>().OrderBy(e => e.Manifest.Name).ToList() : [];
    public void SetEnabled(string id, bool on) { var f = Path.Combine(Root, id, ".aus"); if (on) File.Delete(f); else File.WriteAllText(f, ""); audit?.Append("app", "ext." + (on ? "on" : "off"), id); }
    /// <summary>Entfernen – mit Freigabe, wenn ein ApprovalGate gesetzt ist. Liefert null bei Erfolg, sonst den Grund.</summary>
    public string? Remove(string id, string? approvalId = null)
    {
        if (gate is null) { RemoveCore(id); return null; }
        if (approvalId is null) return "Entfernen braucht deine Freigabe.";
        try { gate.Execute(approvalId, RemoveAction, id, () => { RemoveCore(id); return true; }); return null; }
        catch (ApprovalException e) { return e.Message; }
    }

    void RemoveCore(string id)
    {
        var d = Path.Combine(Root, id); if (Directory.Exists(d)) Directory.Delete(d, true);
        // T-0766: mit der Erweiterung gehen auch ihre Rechte und ihr eigener Datenordner
        if (FreeAssets.SafeRel(id)) { if (File.Exists(RightsFile(id))) File.Delete(RightsFile(id)); var data = DataDir(id); if (Directory.Exists(data)) Directory.Delete(data, true); }
        audit?.Append("app", "ext.remove", id);
    }
    public string EntryText(InstalledExt e) => File.ReadAllText(Path.Combine(e.Dir, e.Manifest.Entry));
}

public static class ExtensionSamples
{
    /// <summary>Legt eine Beispiel-Erweiterung (Baukasten-Vorlage „Visitenkarte“) an – zum Ausprobieren und als Muster für eigene.</summary>
    public static string WriteSample(string parent)
    {
        var d = Path.Combine(parent, "visitenkarte"); Directory.CreateDirectory(d);
        File.WriteAllText(Path.Combine(d, Extensions.ManifestFile), JsonSerializer.Serialize(new ExtManifest("trendo.visitenkarte", "Visitenkarte", "1.0.0", "Trendo", "Eine Seite mit Name, kurzer Vorstellung und Kontakt.", "seite", [], "seite.json", "CC0-1.0"), new JsonSerializerOptions { WriteIndented = true }));
        new SitePage { Title = "Visitenkarte", Blocks = [new("h1", "Dein Name"), new("p", "Was du machst – in zwei Sätzen."), new("list", "Fähigkeit 1\nFähigkeit 2\nFähigkeit 3"), new("contact", "Schreib mir", "du@example.org")] }.Save(Path.Combine(d, "seite.json"));
        return d;
    }
}
