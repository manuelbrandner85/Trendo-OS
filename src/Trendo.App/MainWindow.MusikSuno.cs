using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SkiaSharp;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Trendo Musik wie Suno (V1/V2/V3, freigegeben 07.11.2026 „ja alles zu suno“, Clean-Room – Anordnung und Abläufe, nicht Sunos Aussehen):
/// V1 schmale Navigation links (Erstellen, Studio, Bibliothek, Stimmen), Abspielleiste unten über die ganze Breite;
/// V2 Erstellen-Spalte mit Modellwahl (Schnell/Gründlich/Max), „+ Audio“, „+ Stimme“, „+ Bild“ und Liedideen-Würfel;
/// V3 „Mehr Optionen“ wie Suno – nur Regler, deren Wirkung gemessen ist (siehe reports/musik-suno).</summary>
public partial class MainWindow
{
    string _msPage = "erstellen";
    string _msModel = "schnell";
    MusicSuno.Options _msOpt = new();

    /// <summary>Seite in Trendo Musik wählen (erstellen, studio, bibliothek, stimmen) – öffentlich für Tests und Vorschaubilder.</summary>
    public void MsPage(string page)
    {
        _msPage = page;
        if (page == "erstellen") { _msBeat = false; _msNoten = false; }
        if (CurrentView == MS) Show(MS);
    }
    public string MsCurrentPage => _msPage;
    public MusicSuno.Options MsOptions { get => _msOpt; set => _msOpt = value; }
    public string MsModelChoice { get => _msModel; set => _msModel = value; }

    static readonly (string Id, string Icon, string Label)[] MsPages = [("erstellen", "erstellen", "Erstellen"), ("studio", "regler", "Studio"), ("bibliothek", "bibliothek", "Bibliothek"), ("stimmen", "mikrofon", "Stimmen")]; // G7: Linien-Symbole

    static Control WithCenter(Control c) { c.HorizontalAlignment = HorizontalAlignment.Center; return c; }

    /// <summary>V1: schmale Navigation links (wie Suno: Symbole mit Namen untereinander).</summary>
    Control MsNav()
    {
        var sp = new StackPanel { Name = "ms-nav", Spacing = 4 };
        foreach (var (id, icon, label) in MsPages)
        {
            var on = id == _msPage; var p = id;
            var b = new Button { Name = "ms-nav-" + id, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, Padding = new Thickness(4, 8),
                CornerRadius = new CornerRadius(12), Background = on ? Brush.Parse("#33C9A227") : Brushes.Transparent, BorderBrush = on ? Accent : Brushes.Transparent, BorderThickness = new Thickness(1),
                Content = new StackPanel { Spacing = 3, Children = { WithCenter(TrendoIcons.Icon(icon, 22, on ? Accent : Text)),
                    new TextBlock { Text = label, FontSize = 10.5, Foreground = on ? Text : Muted, HorizontalAlignment = HorizontalAlignment.Center } } } };
            b.Click += (_, _) => MsPage(p);
            sp.Children.Add(b);
        }
        return MsCard("ms-nav-card", sp);
    }

