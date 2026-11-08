using System.Globalization;
using System.IO.Compression;
using System.Text.Json.Nodes;

namespace Trendo.Core.Sys;

/// <summary>Alle Studios, Runde 8 – CORE-11 „Daten schützen“: verschlüsselte, entdoppelte Sicherungen mit Versionen über restic 0.19.1 (BSD-2-Clause,
/// Einzelprogramm, offizieller Download mit fester SHA-256). Das Passwort geht nur über die Umgebung an den Prozess und wird nirgends gespeichert –
/// ohne Passwort ist die Sicherung nicht lesbar. Wiederherstellen nur in einen neuen, leeren Ordner (nichts wird überschrieben).</summary>
public static class ResticBackup
{
    public const string Version = "0.19.1";
    public const string ZipUrl = "https://github.com/restic/restic/releases/download/v0.19.1/restic_0.19.1_windows_amd64.zip";
    /// <summary>Prüfsumme laut GitHub-Release (Feld „digest“), deckt sich mit SHA256SUMS des Projekts.</summary>
    public const string ZipSha256 = "da948ad707ed690426473aaba2046cd61f8f90f6f0e7dab6be0d5796531de67d";
    public static string Exe(string dir) => Path.Combine(dir, OperatingSystem.IsWindows() ? "restic.exe" : "restic");
    public static bool Ready(string dir) => File.Exists(Exe(dir));

    /// <summary>Passwort nur für diesen Prozess; Abfragen sind damit ausgeschlossen.</summary>
    public static Dictionary<string, string> Env(string password) => new() { ["RESTIC_PASSWORD"] = password };

    public static IReadOnlyList<string> InitArgs(string repo) => ["init", "-r", repo, "--json"];
    public static IReadOnlyList<string> BackupArgs(string repo, IEnumerable<string> paths) => ["backup", "-r", repo, "--json", "--tag", "trendo", "--exclude-caches", .. paths];
    public static IReadOnlyList<string> SnapshotsArgs(string repo) => ["snapshots", "-r", repo, "--json"];
    /// <summary>Mit Quellpfad wird gezielt dieser gesicherte Ordner zurückgeholt („id:/C/…“) – direkt in den Zielordner, ohne den ganzen Originalpfad nachzubauen
    /// (echter Lauf 08.11.2026: ohne Pfad baute restic „ziel\C\Users\…“ nach und scheiterte an Zeitstempeln übergeordneter Ordner).</summary>
    public static IReadOnlyList<string> RestoreArgs(string repo, string id, string target, string? sourcePath = null) =>
        ["restore", sourcePath is null ? id : id + ":" + SnapshotPath(sourcePath), "-r", repo, "--target", target];

    /// <summary>Windows-Pfad in die Schreibweise innerhalb eines restic-Stands: „C:\Daten\x“ → „/C/Daten/x“.</summary>
    public static string SnapshotPath(string p)
    {
        var f = p.Replace('\\', '/').TrimEnd('/');
        return f.Length >= 2 && f[1] == ':' ? "/" + f[0] + f[2..] : f.StartsWith('/') ? f : "/" + f;
    }
    public static IReadOnlyList<string> CheckArgs(string repo) => ["check", "-r", repo];

    /// <summary>Eingaben prüfen – leere Liste = in Ordnung.</summary>
    public static IReadOnlyList<string> Validate(string repo, string password, IReadOnlyList<string> paths)
    {
        var e = new List<string>();
        if (password.Length < 8) e.Add("Das Passwort braucht mindestens 8 Zeichen. Ohne Passwort lässt sich die Sicherung nicht öffnen – bitte gut aufbewahren.");
        if (!Path.IsPathFullyQualified(repo)) e.Add("Bitte den Sicherungsort als vollständigen Pfad angeben, z. B. E:\\Sicherung.");
        if (paths.Count == 0) e.Add("Bitte mindestens einen Ordner zum Sichern angeben.");
        foreach (var p in paths)
        {
            if (!Directory.Exists(p) && !File.Exists(p)) e.Add($"„{p}“ gibt es nicht.");
            else if (Path.IsPathFullyQualified(repo) && Inside(Path.GetFullPath(repo), Path.GetFullPath(p))) e.Add($"Der Sicherungsort liegt in „{p}“ – die Sicherung würde sich selbst sichern. Bitte einen anderen Ort (z. B. USB-Platte) wählen.");
        }
        return e;
    }

