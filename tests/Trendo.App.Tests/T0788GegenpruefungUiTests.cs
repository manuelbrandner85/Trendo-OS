using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Trendo.Core.Wissen;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0788 Gegenprüfung: Fund 1 (KI und Claude sehen keine Lebenden, solange der Schutz an ist), Fund 2 (Prüfung je Dokumentstand nur einmal),
/// Fund 3 (großer Import nicht im UI-Thread). KI ersetzt.</summary>
[Collection("Claude")]
public class T0788Gegenpruefung_Ui
{
    const string Fam = MainWindow.FamKey;

    sealed class FixedKi(Func<string, string> reply) : IKiBackend
    {
        public string Name => "Ersatz-KI"; public string BaseUrl => "http://127.0.0.1:9";
        public readonly List<string> All = [];
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["ersatz"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { All.AddRange(messages.Select(m => m.Content)); return Task.FromResult(reply(messages[^1].Content)); }
    }

    static async Task Until(Func<bool> ok, int ms = 15000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }
    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); RealClick(w, b); Settle(); }

    // im Beispiel lebend: Erika Muster (15 AUG 1952, Neustadt), Paul Muster (1955)
    static readonly string[] Living = ["Erika", "Paul", "15 AUG 1952", "1955"];

    [AvaloniaFact(DisplayName = "T-0788 G1: Die KI-Kette sieht im Stammbaum keine lebenden Personen (Name, Datum), solange „Lebende ausblenden“ an ist – ohne Schutz sieht sie alles")]
    public async Task Ki_sees_no_living()
    {
        var (w, _, _) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var ki = new FixedKi(_ => new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["action"] = "stammbaum.personen", ["args"] = new JsonObject { ["personen"] = "Otto Muster | m | Ostern" } }) }.ToJsonString()); // unlesbar → Phase „fehler“, Dokument bleibt
        try
        {
            w.KiBackendsOverride = () => [ki];
            w.Width = 1600; w.Height = 1400; w.ProgramSetLevel(UserLevel.Profi);
            w.Show(Fam); Settle(); Tap(w, "prog-tpl-beispiel");
            var d = w.ProgramActive(Fam)!;
            w.Find<TextBox>("auftrag-prompt").Text = "Füge die Personen aus diesem Text ein: Otto Muster wurde Ostern geboren.";
            Tap(w, "auftrag-pruefen");
            await Until(() => ki.All.Count > 0 && w.AssistLast?.Phase is "fehler" or "vorschau");
            var seen = string.Join("\n", ki.All);
            Assert.Contains("Karl", seen); Assert.Contains("12 MAR 1890", seen); // Verstorbene sieht die KI
            foreach (var s in Living) Assert.DoesNotContain(s, seen);
            Assert.Contains("Erika", d.Data.ToJsonString()); // das Dokument selbst bleibt vollständig
            // Schutz aus → die KI sieht alles
            w.FamSetOption(d, "hideLiving", "false"); ki.All.Clear(); w.Show(Fam); Settle();
            w.Find<TextBox>("auftrag-prompt").Text = "Füge die Personen aus diesem Text ein: Otto Muster wurde zu Ostern geboren.";
            Tap(w, "auftrag-pruefen");
            await Until(() => ki.All.Count > 0);
            Assert.Contains("Erika", string.Join("\n", ki.All));
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0788 G1: Claude-Brücke „dokument_lesen“ gibt bei Lebenden und Schutz an nichts heraus (ganz und für Teilpfade)")]
    public async Task Bridge_read_private()
    {
        var (w, _, _) = Ui.Open(); var dir = Path.Combine(Path.GetTempPath(), "trendo-t0788-bridge-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            w.Width = 1600; w.Height = 1400; w.ProgramSetLevel(UserLevel.Profi);
            w.Show(Fam); Settle(); Tap(w, "prog-tpl-beispiel");
            var m = w.ClaudeBridgeForTest(dir, Fam);
            async Task<string> Read(string path)
            {
                var r = await m.HandleAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call", ["params"] = new JsonObject { ["name"] = "dokument_lesen", ["arguments"] = new JsonObject { ["programm"] = Fam, ["pfad"] = path } } });
                return (string)r!["result"]!["content"]![0]!["text"]!;
            }
            // seit dem Nachtrag zur Gegenprüfung: mit Lebenden und Schutz an bekommt Claude das Dokument gar nicht (ehrliche Meldung) – ganz und für Teilpfade
            var all = await Read("");
            Assert.StartsWith("Lebende Personen sind geschützt", all);
            foreach (var s in Living.Append("Karl")) Assert.DoesNotContain(s, all);
            var one = await Read("persons/4"); // im Dokument: Erika
            Assert.DoesNotContain("Erika", one); Assert.StartsWith("Lebende Personen sind geschützt", one);
        }
        finally { w.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [AvaloniaFact(DisplayName = "T-0788 G2: Prüfung läuft je Dokumentstand nur einmal (Statusleiste und Prüfbereich teilen das Ergebnis)")]
    public void Check_cached()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Width = 1600; w.Height = 1400; w.ProgramSetLevel(UserLevel.Profi);
            w.Show(Fam); Settle(); Tap(w, "prog-tpl-beispiel");
            var d = w.ProgramActive(Fam)!;
            var n0 = MainWindow.FamChecks; w.Show(Fam); Settle(); w.Show(Fam); Settle();
            Assert.Equal(n0, MainWindow.FamChecks); // gleicher Stand: nicht neu gerechnet
            w.FamSet(d, "I1", "death", "1950"); w.Show(Fam); Settle();
            Assert.Equal(n0 + 1, MainWindow.FamChecks); // neuer Stand: genau einmal
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0788 G3: GEDCOM-Import liest im Hintergrund – das Fenster bleibt bedienbar, danach steht alles im Dokument")]
    public async Task Import_in_background()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            w.Width = 1600; w.Height = 1400; w.ProgramSetLevel(UserLevel.Profi);
            Directory.CreateDirectory(root);
            var sb = new System.Text.StringBuilder("0 HEAD\r\n1 CHAR UTF-8\r\n");
            for (var i = 0; i < 20_000; i++) sb.Append($"0 @I{i}@ INDI\r\n1 NAME Person{i} /Muster/\r\n1 BIRT\r\n2 DATE {1700 + i % 200}\r\n");
            sb.Append("0 TRLR\r\n");
            var ged = Path.Combine(root, "gross.ged"); File.WriteAllText(ged, sb.ToString());
            w.Show(Fam); Settle(); Tap(w, "prog-tpl-leer");
            var d = w.ProgramActive(Fam)!;
            w.PickOverride = _ => Task.FromResult<string[]?>([ged]);
            MainWindow.FamImportOnUiThread = null;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Tap(w, "prog-cmd-fam-import");
            var tap = sw.Elapsed;
            await Until(() => MainWindow.FamData(d).Persons.Count == 20_000, 60000);
            Assert.Contains("eingelesen", w.ProgramMessage(Fam));
            Assert.False(MainWindow.FamImportOnUiThread); // gelesen und zerlegt wurde nicht im UI-Thread
            Assert.True(tap < TimeSpan.FromSeconds(10), tap.ToString());
        }
        finally { w.PickOverride = null; w.Close(); }
    }
}
