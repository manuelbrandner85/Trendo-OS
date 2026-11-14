using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0616 „Einfacher bedienen“ (E1–E12, freigegeben 14.11.2026): so einfach wie möglich, ohne eine Funktion zu verlieren.</summary>
public class EinfachBedienen_Ui
{
    const string MP = "mode-programm"; // Kleidung & Produkte: Vorlagen, Eigenschaften, Export
    static IEnumerable<T> All<T>(Control root) where T : Control => root.GetLogicalDescendants().OfType<T>();
    static T? Named<T>(Control root, string name) where T : Control => All<T>(root).FirstOrDefault(c => c.Name == name);
    static Control FlyoutOf(Button b) => (Control)((Flyout)b.Flyout!).Content!;
    static bool CapabilityCatalogHas(string studio) => Trendo.Core.PromptAction.CapabilityCatalog.Find(studio) is not null;

    [AvaloniaFact(DisplayName = "E1: Ein Knopf „KI ▾“ neben „Los“ – Claude, Markierung, kie.ai und Hinzufügen im Klappmenü; kein zweites Claude im Programmband")]
    public void One_ki_button()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show(MP); Settle(); w.ProgramNew(MP, "tshirt"); w.Show(MP); Settle();
            var root = w.ActivePageRoot!;
            var ki = Named<Button>(root, "ki-menu")!; Assert.True(ki.IsEffectivelyVisible);
            var pop = Named<Popup>(root, "ki-menu-popup")!; Assert.False(pop.IsOpen);
            var items = All<Button>((Control)pop.Child!).Select(b => b.Name).ToList();
            Assert.Equal("auftrag-claude", items[0]);
            Assert.Contains("auftrag-claude-markierung", items); Assert.Contains("auftrag-kie", items);
            Assert.Null(Named<Button>(root, "prog-claude")); Assert.Null(Named<Button>(root, "prog-claude-ask"));
            // neben „Los“ stehen keine einzelnen KI-Knöpfe mehr
            var bar = Named<Border>(root, "auftrag-leiste")!;
            Assert.DoesNotContain(All<Button>(bar).Where(b => b.IsEffectivelyVisible && b.FindLogicalAncestorOfType<Popup>() is null), b => b.Name is "auftrag-claude" or "auftrag-kie" or "auftrag-claude-markierung");
            ki.Press(); Assert.True(pop.IsOpen);
            ki.Press(); Assert.False(pop.IsOpen);
            // Hinzufügen geht weiterhin (Studio mit benötigten Programmen)
            var studio = StudioTools.Required.Keys.First(k => w.PageForStudio(k) is { } pk && w.ModuleOf(pk) is not null && CapabilityCatalogHas(k));
            w.Show(w.PageForStudio(studio)!); Settle();
            w.Find<Button>("auftrag-hinzufuegen").Press(); Settle();
            Assert.Equal("hinzufuegen", w.CurrentView);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "E2: „Einfach“ klappt Stufenwahl, Eigenschaften und Rand-Schalter ein; „Mehr Werkzeuge“ holt sie, die Stufe bleibt im „Mehr“-Menü wählbar")]
    public void Simple_tidies_up()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.ProgramSetLevel(UserLevel.Einfach); w.Show(MP); Settle();
            w.Find<Button>("prog-tpl-tshirt").Press(); Settle();
            Assert.Null(w.TryFind<Button>("prog-level-einfach")); Assert.Null(w.TryFind<Border>("prog-properties"));
            Assert.False(w.Find<StackPanel>("prog-panel-toggles").IsVisible);
            var more = w.Find<Button>("prog-more");
            var list = (Control)((ScrollViewer)FlyoutOf(more)).Content!;
            Assert.Contains(All<Button>(list), b => b.Name == "prog-stufe-erweitert");
            w.Find<Button>("prog-tools-more").Press(); Settle();
            Assert.NotNull(w.TryFind<Button>("prog-level-einfach")); Assert.NotNull(w.TryFind<Border>("prog-properties"));
            Assert.True(w.Find<StackPanel>("prog-panel-toggles").IsVisible);
            Assert.Equal("Weniger Werkzeuge", w.Find<Button>("prog-tools-more").Content);
            w.Find<Button>("prog-tools-more").Press(); Settle();
            Assert.Null(w.TryFind<Border>("prog-properties"));
            // Stufe über das Menü „Mehr“ wechseln
            var lv = All<Button>((Control)((ScrollViewer)FlyoutOf(w.Find<Button>("prog-more"))).Content!).First(b => b.Name == "prog-stufe-erweitert");
            lv.Press(); Settle();
            Assert.Equal(UserLevel.Erweitert, w.ProgramLevel);
            Assert.NotNull(w.TryFind<Border>("prog-properties")); Assert.NotNull(w.TryFind<Button>("prog-level-einfach"));
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "E3: Automatisch speichern – unverändert keine Datei, nach einer Änderung im Studio-Ordner, danach bei jeder Änderung; Anzeige „Gespeichert“; Schließen verliert nichts")]
    public void Autosave()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            w.AutoSaveDocs = true;
            w.Show(MP); Settle(); w.Find<Button>("prog-tpl-tshirt").Press(); Settle();
            var d = w.ProgramActive(MP)!;
            Assert.Equal(0, w.ProgramAutosaveNow()); Assert.Null(d.Path); // leere, unveränderte Vorlage legt nichts an
            w.Find<Button>("prog-cmd-variants").Press(); Settle();
            Assert.True(d.Dirty); Assert.Equal("Wird gespeichert …", w.Find<TextBlock>("prog-dirty").Text);
            Assert.Equal(1, w.ProgramAutosaveNow());
            Assert.False(d.Dirty); Assert.NotNull(d.Path); Assert.True(File.Exists(d.Path));
            Assert.StartsWith(root, Path.GetFullPath(d.Path!)); Assert.EndsWith(".tdoc", d.Path);
            Assert.Equal("✓ Gespeichert", w.Find<TextBlock>("prog-dirty").Text);
            Assert.Equal(12, ProgramDocument.Load(d.Path!).List("variants").Count);
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "program.autosave");
            // weitere Änderung: gleiche Datei, kein Datei-Wirrwarr
            var file = d.Path!; d.Set("name", "Shirt Bio"); w.Show(MP); Settle();
            Assert.Equal(1, w.ProgramAutosaveNow()); Assert.Equal(file, d.Path);
            Assert.Equal("Shirt Bio", ProgramDocument.Load(file).Text("name"));
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(file)!, "*.tdoc"));
            // Schließen mit ungespeicherter Änderung speichert, statt nur zu sichern
            d.Set("name", "Shirt Pro"); w.Show(MP); Settle();
            w.Find<Button>("prog-close").Press(); Settle();
            Assert.Empty(w.ProgramDocs(MP));
            Assert.Equal("Shirt Pro", ProgramDocument.Load(file).Text("name"));
            Assert.Empty(ProgramDocument.Orphans(Path.Combine(root, "programme", "sicherung"), "SPEC-08"));
            // ausgeschaltet: wie früher nur sichern
            w.AutoSaveDocs = false; w.ProgramNew(MP, "tshirt").Set("name", "X"); w.ProgramAutosaveAll();
            Assert.Single(ProgramDocument.Orphans(Path.Combine(root, "programme", "sicherung"), "SPEC-08"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0620: Von selbst angelegte Datei folgt dem neuen Titel (samt Versionen); selbst benannte Dateien bleiben, wie sie sind")]
    public void Autosave_rename()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            w.AutoSaveDocs = true;
            w.Show(MP); Settle(); w.Find<Button>("prog-tpl-tshirt").Press(); Settle();
            var d = w.ProgramActive(MP)!;
            d.Set("name", "Erstes Shirt"); Assert.Equal(1, w.ProgramAutosaveNow());
            var first = d.Path!;
            d.Title = "Sommer-Kollektion"; d.Set("name", "Zweites Shirt");
            Assert.Equal(1, w.ProgramAutosaveNow());
            Assert.Equal("Sommer-Kollektion.tdoc", Path.GetFileName(d.Path));
            Assert.False(File.Exists(first)); // keine verwaiste „Unbenannt“-Datei
            Assert.Equal("Zweites Shirt", ProgramDocument.Load(d.Path!).Text("name"));
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(d.Path!)!, "*.tdoc"));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "program.autosave.rename");
            // selbst gespeichert unter eigenem Namen: Titeländerung benennt die Datei nicht mehr um
            var own = Path.Combine(root, "eigen", "Mein Name.tdoc");
            w.ProgramSave(MP, own);
            d.Title = "Anderer Titel"; d.Set("name", "Drittes Shirt");
            Assert.Equal(1, w.ProgramAutosaveNow());
            Assert.Equal(own, d.Path);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "E4 + E5: Speichern unter und Öffnen mit der Dateiauswahl; „Weitermachen“ öffnet das zuletzt bearbeitete Dokument – auf der Startansicht und auf dem Desktop")]
    public async Task Pick_and_continue()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var target = Path.Combine(root, "Eigener Ort", "Kollektion.tdoc"); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var asked = new List<MainWindow.PickRequest>();
            w.PickOverride = r => { asked.Add(r); return Task.FromResult<string[]?>([target]); };
            w.Show(MP); Settle(); w.Find<Button>("prog-tpl-tshirt").Press(); Settle();
            var open = w.Find<Button>("prog-saveas-open");
            All<Button>(FlyoutOf(open)).First(b => b.Name == "prog-saveas-pick").Press(); await Task.Delay(20); Settle();
            Assert.Equal("prog-saveas", asked[^1].Field); Assert.Contains("tdoc", asked[^1].Extensions);
            Assert.Equal(target, w.ProgramActive(MP)!.Path); Assert.True(File.Exists(target));
            Assert.Contains(All<TextBox>(FlyoutOf(w.Find<Button>("prog-saveas-open"))), t => t.Name == "prog-saveas-path"); // eintippen bleibt möglich (auch für Claude)
            w.Find<Button>("prog-close").Press(); Settle();
            // Startansicht: oberste Karte „Weitermachen“
            var cont = w.Find<Button>("prog-continue");
            Assert.Contains("Kollektion", string.Join(" ", All<TextBlock>(cont).Select(t => t.Text)));
            cont.Press(); Settle();
            Assert.Equal(target, w.ProgramActive(MP)!.Path);
            // Öffnen über die Auswahl
            w.Find<Button>("prog-close").Press(); Settle();
            Assert.Null(w.TryFind<TextBox>("prog-open-path")); // kein getippter Pfad mehr
            w.Find<Button>("prog-open").Press(); await Task.Delay(20); Settle();
            Assert.Equal("prog-open", asked[^1].Field); Assert.Equal(target, w.ProgramActive(MP)!.Path);
            // Desktop „Weitermachen“: öffnet das Dokument, nicht nur die Seite
            w.Find<Button>("prog-close").Press(); Settle();
            w.Show("start"); Settle();
            var card = All<Button>(w.Find<WrapPanel>("continue-cards")).First(b => (ToolTip.GetTip(b) as string ?? "").Contains("Kollektion"));
            card.Press(); Settle();
            Assert.Equal(MP, w.CurrentView); Assert.Equal(target, w.ProgramActive(MP)!.Path);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "E6: „Was möchtest du machen?“ startet die Aufgabe – fehlt eine Angabe, fragt Trendo kurz und startet dann mit der Antwort")]
    public void Tasks_start()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.AutoRunPlans = false;
            var key = MainWindow.StudioPages["CORE-06"];
            w.Show(key); Settle();
            w.Find<Button>("studio-task-figur").Press(); Settle();
            Assert.StartsWith("Entwirf für mein Spiel eine Figur", w.Find<TextBox>("auftrag-prompt").Text);
            Assert.Contains("offenes Spiel", w.Find<TextBlock>("studio-task-q").Text);
            w.Find<TextBox>("studio-task-answer").Text = "eine mutige Füchsin";
            w.Find<Button>("studio-task-go").Press(); Settle();
            Assert.Equal("Entwirf für mein Spiel eine Figur: eine mutige Füchsin", w.Find<TextBox>("auftrag-prompt").Text);
            Assert.Null(w.TryFind<Border>("studio-task-question"));
            Assert.NotEqual("Noch nicht gestartet", w.Find<TextBlock>("auftrag-status").Text); // der normale Ablauf läuft (Plan, Rückfrage, Freigabe)
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "E7: Fehler im Programm als Alltagssatz mit „Was tun?“-Knopf und aufklappbarem technischem Text; Benachrichtigung mit ausdrücklichem Ton")]
    public async Task Errors_with_fix()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var missing = Path.Combine(root, "gibt-es-nicht.tdoc");
            w.PickOverride = _ => Task.FromResult<string[]?>([missing]);
            w.Show(MP); Settle();
            w.Find<Button>("prog-open").Press(); await Task.Delay(20); Settle();
            var fe = w.ProgramError(MP)!;
            Assert.Equal(ErrorFix.PickFolder, fe.Fix);
            Assert.Equal("⚠ " + fe.Sentence, w.Find<TextBlock>("error-text").Text);
            Assert.Equal("Anderen Ort wählen", w.Find<Button>("error-fix").Content);
            Assert.Contains("FileNotFoundException", ((SelectableTextBlock)w.Find<Expander>("error-details").Content!).Text);
            // Ein erfolgreicher Befehl räumt die Fehlerkarte weg
            w.Find<Button>("prog-tpl-tshirt").Press(); Settle();
            Assert.Null(w.ProgramError(MP)); Assert.Null(w.TryFind<StackPanel>("error-card"));
            Assert.Equal("!", MainWindow.ToastTone("⚠ Etwas ist schiefgegangen").Icon);
            w.ToastError(new UnauthorizedAccessException("Access to the path 'C:\\Windows\\x' is denied."));
            Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("⚠ Trendo darf an diesem Ort nicht"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "E8: Kurze Einführung beim ersten Öffnen (drei Schritte, überspringbar, gemerkt) und über „Einführung“ wieder abrufbar")]
    public void Intro()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            w.ProgramIntroOn = true;
            w.Show(MP); Settle();
            Assert.Contains("Schritt 1 von 3", w.Find<TextBlock>("prog-intro-step").Text);
            w.Find<Button>("prog-intro-next").Press(); Settle();
            Assert.StartsWith("Hier hilft die KI", w.Find<TextBlock>("prog-intro-text").Text);
            w.Find<Button>("prog-intro-next").Press(); Settle();
            Assert.Equal("Fertig", w.Find<Button>("prog-intro-next").Content);
            w.Find<Button>("prog-intro-next").Press(); Settle();
            Assert.Null(w.TryFind<Border>("prog-intro"));
            Assert.Contains("intro:" + MP, UserSettings.Load(root).SeenHints);
            w.Show(MP); Settle(); Assert.Null(w.TryFind<Border>("prog-intro")); // gemerkt
            w.Find<Button>("prog-tpl-tshirt").Press(); Settle();
            w.Find<Button>("prog-intro-show").Press(); Settle();
            Assert.NotNull(w.TryFind<Border>("prog-intro"));
            w.Find<Button>("prog-intro-skip").Press(); Settle();
            Assert.Null(w.TryFind<Border>("prog-intro"));
            // in abgeschotteten Tests ohne Zutun aus – nichts wartet auf einen Klick
            var (w2, _, _) = Ui.Open();
            try { Assert.False(w2.ProgramIntroOn); w2.Show(MP); Settle(); Assert.Null(w2.TryFind<Border>("prog-intro")); }
            finally { w2.Close(); }
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "E9 + E10: Eine Wahl „Einfach / Erweitert / Profi“ (größere Knöpfe getrennt); Alltagswörter in der linken Leiste mit Fachwort als Hinweis")]
    public void One_switch_and_words()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("bedien-sehen"); Settle();
            Assert.DoesNotContain(w.GetLogicalTexts(), t => t.Contains("Einfachen Modus"));
            Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("Bedienung: Einfach, Erweitert oder Profi"));
            Assert.NotNull(w.TryFind<Button>("level-einfach")); Assert.Equal("Größere Knöpfe", w.Find<CheckBox>("simple-mode").Content);
            Assert.NotNull(w.TryFind<CheckBox>("auto-save"));
            var side = w.Find<Button>("side-ki");
            Assert.Contains("KI-Zentrale", string.Join(" ", All<TextBlock>(side).Select(t => t.Text)));
            Assert.Contains("Command Center", ToolTip.GetTip(side) as string);
            Assert.Contains("Freie Programme", string.Join(" ", All<TextBlock>(w.Find<Border>("widget-github")).Select(t => t.Text)));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "E11: Studio-Übersicht mit einem Suchfeld (Tippen filtert, Enter sucht überall); Hilfetext zu Strg+K stimmt")]
    public void One_search()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("studios"); Settle();
            Assert.Null(w.TryFind<TextBox>("studios-ask"));
            Assert.Single(All<TextBox>(w.ActivePageRoot!), t => t.IsVisible);
            var box = w.Find<TextBox>("txt-studio-search"); box.Text = "Brief"; Settle();
            box = w.Find<TextBox>("txt-studio-search");
            box.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter }); Settle();
            Assert.Equal("suche", w.CurrentView);
            Assert.DoesNotContain("Mit Strg+K springst", MainWindow.HelpTexts["suche"]);
            Assert.Contains("Strg+K öffnet die Befehlsleiste", MainWindow.HelpTexts["suche"]);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "E12: „Einfach“ blendet rechte Spalte, Erdkugel und Schnellaktionen aus (einblendbar unter „Anpassen“); Einstellungen zeigen die Hauptseiten, Rest unter „Mehr“")]
    public void Slim_desktop_and_settings()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            w.Show("start"); Settle();
            Assert.True(w.Find<Border>("widget-ki").IsVisible); Assert.True(w.Find<Grid>("widget-eco").IsVisible);
            w.ProgramSetLevel(UserLevel.Einfach); w.Show("start"); Settle();
            foreach (var n in new[] { "widget-ki", "widget-system", "widget-agenten", "widget-studiomanager", "widget-github" }) Assert.False(w.Find<Border>(n).IsVisible, n);
            Assert.False(w.Find<Grid>("widget-eco").IsVisible); Assert.False(w.Find<WrapPanel>("desk-actions").IsVisible);
            Assert.Equal(0, w.Find<Grid>("shell").ColumnDefinitions[2].Width.Value); // die Mitte bekommt den Platz
            // einblenden über „Startbildschirm anpassen“
            w.Show("settings"); Settle();
            var cb = w.Find<CheckBox>("show-widget-ki"); Assert.False(cb.IsChecked);
            cb.IsChecked = true; Settle();
            Assert.Contains("widget-ki", UserSettings.Load(root).ShownTiles);
            Assert.True(w.TileShown("widget-ki")); Assert.False(w.TileShown("widget-agenten"));
            // Einstellungen: Hauptseiten, Rest unter „Mehr Einstellungen“ – die Suche findet alles
            Assert.Null(w.TryFind<Button>("settings-nav-apps")); Assert.NotNull(w.TryFind<Button>("settings-nav-personen"));
            w.Find<Button>("settings-nav-more").Press(); Settle();
            Assert.NotNull(w.TryFind<Button>("settings-nav-apps"));
            Assert.Contains(w.SettingsSearch("Apps"), h => h.Key == "apps");
            w.ProgramSetLevel(UserLevel.Erweitert); w.Show("settings"); Settle();
            Assert.Null(w.TryFind<Button>("settings-nav-more")); Assert.NotNull(w.TryFind<Button>("settings-nav-apps"));
            Assert.True(w.Find<Border>("widget-agenten").IsVisible);
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }
}