    static bool Inside(string child, string parent)
    {
        var p = parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return (child.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar).StartsWith(p, StringComparison.OrdinalIgnoreCase);
    }

    public sealed record Summary(int FilesNew, int FilesChanged, int FilesUnmodified, long BytesAdded, long BytesProcessed, double Seconds, string SnapshotId);

    /// <summary>Letzte „summary“-Zeile der JSON-Ausgabe von „restic backup --json“ (Felder wie internal/ui/backup/json.go, 0.19.1).</summary>
    public static Summary? ParseSummary(string output)
    {
        foreach (var line in output.Replace("\r", "").Split('\n').Reverse())
        {
            if (!line.TrimStart().StartsWith('{')) continue;
            var j = JsonNode.Parse(line)!;
            if (j["message_type"]?.GetValue<string>() != "summary") continue;
            int I(string k) => j[k] is JsonValue v && v.TryGetValue<int>(out var i) ? i : 0;
            long L(string k) => j[k] is JsonValue v && v.TryGetValue<long>(out var i) ? i : 0;
            return new(I("files_new"), I("files_changed"), I("files_unmodified"), L("data_added"), L("total_bytes_processed"), j["total_duration"]?.GetValue<double>() ?? 0, j["snapshot_id"]?.GetValue<string>() ?? "");
        }
        return null;
    }

    public sealed record Snapshot(string Id, string ShortId, DateTimeOffset Time, IReadOnlyList<string> Paths);

    /// <summary>„restic snapshots --json“: Liste der Stände, neueste zuerst.</summary>
    public static List<Snapshot> ParseSnapshots(string json)
    {
        var a = JsonNode.Parse(json.Trim().Length == 0 ? "[]" : json)?.AsArray() ?? [];
        return [.. a.OfType<JsonObject>().Select(s => new Snapshot(s["id"]?.GetValue<string>() ?? "", s["short_id"]?.GetValue<string>() ?? (s["id"]?.GetValue<string>() ?? "").PadRight(8)[..8],
            DateTimeOffset.Parse(s["time"]!.GetValue<string>(), CultureInfo.InvariantCulture), s["paths"]?.AsArray().Select(p => p!.GetValue<string>()).ToList() ?? []))
            .OrderByDescending(s => s.Time)];
    }

    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    public static string Bytes(long b) => b >= 1L << 30 ? (b / (double)(1L << 30)).ToString("0.0", De) + " GB" : b >= 1L << 20 ? (b / (double)(1L << 20)).ToString("0.0", De) + " MB" : (b / 1024.0).ToString("0", De) + " KB";

    /// <summary>Ist der Sicherungsort schon eine restic-Ablage? (Datei „config“ und Ordner „data“.)</summary>
    public static bool IsRepo(string repo) => File.Exists(Path.Combine(repo, "config")) && Directory.Exists(Path.Combine(repo, "data"));

    /// <summary>Sichern: legt die Ablage bei Bedarf an, sichert, liest die Zusammenfassung.</summary>
    public static (Summary? Result, string Message) Backup(string toolDir, string repo, string password, IReadOnlyList<string> paths, Func<string, IReadOnlyList<string>, IDictionary<string, string>, Cli.Result>? run = null)
    {
        if (Validate(repo, password, paths) is { Count: > 0 } errors) return (null, string.Join(" ", errors));
        if (run is null && !Ready(toolDir)) return (null, "restic ist noch nicht eingerichtet.");
        run ??= (e, a, env) => Cli.Run(e, a, timeoutMs: 6 * 3_600_000, env: env);
        var env = Env(password);
        if (!IsRepo(repo))
        {
            Directory.CreateDirectory(repo);
            var init = run(Exe(toolDir), InitArgs(repo), env);
            if (!init.Ok) return (null, "Sicherungsort ließ sich nicht anlegen: " + First(init));
        }
        var r = run(Exe(toolDir), BackupArgs(repo, paths), env);
        var s = ParseSummary(r.Out);
        if (s is null) return (null, r.Err.Contains("wrong password", StringComparison.OrdinalIgnoreCase) || r.Err.Contains("no key found", StringComparison.OrdinalIgnoreCase)
            ? "Das Passwort passt nicht zu dieser Sicherung." : "Sichern ging nicht: " + First(r));
        return (s, $"Gesichert: {s.FilesNew} neue, {s.FilesChanged} geänderte, {s.FilesUnmodified} unveränderte Dateien – {Bytes(s.BytesAdded)} neu dazu (von {Bytes(s.BytesProcessed)}), verschlüsselt. Stand {s.SnapshotId[..Math.Min(8, s.SnapshotId.Length)]}."
            + (r.Ok ? "" : " Einige Dateien waren nicht lesbar (z. B. geöffnet) und fehlen in diesem Stand."));
    }

