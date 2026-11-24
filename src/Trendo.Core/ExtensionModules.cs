using System.IO.Compression;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>T-0766 (X-04): Rechte einer Funktions-Erweiterung. Die Erweiterung erklärt sie im Manifest; wirksam sind sie erst nach einer eigenen Freigabe
/// (nie im selben Klick wie das Installieren) und nur für genau die installierte Fassung (Prüfsumme). Entziehen jederzeit.
/// Internet gibt es für Erweiterungen nicht – die Sandbox hat keine Netz-Funktionen.</summary>
public static class ExtensionRights
{
    public const string Command = "befehl";
    public const string InputFile = "eingabe-datei";
    public const string ProjectWrite = "projekt-schreiben";
    public const string ProjectRead = "projekt-lesen";
    public const string DataFolder = "daten-ordner";
    public static readonly string[] All = [Command, InputFile, ProjectWrite, ProjectRead, DataFolder];

    /// <summary>Verständliche Beschreibung für die Anzeige vor der Freigabe.</summary>
    public static string Explain(string right) => right switch
    {
        Command => "Einen Knopf im Studio „Projekte“ anbieten",
        InputFile => "Die Datei lesen, die du beim Knopf auswählst – nur diese eine",
        ProjectWrite => "Das Ergebnis als neue Datei in dein offenes Projekt legen (nichts wird überschrieben, rückgängig machbar)",
        ProjectRead => "Alle Dateien im offenen Projekt lesen (nur lesen)",
        DataFolder => $"Einen eigenen Ordner nur für diese Erweiterung lesen und beschreiben (höchstens {SandboxLimits.DefaultDataQuota >> 20} MB und {ExtensionSandbox.MaxDataFiles} Dateien)",
        _ => right,
    };

    public const string GrantAction = "ext.grant";
}

/// <summary>Gespeicherte Freigabe: für welche Fassung (Prüfsumme) welche Rechte gelten.</summary>
public sealed record ExtGrant(string Sha256, string[] Rights, DateTimeOffset At);

/// <summary>Ergebnis eines Befehls: bei Erfolg die neue Datei im Projekt (relativ) und ihre Prüfsumme – Grundlage für „Rückgängig“.</summary>
public sealed record ExtRunResult(bool Ok, string Message, string? OutputRel = null, string? OutputSha = null);

public sealed partial class ExtensionStore
{
    public const string GrantAction = ExtensionRights.GrantAction;
    public string RightsFile(string id) => Path.Combine(DataRoot, "erweiterungen-rechte", id + ".json");
    /// <summary>Eigener Datenordner der Erweiterung – liegt außerhalb ihres Installationsordners, damit die Prüfsumme gleich bleibt.</summary>
    public string DataDir(string id) => Path.Combine(DataRoot, "erweiterungen-daten", id);
    /// <summary>T-0766 (Befund M2): beim Installieren übersetztes Modul, gebunden über Prüfsummen an genau dieses Modul.</summary>
    public string CacheFile(string id) => Path.Combine(DataRoot, "erweiterungen-cache", id + ".cwasm");

    /// <summary>Befund N3: Es gibt eine Freigabe, aber für eine andere Prüfsumme (Erweiterung verändert oder Prüfverfahren erneuert) – ehrlich neu fragen.</summary>
    public bool GrantOutdated(InstalledExt e) => Grant(e.Manifest.Id) is { } g && g.Sha256 != e.Sha256;

    public ExtGrant? Grant(string id)
    {
        if (!FreeAssets.SafeRel(id) || !File.Exists(RightsFile(id))) return null;
        try { return JsonSerializer.Deserialize<ExtGrant>(File.ReadAllText(RightsFile(id))); }
        catch (JsonException) { return null; }
    }

    /// <summary>Wirksame Rechte: nur die freigegebenen, nur für genau diese Fassung und nur solche, die das Manifest auch erklärt.
    /// Nach einem Update (andere Prüfsumme) ist nichts mehr freigegeben.</summary>
    public IReadOnlyList<string> GrantedRights(InstalledExt e) =>
        Grant(e.Manifest.Id) is { } g && g.Sha256 == e.Sha256 ? [.. g.Rights.Where(r => (e.Manifest.Permissions ?? []).Contains(r))] : [];

