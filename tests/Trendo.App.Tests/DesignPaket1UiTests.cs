using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0684 Design-Paket 1 (Bild-Audit vom 16.11.2026, freigegeben): V-05 richtige Wörter an Knöpfen, V-06 „Mehr anzeigen“ versteckt nie den Hauptinhalt,
/// V-10 Fenstertitel = Name der Seite, V-13 eine KI-Eingabe und „Verwandte Programme“ im Menü „Mehr“, V-16 keine leeren Kästen,
/// V-18 Entwicklerseiten nur in „Professionell“, V-19 Wörter unter den Symbolen der Leiste.</summary>
public class DesignPaket1_Ui
{
    static MainWindow Open(int level = UserLevel.Einfach)
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        w.Settings.SetupDone = true;
        w.ProgramSetLevel(level);
        return w;
    }

    static void Go(MainWindow w, string key)
    {
        w.Show(key);
        if (w.Windows.Active is { Maximized: false } win) { w.Windows.ToggleMaximize(win.Id); w.Show(key); }
        W2Ui.Settle(); w.UpdateLayout(); W2Ui.Settle();
    }

    static IEnumerable<T> In<T>(MainWindow w) where T : Control => w.ActivePageRoot!.GetLogicalDescendants().OfType<T>();
    static T? Named<T>(MainWindow w, string name) where T : Control => In<T>(w).FirstOrDefault(c => c.Name == name);
    static string Word(Button b) => SimpleWords.Label(b);

    /// <summary>Inhalt eines Klappmenüs (liegt erst nach dem Öffnen im Baum).</summary>
    static IEnumerable<Control> Flyout(Button owner) => owner.Flyout is Flyout { Content: Control c } ? c.GetLogicalDescendants().OfType<Control>().Prepend(c) : [];

    // ---------- V-05 ----------

    [Fact(DisplayName = "V-05: mehrdeutige Zeichen werden nicht geraten – das Dreieck heißt nur in einer Abspiel-Leiste „Abspielen“")]
    public void Glyph_word_depends_on_context()
    {
        Assert.Equal("Abspielen", GlyphIcons.NameFor("▶", "au-tp-play"));
        Assert.Equal("Weiter", GlyphIcons.NameFor("▶", "photo-next"));
        Assert.Equal("", GlyphIcons.NameFor("▶", "irgendein-knopf")); // kein Wort ist besser als ein falsches
        Assert.Equal("", GlyphIcons.NameFor("▶", null));
        Assert.Equal("Zurück", GlyphIcons.NameFor("◀", "hb-prev"));
        Assert.Equal("", GlyphIcons.NameFor("◀", "irgendein-knopf"));
        Assert.Equal("Schließen", GlyphIcons.NameFor("✕", null)); // eindeutige Zeichen behalten ihr Wort
        Assert.Equal("Pause", GlyphIcons.NameFor("⏸", "cnc-sim-play"));
    }

    [AvaloniaFact(DisplayName = "V-05: Kalender und Geld – die Pfeile heißen „Nächster Monat“ / „Voriger Monat“, nicht „Abspielen“")]
    public void Calendar_and_money_arrows_say_where_they_go()
    {
        var w = Open();
        try
        {
            Go(w, "kalender");
            Assert.Contains("Nächster Monat", Word(Named<Button>(w, "cal-next")!));
            Assert.Contains("Voriger Monat", Word(Named<Button>(w, "cal-prev")!));
            Assert.DoesNotContain("Abspielen", Word(Named<Button>(w, "cal-next")!));
            Named<Button>(w, "cal-mode-woche")!.Press(); W2Ui.Settle();
            Assert.Contains("Nächste Woche", Word(Named<Button>(w, "cal-next")!));
            Named<Button>(w, "cal-mode-tag")!.Press(); W2Ui.Settle();
            Assert.Contains("Nächster Tag", Word(Named<Button>(w, "cal-next")!));
            Named<Button>(w, "cal-mode-monat")!.Press(); W2Ui.Settle();

            Go(w, "finanzen");
            Assert.Contains("Nächster Monat", Word(Named<Button>(w, "hb-next")!));
            Assert.Contains("Voriger Monat", Word(Named<Button>(w, "hb-prev")!));
            Assert.Equal("Nächster Monat", ToolTip.GetTip(Named<Button>(w, "hb-next")!));
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V-05: auf keiner Seite heißt ein Blätter-Pfeil „Abspielen“ (nur Knöpfe einer Abspiel-Leiste tragen das Wort)")]
    public void No_page_calls_an_arrow_play()
    {
        var w = Open();
        var wrong = new List<string>();
        try
        {
            foreach (var key in w.PageKeys.OrderBy(k => k).ToList())
            {
                foreach (var old in w.Windows.Stack.Reverse().Skip(3).ToList()) w.CloseWindow(old.Id);
                try { w.Show(key); } catch (Exception e) when (e is InvalidOperationException or ArgumentException or IOException or NotSupportedException) { continue; }
                Dispatcher.UIThread.RunJobs();
                if (w.ActivePageRoot is not { } root) continue;
                foreach (var b in root.GetLogicalDescendants().OfType<Button>())
                {
                    if (GlyphIcons.GetGlyph(b) is not ("▶" or "⏵")) continue;
                    var word = Word(b) + " " + (ToolTip.GetTip(b) as string);
                    var n = b.Name ?? "";
                    var player = n.Contains("play", StringComparison.OrdinalIgnoreCase) || n.Contains("show", StringComparison.OrdinalIgnoreCase) || n.Contains("listen", StringComparison.OrdinalIgnoreCase) || n.Contains("preview", StringComparison.OrdinalIgnoreCase);
                    if (word.Contains("Abspielen") && !player) wrong.Add($"{key}: {n} = „{word.Trim()}“");
                }
            }
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    [AvaloniaFact(DisplayName = "V-05: kein Knopf „Kopieren“ unter einem kurzen Hinweis – nur unter einem echten Ergebnis; Rechtsklick kopiert weiter")]
    public void No_copy_button_under_short_hints()
    {
        Assert.False(MainWindow.ResultWorthCopying("Vorlage „Lebenslauf“ geöffnet."));
        Assert.True(MainWindow.ResultWorthCopying("Zeile 1\nZeile 2"));
        Assert.True(MainWindow.ResultWorthCopying(new string('x', 200)));
        var w = Open();
        try
        {
            Go(w, "qual-leistung");
            Named<Button>(w, "audit-check")!.Press(); W2Ui.Settle(); // kurze Meldung „✓ Alle … Einträge unverändert.“
            var card = In<StackPanel>(w).Single(s => s.Name == "result-card");
            Assert.Null(Named<Button>(w, "result-copy"));
            var msg = card.GetLogicalDescendants().OfType<Border>().Single(b => b.Name == "w45-msg");
            Assert.Contains(msg.ContextMenu!.Items.OfType<MenuItem>(), m => (string?)m.Header == "Kopieren");
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    // ---------- V-06 ----------

    [Fact(DisplayName = "V-06: „Mehr anzeigen“ nennt, was kommt; ohne Abschnittsnamen bleibt die Zahl")]
    public void More_label_names_what_follows()
    {
        Control Section(string title) => new Border { Child = new StackPanel { Children = { new TextBlock { Text = title, FontWeight = FontWeight.SemiBold }, new Button { Content = "Öffnen" } } } };
        Assert.Equal("Mehr anzeigen: Zuletzt benutzt", MainWindow.MoreLabel([Section("Zuletzt benutzt")], 3));
        Assert.Equal("Mehr anzeigen: Sichern, Etiketten und 1 weitere", MainWindow.MoreLabel([Section("Sichern"), Section("Etiketten (PDF)"), Section("Geburtstage")], 9));
        Assert.Equal("Mehr anzeigen: Im Setup enthalten", MainWindow.MoreLabel([new Expander { Header = "Im Setup enthalten – 10 Programme" }], 10));
        Assert.Equal("Mehr anzeigen (4 weitere Funktionen)", MainWindow.MoreLabel([new WrapPanel { Children = { new Button { Content = "A" } } }], 4));
    }

    [AvaloniaFact(DisplayName = "V-06: Texte & Tabellen zeigt unter „Vorlagen“ die Vorlagen; die KI-Bibliothek zeigt unter der gewählten Art die Modelle")]
    public void Main_content_stays_visible()
    {
        var w = Open();
        try
        {
            Go(w, "doku-start");
            var head = In<TextBlock>(w).First(t => t.Text == "Vorlagen" && t.IsEffectivelyVisible);
            var parent = (Panel)head.GetLogicalParent()!;
            var next = parent.Children[parent.Children.IndexOf(head) + 1];
            Assert.True(next.IsEffectivelyVisible && next.GetLogicalDescendants().OfType<Button>().Any(b => b.IsEffectivelyVisible), "unter „Vorlagen“ steht nichts");
            if (Named<Button>(w, "page-more") is { } more) { Assert.StartsWith("Mehr anzeigen: ", (string)more.Content!); Assert.DoesNotContain("weitere Funktionen", (string)more.Content!); }

            Go(w, "ki-bibliothek");
            Assert.Contains(In<Button>(w), b => b.Name?.StartsWith("kilib-get-", StringComparison.Ordinal) == true && b.IsEffectivelyVisible);
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V-06: lange Liste nennt im Knopf, was sie zeigt; die Einleitung einer Seite wird nie gekürzt")]
    public void List_button_names_its_list_and_intro_is_whole()
    {
        var list = new WrapPanel(); for (var i = 0; i < 15; i++) list.Children.Add(new Button { Content = "Vorlage Nummer " + i });
        var page = new StackPanel { Children = { new TextBlock { Text = "Vorlagen", FontWeight = FontWeight.SemiBold }, new TextBlock { Text = "Such dir eine aus." }, list } };
        Assert.Equal("Vorlagen", MainWindow.ListHeading(list.Children[0]));
        var other = new StackPanel { Children = { new TextBlock { Text = "Vorlagen", FontWeight = FontWeight.SemiBold }, new Button { Content = "etwas anderes dazwischen" }, new WrapPanel { Children = { new Button { Content = "x" } } } } };
        Assert.Null(MainWindow.ListHeading(((WrapPanel)other.Children[2]).Children[0]));
        Assert.NotNull(page);

        var w = Open();
        try
        {
            Go(w, "lizenz");
            var all = In<Button>(w).First(b => b.Name?.StartsWith("einfach-alle", StringComparison.Ordinal) == true);
            Assert.Matches(@"^Studios auf diesem PC: alle \d+ zeigen$", (string)all.Content!);

            Go(w, "components");
            var intro = In<TextBlock>(w).First(t => t.Name != "page-sentence" && (t.Text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 25);
            Assert.NotEqual(1, intro.MaxLines); // früher mit „…“ abgeschnitten
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    // ---------- V-10 ----------

    [AvaloniaTheory(DisplayName = "V-10: Fenstertitel und Seitenkopf nennen die Seite, die man sieht")]
    [InlineData("lizenz", "Konto, Lizenz & Vollversion")]
    [InlineData("hardware", "Hardware & Leistung")]
    [InlineData("verbindungen", "Verbindungen")]
    [InlineData("claude-verbinden", "Mit Claude verbinden")]
    [InlineData("ki-bibliothek", "KI-Bibliothek")]
    [InlineData("ki-internet", "Kostenlose Internet-KIs")]
    [InlineData("system", "Protokoll & Abstürze")]
    [InlineData("ton", "Audio bearbeiten")]
    [InlineData("bild", "Bildbearbeitung")]
    [InlineData("schnitt", "Videoschnitt")]
    public void Window_title_is_the_page(string key, string name)
    {
        var w = Open();
        try
        {
            Go(w, key);
            Assert.Equal(name, w.PageTitle(key));
            Assert.Equal(name, w.Windows.Active!.Title);
            Assert.Equal(name, In<TextBlock>(w).Single(t => t.Name == "page-title").Text);
            // die Titelkarte eines fremden Studios (Symbol, Name, Satz) steht nicht mehr darüber
            if (w.ModuleOf(key) is { } sid && StudioCatalog.For(sid) is { } info && info.Name != name) Assert.DoesNotContain(In<TextBlock>(w), t => t.Name == "page-sentence");
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V-10: Seiten der Einstellungen und der KI-Zentrale heißen im Fenstertitel wie in der linken Leiste; die Startseite eines Studios heißt wie das Studio")]
    public void Shell_pages_are_named_like_the_left_bar()
    {
        var w = Open(UserLevel.Profi);
        try
        {
            var own = 0;
            foreach (var (key, label) in MainWindow.SettingsGroups.Concat(MainWindow.KiGroups).SelectMany(g => g.Pages))
            {
                if (!w.PageKeys.Contains(key) || key is "settings" or "ki") continue; // Einstiegsseiten der Apps: Name der App
                var studio = w.ModuleOf(key) is { } m && StudioCatalog.For(m) is { } info && w.PageForStudio(m) == key ? info.Name : null;
                Assert.Equal(studio ?? label, w.PageTitle(key)); // nie der Name eines Studios, dessen Startseite eine andere Seite ist
                if (studio is null) own++;
            }
            Assert.True(own >= 15, $"nur {own} Seiten mit eigenem Namen");
            Assert.Equal("Einstellungen", w.PageTitle("settings")); // die Einstiegsseite der App behält deren Namen
            Assert.Equal("KI-Zentrale", w.PageTitle("ki")); // wie der Eintrag der linken Leiste des Desktops (T-0663)
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    // ---------- V-13 ----------

    [AvaloniaTheory(DisplayName = "V-13: Seiten mit eigenem KI-Gespräch haben nur eine Eingabe; die Auftragszeile bleibt über „KI ▾“ erreichbar")]
    [InlineData("ki-fragen")]
    [InlineData("ki")]
    [InlineData("dev")]
    public void Ki_pages_have_one_input(string key)
    {
        var w = Open();
        try
        {
            Go(w, key);
            Assert.Null(Named<TextBox>(w, "auftrag-prompt"));
            var show = Named<Button>(w, "auftrag-zeile-zeigen"); // Eintrag im Menü „KI ▾“
            Assert.NotNull(show);
            Assert.NotNull(Named<Button>(w, "ki-menu"));
            show!.Press(); W2Ui.Settle();
            Assert.NotNull(Named<TextBox>(w, "auftrag-prompt"));

            Go(w, "kalender"); // andere Seiten behalten die Auftragszeile
            Assert.NotNull(Named<TextBox>(w, "auftrag-prompt"));
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    [AvaloniaTheory(DisplayName = "V-13: „Gehört hierher:“ ist weg – die verwandten Programme stehen im Menü „Mehr“ und öffnen ihre Seite")]
    [InlineData(UserLevel.Einfach)]
    [InlineData(UserLevel.Erweitert)]
    public void Related_programs_live_in_the_more_menu(int level)
    {
        var w = Open(level);
        try
        {
            Go(w, "settings");
            Assert.DoesNotContain(w.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Gehört hierher:");
            Assert.Null(w.GetLogicalDescendants().OfType<DockPanel>().FirstOrDefault(d => d.Name == "fold-strip"));
            var more = Named<Button>(w, "page-head-more")!;
            Assert.Equal("Mehr", more.Content);
            var menu = Flyout(more).ToList();
            Assert.Contains(menu.OfType<TextBlock>(), t => t.Text == "Verwandte Programme");
            var entry = menu.OfType<Button>().First(b => b.Name == "fold-open-SYS-13");
            entry.Press(); W2Ui.Settle();
            Assert.Equal(w.PageForStudio("SYS-13"), w.CurrentView);
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V-13: Seite ohne Seitenkopf (Fokusmodus) – die verwandten Programme stehen unter einem Knopf „Verwandte Programme ▾“")]
    public void Related_programs_without_page_head()
    {
        var w = Open(UserLevel.Erweitert);
        try
        {
            w.ToggleFocusMode(); // im Fokusmodus baut die Seite keinen Kopf
            Go(w, "settings");
            Assert.Null(Named<Button>(w, "page-head-more"));
            Assert.DoesNotContain(w.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Gehört hierher:");
            var open = w.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "fold-menu");
            Assert.Equal("Verwandte Programme ▾", open.Content);
            var entry = Flyout(open).OfType<Button>().First(b => b.Name == "fold-open-SYS-13");
            entry.Press(); W2Ui.Settle();
            Assert.Equal(w.PageForStudio("SYS-13"), w.CurrentView);
        }
        finally { if (w.FocusMode) w.ToggleFocusMode(); w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    // ---------- V-16 ----------

    [AvaloniaFact(DisplayName = "V-16: Aufgaben ohne Aufgabe zeigen einen Leerzustand, nicht zusätzlich drei leere Spalten; mit Aufgabe steht die Tafel da")]
    public void Tasks_one_empty_state()
    {
        var w = Open();
        try
        {
            Go(w, "aufgaben");
            Assert.NotNull(Named<Border>(w, "todo-empty"));
            Assert.Null(Named<Grid>(w, "todo-board"));
            w.Cal.AddTodo(new Trendo.Core.Sys.TodoItem("t1", "Blumen gießen"));
            Go(w, "aufgaben");
            Assert.Null(Named<Border>(w, "todo-empty"));
            Assert.NotNull(Named<Grid>(w, "todo-board"));
            Assert.NotNull(Named<Border>(w, "todo-col-2"));
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V-16: Projekte und Großprojekte zeichnen rechts keinen leeren Kasten")]
    public void No_empty_boxes()
    {
        var w = Open();
        try
        {
            Go(w, "projects");
            Assert.Null(Named<Border>(w, "proj-right"));
            Assert.NotNull(Named<Border>(w, "proj-center"));
            Go(w, "produktion");
            Assert.Null(Named<StackPanel>(w, "big-details"));
            var big = Named<Grid>(w, "big-window")!;
            Assert.Equal(2, big.Children.Count);
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V-16: Adressbuch ohne Kontakte – ein Leerzustand mit Knopf, die Spalten reichen bis zum unteren Fensterrand")]
    public void Address_book_fills_the_window()
    {
        var w = Open();
        try
        {
            Go(w, "kontakte");
            var book = Named<Grid>(w, "ab-book")!;
            Assert.NotNull(Named<Border>(w, "ab-empty"));
            Assert.DoesNotContain(In<TextBlock>(w), t => t.Text == "Links einen Kontakt anklicken.");
            Assert.DoesNotContain(In<TextBlock>(w), t => (t.Text ?? "").StartsWith("Noch keine Kontakte. Oben", StringComparison.Ordinal));
            var sv = book.FindAncestorOfType<ScrollViewer>()!;
            var bottom = book.TranslatePoint(new Avalonia.Point(0, book.Bounds.Height), sv)!.Value.Y;
            Assert.True(book.Bounds.Height >= 320, $"Adressbuch nur {book.Bounds.Height} hoch");
            Assert.True(sv.Viewport.Height - bottom < 40, $"unter dem Adressbuch bleiben {sv.Viewport.Height - bottom:0} Pixel leer");
            Named<Button>(w, "ab-empty-new")!.Press(); W2Ui.Settle();
            Assert.Null(Named<Border>(w, "ab-empty")); // der neue Kontakt steht zum Ausfüllen da
            Assert.Single(w.Mail.Contacts());
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    // ---------- V-18 ----------

    [AvaloniaFact(DisplayName = "V-18: Bau-Fortschritt und rohe Protokollzeilen nur in „Professionell“")]
    public void Developer_pages_only_in_pro()
    {
        var w = Open();
        try
        {
            foreach (var level in new[] { UserLevel.Einfach, UserLevel.Erweitert })
            {
                w.ProgramSetLevel(level);
                Go(w, "system");
                if (Named<Button>(w, "settings-nav-more") is { } all && (string)all.Content! != "Weniger anzeigen") { all.Press(); W2Ui.Settle(); }
                Assert.Null(Named<Button>(w, "settings-nav-fortschritt"));
                Assert.Null(Named<StackPanel>(w, "system-rawlog"));
                Assert.NotNull(Named<TextBlock>(w, "system-rawlog-hint"));
                Assert.DoesNotContain(In<Button>(w), b => (b.Content as string) == "Bau-Fortschritt");
                Assert.DoesNotContain(In<TextBlock>(w), t => (t.Text ?? "").Contains("core.start")); // Rohzeile des Protokolls
            }
            w.ProgramSetLevel(UserLevel.Profi);
            Go(w, "system");
            Assert.NotNull(Named<Button>(w, "settings-nav-fortschritt"));
            Assert.NotNull(Named<StackPanel>(w, "system-rawlog"));
            Assert.Contains(In<Button>(w), b => (b.Content as string) == "Bau-Fortschritt");
            Go(w, "fortschritt"); // die Seite selbst gibt es weiter
            Assert.Equal("fortschritt", w.CurrentView);
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V-18: Systemcheck – ein Satz mit einem Knopf, die Liste in „Einfach“ zugeklappt unter „Einzelheiten“, Pfade gekürzt")]
    public void System_check_is_one_sentence()
    {
        Assert.Equal("6 Zusatzprogramme fehlen.", MainWindow.SystemCheckSentence(6, 8, 3, 11));
        Assert.Equal("1 Zusatzprogramm fehlt.", MainWindow.SystemCheckSentence(1, 1, 10, 11));
        Assert.Equal("Fast alles in Ordnung – 1 Punkt braucht deine Aufmerksamkeit.", MainWindow.SystemCheckSentence(0, 1, 10, 11));
        Assert.Equal("Alles in Ordnung: 11 von 11 Punkten bereit.", MainWindow.SystemCheckSentence(0, 0, 11, 11));
        var sep = Path.DirectorySeparatorChar;
        Assert.Equal($"…{sep}Trendo{sep}daten", MainWindow.ShortPath(@"C:\Ordner\Unterordner\Trendo\daten"));
        Assert.Equal("", MainWindow.ShortPath(null));

        var w = Open();
        try
        {
            Go(w, "systemcheck");
            for (var i = 0; i < 400 && Named<TextBlock>(w, "syscheck-summary") is null; i++) { Thread.Sleep(25); Dispatcher.UIThread.RunJobs(); }
            var sum = Named<TextBlock>(w, "syscheck-summary");
            Assert.NotNull(sum);
            var details = Named<Expander>(w, "syscheck-details")!;
            Assert.False(details.IsExpanded);
            Assert.StartsWith("Einzelheiten (", (string)details.Header!);
            Assert.NotNull(details.GetLogicalDescendants().OfType<Grid>().FirstOrDefault(g => g.Name == "syscheck")); // die Liste bleibt, einen Klick entfernt
            if (sum!.Text!.Contains("fehl")) Assert.Equal("Jetzt einrichten", Named<Button>(w, "syscheck-setup")!.Content);
            Assert.DoesNotContain(details.GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? "").Contains(Path.GetTempPath().TrimEnd(sep))); // kein voller Pfad
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "V-18: Handy verbinden – der Teil für den Entwicklermodus steht in „Einfach“ zugeklappt unter dem geführten Weg; „Erweitert“ zeigt ihn offen")]
    public void Phone_developer_part_is_folded()
    {
        var w = Open();
        try
        {
            Go(w, "handy");
            var fold = Named<Expander>(w, "phone-pro")!;
            Assert.False(fold.IsExpanded);
            Assert.Contains("Entwicklermodus", (string)fold.Header!);
            Assert.NotNull(fold.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "phone-scan")); // bleibt einen Klick entfernt
            w.ProgramSetLevel(UserLevel.Erweitert);
            Go(w, "handy");
            Assert.Null(Named<Expander>(w, "phone-pro"));
            Assert.True(Named<Button>(w, "phone-scan")!.IsEffectivelyVisible);
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }

    // ---------- V-19 ----------

    [AvaloniaFact(DisplayName = "V-19: jedes Symbol der Leiste am unteren Rand trägt ein Wort")]
    public void Dock_icons_have_words()
    {
        var w = Open();
        try
        {
            w.Show("start"); W2Ui.Settle(); w.UpdateLayout(); W2Ui.Settle();
            var bar = w.GetLogicalDescendants().OfType<Border>().First(b => b.Name == "dock-bar");
            var buttons = bar.GetLogicalDescendants().OfType<Button>().ToList();
            Assert.True(buttons.Count >= 8, $"nur {buttons.Count} Einträge in der Leiste");
            foreach (var b in buttons)
            {
                var word = b.GetLogicalDescendants().OfType<TextBlock>().SingleOrDefault(t => t.Name == b.Name + "-word");
                Assert.True(word is not null && UebersichtEinfach_Ui.HasWord(word.Text ?? ""), b.Name + " ohne Wort");
                if (b.IsEffectivelyVisible) Assert.True(word!.IsEffectivelyVisible && word.Bounds.Width > 0 && word.Bounds.Width <= MainWindow.DockItemWidth, b.Name + ": Wort nicht sichtbar oder zu breit");
            }
            string W(string name) => bar.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Name == name).Text!;
            Assert.Equal("Einstellungen", W("dock-settings-word"));
            Assert.Equal("Dateien", W("dock-files-word"));
            Assert.Equal("Papierkorb", W("dock-trash-word"));
            Assert.Equal("Start", W("start-button-word"));
            Assert.Equal("Hinzufügen – Programme, KI, GitHub", ToolTip.GetTip(bar.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "dock-components"))); // der ausführliche Name bleibt im Hinweis
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
    }
}
