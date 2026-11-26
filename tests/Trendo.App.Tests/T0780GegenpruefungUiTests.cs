using System.Text;
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

/// <summary>T-0780, Gegenprüfung: H1–H4 und M1 am echten Programm – Claude-Brücke, Assistent („dokument.setzen“), Zeichnen und Einlesen. Jeder Test schlägt auf 11c40362d fehl.</summary>
[Collection("Claude")]
public class T0780Gegenpruefung_Ui
{
    static async Task<T> Run<T>(Task<T> t) { for (var i = 0; i < 3000 && !t.IsCompleted; i++) { Settle(); await Task.Delay(2); } Settle(); return await t; }
    static async Task<JsonObject> Call(TrendoMcp m, string tool, JsonObject? args = null) =>
        (await Run(m.HandleAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call", ["params"] = new JsonObject { ["name"] = tool, ["arguments"] = args ?? [] } })))!["result"]!.AsObject();
    static bool Failed(JsonObject r) => (bool)r["isError"]!;
    static string Text(JsonObject r) => (string)r["content"]![0]!["text"]!;
    static string Folder() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0780g-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    sealed class FixedKi(Func<string, string> reply) : IKiBackend
    {
        public string Name => "Ersatz-KI"; public string BaseUrl => "http://127.0.0.1:9";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["ersatz"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) => Task.FromResult(reply(messages[^1].Content));
    }

    [AvaloniaFact(DisplayName = "T-0780 H3: Claude-Brücke hält KiLocked ein – „sources“ (Referate) und „folder“ (Dokumente scannen) setzt Claude nicht, freie Felder weiter schon")]
    public async Task Bridge_respects_locked_fields()
    {
        var (w, _, _) = Ui.Open(); var folder = Folder();
        try
        {
            var m = w.ClaudeBridgeForTest(folder);
            Assert.False(Failed(await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = MainWindow.RefKey, ["vorlage"] = "beispiel" })));
            var d = w.ProgramActive(MainWindow.RefKey)!; var before = d.Data.ToJsonString();
            var r = await Call(m, "dokument_setzen", new JsonObject { ["pfad"] = "sources/2", ["wert"] = new JsonObject { ["id"] = "q-neu", ["kind"] = "buch", ["title"] = "Erfunden", ["year"] = "2021" } });
            Assert.True(Failed(r)); Assert.Contains("sources", Text(r));
            Assert.True(Failed(await Call(m, "dokument_setzen", new JsonObject { ["pfad"] = " Sources /0/title", ["wert"] = "Anders" })));
            // gesperrtes Feld als Schlüssel im geschriebenen Wert (ganzes Dokument-Teilstück mit „sources“) – Sperre aus T-0779 (Session.LockedField)
            Assert.True(Failed(await Call(m, "dokument_setzen", new JsonObject { ["pfad"] = "claude", ["wert"] = new JsonObject { ["sources"] = new JsonArray() } })));
            Assert.Equal(before, d.Data.ToJsonString());
            Assert.False(Failed(await Call(m, "dokument_setzen", new JsonObject { ["pfad"] = "subtitle", ["wert"] = "Neu" })));
            Assert.False(Failed(await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = MainWindow.ScanKey, ["vorlage"] = "leer" })));
            var s = w.ProgramActive(MainWindow.ScanKey)!; var sb = s.Data.ToJsonString();
            Assert.True(Failed(await Call(m, "dokument_setzen", new JsonObject { ["pfad"] = "folder", ["wert"] = @"\\rechner\freigabe" })));
            Assert.Equal(sb, s.Data.ToJsonString());
        }
        finally { w.Close(); try { Directory.Delete(folder, true); } catch (IOException) { } }
    }

    [AvaloniaFact(DisplayName = "T-0780 H1/H2: Claude-Brücke und Assistent („dokument.setzen“) prüfen Zitate wie die Fachaktion – freier Kurzbeleg und unbekannter Verweis abgelehnt, Verweis auf eigene Quelle übernommen")]
    public async Task Bridge_and_assistant_check_citations()
    {
        var (w, _, _) = Ui.Open(); var folder = Folder(); MainWindow.AssistStepDelayMs = 0;
        var reply = "";
        try
        {
            var m = w.ClaudeBridgeForTest(folder);
            Assert.False(Failed(await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = MainWindow.RefKey, ["vorlage"] = "beispiel" })));
            var d = w.ProgramActive(MainWindow.RefKey)!; var before = d.Data.ToJsonString();
            var r = await Call(m, "dokument_setzen", new JsonObject { ["pfad"] = "sections/3/text", ["wert"] = "Mulch spart 70 % Wasser (vgl. Erfunden 2021: 5)." });
            Assert.True(Failed(r)); Assert.Contains("Erfunden 2021", Text(r));
            Assert.True(Failed(await Call(m, "dokument_setzen", new JsonObject { ["pfad"] = "sections/3/text", ["wert"] = "Mulch spart Wasser [ @neu]." })));
            Assert.True(Failed(await Call(m, "dokument_setzen", new JsonObject { ["pfad"] = "sections/3", ["wert"] = new JsonObject { ["id"] = "s4", ["level"] = 2, ["title"] = "Mulchen (vgl. Erfunden 2021)", ["text"] = "x" } })));
            Assert.Equal(before, d.Data.ToJsonString());
            Assert.False(Failed(await Call(m, "dokument_setzen", new JsonObject { ["pfad"] = "sections/3/text", ["wert"] = "Mulch hält den Boden feucht [@q-buch, S. 20]." })));
            Assert.Equal("Mulch hält den Boden feucht [@q-buch, S. 20].", MainWindow.RefSections(d)[3].Text);

            // Assistent: derselbe Weg über „dokument.setzen“ aus dem KI-Plan
            w.KiBackendsOverride = () => [new FixedKi(_ => reply)];
            w.Width = 1600; w.Height = 1100; w.ProgramSetLevel(UserLevel.Profi); w.Show(MainWindow.RefKey); Settle();
            var b2 = d.Data.ToJsonString();
            reply = new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["action"] = AssistChain.EditAction, ["args"] = new JsonObject { ["pfad"] = "sections/0/text", ["wert"] = "Viele Gärten brauchen Wasser (Erfunden, 2021; Ander, 2019)." } }) }.ToJsonString();
            w.Find<TextBox>("auftrag-prompt").Text = "Ergänze in der Einleitung einen Beleg zum Wasserverbrauch von Gärten bitte";
            RealClick(w, w.Find<Button>("auftrag-pruefen"));
            for (var i = 0; i < 500 && w.AssistLast?.Phase is not ("vorschau" or "fehler"); i++) { Settle(); await Task.Delay(10); }
            Assert.Equal("fehler", w.AssistLast!.Value.Phase);
            Assert.Contains("Erfunden, 2021", w.AssistLast.Value.Message);
            Assert.Equal(b2, d.Data.ToJsonString());
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.Close(); try { Directory.Delete(folder, true); } catch (IOException) { } }
    }

    [AvaloniaFact(DisplayName = "T-0780 H4: ungültige Farbe im Modell – Zeichenfläche und PNG-Ausgabe brechen nicht ab, Standardfarbe statt Fehler")]
    public void Canvas_bad_color_no_crash()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            w.Width = 1400; w.Height = 900; w.Show(MainWindow.TlKey); Settle(); RealClick(w, w.Find<Button>("prog-tpl-beispiel")); Settle();
            var canvas = w.Find<TimelineCanvas>("tl-canvas");
            var t = MainWindow.TlData(w.ProgramActive(MainWindow.TlKey)!);
            t.Tracks[0] = t.Tracks[0] with { Color = "keine-farbe" };
            t.Items[0] = t.Items[0] with { Color = "#fff\" onload=\"x" };
            canvas.Data = t; canvas.InvalidateVisual(); canvas.InvalidateMeasure();
            Settle(); Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Settle();
            Directory.CreateDirectory(root);
            var png = TimelineCanvas.SavePng(TimelineKit.Layout(t, -800, 1600, 1200), Path.Combine(root, "z.png"));
            Assert.True(new FileInfo(png).Length > 1000);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0780 H1 (2. Gegenprüfung): Claude-Brücke – „befehl“, „klicken“ und „feld_setzen“ kommen an Quellen-Sperre, Zitatprüfung und Ordner-Sperre nicht vorbei; Schritt wird zurückgerollt und Claude bekommt den Fehler")]
    public async Task Bridge_every_step_checked()
    {
        var (w, _, _) = Ui.Open(); var folder = Folder();
        try
        {
            w.Width = 1600; w.Height = 1100; w.ProgramSetLevel(UserLevel.Profi);
            var m = w.ClaudeBridgeForTest(folder);
            Assert.False(Failed(await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = MainWindow.RefKey, ["vorlage"] = "beispiel" })));
            var d = w.ProgramActive(MainWindow.RefKey)!; var before = d.Data.ToJsonString(); var undo = d.History.Count;
            // 1. befehl „Buch“ legt eine Quelle an → gesperrt
            var r = await Call(m, "befehl", new JsonObject { ["name"] = "ref-src-buch" });
            Assert.True(Failed(r)); Assert.Contains("Zurückgenommen", Text(r));
            Assert.Equal(before, d.Data.ToJsonString()); Assert.Equal(undo, d.History.Count);
            // 2. klicken auf „+ Buch“ unter Literatur → gesperrt
            w.RefSetStep(d, 3); w.Show(MainWindow.RefKey); Settle();
            Assert.True(Failed(await Call(m, "klicken", new JsonObject { ["ziel"] = "ref-srcadd-buch" })));
            Assert.Equal(2, MainWindow.RefSources(w.ProgramActive(MainWindow.RefKey)!).Count);
            // 3. feld_setzen in den Abschnittstext mit erfundenem Beleg → gesperrt, Text unverändert
            w.RefSelect(d, MainWindow.RefSections(d)[3].Id); w.RefSetStep(d, 2); w.Show(MainWindow.RefKey); Settle();
            r = await Call(m, "feld_setzen", new JsonObject { ["ziel"] = "ref-sec-text", ["wert"] = "Mulch spart 70 % Wasser (vgl. Erfunden 2021: 5)." });
            Assert.True(Failed(r)); Assert.Contains("Erfunden 2021", Text(r));
            Assert.Equal("Eine Schicht aus Rasenschnitt oder Laub hält den Boden feucht.", MainWindow.RefSections(w.ProgramActive(MainWindow.RefKey)!)[3].Text);
            // Gegenprobe: derselbe Weg mit Verweis auf eine eigene Quelle wird übernommen (das Feld wird wirklich übernommen, nicht nur angezeigt)
            w.Show(MainWindow.RefKey); Settle();
            Assert.False(Failed(await Call(m, "feld_setzen", new JsonObject { ["ziel"] = "ref-sec-text", ["wert"] = "Mulch hält den Boden feucht [@q-buch, S. 20]." })));
            Assert.Equal("Mulch hält den Boden feucht [@q-buch, S. 20].", MainWindow.RefSections(w.ProgramActive(MainWindow.RefKey)!)[3].Text);
            // 4. Scannen: Ablage-Ordner per feld_setzen → gesperrt
            Assert.False(Failed(await Call(m, "programm_oeffnen", new JsonObject { ["programm"] = MainWindow.ScanKey, ["vorlage"] = "leer" })));
            // mit einem Foto (sonst steht nur der erste Schritt da und die Felder sind gesperrt)
            var foto = Path.Combine(folder, "seite.png"); File.WriteAllBytes(foto, Trendo.Core.Kreativ.PngCodec.Encode(Trendo.Core.Office.DocScan.TestPhoto(600, 450, [(130, 45), (465, 75), (505, 410), (85, 380)])));
            w.PickOverride = _ => Task.FromResult<string[]?>([foto]); w.Show(MainWindow.ScanKey); Settle();
            Assert.False(Failed(await Call(m, "befehl", new JsonObject { ["name"] = "scan-add" }))); for (var i = 0; i < 50; i++) Settle(); w.PickOverride = null;
            var s = w.ProgramActive(MainWindow.ScanKey)!; var sb = s.Data.ToJsonString();
            w.Show(MainWindow.ScanKey); Settle();
            r = await Call(m, "feld_setzen", new JsonObject { ["ziel"] = "scan-folder-box", ["wert"] = @"\\rechner\freigabe" });
            Assert.True(Failed(r), Text(r)); // abgelehnt: das Pfadfeld ist nur über „Ordner wählen …“ änderbar, und jede Änderung an „folder“ würde am Schritt-Ende zurückgerollt
            Assert.Equal(sb, w.ProgramActive(MainWindow.ScanKey)!.Data.ToJsonString());
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.zurueckgerollt");
        }
        finally { w.Close(); try { Directory.Delete(folder, true); } catch (IOException) { } }
    }

    [AvaloniaFact(DisplayName = "T-0780 M2 (2. Gegenprüfung): unechtes PNG mit gültigem Kopf – Import verwirft und meldet es, PNG-Export und Zeichenfläche laufen weiter")]
    public void Fake_png_does_not_break()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var fake = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.Concat(Enumerable.Repeat((byte)0x41, 400)).ToArray();
            Assert.True(TimelineKit.ValidImage(fake)); // Kopf allein genügt nicht …
            Assert.Null(TimelineCanvas.Reencode(fake)); // … erst das Dekodieren entscheidet
            w.Width = 1400; w.Height = 900; w.Show(MainWindow.TlKey); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            var d = w.ProgramActive(MainWindow.TlKey)!;
            Directory.CreateDirectory(root);
            var json = new JsonObject { ["title"] = "x", ["tracks"] = new JsonArray(new JsonObject { ["id"] = "t1", ["name"] = "A", ["color"] = "#D4A017" }),
                ["items"] = new JsonArray(new JsonObject { ["id"] = "e1", ["title"] = "Kaputt", ["start"] = "1789", ["track"] = "t1", ["image"] = Convert.ToBase64String(fake) }) };
            var f = Path.Combine(root, "fake.json"); File.WriteAllText(f, json.ToJsonString());
            Assert.Equal(1, w.TlImport(d, f));
            Assert.Null(MainWindow.TlData(d).Items.Single().Image);
            Assert.Contains("Bild", w.ProgramMessage(MainWindow.TlKey));
            // steht das unechte Bild schon im Dokument (alte Datei), brechen PNG-Export und Zeichnen nicht ab
            var t = MainWindow.TlData(d); t.Items[0] = t.Items[0] with { Image = fake };
            var png = TimelineCanvas.SavePng(TimelineKit.Layout(t, 1700, 1900, 1200), Path.Combine(root, "z.png"));
            Assert.True(new FileInfo(png).Length > 1000);
            var canvas = w.Find<TimelineCanvas>("tl-canvas"); canvas.Data = t; canvas.InvalidateVisual();
            Settle(); Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Settle();
            TimelineKit.ToPdf(TimelineKit.Layout(t, 1700, 1900, 1200), Path.Combine(root, "z.pdf"));
            Assert.True(File.Exists(Path.Combine(root, "z.pdf")));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0780 M1: JSON-Datei mit Nicht-Bild als „image“ einlesen – Bild weggelassen und gemeldet, echtes Bild neu kodiert (JPEG)")]
    public void Import_reencodes_images()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            w.Width = 1400; w.Height = 900; w.Show(MainWindow.TlKey); Settle(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Settle();
            var d = w.ProgramActive(MainWindow.TlKey)!;
            Directory.CreateDirectory(root);
            var png = Trendo.Core.Kreativ.PngCodec.Encode(new Trendo.Core.Kreativ.Pixmap(40, 30, Enumerable.Repeat((byte)120, 40 * 30 * 4).ToArray()));
            var json = new JsonObject
            {
                ["format"] = "trendo-zeitleiste", ["title"] = "x",
                ["tracks"] = new JsonArray(new JsonObject { ["id"] = "t1", ["name"] = "A", ["color"] = "#D4A017" }),
                ["items"] = new JsonArray(
                    new JsonObject { ["id"] = "e1", ["title"] = "Echt", ["start"] = "1789", ["track"] = "t1", ["image"] = Convert.ToBase64String(png) },
                    new JsonObject { ["id"] = "e2", ["title"] = "Falsch", ["start"] = "1790", ["track"] = "t1", ["image"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("<svg onload=alert(1)>")) }),
            };
            var f = Path.Combine(root, "z.json"); File.WriteAllText(f, json.ToJsonString());
            Assert.Equal(2, w.TlImport(d, f));
            var items = MainWindow.TlData(d).Items;
            Assert.Equal(0xFF, items.Single(i => i.Title == "Echt").Image![0]); // neu kodiert als JPEG
            Assert.Null(items.Single(i => i.Title == "Falsch").Image);
            Assert.Contains("Bild", w.ProgramMessage(MainWindow.TlKey));
        }
        finally { w.Close(); }
    }
}