    /// <summary>Rechte freigeben – alle im Manifest erklärten. Mit ApprovalGate nur mit eigener Freigabe (Ziel: Kennung@Prüfsumme).</summary>
    /// <param name="expectedSha">Befund N4: die Prüfsumme, die der Person angezeigt wurde. Weicht die installierte ab, wird nichts freigegeben.</param>
    public string? GrantRights(string id, string? approvalId = null, string? expectedSha = null)
    {
        if (Get(id) is not { } e) return "Erweiterung nicht gefunden.";
        if (expectedSha is not null && expectedSha != e.Sha256) return "Die Erweiterung hat sich seit der Anzeige geändert – nichts freigegeben. Bitte die Rechte erneut prüfen.";
        if (e.Manifest.Kind != Extensions.ModuleKind) return "Nur Funktions-Erweiterungen brauchen Rechte.";
        void Write()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RightsFile(id))!);
            AtomicFile.WriteAllText(RightsFile(id), JsonSerializer.Serialize(new ExtGrant(e.Sha256, e.Manifest.Permissions ?? [], DateTimeOffset.Now)));
            Audit?.Append("app", "ext.grant", $"{id} {string.Join(",", e.Manifest.Permissions ?? [])}");
        }
        if (Gate is null) { Write(); return null; }
        if (approvalId is null) return "Rechte freigeben braucht deine Freigabe.";
        try { Gate.Execute(approvalId, GrantAction, GrantTarget(e), () => { Write(); return true; }); return null; }
        catch (ApprovalException x) { return x.Message; }
    }

    public static string GrantTarget(InstalledExt e) => e.Manifest.Id + "@" + e.Sha256;

    /// <summary>Rechte entziehen – sofort, ohne Freigabe (weniger Rechte sind immer erlaubt).</summary>
    public void RevokeRights(string id)
    {
        if (!FreeAssets.SafeRel(id)) return;
        if (File.Exists(RightsFile(id))) File.Delete(RightsFile(id));
        Audit?.Append("app", "ext.revoke", id);
    }

    /// <summary>Befehle aller eingeschalteten Funktions-Erweiterungen, deren Rechte freigegeben sind – für die Knöpfe im Studio.</summary>
    public IEnumerable<(InstalledExt Ext, ExtCommand Cmd)> ReadyCommands()
    {
        foreach (var e in All().Where(e => e.Enabled && e.Manifest.Kind == Extensions.ModuleKind))
        {
            var g = GrantedRights(e);
            if (!NeededRights.All(g.Contains)) continue;
            foreach (var c in e.Manifest.Commands ?? []) yield return (e, c);
        }
    }

    static readonly string[] NeededRights = [ExtensionRights.Command, ExtensionRights.InputFile, ExtensionRights.ProjectWrite];

    /// <summary>Befehl ausführen: Eingabedatei aus dem Projekt → Sandbox → Ergebnis als neue Datei ins Projekt (nie überschreiben).
    /// Ohne Rechte, ausgeschaltet, bei Not-Stopp oder bei jedem Fehler des Moduls passiert nichts am Projekt.</summary>
    public ExtRunResult RunCommand(string id, string commandId, ProjectStore projects, string projectId, string inputRel, CancellationToken ct = default, SandboxLimits? limits = null)
    {
        limits ??= SandboxLimits.Default;
        if (Get(id) is not { } e) return new(false, "Erweiterung ist nicht installiert oder beschädigt.");
        if (!e.Enabled) return new(false, $"„{e.Manifest.Name}“ ist ausgeschaltet.");
        if (e.Manifest.Kind != Extensions.ModuleKind || e.Manifest.Commands?.FirstOrDefault(c => c.Id == commandId) is not { } cmd) return new(false, "Diesen Befehl gibt es nicht.");
        if (Gate?.Halted == true) return new(false, "Not-Stopp ist aktiv – es läuft keine Erweiterung.");
        try { return RunCore(e, cmd, projects, projectId, inputRel, ct, limits); }
        // Befund N2: Datei belegt, Rechte fehlen, Platte voll – verständlich melden statt abstürzen
        catch (Exception x) when (x is IOException or UnauthorizedAccessException)
        { Audit?.Append("app", "ext.run.fail", $"{id} {x.GetType().Name}"); return new(false, "Datei konnte nicht gelesen oder geschrieben werden: " + FriendlyError.Say(x)); }
    }

    ExtRunResult RunCore(InstalledExt e, ExtCommand cmd, ProjectStore projects, string projectId, string inputRel, CancellationToken ct, SandboxLimits limits)
    {
        var id = e.Manifest.Id; var commandId = cmd.Id;
        var granted = GrantedRights(e);
        var missing = NeededRights.Where(r => !granted.Contains(r)).ToList();
        if (missing.Count > 0) return new(false, $"Rechte fehlen ({string.Join(", ", missing)}). Erst auf der Seite „Erweiterungen“ freigeben.");
        string input;
        try { input = projects.ResolveInProject(projectId, inputRel); }
        catch (ProjectIsolationException x) { return new(false, x.Message); }
        if (!File.Exists(input)) return new(false, "Datei nicht gefunden.");
        if (!cmd.Input.Contains(Path.GetExtension(input).ToLowerInvariant())) return new(false, $"„{cmd.Label}“ nimmt nur {string.Join(", ", cmd.Input)}.");
        if (new FileInfo(input).Length > limits.MaxInput) return new(false, $"Datei ist größer als {limits.MaxInput >> 20} MB.");
        var wasm = File.ReadAllBytes(Path.Combine(e.Dir, e.Manifest.Entry));
        if (!string.Equals(ExtensionSandbox.Sha256(wasm), e.Manifest.ModuleSha256, StringComparison.OrdinalIgnoreCase)) return new(false, "Modul wurde verändert – nicht gestartet.");
        var dirs = new SandboxDirs(granted.Contains(ExtensionRights.DataFolder) ? DataDir(id) : null, granted.Contains(ExtensionRights.ProjectRead) ? projects.ProjectFilesRoot(projectId) : null, false);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, Gate?.HaltToken ?? CancellationToken.None);
        Audit?.Append("app", "ext.run", $"{id} {commandId} {projectId}:{inputRel}");
        var r = ExtensionSandbox.Run(wasm, [id, commandId], File.ReadAllBytes(input), dirs, limits, linked.Token, CacheFile(id));
        if (!r.Ok) { Audit?.Append("app", "ext.run.fail", $"{id} {r.Error}"); return new(false, r.Error); }
        var rel = UniqueRel(projects, projectId, Path.ChangeExtension(inputRel.Replace('\\', '/'), cmd.Output));
        projects.WriteBytes(projectId, rel, r.Output);
        return new(true, $"„{cmd.Label}“: {Path.GetFileName(rel)} liegt jetzt im Projekt.", rel, ExtensionSandbox.Sha256(r.Output));
    }

    static string UniqueRel(ProjectStore projects, string projectId, string rel)
    {
        var dir = Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? ""; var stem = Path.GetFileNameWithoutExtension(rel); var ext = Path.GetExtension(rel);
        var cand = rel;
        for (var n = 2; projects.ResolveInProject(projectId, cand) is var full && (File.Exists(full) || Directory.Exists(full)); n++) cand = (dir.Length > 0 ? dir + "/" : "") + $"{stem} ({n}){ext}";
        return cand;
    }
}

