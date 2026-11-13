using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 8 – CORE-11: Karte „Verschlüsselte Sicherung mit Versionen (restic)“ (restic nachgebildet; Echtlauf erst nach Freigabe).</summary>
public class SchutzRestic_Ui
{
    [AvaloniaFact(DisplayName = "restic-Karte: ohne Einrichtung Einrichten-Knopf; sichern legt Ablage an, Passwort nur in der Umgebung; Stände anzeigen; Wiederherstellen in neuen Ordner")]
    public async Task Backup_list_restore()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            MainWindow.ResticDirOverride = null; MainWindow.ResticRunOverride = null;
            w.Show("schutz"); Assert.NotNull(w.Find<Button>("sec-restic-setup"));
            var data = Path.Combine(root, "eigene-daten"); Directory.CreateDirectory(data); File.WriteAllText(Path.Combine(data, "brief.txt"), "x");
            var repo = Path.Combine(root, "usb", "sicherung"); var calls = new List<IReadOnlyList<string>>(); var envs = new List<string>();
            MainWindow.ResticDirOverride = Path.Combine(root, "restic");
            MainWindow.ResticRunOverride = (_, a, env) =>
            {
                calls.Add(a); envs.Add(env["RESTIC_PASSWORD"]);
                return a[0] switch
                {
                    "init" => Init(),
                    "backup" => new Cli.Result(0, """{"message_type":"summary","files_new":1,"files_changed":0,"files_unmodified":0,"data_added":2048,"total_bytes_processed":2048,"total_duration":0.4,"snapshot_id":"abcdef1234567890"}""", ""),
                    "snapshots" => new Cli.Result(0, """[{"time":"2026-11-08T10:00:00+01:00","paths":["X:\\eigene-daten"],"id":"abcdef1234567890","short_id":"abcdef12"}]""", ""),
                    "restore" => new Cli.Result(0, "", ""),
                    _ => new Cli.Result(1, "", "unbekannt"),
                };
                Cli.Result Init() { Directory.CreateDirectory(Path.Combine(repo, "data")); File.WriteAllText(Path.Combine(repo, "config"), "x"); return new Cli.Result(0, "", ""); }
            };
            w.Show("schutz"); Assert.NotNull(w.Find<TextBox>("sec-restic-pw"));
            await w.SchutzResticBackup(data, repo, "geheim-12345");
            Assert.StartsWith("Gesichert: 1 neue", w.ResticMessage); Assert.Contains("Passwort gut aufbewahren", w.ResticMessage);
            Assert.Equal(["init", "backup"], calls.Select(c => c[0])); Assert.All(envs, e => Assert.Equal("geheim-12345", e));
            Assert.DoesNotContain(calls.SelectMany(c => c), a => a.Contains("geheim"));
            Assert.DoesNotContain("geheim-12345", File.ReadAllText(w.Core.Audit.FilePath));
            await w.SchutzResticList(repo, "geheim-12345");
            var snap = Assert.Single(w.ResticSnapshots!); Assert.Equal("abcdef12", snap.ShortId);
            Assert.NotNull(w.Find<Button>("sec-restic-restore-0"));
            var target = await w.SchutzResticRestore(snap, "geheim-12345");
            Assert.NotNull(target); Assert.Contains("abcdef12", target); Assert.Equal(["restore", "abcdef1234567890:/X/eigene-daten"], calls[^1].Take(2)); // gezielt der gesicherte Ordner
            Assert.StartsWith("Wiederhergestellt", w.ResticMessage);
            await w.SchutzResticBackup(data, Path.Combine(data, "innen"), "geheim-12345");
            Assert.Contains("würde sich selbst sichern", w.ResticMessage);
        }
        finally { MainWindow.ResticDirOverride = null; MainWindow.ResticRunOverride = null; w.Close(); }
    }
}
