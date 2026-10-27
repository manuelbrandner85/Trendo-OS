using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Welle 0b: Designs, Akzentfarbe, Hintergründe, Animationen, Hilfe, Desktop anpassen.</summary>
public sealed partial class MainWindow
{
    private readonly Image _wallImg = new() { Name = "wallpaper" };
    private readonly Image _blurImg = new();
    private string _wallName = "orbit";
    private bool _reopening;
    private readonly StackPanel _hintHost = new() { Name = "hint-host" };

    public static readonly (string Id, string Label)[] AccentChoices =
        [("#2F86F6", "Blau"), ("#22C3A6", "Türkis"), ("#8B5CF6", "Violett"), ("#F472B6", "Pink"), ("#F59E0B", "Bernstein"), ("#22C55E", "Grün")];

    public static readonly (string Id, string Label)[] WidgetChoices =
        [("widget-projekte", "Aktuelle Projekte"), ("widget-ki", "KI-Router"), ("widget-system", "Geräte und Messwerte"), ("widget-agenten", "Agenten Live"), ("widget-studiomanager", "Studio Manager"), ("widget-github", "Open Source")];

    private static string _theme = "dunkel";
    private string _settingsSection = "aussehen";
    /// <summary>Nur für den direkten Neuaufbau: die nächste Instanz öffnet denselben Abschnitt. Sonst immer „Aussehen“.</summary>
    private static string? _reopenSection;
    /// <summary>Farbe je Design: im hellen Design die helle Variante.</summary>
    private static string C(string dark, string light) => _theme == "hell" ? light : dark;

    private static void ApplyTheme(UserSettings s)
    {
        _theme = s.Theme;
        switch (s.Theme)
        {
            case "hell":
                Glass = Brush.Parse("#D9F4F7FC"); GlassStrong = Brush.Parse("#F2FFFFFF"); TileBg = Brush.Parse("#CCFFFFFF");
                Edge = Brush.Parse("#33335C99"); Muted = Brush.Parse("#41526E"); Text = Brush.Parse("#0F1B2E"); break;
            case "kontrast":
                Glass = Brush.Parse("#FF000000"); GlassStrong = Brush.Parse("#FF000000"); TileBg = Brush.Parse("#FF000000");
                Edge = Brush.Parse("#FFFFFF00"); Muted = Brush.Parse("#FFFFFFFF"); Text = Brush.Parse("#FFFFFFFF"); break;
            default:
                Glass = Brush.Parse("#B30B1730"); GlassStrong = Brush.Parse("#F50A1428"); TileBg = Brush.Parse("#99142445");
                Edge = Brush.Parse("#3355A8FF"); Muted = Brush.Parse("#9DB0CF"); Text = Brush.Parse("#EEF3FB"); break;
        }
        Accent = Brush.Parse(s.Theme == "kontrast" ? "#FFD400" : AccentChoices.Any(a => a.Id == s.EffectiveAccent(DateTime.Now)) ? s.EffectiveAccent(DateTime.Now) : "#2F86F6");
        // Vorgabe 27.10.2026: hochwertig statt flach – Flächen mit Verlauf, Kanten mit Lichtkante oben, weiche Schatten (nicht im Kontrast-Design)
        if (s.Theme == "kontrast") { EdgeLit = Edge; CardShadow = default; return; }
        var light = s.Theme == "hell";
        TileBg = Vertical(light ? "#F7FFFFFF" : "#B2213A6B", light ? "#E0E9F0FA" : "#A00F1C3A");
        EdgeLit = Vertical(light ? "#CCE8C77A" : "#80F5C451", light ? "#403B82F6" : "#333B82F6"); // Gold oben, Blau unten (Logo C, 27.10.2026)
        CardShadow = BoxShadows.Parse(light ? "0 8 22 -10 #40223355" : "0 10 26 -10 #99000614");
    }

