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
}
