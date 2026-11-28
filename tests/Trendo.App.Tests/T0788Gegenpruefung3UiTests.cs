using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Trendo.Core.Wissen;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0788, 2. Prüfrunde (HOCH): Der .tdoc-Rückweg darf ein geschütztes Dokument nicht verraten – Abgleich an das Dokument gebunden, aus dem die Datei stammt;
/// bei Schutz wird nie zurückgeschrieben, die von Trendo angelegte Datei wird gelöscht. Geprüft wird der INHALT der Datei in Claudes Ordner. Claude ersetzt.</summary>
[Collection("Claude")]
public class T0788Gegenpruefung3_Ui
{
    const string Fam = MainWindow.FamKey, Studio = MainWindow.FamStudio;
    static readonly string[] Secret = ["Erika", "Karl", "15 AUG 1952"];

    static async Task Run(Task t) { while (!t.IsCompleted) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }

    static List<string> On(Action<string> during)
    {
        var folders = new List<string>();
        MainWindow.ClaudeStatusOverride = () => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
        MainWindow.ClaudeStepDelay = 0;
        MainWindow.ClaudeRunOverride = (args, cwd, line, _) =>
        {
            folders.Add(cwd); during(cwd);
            line(System.Text.Json.JsonSerializer.Serialize(new { type = "result", subtype = "success", is_error = false, result = "ok", session_id = "s1", num_turns = 1 }));
            return Task.FromResult(0);
        };
        return folders;
    }
    static void Off() { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; MainWindow.ClaudeStepDelay = 350; }

    /// <summary>Was in Claudes Ordner an .tdoc-Dateien liegt (Inhalt aller Dateien).</summary>
    static string TdocText(string folder) => Directory.Exists(folder) ? string.Join("\n", Directory.EnumerateFiles(folder, "*" + ProgramDocument.Extension).Select(File.ReadAllText)) : "";

    /// <summary>Claude ändert die .tdoc im Ordner (fügt eine Person hinzu), wie es das Studio-Dokument bearbeiten würde.</summary>
    static void ClaudeEdits(string folder)
    {
        var f = Directory.EnumerateFiles(folder, "*" + ProgramDocument.Extension).Single();
        var doc = ProgramDocument.Load(f); var t = FamilyKit.FromJson(doc.Data);
        var p = new FamPerson { Id = t.NewId("I"), Sex = "M" }; FamilyKit.SetName(p, "Neu", "Muster"); t.Persons.Add(p);
        doc.ReplaceData(FamilyKit.ToJson(t).ToJsonString()); doc.Save(f, keepVersion: false, overwriteChanged: true);
    }

    static MainWindow Window(out string root) { var (w, _, r) = Ui.Open(); root = r; w.Width = 1600; w.Height = 1400; w.ProgramSetLevel(UserLevel.Profi); return w; }

    [AvaloniaFact(DisplayName = "T-0788 R2 HOCH: Reiterwechsel während des Laufs (Chat auf Dokument nur mit Verstorbenen, dann wird der geschützte Stammbaum aktiv) – Claudes Ordner enthält danach keine Lebenden, der geschützte Stammbaum bleibt unverändert")]
    public async Task Tab_switch_during_run()
    {
        var w = Window(out _);
        ProgramDocument? a = null;
        var folders = On(cwd => { ClaudeEdits(cwd); a = w.ProgramNew(Fam, "beispiel"); w.Show(Fam); });
        try
        {
            await Run(w.ClaudeCheck(true));
            var b = w.ProgramNew(Fam, "leer"); w.Show(Fam); Settle();
            w.FamAddLines(b, "Otto Muster | m | 1820 | Musterstadt | 1890 | Musterstadt"); w.Show(Fam); Settle();
            w.ClaudeDockToggle(Studio); Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Ergänze Ottos Geschwister"));
            for (var i = 0; i < 30; i++) Settle();
            Assert.Single(folders);
            var text = TdocText(folders[0]);
            foreach (var s in Secret) Assert.DoesNotContain(s, text);
            Assert.Equal(7, MainWindow.FamData(a!).Persons.Count); // geschützter Stammbaum unverändert
            Assert.DoesNotContain(MainWindow.FamData(a!).Persons, p => p.Given == "Neu");
        }
        finally { Off(); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0788 R2 HOCH: Schutz wird während des Laufs eingeschaltet – Claudes Ordner enthält danach keine Lebenden, nichts wird übernommen")]
    public async Task Protection_on_during_run()
    {
        var w = Window(out _);
        ProgramDocument? d = null;
        string? claudeWrote = null;
        var folders = On(cwd => { ClaudeEdits(cwd); claudeWrote = TdocText(cwd); w.FamSetOption(d!, "hideLiving", "true"); });
        try
        {
            await Run(w.ClaudeCheck(true));
            d = w.ProgramNew(Fam, "beispiel"); w.Show(Fam); Settle();
            w.FamSetOption(d, "hideLiving", "false"); w.Show(Fam); Settle();
            w.ClaudeDockToggle(Studio); Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Ergänze Erikas Geschwister"));
            for (var i = 0; i < 30; i++) Settle();
            Assert.Single(folders);
            // T-0788 (3. Prüfrunde, Regel a): Claude hat die Datei geändert – sie ist nicht mehr, was Trendo geschrieben hat, und wird deshalb weder gelöscht noch
            // überschrieben. Prüfung: Trendo hat NICHTS dazugeschrieben (Inhalt genau Claudes Fassung, die Claude ohnehin hatte); die Person bekommt die Meldung.
            Assert.Equal(claudeWrote, TdocText(folders[0]));
            Assert.Contains("bitte schließen bzw. selbst löschen", w.ProgramMessage(Fam) + w.ClaudeMessage);
            Assert.DoesNotContain(MainWindow.FamData(d).Persons, p => p.Given == "Neu");
            Assert.DoesNotContain(folders[0], d.Path!); // das Dokument selbst liegt nicht mehr in Claudes Ordner
        }
        finally { Off(); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0788 R2 NIEDRIG: Schutz einschalten löscht die frühere Arbeitskopie dieses Stammbaums in Claudes Ordner (nur die von Trendo angelegte) und sagt es")]
    public async Task Protection_on_deletes_old_copy()
    {
        var w = Window(out _);
        var folders = On(_ => { });
        try
        {
            await Run(w.ClaudeCheck(true));
            var d = w.ProgramNew(Fam, "beispiel"); w.Show(Fam); Settle();
            w.FamSetOption(d, "hideLiving", "false"); w.Show(Fam); Settle();
            w.ClaudeDockToggle(Studio); Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Ergänze Erikas Geschwister"));
            for (var i = 0; i < 10; i++) Settle();
            var other = Path.Combine(folders[0], "notiz.txt"); File.WriteAllText(other, "von Claude"); // fremde Datei im Ordner bleibt
            Assert.Contains("Erika", TdocText(folders[0])); // Vorbedingung: Kopie liegt dort
            w.FamSetOption(d, "hideLiving", "true"); w.Show(Fam); Settle();
            Assert.Equal("", TdocText(folders[0]));
            Assert.True(File.Exists(other));
            Assert.Contains("liegt jetzt in", w.ProgramMessage(Fam)); // T-0788 (3. Prüfrunde): das Dokument selbst lag dort – es wurde verlegt, die alte (von Trendo geschriebene) Datei entfernt
        }
        finally { Off(); w.Close(); }
    }
}