    /// <summary>V1: Seiteninhalt (Spalten 2–6 des Rasters). Erstellen: Erstellen | Lieder | Lied. Studio: Werkzeuge | Arrangement/Noten | Lied.
    /// Bibliothek: Lieder (breit) | Lied. Stimmen: Stimmen | Lied.</summary>
    void MsPageColumns(Grid root)
    {
        Control At(Control c, int col, int span = 1) { Grid.SetRow(c, 0); Grid.SetColumn(c, col); Grid.SetColumnSpan(c, span); return c; }
        if (_msLyricsFull && _msPage == "erstellen") { root.Children.Add(At(MsLyricsFullEditor(), 2, 5)); return; } // V6: Liedtext im Vollbild
        switch (_msPage)
        {
            case "studio":
                root.Children.Add(At(MsStudioTools(), 2));
                root.Children.Add(At(_msNoten ? MsNotenPanel() : MsArrangePanel() is { } arr ? MsCard("ms-studio-arrange", arr) : MsCard("ms-studio-empty",
                    new StackPanel { Spacing = 8, Children = { H("Studio", 16), P("Wähle in der Bibliothek ein Lied – hier erscheint es als Arrangement mit Abschnitten, Text und Spuren.", Muted, 12.5) } }), 4));
                root.Children.Add(At(MsDetailPanel(), 6));
                break;
            case "bibliothek":
                root.Children.Add(At(MsCollectionsPanel(), 2));
                root.Children.Add(At(MsLibraryPanel(), 4));
                root.Children.Add(At(MsDetailPanel(), 6));
                break;
            case "stimmen":
                root.Children.Add(At(MsVoicesPage(), 2, 3));
                root.Children.Add(At(MsDetailPanel(), 6));
                break;
            default:
                root.Children.Add(At(MsCreatePanel(), 2));
                root.Children.Add(At(MsLibraryPanel(), 4));
                root.Children.Add(At(MsDetailPanel(), 6));
                break;
        }
    }

    /// <summary>Studio-Werkzeuge links: Arrangement, Beat (Step-Sequencer), Noten (Piano-Roll); Mischpult.</summary>
    Control MsStudioTools()
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(H("Studio", 16));
        var tabs = new WrapPanel();
        Button Tab(string name, string label, bool on, Action a)
        {
            var b = new Button { Name = name, Content = label, Padding = new Thickness(14, 5), Margin = new Thickness(0, 0, 4, 4), CornerRadius = new CornerRadius(14), Background = on ? Accent : Brush.Parse("#22FFFFFF"), Foreground = on ? Brushes.White : Text, BorderThickness = new Thickness(0) };
            b.Click += (_, _) => { a(); Show(MS); };
            return b;
        }
        tabs.Children.Add(Tab("ms-studio-tab-arrange", "Arrangement", !_msBeat && !_msNoten, () => { _msBeat = false; _msNoten = false; }));
        tabs.Children.Add(Tab("ms-mode-beat", "Beat", _msBeat, () => { _msBeat = true; _msNoten = false; }));
        tabs.Children.Add(Tab("ms-mode-noten", "Noten", _msNoten, () => { _msNoten = true; _msBeat = false; }));
        sp.Children.Add(tabs);
        if (_msMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "ms-msg", Text = _msMsg, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        if (_msBeat) sp.Children.Add(MsBeatPanel());
        else if (!_msNoten) sp.Children.Add(P("Das gewählte Lied als Arrangement: Abschnitte, Text, getrennte Spuren. „Im Mischpult öffnen“ legt die Spuren als Mehrspur-Projekt an – mit Effekten, Bussen, Automation und Plugins.", Muted, 12));
        return MsCard("ms-studio-tools", new ScrollViewer { Content = sp });
    }

    /// <summary>Seite „Stimmen“: eigene Stimme (Analyse, Mikrofon) und die Stimmen-Bibliothek – Gesang in einer Stimme für neue Lieder wählen.</summary>
    Control MsVoicesPage()
    {
        var sp = new StackPanel { Spacing = 10 };
        sp.Children.Add(H("Stimmen", 16));
        sp.Children.Add(P("Eigene Stimme und Stimmen mit Einwilligung – bekannte Künstlerinnen und Künstler sind gesperrt. Die gewählte Stimme singt deine neuen Lieder.", Muted, 12.5));
        sp.Children.Add(MsSingVoiceBox());
        sp.Children.Add(MsVoicePanel(() => { }));
        var voices = Bank.All();
        sp.Children.Add(new TextBlock { Text = $"STIMMEN-BIBLIOTHEK ({voices.Count + (OwnVoiceReady ? 1 : 0)})", Foreground = Muted, FontSize = 10, FontWeight = FontWeight.Bold });
        if (OwnVoiceReady) sp.Children.Add(P("• Meine Stimme (Mikrofon) – gelernt", Text, 12));
        foreach (var v in voices) sp.Children.Add(P($"• {v.Name} – {(v.Ready ? "gelernt" : "noch nicht gelernt")} · {(v.Consent.Kind == "eigen" ? "eigene Stimme" : "Einwilligung: " + v.Consent.Person)}", Text, 12));
        if (voices.Count == 0 && !OwnVoiceReady) sp.Children.Add(P("Noch keine Stimme – im Lied unter „Spuren & Mitsingen“ aus einem Gesang lernen oder oben deine Stimme aufnehmen.", Muted, 12));
        sp.Children.Add(MsPersonasPanel()); // V11: Personas
        return MsCard("ms-voices-page", new ScrollViewer { Content = sp });
    }

