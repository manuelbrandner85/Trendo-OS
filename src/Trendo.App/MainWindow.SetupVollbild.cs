using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Ersteinrichtung im Vollbild (Wunsch 04.11.2026: „wie bei Windows“). Vor den bekannten Schritten kommt ein Begrüßungsschritt mit
/// „Claude und Claude Code einrichten“ (freiwillig) und der Wahl, wie Trendo OS genutzt wird: als Programm unter Windows, auf einem USB-Stick
/// oder auf einer externen SSD. Die Laufwerke werden echt erkannt (mit Größe, zu kleine gesperrt); das Schreiben des Trendo-OS-Abbilds schaltet
/// sich frei, sobald das Abbild im Release liegt – bis dahin sagt der Schritt das ehrlich.</summary>
public partial class MainWindow
{
    readonly Grid _wizLayer = new() { Name = "wiz-fullscreen", IsVisible = false, ZIndex = 4800 };
    bool _wizFull;
    string _wizUse = "programm";
    /// <summary>Tests: Laufwerke ersetzen (Name, Bezeichnung, Größe in GB, Wechseldatenträger?).</summary>
    public Func<IReadOnlyList<(string Name, string Label, double Gb, bool Removable)>>? DrivesOverride { get; set; }
    public const double MinStickGb = 32;
    public bool SetupFullscreen => _wizFull;
    /// <summary>Nutzungsart wählen (Bildschirmfotos, Tests).</summary>
    public void SetupUse(string id) { _wizUse = id; if (_wizFull) Show("einrichtung-start"); }

    /// <summary>Beim ersten Start: Einrichtung im Vollbild beginnen (zuerst Begrüßung mit Claude und Nutzungsart).</summary>
    public void EnterSetupFullscreen()
    {
        _wizFull = true;
        if (!_opt.Isolated) WindowState = WindowState.FullScreen;
        Show("einrichtung-start");
        _ = CheckImageAsync(); // liegt das Trendo-OS-Abbild im neuesten Release?
    }

    void LeaveSetupFullscreen()
    {
        _wizLayer.IsVisible = false; _wizLayer.Children.Clear();
        if (!_wizFull) return;
        _wizFull = false;
        if (!_opt.Isolated) WindowState = WindowState.Maximized;
    }

    /// <summary>Seiten der Einrichtung im Vollbild statt im Fenster zeigen. true = erledigt.</summary>
    bool PresentSetupFullscreen(string key)
    {
        if (!_wizFull || !(key == "einrichtung" || key.StartsWith("einrichtung"))) return false;
        _wizLayer.Children.Clear();
        _wizLayer.Background = new RadialGradientBrush
        {
            Center = new RelativePoint(0.5, 0.25, RelativeUnit.Relative), GradientOrigin = new RelativePoint(0.5, 0.25, RelativeUnit.Relative), RadiusX = new RelativeScalar(0.9, RelativeUnit.Relative), RadiusY = new RelativeScalar(0.9, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#2A2110"), 0), new GradientStop(Color.Parse("#0B0F1A"), 0.55), new GradientStop(Color.Parse("#05070D"), 1) },
        };
        var card = new Border
        {
            MaxWidth = 860, Margin = new Thickness(24), Padding = new Thickness(40, 32), CornerRadius = new CornerRadius(22), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Background = Brush.Parse("#CC10141F"), BorderBrush = Brush.Parse("#55D4A84B"), BorderThickness = new Thickness(1), BoxShadow = BoxShadows.Parse("0 30 80 0 #CC000000, 0 0 0 1 #22F5C451"),
            Child = new ScrollViewer { Content = _showMap.TryGetValue(key, out var view) ? view() : SafeView(key), MaxHeight = Math.Max(500, Bounds.Height - 120) }, // ohne Studio-Eingabezeile
        };
        _wizLayer.Children.Add(card);
        Grid.SetRowSpan(_wizLayer, 99); Grid.SetColumnSpan(_wizLayer, 99); // ganze Fläche, auch über Taskleiste und Desktop
        _wizLayer.IsVisible = true;
        return true;
    }

    IReadOnlyList<(string Name, string Label, double Gb, bool Removable)> Drives()
    {
        if (DrivesOverride is { } o) return o();
        var sys = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        var res = new List<(string, string, double, bool)>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady || string.Equals(d.Name, sys, StringComparison.OrdinalIgnoreCase)) continue; // Windows-Laufwerk erscheint nie
                if (d.DriveType is not (DriveType.Removable or DriveType.Fixed)) continue;
                res.Add((d.Name.TrimEnd('\\'), d.VolumeLabel, d.TotalSize / 1e9, d.DriveType == DriveType.Removable));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return res;
    }

