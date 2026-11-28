using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Trendo.Core.Wissen;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0788, 3. Prüfrunde (HOCH): Löschen trifft nie die echte Datei der Person. Ein geschützter Stammbaum liegt nicht dauerhaft in Claudes Ordner –
/// er wird verlegt (neue Datei schreiben, prüfen, dann die alte von Trendo geschriebene entfernen); Strg+S schreibt bei Schutz nie in Claudes Ordner.
/// Eine Datei, deren Inhalt nicht mehr dem entspricht, was Trendo geschrieben hat, bleibt unverändert; was sich nicht löschen lässt, wird ehrlich gemeldet. Claude ersetzt.</summary>
[Collection("Claude")]
public class T0788Gegenpruefung4_Ui
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
    static string TdocText(string folder) => Directory.Exists(folder) ? string.Join("\n", Directory.EnumerateFiles(folder, "*" + ProgramDocument.Extension, SearchOption.AllDirectories).Select(File.ReadAllText)) : "";
    static bool Inside(string folder, string? file) => file is not null && Path.GetFullPath(file).StartsWith(Path.GetFullPath(folder).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);

    /// <summary>Beispiel ohne Schutz, Claude-Chat: Trendo legt das Dokument (wie seit T-0680) in Claudes Projektordner – doc.Path liegt dort.</summary>
    static async Task<(MainWindow W, ProgramDocument D, string Folder)> ChatOnUnprotected(List<string> folders, bool saveFirst = false)
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1400; w.ProgramSetLevel(UserLevel.Profi);
        await Run(w.ClaudeCheck(true));
        var d = w.ProgramNew(Fam, "beispiel"); w.Show(Fam); Settle();
        w.FamSetOption(d, "hideLiving", "false"); w.Show(Fam); Settle();
        if (saveFirst) w.ProgramSave(Fam);
        w.ClaudeDockToggle(Studio); Dispatcher.UIThread.RunJobs();
        await Run(w.ClaudeChatSend("Ergänze Erikas Geschwister"));
        for (var i = 0; i < 10; i++) Settle();
        return (w, w.ProgramActive(Fam)!, folders[0]);
    }

    [AvaloniaFact(DisplayName = "T-0788 R3 HOCH: eigenes Dokument in Claudes Ordner → Schutz an → Strg+S: in Claudes Ordner keine Lebenden, Dokument am neuen Ort vollständig, Rückgängig geht")]
    public async Task Own_saved_protect_save()
    {
        var folders = ClaudeOn(); MainWindow? w = null;
        try
        {
            (w, var d, var folder) = await ChatOnUnprotected(folders);
            Assert.True(Inside(folder, d.Path), $"Vorbedingung: Dokument liegt in Claudes Ordner ({d.Path})");
            w.FamSetOption(d, "hideLiving", "true"); w.Show(Fam); Settle();
            Assert.False(Inside(folder, d.Path), d.Path);
            Assert.Contains("liegt jetzt in", w.ProgramMessage(Fam));
            w.ProgramSave(Fam); // Strg+S
            Assert.False(Inside(folder, d.Path));
            var text = TdocText(folder);
            foreach (var s in Secret) Assert.DoesNotContain(s, text);
            var moved = FamilyKit.FromJson(ProgramDocument.Load(d.Path!).Data);
            Assert.Equal(7, moved.Persons.Count); Assert.Contains(moved.Persons, p => p.Given == "Erika"); Assert.True(moved.HideLiving);
            // Rückgängig geht weiter (Schutz wieder aus)
            Assert.True(d.CanUndo); Assert.True(d.Undo()); Assert.False(MainWindow.FamData(d).HideLiving);
        }
        finally { Off(); w?.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0788 R3 HOCH: gleichnamige eigene Datei mit anderem Inhalt in Claudes Ordner bleibt beim Einschalten des Schutzes unverändert – ehrliche Meldung")]
    public async Task Foreign_same_name_kept()
    {
        var folders = ClaudeOn(); MainWindow? w = null;
        try
        {
            (w, var d, var folder) = await ChatOnUnprotected(folders);
            var file = d.Path!;
            const string mine = "Meine eigene Notiz – nicht von Trendo";
            File.WriteAllText(file, mine); // die Person legt dort eine eigene Datei mit gleichem Namen ab
            w.FamSetOption(d, "hideLiving", "true"); w.Show(Fam); Settle();
            Assert.Equal(mine, File.ReadAllText(file));
            Assert.False(Inside(folder, d.Path));
            Assert.Contains("konnte nicht gelöscht werden", w.ProgramMessage(Fam));
            Assert.Contains("bitte schließen bzw. selbst löschen", w.ProgramMessage(Fam));
        }
        finally { Off(); w?.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0788 R3 NIEDRIG: Arbeitskopie lässt sich nicht löschen (schreibgeschützt) – Meldung mit Namen statt still schlucken; gespeichertes Dokument bleibt, wo es war")]
    public async Task Undeletable_reported()
    {
        var folders = ClaudeOn(); MainWindow? w = null; string? work = null;
        try
        {
            (w, var d, var folder) = await ChatOnUnprotected(folders, saveFirst: true); // eigenes Dokument im Studio-Ordner → Arbeitskopie ist eine eigene Datei
            Assert.False(Inside(folder, d.Path));
            work = Directory.EnumerateFiles(folder, "*" + ProgramDocument.Extension).Single();
            File.SetAttributes(work, FileAttributes.ReadOnly);
            w.FamSetOption(d, "hideLiving", "true"); w.Show(Fam); Settle();
            Assert.True(File.Exists(work));
            Assert.Contains($"Arbeitskopie konnte nicht gelöscht werden: {Path.GetFileName(work)}", w.ProgramMessage(Fam));
            File.SetAttributes(work, FileAttributes.Normal);
            // Gegenprobe: ohne Schreibschutz wird dieselbe Kopie beim nächsten Einschalten gelöscht
            w.FamSetOption(d, "hideLiving", "false"); w.Show(Fam); Settle();
            await Run(w.ClaudeChatSend("Noch einmal"));
            w.FamSetOption(d, "hideLiving", "true"); w.Show(Fam); Settle();
            Assert.False(File.Exists(work), w.ProgramMessage(Fam) + " | " + string.Join(", ", w.Core.Audit.ReadAll().TakeLast(8).Select(a => a.Action + " " + a.Detail)));
        }
        finally { if (work is not null && File.Exists(work)) File.SetAttributes(work, FileAttributes.Normal); Off(); w?.Close(); }
    }
}