    private static IBrush EdgeLit = new SolidColorBrush(Color.Parse("#3355A8FF"));
    private static BoxShadows CardShadow = BoxShadows.Parse("0 10 26 -10 #99000614");
    private static LinearGradientBrush Vertical(string top, string bottom) => new()
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Color.Parse(top), 0), new GradientStop(Color.Parse(bottom), 1) },
    };

    private static Bitmap? LoadWallpaper(string name)
    {
        foreach (var uri in new[] { $"avares://TrendoOS/Assets/wallpapers/{name}.jpg", $"avares://TrendoOS/Assets/wallpapers/{name}.png", "avares://TrendoOS/Assets/wallpaper.jpg" })
            try { using var st = AssetLoader.Open(new Uri(uri)); return new Bitmap(st); } catch { }
        return null;
    }

    private void InitLook(Grid root, AppOptions opt)
    {
        _showMap["settings"] = SettingsView;
        if (_settings.Theme == "hell") RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        // Hintergrund nach Tageszeit: jede Minute prüfen
        var t = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        t.Tick += (_, _) =>
        {
            var w = _settings.CurrentWallpaper(DateTime.Now);
            if (w == _wallName) return;
            SetWallpaper(w);
            if (_settings.AccentFromWallpaper && CurrentView == "settings") Reopen("settings");
        };
        OwnTimer(t);
        if (!Motionless)
        {
            _center.Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(220), Easing = new Avalonia.Animation.Easings.CubicEaseOut() }];
            // Ruhiger Startbildschirm: Logo blendet sanft aus
            if (opt.ScreenshotPath is null && !_reopening)
            {
                var splash = new Border { Name = "splash", Background = Brush.Parse("#050B18"), IsHitTestVisible = false,
                    Child = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 14, Children = {
                        new TextBlock { Text = "Trendo OS", FontSize = 44, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#EEF3FB"), HorizontalAlignment = HorizontalAlignment.Center },
                        new TextBlock { Text = "Einen Moment …", FontSize = 13, Foreground = Brush.Parse("#9DB0CF"), HorizontalAlignment = HorizontalAlignment.Center } } },
                    Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(700) }] };
                Grid.SetRowSpan(splash, 2); root.Children.Add(splash);
                DispatcherTimer.RunOnce(() => splash.Opacity = 0, TimeSpan.FromMilliseconds(600));
                DispatcherTimer.RunOnce(() => root.Children.Remove(splash), TimeSpan.FromMilliseconds(1500));
            }
        }
    }

    /// <summary>Wird nach jedem Seitenwechsel aufgerufen: sanftes Einblenden und Hinweis beim ersten Öffnen.</summary>
    private void AfterShow(string key)
    {
        if (!Motionless) { _center.Opacity = 0; Dispatcher.UIThread.Post(() => _center.Opacity = 1, DispatcherPriority.Background); }
        ShowHelp(key, force: false);
        EnhancePathFields(ActivePageRoot, key);
        if (ActivePageRoot is { } c0) ApplyAccessibility(c0);
        ApplyAccessibility(_hintHost);
        ApplyShellAccessibility();
    }

    /// <summary>Hover-Glow für Kacheln und Dock (aus bei „Bewegung reduzieren“).</summary>
    private void Hover(Control c)
    {
        if (Motionless) return;
        c.RenderTransformOrigin = RelativePoint.Center;
        c.Transitions = [new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(140) }];
        c.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("scale(1)");
        c.PointerEntered += (_, _) => c.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("scale(1.05)");
        c.PointerExited += (_, _) => c.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("scale(1)");
    }

    // ---------- Hilfe: einfache Erklärungen ----------
    public static readonly IReadOnlyDictionary<string, string> HelpTexts = new Dictionary<string, string>
    {
        ["studios"] = "Hier findest du alle Studios. Jedes Studio ist ein Arbeitsplatz für eine Aufgabe – zum Beispiel Briefe schreiben oder Videos schneiden. Was noch nicht fertig ist, steht als „Noch nicht verfügbar“ da.",
        ["projects"] = "Ein Projekt ist ein Ordner für eine Sache, an der du arbeitest. Lege ein Projekt an, und Trendo hält alles dazu beisammen.",
        ["approvals"] = "Manche Aktionen brauchen deine Erlaubnis, zum Beispiel ein Programm installieren. Hier siehst du, was auf deine Entscheidung wartet.",
        ["system"] = "Hier siehst du, ob alles in Ordnung ist: Systemcheck, Sicherungen und was Trendo zuletzt gemacht hat.",
        ["ki"] = "Die Trendo KI läuft direkt auf deinem PC. Nichts geht ins Internet. Ein größeres Modell schreibt besser, braucht aber mehr Speicher.",
        ["settings"] = "Stell Trendo so ein, wie es dir gefällt: Aussehen, Farbe, Hintergrund und was auf dem Startbildschirm zu sehen ist.",
        ["components"] = "Zusatzprogramme, die Trendo für einige Studios braucht. Trendo installiert sie nach deiner Zustimmung – ohne Admin, wo das geht.",
        ["systemcheck"] = "Grün heißt: funktioniert. Rot heißt: fehlt oder kaputt – ein Klick auf „Reparieren“ behebt das meistens.",
        ["sicherung"] = "Trendo sichert deine Daten regelmäßig. Ging etwas schief, holst du hier einen früheren Stand zurück.",
        ["apps"] = "Alle Programme an einem Ort. Was da ist, startet mit einem Klick. Oben wechselst du zu Werkzeugen, Medien, Dokumenten, Briefen und Downloads.",
        ["werkzeuge"] = "Kleine Helfer für jeden Tag: Notizen, ein Rechner und ein Kurzzeitwecker. Alles bleibt auf deinem PC.",
        ["medien"] = "Deine Musik und Videos aus den Ordnern Musik, Videos und deinem Projektordner – neueste zuerst.",
        ["ressourcen"] = "Hier siehst du, was gerade Arbeitsspeicher belegt. Die KI kannst du mit einem Klick aus dem Speicher nehmen.",
        ["office"] = "Deine Dokumente an einem Ort: neue Briefe aus DIN-Vorlagen, zuletzt bearbeitete Dateien öffnen und als PDF speichern.",
        ["brief"] = "Schreib einen Brief in vier Schritten: Art wählen, Absender und Empfänger, Text, prüfen. Trendo speichert ihn als Word-Datei im Ordner Briefe – und auf Wunsch als PDF.",
        ["alltag-dateien"] = "Dateien umbenennen, Doppelte finden, Speicherplatz ansehen, ZIP packen und Fotos nach Datum ordnen. Der Papierkorb zeigt, was gelöscht wurde.",
        ["alltag-bilder"] = "Bilder drehen oder verkleinern, ein Bildschirmfoto machen und Stellen schwärzen. Die Mediathek spielt Musik und Videos von diesem PC.",
        ["alltag-zeit"] = "Uhr, Wetter, Fokuszeit, Stoppuhr, Erinnerungen und Termine. Das Wetter wird nur geladen, wenn du auf „Wetter laden“ klickst.",
        ["alltag-helfer"] = "Text merken, Einheiten und Geld umrechnen, Farben ablesen. Den Geldkurs trägst du selbst ein – Trendo holt ihn nicht aus dem Internet.",
        ["doku-pdf"] = "PDFs zusammenfügen, Seiten herausholen, Text lesen, Formulare ausfüllen und eine Druckvorschau ansehen. Gedruckt wird erst nach einer zweiten Bestätigung.",
        ["kreativ-medien"] = "Wähle eine Video- oder Audiodatei und was daraus werden soll. Das Original bleibt liegen, das Ergebnis landet im Medienordner.",
        ["netz-laden"] = "Dateien herunterladen, den Download-Ordner sortieren und eine Webseite zum Lesen speichern. Ohne Adresse wird nichts geladen.",
        ["produktion"] = "Ein Ablauf verbindet mehrere Studios. Du siehst zuerst den Plan und gibst ihn frei, bevor Trendo etwas ausführt.",
        ["ki-bibliothek"] = "Kostenlose KI nach Arten: Chat, Bilder, Videos, Sprache, Vorlesen und mehr. Laden mit einem Klick, ausprobieren direkt hier.",
        ["ki-quellen"] = "Trendo sucht kostenlose KI auf diesem PC. Die Reihenfolge bestimmst du. Ein Modell wird nur geladen, wenn du zustimmst.",
        ["downloads"] = "Gib eine Adresse oder einen Suchbegriff ein und wähle den Browser. Darunter siehst du deine neuesten Downloads – Programmdateien sind markiert.",
        ["akten"] = "Eine Akte hält alles zu einer Sache zusammen: Fakten, Ablauf, Anlagen, Fristen, Quellen und Eingangspost. Lege für jede Angelegenheit eine an.",
        ["akte"] = "Trag hier ein, was belegt ist, was du nur vermutest und was offen ist. Trendo fragt nach, statt etwas zu erfinden. Fristen gelten erst, wenn du sie mit Quelle bestätigst.",
        ["ausgabe"] = "Prüfen, Ton wählen, als Word/ODT/PDF ausgeben und freigeben. Trendo verschickt nie selbst – du sendest, und trägst danach den Nachweis ein.",
        ["briefkoepfe"] = "Deine Absender: privat und Firma getrennt. In jeder Akte wählst du bewusst, welcher Briefkopf gilt.",
        ["suche"] = "Die Suche findet Seiten, Studios, Projekte und Dateien in deinem Projektordner. Mit Strg+K springst du jederzeit ins Suchfeld.",
        ["fortschritt"] = "So weit ist Trendo OS gebaut. Die Angaben kommen direkt aus der Projektliste – nichts ist geschönt.",
        ["keys"] = "Für einige Online-Dienste brauchst du einen Zugangsschlüssel. Er wird sicher in Windows gespeichert, nicht in einer Datei.",
        ["3d"] = "Baue ein Objekt aus Würfel, Kugel und den anderen Formen. Stell Größe, Lage und Farbe ein, runde Kanten ab oder bohre ein Loch. Die Vorschau siehst du hier, von mehreren Seiten. Speichern legt eine Datei in den 3D-Ordner. Blender öffnet genau dieses Objekt und wird nur installiert, wenn du zustimmst.",
        ["dev"] = "Beschreibe eine Webseite, wähle auf Wunsch Stil und Technik, und Trendo legt einen Ordner mit HTML, CSS und JavaScript an. Die Vorschau zeigt Handy und Desktop. Veröffentlichen geht nur nach deiner Bestätigung und lädt nichts hoch.",
    };

    private void ShowHelp(string key, bool force)
    {
        _hintHost.Children.Clear();
        if (!HelpTexts.TryGetValue(key, out var text)) return;
        if (!force && _settings.SeenHints.Contains(key)) return;
        var ok = new Button { Name = "hint-ok", Content = "Verstanden", FontSize = 11, Background = Accent, VerticalAlignment = VerticalAlignment.Center };
        ok.Click += (_, _) => { if (!_settings.SeenHints.Contains(key)) { _settings.SeenHints.Add(key); SaveSettings(); } _hintHost.Children.Clear(); };
        _hintHost.Children.Add(new Border { Name = "hint", Background = Brush.Parse("#332F86F6"), BorderBrush = Accent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = {
                new TextBlock { Text = "💡", FontSize = 16, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center },
                Pos(new TextBlock { Text = _settings.EasyLanguage ? Trendo.Core.Sys.A11y.Easy(text) : text, Foreground = Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center }, 1), Pos(ok, 2) } } });
    }

    private void SaveSettings() { try { _settings.Save(_core.DataRoot); } catch (Exception ex) { _core.Audit.Append("app", "settings.save_failed", ex.GetType().Name); } }

    /// <summary>Baut das Fenster mit neuen Einstellungen neu auf (Design, Farbe, Anordnung).</summary>
    /// <summary>Für Tests: das neue Hauptfenster nach „Einstellungen übernehmen“.</summary>
    public MainWindow? ReopenedAs { get; private set; }

    private void Reopen(string page)
    {
        SaveSettings();
        _reopenSection = _settingsSection;
        var w = new MainWindow(_core, _opt with { Page = page, FirstRun = false, ScreenshotPath = null, AutoExitSeconds = 0 }, _probe) { WindowState = WindowState, Width = Width, Height = Height };
        w._reopening = true;
        // Offene Fenster bleiben: gleiche Seiten, Lage, Größe und Reihenfolge (aktiv bleibt das, auf dem man war)
        var snap = Windows.Snapshot(); var active = Windows.Active?.Page;
        w.Windows.AreaW = Windows.AreaW; w.Windows.AreaH = Windows.AreaH;
        if (snap.Windows.Count > 0) w.RestoreWindows(snap, active ?? page);
        ReopenedAs = w;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d) d.MainWindow = w;
        w.Show(); _reopening = true; Close();
    }

    // ---------- Einstellungen ----------
    private Control SettingsView()
    {
        var sections = new StackPanel { Spacing = 12 };
        Control Section(string id, string title, Control body) => new StackPanel { Name = "settings-section-" + id, Spacing = 6, IsVisible = _settingsSection == id, Children = { H(title, 15), body } };

        var themes = new WrapPanel();
        foreach (var (id, label) in new[] { ("dunkel", "Dunkel Premium"), ("hell", "Hell"), ("kontrast", "Hoher Kontrast") })
        {
            var on = _settings.Theme == id;
            var b = new Button { Name = "theme-" + id, Content = (on ? "✓ " : "") + label, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(10), Foreground = Text, Background = on ? Accent : Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(on ? 0 : 1) };
            b.Click += (_, _) => { _settings.Theme = id; Reopen("settings"); };
            themes.Children.Add(b);
        }
        sections.Children.Add(Section("aussehen", "Aussehen", themes));

        var colors = new WrapPanel();
        foreach (var (id, label) in AccentChoices)
        {
            var b = new Button { Name = "accent-" + label.ToLowerInvariant(), Width = 34, Height = 34, Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(17), Background = Brush.Parse(id),
                BorderBrush = Brushes.White, BorderThickness = new Thickness(!_settings.AccentFromWallpaper && _settings.Accent == id ? 3 : 0) };
            ToolTip.SetTip(b, label); Avalonia.Automation.AutomationProperties.SetName(b, "Akzentfarbe " + label);
            b.Click += (_, _) => { _settings.Accent = id; _settings.AccentFromWallpaper = false; Reopen("settings"); };
            colors.Children.Add(b);
        }
        var auto = new CheckBox { Name = "accent-auto", Content = "Farbe passend zum Hintergrund wählen", IsChecked = _settings.AccentFromWallpaper, Foreground = Text };
        auto.IsCheckedChanged += (_, _) => { _settings.AccentFromWallpaper = auto.IsChecked == true; Reopen("settings"); };
        sections.Children.Add(Section("farbe", "Akzentfarbe", new StackPanel { Spacing = 6, Children = { colors, auto } }));

        var walls = new WrapPanel();
        foreach (var (id, label) in new[] { ("gold-orbit", "Gold-Orbit"), ("orbit", "Orbit"), ("abendrot", "Abendrot"), ("bergsee-nacht", "Bergsee"), ("polarlicht", "Polarlicht"), ("wellen", "Wellen"), ("morgen", "Morgen") })
        {
            var on = !_settings.WallpaperByTime && _settings.Wallpaper == id;
            var b = new Button { Name = "wall-" + id, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(3), BorderBrush = on ? Accent : Brushes.Transparent, BorderThickness = new Thickness(2), Background = Brushes.Transparent, CornerRadius = new CornerRadius(8),
                Content = new StackPanel { Spacing = 4, Children = { new Image { Source = LoadWallpaper(id), Width = 150, Height = 84, Stretch = Stretch.UniformToFill }, new TextBlock { Text = label, Foreground = Text, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center } } } };
            b.Click += (_, _) => { _settingsSection = "hintergrund"; _settings.Wallpaper = id; _settings.WallpaperByTime = false; SaveSettings(); SetWallpaper(id); if (_settings.AccentFromWallpaper) Reopen("settings"); else Show("settings"); };
            walls.Children.Add(b);
        }
        var byTime = new CheckBox { Name = "wall-bytime", Content = "Hintergrund nach Tageszeit wechseln (Morgen · Wellen · Bergsee · Polarlicht)", IsChecked = _settings.WallpaperByTime, Foreground = Text };
        byTime.IsCheckedChanged += (_, _) => { _settings.WallpaperByTime = byTime.IsChecked == true; SaveSettings(); var w = _settings.CurrentWallpaper(DateTime.Now); SetWallpaper(w); };
        sections.Children.Add(Section("hintergrund", "Hintergrund", new StackPanel { Spacing = 6, Children = { walls, byTime } }));

        var motion = new CheckBox { Name = "reduce-motion", Content = "Bewegung reduzieren (keine Animationen)", IsChecked = _settings.ReduceMotion, Foreground = Text };
        motion.IsCheckedChanged += (_, _) => { _settingsSection = "bedienung"; _settings.ReduceMotion = motion.IsChecked == true; Reopen("settings"); };
        var power = new WrapPanel();
        foreach (var (id, label) in new[] { ("auto", "Automatisch"), ("hoch", "Volle Effekte"), ("sparsam", "Sparsam (Raspberry Pi)") })
        {
            var on = _settings.PowerProfile == id;
            var b = new Button { Name = "power-" + id, Content = (on ? "✓ " : "") + label, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(10), Foreground = Text, Background = on ? Accent : Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(on ? 0 : 1) };
            b.Click += (_, _) => { _settingsSection = "bedienung"; _settings.PowerProfile = id; Reopen("settings"); };
            power.Children.Add(b);
        }
        var upd = new CheckBox { Name = "auto-update", Content = $"Updates automatisch laden ({_updateInfo})", IsChecked = _settings.AutoUpdate, Foreground = Text };
        upd.IsCheckedChanged += (_, _) => { _settings.AutoUpdate = upd.IsChecked == true; SaveSettings(); };
        var sizes = new WrapPanel();
        foreach (var (v, label) in new[] { (1.0, "Normal"), (1.15, "Groß"), (1.3, "Sehr groß") })
        {
            var on = Math.Abs(_settings.UiScale - v) < 0.01;
            var b = new Button { Name = "size-" + (int)(v * 100), Content = (on ? "✓ " : "") + label, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(10), Foreground = Text, Background = on ? Accent : Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(on ? 0 : 1) };
            b.Click += (_, _) => { _settingsSection = "bedienung"; _settings.UiScale = v; SaveSettings(); Rescale?.Invoke(); Show("settings"); };
            sizes.Children.Add(b);
        }
        sections.Children.Add(Section("bedienung", "Bedienung", new StackPanel { Spacing = 4, Children = { motion, upd, P("Anzeigegröße (Schrift und Bedienelemente):", Text), sizes,
            P(LowPower ? "Leistung: sparsam – ohne Unschärfe, Schatten und Animationen." : "Leistung (Sparsam: ohne Unschärfe, Schatten und Animationen):", Text), power,
            P("Tastatur: Strg+K Suche · F1 Hilfe zur Seite · Strg+1 bis 5 Hauptbereiche · Tab/Enter bedienen alles ohne Maus", null, 11) } }));

        var desk = new StackPanel { Spacing = 4 };
        foreach (var (id, label) in WidgetChoices)
        {
            var cb = new CheckBox { Name = "show-" + id, Content = label, IsChecked = !_settings.HiddenTiles.Contains(id), Foreground = Text };
            cb.IsCheckedChanged += (_, _) => { if (cb.IsChecked == true) _settings.HiddenTiles.Remove(id); else if (!_settings.HiddenTiles.Contains(id)) _settings.HiddenTiles.Add(id); SaveSettings(); };
            desk.Children.Add(cb);
        }
        desk.Children.Add(P("Reihenfolge der Studio-Kacheln:", Text));
        desk.Children.Add(OrderList("tile", FeaturedStudios.Select(f => (f.Id, StudioCatalog.NameOf(f.Id, f.Title))).ToList(), _settings.TileOrder, l => _settings.TileOrder = l));
        desk.Children.Add(P("Reihenfolge im Dock:", Text));
        desk.Children.Add(OrderList("dock", DockIds, _settings.DockOrder, l => _settings.DockOrder = l));
        var apply = new Button { Name = "btn-apply-desktop", Content = "Startbildschirm übernehmen", Background = Accent, CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 6) };
        apply.Click += (_, _) => Reopen("start");
        desk.Children.Add(apply);
        sections.Children.Add(Section("start", "Startbildschirm anpassen", desk));

        sections.Children.Add(Section("mehr", "Mehr", new WrapPanel { Children = { Nav2("Zusatzprogramme", "components"), Nav2("Online-Dienste", "keys"), Nav2("Ersteinrichtung", "einrichtung"), Nav2("Systemcheck", "systemcheck") } }));
        sections.Children.Add(new TextBlock { Text = $"Version {CrashLog.AppVersion()} · Trendo-Logo: eigener Entwurf · Hintergründe: KI-erstellt für Trendo OS", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, IsVisible = _settingsSection == "mehr" });

        var nav = new StackPanel { Spacing = 4 };
        void NavItem(string id, string label, string icon)
        {
            var on = _settingsSection == id;
            var b = new Button { Name = "settings-nav-" + id, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 8), CornerRadius = new CornerRadius(10), Foreground = Text, Background = on ? Accent : Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(on ? 0 : 1),
                Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Ico(icon, 16, Text), new TextBlock { Text = label, Foreground = Text, VerticalAlignment = VerticalAlignment.Center } } } };
            b.Click += (_, _) => { _settingsSection = id; Show("settings"); };
            nav.Children.Add(b);
        }
        NavItem("aussehen", "Aussehen", Mdi.Palette);
        NavItem("farbe", "Farbe", Mdi.StarFourPoints);
        NavItem("hintergrund", "Hintergrund", Mdi.Image);
        NavItem("bedienung", "Bedienung", Mdi.Tune);
        NavItem("start", "Startbildschirm", Mdi.Monitor);
        NavItem("mehr", "Weiteres", Mdi.Cog);
        var navBox = new Border { Background = Brush.Parse(C("#55101830", "#CCFFFFFF")), BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(8), Child = nav };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("200,*"), ColumnSpacing = 14 };
        grid.Children.Add(navBox); Grid.SetColumn(sections, 1); grid.Children.Add(sections);
        return Page("Einstellungen", grid);
    }

    private static readonly List<(string Id, string Label)> DockIds =
        [("start", "Start"), ("files", "Dateien"), ("browser", "Browser"), ("mail", "E-Mail"), ("ki", "Trendo KI"), ("media", "Medien"), ("settings", "Einstellungen"),
         ("blender", "Blender"), ("unreal", "Unreal"), ("components", "Zusatzprogramme"), ("code", "Web & App"), ("office", "Office"), ("security", "Sicherheit")];

    private Control OrderList(string prefix, List<(string Id, string Label)> items, List<string> order, Action<List<string>> set)
    {
        var cur = UserSettings.Ordered(items, x => x.Id, order).Select(x => x.Id).ToList();
        var wrap = new WrapPanel();
        foreach (var id in cur)
        {
            var label = items.First(x => x.Id == id).Label;
            Button Arrow(string t, int d) { var b = new Button { Name = $"{prefix}-{id}-{(d < 0 ? "vor" : "zurueck")}", Content = t, Padding = new Thickness(5, 0), FontSize = 11 }; b.Click += (_, _) => { set(UserSettings.Move(cur, id, d)); SaveSettings(); Show("settings"); }; return b; }
            wrap.Children.Add(new Border { Background = Brush.Parse("#22FFFFFF"), CornerRadius = new CornerRadius(8), Padding = new Thickness(6, 3), Margin = new Thickness(0, 0, 6, 6),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { Arrow("◀", -1), new TextBlock { Text = label, Foreground = Text, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center }, Arrow("▶", 1) } } });
        }
        return wrap;
    }
}
