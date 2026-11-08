using System.IO.Compression;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 8 – CORE-11: verschlüsselte Sicherungen mit restic 0.19.1. JSON wie im Quellcode (internal/ui/backup/json.go, cmd_snapshots.go); Echtlauf erst nach Freigabe.</summary>
public class ResticBackupTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "trendo-restic-" + Guid.NewGuid().ToString("N"));
    public ResticBackupTests() { Directory.CreateDirectory(Path.Combine(_dir, "daten")); File.WriteAllText(Path.Combine(_dir, "daten", "a.txt"), "x"); }
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }
    string Data => Path.Combine(_dir, "daten"); string Repo => Path.Combine(_dir, "usb", "sicherung");

    const string BackupOut = """
        {"message_type":"status","percent_done":0.5,"total_files":2}
        {"message_type":"summary","files_new":120,"files_changed":3,"files_unmodified":2000,"dirs_new":4,"dirs_changed":1,"dirs_unmodified":90,"data_blobs":80,"tree_blobs":6,"data_added":52428800,"data_added_packed":40000000,"total_files_processed":2123,"total_bytes_processed":2147483648,"total_duration":12.5,"backup_start":"2026-11-08T10:00:00Z","backup_end":"2026-11-08T10:00:12Z","snapshot_id":"4f2a9c1d77e0aa11bb22cc33dd44ee55ff66aa77bb88cc99dd00ee11ff223344"}
        """;
    const string Snaps = """[{"time":"2026-11-07T09:00:00.1+01:00","tree":"t1","paths":["C:\\Daten"],"hostname":"pc","id":"aaaa1111bbbb2222cccc3333dddd4444eeee5555ffff6666aaaa1111bbbb2222","short_id":"aaaa1111"},{"time":"2026-11-08T09:00:00+01:00","tree":"t2","paths":["C:\\Daten"],"id":"bbbb2222cccc3333dddd4444eeee5555ffff6666aaaa1111bbbb2222cccc3333","short_id":"bbbb2222"}]""";

    [Fact(DisplayName = "restic +1: Zusammenfassung aus „backup --json“ lesen (letzte summary-Zeile), Statuszeilen ignoriert")]
    public void Summary()
    {
        var s = ResticBackup.ParseSummary(BackupOut)!;
        Assert.Equal((120, 3, 2000, 52428800L, 2147483648L, 12.5), (s.FilesNew, s.FilesChanged, s.FilesUnmodified, s.BytesAdded, s.BytesProcessed, s.Seconds));
        Assert.StartsWith("4f2a9c1d", s.SnapshotId); Assert.Null(ResticBackup.ParseSummary("{\"message_type\":\"status\"}"));
    }

    [Fact(DisplayName = "restic +2: Stände aus „snapshots --json“ – neueste zuerst, Kurz-ID, Pfade")]
    public void Snapshots()
    {
        var l = ResticBackup.ParseSnapshots(Snaps);
        Assert.Equal(["bbbb2222", "aaaa1111"], l.Select(x => x.ShortId)); Assert.Equal(["C:\\Daten"], l[0].Paths);
        Assert.Empty(ResticBackup.ParseSnapshots("")); Assert.Empty(ResticBackup.ParseSnapshots("[]"));
    }

    [Fact(DisplayName = "restic +3: erste Sicherung legt die Ablage an (init), dann backup mit Tag trendo; Passwort nur in der Umgebung, nie in den Argumenten")]
    public void Backup_flow()
    {
        var calls = new List<(IReadOnlyList<string> A, IDictionary<string, string> Env)>();
        var (s, msg) = ResticBackup.Backup(_dir, Repo, "geheim-12345", [Data], (e, a, env) =>
        {
            calls.Add((a, env));
            if (a[0] == "init") { Directory.CreateDirectory(Path.Combine(Repo, "data")); File.WriteAllText(Path.Combine(Repo, "config"), "x"); return new Cli.Result(0, "{\"message_type\":\"initialized\"}", ""); }
            return new Cli.Result(0, BackupOut, "");
        });
        Assert.NotNull(s); Assert.Equal(["init", "backup"], calls.Select(c => c.A[0]));
        Assert.Equal(["backup", "-r", Repo, "--json", "--tag", "trendo", "--exclude-caches", Data], calls[1].A);
        Assert.All(calls, c => { Assert.Equal("geheim-12345", c.Env["RESTIC_PASSWORD"]); Assert.DoesNotContain("geheim-12345", c.A); });
        Assert.StartsWith("Gesichert: 120 neue, 3 geänderte, 2000 unveränderte Dateien – 50,0 MB neu dazu (von 2,0 GB), verschlüsselt. Stand 4f2a9c1d.", msg);
        calls.Clear(); ResticBackup.Backup(_dir, Repo, "geheim-12345", [Data], (e, a, env) => { calls.Add((a, env)); return new Cli.Result(0, BackupOut, ""); });
        Assert.Equal(["backup"], calls.Select(c => c.A[0])); // Ablage besteht schon
    }

    [Fact(DisplayName = "restic +4: Wiederherstellen nur in neuen/leeren Ordner; Argumente restore <id> --target")]
    public void Restore()
    {
        IReadOnlyList<string>? seen = null; var target = Path.Combine(_dir, "zurueck");
        var (ok, msg) = ResticBackup.Restore(_dir, Repo, "geheim-12345", "bbbb2222", target, (_, a, _) => { seen = a; return new Cli.Result(0, "", ""); });
        Assert.True(ok, msg); Assert.Equal(["restore", "bbbb2222", "-r", Repo, "--target", target], seen);
        Assert.False(ResticBackup.Restore(_dir, Repo, "geheim-12345", "bbbb2222", Data, (_, _, _) => new Cli.Result(0, "", "")).Ok); // nicht leer
        Assert.Equal("Ungültiger Stand.", ResticBackup.Restore(_dir, Repo, "x", "latest; rm", target).Message);
    }

    [Fact(DisplayName = "restic −1: Prüfungen – Passwort ≥ 8, vollständiger Pfad, Ordner vorhanden, Sicherungsort nicht im gesicherten Ordner")]
    public void Validate()
    {
        var e = ResticBackup.Validate("relativ", "kurz", []);
        Assert.Equal(3, e.Count);
        Assert.Contains(ResticBackup.Validate(Path.Combine(Data, "sich"), "geheim-12345", [Data]), x => x.Contains("würde sich selbst sichern"));
        Assert.Contains(ResticBackup.Validate(Repo, "geheim-12345", [Path.Combine(_dir, "weg")]), x => x.Contains("gibt es nicht"));
        Assert.Empty(ResticBackup.Validate(Repo, "geheim-12345", [Data]));
        Assert.StartsWith("Das Passwort braucht", ResticBackup.Backup(_dir, Repo, "kurz", [Data], (_, _, _) => throw new InvalidOperationException("darf nicht laufen")).Message);
    }

    [Fact(DisplayName = "restic −2: falsches Passwort verständlich; ohne Einrichtung klare Meldung; Archiv liefert nur restic.exe; feste Fassung gepinnt")]
    public void Errors_and_setup()
    {
        Directory.CreateDirectory(Path.Combine(Repo, "data")); File.WriteAllText(Path.Combine(Repo, "config"), "x");
        Assert.Equal("Das Passwort passt nicht zu dieser Sicherung.", ResticBackup.Backup(_dir, Repo, "falsch-123", [Data], (_, _, _) => new Cli.Result(1, "", "Fatal: wrong password or no key found")).Message);
        Assert.Equal("restic ist noch nicht eingerichtet.", ResticBackup.Backup(_dir, Repo, "geheim-12345", [Data]).Message);
        using var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true)) { using (var w = new StreamWriter(z.CreateEntry("restic_0.19.1_windows_amd64.exe").Open())) w.Write("MZ"); using (var w = new StreamWriter(z.CreateEntry("unter/boese.exe").Open())) w.Write("x"); }
        var tools = Path.Combine(_dir, "tools"); Assert.True(ResticBackup.ExtractExe(ms.ToArray(), tools));
        Assert.Equal(["restic.exe"], Directory.GetFiles(tools).Select(Path.GetFileName));
        Assert.Contains("v" + ResticBackup.Version, ResticBackup.ZipUrl); Assert.Equal(64, ResticBackup.ZipSha256.Length);
    }
}