    /// <summary>V2: Kopf der Erstellen-Spalte – Modellwahl rechts oben wie bei Suno.</summary>
    Control MsCreateHeader()
    {
        var model = new ComboBox { Name = "ms-model", FontSize = 12, ItemsSource = new[] { "Schnell", "Gründlich", "Max" }, SelectedIndex = _msModel switch { "gruendlich" => 1, "max" => 2, _ => 0 } };
        ToolTip.SetTip(model, "Schnell: Turbo-Modell (Sekunden). Gründlich: Grundmodell, sorgfältiger. Max: Grundmodell mit vielen Rechenschritten – dauert am längsten.");
        model.SelectionChanged += (_, _) => _msModel = model.SelectedIndex switch { 1 => "gruendlich", 2 => "max", _ => "schnell" };
        return new DockPanel { Children = { Dk(model, Avalonia.Controls.Dock.Right), H("Erstellen", 16) } };
    }

    /// <summary>Modell und Zusatz-Einstellungen für den Auftrag (Max = Grundmodell mit 64 Schritten).</summary>
    (string Model, bool Max) MsModelFor() => _msModel switch { "gruendlich" => (AceStepClient.BaseModel, false), "max" => (AceStepClient.BaseModel, true), _ => (AceStepClient.TurboModel, false) };

    /// <summary>V2: „+ Audio“, „+ Stimme“, „+ Bild“ und der Liedideen-Würfel.</summary>
    Control MsReferenceRow(Action sync)
    {
        var row = new WrapPanel { Name = "ms-refs" };
        Button B(string name, string label, string tip, Func<Task> a)
        {
            var b = new Button { Name = name, Content = label, FontSize = 12, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(14), Background = Brush.Parse("#1FFFFFFF"), BorderThickness = new Thickness(0) };
            ToolTip.SetTip(b, tip); b.Click += async (_, _) => { sync(); await a(); }; return b;
        }
        row.Children.Add(B("ms-ref-audio", _msRefFile is null ? "+ Audio" : "✓ Audio: " + Path.GetFileNameWithoutExtension(_msRefFile), "Klangvorbild: eine Aufnahme oder ein eigenes Lied – der neue Song klingt ähnlich.", async () =>
        {
            var f = await PickMedia("ms-ref-audio", ["*.mp3", "*.wav", "*.flac", "*.ogg", "*.m4a"]);
            if (f is not null) { _msRefFile = f; _msMsg = "Klangvorbild: " + Path.GetFileName(f); }
            Show(MS);
        }));
        var voiceLabel = MsVoiceChoices().FirstOrDefault(c => c.Id == MsSingVoice).Label;
        row.Children.Add(B("ms-ref-voice", MsSingVoice.Length == 0 ? "+ Stimme" : "✓ " + voiceLabel, "In welcher Stimme gesungen wird – eigene Stimme oder eine Stimme mit Einwilligung.", () => { MsPage("stimmen"); return Task.CompletedTask; }));
        row.Children.Add(MsPersonaChip(B)); // V11
        row.Children.Add(B("ms-ref-image", "+ Bild", "Ein Foto bestimmt die Stimmung: Farben, Helligkeit und Lebhaftigkeit werden zu Stil-Wörtern.", async () =>
        {
            var f = await PickMedia("ms-ref-image", ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"]);
            if (f is null) return;
            try { MsApplyImageMood(f); }
            catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException) { _msMsg = "Das Bild ließ sich nicht lesen: " + e.Message; }
            Show(MS);
        }));
        row.Children.Add(B("ms-dice", "Idee", "Liedidee würfeln", () => { var idea = MusicSuno.Idea(Random.Shared.Next()); if (_msPro) _msStyle = idea; else _msDesc = idea; Show(MS); return Task.CompletedTask; }));
        return row;
    }

