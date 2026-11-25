using System.IO.Compression;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.PromptAction;
using Trendo.Core.Sys;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0765 (X-03 echter App-Baukasten) über die Oberfläche: Leerzustand mit erstem Schritt → Vorlage „Einkaufsliste“ (zwei verlinkte Seiten,
/// Daten) → Seite in der Vorschau wechseln → Text per Doppelklick direkt bearbeiten (Hülle sichert das Getippte) → Rückgängig, Wiederholen (Strg+Y und
/// Strg+Umschalt+Z) → Seiten anlegen, umbenennen, sortieren, löschen → Knopf-Ziel „zu Seite X“ → Felder der Sammlung → Rechtsklick, Entf → Export als ZIP
/// → ZIP über „Öffnen“ wieder öffnen (Rundreise) → Rückgängig. Dazu: KI-Ergebnis „seite.schreiben“ ist im Baukasten rückgängig machbar.</summary>
public class T0765_AppBaukasten_Ui
{
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static bool Has(MainWindow w, string name) { Settle(); return w.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name && c.IsEffectivelyVisible); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); Settle(); }
    static Point Mid(MainWindow w, Control c) { c.BringIntoView(); Settle(); return c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value; }
    static void Key(MainWindow w, Key k, RawInputModifiers m = RawInputModifiers.None) { w.KeyPress(k, m, PhysicalKey.None, null); Settle(); w.KeyRelease(k, m, PhysicalKey.None, null); Settle(); }
    static void Grid(MainWindow w, Key k, KeyModifiers m = KeyModifiers.None)
    {
        var g = Get<Grid>(w, "builder-designer");
        g.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = k, KeyModifiers = m, Source = g }); Settle();
    }
    static void TypeText(MainWindow w, string text) { foreach (var ch in text) { w.KeyTextInput(ch.ToString()); Settle(); } }

    [AvaloniaFact(DisplayName = "T-0765 (UI): erster Schritt → Vorlage Einkaufsliste → Seite wechseln → Doppelklick-Text → Rückgängig/Wiederholen → Seiten verwalten → Knopf-Ziel → Felder → Export → ZIP wieder öffnen")]
    public void App_aus_vorlage_bearbeiten_und_rundreise()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1200;
        try
        {
            w.Show("baukasten"); Settle();
            // Leerzustand: erster Schritt sichtbar, eine goldene Hauptaktion (Vorlage), Abgrenzung zum Web-Design-Studio
            Assert.True(w.BuilderFresh);
            Assert.True(Has(w, "builder-first"));
            Assert.Contains("Baukasten = einfache Apps ohne Code. Für Profi-Webseiten mit eigenem Design gibt es das Web-Design-Studio.", w.GetLogicalTexts());
            Assert.False(File.Exists(w.BuilderFile));

            Press(w, "builder-tpl-einkaufsliste");
            Assert.False(Has(w, "builder-first"));
            Assert.Equal(["liste", "neu"], w.BuilderPage.Pages.Select(s => s.Id));
            Assert.True(SitePage.Load(w.BuilderFile).IsApp); // sofort gespeichert
            Assert.True(Has(w, "builder-nav-1"));

            // in der Vorschau auf die zweite Seite wechseln: Formular mit Pflichtfeld
            Press(w, "builder-nav-1");
            Assert.Equal(1, w.BuilderScreen);
            Assert.Contains(w.BuilderScreenBlocks, b => b.Kind == "form" && b.Data == "einkaeufe");
            Assert.Contains("Artikel *", w.GetLogicalTexts());

            // Doppelklick auf die Überschrift: Text direkt auf der Seite bearbeiten
            var p = Mid(w, Get<Border>(w, "builder-block-0"));
            w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); Settle();
            w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); Settle();
            Assert.Equal(0, w.BuilderInline);
            var tb = Get<TextBox>(w, "builder-inline-0");
            Assert.True(tb.IsFocused);
            TypeText(w, "Neu eintragen"); // ersetzt die Auswahl
            Assert.Equal("Neu eintragen", w.LiveInputsOf("baukasten")["builder-inline-0"]); // die Hülle sichert das Getippte (Notsicherung)
            Key(w, Avalonia.Input.Key.Enter);
            Assert.Equal(-1, w.BuilderInline);
            Assert.Equal("Neu eintragen", w.BuilderScreenBlocks[0].Text);
            Assert.Equal("Neu eintragen", SitePage.Load(w.BuilderFile).Pages[1].Blocks[0].Text);

            // Rückgängig (Knopf), Wiederholen (Strg+Y), noch einmal zurück, Wiederholen (Strg+Umschalt+Z)
            Press(w, "builder-undo"); Assert.Equal("Etwas hinzufügen", w.BuilderPage.Pages[1].Blocks[0].Text);
            Grid(w, Avalonia.Input.Key.Y, KeyModifiers.Control); Assert.Equal("Neu eintragen", w.BuilderPage.Pages[1].Blocks[0].Text);
            Grid(w, Avalonia.Input.Key.Z, KeyModifiers.Control); Assert.Equal("Etwas hinzufügen", w.BuilderPage.Pages[1].Blocks[0].Text);
            Grid(w, Avalonia.Input.Key.Z, KeyModifiers.Control | KeyModifiers.Shift); Assert.Equal("Neu eintragen", w.BuilderPage.Pages[1].Blocks[0].Text);
            Assert.Equal("Neu eintragen", SitePage.Load(w.BuilderFile).Pages[1].Blocks[0].Text);

            // F2 bearbeitet den gewählten Baustein, Esc bricht ab (nichts geändert)
            w.BuilderSel = 1; w.Show("baukasten"); Grid(w, Avalonia.Input.Key.F2);
            Assert.Equal(1, w.BuilderInline);
            Key(w, Avalonia.Input.Key.Escape); Assert.Equal(-1, w.BuilderInline);

            // Seiten: anlegen, umbenennen, nach vorn, löschen
            Press(w, "builder-page-add");
            Assert.Equal(3, w.BuilderPage.Pages.Count); Assert.Equal(2, w.BuilderScreen);
            Assert.Contains(w.BuilderScreenBlocks, b => b.Kind == "button" && b.Url == "seite:liste");
            var name = Get<TextBox>(w, "builder-page-name"); name.Text = "Kontakt";
            name.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter }); Settle();
            Assert.Equal("Kontakt", w.BuilderPage.Pages[2].Name);
            Assert.Contains("Kontakt", w.GetLogicalTexts());
            Press(w, "builder-page-up"); Assert.Equal(["liste", "seite-3", "neu"], w.BuilderPage.Pages.Select(s => s.Id)); Assert.Equal(1, w.BuilderScreen);
            Press(w, "builder-page-del"); Assert.Equal(["liste", "neu"], w.BuilderPage.Pages.Select(s => s.Id));

            // Knopf-Ziel „zu Seite X“ (erste Seite, Baustein 4 = „Etwas hinzufügen“)
            Press(w, "builder-page-0"); Assert.Equal(0, w.BuilderScreen);
            Assert.Equal("seite:neu", w.BuilderScreenBlocks[4].Url);
            Tap(w, Get<Border>(w, "builder-block-4")); Assert.Equal(4, w.BuilderSel);
            Get<ComboBox>(w, "builder-target-4").SelectedIndex = 1; Settle(); // „Seite: Liste“
            Assert.Equal("seite:liste", w.BuilderScreenBlocks[4].Url);
            Get<ComboBox>(w, "builder-target-4").SelectedIndex = 2; Settle(); // „Seite: Hinzufügen“
            Assert.Equal("seite:neu", w.BuilderScreenBlocks[4].Url);

            // Datensammlung: Feld hinzufügen, Art wechseln, Pflicht
            Press(w, "builder-field-add-0");
            Assert.Equal(4, w.BuilderPage.Collections[0].Fields.Count);
            Press(w, "builder-fieldtype-0-3"); Assert.Equal("zahl", w.BuilderPage.Collections[0].Fields[3].Type);
            Get<CheckBox>(w, "builder-fieldreq-0-3").IsChecked = true; Settle(); Assert.True(w.BuilderPage.Collections[0].Fields[3].Required);
            Press(w, "builder-fielddel-0-3"); Assert.Equal(3, w.BuilderPage.Collections[0].Fields.Count);

            // Baustein „Auswertung“ aus der Leiste: hängt an der Sammlung, zählt
            Press(w, "builder-add-stat");
            Assert.Equal("stat", w.BuilderScreenBlocks[5].Kind); Assert.Equal("einkaeufe", w.BuilderScreenBlocks[5].Data); Assert.Equal("anzahl", w.BuilderScreenBlocks[5].Url);
            // Rechtsklick-Menü am Baustein, Entf entfernt
            Assert.Contains(((IEnumerable<object>)Get<Border>(w, "builder-block-5").ContextMenu!.ItemsSource!).OfType<MenuItem>(), m => m.Name == "builder-ctx-del-5");
            Grid(w, Avalonia.Input.Key.Delete); Assert.Equal(5, w.BuilderScreenBlocks.Count);

            // Export als ZIP: eigene App mit Navigation, Abschnitten, Formular und Skript
            var zip = w.BuilderExport();
            string html; using (var z = ZipFile.OpenRead(zip)) { using var r = new StreamReader(z.GetEntry("index.html")!.Open()); html = r.ReadToEnd(); }
            Assert.Contains("<section id=\"seite-neu\"", html); Assert.Contains("href=\"#seite-neu\"", html); Assert.Contains("<form class=\"t-form\"", html);
            Assert.Contains("Neu eintragen", html);

            // Rundreise: Titel ändern, dann die exportierte ZIP über „Öffnen (ZIP) …“ wieder öffnen
            var title = Get<TextBox>(w, "builder-title"); title.Text = "Anders";
            title.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter }); Settle();
            Assert.Equal("Anders", w.BuilderPage.Title);
            var exported = SitePage.FromExport(zip).ToJson();
            w.PickOverride = _ => Task.FromResult<string[]?>([zip]);
            Press(w, "builder-open");
            Assert.Equal("Einkaufsliste", w.BuilderPage.Title);
            Assert.Equal(exported, w.BuilderPage.ToJson());
            Assert.Equal(exported, SitePage.Load(w.BuilderFile).ToJson());
            Assert.Contains(w.Core.Audit.ReadAll(), x => x.Action == "builder.import");
            Press(w, "builder-undo"); Assert.Equal("Anders", w.BuilderPage.Title); // Öffnen ist ein Schritt
        }
        finally { w.Close(); }
    }

    static void Tap(MainWindow w, Control c) { var p = Mid(w, c); w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); Settle(); }

    static int GoldButtons(MainWindow w, Color gold) =>
        w.GetVisualDescendants().OfType<Button>().Count(b => b.IsEffectivelyVisible && b.Name is { } n && (n.StartsWith("builder-", StringComparison.Ordinal)) && b.Background is ISolidColorBrush s && s.Color == gold);

    [AvaloniaFact(DisplayName = "T-0765 Gegenprüfung (UI): eine goldene Hauptaktion je Zustand, Speicher-Hinweis, zu große ZIP wird sichtbar abgelehnt, Import überschreibt nichts und schreibt bei neuerer seite.json gar nichts")]
    public void Gegenpruefung_ui()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1200;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-t0765g-ui-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(dir);
        try
        {
            w.Show("baukasten"); Settle();
            var gold = ((ISolidColorBrush)Get<Button>(w, "builder-tpl-einkaufsliste").Background!).Color;
            Assert.Equal(1, GoldButtons(w, gold)); // Leerzustand: nur „Vorlage nehmen“
            Press(w, "builder-tpl-einkaufsliste");
            Assert.Equal(1, GoldButtons(w, gold)); // danach: nur „Ausprobieren im Browser“ (Handy/Bildschirm zeigen ✓ statt Gold)
            Assert.Equal(gold, ((ISolidColorBrush)Get<Button>(w, "builder-preview").Background!).Color);
            Assert.Contains("✓ Handy", w.GetLogicalTexts());
            Assert.Equal(1.0, Get<Button>(w, "builder-open").BorderThickness.Top); // als Knopf erkennbar (Rahmen)
            Assert.Equal(1.0, Get<Button>(w, "builder-veroeffentlichen").BorderThickness.Top);
            Assert.Contains(SitePage.StorageNote, w.GetLogicalTexts());

            // vorhandenes Bild im Baukasten-Ordner
            File.WriteAllText(Path.Combine(w.BuilderFolder, "logo.png"), "ALT");
            var before = w.BuilderPage.ToJson();
            // zu große ZIP über „Öffnen (ZIP) …“: sichtbare Meldung, App unverändert
            var big = Path.Combine(dir, "gross.zip");
            using (var a = ZipFile.Open(big, ZipArchiveMode.Create)) { using var s = a.CreateEntry("index.html").Open(); var buf = new byte[1 << 20]; for (var i = 0; i < 21; i++) s.Write(buf); }
            w.PickOverride = _ => Task.FromResult<string[]?>([big]);
            Press(w, "builder-open");
            Assert.Contains(w.GetLogicalTexts(), t => t.Contains("zu groß", StringComparison.Ordinal));
            Assert.Equal(before, w.BuilderPage.ToJson());

            // gültige ZIP mit eigenem logo.png: vorhandenes bleibt, Bild landet unter importiert/…
            var page = SitePage.Template("einkaufsliste")!; page.Title = "Mit Logo"; page.Pages[0].Blocks.Add(new Block("img", "Logo", "logo.png"));
            var ok = Path.Combine(dir, "ok.zip");
            using (var a = ZipFile.Open(ok, ZipArchiveMode.Create))
            {
                using (var wr = new StreamWriter(a.CreateEntry("index.html").Open())) wr.Write(page.ToHtml());
                using (var wr = new StreamWriter(a.CreateEntry("logo.png").Open())) wr.Write("NEU");
            }
            w.PickOverride = _ => Task.FromResult<string[]?>([ok]);
            Press(w, "builder-open");
            Assert.Equal("Mit Logo", w.BuilderPage.Title);
            Assert.Equal("ALT", File.ReadAllText(Path.Combine(w.BuilderFolder, "logo.png")));
            var url = w.BuilderPage.Pages[0].Blocks.Single(b => b.Kind == "img").Url;
            Assert.StartsWith("importiert/", url);
            Assert.Equal("NEU", File.ReadAllText(Path.Combine(w.BuilderFolder, url)));

            // neuere seite.json (T-0716): Öffnen schreibt gar nichts
            File.WriteAllText(w.BuilderFile, "{ \"Version\": 99, \"Title\": \"Zukunft\" }");
            var files = Directory.GetFiles(w.BuilderFolder, "*", SearchOption.AllDirectories).Length;
            w.Show("baukasten"); Settle();
            Press(w, "builder-open");
            Assert.Equal(files, Directory.GetFiles(w.BuilderFolder, "*", SearchOption.AllDirectories).Length);
            Assert.Equal("{ \"Version\": 99, \"Title\": \"Zukunft\" }", File.ReadAllText(w.BuilderFile));
            Assert.Contains(w.GetLogicalTexts(), t => t.Contains("neueren Trendo-Fassung", StringComparison.Ordinal));
        }
        finally { w.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [AvaloniaFact(DisplayName = "T-0765 (UI): KI „seite.schreiben“ ändert die App im Projekt – im Baukasten sichtbar und mit einem Klick rückgängig, Seiten und Daten bleiben")]
    public void Ki_ergebnis_rueckgaengig()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1200;
        try
        {
            var project = w.Core.Projects.Create("app", "App").Id;
            w.Show("baukasten"); Settle();
            Press(w, "builder-tpl-einkaufsliste");
            var json = w.Core.Projects.ResolveInProject(project, "baukasten/seite.json");
            Assert.True(SitePage.Load(json).IsApp);

            var wf = new StudioWorkflow(w.Core, PromptActions.CreateDefault());
            var ctx = new StudioExecutionContext(project, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default);
            var req = new StudioPromptRequest("ki-1", project, "X-03", "Titel: Wocheneinkauf\nText: Alles für die Woche", [], "de", null, PrivacyMode.Local, new Provenance("test", null, null));
            var r = wf.Los(project, wf.Start(req, ctx).RunId, ctx);
            Assert.Equal(RunStatus.Verified, r.Status);

            w.Show("baukasten"); Settle();
            Assert.Equal("Wocheneinkauf", w.BuilderPage.Title);
            Assert.Equal(["liste", "neu"], w.BuilderPage.Pages.Select(s => s.Id)); // Seiten und Daten der App bleiben
            Assert.Single(w.BuilderPage.Collections);
            Assert.Contains("Alles für die Woche", w.GetLogicalTexts());

            Press(w, "builder-undo");
            Assert.Equal("Einkaufsliste", w.BuilderPage.Title);
            Assert.Equal("Einkaufsliste", SitePage.Load(json).Title); // auch im Projekt zurück
            Press(w, "builder-redo");
            Assert.Equal("Wocheneinkauf", SitePage.Load(json).Title);
        }
        finally { w.Close(); }
    }
}
