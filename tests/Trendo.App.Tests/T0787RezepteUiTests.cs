using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Programs;
using Trendo.Core.Wissen;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// T-0787 (Etappe 0.9.115, Wissen &amp; Lernen E): „Rezepte &amp; Ernährung“ (STUDIO-RECIPE-01) als Programm der Hülle – vom Klick bis zur Datei:
/// leeres Rezeptbuch → erster Schritt → Name, Zutaten, Schritte tippen → Portionen ansehen und übernehmen → Wochenplan → Einkaufsliste → Exporte (PDF, JSON-LD, HTML, DOCX, CSV).
/// Fremddatei (JSON-LD aus einer Rezeptseite) einlesen, Rechtsklick „anderes Lebensmittel“, Einfach/Profi. KI-Fachaktion „rezept.anlegen“ mit Vorschau, „Ja, behalten“
/// und einem Rückgängig-Schritt (KI ersetzt); <see cref="Real_local_ki"/> läuft mit dem Ollama-Modell dieses PCs (nur mit TRENDO_T0787_ECHT=&lt;Belegordner&gt;).
[Collection("Claude")]
public class T0787Rezepte_Ui
{
    const string K = MainWindow.RzKey;

    sealed class FixedKi(Func<string, string> reply) : IKiBackend
    {
        public string Name => "Ersatz-KI"; public string BaseUrl => "http://127.0.0.1:9";
        public readonly List<string> Systems = [];
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["ersatz"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Systems.Add(messages[0].Content); return Task.FromResult(reply(messages[^1].Content)); }
    }

