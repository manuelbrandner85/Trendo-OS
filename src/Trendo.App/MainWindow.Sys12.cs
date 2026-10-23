using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>SYS-12 Barrierefreiheit & Anpassung (freigegebener Katalog, 21.10.2026): Sehen, Hilfen, Prüfen.</summary>
public partial class MainWindow
{
    public static readonly List<(string Key, string Label, string Module)> BedienTabs = [];
    private string _a11yOut = "";
    public UserSettings Settings => _settings;
    /// <summary>Akzentfarbe für Text auf der Fläche – automatisch so angepasst, dass der Kontrast AA erreicht.</summary>
    public static string PanelColor(string theme) => theme switch { "hell" => "#F4F7FC", "kontrast" => "#000000", _ => "#0B1730" };
    private IBrush AccentText => Brush.Parse(A11y.AccentForText(Hx(((ISolidColorBrush)Accent).Color), PanelColor(_settings.Theme)));

    private static string Hx(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private void InitSys12()
    {
        RegisterTab(BedienTabs, "bedien-sehen", "Sehen", "SYS-12", BedienSeeView, "schriftgröße", "schrift", "kontrast", "farbblind", "einfacher modus", "große knöpfe", "mauszeiger", "animation");
        RegisterTab(BedienTabs, "bedien-hilfen", "Hilfen", "SYS-12", BedienHelpView, "leichte sprache", "vorlesen", "diktat", "benachrichtigung", "leise", "einstellungen sichern", "export", "import");
        RegisterTab(BedienTabs, "bedien-pruefen", "Prüfen", "SYS-12", BedienCheckView, "barrierefreiheit", "bericht", "bildschirmleser", "narrator", "tastatur", "englisch");
        RestoreWindowPlace();
        Closing += (_, _) => { if (WindowState == WindowState.Normal) { _settings.WindowRect = [Position.X, Position.Y, (int)Width, (int)Height]; SaveSettings(); } };
    }
    private Control BedienPage(string title, Control body) => W45Page(title, BedienTabs, "bedien", body);

    private void RestoreWindowPlace()
    {
        if (_settings.WindowRect is not { Length: 4 } r) return;
        var screens = Screens?.All.Select(s => new A11y.Rect(s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height)).ToList() ?? [];
        if (A11y.RestorePosition(new(r[0], r[1], r[2], r[3]), screens) is { } p) { WindowStartupLocation = WindowStartupLocation.Manual; Position = new PixelPoint(p.X, p.Y); Width = p.W; Height = p.H; }
    }

    /// <summary>Nach jedem Seitenwechsel: Mindestschrift, Namen für Bildschirmleser, Tab-Erreichbarkeit, keine Übergänge bei „Bewegung reduzieren“, große Knöpfe im einfachen Modus.</summary>
    public void ApplyAccessibility(Control root)
    {
        foreach (var c in root.GetLogicalDescendants().OfType<Control>().Prepend(root))
        {
            if (_settings.ReduceMotion && c.Transitions is { Count: > 0 }) c.Transitions = null;
            if (c is TextBlock tb) tb.FontSize = A11y.EffectiveFont(tb.FontSize, _settings.FontMin);
            if (c is TemplatedControl tc)
            {
                tc.FontSize = A11y.EffectiveFont(tc.FontSize, _settings.FontMin);
                if (_settings.SimpleMode && c is Button btn)
                {
                    tc.FontSize = Math.Max(tc.FontSize, A11y.SimpleButtonFont);
                    if (btn.MinHeight < 40) btn.MinHeight = 40;
                }
            }
            if (c is Button or TextBox or CheckBox or ComboBox or ToggleButton)
            {
                c.Focusable = true; if (c is Avalonia.Input.InputElement ie) ie.IsTabStop = true;
                if (string.IsNullOrEmpty(AutomationProperties.GetName(c)) && LabelOf(c) is { Length: > 0 } n) AutomationProperties.SetName(c, n);
            }
        }
    }
    public static string? LabelOf(Control c) => c switch
    {
        TextBox t => t.PlaceholderText ?? t.Watermark ?? t.Name,
        ContentControl { Content: string s } => s.TrimStart('✓', ' '),
        ContentControl { Content: Control inner } => inner.GetLogicalDescendants().Prepend(inner).OfType<TextBlock>().Select(x => x.Text).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x) && x.Any(char.IsLetter))
                                                  ?? ToolTip.GetTip(c) as string ?? c.Name,
        _ => ToolTip.GetTip(c) as string ?? c.Name,
    };

    private CheckBox Schalter(string name, string label, bool on, Action<bool> set, string? page)
    {
        var cb = new CheckBox { Name = name, Foreground = Text, IsChecked = on };
        void Malen() => cb.Content = label + (cb.IsChecked == true ? " · Ein" : " · Aus");
        Malen();
        cb.IsCheckedChanged += (_, _) =>
        {
            Malen();
            set(cb.IsChecked == true);
            if ((name == "reduce-motion" || name == "a11y-motion") && cb.IsChecked == true) _center.Transitions = null;
            SaveSettings();
            if (page is not null) Reopen(page);
        };
        return cb;
    }

    private CheckBox Opt(string name, string label, bool on, Action<bool> set, string page) => Schalter(name, label, on, set, page);

    // ---------- Sehen ----------
    private Control BedienSeeView()
    {
        var sp = new StackPanel { Spacing = 10 }; const string V = "bedien-sehen";
        var fonts = Line(); foreach (var m in A11y.FontMins)
        { var on = _settings.FontMin == m; fonts.Children.Add(Act($"font-{m}", (on ? "✓ " : "") + (m == 0 ? "Wie gestaltet" : $"mindestens {m} pt"), () => { _settings.FontMin = m; SaveSettings(); Show(V); }, on)); }
        sp.Children.Add(Card(Section("Schriftgröße", "Unabhängig von der Anzeigegröße: kleine Schrift wird auf diese Mindestgröße angehoben.", fonts)));
        sp.Children.Add(Card(Section("Einfacher Modus", "Nur die wichtigsten Kacheln, keine Fachwerkzeuge, größere Knöpfe.",
            Opt("simple-mode", "Einfachen Modus einschalten", _settings.SimpleMode, v => _settings.SimpleMode = v, V))));
        sp.Children.Add(Card(Section("Farben und Bewegung", "Status zusätzlich mit Zeichen (✓ ⚠ ✗), damit man ihn auch ohne Farben erkennt.",
            Opt("color-blind", "Status mit Zeichen statt nur Farbe", _settings.ColorBlind, v => _settings.ColorBlind = v, V),
            Opt("a11y-motion", "Bewegung reduzieren (keine Animationen)", _settings.ReduceMotion, v => _settings.ReduceMotion = v, V),
            new TextBlock { Name = "status-sample", Foreground = Text, Text = _settings.ColorBlind ? $"{A11y.WithSymbol(A11y.Level.Gut, "In Ordnung")}   {A11y.WithSymbol(A11y.Level.Hinweis, "Hinweis")}   {A11y.WithSymbol(A11y.Level.Problem, "Problem")}" : "● In Ordnung   ● Hinweis   ● Problem" },
            new TextBlock { Name = "accent-sample", Text = "So sieht Text in deiner Akzentfarbe aus – Kontrast geprüft.", Foreground = AccentText })));
        sp.Children.Add(Card(Section("Großer Mauszeiger", "Mauszeigergröße und -farbe stellst du in Windows ein.",
            Line(Act("pointer-settings", "Windows-Einstellungen öffnen", () => Try(V, () => { if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Das geht nur unter Windows."); Launch("ms-settings:easeofaccess-mousepointer"); }))))));
        return BedienPage("Bedienung – Sehen", sp);
    }

    // ---------- Hilfen ----------
    private Control BedienHelpView()
    {
        var sp = new StackPanel { Spacing = 10 }; const string V = "bedien-hilfen";
        sp.Children.Add(Card(Section("Leichte Sprache", "Hilfetexte werden kürzer: wenige kurze Sätze, einfache Wörter.",
            Opt("easy-lang", "Leichte Sprache für Hilfetexte", _settings.EasyLanguage, v => { _settings.EasyLanguage = v; _settings.SeenHints.Clear(); }, V),
            new TextBlock { Name = "easy-sample", Foreground = Text, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Text = _settings.EasyLanguage ? A11y.Easy("Die Konfiguration ist verfügbar.") : "Die Konfiguration ist verfügbar." })));
        var notes = Line(); foreach (var (id, label) in new[] { ("normal", "Normal – Fenster kommt nach vorn"), ("leise", "Leise – nur Eintrag, nichts springt auf") })
        { var on = _settings.NotifyStyle == id; notes.Children.Add(Act("notify-" + id, (on ? "✓ " : "") + label, () => { _settings.NotifyStyle = id; SaveSettings(); Show(V); }, on)); }
        sp.Children.Add(Card(Section("Benachrichtigungen", "Wie sich Erinnerungen melden.", notes)));
        var say = new TextBox { Name = "speak-text", PlaceholderText = "Text zum Vorlesen", AcceptsReturn = true, Width = 520, MinHeight = 60, Text = _w45Msg };
        sp.Children.Add(Card(Section("Vorlesen", "Liest Text mit der Windows-Stimme vor – ganz ohne Internet.",
            say, Line(Act("speak-run", "Vorlesen", () => Try(V, () =>
            {
                var cmd = A11y.SpeakCommand(say.Text ?? "", OperatingSystem.IsWindows(), Cli.Find) ?? throw new InvalidOperationException(string.IsNullOrWhiteSpace(say.Text) ? "Bitte Text eingeben." : "BLOCKED: Hier gibt es keine Sprachausgabe (unter Windows ist sie eingebaut).");
                _ = Task.Run(() => Cli.Run(cmd.Exe, cmd.Args, timeoutMs: 300000)); Say("Wird vorgelesen …", V);
            }), true)),
            P("Diktieren: Drück Windows-Taste + H in einem Textfeld – Windows schreibt mit, was du sagst.", Muted, 11))));
        var ef = Field("settings-file", "Datei zum Einlesen (.json)");
        sp.Children.Add(Card(Section("Einstellungen sichern", "Alle Einstellungen in eine Datei speichern oder auf einem anderen Rechner wieder einlesen.",
            Line(Act("settings-export", "Speichern", () => Try(V, () => { var f = Path.Combine(WorkFolder("Einstellungen"), $"trendo-einstellungen-{DateTime.Now:yyyyMMdd-HHmm}.json"); A11y.ExportSettings(_settings, f); Say("Gespeichert: " + f, V); }), true),
                ef, Act("settings-import", "Einlesen", () => Try(V, () => { var s = A11y.ImportSettings(Clean(ef)); s.Save(_core.DataRoot); _settings = s; _core.Audit.Append("app", "settings.import", Path.GetFileName(Clean(ef))); Reopen(V); }))))));
        return BedienPage("Bedienung – Hilfen", sp);
    }

    // ---------- Prüfen ----------
    public A11y.PageCheck CheckPage(string key)
    {
        Show(key); var root = ActivePageRoot!;
        var ctrls = root.GetLogicalDescendants().OfType<Control>().Where(c => c is Button or TextBox or CheckBox or ComboBox).ToList();
        var unnamed = ctrls.Where(c => string.IsNullOrWhiteSpace(AutomationProperties.GetName(c))).Select(c => c.Name ?? c.GetType().Name).ToList();
        var labels = root.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").Concat(ctrls.Select(c => LabelOf(c) ?? "")).Where(s => s.Length > 0);
        var bg = PanelColor(_settings.Theme);
        var low = root.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Foreground is ISolidColorBrush { Color.A: 255 } b && !A11y.MeetsAA(Hx(b.Color), bg, t.FontSize >= 18)).Select(t => (t.Text ?? "").Length > 20 ? t.Text![..20] + "…" : t.Text ?? "").ToList();
        return new(key, ctrls.Count, unnamed, A11y.EnglishLabels(labels), low, NavDepth(key));
    }
    /// <summary>Ebene 1 = Hauptbereich, 2 = Reiter in einer Gruppe. Tiefer gibt es nicht.</summary>
    public int NavDepth(string key) => _showMap.ContainsKey(key) ? (TabbedPages.Contains(key) ? 2 : 1) : 3;
    private readonly HashSet<string> TabbedPages = [];

    public List<string> AllPageKeys() => _showMap.Keys.Where(k => k is not "bedien-pruefen" and not "einrichtung").ToList();

    private Control BedienCheckView()
    {
        var sp = new StackPanel { Spacing = 10 }; const string V = "bedien-pruefen";
        sp.Children.Add(Card(Section("Barrierefreiheits-Bericht", "Öffnet jede Seite einmal und prüft: Namen für Bildschirmleser, englische Wörter, Textkontrast und Navigationstiefe. Der Bericht landet im Ordner „berichte“.",
            Line(Act("a11y-report", "Alle Seiten prüfen", () => Try(V, () =>
            {
                var keys = AllPageKeys(); var checks = keys.Select(CheckPage).ToList();
                var f = Path.Combine(_core.DataRoot, "berichte", $"barrierefreiheit-{DateTime.Now:yyyyMMdd-HHmmss}.md"); Directory.CreateDirectory(Path.GetDirectoryName(f)!);
                File.WriteAllText(f, A11y.Report(checks, _settings.Theme));
                _a11yOut = $"{checks.Count} Seiten geprüft. Ohne Namen: {checks.Sum(c => c.Unnamed.Count)} · Englisch: {checks.Sum(c => c.EnglishLabels.Count)} · Kontrast: {checks.Sum(c => c.LowContrast.Count)} · zu tief: {checks.Count(c => c.Depth > 2)}\nBericht: {f}";
                Say("Prüfung fertig.", V);
            }), true)), new TextBlock { Name = "a11y-out", Text = _a11yOut, Foreground = Text, TextWrapping = TextWrapping.Wrap })));
        sp.Children.Add(Card(Section("Tastatur", "Alles geht ohne Maus: Tab springt zum nächsten Knopf, Enter oder Leertaste drückt ihn. Strg+K sucht, F1 hilft.", new TextBlock())));
        return BedienPage("Bedienung – Prüfen", sp);
    }
}