    /// <summary>Gibt es das Trendo-OS-Abbild schon im Release? (Bis dahin ist „Auf Stick/SSD schreiben“ gesperrt und sagt das.)</summary>
    public Func<bool>? ImageReadyOverride { get; set; }
    bool ImageReady => ImageReadyOverride?.Invoke() ?? _imgManifest is not null;

    Control SetupStartView()
    {
        var sp = new StackPanel { Name = "wiz-start", Spacing = 14 };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Children = {
            new Image { Source = TrendoLogo.Emblem(), Width = 56, Height = 56 },
            new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = {
                new TextBlock { Text = "Willkommen bei Trendo OS", FontSize = 28, FontWeight = FontWeight.SemiBold, Foreground = GoldText() },
                new TextBlock { Text = "In ein paar Schritten ist alles eingerichtet.", Foreground = Muted, FontSize = 14 } } } } });

        // 1) Claude – freiwillig, gleich am Anfang
        var claude = new StackPanel { Spacing = 6 };
        claude.Children.Add(H("Claude und Claude Code (freiwillig)", 16));
        claude.Children.Add(P("Mit Claude arbeitet Trendo besonders klug: Texte, Programme, Ideen. Du meldest dich mit deinem eigenen Claude-Konto an – Trendo speichert keine Zugangsdaten. Ohne Claude arbeitet Trendo mit den freien KIs auf deinem PC.", Text, 13));
        var cs = _claude;
        claude.Children.Add(new WrapPanel { Children = {
            cs.Installed ? (Control)new TextBlock { Name = "wiz-claude-ok", Text = "✓ Claude Code ist eingerichtet", Foreground = Brush.Parse("#4ADE80"), FontSize = 13, VerticalAlignment = VerticalAlignment.Center }
                : Act("wiz-claude", "Claude einrichten", () => _ = ClaudeInstall(), true),
            new Border { Width = 8 },
            new TextBlock { Text = "– oder einfach überspringen", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } });
        sp.Children.Add(new Border { Background = Brush.Parse("#14FFFFFF"), CornerRadius = new CornerRadius(14), Padding = new Thickness(16, 12), Child = claude });

        // 2) Wie nutzen?
        sp.Children.Add(H("Wie möchtest du Trendo OS nutzen?", 16));
        Control Option(string id, string title, string text, string icon)
        {
            var on = _wizUse == id;
            var b = new Button { Name = "wiz-use-" + id, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(16, 12), CornerRadius = new CornerRadius(14),
                Background = on ? Brush.Parse("#33D4A84B") : Brush.Parse("#10FFFFFF"), BorderBrush = Brush.Parse(on ? "#D4A84B" : "#22FFFFFF"), BorderThickness = new Thickness(on ? 2 : 1),
                Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = {
                    new TextBlock { Text = icon, FontSize = 24, VerticalAlignment = VerticalAlignment.Center },
                    new StackPanel { Children = { new TextBlock { Text = title, Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold }, new TextBlock { Text = text, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap } } } } } };
            b.Click += (_, _) => { _wizUse = id; Show("einrichtung-start"); };
            return b;
        }
        sp.Children.Add(Option("programm", "Als Programm unter Windows", "So wie jetzt – Trendo startet wie jede App. Am einfachsten.", "🖥"));
        sp.Children.Add(Option("stick", "Auf einem USB-Stick", $"Eigenes System zum Mitnehmen und Ausprobieren. Mindestens {MinStickGb:0} GB, empfohlen 64 GB oder mehr.", "🔌"));
        sp.Children.Add(Option("ssd", "Auf einer externen SSD (empfohlen für den Alltag)", "Fast so schnell wie im PC, viel Platz für KI-Modelle, Windows bleibt unberührt.", "💽"));

        if (_wizUse is "stick" or "ssd")
        {
            var drives = Drives().Where(d => _wizUse == "stick" ? d.Removable : !d.Removable).ToList();
            var box = new StackPanel { Name = "wiz-drives", Spacing = 6 };
            if (drives.Count == 0)
                box.Children.Add(P(_wizUse == "stick" ? "Bitte jetzt einen USB-Stick einstecken – er erscheint hier sofort." : "Bitte jetzt die externe SSD anschließen – sie erscheint hier sofort.", Text, 13));
            foreach (var d in drives)
            {
                var ok = d.Gb >= MinStickGb;
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
                row.Children.Add(new TextBlock { Name = "wiz-drive-" + d.Name.TrimEnd(':'), Foreground = ok ? Text : Muted, FontSize = 13, VerticalAlignment = VerticalAlignment.Center,
                    Text = $"{(d.Label.Length > 0 ? d.Label : "Laufwerk")} ({d.Name}) · {d.Gb:0} GB · " + (ok ? "passt" : $"zu klein – mindestens {MinStickGb:0} GB") });
                if (ok && ImageReady)
                {
                    var name = d.Name; var on = _wizDrive == name;
                    var pick = new Button { Name = "wiz-pick-" + name.TrimEnd(':'), Content = on ? "✓ gewählt" : "Dieses nehmen", Padding = new Thickness(10, 3), FontSize = 12,
                        Background = on ? Brush.Parse("#33D4A84B") : Brush.Parse("#14FFFFFF"), BorderBrush = Brush.Parse(on ? "#D4A84B" : "#22FFFFFF"), BorderThickness = new Thickness(1) };
                    pick.Click += (_, _) => SetupPickDrive(name);
                    row.Children.Add(pick);
                }
                box.Children.Add(row);
            }
            if (_wizDrive is { } wd && !drives.Any(x => x.Name == wd && x.Gb >= MinStickGb)) _wizDrive = null; // abgezogen oder anderer Typ
            box.Children.Add(P(ImageReady ? $"Trendo OS {_imgManifest?.Version} liegt bereit. Laufwerk wählen, dann Weiter: du bestätigst das Löschen, Trendo schreibt und prüft alles. Auf diesem PC wird dabei nichts installiert."
                : _core.Offline ? "Ohne Internet kann Trendo das Abbild nicht laden. Bitte mit dem Internet verbinden – oder oben „Als Programm unter Windows“ wählen."
                : "Trendo sucht das Abbild im neuesten Release … Ist es nicht da, bitte später erneut versuchen – oder oben „Als Programm unter Windows“ wählen.", ImageReady ? Text : Brush.Parse("#FACC15"), 12));
            if (ImageReady && _wizDrive is null && drives.Any(x => x.Gb >= MinStickGb)) box.Children.Add(P("Bitte oben das Laufwerk wählen („Dieses nehmen“).", Brush.Parse("#FACC15"), 12));
            sp.Children.Add(new Border { Background = Brush.Parse("#10FFFFFF"), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10), Child = box });
        }

        var go = new Button { Name = "wiz-start-next", Content = "Weiter", Background = Accent, Padding = new Thickness(22, 8), FontSize = 15, HorizontalAlignment = HorizontalAlignment.Right,
            IsEnabled = _wizUse == "programm" || (ImageReady && _wizDrive is not null) }; // Stick/SSD: nie still auf den PC ausweichen
        go.Click += (_, _) =>
        {
            _core.Audit.Append(Principal.LocalOwner.Name, "setup.start", _wizUse);
            if (_wizUse is "stick" or "ssd") { if (ImageReady && _wizDrive is not null) { _imgOk = null; _imgStatus = ""; Show("einrichtung-abbild"); } }
            else if (_settings.SetupDone) { LeaveSetupFullscreen(); Show("modus"); } // schon eingerichtet (aus „Betriebsmodus“ geöffnet): nichts neu einrichten
            else Show("einrichtung");
        };
        sp.Children.Add(go);
        return sp;
    }
}
