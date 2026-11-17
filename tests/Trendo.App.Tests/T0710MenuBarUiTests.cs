using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0710 (Wunsch 17.11.2026, klassische Menüleiste): Datei · Bearbeiten · Ansicht · Fachmenüs · KI · Hilfe in jedem Programm –
/// geprüft mit echten Tastaturereignissen im Fenster (Alt+D, Pfeil runter, Enter, Esc, F10, Strg+O …) und echten Dateien.</summary>
public class T0710_MenuBar_Ui(ITestOutputHelper log)
{
    /// <summary>Fünf Programme aus verschiedenen Fächern: Bild, Video, Ton, Büro (Formulare), Druck (Karten).</summary>
    public static readonly string[] Fach = ["bild", "schnitt", "ton", "formulare-programm", "karten-programm"];

    /// <summary>Fensterbreite, in der im Test (Ersatzschrift ohne echte Maße, Zeichen breiter als in Wirklichkeit) alle Menüs in der Leiste stehen.
    /// Bei 1600 px zeigt das echte Bild alle Menüs in einer Zeile; im Test wandern sie dort unter „…“ (eigener Test unten).</summary>
    const double Wide = 3000;

    static void Settle() { for (var i = 0; i < 6; i++) Dispatcher.UIThread.RunJobs(); }
    static void Press(MainWindow w, Key k, RawInputModifiers m = RawInputModifiers.None, PhysicalKey p = PhysicalKey.None)
    { w.KeyPress(k, m, p, null); Settle(); w.KeyRelease(k, m, p, null); Settle(); }
    static void AltLetter(MainWindow w, Key k, PhysicalKey p)
    {
        w.KeyPress(Key.LeftAlt, RawInputModifiers.Alt, PhysicalKey.AltLeft, null); Settle();
        w.KeyPress(k, RawInputModifiers.Alt, p, null); Settle(); w.KeyRelease(k, RawInputModifiers.Alt, p, null); Settle();
        w.KeyRelease(Key.LeftAlt, RawInputModifiers.None, PhysicalKey.AltLeft, null); Settle();
    }
    static Control? Focused(MainWindow w) => w.FocusManager?.GetFocusedElement() as Control;
    static Menu Bar(MainWindow w) => w.ActiveMenuBar() ?? throw new Xunit.Sdk.XunitException("keine Menüleiste auf " + w.CurrentView);
    static MenuItem Top(MainWindow w, string title) => Bar(w).Items.OfType<MenuItem>().First(m => m.Name == "menubar-" + title);
    static MenuItem Item(MainWindow w, string name) => Bar(w).GetLogicalDescendants().OfType<MenuItem>().First(m => m.Name == name);

    /// <summary>Mit Pfeil runter bis zum Eintrag – wie eine Person, die die Liste entlanggeht.</summary>
    static void DownTo(MainWindow w, string name)
    {
        for (var i = 0; i < 20 && Focused(w)?.Name != name; i++) Press(w, Key.Down, p: PhysicalKey.ArrowDown);
        Assert.Equal(name, Focused(w)?.Name);
    }

    static ProgramDocument OpenDoc(MainWindow w, string key)
    {
        foreach (var old in w.Windows.Stack.Reverse().Skip(3).ToList()) w.CloseWindow(old.Id);
        while (w.ProgramDocs(key).Count > 0) w.ProgramClose(key, 0);
        w.Show(key);
        var d = w.ProgramNew(key, MainWindow.Programs[key].Templates.FirstOrDefault()?.Id);
        w.Show(key); Settle(); w.UpdateLayout(); Settle();
        return d;
    }

