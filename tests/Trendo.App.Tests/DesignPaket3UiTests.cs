using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0700 Design-Paket 3 (Bild-Audit vom 16.11.2026, freigegeben): V-07 Dateiauswahl mit einem Knopf, V-09 „Jetzt einrichten“ an der Hauptaktion,
/// V-11 Alltagswörter, V-12 Knöpfe statt Schreibzeichen, V-14 Speichern/Rückgängig am selben Platz, V-15 Reiter nur mit Inhalt, V-17 eine Flächenfarbe und ruhige Kennzahlen,
/// offene Punkte aus T-0686 („Los“, Hauptaktion je Programm, Gold in allen Bedienstilen) und Ausrichtungs-Symbole statt Dreiecken.
/// Echte Oberflächen-Tests im Avalonia-Headless-Fenster; ersetzt ist nur die System-Dateiauswahl (<see cref="MainWindow.PickOverride"/>).</summary>
public class DesignPaket3_Ui
{
    static void Settle() { for (var i = 0; i < 6; i++) Dispatcher.UIThread.RunJobs(); }

    static MainWindow Open(int level = UserLevel.Einfach)
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        w.Settings.SetupDone = true;
        w.ProgramSetLevel(level);
        return w;
    }

    static void Go(MainWindow w, string key)
    {
        foreach (var old in w.Windows.Stack.Reverse().Skip(3).ToList()) w.CloseWindow(old.Id);
        w.Show(key);
        if (w.Windows.Active is { Maximized: false } win) { w.Windows.ToggleMaximize(win.Id); w.Show(key); }
        Settle(); w.UpdateLayout(); Settle();
    }

    static ProgramDocument Doc(MainWindow w, string key, string? tpl = null)
    {
        foreach (var old in w.Windows.Stack.Reverse().Skip(3).ToList()) w.CloseWindow(old.Id);
        while (w.ProgramDocs(key).Count > 0) w.ProgramClose(key, 0);
        w.Show(key);
        var d = w.ProgramNew(key, tpl ?? MainWindow.Programs[key].Templates.FirstOrDefault()?.Id);
        Go(w, key);
        return d;
    }

    static IEnumerable<T> In<T>(MainWindow w) where T : Control => w.ActivePageRoot!.GetLogicalDescendants().OfType<T>();
    static T? Named<T>(MainWindow w, string name) where T : Control => In<T>(w).FirstOrDefault(c => c.Name == name);
    static T Need<T>(MainWindow w, string name) where T : Control => Named<T>(w, name) ?? throw new Xunit.Sdk.XunitException($"{name} fehlt auf {w.CurrentView}");
    static List<Button> VisibleGold(MainWindow w) => In<Button>(w).Where(b => b.IsEffectivelyVisible && MainWindow.IsGold(b)).ToList();
    /// <summary>Goldene Knöpfe der Seite selbst (ohne KI-Leiste).</summary>
    static List<Button> PageGold(MainWindow w) => VisibleGold(w).Where(b => !(b.Name ?? "").StartsWith("auftrag-", StringComparison.Ordinal)).ToList();

    // ---------- V-07 ----------

    [Fact(DisplayName = "V-07: Nur reine Einlese-Knöpfe laufen nach der Auswahl von selbst – Prüfen und Entpacken, Löschen, Senden, Installieren nie")]
    public void Read_step_rule()
    {
        foreach (var ok in new[] { "Einlesen", "Laden", "Öffnen", "Übernehmen", "Ordner aufnehmen", "Einlesen und prüfen", "Bild öffnen …", "Importieren" })
            Assert.True(MainWindow.IsReadStep(ok), ok);
        foreach (var no in new[] { "Prüfen und entpacken", "Prüfen", "Löschen", "Senden", "Installieren", "Hochladen und drucken …", "Zurücksetzen …", "Dateien einlesen und alles löschen", "" })
            Assert.False(MainWindow.IsReadStep(no), no);
    }

    [AvaloniaFact(DisplayName = "V-07: Adressbuch – in „Einfach“ ein Knopf „Datei wählen …“ statt Feld + zwei Knöpfe; nach der Auswahl ist die Datei sofort eingelesen")]
    public void File_pick_is_one_click()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; w.Settings.SetupDone = true; w.ProgramSetLevel(UserLevel.Einfach);
        try
        {
            var vcf = Path.Combine(root, "kontakte-probe.vcf");
            File.WriteAllText(vcf, "BEGIN:VCARD\r\nVERSION:3.0\r\nFN:Erika Muster\r\nEMAIL:erika@example.org\r\nEND:VCARD\r\n");
            w.PickOverride = _ => Task.FromResult<string[]?>([vcf]);
            Go(w, "kontakte");
            var field = Need<TextBox>(w, "ab-vcf-file");
            Assert.False(field.IsVisible); // Pfad als Text nur in „Professionell“
            Assert.False(Need<Button>(w, "ab-vcf-import").IsVisible); // „Einlesen“ läuft von selbst
            var pick = Need<Button>(w, "ab-vcf-file-pick");
            Assert.True(pick.IsEffectivelyVisible); Assert.Equal("Datei wählen …", pick.Content);
            Assert.True(Need<TextBlock>(w, "ab-vcf-file-what").IsEffectivelyVisible); // sagt, welche Datei gemeint ist – in Alltagswörtern
            Assert.DoesNotContain("vCard", Need<TextBlock>(w, "ab-vcf-file-what").Text);
            var before = w.Mail.Search("").Count;
            pick.Press(); Settle();
            Assert.Equal(before + 1, w.Mail.Search("").Count);
            Assert.Contains(w.Mail.Search(""), c => c.Name == "Erika Muster");

            // „Professionell“: Feld und „Einlesen“ sichtbar, nichts läuft von selbst
            w.ProgramSetLevel(UserLevel.Profi); Go(w, "kontakte");
            Assert.True(Need<TextBox>(w, "ab-vcf-file").IsEffectivelyVisible);
            Assert.True(Need<Button>(w, "ab-vcf-import").IsEffectivelyVisible);
            var n = w.Mail.Search("").Count;
            File.WriteAllText(vcf, "BEGIN:VCARD\r\nVERSION:3.0\r\nFN:Max Probe\r\nEND:VCARD\r\n");
            Need<Button>(w, "ab-vcf-file-pick").Press(); Settle();
            Assert.Equal(n, w.Mail.Search("").Count);
            Need<Button>(w, "ab-vcf-import").Press(); Settle();
            Assert.Equal(n + 1, w.Mail.Search("").Count);
        }
        finally { w.Close(); }
    }

    // ---------- V-09 ----------

    [AvaloniaFact(DisplayName = "V-09: Bilder mit KI – „Bild-KI fehlt – Jetzt einrichten“ steht direkt neben „Bilder malen“ und ist der eine goldene Knopf")]
    public void Blocker_sits_at_the_main_action()
    {
        var w = Open();
        var old = MainWindow.ImageAiOverride;
        try
        {
            MainWindow.ImageAiOverride = null;
            Doc(w, "bild-ki-programm");
            var chip = Need<Border>(w, "prog-blocker");
            Assert.True(chip.IsEffectivelyVisible);
            var main = Need<Button>(w, "prog-cmd-img-generate");
            Assert.Same(main.Parent, chip.Parent); // gleiche Gruppe im Menüband
            var gold = VisibleGold(w).Where(b => b.Name != "auftrag-pruefen").ToList();
            Assert.Single(gold); Assert.Equal("prog-blocker-setup", gold[0].Name);
            Assert.False(MainWindow.IsGold(main));
            Need<Button>(w, "prog-blocker-setup").Press(); Settle();
            Assert.Equal("ki-bibliothek", w.CurrentView);

            // eingerichtet: kein Hinweis, „Bilder malen“ ist wieder die Hauptaktion
            MainWindow.ImageAiOverride = (_, _, _, _, _) => Task.FromResult(false);
            Doc(w, "bild-ki-programm");
            Assert.Null(Named<Border>(w, "prog-blocker"));
            Assert.True(MainWindow.IsGold(Need<Button>(w, "prog-cmd-img-generate")));
        }
        finally { MainWindow.ImageAiOverride = old; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V-09: 3D – ohne lokale KI steht „Jetzt einrichten“ neben „Teile vorschlagen“ und öffnet die Zusatzprogramme")]
    public void Blocker_in_3d()
    {
        var w = Open();
        try
        {
            Go(w, "3d");
            if (Named<Border>(w, "3d-ai-blocker") is not { } chip) return; // lokale KI läuft auf diesem PC: dann gibt es nichts einzurichten
            var ai = Need<Button>(w, "3d-ai");
            Assert.Same(ai.Parent, chip.Parent);
            Need<Button>(w, "3d-ai-blocker-setup").Press(); Settle();
            Assert.Equal("components", w.CurrentView);
        }
        finally { w.Close(); }
    }

    // ---------- V-11 ----------

    [Fact(DisplayName = "V-11: Alltagswörter für feste Texte und Tastenkürzel im Hinweis statt im Knopf")]
    public void Plain_words_rules()
    {
        Assert.Equal("Tour-Datei laden", PlainWords.SimpleText("GPX laden"));
        Assert.Equal("✓ Sensoren anschließen", PlainWords.SimpleText("✓ GPIO / Sensoren"));
        Assert.Null(PlainWords.SimpleText("Mein GPX laden")); // nur ganze Texte
        Assert.Equal(("Teilen", "S"), PlainWords.WithoutShortcut("Teilen (S)"));
        Assert.Equal(("Abspielen", "Leertaste"), PlainWords.WithoutShortcut("Abspielen (Leertaste)"));
        Assert.Equal(("Speichern", "Strg+S"), PlainWords.WithoutShortcut("Speichern (Strg+S)"));
        Assert.Null(PlainWords.WithoutShortcut("Etiketten (PDF)"));
        Assert.Equal("Kalenderdatei einlesen …", PlainWords.Simple("ICS einlesen …"));
        Assert.True(PlainWords.SimpleLabels.Count + PlainWords.SimpleTexts.Count >= 50); // V7 + V-11: rund 40 neue Stellen
    }

    [AvaloniaFact(DisplayName = "V-11: Betriebsmodus und Adressbuch zeigen in „Einfach“ keine Fachwörter (winget, Get-PnpDevice, GPIO, vCard); „Erweitert“ behält sie")]
    public void No_jargon_in_simple()
    {
        var w = Open();
        try
        {
            string All() => string.Join(" | ", In<TextBlock>(w).Where(t => t.IsEffectivelyVisible).Select(t => t.Text)) + " | " + string.Join(" | ", In<Button>(w).Where(b => b.IsEffectivelyVisible).Select(SimpleWords.Label));
            Go(w, "modus");
            var simple = All();
            foreach (var jargon in new[] { "winget", "Get-PnpDevice", "GPIO" }) Assert.DoesNotContain(jargon, simple);
            Go(w, "kontakte");
            Assert.DoesNotContain("vCard", All());
            Assert.Contains("Fachwort", ToolTip.GetTip(Need<Button>(w, "ab-vcf-export")) as string ?? "");
            w.ProgramSetLevel(UserLevel.Erweitert); Go(w, "modus");
            Assert.Contains("winget", All());
        }
        finally { w.Close(); }
    }

    // ---------- V-12 ----------

    [AvaloniaFact(DisplayName = "V-12: Bücher schreiben – „Kapitelüberschrift“, „Fett“, „Kursiv“, „Aufzählung“ setzen die Zeichen selbst; Wissen: „Verweis auf Seite“")]
    public void Marks_by_button()
    {
        var w = Open();
        try
        {
            Doc(w, "buch-programm", "leer");
            var box = Need<TextBox>(w, "book-text");
            box.Text = "Es war einmal\nein Leuchtturm";
            box.SelectionStart = 0; box.SelectionEnd = 2;
            Need<Button>(w, "book-mark-bold").Press();
            Assert.Equal("**Es** war einmal\nein Leuchtturm", box.Text);
            box.SelectionStart = box.SelectionEnd = box.Text!.IndexOf("ein ", StringComparison.Ordinal) + 1;
            Need<Button>(w, "book-mark-heading").Press();
            Assert.Contains("\n# ein Leuchtturm", box.Text);
            foreach (var n in new[] { "book-mark-italic", "book-mark-list" }) Assert.True(Need<Button>(w, n).IsEffectivelyVisible);

            Go(w, "ki-wissen");
            var text = Need<TextBox>(w, "wissen-text");
            text.Text = "siehe Stromzähler"; text.SelectionStart = 6; text.SelectionEnd = 17;
            Need<Button>(w, "wissen-mark-link").Press();
            Assert.Equal("siehe [[Stromzähler]]", text.Text);
            Assert.DoesNotContain("[[", text.PlaceholderText);
        }
        finally { w.Close(); }
    }

    // ---------- V-14 ----------

    [AvaloniaFact(DisplayName = "V-14: Trendo Text, Geld und Briefe – Speichern · Rückgängig · Wiederholen · Exportieren stehen links oben in fester Reihenfolge")]
    public void Doc_actions_same_place()
    {
        var w = Open();
        try
        {
            foreach (var (page, names) in new[] { ("text", new[] { "text-save", "text-undo", "text-redo" }), ("finanzen", new[] { "geld-undo", "geld-redo" }), ("brief", new[] { "letter-save", "letter-undo", "letter-redo", "letter-export" }) /* T-0708: Briefe haben jetzt Rückgängig und Wiederholen */ })
            {
                Go(w, page);
                var group = Need<StackPanel>(w, MainWindow.DocActionsName);
                Assert.Equal(names, group.Children.Select(c => c.Name).ToArray());
                var at = group.TranslatePoint(new Point(0, 0), w.ActivePageRoot!) ?? throw new Xunit.Sdk.XunitException("nicht gezeichnet: " + page);
                var size = w.ActivePageRoot!.Bounds;
                Assert.True(at.X < size.Width * 0.4, $"{page}: Gruppe steht bei x={at.X:0} – nicht links");
                Assert.True(at.Y < size.Height * 0.4, $"{page}: Gruppe steht bei y={at.Y:0} – nicht oben");
                Assert.All(group.Children, c => Assert.True(c.IsEffectivelyVisible, $"{page}: {c.Name} unsichtbar"));
                // KI rechts oben (Leiste) und Hilfe im Kopf gibt es überall
                Assert.NotNull(Named<Button>(w, "ki-menu"));
            }
        }
        finally { w.Close(); }
    }

    // ---------- V-15 ----------

    [AvaloniaFact(DisplayName = "V-15: TV ohne Sender zeigt nur Reiter mit Inhalt; Verkaufen zeigt die Ideen-Zeile erst ab zwei Ideen")]
    public void Tabs_only_with_content()
    {
        var w = Open();
        try
        {
            Go(w, "tv");
            foreach (var empty in new[] { "live", "film", "serie", "guide", "multi" })
                Assert.False(Need<Button>(w, "tv-tab-" + empty).IsEffectivelyVisible, empty);
            foreach (var full in new[] { "mediathek", "videos", "quellen" })
                Assert.True(Need<Button>(w, "tv-tab-" + full).IsEffectivelyVisible, full);
            Assert.True(w.TvTabHasContent("live", 3, 0, 0)); Assert.False(w.TvTabHasContent("film", 3, 0, 0)); Assert.True(w.TvTabHasContent("serie", 0, 0, 2));
            Assert.DoesNotContain(In<TextBlock>(w), t => t.IsEffectivelyVisible && (t.Text ?? "").Contains("Xtream"));

            Go(w, "verkaufen");
            Assert.All(In<WrapPanel>(w).Where(p => p.Name is { } n && n.EndsWith("-ideas", StringComparison.Ordinal)), p => Assert.False(p.IsVisible));
        }
        finally { w.Close(); }
    }

    // ---------- V-17 ----------

    [AvaloniaFact(DisplayName = "V-17: Kennzahlen dunkel mit Gold, eine Flächenfarbe für Programmseiten, lesbare gewählte Kachel, keine gleichen Reiter-Symbole")]
    public void One_surface_calm_numbers()
    {
        var w = Open();
        try
        {
            Go(w, "finanzen");
            var tiles = In<Border>(w).Where(b => b.Classes.Contains("kennzahl")).ToList();
            Assert.NotEmpty(tiles);
            Assert.All(tiles, t => Assert.IsAssignableFrom<ISolidColorBrush>(t.Background)); // keine bunten Verläufe
            Assert.Single(tiles.Select(t => ((ISolidColorBrush)t.Background!).Color).Distinct());
            var gold = MainWindow.AccentColorNow;
            Assert.Contains(tiles, t => t.GetLogicalDescendants().OfType<TextBlock>().Any(x => x.Foreground is ISolidColorBrush { Color: var c } && c == gold));
            Assert.False(MainWindow.KpiWarn("#2FBF71")); Assert.True(MainWindow.KpiWarn("#F0644B"));

            var surfaces = new List<Color>();
            foreach (var (page, root) in new[] { ("text", "text-page-root"), ("tabelle", "tabelle-page-root"), ("praesentation", "praesentation-page-root"), ("pdf", "pdf-page-root") })
            { Go(w, page); surfaces.Add(((ISolidColorBrush)Need<Border>(w, root).Background!).Color); }
            Assert.Single(surfaces.Distinct());
            Assert.True(surfaces[0].B <= surfaces[0].R + 4, "Fläche ist bläulich");

            Go(w, "tabelle");
            Assert.NotNull(Named<Control>(w, "tile-icon-haushalt")); // einfarbiges Symbol statt Bildzeichen

            Go(w, "brief");
            var chosen = Named<Button>(w, "letter-gallery-frei");
            if (chosen is not null && MainWindow.IsGold(chosen))
                Assert.All(chosen.GetLogicalDescendants().OfType<TextBlock>(), t => Assert.True(t.Foreground is ISolidColorBrush { Color: var c } && c.R < 0x40 && c.G < 0x40, $"„{t.Text}“ nicht dunkel auf Gold"));

            Go(w, "prog-pflege");
            var icons = In<Control>(w).Where(c => c.Name is { } n && n.Contains("-tabicon-")).ToList();
            Assert.True(icons.Count == 0 || icons.Count == icons.Select(i => i.Name).Distinct().Count());
        }
        finally { w.Close(); }
    }

    // ---------- T-0686 offene Punkte ----------

    [AvaloniaFact(DisplayName = "T-0686-Rest: „Los“ der KI-Leiste ist neutral, sobald die Seite eine goldene Hauptaktion zeigt – sonst golden")]
    public void Prompt_go_yields_to_page_action()
    {
        var w = Open();
        try
        {
            foreach (var page in new[] { "text", "kontakte", "finanzen", "medien", "tv", "modus", "settings", "studios", "ki-wissen", "verkaufen" })
            {
                Go(w, page);
                if (Named<Button>(w, "auftrag-pruefen") is not { IsEffectivelyVisible: true } go) continue;
                var pageGold = VisibleGold(w).Where(b => !ReferenceEquals(b, go) && !(b.Name ?? "").StartsWith("auftrag-", StringComparison.Ordinal)).ToList();
                Assert.True(MainWindow.IsGold(go) == (pageGold.Count == 0), $"{page}: Los golden={MainWindow.IsGold(go)}, Seite golden: {string.Join(", ", pageGold.Select(b => b.Name))}");
            }
            Doc(w, "technik");
            Assert.False(MainWindow.IsGold(Need<Button>(w, "auftrag-pruefen")));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0686-Rest: Karten, Tragwerk, Messdaten, Print on Demand, Konsole und Bildbearbeitung (nach dem ersten Foto) haben genau eine goldene Hauptaktion")]
    public void Every_program_has_one_main_action()
    {
        foreach (var level in new[] { UserLevel.Einfach, UserLevel.Erweitert })
        {
            var w = Open(level);
            try
            {
                foreach (var (key, expected) in new[] { ("karten", "prog-export-gpx"), ("technik", "prog-export-pdf"), ("wissenschaft", "prog-export-report"), ("pod-programm", "prog-export-png"), ("konsole-programm", "lib-wish-go") })
                {
                    Doc(w, key);
                    var gold = PageGold(w);
                    Assert.True(gold.Count == 1, $"{key} ({level}): {gold.Count} goldene Knöpfe: {string.Join(", ", gold.Select(b => b.Name))}");
                    Assert.Equal(expected, gold[0].Name);
                }
                var img = Doc(w, "bild");
                Assert.Equal("prog-first-step", Assert.Single(PageGold(w)).Name); // leer: „Foto öffnen“
                img.Edit("Mal-Ebene", x => Trendo.Core.Media.ImageDoc.AddPaint(x)); Go(w, "bild");
                Assert.Equal("prog-export-png", Assert.Single(PageGold(w)).Name);
            }
            finally { w.Close(); }
        }
    }

    [AvaloniaFact(DisplayName = "T-0686-Rest: Bedienstile Windows und GNOME zeigen den Akzent in Gold statt Blau")]
    public void Os_styles_use_gold()
    {
        var (w0, _, _) = Ui.Open(); MainWindow? w = null;
        static MainWindow Switch(MainWindow w, string style) { w.Show("settings"); Settle(); w.Find<Button>("style-" + style).Press(); Settle(); var n = w.ReopenedAs ?? w; Settle(); return n; }
        try
        {
            foreach (var style in new[] { "windows", "linux", "mac" })
            {
                w = Switch(w ?? w0, style);
                Assert.Equal(style, MainWindow.StyleNow);
                var c = MainWindow.AccentColorNow;
                Assert.True(c.R > c.B + 60 && c.G > c.B, $"{style}: Akzent {c} ist nicht golden");
            }
        }
        finally { try { var t = Switch(w ?? w0, "trendo"); t.Close(); } catch (InvalidOperationException) { } }
    }

    // ---------- Dreiecke ----------

    [AvaloniaFact(DisplayName = "Ausrichtung in Trendo Text und Präsentation mit Ausrichtungs-Symbolen statt Dreiecken („▶ Rechtsbündig“)")]
    public void Align_buttons_have_align_icons()
    {
        var w = Open(UserLevel.Erweitert);
        try
        {
            Go(w, "text");
            foreach (var (name, kind) in new[] { ("text-align-left", "links"), ("text-align-center", "mitte"), ("text-align-right", "rechts"), ("text-align-justify", "block") })
            {
                var b = Need<Button>(w, name);
                Assert.Contains(b.GetLogicalDescendants().Prepend(b).OfType<Avalonia.Controls.Shapes.Path>(), p => p.Name == "align-icon-" + kind);
            }
            var labels = string.Join(" ", In<Button>(w).Select(SimpleWords.Label)) + string.Join(" ", In<TextBlock>(w).Select(t => t.Text));
            Assert.DoesNotContain("⯇", labels); Assert.DoesNotContain("⯈", labels);
            var src = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Trendo.App"), "*.cs");
            Assert.DoesNotContain(src, f => File.ReadAllText(f).Contains('⯇') || File.ReadAllText(f).Contains('⯈'));
        }
        finally { w.Close(); }
    }
}
