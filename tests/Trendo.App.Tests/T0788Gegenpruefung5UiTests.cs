using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Trendo.Core.Wissen;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0788, 4. Prüfrunde: beim Verlegen eines geschützten Stammbaums aus Claudes Ordner ziehen Versionen und Begleitdateien mit (nichts mit Lebenden bleibt dort),
/// Unterordner von Claudes Ordnern zählen mit, voller Datenträger verliert nichts und sagt ehrlich, dass die Kopie noch dort liegt. Claude ersetzt.</summary>
[Collection("Claude")]
public class T0788Gegenpruefung5_Ui
{
    const string Fam = MainWindow.FamKey, Studio = MainWindow.FamStudio;
    static readonly string[] Secret = ["Erika", "Karl", "15 AUG 1952"];

    static async Task Run(Task t) { while (!t.IsCompleted) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static List<string> ClaudeOn()
    {
        var folders = new List<string>();
        MainWindow.ClaudeStatusOverride = () => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
        MainWindow.ClaudeStepDelay = 0;
        MainWindow.ClaudeRunOverride = (args, cwd, line, _) =>
        {
            folders.Add(cwd);
            line(System.Text.Json.JsonSerializer.Serialize(new { type = "result", subtype = "success", is_error = false, result = "ok", session_id = "s1", num_turns = 1 }));
            return Task.FromResult(0);
        };
        return folders;
    }
    static void Off() { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; MainWindow.ClaudeStepDelay = 350; }
    static bool Inside(string folder, string? file) => file is not null && Path.GetFullPath(file).StartsWith(Path.GetFullPath(folder).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
    /// <summary>Alles, was in Claudes Ordner liegt (alle Dateien, alle Unterordner) als Text.</summary>
    static string AllText(string folder) => Directory.Exists(folder) ? string.Join("\n", Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Select(f => { try { return File.ReadAllText(f); } catch (IOException) { return ""; } })) : "";
    static string ClaudeRoot(MainWindow w) => Path.GetDirectoryName(w.ClaudeChatRoot(Studio))!; // …\projekte\Claude
    static string StudioFolder(MainWindow w) => Path.Combine(Path.GetDirectoryName(ClaudeRoot(w))!, "Stammbaum");

    static async Task<(MainWindow W, ProgramDocument D, string Folder)> ChatOnUnprotected(List<string> folders)
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1400; w.ProgramSetLevel(UserLevel.Profi);
        await Run(w.ClaudeCheck(true));
        var d = w.ProgramNew(Fam, "beispiel"); w.Show(Fam); Settle();
        w.FamSetOption(d, "hideLiving", "false"); w.Show(Fam); Settle();
        w.ClaudeDockToggle(Studio); Dispatcher.UIThread.RunJobs();
        await Run(w.ClaudeChatSend("Ergänze Erikas Geschwister"));
        for (var i = 0; i < 10; i++) Settle();
        Assert.True(Inside(folders[0], d.Path), "Vorbedingung: Dokument liegt in Claudes Ordner");
        return (w, d, folders[0]);
    }

    [AvaloniaFact(DisplayName = "T-0788 R4 HOCH: Versionen ziehen beim Verlegen einzeln mit (kopiert, geprüft, dann entfernt) – in Claudes Ordner bleibt nichts mit Lebenden, die Meldung widerspricht sich nicht")]
    public async Task Versions_moved()
    {
        var folders = ClaudeOn(); MainWindow? w = null;
        try
        {
            (w, var d, var folder) = await ChatOnUnprotected(folders);
            foreach (var title in new[] { "Familie Muster A", "Familie Muster B", "Familie Muster C" }) { w.FamSetOption(d, "title", title); w.ProgramSave(Fam); }
            var oldVersions = Directory.GetFiles(ProgramDocument.VersionDir(d.Path!));
            Assert.True(oldVersions.Length >= 2, $"Vorbedingung: Versionen in Claudes Ordner ({oldVersions.Length})");
            Assert.Contains("Erika", AllText(folder));
            w.FamSetOption(d, "hideLiving", "true"); w.Show(Fam); Settle();
            Assert.False(Inside(folder, d.Path));
            var all = AllText(folder);
            foreach (var s in Secret) Assert.DoesNotContain(s, all);
            Assert.Equal(oldVersions.Length, Directory.GetFiles(ProgramDocument.VersionDir(d.Path!)).Length);
            var msg = w.ProgramMessage(Fam);
            Assert.Contains("liegt jetzt in", msg); Assert.DoesNotContain("konnte nicht", msg);
        }
        finally { Off(); w?.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0788 R4 HOCH: Begleitdateien (Wiederherstellung, Notsicherung, Zwischendatei) bleiben nicht in Claudes Ordner – die Wiederherstellung liegt am neuen Ort")]
    public async Task Companions_moved()
    {
        var folders = ClaudeOn(); MainWindow? w = null;
        try
        {
            (w, var d, var folder) = await ChatOnUnprotected(folders);
            var p = d.Path!;
            File.Copy(p, ProgramDocument.RecoveryFile(p)); File.Copy(p, ProgramDocument.AutosaveFile(p)); File.Copy(p, p + ".1a2b3c4d.tmp");
            w.FamSetOption(d, "hideLiving", "true"); w.Show(Fam); Settle();
            var all = AllText(folder);
            foreach (var s in Secret) Assert.DoesNotContain(s, all);
            Assert.False(File.Exists(ProgramDocument.RecoveryFile(p))); Assert.False(File.Exists(ProgramDocument.AutosaveFile(p))); Assert.False(File.Exists(p + ".1a2b3c4d.tmp"));
            Assert.True(File.Exists(ProgramDocument.RecoveryFile(d.Path!)));
            Assert.Contains("Erika", File.ReadAllText(ProgramDocument.RecoveryFile(d.Path!))); // nichts verloren
        }
        finally { Off(); w?.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0788 R4 MITTEL: „Speichern unter“ in einen Unterordner unterhalb von Claudes Ordnern legt den geschützten Stammbaum nicht dort ab; das neue Ziel liegt nie darunter")]
    public void Subfolder_save_as()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Width = 1600; w.Height = 1400; w.ProgramSetLevel(UserLevel.Profi);
            var d = w.ProgramNew(Fam, "beispiel"); w.Show(Fam); Settle(); // Schutz an (Standard), Lebende im Dokument
            Assert.True(w.FamProtected(d));
            var bad = Path.Combine(w.ClaudeChatRoot(Studio), "ein-projekt", "unter", "x.tdoc");
            w.ProgramSave(Fam, bad);
            Assert.False(File.Exists(bad));
            Assert.False(Inside(ClaudeRoot(w), d.Path), d.Path);
            Assert.Contains("liegt jetzt in", w.ProgramMessage(Fam));
            Assert.Equal(7, FamilyKit.FromJson(ProgramDocument.Load(d.Path!).Data).Persons.Count);
            // Gegenprobe: ohne Schutz darf die Person dort speichern
            w.FamSetOption(d, "hideLiving", "false");
            w.ProgramSave(Fam, bad);
            Assert.True(File.Exists(bad));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0788 R4 NIEDRIG: Verlegen scheitert (Ziel nicht schreibbar) – Schutz gilt trotzdem, nichts geht verloren, die Meldung sagt, dass die Kopie noch in Claudes Ordner liegt")]
    public async Task Move_fails_honestly()
    {
        var folders = ClaudeOn(); MainWindow? w = null; string? blocker = null;
        try
        {
            (w, var d, var folder) = await ChatOnUnprotected(folders);
            var p = d.Path!;
            blocker = Path.Combine(StudioFolder(w), DevStudio.SafeName(d.Title) + ProgramDocument.Extension);
            Directory.CreateDirectory(blocker); // ein Ordner mit dem Namen des neuen Ziels: Schreiben dort geht nicht (wie bei vollem Datenträger)
            w.FamSetOption(d, "hideLiving", "true"); w.Show(Fam); Settle();
            Assert.True(MainWindow.FamData(d).HideLiving); Assert.True(w.FamClaudeBlocked());
            Assert.Equal(p, d.Path); Assert.True(File.Exists(p));
            Assert.Equal(7, MainWindow.FamData(d).Persons.Count);
            Assert.Contains("Kopie liegt noch in Claudes Ordner", w.ProgramMessage(Fam));
        }
        finally { if (blocker is not null && Directory.Exists(blocker)) Directory.Delete(blocker); Off(); w?.Close(); }
    }
}
