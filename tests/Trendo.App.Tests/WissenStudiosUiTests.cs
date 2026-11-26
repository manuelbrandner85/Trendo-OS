using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Office;
using Trendo.Core.Programs;
using Trendo.Core.Wissen;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0780 (Etappe 0.9.113 „Wissen &amp; Lernen“): Studios „Referate &amp; Hausarbeiten“ und „Zeitleisten“ vom Klick bis zur echten Datei,
/// Ziehen auf der Arbeitsfläche, Rechtsklick, Stufe Einfach, KI-Fachaktionen über die eine Kette (Vorschau → „Ja, behalten“ → ein Rückgängig-Schritt).
/// In den KI-Tests ist die KI ersetzt (feste Antwort); <see cref="Real_local_ki"/> läuft mit dem Ollama-Modell dieses PCs (nur mit TRENDO_T0780_ECHT=&lt;Belegordner&gt;).</summary>
[Collection("Claude")]
public class WissenStudios_Ui
{
    const string Ref = MainWindow.RefKey, Tl = MainWindow.TlKey;

    sealed class FixedKi(Func<string, string> reply) : IKiBackend
    {
        public string Name => "Ersatz-KI"; public string BaseUrl => "http://127.0.0.1:9";
        public readonly List<string> Systems = [];
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["ersatz"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Systems.Add(messages[0].Content); return Task.FromResult(reply(messages[^1].Content)); }
    }

