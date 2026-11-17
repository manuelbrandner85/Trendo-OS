using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Office;
using Trendo.Core.Programs;
using Trendo.Core.Sys;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0730: Zurückholen einer Notsicherung prüft ERST den Inhalt (lesen, mit dem Programm öffnen) und übernimmt sie DANN.
/// Scheitert das Öffnen, bleiben Anschluss und Sicherung unberührt: die Karte bietet sie weiter an, der 20-s-Takt überschreibt und löscht sie nicht.
/// Dazu: liegengebliebene „…uebernahme-&lt;PID&gt;“ aus einer abgebrochenen Umwandlung werden wieder angeboten.</summary>
public class T0730_OfficeRecover_Ui
{
    static bool Has(MainWindow w, string name) { Settle(); return w.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name && c.IsEffectivelyVisible); }

    static MainWindow Reopen(string root)
    {
        var w = new MainWindow(new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher()), new AppOptions(root, null, 0, Isolated: true), new Probe("browser"));
        w.ProgramSetLevel(UserLevel.Erweitert); w.FreigabeConfirm = _ => Task.FromResult(true); w.Width = 1600; w.Height = 1100; w.Show(); Settle(); return w;
    }

    static string Backup(string root) => Path.Combine(root, "programme", "sicherung");

    /// <summary>Sicherung wie aus einem abgestürzten Fenster (ohne Sperre) mit beliebigem Inhalt schreiben.</summary>
    static string WriteBackup(string root, string kind, JsonObject state)
    {
        var l = new ProgramLink("LINK-" + kind.ToUpperInvariant(), kind, null, "Kaputte Sicherung");
        l.Track(state);
        return l.Autosave(Backup(root))!;
    }

    static void BrokenStays(string kind, Action<MainWindow> open, JsonObject state)
    {
        var (w0, _, root) = Ui.Open(); w0.Close();
        var file = WriteBackup(root, kind, state);
        var bytes = File.ReadAllBytes(file);
        var w = Reopen(root);
        try
        {
            open(w); Settle();
            Assert.Single(w.OfficeRecoveries(kind));
            Assert.ThrowsAny<InvalidDataException>(() => w.OfficeRecover(kind, 0, restore: true));
            Assert.False(ProgramDocument.SameFile(w.OfficeLink(kind).Doc.BackupFile(Backup(root)), file)); // Anschluss hat die Sicherung NICHT übernommen
            Assert.False(w.OfficeLink(kind).Dirty);
            Assert.True(File.Exists(file));
            Assert.Single(w.OfficeRecoveries(kind)); // weiter angeboten
            w.Show(kind); Assert.True(Has(w, $"office-recovery-{kind}"), kind + ": Karte verschwunden");
            w.ProgramAutosaveAll(); // Takt der Hülle
            Assert.Equal(bytes, File.ReadAllBytes(file)); // nicht überschrieben
        }
        finally { w.Close(); }
        Assert.True(File.Exists(file)); // auch nach dem Schließen noch da
    }

    [AvaloniaFact(DisplayName = "T-0730 Text: Sicherung mit kaputtem Base64 – Zurückholen scheitert, Sicherung bleibt erhalten und angeboten")]
    public void Text_broken_base64() => BrokenStays("text", w => { w.TextNew(); w.Show("text"); },
        new JsonObject { ["datei"] = "text.docx", ["base64"] = "%%% kein base64 %%%" });

    [AvaloniaFact(DisplayName = "T-0730 Text: beschädigte DOCX in der Sicherung – Sicherung bleibt erhalten und angeboten")]
    public void Text_broken_docx() => BrokenStays("text", w => { w.TextNew(); w.Show("text"); },
        ProgramLink.FileState("text.docx", "das ist keine docx"u8.ToArray()));

    [AvaloniaFact(DisplayName = "T-0730 3D: Szene aus neuerer Fassung – Sicherung bleibt erhalten und angeboten")]
    public void Scene_newer_version() => BrokenStays("3d", w => w.Show("3d"),
        ProgramLink.FileState("3d-szene.json", "{\"version\":99,\"name\":\"Zukunft\",\"parts\":[]}"u8.ToArray())); // Szene-JSON schreibt kleine Namen (camelCase)

    [AvaloniaFact(DisplayName = "T-0730 CAD: Bauteil aus neuerer Fassung – Sicherung bleibt erhalten und angeboten")]
    public void Cad_newer_version() => BrokenStays("cad", w => { w.CadNew(); w.Show("cad"); },
        ProgramLink.FileState("cad.tcad", "{\"Version\":99,\"Params\":{},\"Bodies\":[]}"u8.ToArray()));

    [AvaloniaFact(DisplayName = "T-0730 Text: lesbare Sicherung wird nach der Prüfung wie bisher übernommen")]
    public void Text_good_still_restores()
    {
        var (w0, _, root) = Ui.Open(); w0.Close();
        var tmp = Path.Combine(root, "gut.docx"); TextFiles.Save(new TextDoc { Blocks = [new Para("Guter Entwurf")] }, tmp);
        var file = WriteBackup(root, "text", ProgramLink.FileState("text.docx", File.ReadAllBytes(tmp)));
        var w = Reopen(root);
        try
        {
            w.TextNew(); w.Show("text"); Settle();
            w.OfficeRecover("text", 0, restore: true);
            Assert.Equal("Guter Entwurf", string.Join("|", w.TextDocument.Paras.Select(p => p.Text)));
            Assert.True(ProgramDocument.SameFile(w.OfficeLink("text").Doc.BackupFile(Backup(root)), file));
            Assert.Empty(w.OfficeRecoveries("text"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0730 Umzug: liegengebliebene „text.docx.uebernahme-<PID>“ eines beendeten Prozesses wird wieder angeboten")]
    public void Stale_migration_claim_offered()
    {
        var (w0, _, root) = Ui.Open(); w0.Close();
        var old = Path.Combine(root, "wiederherstellen"); Directory.CreateDirectory(old);
        var claim = Path.Combine(old, "text.docx.uebernahme-" + int.MaxValue); // kein laufender Prozess
        TextFiles.Save(new TextDoc { Blocks = [new Para("Hängengebliebener Entwurf")] }, Path.Combine(old, "text.docx"));
        File.Move(Path.Combine(old, "text.docx"), claim);
        var w = Reopen(root);
        try
        {
            w.TextNew(); w.Show("text"); Settle();
            Assert.Single(w.OfficeRecoveries("text"));
            Assert.False(File.Exists(claim));
            Assert.False(Directory.Exists(old));
            w.OfficeRecover("text", 0, restore: true);
            Assert.Equal("Hängengebliebener Entwurf", string.Join("|", w.TextDocument.Paras.Select(p => p.Text)));
        }
        finally { w.Close(); }
    }
}