    /// <summary>Bildwerte aus einem Foto (auf 64 × 64 verkleinert) – Farbton, Sättigung, Helligkeit, Kontrast.</summary>
    public static MusicSuno.ImageStats ImageStatsOf(string file)
    {
        using var src = SKBitmap.Decode(file) ?? throw new InvalidDataException("Kein lesbares Bild.");
        using var small = src.Resize(new SKImageInfo(64, 64), new SKSamplingOptions(SKFilterMode.Linear)) ?? throw new InvalidDataException("Bild nicht verkleinerbar.");
        double sx = 0, sy = 0, sat = 0, light = 0, l2 = 0; var n = 0;
        for (var y = 0; y < small.Height; y++)
            for (var x = 0; x < small.Width; x++)
            {
                var c = small.GetPixel(x, y); c.ToHsl(out var h, out var s, out var l);
                var w = s / 100.0; sx += Math.Cos(h * Math.PI / 180) * w; sy += Math.Sin(h * Math.PI / 180) * w; // Farbton als Kreismittel, gewichtet mit der Sättigung
                sat += s / 100.0; light += l / 100.0; l2 += l / 100.0 * (l / 100.0); n++;
            }
        var hue = (Math.Atan2(sy, sx) * 180 / Math.PI + 360) % 360; var mean = light / n;
        return new(hue, sat / n, mean, Math.Sqrt(Math.Max(0, l2 / n - mean * mean)));
    }

    /// <summary>V3: Regler, deren Wirkung gemessen ist (reports/musik-suno/v3-regler-messung.md). Nicht gemessene oder unwirksame erscheinen nicht.</summary>
    public static readonly HashSet<string> MsMeasuredSliders = ["variety"]; // V3-Messung 07.11.2026 (reports/musik-suno/v3-regler-messung.md): nur Vielfalt wirkt nachweisbar