/// <summary>T-0766: Rückgängig für Ergebnisse von Erweiterungen. Entfernt nur eine Datei, die die Erweiterung angelegt hat und die seitdem unverändert ist
/// (Prüfsumme) – sonst ehrlicher Hinweis, nichts passiert.</summary>
public sealed class ExtensionResultUndo
{
    sealed record Entry(string ProjectId, string Rel, string Sha, string Label);
    readonly List<Entry> _list = [];
    public bool CanUndo => _list.Count > 0;
    public string? Next => _list.Count == 0 ? null : _list[^1].Label;

    public void Record(string projectId, ExtRunResult r) { if (r is { Ok: true, OutputRel: { } rel, OutputSha: { } sha }) _list.Add(new(projectId, rel, sha, Path.GetFileName(rel))); }

    public string Undo(ProjectStore projects)
    {
        if (_list.Count == 0) return "Es gibt nichts rückgängig zu machen.";
        var e = _list[^1]; _list.RemoveAt(_list.Count - 1);
        string path;
        try { path = projects.ResolveInProject(e.ProjectId, e.Rel); }
        catch (ProjectIsolationException x) { return x.Message; }
        if (!File.Exists(path)) return $"„{e.Label}“ ist schon weg – nichts zu tun.";
        if (ExtensionCatalog.FileSha256(path) != e.Sha) return $"„{e.Label}“ wurde inzwischen geändert und bleibt deshalb liegen.";
        File.Delete(path);
        return $"Rückgängig: „{e.Label}“ entfernt.";
    }
}