    static string Plan(string action, JsonObject args) => new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["action"] = action, ["args"] = args }) }.ToJsonString();
    static async Task Until(Func<bool> ok, int ms = 10000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }
    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); RealClick(w, b); Settle(); }
    static string Hash(string f) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant();

    /// <summary>Tippen wie eine Person: Text setzen, dann verlässt der Fokus das Feld (übernimmt).</summary>
    static void Tippe(MainWindow w, string name, string text)
    {
        var t = w.Find<TextBox>(name); t.BringIntoView(); t.Focus(); Settle();
        t.Text = text; Settle();
        w.Find<TextBox>("auftrag-prompt").Focus(); Settle(); // Fokus verlässt das Feld – es übernimmt
    }

    static ProgramDocument Open(MainWindow w, string tpl = "leer", int level = UserLevel.Erweitert)
    {
        w.Width = 1600; w.Height = 1100; w.ProgramSetLevel(level);
        w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-" + tpl)); Settle();
        return w.ProgramActive(K)!;
    }

    static string Exportiere(MainWindow w, string name)
    {
        RealClick(w, w.Find<Button>("prog-export-" + name)); Settle();
        Assert.True(w.ProgramMessage(K).StartsWith("Exportiert:", StringComparison.Ordinal), w.ProgramMessage(K));
        var file = w.ProgramMessage(K)["Exportiert: ".Length..].Trim();
        Assert.True(File.Exists(file) && new FileInfo(file).Length > 100, name + ": " + file);
        return file;
    }

    static void Menue(MainWindow w, string owner, string item)
    {
        var mi = w.Find<Control>(owner).ContextMenu!.Items.OfType<MenuItem>().First(m => m.Name == item);
        mi.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Settle();
    }

    [AvaloniaFact(DisplayName = "T-0787 Rezepte: leeres Buch → „Erstes Rezept anlegen“ → Name, Zutaten, Schritte tippen → Tabelle mit Gramm/kcal → für 8 anzeigen und übernehmen (ein Rückgängig-Schritt) → PDF, JSON-LD, HTML, DOCX")]
    public void Klick_bis_datei()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var d = Open(w);
            Assert.NotNull(w.TryFind<Button>("prog-first-step")); // erster Schritt im Leerzustand sichtbar
            Tap(w, "prog-first-step");
            Assert.Single(MainWindow.RzRezepte(d));
            Tippe(w, "rz-name", "Pfannkuchen");
            Tippe(w, "rz-zutaten", "250 g Weizenmehl\n500 ml Milch\n3 Eier\n1 Prise Salz\n2 EL Butter, zum Ausbacken\nZucker nach Geschmack");
            Tippe(w, "rz-schritte", "1. Mehl, Milch, Eier und Salz glatt rühren.\n2. In heißer Butter goldbraun backen.");
            var r = MainWindow.RzRezepte(d)[0];
            Assert.Equal("Pfannkuchen", r.Name); Assert.Equal(6, r.Zutaten.Count); Assert.Equal(["Mehl, Milch, Eier und Salz glatt rühren.", "In heißer Butter goldbraun backen."], r.Schritte);
            Assert.Equal("250 g", w.Find<TextBlock>("rz-zt-menge-0").Text);
            Assert.Equal("Weizenmehl (Type 405/550)", ((TextBlock)w.Find<Button>("rz-zt-0").Content!).Text);
            var kcal = Math.Round(RezeptKit.JePortion(r).Kcal);
            Assert.Contains($"{kcal} kcal", w.Find<TextBlock>("rz-nw-0").Text);
            Assert.Contains("Richtwerte", w.Find<TextBlock>("rz-nw-richtwert").Text);
            // Ansicht für 8 Portionen: nur anzeigen, Rezept bleibt
            var hist = d.History.Count;
            for (var i = 0; i < 4; i++) Tap(w, "rz-view-plus");
            Assert.Equal("8 Portionen", w.Find<TextBlock>("rz-view").Text);
            Assert.Equal("500 g", w.Find<TextBlock>("rz-zt-menge-0").Text); Assert.Equal("1 l", w.Find<TextBlock>("rz-zt-menge-1").Text); Assert.Equal("6", w.Find<TextBlock>("rz-zt-menge-2").Text);
            Assert.Equal(4, MainWindow.RzRezepte(d)[0].Portionen); Assert.Equal(hist, d.History.Count);
            Tap(w, "rz-view-apply");
            Assert.Equal(8, MainWindow.RzRezepte(d)[0].Portionen); Assert.Equal(hist + 1, d.History.Count);
            Assert.Equal("500 g Weizenmehl", RezeptKit.Zeile(MainWindow.RzRezepte(d)[0].Zutaten[0]));
            Tap(w, "prog-undo"); Assert.Equal(4, MainWindow.RzRezepte(d)[0].Portionen);
            Tap(w, "prog-redo"); Assert.Equal(8, MainWindow.RzRezepte(d)[0].Portionen);
            // Exporte
            var pdf = Exportiere(w, "pdf");
            var txt = string.Join("\n", Enumerable.Range(0, PdfView.PageCount(pdf)).Select(i => PdfView.Text(pdf, i)));
            Assert.Contains("Pfannkuchen", txt); Assert.Contains("500 g", txt); Assert.Contains("Richtwerte", txt); Assert.Contains("FoodData Central", txt);
            var json = JsonNode.Parse(File.ReadAllText(Exportiere(w, "jsonld")))!.AsObject();
            Assert.Equal("Recipe", json["@type"]!.GetValue<string>()); Assert.Equal(6, json["recipeIngredient"]!.AsArray().Count);
            Assert.Contains("application/ld+json", File.ReadAllText(Exportiere(w, "html")));
            Assert.Contains("Weizenmehl", Trendo.Core.Office.DocxFormat.Read(Exportiere(w, "docx")).PlainText());
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0787 Wochenplan und Einkaufsliste über die Oberfläche: Rezept aus Text, auf Tage legen, Liste zusammengefasst, abhaken, eigener Eintrag, Rechtsklick „auf Freitag“, Export PDF und CSV")]
    public void Woche_und_einkauf()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var d = Open(w);
            Tippe(w, "rz-paste", "Rührei\nFür 2 Portionen\nZutaten\n4 Eier\n100 ml Milch\n1 Prise Salz\nZubereitung\n1. Verquirlen.\n2. In der Pfanne stocken lassen.");
            Tap(w, "rz-paste-apply");
            var r = Assert.Single(MainWindow.RzRezepte(d)); Assert.Equal("Rührei", r.Name); Assert.Equal(2, r.Portionen); Assert.Equal(3, r.Zutaten.Count);
            // zweites Rezept über den Text-Einfüger unten im Editor (aufklappen)
            w.Find<Expander>("rz-paste-exp").IsExpanded = true; Settle();
            Tippe(w, "rz-paste", "Milchreis\nFür 4 Portionen\nZutaten\n125 g Milchreis\n1 l Milch\n2 EL Zucker\nZubereitung\nAlles 30 Minuten quellen lassen.");
            Tap(w, "rz-paste-apply");
            Assert.Equal(2, MainWindow.RzRezepte(d).Count);
            Tap(w, "rz-mode-1"); Assert.NotNull(w.Find<StackPanel>("rz-plan"));
            // Rührei für 4 am Montag, Milchreis am Mittwoch
            var box = w.Find<ComboBox>("rz-plan-rezept"); box.SelectedIndex = 0; w.Find<ComboBox>("rz-plan-tag").SelectedIndex = 0; w.Find<TextBox>("rz-plan-portionen").Text = "4"; Settle();
            Tap(w, "rz-plan-add");
            box = w.Find<ComboBox>("rz-plan-rezept"); box.SelectedIndex = 1; w.Find<ComboBox>("rz-plan-tag").SelectedIndex = 2; Settle();
            Tap(w, "rz-plan-add");
            var plan = RezeptKit.Plan(d.Data);
            Assert.Equal(2, plan.Count); Assert.Equal(4, plan[0].Portionen); Assert.Equal(2, plan[1].Tag);
            // Rechtsklick: Rührei auf Freitag
            Menue(w, "rz-pe-0", "rz-pe-0-tag-4");
            Assert.Equal(4, RezeptKit.Plan(d.Data)[0].Tag);
            Tap(w, "rz-plan-to-list");
            var items = RezeptKit.Einkaufsliste(d.Data);
            Assert.Equal(1.2, items.Single(p => p.Lm == "vollmilch").Menge); Assert.Equal("l", items.Single(p => p.Lm == "vollmilch").Einheit); // 200 ml (Rührei für 4 statt 2) + 1 l
            Assert.Equal(8, items.Single(p => p.Lm == "ei").Menge);
            // abhaken (über die Checkbox) und eigener Eintrag
            var cb = w.Find<CheckBox>("rz-li-0"); cb.IsChecked = true; Settle();
            Assert.Single(RezeptKit.Abgehakt(d.Data));
            Tippe(w, "rz-li-extra", "Spülmittel"); Tap(w, "rz-li-extra-add");
            Assert.Equal(["Spülmittel"], RezeptKit.Extra(d.Data));
            var pdf = Exportiere(w, "woche");
            var t = string.Join("\n", Enumerable.Range(0, PdfView.PageCount(pdf)).Select(i => PdfView.Text(pdf, i)));
            Assert.Contains("Freitag", t); Assert.Contains("Einkaufsliste", t); Assert.Contains("Spülmittel", t);
            var csv = File.ReadAllText(Exportiere(w, "liste-csv"));
            Assert.Contains("1,2;l;Vollmilch (3,25 %)", csv); Assert.Contains(";ja", csv);
            Assert.Contains("[x]", File.ReadAllText(Exportiere(w, "liste-txt")));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0787 Fremddatei: gespeicherte Rezeptseite mit JSON-LD (@graph, HowToSection) über „Rezept öffnen …“ einlesen, Datei unverändert; Rechtsklick wählt anderes Lebensmittel; Einfach zeigt die Grundbefehle")]
    public void Import_und_bedienung()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var d = Open(w);
            var f = Path.Combine(root, "linsensuppe.html");
            File.WriteAllText(f, """
                <!doctype html><html><head><title>Linsensuppe</title>
                <script type="application/ld+json">{"@context":"https://schema.org","@graph":[{"@type":"WebSite","name":"Beispielseite"},{"@type":"Recipe","name":"Linsensuppe","recipeYield":"6 Teller",
                "prepTime":"PT15M","cookTime":"PT45M","recipeIngredient":["250 g Linsen","2 Karotten","1 Stange Lauch","1 l Gemüsebrühe","1 EL Olivenöl","Salz"],
                "recipeInstructions":[{"@type":"HowToSection","name":"Vorbereiten","itemListElement":[{"@type":"HowToStep","text":"Gemüse würfeln."}]},{"@type":"HowToStep","text":"Alles 45 Minuten kochen."}]}]}</script>
                </head><body><h1>Linsensuppe</h1></body></html>
                """, new UTF8Encoding(false));
            var vorher = Hash(f);
            w.PickOverride = _ => Task.FromResult<string[]?>([f]);
            Tap(w, "prog-cmd-rz-import"); Settle();
            for (var i = 0; i < 500 && (MainWindow.RzRezepte(d).Count == 0 || w.RzImportBusy > 0); i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); } // Einlesen läuft im Hintergrund
            var r = Assert.Single(MainWindow.RzRezepte(d));
            Assert.Equal("Linsensuppe", r.Name); Assert.Equal(6, r.Portionen); Assert.Equal("Teller", r.PortionWort); Assert.Equal(45, r.Garzeit);
            Assert.Equal(["Vorbereiten: Gemüse würfeln.", "Alles 45 Minuten kochen."], r.Schritte);
            Assert.Equal(vorher, Hash(f));
            Assert.Contains("eingelesen", w.ProgramMessage(K));
            // Rechtsklick auf „Gemüsebrühe“ (Zeile 3): anderes Lebensmittel bzw. nicht mitrechnen
            Assert.Equal("haehnchenbruehe", r.Zutaten[3].Lm);
            Menue(w, "rz-zt-3", "rz-lm-3-none");
            Assert.Equal(Zutat.KeinLm, MainWindow.RzRezepte(d)[0].Zutaten[3].Lm);
            Assert.Contains("nicht", w.ProgramMessage(K));
            Tap(w, "prog-undo"); Assert.Equal("haehnchenbruehe", MainWindow.RzRezepte(d)[0].Zutaten[3].Lm);
            // Rechtsklick in der Liste: Kopieren, dann löschen
            Menue(w, "rz-item-0", "rz-m-dup"); Assert.Equal(2, MainWindow.RzRezepte(d).Count);
            Menue(w, "rz-item-1", "rz-m-del"); Assert.Single(MainWindow.RzRezepte(d));
            // kaputte Datei: verständliche Meldung, nichts angelegt
            var bad = Path.Combine(root, "kaputt.json"); File.WriteAllText(bad, "{ kein json");
            w.PickOverride = _ => Task.FromResult<string[]?>([bad]);
            Tap(w, "prog-cmd-rz-import"); for (var i = 0; i < 500 && (w.RzImportBusy > 0 || !w.ProgramMessage(K).Contains("JSON")); i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
            Assert.Single(MainWindow.RzRezepte(d)); Assert.Contains("JSON", w.ProgramMessage(K));
            // Einfach: Grundbefehle direkt sichtbar, eine goldene Hauptaktion
            w.ProgramSetLevel(UserLevel.Einfach); w.Show(K); Settle();
            foreach (var n in new[] { "prog-cmd-rz-new", "prog-cmd-rz-import", "prog-cmd-rz-scale", "prog-cmd-rz-to-plan" }) Assert.NotNull(w.TryFind<Button>(n));
        }
        finally { w.PickOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0787 KI schreibt ein Rezept ins offene Buch: Vorschau ändert nichts, „Ja, behalten“ legt es an (ein Rückgängig-Schritt, Nährwerte vom Kern), Rückgängig nimmt es heraus; Heilversprechen wird abgelehnt (KI ersetzt)")]
    public async Task Ki_rezept()
    {
        var (w, _, _) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var answer = Plan("rezept.anlegen", new JsonObject { ["name"] = "Tomatensoße", ["portionen"] = "4", ["zutaten"] = "1 Zwiebel\n2 EL Olivenöl\n1 Dose gehackte Tomaten\n1 TL Zucker", ["schritte"] = "Zwiebel würfeln und im Öl andünsten.\nTomaten und Zucker dazugeben und 15 Minuten köcheln.", ["beschreibung"] = "Einfache Soße für Nudeln." });
        var ki = new FixedKi(_ => answer);
        try
        {
            w.KiBackendsOverride = () => [ki];
            var d = Open(w);
            var vorher = d.Data.ToJsonString(); var hist = d.History.Count;
            Assert.Equal(AssistRoute.Dokument, w.AssistRouteFor(K));
            Assert.Equal("vorschau", await Ask(w, "Schreib ein Rezept für eine einfache Tomatensoße zu Nudeln für vier Personen"));
            Assert.Contains("rezept.anlegen", ki.Systems[^1]); Assert.Contains("Gesundheitsversprechen", ki.Systems[^1]);
            Assert.Equal(vorher, d.Data.ToJsonString()); Assert.Equal(hist, d.History.Count);
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler");
            Assert.True(w.AssistLast!.Value.Phase == "fertig", w.AssistLast.Value.Message);
            var r = Assert.Single(MainWindow.RzRezepte(d));
            Assert.Equal("Tomatensoße", r.Name); Assert.Equal("tomaten-dose", r.Zutaten[2].Lm); Assert.Equal(hist + 1, d.History.Count);
            Assert.True(RezeptKit.JePortion(r).Kcal > 0);
            Tap(w, "assist-undo");
            Assert.Equal(vorher, w.ProgramActive(K)!.Data.ToJsonString());
            // Heilversprechen: nicht übernommen
            answer = Plan("rezept.anlegen", new JsonObject { ["name"] = "Detox-Suppe", ["zutaten"] = "2 Karotten", ["schritte"] = "Kochen – entgiftet den Körper.", ["portionen"] = "2" });
            var ph = await Ask(w, "Schreib ein Rezept für eine Detox-Suppe mit Karotten für zwei Personen");
            Assert.NotEqual("fertig", ph);
            if (ph == "vorschau") { Tap(w, "assist-apply"); await Until(() => w.AssistLast?.Phase is "fertig" or "fehler"); Assert.Equal("fehler", w.AssistLast!.Value.Phase); }
            Assert.Empty(MainWindow.RzRezepte(w.ProgramActive(K)!));
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.Close(); }
    }

    static async Task<string> Ask(MainWindow w, string prompt, int ms = 10000)
    {
        var audits = w.Core.Audit.ReadAll().Count;
        bool NoPlan() => w.Core.Audit.ReadAll().Skip(audits).Any(a => a.Action is "assist.ki.ohneplan" or "assist.ki.fehler" && a.Detail.StartsWith(K));
        w.Find<TextBox>("auftrag-prompt").Text = prompt;
        Tap(w, "auftrag-pruefen");
        for (var i = 0; i < ms / 20 && !(NoPlan() || w.AssistLastKey == K && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen"); i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
        Assert.True(NoPlan() || w.AssistLastKey == K && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen", $"Phase {w.AssistLast?.Phase} ({w.AssistLastKey}) – {w.AssistLast?.Message} | {string.Join(" | ", w.AssistLog)} | KI: {w.AssistLastKiAnswer}");
        Assert.False(NoPlan(), $"kein Plan aus der KI-Antwort: {w.AssistLastKiAnswer}");
        return w.AssistLast!.Value.Phase;
    }

    static string? Beleg() => Environment.GetEnvironmentVariable("TRENDO_T0787_ECHT") is { Length: > 0 } d ? Directory.CreateDirectory(d).FullName : null;

    [AvaloniaFact(DisplayName = "T-0787 ECHT: lokale KI (Ollama) macht aus einem eingefügten Rezepttext ein Rezept im offenen Buch – Vorschau, „Ja, behalten“, Nährwerte vom Kern, Rückgängig (nur mit TRENDO_T0787_ECHT)")]
    public async Task Real_local_ki()
    {
        if (Beleg() is not { } beleg) { Assert.Skip("Echtlauf nur mit TRENDO_T0787_ECHT=<Belegordner> (braucht Ollama mit Modell auf diesem PC)."); return; }
        var (w, _, _) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var log = new StringBuilder();
        void L(string s) => log.AppendLine(s);
        var seen = 0;
        void KiAudit() { var all = w.Core.Audit.ReadAll(); foreach (var a in all.Skip(seen).Where(a => a.Action.StartsWith("ki.", StringComparison.Ordinal) || a.Action.StartsWith("assist.", StringComparison.Ordinal))) L($"  - Protokoll: {a.Action} {a.Detail}"); seen = all.Count; }
        try
        {
            w.KiBackendsOverride = () => [new OllamaBackend()];
            Ui.UseRealModels(w);
            _ = w.InstalledModels; Assert.Equal("ollama-api", w.LastModelScan?.Source);
            if (Environment.GetEnvironmentVariable("TRENDO_ASSIST_MODELL") is { Length: > 0 } own) KiModel.Select(w.Core.DataRoot, own, w.Core.Audit);
            var choice = w.KiModelChoice;
            L("# T-0787 ECHT: Rezepttext → Rezept im offenen Rezeptbuch (Rezepte & Ernährung)"); L("");
            L($"Datum: {DateTime.Now:yyyy-MM-dd HH:mm} · KI: Ollama 127.0.0.1:11434 · Modell: {choice.Model} ({choice.Reason})");
            L("Bedienung über die Oberfläche (Vorlage, Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“), Fenster headless. Claude nicht beteiligt. Zutaten gelesen und Nährwerte gerechnet hat der eigene Kern."); L("");
            var d = Open(w); seen = w.Core.Audit.ReadAll().Count;
            var vorher = d.Data.ToJsonString(); var hist = d.History.Count;
            const string p = "Mach aus diesem Text ein Rezept: Kartoffelsuppe für 4 Personen. Man braucht 800 g Kartoffeln, 2 Karotten, 1 Zwiebel, 1 Liter Gemüsebrühe, 100 ml Sahne und 1 EL Butter. "
                           + "Zwiebel in Butter andünsten, Kartoffeln und Karotten würfeln und dazugeben, mit Brühe aufgießen und 20 Minuten kochen, dann pürieren und die Sahne unterrühren.";
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var ph = await Ask(w, p, 900_000); sw.Stop();
            L($"- Auftrag: {p}");
            L($"- Phase: {ph} – {w.AssistLast?.Message}");
            L($"- Plan ({w.Find<TextBlock>("assist-source").Text?.Trim()}, {sw.Elapsed.TotalSeconds:F0} s): {string.Join(" → ", w.AssistLast!.Value.Steps)}");
            L($"- KI-Antwort (roh): `{(w.AssistLastKiAnswer ?? "").Replace("`", "'").ReplaceLineEndings(" ")}`"); KiAudit();
            Assert.Equal("vorschau", ph);
            Assert.Equal(vorher, d.Data.ToJsonString());
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler", 60000);
            Assert.True(w.AssistLast!.Value.Phase == "fertig", w.AssistLast.Value.Message);
            var r = Assert.Single(MainWindow.RzRezepte(d));
            var nw = RezeptKit.JePortion(r);
            L($"- Nach „Ja, behalten“: „{r.Name}“, {r.Portionen} {r.PortionWort}");
            foreach (var z in r.Zutaten) L($"  - {RezeptKit.Zeile(z)} → {RezeptKit.LmVon(z)?.Name ?? "nicht zugeordnet"}, {(RezeptKit.Gramm(z) is { } g ? Math.Round(g) + " g" : "ohne Gewicht")}");
            foreach (var (i, s) in r.Schritte.Select((s, i) => (i + 1, s))) L($"  {i}. {s}");
            L($"- Nährwerte je Portion (Kern, Richtwert): {Math.Round(nw.Kcal)} kcal, Eiweiß {Math.Round(nw.Eiweiss, 1)} g, Fett {Math.Round(nw.Fett, 1)} g, Kohlenhydrate {Math.Round(nw.Kh, 1)} g; nicht erfasst: {(nw.Fehlt.Count == 0 ? "–" : string.Join(", ", nw.Fehlt))}");
            L($"- Rückgängig-Liste: {hist} → {d.History.Count}, oben „{d.UndoLabel}“");
            Assert.True(r.Zutaten.Count >= 5, "zu wenige Zutaten"); Assert.True(r.Schritte.Count >= 2, "zu wenige Schritte");
            Assert.Contains(r.Zutaten, z => z.Lm == "kartoffel"); Assert.Equal(hist + 1, d.History.Count);
            Assert.Empty(RezeptKit.Gesundheitsversprechen(string.Join(" ", [r.Name, r.Beschreibung, .. r.Schritte])));
            Tap(w, "assist-undo");
            L($"- Rückgängig: Rezeptbuch wie vorher = {vorher == w.ProgramActive(K)!.Data.ToJsonString()}");
            Assert.Equal(vorher, w.ProgramActive(K)!.Data.ToJsonString());
            L(""); L("Ergebnis: bestanden.");
        }
        catch (Exception e) { L(""); L("FEHLER: " + e.Message); throw; }
        finally
        {
            File.WriteAllText(Path.Combine(beleg, "echtlauf-lokale-ki.md"), log.ToString().Replace(Environment.UserName, "<user>"), new UTF8Encoding(false));
            MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.VramOverride = null; w.InstalledModelsOverride = null; w.Close();
        }
    }
}