    /// <summary>V3: „Mehr Optionen“ wie Suno – Stile ausschließen, Stimme, Dauer genau, Regler.</summary>
    Control MsAdvancedOptions(Action sync)
    {
        var sp = new StackPanel { Name = "ms-advanced", Spacing = 6 };
        sp.Children.Add(H("Feinsteuerung", 13));
        var ex = new TextBox { Name = "ms-exclude", Text = _msOpt.Exclude, Watermark = "Stile ausschließen, z. B. Schlagzeug, Autotune, Rap", FontSize = 12 };
        ex.TextChanged += (_, _) => _msOpt = _msOpt with { Exclude = ex.Text ?? "" };
        sp.Children.Add(ex);
        var gender = new ComboBox { Name = "ms-gender", FontSize = 12, ItemsSource = new[] { "Stimme: automatisch", "Stimme: weiblich", "Stimme: männlich" }, SelectedIndex = (int)_msOpt.VoiceGender };
        gender.SelectionChanged += (_, _) => _msOpt = _msOpt with { VoiceGender = (MusicSuno.Voice)Math.Max(0, gender.SelectedIndex) };
        var exact = new CheckBox { Name = "ms-dur-exact", Content = "Dauer genau", IsChecked = _msOpt.Seconds is not null, Foreground = Text, FontSize = 12 };
        var durLabel = new TextBlock { Name = "ms-dur-label", Text = DurText(_msOpt.Seconds ?? 180), Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Width = 54 };
        var dur = new Slider { Name = "ms-dur", Minimum = 10, Maximum = 600, Value = _msOpt.Seconds ?? 180, Width = 150, IsEnabled = _msOpt.Seconds is not null, SmallChange = 5, LargeChange = 30 };
        dur.ValueChanged += (_, _) => { var v = (int)(Math.Round(dur.Value / 5) * 5); durLabel.Text = DurText(v); if (exact.IsChecked == true) _msOpt = _msOpt with { Seconds = v }; };
        exact.IsCheckedChanged += (_, _) => { dur.IsEnabled = exact.IsChecked == true; _msOpt = _msOpt with { Seconds = exact.IsChecked == true ? (int)(Math.Round(dur.Value / 5) * 5) : null }; };
        sp.Children.Add(gender);
        sp.Children.Add(new WrapPanel { Children = { exact, dur, durLabel } });
        Control Knob(string id, string label, string tip, int value, Func<MusicSuno.Options, int, MusicSuno.Options> set)
        {
            var v = new TextBlock { Text = value.ToString(), Foreground = Muted, FontSize = 11.5, Width = 26, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right };
            var s = new Slider { Name = "ms-knob-" + id, Minimum = 0, Maximum = 100, Value = value, Width = 150, SmallChange = 5, LargeChange = 10 };
            s.ValueChanged += (_, _) => { var n = (int)Math.Round(s.Value); v.Text = n.ToString(); _msOpt = set(_msOpt, n); };
            var row = new DockPanel { Children = { Dk(v, Avalonia.Controls.Dock.Right), Dk(s, Avalonia.Controls.Dock.Right), new TextBlock { Text = label, Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } };
            ToolTip.SetTip(row, tip); return row;
        }
        if (MsMeasuredSliders.Contains("weirdness")) sp.Children.Add(Knob("weirdness", "Verrücktheit", "Links: sichere, erwartbare Umsetzung. Rechts: überraschender, wilder.", _msOpt.Weirdness, (o, n) => o with { Weirdness = n }));
        if (MsMeasuredSliders.Contains("style")) sp.Children.Add(Knob("style", "Stil-Treue", "Wie eng die Musik-KI deinem Stil folgt.", _msOpt.StyleInfluence, (o, n) => o with { StyleInfluence = n }));
        if (MsMeasuredSliders.Contains("audio") && _msRefFile is not null) sp.Children.Add(Knob("audio", "Audio-Treue", "Wie stark das Klangvorbild („+ Audio“) den neuen Song prägt.", _msOpt.AudioInfluence, (o, n) => o with { AudioInfluence = n }));
        if (MsMeasuredSliders.Contains("variety")) sp.Children.Add(Knob("variety", "Vielfalt", "Links: eng an deinen Stil-Wörtern. Rechts: mehr Abwechslung.", _msOpt.Variety, (o, n) => o with { Variety = n }));
        var reset = Act("ms-opt-reset", "Zurücksetzen", () => { _msOpt = new(); Show(MS); });
        sp.Children.Add(reset);
        return sp;
        static string DurText(int s) => $"{s / 60}:{s % 60:00}";
    }

    public void MsApplyImageMood(string file)
    {
        var mood = MusicSuno.ImageMood(ImageStatsOf(file));
        if (_msPro) _msStyle = string.IsNullOrWhiteSpace(_msStyle) ? mood : _msStyle.TrimEnd(' ', ',') + ", " + mood;
        else _msDesc = string.IsNullOrWhiteSpace(_msDesc) ? "Lied, " + mood : _msDesc.TrimEnd(' ', ',', '.') + ", " + mood;
        _msMsg = $"Stimmung aus dem Bild „{Path.GetFileName(file)}“: {mood}";
    }
}