    static string Plan(string action, JsonObject args) => new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["action"] = action, ["args"] = args }) }.ToJsonString();
    static async Task Until(Func<bool> ok, int ms = 10000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }
    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); RealClick(w, b); Settle(); }
    /// <summary>Tippen wie die Person: Feld fokussieren, Text setzen, Feld verlassen (Fokus weg) – die Felder übernehmen beim Verlassen.</summary>
    static void Type(MainWindow w, string name, string text) { var t = w.Find<TextBox>(name); t.BringIntoView(); t.Focus(); Settle(); t.Text = text; Settle(); w.Find<Button>("prog-undo").Focus(); Settle(); }
    static string Hash(string f) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant();
    static string Exported(MainWindow w, string key, string export)
    {
        Tap(w, "prog-export-" + export);
        var msg = w.ProgramMessage(key);
        Assert.StartsWith("Exportiert: ", msg);
        var f = msg["Exportiert: ".Length..].Trim();
        Assert.True(File.Exists(f), f);
        return f;
    }

    static async Task<string> Ask(MainWindow w, string key, string prompt, int ms = 10000)
    {
        var audits = w.Core.Audit.ReadAll().Count;
        bool NoPlan() => w.Core.Audit.ReadAll().Skip(audits).Any(a => a.Action is "assist.ki.ohneplan" or "assist.ki.fehler" && a.Detail.StartsWith(key));
        w.Find<TextBox>("auftrag-prompt").Text = prompt;
        Tap(w, "auftrag-pruefen");
        // erst ein neuer Lauf zählt (ein früherer Lauf desselben Programms steht sonst noch auf „verworfen“)
        bool Planned() => w.Core.Audit.ReadAll().Skip(audits).Any(a => a.Action is "assist.plan" or "assist.hinweis" or "assist.rechte" && a.Detail.StartsWith(key));
        bool Done() => NoPlan() || Planned() && w.AssistLastKey == key && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen" or "fragen";
        for (var i = 0; i < ms / 20 && !Done(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
        Assert.True(Done(), $"{key}: nach {ms / 1000} s noch Phase „{w.AssistLast?.Phase}“ – {w.AssistLast?.Message} | {string.Join(" | ", w.AssistLog)} | Protokoll: {string.Join(" ; ", w.Core.Audit.ReadAll().Skip(audits).Select(a => a.Action + " " + a.Detail))}");
        Assert.False(NoPlan(), $"{key}: kein Plan aus der KI-Antwort: {w.AssistLastKiAnswer}");
        return w.AssistLast!.Value.Phase;
    }

    // ======================= Referate & Hausarbeiten =======================

    [AvaloniaFact(DisplayName = "T-0780 Referate (UI): leere Vorlage → Thema → Gliederung → Text → Quelle → Zitat per Klick → Word, PDF, ODT, PPTX, ODP als echte Dateien")]
    public void Referat_click_to_files()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            w.Width = 1600; w.Height = 1100; w.ProgramSetLevel(UserLevel.Profi);
            w.Show(Ref); Settle(); Tap(w, "prog-tpl-leer");
            var d = w.ProgramActive(Ref)!;
            Assert.NotNull(w.TryFind<Button>("prog-first-step")); // Leerzustand: erster Schritt sichtbar
            Tap(w, "prog-first-step");
            Type(w, "ref-f-title", "Wasser sparen im Garten"); Type(w, "ref-f-author", "Erika Muster"); Type(w, "ref-f-school", "Musterschule");
            Tap(w, "ref-step-1");
            Assert.Equal("Wasser sparen im Garten", d.Text("title"));
            w.Find<TextBox>("ref-outline-text").Text = "1 Einleitung\n2 Hauptteil\n2.1 Regentonne\n3 Fazit";
            Tap(w, "ref-outline-apply");
            Assert.Equal(["Einleitung", "Hauptteil", "Regentonne", "Fazit"], MainWindow.RefSections(d).Select(s => s.Title));
            // Literatur: Buch anlegen und ausfüllen
            Tap(w, "ref-step-3"); Tap(w, "ref-srcadd-buch");
            foreach (var (k, v) in new[] { ("authors", "Muster, Erika"), ("title", "Gärten für Einsteiger"), ("year", "2024"), ("publisher", "Grünverlag"), ("place", "Musterstadt") }) Type(w, "ref-srcf-" + k, v);
            var src = MainWindow.RefSources(d).Single();
            Assert.Equal(("Gärten für Einsteiger", "2024"), (src.Title, src.Year));
            // Text: Abschnitt wählen, schreiben, Quelle zitieren
            Tap(w, "ref-sec-2");
            Assert.Equal(2, w.RefStepOf(d));
            Type(w, "ref-sec-text", "Regenwasser ist weich und gut für Pflanzen.");
            w.Find<TextBox>("ref-cite-page").Text = "12";
            Tap(w, "ref-cite-0");
            Assert.Equal($"Regenwasser ist weich und gut für Pflanzen [@{src.Id}, S. 12].", MainWindow.RefSections(d)[2].Text);
            Assert.Contains("(Muster, 2024, S. 12)", w.Find<SelectableTextBlock>("ref-sec-preview").Text);
            foreach (var (i, t) in new[] { (0, "Gärten brauchen viel Wasser."), (3, "Es lohnt sich.") }) { Tap(w, "ref-sec-" + i); Type(w, "ref-sec-text", t); }
            Tap(w, "ref-step-4"); Assert.NotNull(w.TryFind<Border>("ref-slide-0"));
            // Ausgabe über die Knöpfe „Exportieren“
            var docx = Exported(w, Ref, "docx"); var back = DocxFormat.Read(docx);
            Assert.Contains(back.Paras, p => p.Style == ParaStyle.Heading2 && p.Text == "2.1 Regentonne");
            Assert.Contains(back.Paras, p => p.Text.Contains("(Muster, 2024, S. 12)"));
            Assert.Single(back.Blocks.OfType<TocBlock>()); Assert.True(back.Page.PageNumbers);
            var odt = Exported(w, Ref, "odt"); Assert.Contains(OdtFormat.Read(odt).Paras, p => p.Text == "Literaturverzeichnis");
            var pdf = Exported(w, Ref, "pdf"); using (var pd = PdfSharp.Pdf.IO.PdfReader.Open(pdf, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import)) Assert.True(pd.PageCount >= 4);
            var pptx = Exported(w, Ref, "pptx"); var deck = SlideFiles.Open(pptx);
            Assert.Equal(["Wasser sparen im Garten", "Gliederung", "1 Einleitung", "2 Hauptteil", "3 Fazit", "Quellen"], deck.Slides.Select(s => s.Title));
            var odp = Exported(w, Ref, "odp"); Assert.Equal(deck.Slides.Count, SlideFiles.Open(odp).Slides.Count);
            Assert.Contains("✓ bereit", w.Find<TextBlock>("prog-status-text").Text ?? "");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0780 Referate (UI): Abschnitt mit der Maus in der Gliederung verschieben, Rechtsklick-Menü, Rückgängig; Verweis ohne Quelle sperrt die Ausgabe sichtbar")]
    public void Referat_drag_menu_undo_block()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Width = 1600; w.Height = 1100; w.ProgramSetLevel(UserLevel.Profi);
            w.Show(Ref); Settle(); Tap(w, "prog-tpl-beispiel");
            var d = w.ProgramActive(Ref)!;
            Assert.Equal(["Einleitung", "Wege zum Wassersparen", "Regenwasser sammeln", "Mulchen", "Fazit"], MainWindow.RefSections(d).Select(s => s.Title));
            // Ziehen: „Fazit“ (Zeile 4) auf „Einleitung“ (Zeile 0)
            BueroAusbau_Ui.Drag(w, "ref-sec-4", "ref-sec-0");
            Assert.Equal("Fazit", MainWindow.RefSections(d)[0].Title);
            Tap(w, "prog-undo");
            Assert.Equal("Einleitung", MainWindow.RefSections(w.ProgramActive(Ref)!)[0].Title);
            // Rechtsklick-Menü am Abschnitt: „Ebene tiefer“
            d = w.ProgramActive(Ref)!;
            Assert.Equal("ref-sec-menu-3", w.Find<Button>("ref-sec-3").ContextMenu!.Name);
            BueroAusbau_Ui.Menu(w, "ref-sec-3", "ref-m-in");
            Assert.Equal(3, MainWindow.RefSections(w.ProgramActive(Ref)!)[3].Level);
            // erfundener Verweis: sichtbar markiert, Ausgabe verweigert mit klarer Meldung
            d = w.ProgramActive(Ref)!;
            w.RefSetSection(d, MainWindow.RefSections(d)[0].Id, text: "Laut Studie spart man 90 % [@studie-2023].");
            w.Show(Ref); Settle();
            Assert.Contains("✗", w.Find<Button>("ref-sec-0").Content is TextBlock tb ? tb.Text : "");
            Tap(w, "prog-export-docx");
            Assert.Contains("[@studie-2023]", w.ProgramMessage(Ref));
            Assert.DoesNotContain("Exportiert", w.ProgramMessage(Ref));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0780 Referate (UI): Quellen aus „Recherche mit Quellen“ übernehmen (offenes Dokument) und RIS aus Zotero einlesen – Kennungen bleiben, Doppelte nicht doppelt")]
    public void Referat_sources_from_cite_and_ris()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            w.Width = 1600; w.Height = 1100; w.ProgramSetLevel(UserLevel.Profi);
            w.Show("quellen-programm"); Settle(); Tap(w, "prog-tpl-beispiel");
            Assert.Equal(3, MainWindow.CiteSources(w.ProgramActive("quellen-programm")!).Count);
            w.Show(Ref); Settle(); Tap(w, "prog-tpl-leer");
            var d = w.ProgramActive(Ref)!;
            Tap(w, "prog-cmd-ref-from-cite");
            Assert.Equal(["q-buch", "q-artikel", "q-web"], MainWindow.RefSources(d).Select(s => s.Id));
            Tap(w, "prog-cmd-ref-from-cite"); Assert.Equal(3, MainWindow.RefSources(d).Count); // nicht doppelt
            var ris = Path.Combine(root, "zotero.ris"); Directory.CreateDirectory(root);
            File.WriteAllText(ris, "TY  - JOUR\r\nAU  - Beispiel, Max\r\nTI  - Mulch im Sommer\r\nT2  - Gartenpraxis\r\nPY  - 2022\r\nER  - \r\n");
            w.PickOverride = _ => Task.FromResult<string[]?>([ris]);
            Tap(w, "prog-cmd-ref-import"); await_(w);
            Assert.Contains(MainWindow.RefSources(d), s => s.Title == "Mulch im Sommer" && s.Kind == "artikel");
            // Ziehen und Ablegen: BibTeX-Datei aus dem Explorer auf die Arbeitsfläche
            var bib = Path.Combine(root, "jabref.bib");
            File.WriteAllText(bib, "@book{kompost2021,\n  author = {Muster, Erika},\n  title = {Kompost richtig anlegen},\n  year = {2021},\n  publisher = {Grünverlag},\n  address = {Musterstadt}\n}\n");
            Assert.Equal(Avalonia.Input.DragDropEffects.Copy, BueroAusbau_Ui.Drop(w, "ref-binder-card", bib));
            Assert.Contains(MainWindow.RefSources(w.ProgramActive(Ref)!), s => s.Title == "Kompost richtig anlegen");
        }
        finally { w.PickOverride = null; w.Close(); }
        static void await_(MainWindow w) { for (var i = 0; i < 50; i++) { Settle(); Thread.Sleep(10); } }
    }

    [AvaloniaFact(DisplayName = "T-0780 Referate (UI): Stufe Einfach – eine goldene Hauptaktion, Kette und Gliederung sichtbar, Thema auch ohne rechte Spalte eintragbar")]
    public void Referat_simple_level()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Width = 1400; w.Height = 950; w.ProgramSetLevel(UserLevel.Einfach);
            w.Show(Ref); Settle(); Tap(w, "prog-tpl-leer");
            Tap(w, "prog-first-step");
            Assert.NotNull(w.TryFind<TextBox>("ref-f-title")); Assert.NotNull(w.TryFind<WrapPanel>("ref-chain")); Assert.NotNull(w.TryFind<StackPanel>("ref-binder"));
            var gold = w.GetLogicalDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && MainWindow.IsGold(b) && ((b.Name ?? "").StartsWith("prog-", StringComparison.Ordinal) || (b.Name ?? "").StartsWith("ref-", StringComparison.Ordinal))).Select(b => b.Name).ToList();
            Assert.True(gold.Count == 1, "goldene Knöpfe: " + string.Join(", ", gold));
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0780 Referate KI: „Gliederung“ – Vorschau ändert nichts, „Ja, behalten“ schreibt die Gliederung ins offene Dokument als EIN Rückgängig-Schritt, Rückgängig stellt alles her (KI ersetzt)")]
    public async Task Referat_ki_outline()
    {
        var (w, _, _) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var ki = new FixedKi(_ => Plan("referat.gliederung", new JsonObject { ["gliederung"] = "1 Einleitung\n1.1 Fragestellung\n2 Ursachen\n3 Folgen\n4 Fazit" }));
        try
        {
            w.KiBackendsOverride = () => [ki];
            w.Width = 1600; w.Height = 1100; w.ProgramSetLevel(UserLevel.Profi);
            w.Show(Ref); Settle(); Tap(w, "prog-tpl-leer");
            var d = w.ProgramActive(Ref)!; d.Set("title", "Klimawandel in den Alpen", "Thema");
            var before = d.Data.ToJsonString(); var history = d.History.Count;
            Assert.Equal("vorschau", await Ask(w, Ref, "Schlag mir eine Gliederung für meine Hausarbeit zum Klimawandel in den Alpen vor"));
            Assert.Contains("referat.gliederung", ki.Systems[^1]); Assert.Contains("[@kennung", ki.Systems[^1]);
            foreach (var cmd in new[] { "ref-add", "ref-check", "ref-to-text", "ref-to-deck", "ref-from-cite" }) Assert.DoesNotContain($"- {cmd}:", ki.Systems[^1]); Assert.Contains("Klimawandel in den Alpen", ki.Systems[^1]);
            Assert.Equal(before, d.Data.ToJsonString());
            Assert.Contains("kommt es ins Dokument", w.AssistLast!.Value.Message); Assert.DoesNotContain("neue Datei", w.AssistLast.Value.Message);
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler");
            Assert.True(w.AssistLast!.Value.Phase == "fertig", w.AssistLast.Value.Message);
            Assert.Equal(["Einleitung", "Fragestellung", "Ursachen", "Folgen", "Fazit"], MainWindow.RefSections(d).Select(s => s.Title));
            Assert.Equal(history + 1, d.History.Count); Assert.StartsWith(AssistChain.UndoPrefix, d.UndoLabel);
            Tap(w, "assist-undo");
            Assert.Equal(before, w.ProgramActive(Ref)!.Data.ToJsonString());
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0780 Referate KI: „Abschnitt schreiben“ mit Zitat aus der Liste kommt ins Dokument; erfundene Quelle ([@neu] oder „(Müller, 2019)“) und neue Quellen per „dokument.setzen“ werden abgelehnt (KI ersetzt)")]
    public async Task Referat_ki_section_no_invented_sources()
    {
        var (w, _, _) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var reply = "";
        var ki = new FixedKi(_ => reply);
        try
        {
            w.KiBackendsOverride = () => [ki];
            w.Width = 1600; w.Height = 1100; w.ProgramSetLevel(UserLevel.Profi);
            w.Show(Ref); Settle(); Tap(w, "prog-tpl-beispiel");
            var d = w.ProgramActive(Ref)!; var before = d.Data.ToJsonString();
            // 1. erfundener Verweis
            reply = Plan("referat.abschnitt", new JsonObject { ["abschnitt"] = "4", ["text"] = "Mulch spart bis zu 70 % Wasser [@mueller-2019]." });
            Assert.Equal("fehler", await Ask(w, Ref, "Schreib den Abschnitt Mulchen mit einer Quelle zu den Einsparungen"));
            Assert.Contains("nicht in deiner Liste", w.AssistLast!.Value.Message); Assert.Equal(before, d.Data.ToJsonString());
            // 2. freier Kurzbeleg ohne passende Quelle
            reply = Plan("referat.abschnitt", new JsonObject { ["abschnitt"] = "Mulchen", ["text"] = "Mulch spart bis zu 70 % Wasser (Müller, 2019, S. 4). Das ist viel." });
            Assert.Equal("fehler", await Ask(w, Ref, "Schreib den Abschnitt Mulchen bitte noch einmal ganz neu"));
            Assert.Contains("(Müller, 2019, S. 4)", w.AssistLast!.Value.Message); Assert.Equal(before, d.Data.ToJsonString());
            // 3. neue Quelle über dokument.setzen – „sources“ ist für die KI gesperrt
            reply = Plan(AssistChain.EditAction, new JsonObject { ["pfad"] = "sources/2", ["wert"] = """{"id":"q-neu","kind":"buch","title":"Erfunden","year":"2020"}""" });
            Assert.Equal("fehler", await Ask(w, Ref, "Leg eine weitere Quelle zum Thema Mulchen in meiner Liste an"));
            Assert.Equal(before, d.Data.ToJsonString());
            // 4. richtig: Verweis auf eine Quelle der Liste
            reply = Plan("referat.abschnitt", new JsonObject { ["abschnitt"] = "s4", ["text"] = "Eine Mulchschicht aus Laub hält den Boden feucht [@q-buch, S. 20].\n\nSo verdunstet weniger Wasser." });
            Assert.Equal("vorschau", await Ask(w, Ref, "Schreib den Abschnitt Mulchen mit Bezug auf mein Gartenbuch"));
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler");
            Assert.True(w.AssistLast!.Value.Phase == "fertig", w.AssistLast.Value.Message);
            var s = MainWindow.RefSections(d).Single(x => x.Title == "Mulchen");
            Assert.StartsWith("Eine Mulchschicht aus Laub", s.Text);
            Assert.Equal(2, MainWindow.RefSources(d).Count);
            Assert.StartsWith(AssistChain.UndoPrefix, d.UndoLabel);
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.Close(); }
    }

    // ======================= Zeitleisten =======================

    [AvaloniaFact(DisplayName = "T-0780 Zeitleisten (UI): leere Vorlage → erstes Ereignis (44 v. Chr.) → Zeitraum → Spur → SVG, PNG, PDF, CSV, JSON als echte Dateien")]
    public void Timeline_click_to_files()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Width = 1600; w.Height = 1000; w.ProgramSetLevel(UserLevel.Profi);
            w.Show(Tl); Settle(); Tap(w, "prog-tpl-leer");
            var d = w.ProgramActive(Tl)!;
            Assert.NotNull(w.TryFind<Button>("prog-first-step"));
            Tap(w, "prog-first-step");
            Type(w, "tl-f-start", "15.03.44 v. Chr."); Type(w, "tl-f-title", "Tod Caesars");
            var t = MainWindow.TlData(d);
            Assert.Equal(("Tod Caesars", -44, 3, 15), (t.Items[0].Title, t.Items[0].Start.Year, t.Items[0].Start.Month, t.Items[0].Start.Day));
            Tap(w, "prog-cmd-tl-add-period");
            Type(w, "tl-f-start", "27 v. Chr."); Type(w, "tl-f-end", "476"); Type(w, "tl-f-title", "Römisches Kaiserreich");
            Tap(w, "prog-cmd-tl-add-track");
            Assert.Equal(2, MainWindow.TlData(d).Tracks.Count);
            Tap(w, "prog-cmd-tl-add-event");
            Type(w, "tl-f-start", "ca. 1200"); Type(w, "tl-f-title", "Nibelungenlied");
            var combo = w.Find<ComboBox>("tl-f-track"); combo.SelectedIndex = 1; Settle();
            t = MainWindow.TlData(d);
            Assert.Equal(3, t.Items.Count); Assert.True(t.Items[2].Start.Approx); Assert.Equal(t.Tracks[1].Id, t.Items[2].Track);
            Type(w, "tl-p-title", "Antike und Mittelalter");
            var svg = Exported(w, Tl, "svg"); var x = System.Xml.Linq.XDocument.Load(svg);
            var texts = x.Descendants().Where(e => e.Name.LocalName == "text").Select(e => e.Value).ToList();
            Assert.Contains("Antike und Mittelalter", texts); Assert.Contains("Tod Caesars", texts); Assert.Contains("15.03.44 v. Chr.", texts); Assert.Contains("ca. Römisches Kaiserreich", texts.Select(s => s.Replace("ca. ca.", "ca.")).Append("ca. Römisches Kaiserreich"));
            var png = Exported(w, Tl, "png"); var bytes = File.ReadAllBytes(png); Assert.Equal(0x89, bytes[0]); Assert.Equal((byte)'P', bytes[1]);
            var pdf = Exported(w, Tl, "pdf"); using (var pd = PdfSharp.Pdf.IO.PdfReader.Open(pdf, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import)) Assert.Equal(1, pd.PageCount);
            var csv = Exported(w, Tl, "csv"); var (fromCsv, probs) = TimelineKit.FromCsv(File.ReadAllText(csv)); Assert.Empty(probs); Assert.Equal(3, fromCsv.Items.Count);
            var json = Exported(w, Tl, "json"); Assert.Equal(3, TimelineKit.FromExchangeJson(File.ReadAllText(json)).Data.Items.Count);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0780 Zeitleisten (UI): Eintrag mit der Maus in der Zeit verschieben, Mausrad zoomt, Ziehen auf freier Fläche verschiebt die Ansicht, Rechtsklick-Menü, Entf löscht, Rückgängig")]
    public void Timeline_mouse()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Width = 1600; w.Height = 1000; w.ProgramSetLevel(UserLevel.Profi);
            w.Show(Tl); Settle(); Tap(w, "prog-tpl-beispiel");
            var d = w.ProgramActive(Tl)!;
            var canvas = w.Find<TimelineCanvas>("tl-canvas");
            var item = MainWindow.TlData(d).Items.Single(i => i.Title == "Kaiserkrönung Karls des Großen");
            var scene = canvas.SceneFor(canvas.Bounds.Width);
            var (hx, hy, _, hh) = scene.Hits[item.Id];
            var start = canvas.TranslatePoint(new Point(hx + 8, hy + hh / 2), w)!.Value;
            var perYear = (canvas.Bounds.Width - TimelineKit.RightPad - TimelineKit.Left) / (canvas.To - canvas.From);
            var target = start + new Point(100 * perYear, 0); // 100 Jahre später
            w.MouseDown(start, MouseButton.Left); for (var i = 1; i <= 6; i++) { w.MouseMove(start + (target - start) * (i / 6.0)); Settle(); } w.MouseUp(target, MouseButton.Left); Settle();
            var moved = MainWindow.TlData(d).Items.Single(i => i.Id == item.Id);
            Assert.InRange(moved.Start.Year, 895, 905);
            Tap(w, "prog-undo");
            d = w.ProgramActive(Tl)!;
            Assert.Equal(800, MainWindow.TlData(d).Items.Single(i => i.Id == item.Id).Start.Year);
            // Mausrad zoomt hinein, Ziehen auf freier Fläche verschiebt
            canvas = w.Find<TimelineCanvas>("tl-canvas");
            var span0 = canvas.To - canvas.From;
            var free = canvas.TranslatePoint(new Point(canvas.Bounds.Width / 2, 40), w)!.Value;
            w.MouseWheel(free, new Vector(0, 1)); Settle();
            Assert.True(canvas.To - canvas.From < span0 * 0.9, "kein Zoom");
            var (f0, _) = w.TlViewOf(d);
            w.MouseDown(free, MouseButton.Left); for (var i = 1; i <= 4; i++) { w.MouseMove(free + new Point(30 * i, 0)); Settle(); } w.MouseUp(free + new Point(120, 0), MouseButton.Left); Settle();
            Assert.True(w.TlViewOf(d).From < f0, "Ansicht nicht verschoben");
            // Rechtsklick-Menü der Zeitleiste
            Assert.Equal("tl-menu", w.Find<TimelineCanvas>("tl-canvas").ContextMenu!.Name);
            // wählen und mit Entf löschen
            w.Show(Tl); Settle();
            w.TlSelect(d, item.Id); w.Show(Tl); Settle();
            canvas = w.Find<TimelineCanvas>("tl-canvas"); canvas.Focus();
            w.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None); Settle();
            Assert.DoesNotContain(MainWindow.TlData(w.ProgramActive(Tl)!).Items, i => i.Id == item.Id);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0780 Zeitleisten (UI): TimelineJS-Tabelle einlesen, Bild an ein Ereignis hängen (verkleinert, in SVG eingebettet), Zeilen aus Text einfügen")]
    public void Timeline_import_image_lines()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            w.Width = 1600; w.Height = 1000; w.ProgramSetLevel(UserLevel.Profi);
            Directory.CreateDirectory(root);
            w.Show(Tl); Settle(); Tap(w, "prog-tpl-leer");
            var d = w.ProgramActive(Tl)!;
            var csv = Path.Combine(root, "timelinejs.csv");
            File.WriteAllText(csv, "Year,Month,Day,Headline,Text,Group,Type\n,,,Probe,,,title\n-490,,,Marathon,Schlacht,Krieg,\n1066,10,14,Hastings,,Krieg,\n");
            w.PickOverride = _ => Task.FromResult<string[]?>([csv]);
            Tap(w, "prog-cmd-tl-import"); for (var i = 0; i < 30; i++) Settle();
            var t = MainWindow.TlData(d);
            Assert.Equal(["Marathon", "Hastings"], t.Items.Select(i => i.Title));
            Assert.Equal(["Krieg"], t.Tracks.Select(x => x.Name)); // leere Platzhalter-Spur entfernt
            // Ziehen und Ablegen: JSON-Datei aus dem Explorer auf die Zeitleiste
            var json = Path.Combine(root, "zeitleiste.json");
            File.WriteAllText(json, TimelineKit.ToExchangeJson(new Timeline { Title = "x", Tracks = { new("t1", "Kunst", "#F472B6") }, Items = { new("e1", "Mona Lisa", HistDate.Parse("um 1503")!.Value, null, "t1") } }));
            Assert.Equal(Avalonia.Input.DragDropEffects.Copy, BueroAusbau_Ui.Drop(w, "tl-canvas", json));
            t = MainWindow.TlData(w.ProgramActive(Tl)!); d = w.ProgramActive(Tl)!;
            Assert.Contains(t.Items, i => i.Title == "Mona Lisa" && i.Start.Approx);
            Assert.Equal(["Krieg", "Kunst"], t.Tracks.Select(x => x.Name));
            // Bild: 1200×800 PNG wird verkleinert eingebettet
            var img = Path.Combine(root, "bild.png");
            File.WriteAllBytes(img, Trendo.Core.Kreativ.PngCodec.Encode(new Trendo.Core.Kreativ.Pixmap(1200, 800, Enumerable.Repeat((byte)200, 1200 * 800 * 4).ToArray())));
            w.TlSelect(d, t.Items[0].Id); w.Show(Tl); Settle();
            w.PickOverride = _ => Task.FromResult<string[]?>([img]);
            Tap(w, "prog-cmd-tl-add-image"); for (var i = 0; i < 30; i++) Settle();
            var withImg = MainWindow.TlData(d).Items[0];
            Assert.Equal("Marathon", withImg.Title);
            Assert.NotNull(withImg.Image); Assert.True(withImg.Image!.Length < TimelineKit.MaxImageBytes);
            Assert.Contains("data:image/jpeg;base64,", TimelineKit.ToSvg(TimelineKit.Layout(MainWindow.TlData(d), -600, 1200, 1400)));
            // Zeilen aus Text
            w.Find<TextBox>("tl-p-lines").Text = "1618 – 1648 | Dreißigjähriger Krieg | Krieg\nirgendwann | nichts";
            Tap(w, "tl-p-lines-add");
            Assert.Contains(MainWindow.TlData(d).Items, i => i.Title == "Dreißigjähriger Krieg" && i.IsPeriod);
            Assert.Contains("1 Zeile(n) nicht lesbar", w.ProgramMessage(Tl));
        }
        finally { w.PickOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0780 Zeitleisten KI: „Ereignisse aus Text einfügen“ – Vorschau ändert nichts, „Ja, behalten“ fügt die Ereignisse als EIN Rückgängig-Schritt ein; unlesbare Daten werden abgelehnt (KI ersetzt)")]
    public async Task Timeline_ki_events()
    {
        var (w, _, _) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var reply = "";
        var ki = new FixedKi(_ => reply);
        try
        {
            w.KiBackendsOverride = () => [ki];
            w.Width = 1600; w.Height = 1000; w.ProgramSetLevel(UserLevel.Profi);
            w.Show(Tl); Settle(); Tap(w, "prog-tpl-leer");
            var d = w.ProgramActive(Tl)!; var before = d.Data.ToJsonString(); var history = d.History.Count;
            reply = Plan("zeitleiste.ereignisse", new JsonObject { ["ereignisse"] = "um 1000 | Wikinger in Amerika\nirgendwann | nichts" });
            Assert.Equal("fehler", await Ask(w, Tl, "Füge die Ereignisse aus diesem Text in die Zeitleiste ein: Um 1000 erreichten Wikinger Amerika."));
            Assert.Equal(before, d.Data.ToJsonString());
            reply = Plan("zeitleiste.ereignisse", new JsonObject { ["ereignisse"] = "1789 | Französische Revolution | Politik\n1914 – 1918 | Erster Weltkrieg | Krieg\n44 v. Chr. | Tod Caesars | Politik" });
            Assert.Equal("vorschau", await Ask(w, Tl, "Füge die Ereignisse aus diesem Text in die Zeitleiste ein: 1789 begann die Revolution, 1914 bis 1918 der Erste Weltkrieg, 44 v. Chr. starb Caesar."));
            Assert.Contains("zeitleiste.ereignisse", ki.Systems[^1]);
            Assert.DoesNotContain("tl-add-event", ki.Systems[^1]); Assert.DoesNotContain("tl-add-period", ki.Systems[^1]); // leere Einträge legt die KI nicht an (Befund Echtlauf)
            Assert.Equal(before, d.Data.ToJsonString());
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler");
            Assert.True(w.AssistLast!.Value.Phase == "fertig", w.AssistLast.Value.Message);
            var t = MainWindow.TlData(d);
            Assert.Equal(["Französische Revolution", "Erster Weltkrieg", "Tod Caesars"], t.Items.Select(i => i.Title));
            Assert.Equal(-44, t.Items[2].Start.Year); Assert.True(t.Items[1].IsPeriod);
            Assert.Equal(history + 1, d.History.Count); Assert.StartsWith(AssistChain.UndoPrefix, d.UndoLabel);
            Tap(w, "assist-undo");
            Assert.Equal(before, w.ProgramActive(Tl)!.Data.ToJsonString());
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.Close(); }
    }

    // ======================= Echtlauf lokale KI =======================

    static string? Beleg() => Environment.GetEnvironmentVariable("TRENDO_T0780_ECHT") is { Length: > 0 } d ? Directory.CreateDirectory(d).FullName : null;

    [AvaloniaFact(DisplayName = "T-0780 ECHT: lokale KI (Ollama) – Referate „Gliederung“ und „Abschnitt“, Zeitleisten „Ereignisse aus Text“ im offenen Programm, Rückgängig (nur mit TRENDO_T0780_ECHT)")]
    public async Task Real_local_ki()
    {
        if (Beleg() is not { } beleg) { Assert.Skip("Echtlauf nur mit TRENDO_T0780_ECHT=<Belegordner> (braucht Ollama mit Modell auf diesem PC)."); return; }
        var (w, _, root) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var log = new StringBuilder();
        void L(string s) => log.AppendLine(s);
        var seen = 0;
        void KiAudit() { var all = w.Core.Audit.ReadAll(); foreach (var a in all.Skip(seen).Where(a => a.Action.StartsWith("ki.", StringComparison.Ordinal) || a.Action.StartsWith("assist.", StringComparison.Ordinal))) L($"  - Protokoll: {a.Action} {a.Detail}"); seen = all.Count; }
        string Keep(string file, string name) { var to = Path.Combine(beleg, name); File.Copy(file, to, true); L($"- Datei {name}: {new FileInfo(to).Length} Bytes, SHA-256 {Hash(to)}"); return to; }
        async Task<bool> Run(string key, string prompt)
        {
            seen = w.Core.Audit.ReadAll().Count;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var phase = await Ask(w, key, prompt, 900_000); sw.Stop();
            L($"- Auftrag: {prompt}");
            L($"- Phase nach {sw.Elapsed.TotalSeconds:F0} s: {phase} – {w.AssistLast!.Value.Message}");
            L($"- Plan: {string.Join(" → ", w.AssistLast.Value.Steps)}");
            L($"- KI-Antwort (roh): `{(w.AssistLastKiAnswer ?? "").Replace("`", "'").ReplaceLineEndings(" ")}`"); KiAudit();
            if (phase != "vorschau") return false;
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler", 60000);
            L($"- Nach „Ja, behalten“: {w.AssistLast!.Value.Phase} {w.AssistLast.Value.Message}");
            return w.AssistLast.Value.Phase == "fertig";
        }
        try
        {
            w.KiBackendsOverride = () => [new OllamaBackend()];
            Ui.UseRealModels(w);
            // Grafikspeicher wird mit anderen Programmen geteilt: qwen3:14b läuft dann teilweise auf dem Prozessor (gemessen etwa 5 Token/s) – Wartezeit der Kette für diesen Echtlauf verlängert
            w.Intel.LocalFirstAnswerTimeout = TimeSpan.FromMinutes(8);
            _ = w.InstalledModels; Assert.Equal("ollama-api", w.LastModelScan?.Source);
            if (Environment.GetEnvironmentVariable("TRENDO_ASSIST_MODELL") is { Length: > 0 } own) KiModel.Select(w.Core.DataRoot, own, w.Core.Audit);
            var choice = w.KiModelChoice;
            L("# T-0780 ECHT: KI schreibt ins offene Dokument (Referate & Hausarbeiten, Zeitleisten)"); L("");
            L($"Datum: {DateTime.Now:yyyy-MM-dd HH:mm} · KI: Ollama 127.0.0.1:11434 · Modell: {choice.Model} ({choice.Reason})");
            L("Wartezeit der Kette auf die erste KI-Antwort für diesen Lauf: 8 min (Standard 90 s) – der Grafikspeicher ist mit anderen Programmen geteilt, das Modell rechnet teilweise auf dem Prozessor.");
            L("Bedienung über die Oberfläche (Vorlage, Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“), Fenster headless. Claude nicht beteiligt."); L("");
            w.Width = 1600; w.Height = 1600; w.ProgramSetLevel(UserLevel.Profi); // lange KI-Pläne: Vorschau mit „Ja, behalten“ ganz im Bild

            // Referate: Gliederung
            w.Show(Ref); Settle(); Tap(w, "prog-tpl-leer");
            var d = w.ProgramActive(Ref)!; d.Set("title", "Bienen und ihre Bedeutung für die Landwirtschaft", "Thema");
            var before = d.Data.ToJsonString(); var history = d.History.Count;
            L("## Referate – Gliederung"); L("");
            var ok1 = await Run(Ref, "Erstelle für meine Hausarbeit zum Thema „Bienen und ihre Bedeutung für die Landwirtschaft“ eine Gliederung mit Einleitung, drei Kapiteln im Hauptteil und Fazit.");
            Assert.True(ok1, "Gliederung nicht übernommen: " + w.AssistLast!.Value.Message + " | " + w.AssistLastKiAnswer);
            var secs = MainWindow.RefSections(d);
            L($"- Gliederung im Dokument: {string.Join(" | ", ReferatKit.Numbered(secs).Select(x => x.Number + " " + x.Section.Title))}");
            L($"- Rückgängig-Liste: {history} → {d.History.Count}, oben „{d.UndoLabel}“");
            Assert.True(secs.Count >= 4); Assert.Equal(history + 1, d.History.Count);
            Tap(w, "assist-undo");
            L($"- Rückgängig: Dokument wie vorher = {before == w.ProgramActive(Ref)!.Data.ToJsonString()}"); Assert.Equal(before, w.ProgramActive(Ref)!.Data.ToJsonString());
            L("");

            // Referate: Abschnitt (Beispiel mit zwei Quellen)
            w.ProgramNew(Ref, "beispiel"); w.Show(Ref); Settle(); // zweites Dokument im offenen Programm (neuer Reiter)
            var r = w.ProgramActive(Ref)!; var rHistory = r.History.Count;
            L("## Referate – Abschnitt schreiben (Beispiel mit zwei eigenen Quellen)"); L("");
            var ok2 = await Run(Ref, "Schreib den Abschnitt 2.2 „Mulchen“ neu: drei Sätze, wie Mulch beim Wassersparen hilft. Zitiere nur meine Quelle q-buch, wenn es passt.");
            var mulch = MainWindow.RefSections(r).Single(s => s.Title == "Mulchen");
            L($"- Abschnitt „Mulchen“ im Dokument: „{mulch.Text.ReplaceLineEndings(" ")}“");
            L($"- Verweise: {string.Join(", ", ReferatKit.Citations(mulch.Text).Select(c => c.Id))} · unbekannt: {ReferatKit.UnknownCitations(mulch.Text, MainWindow.RefSources(r)).Count} · Quellen in der Liste: {MainWindow.RefSources(r).Count}");
            if (!ok2) L("- Hinweis: abgelehnt (das ist bei einer erfundenen Quelle das richtige Verhalten) – Meldung siehe oben.");
            Assert.Equal(2, MainWindow.RefSources(r).Count); Assert.Empty(ReferatKit.UnknownCitations(mulch.Text, MainWindow.RefSources(r)));
            if (ok2)
            {
                Assert.Equal(rHistory + 1, r.History.Count);
                var docx = Path.Combine(root, "referat-ki.docx"); Directory.CreateDirectory(root); MainWindow.Programs[Ref].Exports.Single(x => x.Name == "docx").Write(r, docx); Keep(docx, "referat-mit-ki-abschnitt.docx");
            }
            L("");

            // Zeitleisten
            w.Show(Tl); Settle(); Tap(w, "prog-tpl-leer");
            var t = w.ProgramActive(Tl)!; var tBefore = t.Data.ToJsonString(); var tHistory = t.History.Count;
            L("## Zeitleisten – Ereignisse aus Text"); L("");
            const string text = "Füge die Ereignisse aus diesem Text ein: Im Jahr 44 v. Chr. wurde Caesar ermordet. Karl der Große wurde im Jahr 800 zum Kaiser gekrönt. Der Dreißigjährige Krieg dauerte von 1618 bis 1648. Um 1450 erfand Gutenberg den Buchdruck mit beweglichen Lettern.";
            var ok3 = await Run(Tl, text);
            Assert.True(ok3, "Ereignisse nicht übernommen: " + w.AssistLast!.Value.Message + " | " + w.AssistLastKiAnswer);
            var items = MainWindow.TlData(t).Items;
            L($"- Einträge im Dokument: {string.Join(" | ", items.Select(i => $"{i.Start}{(i.End is { } e ? " – " + e : "")}: {i.Title}"))}");
            L($"- Rückgängig-Liste: {tHistory} → {t.History.Count}, oben „{t.UndoLabel}“");
            Assert.True(items.Count >= 3); Assert.Contains(items, i => i.Start.Year == -44); Assert.Equal(tHistory + 1, t.History.Count);
            var svg = Path.Combine(root, "zeitleiste-ki.svg"); MainWindow.Programs[Tl].Exports.Single(x => x.Name == "svg").Write(t, svg); Keep(svg, "zeitleiste-mit-ki.svg");
            Tap(w, "assist-undo");
            L($"- Rückgängig: Dokument wie vorher = {tBefore == w.ProgramActive(Tl)!.Data.ToJsonString()}"); Assert.Equal(tBefore, w.ProgramActive(Tl)!.Data.ToJsonString());
            L(""); L("Ergebnis: bestanden.");
        }
        catch (Exception e) { L(""); L("FEHLER: " + e.Message); throw; }
        finally
        {
            File.WriteAllText(Path.Combine(beleg, "echtlauf-lokale-ki.md"), log.ToString().Replace(Environment.UserName, "<user>"), new UTF8Encoding(false));
            MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.VramOverride = null; w.InstalledModelsOverride = null; w.PickOverride = null; w.Close();
        }
    }
}
