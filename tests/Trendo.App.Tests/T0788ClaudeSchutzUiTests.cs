using System.Text.Json.Nodes;
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

/// <summary>T-0788 (Nachtrag zur Gegenprüfung): Solange im offenen Stammbaum „Lebende ausblenden“ an ist und das Dokument lebende Personen enthält,
/// bekommt Claude das Dokument auf keinem Weg – weder über den Chat mit der Studio-.tdoc (hin und zurück), noch über Plan-Zeilen (ClaudeRunPlan), noch über die
/// Brücke (dokument_lesen/dokument_setzen). Stattdessen kommt der ehrliche Satz. Schutz aus oder keine Lebenden: erlaubt. Claude ist ersetzt (kein Echtlauf).</summary>
[Collection("Claude")]
public class T0788ClaudeSchutz_Ui
{
    const string Fam = MainWindow.FamKey, Studio = MainWindow.FamStudio;
    const string Why = "Lebende Personen sind geschützt";

    static async Task Run(Task t) { while (!t.IsCompleted) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static ProgramDocument Beispiel(MainWindow w)
    {
        w.Width = 1600; w.Height = 1400; w.ProgramSetLevel(UserLevel.Profi); w.Show(Fam); Settle();
        RealClick(w, w.Find<Button>("prog-tpl-beispiel")); Settle();
        return w.ProgramActive(Fam)!;
    }

    sealed class FakeClaude
    {
        public readonly List<string> Folders = [];
        public Func<string, string> Reply = _ => "ok";
        public Action<string>? During;
        public int Calls => Folders.Count;
        public void On()
        {
            MainWindow.ClaudeStatusOverride = () => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe"));
            MainWindow.ClaudeStepDelay = 0;
            MainWindow.ClaudeRunOverride = (args, cwd, line, _) =>
            {
                Folders.Add(cwd); During?.Invoke(cwd);
                var text = Reply(cwd);
                line(System.Text.Json.JsonSerializer.Serialize(new { type = "system", subtype = "init", session_id = "s1", model = "claude" }));
                line(System.Text.Json.JsonSerializer.Serialize(new { type = "assistant", message = new { content = new object[] { new { type = "text", text } } } }));
                line(System.Text.Json.JsonSerializer.Serialize(new { type = "result", subtype = "success", is_error = false, result = text, session_id = "s1", num_turns = 1 }));
                return Task.FromResult(0);
            };
        }
        public static void Off() { MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; MainWindow.ClaudeStepDelay = 350; }
    }

    static string? Tdoc(string folder) => Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*" + ProgramDocument.Extension).FirstOrDefault() : null;

    [AvaloniaFact(DisplayName = "T-0788 Claude-Schutz: Chat mit Studio-.tdoc – Schutz an + Lebende: kein Claude-Aufruf, keine .tdoc, ehrliche Meldung; Schutz aus: Claude bekommt die .tdoc")]
    public async Task Chat_tdoc_refused_then_allowed()
    {
        var (w, _, _) = Ui.Open(); var claude = new FakeClaude();
        try
        {
            claude.On(); await Run(w.ClaudeCheck(true));
            var d = Beispiel(w);
            // 1) Schutz an: der Claude-Bereich geht gar nicht erst auf, die Meldung nennt den Grund
            w.ClaudeDockToggle(Studio); Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Ergänze die Geschwister von Erika"));
            Assert.Equal(0, claude.Calls);
            Assert.StartsWith(Why, w.ProgramMessage(Fam));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.dock.lokal");
            // 2) Schutz aus → Claude bekommt das Dokument als .tdoc
            w.FamSetOption(d, "hideLiving", "false"); w.Show(Fam); Settle();
            w.ClaudeDockToggle(Studio); Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Ergänze die Geschwister von Erika"));
            Assert.Equal(1, claude.Calls);
            Assert.Contains("Erika", File.ReadAllText(Tdoc(claude.Folders[0])!));
            // 3) Bereich ist offen, Schutz wieder an → der nächste Chat geht nicht an Claude, keine neue .tdoc
            w.FamSetOption(w.ProgramActive(Fam)!, "hideLiving", "true"); w.Show(Fam); Settle();
            Assert.Null(Tdoc(claude.Folders[0])); // T-0788 (2. Prüfrunde): Schutz an löscht die frühere Arbeitskopie
            await Run(w.ClaudeChatSend("Und die Cousins"));
            Assert.Equal(1, claude.Calls);
            Assert.Null(Tdoc(claude.Folders[0])); // und es entsteht keine neue
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.chat.lokal");
            Assert.StartsWith(Why, w.LocalOnlyRefusal(Fam));
        }
        finally { FakeClaude.Off(); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0788 Claude-Schutz: Claudes .tdoc-Änderung wird nicht übernommen, wenn während des Laufs der Schutz eingeschaltet wurde (Rückweg des Abgleichs)")]
    public async Task Tdoc_sync_back_refused()
    {
        var (w, _, _) = Ui.Open(); var claude = new FakeClaude();
        try
        {
            claude.On(); await Run(w.ClaudeCheck(true));
            var d = Beispiel(w);
            w.FamSetOption(d, "hideLiving", "false"); w.Show(Fam); Settle();
            w.ClaudeDockToggle(Studio); Dispatcher.UIThread.RunJobs();
            claude.During = cwd =>
            {
                // Claude schreibt in die .tdoc (Geburtsjahr von Erika geändert); gleichzeitig schaltet die Person den Schutz ein
                var f = Tdoc(cwd)!; var doc = ProgramDocument.Load(f);
                var t = FamilyKit.FromJson(doc.Data); t.P("I5")!.Ev("BIRT")!.Date = "1953"; t.HideLiving = false;
                doc.ReplaceData(FamilyKit.ToJson(t).ToJsonString()); doc.Save(f, keepVersion: false, overwriteChanged: true);
                w.FamSetOption(w.ProgramActive(Fam)!, "hideLiving", "true");
            };
            await Run(w.ClaudeChatSend("Korrigiere Erikas Geburtsjahr"));
            for (var i = 0; i < 30; i++) Settle();
            Assert.Equal(1, claude.Calls);
            Assert.Equal("15 AUG 1952", MainWindow.FamData(w.ProgramActive(Fam)!).P("I5")!.Birth!.Date);
            // T-0788 (2. Prüfrunde): das Einschalten des Schutzes löscht Claudes Arbeitskopie sofort – nichts wird übernommen, nichts zurückgeschrieben
            Assert.Null(Tdoc(claude.Folders[0]));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.tdoc.geloescht");
        }
        finally { FakeClaude.Off(); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0788 Claude-Schutz: Plan-Zeilen (ClaudeRunPlan) im geschützten Stammbaum werden nicht ausgeführt; Schutz aus: ausgeführt")]
    public async Task Plan_refused_then_allowed()
    {
        var (w, _, _) = Ui.Open();
        var claude = new FakeClaude { Reply = _ => "Ich lege eine Person an.\n[[trendo:klick | prog-cmd-fam-add-person]]\n[[trendo:feld | prog-saveas-path | x]]" };
        try
        {
            claude.On(); await Run(w.ClaudeCheck(true));
            var d = Beispiel(w);
            int Persons() => MainWindow.FamData(w.ProgramActive(Fam)!).Persons.Count;
            int Refusals() => w.Core.Audit.ReadAll().Count(a => a.Action == "claude.plan.lokal");
            // 1) Plan entsteht in einem anderen Programm (dort ist Claude erlaubt), ausgeführt werden soll er im geschützten Stammbaum → abgelehnt
            var other = MainWindow.Programs.Keys.First(k => MainWindow.Programs[k].Studio == "SPEC-01");
            w.Show(other); w.ProgramNew(other); w.Show(other); Settle();
            w.ClaudeDockToggle("SPEC-01"); Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Leg etwas an"));
            Assert.True(w.ClaudePlanPending());
            w.Show(Fam); Settle();
            await Run(w.ClaudeRunPlan());
            Assert.Equal(7, Persons());
            Assert.Equal(1, Refusals());
            Assert.Contains("Nicht ausgeführt", w.ClaudeChatCurrent()!.Messages[^1].Text);
            // 2) Schutz aus: Claude-Bereich im Stammbaum, Plan „Person anlegen“ → ausgeführt
            w.ClaudeDockToggle(null); Dispatcher.UIThread.RunJobs(); // Bereich des anderen Programms schließen
            w.FamSetOption(d, "hideLiving", "false"); w.Show(Fam); Settle();
            w.ClaudeDockToggle(Studio); Dispatcher.UIThread.RunJobs();
            await Run(w.ClaudeChatSend("Leg eine Person an"));
            Assert.True(w.ClaudePlanPending());
            await Run(w.ClaudeRunPlan());
            Assert.Equal(8, Persons());
            Assert.Equal(1, Refusals());
        }
        finally { FakeClaude.Off(); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0788 Claude-Schutz: Brücke dokument_lesen/dokument_setzen mit Lebenden und Schutz an abgelehnt; ohne Lebende oder mit Schutz aus erlaubt")]
    public async Task Bridge_refused_then_allowed()
    {
        var (w, _, _) = Ui.Open(); var dir = Path.Combine(Path.GetTempPath(), "trendo-t0788-schutz-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var d = Beispiel(w);
            var m = w.ClaudeBridgeForTest(dir, Fam);
            async Task<(bool Error, string Text)> Call(string tool, JsonObject args)
            {
                args["programm"] = Fam;
                var r = await m.HandleAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call", ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = args } });
                return ((bool?)r!["result"]!["isError"] ?? false, (string)r["result"]!["content"]![0]!["text"]!);
            }
            var (err, text) = await Call("dokument_lesen", new JsonObject { ["pfad"] = "" });
            Assert.True(err); Assert.StartsWith(Why, text); Assert.DoesNotContain("Karl", text);
            (err, _) = await Call("dokument_setzen", new JsonObject { ["pfad"] = "title", ["wert"] = "X" });
            Assert.True(err); Assert.Equal("Familie Muster", MainWindow.FamData(d).Title);
            // Schutz aus → erlaubt (vollständig)
            w.FamSetOption(d, "hideLiving", "false");
            (err, text) = await Call("dokument_lesen", new JsonObject { ["pfad"] = "" });
            Assert.False(err); Assert.Contains("Erika", text);
            // Schutz an, aber keine Lebenden im Dokument → erlaubt
            var e = w.ProgramNew(Fam, "leer"); w.Show(Fam); Settle();
            w.FamAddLines(e, "Otto Muster | m | 1820 | Musterstadt | 1890 | Musterstadt");
            Assert.True(MainWindow.FamData(e).HideLiving);
            (err, text) = await Call("dokument_lesen", new JsonObject { ["pfad"] = "" });
            Assert.False(err); Assert.Contains("Otto", text);
        }
        finally { w.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