/// <summary>T-0766: Beispiel-Funktions-Erweiterung „CSV als Tabelle“ – Quelle (WAT) und Binärdatei liegen im Repo (src/Trendo.Core/Erweiterungen) mit Prüfsumme;
/// der Test „Beispielmodul baut byte-gleich“ belegt, dass die Binärdatei aus der Quelle entsteht.</summary>
public static class ExtensionModuleSample
{
    public const string Id = "trendo.csv-tabelle";
    public const string WasmSha256 = "702f52b86e91ab225330a3dfd2045c0214ff0502231642c047688ff8730fa351";

    static byte[] Resource(string name)
    {
        using var s = typeof(ExtensionModuleSample).Assembly.GetManifestResourceStream("Trendo.Core.Erweiterungen." + name) ?? throw new InvalidOperationException("Beispielmodul fehlt im Programm: " + name);
        using var ms = new MemoryStream(); s.CopyTo(ms); return ms.ToArray();
    }
    public static string Source => System.Text.Encoding.UTF8.GetString(Resource("csv-tabelle.wat"));
    public static byte[] Wasm => Resource("csv-tabelle.wasm");

    public static ExtManifest Manifest(string sha, string id = Id, string name = "CSV als Tabelle", string version = "1.0.0", string author = "Trendo") =>
        new(id, name, version, author, "Wandelt eine CSV-Datei im Projekt in eine übersichtliche Tabelle (Markdown) um. Läuft in der Sandbox: ohne Internet, ohne Zugriff auf andere Dateien.",
            Extensions.ModuleKind, [ExtensionRights.Command, ExtensionRights.InputFile, ExtensionRights.ProjectWrite], "modul.wasm", "CC0-1.0",
            [new ExtCommand("umwandeln", "CSV als Tabelle", [".csv"], ".md")], sha);

    /// <summary>Legt die Beispiel-Erweiterung als Ordner an (Manifest, Modul, Quelltext zum Nachlesen).</summary>
    public static string Write(string parent)
    {
        var d = Path.Combine(parent, "csv-tabelle"); Directory.CreateDirectory(Path.Combine(d, "quelle"));
        var wasm = Wasm;
        File.WriteAllBytes(Path.Combine(d, "modul.wasm"), wasm);
        File.WriteAllText(Path.Combine(d, "quelle", "modul.wat"), Source);
        File.WriteAllText(Path.Combine(d, Extensions.ManifestFile), JsonSerializer.Serialize(Manifest(ExtensionSandbox.Sha256(wasm)), ExtensionWorkshop.Json));
        return d;
    }
}