    /// <summary>Wiederherstellen in einen neuen, leeren Ordner.</summary>
    public static (bool Ok, string Message) Restore(string toolDir, string repo, string password, string snapshotId, string target, Func<string, IReadOnlyList<string>, IDictionary<string, string>, Cli.Result>? run = null, IReadOnlyList<string>? sourcePaths = null)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(snapshotId, "^[0-9a-f]{8,64}$")) return (false, "Ungültiger Stand.");
        if (!Path.IsPathFullyQualified(target)) return (false, "Bitte einen vollständigen Zielordner angeben.");
        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any()) return (false, "Der Zielordner ist nicht leer – Trendo stellt nur in einen neuen oder leeren Ordner wieder her, damit nichts überschrieben wird.");
        if (run is null && !Ready(toolDir)) return (false, "restic ist noch nicht eingerichtet.");
        run ??= (e, a, env) => Cli.Run(e, a, timeoutMs: 6 * 3_600_000, env: env);
        Directory.CreateDirectory(target);
        if (sourcePaths is null || sourcePaths.Count == 0)
        {
            var r0 = run(Exe(toolDir), RestoreArgs(repo, snapshotId, target), Env(password));
            return r0.Ok ? (true, $"Wiederhergestellt nach „{target}“.") : (false, "Wiederherstellen ging nicht: " + First(r0));
        }
        foreach (var src in sourcePaths) // ein Ordner: direkt ins Ziel; mehrere: je ein Unterordner mit dem Ordnernamen
        {
            var name = string.Concat(Path.GetFileName(src.TrimEnd('\\', '/')).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var dest = sourcePaths.Count == 1 ? target : Path.Combine(target, name.Length > 0 ? name : "laufwerk");
            var r = run(Exe(toolDir), RestoreArgs(repo, snapshotId, dest, src), Env(password));
            if (!r.Ok) return (false, "Wiederherstellen ging nicht: " + First(r));
        }
        return (true, $"Wiederhergestellt nach „{target}“.");
    }

    /// <summary>Stände der Sicherung abrufen.</summary>
    public static (List<Snapshot>? List, string Message) Snapshots(string toolDir, string repo, string password, Func<string, IReadOnlyList<string>, IDictionary<string, string>, Cli.Result>? run = null)
    {
        if (!IsRepo(repo)) return (null, "Am Sicherungsort liegt noch keine Sicherung.");
        if (run is null && !Ready(toolDir)) return (null, "restic ist noch nicht eingerichtet.");
        run ??= (e, a, env) => Cli.Run(e, a, timeoutMs: 120_000, env: env);
        var r = run(Exe(toolDir), SnapshotsArgs(repo), Env(password));
        if (!r.Ok) return (null, r.Err.Contains("wrong password", StringComparison.OrdinalIgnoreCase) ? "Das Passwort passt nicht zu dieser Sicherung." : "Stände nicht lesbar: " + First(r));
        try { var l = ParseSnapshots(r.Out); return (l, $"{l.Count} Stände."); }
        catch (Exception e) when (e is System.Text.Json.JsonException or FormatException) { return (null, "restic hat keine lesbare Antwort geliefert."); }
    }

    static string First(Cli.Result r) => (r.Err.Length > 0 ? r.Err : r.Out).Trim().Split('\n')[0];

    /// <summary>Aus dem geprüften Archiv nur das Programm übernehmen (im Archiv heißt es restic_0.19.1_windows_amd64.exe).</summary>
    public static bool ExtractExe(byte[] zip, string toolDir)
    {
        using var a = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        var e = a.Entries.FirstOrDefault(x => x.FullName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !x.FullName.Contains('/') && x.Name.StartsWith("restic", StringComparison.OrdinalIgnoreCase));
        if (e is null) return false;
        Directory.CreateDirectory(toolDir); e.ExtractToFile(Exe(toolDir), overwrite: true); return true;
    }
}