    [AvaloniaFact(DisplayName = "T-0710: Alt+D öffnet „Datei“, Pfeil runter bis „Speichern“, Enter schreibt die echte Datei – in fünf Programmen aus verschiedenen Fächern")]
    public void AltD_down_enter_saves_real_file()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            Assert.False(w.AutoSaveDocs); // gespeichert wird nur durch den Menüeintrag
            foreach (var key in Fach)
            {
                var d = OpenDoc(w, key);
                d.Edit("Menütest", x => x["menue-test"] = key);
                w.Show(key); Settle();
                Assert.True(d.Dirty);
                AltLetter(w, Key.D, PhysicalKey.D);
                Assert.True(Top(w, "Datei").IsSubMenuOpen, key + ": Alt+D hat „Datei“ nicht geöffnet");
                DownTo(w, "menu-prog-save");
                Press(w, Key.Enter, p: PhysicalKey.Enter);
                Assert.False(d.Dirty, key);
                Assert.NotNull(d.Path);
                Assert.True(File.Exists(d.Path), key);
                Assert.Contains("menue-test", File.ReadAllText(d.Path!));
                log.WriteLine($"{key}: {Path.GetFileName(d.Path)} {new FileInfo(d.Path!).Length} Bytes");
                w.ProgramClose(key, 0);
            }
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0710: Rückgängig über „Bearbeiten“ (Alt+B, Enter); ausgegrauter Eintrag tut nichts – weder Buchstabe noch Pfeil erreichen ihn")]
    public void Undo_through_menu_and_disabled_entry_does_nothing()
    {
        var (w, _, _) = Ui.Open(); w.Width = Wide; w.Height = 1000;
        try
        {
            foreach (var key in Fach)
            {
                var d = OpenDoc(w, key);
                d.Edit("Menütest", x => x["menue-undo"] = 1);
                w.Show(key); Settle();
                AltLetter(w, Key.B, PhysicalKey.B);
                Assert.True(Top(w, "Bearbeiten").IsSubMenuOpen, key);
                Assert.Equal("menu-prog-undo", Focused(w)?.Name); // erster möglicher Eintrag
                Press(w, Key.Enter, p: PhysicalKey.Enter);
                Assert.False(d.Data.ContainsKey("menue-undo"), key + ": Rückgängig hat nicht gewirkt");
                Assert.True(d.CanRedo);

                // jetzt ist „Rückgängig“ ausgegraut: Alt+B markiert gleich „Wiederholen“, der Buchstabe R (Rückgängig) bewirkt nichts
                AltLetter(w, Key.B, PhysicalKey.B);
                Assert.False(Item(w, "menu-prog-undo").IsEnabled, key);
                Assert.Equal("menu-prog-redo", Focused(w)?.Name);
                var before = d.Data.ToJsonString();
                Press(w, Key.R, p: PhysicalKey.R);
                Assert.Equal(before, d.Data.ToJsonString());
                Assert.True(d.CanRedo, key + ": der ausgegraute Eintrag hat etwas ausgelöst");
                Assert.True(Top(w, "Bearbeiten").IsSubMenuOpen);
                Press(w, Key.Up, p: PhysicalKey.ArrowUp); // nach oben: der ausgegraute Eintrag wird übersprungen
                Assert.NotEqual("menu-prog-undo", Focused(w)?.Name);
                Press(w, Key.Escape, p: PhysicalKey.Escape); Press(w, Key.Escape, p: PhysicalKey.Escape);
                Assert.False(Bar(w).IsOpen);
                w.ProgramClose(key, 0);
            }
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0710: Alt allein und F10 holen die Leiste, Pfeil rechts wandert, Esc schließt und gibt den Fokus an die Arbeit zurück")]
    public void Alt_F10_arrows_and_escape()
    {
        var (w, _, _) = Ui.Open(); w.Width = Wide; w.Height = 1000;
        try
        {
            OpenDoc(w, "bild");
            var title = w.ActivePageRoot!.GetLogicalDescendants().OfType<TextBox>().First(t => t.Name == "prog-title");
            title.Focus(); Settle();
            w.KeyPress(Key.LeftAlt, RawInputModifiers.Alt, PhysicalKey.AltLeft, null); Settle(); w.KeyRelease(Key.LeftAlt, RawInputModifiers.None, PhysicalKey.AltLeft, null); Settle();
            Assert.True(Bar(w).IsOpen); Assert.Equal("menubar-Datei", Focused(w)?.Name);
            Press(w, Key.Right, p: PhysicalKey.ArrowRight);
            Assert.Equal("menubar-Bearbeiten", Focused(w)?.Name);
            Press(w, Key.Escape, p: PhysicalKey.Escape);
            if (Bar(w).IsOpen) Press(w, Key.Escape, p: PhysicalKey.Escape);
            Assert.False(Bar(w).IsOpen);
            Assert.Same(title, Focused(w));
            Press(w, Key.F10, p: PhysicalKey.F10);
            Assert.True(Bar(w).IsOpen); Assert.Equal("menubar-Datei", Focused(w)?.Name);
            Press(w, Key.Down, p: PhysicalKey.ArrowDown);
            Assert.True(Top(w, "Datei").IsSubMenuOpen);
            Press(w, Key.Escape, p: PhysicalKey.Escape); Press(w, Key.Escape, p: PhysicalKey.Escape);
            Assert.False(Bar(w).IsOpen);
            Assert.Same(title, Focused(w));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0710: Kürzel der Menüleiste wirken – Strg+O öffnet die Dateiauswahl, Strg+Umschalt+S speichert unter neuem Namen (echte Datei)")]
    public void Shortcuts_from_menu_work()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            var asked = new List<string>();
            var target = Path.Combine(root, "menue-speichern-unter" + ProgramDocument.Extension);
            w.PickOverride = r => { asked.Add(r.Field); return Task.FromResult<string[]?>(r.Field == "prog-saveas" ? [target] : null); };
            var d = OpenDoc(w, "ton");
            var title = w.ActivePageRoot!.GetLogicalDescendants().OfType<TextBox>().First(t => t.Name == "prog-title");
            title.Focus(); Settle();
            Press(w, Key.O, RawInputModifiers.Control, PhysicalKey.O);
            Assert.Contains("prog-open", asked);
            Press(w, Key.S, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.S);
            Assert.Contains("prog-saveas", asked);
            Assert.True(File.Exists(target));
            Assert.Equal(Path.GetFullPath(target), Path.GetFullPath(d.Path!));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0710: Programme außerhalb der Hülle haben die Leiste – Trendo Text: Alt+D, Pfeil runter bis „Speichern“, Enter schreibt die DOCX-Datei")]
    public void Outside_programs_have_the_bar()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        try
        {
            var missing = new List<string>();
            foreach (var key in MainWindow.ExternalMenuPages.Take(9)) // die neun mit eigener Werkzeugleiste (V-14); die über ProgramLink angeschlossenen prüft der Test unten
            {
                foreach (var old in w.Windows.Stack.Reverse().Skip(3).ToList()) w.CloseWindow(old.Id);
                w.Show(key); Settle();
                if (w.ActiveMenuBar() is not { } bar) { missing.Add(key); continue; }
                var titles = bar.Items.OfType<MenuItem>().Select(m => m.Name).ToList();
                log.WriteLine($"{key}: {string.Join(" · ", titles)}");
                Assert.Contains("menubar-Datei", titles);
                Assert.Contains("menubar-Hilfe", titles);
            }
            Assert.Empty(missing);

            foreach (var old in w.Windows.Stack.Reverse().Skip(3).ToList()) w.CloseWindow(old.Id);
            w.Show("text"); Settle();
            AltLetter(w, Key.D, PhysicalKey.D);
            Assert.True(Top(w, "Datei").IsSubMenuOpen);
            DownTo(w, "menu-text-save");
            Press(w, Key.Enter, p: PhysicalKey.Enter);
            Assert.NotNull(w.TextPath);
            Assert.True(File.Exists(w.TextPath));
            log.WriteLine("Text gespeichert: " + Path.GetFileName(w.TextPath) + " " + new FileInfo(w.TextPath!).Length + " Bytes");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0710: alle Hüllen-Programme – eine schmale Zeile, nichts golden, keine Kürzel-Konflikte, Datei/Bearbeiten/Ansicht/KI/Hilfe; „Einfach“ hat jeden Eintrag aus „Erweitert“")]
    public void Every_shell_program_has_a_clean_bar()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        var bad = new List<string>();
        try
        {
            var names = new Dictionary<(string, int), HashSet<string>>();
            foreach (var level in new[] { UserLevel.Einfach, UserLevel.Erweitert, UserLevel.Profi })
            {
                w.ProgramSetLevel(level);
                foreach (var key in MainWindow.Programs.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList())
                {
                    OpenDoc(w, key);
                    var bar = Bar(w);
                    if (!bar.IsEffectivelyVisible) bad.Add($"{key}/{level}: Leiste unsichtbar");
                    if (bar.Bounds.Height > 28) bad.Add($"{key}/{level}: Leiste {bar.Bounds.Height:0} px hoch");
                    var model = (List<MenuTop>)bar.Tag!;
                    foreach (var c in ProgramMenu.Conflicts(model)) bad.Add($"{key}/{level}: {c}");
                    var titles = model.Select(m => m.Title).ToList();
                    foreach (var must in new[] { "Datei", "Bearbeiten", "Ansicht", "KI", "Hilfe" }) if (!titles.Contains(must)) bad.Add($"{key}/{level}: Menü {must} fehlt");
                    if (!ProgramMenu.Flat(model[0].Items).Any(c => c.Id == "prog-save")) bad.Add($"{key}: Datei ohne Speichern");
                    var gold = Color.Parse("#C9A227");
                    foreach (var tc in bar.GetLogicalDescendants().OfType<MenuItem>().Prepend<Avalonia.Controls.Primitives.TemplatedControl>(bar))
                        if (tc.Background is ISolidColorBrush { } b && b.Color == gold || tc.Foreground is ISolidColorBrush { } f && f.Color == gold) bad.Add($"{key}/{level}: {tc.Name} golden");
                    names[(key, level)] = bar.GetLogicalDescendants().OfType<MenuItem>().Select(m => m.Name!).ToHashSet();
                    w.ProgramClose(key, 0);
                }
            }
            foreach (var key in MainWindow.Programs.Keys)
                foreach (var n in names[(key, UserLevel.Erweitert)].Except(names[(key, UserLevel.Einfach)])) bad.Add($"{key}: „{n}“ fehlt in Einfach");
            log.WriteLine($"{MainWindow.Programs.Count} Programme × 3 Stufen geprüft");
        }
        finally { w.ProgramSetLevel(UserLevel.Erweitert); w.Close(); }
        Assert.Empty(bad);
    }

    [AvaloniaFact(DisplayName = "T-0710: schmales Fenster – hintere Menüs stehen unter „…“, die Kopfzeile bleibt eine Zeile; Alt+B öffnet „Bearbeiten“ trotzdem und Enter macht rückgängig")]
    public void Overflow_keeps_every_menu_reachable()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1100; w.Height = 900;
        try
        {
            var d = OpenDoc(w, "bild");
            d.Edit("Menütest", x => x["menue-eng"] = 1);
            w.Show("bild"); Settle(); w.UpdateLayout(); Settle();
            var more = Bar(w).Items.OfType<MenuItem>().First(m => m.Name == "menubar-mehr");
            Assert.True(more.IsVisible, "bei 1100 px erwartet: „…“ sichtbar");
            Assert.False(Top(w, "Bearbeiten").IsVisible);
            AltLetter(w, Key.B, PhysicalKey.B);
            Assert.True(more.IsSubMenuOpen);
            var proxy = more.Items.OfType<MenuItem>().First(p => p.IsSubMenuOpen);
            Assert.Equal("_Bearbeiten", proxy.Header);
            Press(w, Key.Enter, p: PhysicalKey.Enter); // erster möglicher Eintrag: Rückgängig
            Assert.False(d.Data.ContainsKey("menue-eng"));
            var bandRows = w.ActivePageRoot!.GetLogicalDescendants().OfType<Border>().First(b => b.Name == "prog-band").Child as Grid;
            Assert.Single(bandRows!.RowDefinitions);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0710: über ProgramLink angeschlossene Programme (T-0711) haben die Leiste – im Editor macht „Bearbeiten › Rückgängig“ (Alt+B, Enter) das Getippte rückgängig")]
    public void Linked_programs_have_the_bar()
    {
        MainWindow.LinkTypingDelay = TimeSpan.FromMilliseconds(300);
        var (w, _, _) = Ui.Open(); w.Width = Wide; w.Height = 1000;
        try
        {
            var pages = new[] { "editor", "post", "pdf", "musik-studio", "spiele", "recherche", "ki-wissen", "ki-vorlagen", "ki-skills", "nachbau" }; // „roboter“ mit T-0720 entfernt
            var missing = new List<string>();
            foreach (var key in pages)
            {
                foreach (var old in w.Windows.Stack.Reverse().Skip(3).ToList()) w.CloseWindow(old.Id);
                try { w.Show(key); } catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException) { missing.Add(key + ": " + e.Message); continue; }
                Settle();
                if (w.ActiveMenuBar() is not { } bar) { missing.Add(key + ": keine Leiste"); continue; }
                var names = bar.GetLogicalDescendants().OfType<MenuItem>().Select(m => m.Name).ToList();
                log.WriteLine($"{key}: {string.Join(" ", names.Where(n => n?.StartsWith("menubar-") == true))}");
                if (w.LinkSlots.Values.Where(x => x.Page == key).Any(x => !names.Contains("menu-" + x.Prefix + "-undo"))) missing.Add(key + ": Rückgängig fehlt im Menü");
            }
            Assert.Empty(missing);

            foreach (var old in w.Windows.Stack.Reverse().Skip(3).ToList()) w.CloseWindow(old.Id);
            w.Show("editor"); Settle();
            var tb = w.ActivePageRoot!.GetLogicalDescendants().OfType<TextBox>().First(t => t.Name == "ed-text");
            tb.Focus(); Settle();
            var before = w.EdDocs[0].Text;
            foreach (var ch in "Milch") { w.KeyTextInput(ch.ToString()); Settle(); }
            var until = DateTime.Now + MainWindow.LinkTypingDelay + TimeSpan.FromMilliseconds(200);
            while (DateTime.Now < until) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
            Settle();
            Assert.Contains("Milch", w.EdDocs[0].Text);
            AltLetter(w, Key.B, PhysicalKey.B);
            Assert.True(Top(w, "Bearbeiten").IsSubMenuOpen);
            Assert.Equal("menu-ed-undo", Focused(w)?.Name);
            Press(w, Key.Enter, p: PhysicalKey.Enter);
            Assert.Equal(before, w.EdDocs[0].Text);
        }
        finally { MainWindow.LinkTypingDelay = TimeSpan.FromSeconds(1); w.Close(); }
    }
}