/// <summary>T-0766: Erweiterung in Trendo selbst bauen – Vorlage anlegen, Manifest und Quelltext bearbeiten, mit der eingebauten Wasmtime-Bibliothek
/// aus WebAssembly-Text bauen (kein weiteres Werkzeug nötig), als ZIP ausgeben. Installiert wird wie jede andere Erweiterung (mit Rückfrage und Freigabe).</summary>
public static class ExtensionWorkshop
{
    public const string SourceFile = "quelle/modul.wat";
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Root(string dataRoot) => Path.Combine(dataRoot, "erweiterungen-werkstatt");
    public static string ExportDir(string dataRoot) => Path.Combine(dataRoot, "erweiterungen-export");

    /// <summary>Neue eigene Erweiterung aus der Vorlage (Beispiel „CSV als Tabelle“ als Ausgangspunkt).</summary>
    public static string Create(string dataRoot, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Bitte einen Namen eingeben.");
        var slug = ProjectStore.Slug(name);
        var id = "eigene." + slug;
        var d = Path.Combine(Root(dataRoot), id);
        for (var n = 2; Directory.Exists(d); n++) { id = $"eigene.{slug}-{n}"; d = Path.Combine(Root(dataRoot), id); }
        Directory.CreateDirectory(Path.Combine(d, "quelle"));
        File.WriteAllText(Path.Combine(d, SourceFile), ExtensionModuleSample.Source);
        Save(d, ExtensionModuleSample.Manifest("", id, name.Trim(), "1.0.0", "Ich") with { Description = "Meine eigene Erweiterung." });
        return d;
    }

    public static IReadOnlyList<string> List(string dataRoot) => Directory.Exists(Root(dataRoot)) ? [.. Directory.GetDirectories(Root(dataRoot)).Where(d => File.Exists(Path.Combine(d, Extensions.ManifestFile))).Order()] : [];

    public static ExtManifest Load(string dir) => JsonSerializer.Deserialize<ExtManifest>(File.ReadAllText(Path.Combine(dir, Extensions.ManifestFile)), Json) ?? throw new InvalidDataException("Beschreibung ist leer.");
    public static void Save(string dir, ExtManifest m) => AtomicFile.WriteAllText(Path.Combine(dir, Extensions.ManifestFile), JsonSerializer.Serialize(m, Json));
    public static string LoadSource(string dir) => File.Exists(Path.Combine(dir, SourceFile)) ? File.ReadAllText(Path.Combine(dir, SourceFile)) : "";
    public static void SaveSource(string dir, string wat) { Directory.CreateDirectory(Path.Combine(dir, "quelle")); AtomicFile.WriteAllText(Path.Combine(dir, SourceFile), wat); }

    /// <summary>Quelltext bauen, Modul prüfen, Prüfsumme ins Manifest – danach die ganze Erweiterung wie beim Installieren prüfen.</summary>
    public static List<string> Build(string dir)
    {
        var (wasm, err) = ExtensionSandbox.BuildFromText(LoadSource(dir));
        if (wasm is null) return err;
        File.WriteAllBytes(Path.Combine(dir, "modul.wasm"), wasm);
        Save(dir, Load(dir) with { Entry = "modul.wasm", Kind = Extensions.ModuleKind, ModuleSha256 = ExtensionSandbox.Sha256(wasm) });
        return Extensions.Validate(dir).Errors;
    }

    /// <summary>Als ZIP ausgeben (nur wenn die Prüfung grün ist). Liefert den Pfad der ZIP.</summary>
    public static (string? Zip, List<string> Errors) Export(string dir, string outDir)
    {
        var (m, err) = Extensions.Validate(dir);
        if (m is null || err.Count > 0) return (null, err.Count > 0 ? err : ["Beschreibung fehlt."]);
        Directory.CreateDirectory(outDir);
        var zip = Path.Combine(outDir, $"{m.Id}-{m.Version}.zip");
        var tmp = zip + ".neu";
        File.Delete(tmp);
        ZipFile.CreateFromDirectory(dir, tmp, CompressionLevel.Optimal, includeBaseDirectory: false);
        File.Move(tmp, zip, overwrite: true);
        return (zip, []);
    }
}
