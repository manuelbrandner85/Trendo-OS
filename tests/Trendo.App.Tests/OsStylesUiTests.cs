using Avalonia;
using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core.Shell;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Wunsch 04.11.2026: Bedienstil wie Windows 11, macOS oder GNOME – Desktop, Leisten, Startmenü/Launchpad/Übersicht, Widgets, Fenster,
/// Dateimanager und Verhalten wie im Original; keine Trendo-Funktion geht verloren; „Trendo“ bleibt unverändert.</summary>
public class OsStyles_Ui
{
    static void Pump() { for (var i = 0; i < 6; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
    static MainWindow Switch(MainWindow w, string style) { w.Show("settings"); Pump(); w.Find<Button>("style-" + style).Press(); Pump(); var n = w.ReopenedAs ?? w; Pump(); return n; }
    static bool Visible(MainWindow w, string name) => w.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name && c.IsEffectivelyVisible);
    static void Back(MainWindow w) { try { var t = Switch(w, "trendo"); Assert.Equal("trendo", MainWindow.StyleNow); t.Close(); } catch (InvalidOperationException) { } }

    [AvaloniaFact(DisplayName = "Wie Windows 11: Taskleiste mit Start/Suche/Uhr, Startmenü mit Angeheftet und Suche (Trendo KI), Widgets mit allen Trendo-Karten, eckige Fensterknöpfe, Datei-Explorer mit „Dieser PC“")]
    public void Windows11()
    {
        var (w0, _, _) = Ui.Open(); MainWindow? w = null;
        try
        {
            w = Switch(w0, "windows"); Assert.Equal("windows", MainWindow.StyleNow);
            Assert.True(Visible(w, "os-taskbar")); Assert.False(Visible(w, "left-column")); Assert.False(Visible(w, "top-bar"));
            Assert.True(Visible(w, "os-desk-pc")); Assert.True(Visible(w, "os-clock"));
            w.Find<Button>("os-toggle-start").Press(); Pump();
            Assert.True(w.OsLauncherOpen); Assert.Equal("start", w.OsLauncherMode);
            Assert.True(Visible(w, "os-nav-settings")); Assert.True(Visible(w, "os-ki")); Assert.True(Visible(w, "os-power"));
            var search = w.Find<TextBox>("os-search"); search.Text = "Einstell"; Pump();
            Assert.True(Visible(w, "os-hit-ki")); Assert.True(Visible(w, "os-nav-settings-hit"));
            w.Find<Button>("os-nav-settings-hit").Press(); Pump();
            Assert.False(w.OsLauncherOpen); Assert.Equal("settings", w.CurrentView);
            // Widgets: rechte Spalte und Desktop-Schnellaktionen sind alle noch da
            w.Find<Button>("os-toggle-widgets").Press(); Pump();
            Assert.True(w.OsWidgetsOpen); Assert.True(Visible(w, "right-column")); Assert.True(Visible(w, "os-desk-extras"));
            w.ToggleOsWidgets(); Pump();
            // Fenster: Windows-Knöpfe 46 × 32, Alt+F4 schließt
            w.Show("brief"); Pump();
            var id = w.Windows.Active!.Id;
            var caps = w.Find<StackPanel>("win-caps-" + id);
            Assert.Equal(46, ((Button)caps.Children[2]).Width); Assert.StartsWith("win-close", caps.Children[2].Name);
            Assert.Contains(w.GetVisualDescendants().OfType<Button>(), b => b.Name == "os-task-" + id);
            Assert.True(w.OsWindowKey(Key.F4, KeyModifiers.Alt)); Pump();
            Assert.Null(w.Windows.Get(id));
            // Datei-Explorer
            w.FilesGo(MainWindow.PcPlace); Pump();
            Assert.Equal("Datei-Explorer", MainWindow.FilesTitle);
            Assert.True(Visible(w, "fb-drives")); Assert.True(Visible(w, "fb-side-pc")); Assert.True(Visible(w, "fb-commands"));
        }
        finally { Back(w ?? w0); }
    }

    [AvaloniaFact(DisplayName = "Wie macOS: Menüleiste mit Menüs und Uhr, Dock mit Launchpad und Papierkorb, Spotlight, Ampel-Knöpfe, ⌘W schließt, Finder mit Seitenleiste und Symbolansicht")]
    public void MacOs()
    {
        var (w0, _, _) = Ui.Open(); MainWindow? w = null;
        try
        {
            w = Switch(w0, "mac");
            Assert.True(Visible(w, "os-menubar")); Assert.True(Visible(w, "os-dock")); Assert.True(Visible(w, "os-menu-ablage")); Assert.False(Visible(w, "left-column"));
            Assert.True(Visible(w, "os-trash"));
            w.Find<Button>("os-toggle-start").Press(); Pump();
            Assert.True(w.OsLauncherOpen); Assert.True(Visible(w, "os-grid"));
            w.CloseOsLauncher();
            Assert.True(w.OsWindowKey(Key.Space, KeyModifiers.Control)); Pump();
            Assert.Equal("spotlight", w.OsLauncherMode);
            w.Find<TextBox>("os-search").Text = "Projekte"; Pump();
            Assert.True(Visible(w, "os-nav-projects-hit"));
            w.CloseOsLauncher();
            w.Show("brief"); Pump();
            var id = w.Windows.Active!.Id;
            var caps = w.Find<StackPanel>("win-caps-" + id);
            Assert.StartsWith("win-close", caps.Children[0].Name); Assert.Equal(14, ((Button)caps.Children[0]).Width);
            Assert.True(w.OsWindowKey(Key.W, KeyModifiers.Control)); Pump(); // ⌘W (PC-Tastatur: Strg)
            Assert.Null(w.Windows.Get(id));
            w.FilesGo(w.Files.Home); Pump();
            Assert.Equal("Finder", MainWindow.FilesTitle); Assert.True(Visible(w, "fb-sidebar"));
            Assert.True(Visible(w, "fb-empty") || Visible(w, "fb-icons"));
        }
        finally { Back(w ?? w0); }
    }

    [AvaloniaFact(DisplayName = "Wie GNOME: schwarze Leiste mit Aktivitäten und Uhr in der Mitte, Übersicht mit Suche und Dash, nur Schließen-Knopf im Fenster, Dateien mit „Andere Orte“")]
    public void Gnome()
    {
        var (w0, _, _) = Ui.Open(); MainWindow? w = null;
        try
        {
            w = Switch(w0, "linux");
            Assert.True(Visible(w, "os-topbar")); Assert.False(Visible(w, "os-dock")); Assert.False(Visible(w, "left-column"));
            w.Find<Button>("os-toggle-activities").Press(); Pump();
            Assert.True(w.OsLauncherOpen); Assert.True(Visible(w, "os-dash")); Assert.True(Visible(w, "os-grid"));
            w.CloseOsLauncher();
            w.Find<Button>("os-toggle-clock").Press(); Pump(); Assert.True(w.OsWidgetsOpen); w.ToggleOsWidgets();
            w.Show("brief"); Pump();
            var id = w.Windows.Active!.Id;
            var caps = w.Find<StackPanel>("win-caps-" + id);
            Assert.False(caps.Children[0].IsVisible); Assert.False(caps.Children[1].IsVisible); Assert.True(caps.Children[2].IsVisible);
            Assert.True(w.OsWindowKey(Key.Up, KeyModifiers.Meta)); Pump(); Assert.True(w.Windows.Get(id)!.Maximized);
            w.FilesGo(MainWindow.PcPlace); Pump();
            Assert.Equal("Dateien", MainWindow.FilesTitle); Assert.True(Visible(w, "fb-side-pc"));
        }
        finally { Back(w ?? w0); }
    }

    [AvaloniaFact(DisplayName = "Dateimanager-Verhalten je System: Ziehen verschiebt (Strg kopiert), Kopiename je System, Finder-Zeilenschalter benennt um, Entf/⌘⌫ legt in den Papierkorb, Leertaste zeigt Vorschau")]
    public void Files_behaviour()
    {
        var (w0, _, _) = Ui.Open(); MainWindow? w = null;
        try
        {
            w = Switch(w0, "windows");
            w.Show("dateien"); Pump(); Assert.True(Visible(w, "fb-root")); // Seite „Dateien“ öffnet als eigenes Fenster
            var home = w.Files.Home; var docs = w.Files.KnownFolders().First(k => k.Id == "dokumente").Path;
            var f = Path.Combine(home, "Brief.txt"); File.WriteAllText(f, "Hallo");
            w.FilesGo(home); Pump();
            Assert.True(Visible(w, "fb-item-Brief.txt"));
            var moved = w.FilesDrop(f, docs, OsBehavior.Mods.None)!; Assert.False(File.Exists(f)); Assert.True(File.Exists(moved)); // gleiches Laufwerk: verschieben
            var copied = w.FilesDrop(moved, docs, OsBehavior.Mods.Ctrl)!; Assert.Equal("Brief - Kopie.txt", Path.GetFileName(copied)); // Strg: kopieren
            w.FilesGo(docs); Pump();
            w.FilesSelected = copied;
            Assert.True(w.FilesKey(Key.Delete, KeyModifiers.None)); Pump();
            Assert.False(File.Exists(copied)); Assert.Contains("Papierkorb", w.FilesMessage);
            var nf = w.FilesNewFolder(); Assert.Equal("Neuer Ordner", Path.GetFileName(nf));
            w.FilesRename("Rechnungen"); Assert.True(Directory.Exists(Path.Combine(docs, "Rechnungen")));
            // Mac: Zeilenschalter = umbenennen, ⌘⌫ = Papierkorb, Kopie „ Kopie“
            w = Switch(w, "mac");
            w.FilesGo(docs); Pump();
            w.FilesSelected = moved;
            Assert.Equal("mac", MainWindow.StyleNow); Assert.Equal("rename", OsBehavior.FileKey("mac", Key.Enter.ToString(), OsBehavior.Mods.None)); Assert.True(w.FilesKey(Key.Enter, KeyModifiers.None), "sel=" + w.FilesSelected); Pump();
            Assert.True(Visible(w, "fb-rename-box"));
            w.FilesRename("Brief alt.txt"); var renamed = Path.Combine(docs, "Brief alt.txt"); Assert.True(File.Exists(renamed));
            w.FilesSelected = renamed; w.FilesCopy(false); w.FilesPaste(); Assert.True(File.Exists(Path.Combine(docs, "Brief alt Kopie.txt")));
            w.FilesSelected = renamed; Assert.True(w.FilesKey(Key.Space, KeyModifiers.None)); Pump(); Assert.True(Visible(w, "fb-preview"));
            Assert.True(w.FilesKey(Key.Back, KeyModifiers.Control)); Assert.False(File.Exists(renamed));
            // GNOME: Kopie „(Kopie)“, neuer Ordner „Unbenannter Ordner“
            w = Switch(w, "linux");
            w.FilesGo(docs); Pump();
            w.FilesSelected = Path.Combine(docs, "Brief alt Kopie.txt"); w.FilesCopy(false); w.FilesPaste();
            Assert.True(File.Exists(Path.Combine(docs, "Brief alt Kopie (Kopie).txt")));
            Assert.Equal("Unbenannter Ordner", Path.GetFileName(w.FilesNewFolder()));
        }
        finally { Back(w ?? w0); }
    }

    [AvaloniaFact(DisplayName = "Stil Trendo bleibt unverändert: Trendo-Kopf, Seitenspalten, Dock – keine Fremd-Leisten")]
    public void Trendo_unchanged()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            Assert.Equal("trendo", MainWindow.StyleNow);
            Assert.True(Visible(w, "left-column")); Assert.True(Visible(w, "right-column")); Assert.True(Visible(w, "dock-bar")); Assert.True(Visible(w, "top-bar"));
            Assert.False(Visible(w, "os-taskbar")); Assert.False(Visible(w, "os-menubar")); Assert.False(Visible(w, "os-topbar"));
        }
        finally { w.Close(); }
    }
    [AvaloniaFact(DisplayName = "Mehrfachauswahl wie im System: Umschalt-Klick Bereich, Strg-Klick dazu/weg, Rahmen mit der Maus aufziehen, Alles auswählen, mehrere kopieren/einfügen und in den Papierkorb, Klick ins Leere hebt auf")]
    public void Multi_select()
    {
        var (w0, _, _) = Ui.Open(); MainWindow? w = null;
        try
        {
            w = Switch(w0, "windows"); w.Width = 1600; w.Height = 1000;
            var dir = w.Files.KnownFolders().First(k => k.Id == "bilder").Path; var target = w.Files.KnownFolders().First(k => k.Id == "musik").Path;
            var names = new[] { "a.txt", "b.txt", "c.txt", "d.txt", "e.txt" };
            foreach (var n in names) File.WriteAllText(Path.Combine(dir, n), n);
            w.FilesView = "liste"; w.FilesGo(dir); Pump();
            string P(string n) => Path.Combine(dir, n);
            w.FilesClick(P("a.txt"), KeyModifiers.None); w.FilesClick(P("c.txt"), KeyModifiers.Shift);
            Assert.Equal(["a.txt", "b.txt", "c.txt"], w.FilesSelection.Select(Path.GetFileName).Order());
            Assert.Contains("3 Elemente ausgewählt", w.Find<TextBlock>("fb-status").Text);
            // echter Strg-Klick mit der Maus auf e.txt
            Pump(); var eb = w.Find<Button>("fb-item-e.txt"); var pt = eb.TranslatePoint(new Avalonia.Point(20, eb.Bounds.Height / 2), w)!.Value;
            w.MouseDown(pt, MouseButton.Left, RawInputModifiers.Control); w.MouseUp(pt, MouseButton.Left, RawInputModifiers.Control); Pump();
            Assert.Equal(4, w.FilesSelection.Count);
            w.FilesClick(P("b.txt"), KeyModifiers.Control); Assert.Equal(3, w.FilesSelection.Count); Assert.DoesNotContain(P("b.txt"), w.FilesSelection);
            // mehrere kopieren und woanders einfügen
            w.FilesCopy(false); w.FilesGo(target); Pump(); w.FilesPaste(); Pump();
            Assert.Equal(["a.txt", "c.txt", "e.txt"], Directory.GetFiles(target).Select(Path.GetFileName).Order());
            Assert.Equal(3, w.FilesSelection.Count);
            // Rahmen aufziehen: rechts neben den Namen von oben nach unten über die ersten zwei Zeilen
            w.FilesGo(dir); Pump();
            var area = w.Find<Border>("fb-area"); var first = w.Find<Button>("fb-item-a.txt"); var second = w.Find<Button>("fb-item-b.txt");
            var from = first.TranslatePoint(new Avalonia.Point(first.Bounds.Width - 30, 2), w)!.Value; var to = second.TranslatePoint(new Avalonia.Point(first.Bounds.Width - 40, second.Bounds.Height - 4), w)!.Value;
            var start = area.TranslatePoint(new Avalonia.Point(area.Bounds.Width - 20, area.Bounds.Height - 20), w)!.Value; // im Leeren unten rechts beginnen
            w.MouseDown(start, MouseButton.Left); for (var i = 1; i <= 6; i++) { w.MouseMove(start + (from - start) * (i / 6.0)); Pump(); } w.MouseUp(from, MouseButton.Left); Pump();
            Assert.True(w.FilesSelection.Count >= 2, "Rahmen hat nichts gewählt: " + w.FilesSelection.Count);
            Assert.DoesNotContain(w.FilesSelection, x => x.EndsWith("Musik", StringComparison.Ordinal));
            // Klick ins Leere hebt die Auswahl auf
            w.MouseDown(start, MouseButton.Left); w.MouseUp(start, MouseButton.Left); Pump();
            Assert.Empty(w.FilesSelection);
            // Strg+A, dann Entf: alles in den Papierkorb
            Assert.True(w.FilesKey(Key.A, KeyModifiers.Control)); Assert.Equal(5, w.FilesSelection.Count);
            Assert.True(w.FilesKey(Key.Delete, KeyModifiers.None)); Pump();
            Assert.Empty(Directory.GetFiles(dir)); Assert.Contains("5 Elemente", w.FilesMessage);
            // Mac: ⌘-Klick (PC-Tastatur Strg) schaltet ebenfalls dazu
            w = Switch(w, "mac"); w.FilesGo(target); Pump();
            w.FilesClick(Path.Combine(target, "a.txt"), KeyModifiers.None); w.FilesClick(Path.Combine(target, "e.txt"), KeyModifiers.Control);
            Assert.Equal(2, w.FilesSelection.Count);
            Assert.False(w.FilesKey(Key.Enter, KeyModifiers.None)); // Umbenennen geht nur bei einem Element
        }
        finally { Back(w ?? w0); }
    }
    [AvaloniaFact(DisplayName = "Registerkarten wie Explorer/Finder/Dateien: Strg/⌘+T neu, Mittelklick-Ordner in neuer Karte, Wechsel per Klick und Strg+Bild↓, eigener Verlauf je Karte, Strg/⌘+W schließt erst die Karte, dann das Fenster")]
    public void Tabs()
    {
        var (w0, _, _) = Ui.Open(); MainWindow? w = null;
        try
        {
            w = Switch(w0, "windows");
            var k = w.Files.KnownFolders(); var docs = k.First(x => x.Id == "dokumente").Path; var pics = k.First(x => x.Id == "bilder").Path;
            Directory.CreateDirectory(Path.Combine(docs, "Steuern"));
            w.FilesGo(docs); Pump();
            Assert.True(Visible(w, "fb-tabbar")); Assert.Equal(1, w.FilesTabCount); // Windows: Leiste immer sichtbar
            Assert.True(w.FilesKey(Key.T, KeyModifiers.Control)); Pump();
            Assert.Equal(2, w.FilesTabCount); Assert.Equal(1, w.FilesTabIndex); Assert.True(SamePath(w.FilesDir, w.Files.Home));
            w.FilesGo(pics); Pump(); Assert.Equal(["Dokumente", "Bilder"], w.FilesTabTitles);
            // Karte 1 behält ihren Verlauf
            w.Find<Button>("fb-tab-0").Press(); Pump(); Assert.True(SamePath(w.FilesDir, docs));
            w.FilesGo(Path.Combine(docs, "Steuern")); w.FilesBack(); Assert.True(SamePath(w.FilesDir, docs));
            Assert.True(w.FilesKey(Key.PageDown, KeyModifiers.Control)); Assert.Equal(1, w.FilesTabIndex); Assert.True(SamePath(w.FilesDir, pics));
            // Ordner in neuer Karte (Kontextmenü/Mittelklick)
            w.FilesSelectTab(0); w.FilesNewTab(Path.Combine(docs, "Steuern")); Pump();
            Assert.Equal(3, w.FilesTabCount); Assert.Equal("Steuern", w.FilesTabTitles[1]);
            // Strg+W schließt die Karte, bei der letzten das Fenster
            Assert.True(w.FilesKey(Key.W, KeyModifiers.Control)); Assert.Equal(2, w.FilesTabCount);
            w.Find<Button>("fb-tab-close-1").Press(); Pump(); Assert.Equal(1, w.FilesTabCount);
            var id = w.Windows.Active!.Id; Assert.Equal("dateien", w.Windows.Active!.Page);
            Assert.True(w.FilesKey(Key.W, KeyModifiers.Control)); Pump(); Assert.Null(w.Windows.Get(id));
            // Mac: Tab-Leiste erst ab zwei Tabs; ⌘W (PC: Strg+W) schließt zuerst den Tab
            w = Switch(w, "mac"); w.FilesGo(docs); Pump();
            Assert.False(Visible(w, "fb-tabbar"));
            w.FilesNewTab(pics); Pump(); Assert.True(Visible(w, "fb-tabbar"));
            var wid = w.Windows.Active!.Id;
            Assert.True(w.OsWindowKey(Key.W, KeyModifiers.Control)); Pump();
            Assert.Equal(1, w.FilesTabCount); Assert.NotNull(w.Windows.Get(wid));
        }
        finally { Back(w ?? w0); }
    }

    [AvaloniaFact(DisplayName = "Wie im Original: eine Kopfleiste in GNOME-Dateien/Finder, Uhr öffnet Mitteilungen + Monatskalender, Schnelleinstellungen mit Kacheln, „Nicht stören“, GNOME-Suche per Lupe/Lostippen, „Andere Orte“, Explorer springt beim Tippen")]
    public void Original_details()
    {
        var (w0, _, _) = Ui.Open(); MainWindow? w = null;
        try
        {
            w = Switch(w0, "linux");
            var docs = w.Files.KnownFolders().First(k => k.Id == "dokumente").Path;
            foreach (var n in new[] { "Brief.txt", "Bericht.txt", "Antrag.pdf" }) File.WriteAllText(Path.Combine(docs, n), n);
            w.FilesGo(docs); Pump();
            var id = w.Windows.Active!.Id;
            Assert.False(w.Find<Border>("win-title-" + id).IsVisible); // keine zweite Titelleiste
            Assert.True(Visible(w, "fb-headerbar")); Assert.True(Visible(w, "fb-close"));
            // Suche: hinter der Lupe, Lostippen öffnet sie und filtert
            Assert.False(Visible(w, "fb-search")); Assert.True(Visible(w, "fb-search-toggle"));
            Assert.True(w.FilesKey(Key.B, KeyModifiers.None)); Pump();
            Assert.True(w.FilesSearchOpen); Assert.True(Visible(w, "fb-search"));
            Assert.True(Visible(w, "fb-item-Brief.txt")); Assert.False(Visible(w, "fb-item-Antrag.pdf"));
            // Andere Orte
            w.FilesGo(MainWindow.PcPlace); Pump();
            Assert.True(Visible(w, "fb-no-network")); Assert.False(Visible(w, "fb-pc-folders")); Assert.False(w.FilesSearchOpen);
            // Uhr: Mitteilungen und Kalender; „Nicht stören“ hält Hinweise zurück
            w.Toast("Export fertig"); Pump();
            w.Find<Button>("os-toggle-clock").Press(); Pump();
            Assert.True(w.OsCalendarOpen); Assert.True(Visible(w, "os-calendar")); Assert.True(Visible(w, "os-notes"));
            Assert.Contains("Export fertig", w.OsNotifications);
            w.ToggleOsWidgets(); Pump();
            w.Find<Button>("os-toggle-quick").Press(); Pump();
            Assert.True(w.OsQuickOpen); Assert.True(Visible(w, "os-q-dnd")); Assert.True(Visible(w, "os-q-power"));
            w.Find<Button>("os-q-dnd").Press(); Pump(); Assert.True(w.OsDnd); Assert.True(w.OsQuickOpen);
            var before = w.GetVisualDescendants().OfType<Border>().Count(b => b.Name == "toast");
            w.Toast("Leise"); Pump();
            Assert.Equal(before, w.GetVisualDescendants().OfType<Border>().Count(b => b.Name == "toast")); Assert.Contains("Leise", w.OsNotifications);
            w.Find<Button>("fb-close").Press(); Pump(); Assert.Null(w.Windows.Get(id)); // Schließen in der Kopfleiste
            // Windows: Uhr öffnet Benachrichtigungen + Kalender rechts, Tippen springt zum Element
            w = Switch(w, "windows"); w.FilesView = "liste"; w.FilesGo(docs); Pump();
            Assert.False(Visible(w, "win-title-" + w.Windows.Active!.Id)); // Windows-Explorer: Registerkarten sind die Titelleiste
            Assert.True(Visible(w, "head-close")); Assert.True(Visible(w, "head-min")); Assert.Equal(46, w.GetVisualDescendants().OfType<Button>().First(b => b.Name == "head-close").Width);
            w.FilesNewTab(); Pump(); Assert.Equal(2, w.FilesTabCount);
            w.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Key.Tab, KeyModifiers = KeyModifiers.Control }); Pump();
            Assert.Equal(0, w.FilesTabIndex); // Strg+Tab wechselt die Karte, nicht das Fenster
            w.FilesCloseTab(1); Pump();
            Assert.True(w.FilesKey(Key.B, KeyModifiers.None)); Assert.Equal("Bericht.txt", Path.GetFileName(w.FilesSelected));
            w.Find<Button>("os-toggle-clock").Press(); Pump();
            Assert.True(Visible(w, "os-calendar-panel")); Assert.True(Visible(w, "os-cal-month"));
            w.Find<Button>("os-cal-next").Press(); Pump();
            Assert.Contains(DateTime.Today.AddMonths(1).ToString("MMMM", System.Globalization.CultureInfo.GetCultureInfo("de-DE")), w.Find<TextBlock>("os-cal-month").Text);
            w.Find<Button>("os-toggle-quick").Press(); Pump(); Assert.True(w.OsQuickOpen); Assert.False(Visible(w, "os-calendar-panel"));
            // Mac: Finder mit Ampel in der Seitenleiste, keine zweite Titelleiste
            w = Switch(w, "mac"); w.FilesGo(docs); Pump();
            Assert.False(w.Find<Border>("win-title-" + w.Windows.Active!.Id).IsVisible); Assert.True(Visible(w, "fb-close")); Assert.True(Visible(w, "fb-max"));
        }
        finally { Back(w ?? w0); }
    }

    [AvaloniaFact(DisplayName = "Programme im Stil des Systems: Windows-Menüzeile im Fenster, Mac-Menüs oben in der Menüleiste (Ablage …), GNOME-Kopfleiste mit „Speichern“ blau und Hauptmenü ☰ – Menüeinträge lösen dieselben Befehle aus")]
    public void Programs_like_os()
    {
        var (w0, _, _) = Ui.Open(); MainWindow? w = null;
        try
        {
            static MenuItem Item(MainWindow w, string name) => w.GetLogicalDescendants().OfType<MenuItem>().First(m => m.Name == name);
            w = Switch(w0, "windows"); w.Width = 1600; w.Height = 1000;
            w.Show("bild"); Pump(); w.ProgramNew("bild"); w.Show("bild"); Pump();
            Assert.True(Visible(w, "prog-menubar")); Assert.True(Visible(w, "prog-ribbon"));
            Assert.Contains("Datei", w.OsProgramMenus("bild")); Assert.Contains("Bearbeiten", w.OsProgramMenus("bild"));
            w.ImgZoom = 0; Item(w, "menu-prog-cmd-img-zoom-100").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent)); Pump();
            Assert.Equal(1, w.ImgZoom); // Menüeintrag = derselbe Befehl wie der Knopf
            Assert.True(Visible(w, "prog-cmd-img-zoom-fit")); // Knöpfe bleiben im Menüband
            // Mac: Programm-Menüs wandern in die Menüleiste oben
            w = Switch(w, "mac"); w.Show("bild"); Pump(); w.ProgramNew("bild"); w.Show("bild"); Pump(); Pump();
            Assert.False(Visible(w, "prog-menubar")); Assert.True(Visible(w, "prog-toolbar"));
            Assert.Contains("Ablage", w.OsProgramMenus("bild"));
            Assert.True(Visible(w, "os-menu-prog-Ablage")); Assert.True(Visible(w, "os-menu-prog-Bearbeiten"));
            w.ImgZoom = 0; Assert.True(w.OsRunMenu("bild", "prog-cmd-img-zoom-100")); Pump(); Assert.Equal(1, w.ImgZoom);
            // GNOME: Kopfleiste, Speichern als empfohlene Aktion, Hauptmenü
            w = Switch(w, "linux"); w.Show("bild"); Pump(); w.ProgramNew("bild"); w.Show("bild"); Pump();
            Assert.True(Visible(w, "prog-headerbar")); Assert.True(Visible(w, "prog-mainmenu")); Assert.False(Visible(w, "prog-menubar"));
            var save = w.Find<Button>("prog-save"); Assert.Same(Avalonia.Media.Brushes.White, save.Foreground); // empfohlene Aktion: Akzentfläche, weiße Schrift
            Assert.NotNull(w.Find<Button>("prog-mainmenu").Flyout);
            Assert.False(Visible(w, "prog-tabs")); // Reiter erst ab zwei Dokumenten
        }
        finally { Back(w ?? w0); }
    }

    [AvaloniaFact(DisplayName = "Studio-Fenster wie im System: Mac und GNOME – KI-Zeile ist die Titelleiste (Ampel bzw. Schließen, Titel, Zurück), Windows behält die Titelleiste; Schließen in der Kopfleiste schließt das Fenster")]
    public void Studio_windows()
    {
        var (w0, _, _) = Ui.Open(); MainWindow? w = null;
        try
        {
            foreach (var style in new[] { "mac", "linux" })
            {
                w = Switch(w ?? w0, style);
                w.Show("brief"); Pump(); Pump();
                var id = w.Windows.Active!.Id;
                Assert.False(w.Find<Border>("win-title-" + id).IsVisible); // keine zweite Leiste
                var bar = w.Find<Border>("auftrag-leiste");
                Assert.Contains(bar.GetLogicalDescendants().OfType<Button>(), b => b.Name == "head-close");
                Assert.Contains(bar.GetLogicalDescendants().OfType<Button>(), b => b.Name == "head-back");
                Assert.Equal("Briefe schreiben", bar.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Name == "head-title").Text);
                Assert.Equal(style == "mac", bar.GetLogicalDescendants().OfType<Button>().Any(b => b.Name == "head-max"));
                bar.GetLogicalDescendants().OfType<Button>().First(b => b.Name == "head-close").Press(); Pump();
                Assert.Null(w.Windows.Get(id));
            }
            w = Switch(w!, "windows"); w.Show("brief"); Pump();
            Assert.True(w.Find<Border>("win-title-" + w.Windows.Active!.Id).IsVisible);
            Assert.DoesNotContain(w.Find<Border>("auftrag-leiste").GetLogicalDescendants().OfType<Button>(), b => b.Name == "head-close");
        }
        finally { Back(w ?? w0); }
    }

    static bool SamePath(string a, string b) => string.Equals(Path.GetFullPath(a).TrimEnd((char)92, '/'), Path.GetFullPath(b).TrimEnd((char)92, '/'), StringComparison.OrdinalIgnoreCase);
}
