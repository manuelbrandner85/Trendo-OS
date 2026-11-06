using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Audio bearbeiten – Mehrspur-Ansicht wie Audition (Profi 25, 05.11.2026) und spektrales Retuschieren wie iZotope RX (AU-12).</summary>
public partial class MainWindow
{
    readonly Dictionary<string, (double Lo, double Hi)> _auFreq = [];
    readonly List<ClipLane> _auLanes = [];
    static readonly string[] AuTrackColors = ["#F5B83D", "#38BDF8", "#A78BFA", "#34D399", "#F472B6", "#FB923C", "#60A5FA", "#FACC15"];

    /// <summary>Frequenzbereich der Auswahl im Spektrogramm (Hz); Hi ≤ Lo = keiner. Öffentlich für Tests und die KI.</summary>
    public (double Lo, double Hi) AuFreq
    {
        get => ProgramActive(AUK) is { } d ? _auFreq.GetValueOrDefault(d.Id) : default;
        set { if (ProgramActive(AUK) is { } d) _auFreq[d.Id] = (Math.Min(value.Lo, value.Hi), Math.Max(value.Lo, value.Hi)); }
    }

    /// <summary>Gewählten Zeit-Frequenz-Bereich absenken (RX „Attenuate“).</summary>
    public void AuSpectral(ProgramDocument d, double gainDb)
    {
        var (s, e) = AuSel; var (lo, hi) = AuFreq;
        if (e <= s || hi <= lo) throw new InvalidOperationException("Erst „Spektrogramm“ einschalten und darin ein Rechteck um das Störgeräusch ziehen (Zeit und Frequenz).");
        d.Edit(AudioEdit.Label("spectral"), x => AudioEdit.Add(x, AudioEdit.SpectralOp(s, e, lo, hi, gainDb)));
        _progMsg[AUK] = $"{AuHz(lo)}–{AuHz(hi)} von {AudioEdit.Time(s)} bis {AudioEdit.Time(e)} um {-gainDb:0} dB abgesenkt – Strg+Z macht es rückgängig.";
    }

    static string AuHz(double hz) => hz >= 1000 ? $"{hz / 1000:0.#} kHz" : $"{hz:0} Hz";

    /// <summary>Datei als neue Spur ins Mehrspur-Projekt, ab der Marke.</summary>
    public void AuAddTrack(string file)
    {
        var d = ProgramActive(AUK) is { } cur && AudioMulti.IsMulti(cur.Data) ? cur : ProgramNew(AUK, "mehrspur");
        file = Path.GetFullPath(file.Trim().Trim('"')); var at = AuSel.A;
        d.Edit("Spur hinzufügen", x => AudioMulti.AddTrack(x, file, at));
        _progMsg[AUK] = $"„{Path.GetFileName(file)}“ liegt auf Spur {AudioMulti.Tracks(d.Data).Count} ab {AudioEdit.Time(at)}.";
    }

    /// <summary>Mehrspur-Projekt abmischen, als WAV speichern und als einzelne Spur öffnen.</summary>
    public string AuMixdown(ProgramDocument d)
    {
        if (AudioMulti.Tracks(d.Data).Count == 0) throw new InvalidOperationException("Erst Spuren hinzufügen.");
        var dir = Path.Combine(WorkFolder("Medien"), "Abmischungen"); Directory.CreateDirectory(dir);
        var f = Path.Combine(dir, $"{(d.Title.Length > 0 ? string.Concat(d.Title.Split(Path.GetInvalidFileNameChars())) : "Abmischung")} {DateTime.Now:yyyy-MM-dd HH-mm-ss}.wav");
        AudioFile.WriteWav(AudioEdit.Render(d.Data), f, 24);
        ProgramNew(AUK, "leer"); AuOpen(f);
        _progMsg[AUK] = $"Abgemischt nach „{Path.GetFileName(f)}“ und als Spur geöffnet.";
        return f;
    }

    Control AuMultiWorkspace(ProgramDocument d)
    {
        var tracks = AudioMulti.Tracks(d.Data); _auLanes.Clear(); _auWave = null;
        var parts = tracks.Select(t => { try { return (T: t, A: (AudioBuf?)AudioFile.Read(t.File)); } catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException) { return (T: t, A: (AudioBuf?)null); } }).ToList();
        var total = Math.Max(10, parts.Count == 0 ? 0 : parts.Max(p => p.T.Start + (p.A?.Seconds ?? 0)) * 1.1);
        var marker = AuSel.A;
        var rows = new Grid { Name = "au-multi", ColumnDefinitions = new ColumnDefinitions("230,*") };
        rows.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        var head = new TextBlock { Text = $"Spuren ({tracks.Count})", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        rows.Children.Add(head);
        var ruler = new TimeRuler { Total = total }; Grid.SetColumn(ruler, 1); rows.Children.Add(ruler);
        for (var i = 0; i < parts.Count; i++)
        {
            var (t, a) = parts[i]; var id = t.Id; var row = i + 1;
            rows.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var audible = AudioMulti.Audible(tracks, t);
            Button Tog(string name, string label, bool on, string tip, Action act)
            {
                var b = new Button { Name = name, Content = label, Width = 28, Height = 24, Padding = new Thickness(0), FontSize = 11, FontWeight = FontWeight.Bold, HorizontalContentAlignment = HorizontalAlignment.Center,
                    Background = on ? Brush.Parse(label == "S" ? "#FACC15" : "#EF4444") : Brush.Parse("#22FFFFFF"), Foreground = on ? Brushes.Black : Text, Margin = new Thickness(0, 0, 4, 0) };
                ToolTip.SetTip(b, tip); b.Click += (_, _) => ProgramRun(AUK, act); return b;
            }
            var vol = new Slider { Name = $"au-track-vol-{i}", Minimum = -30, Maximum = 6, Value = t.GainDb, Width = 92, Margin = new Thickness(0, -8, 0, -8) };
            var pan = new Slider { Name = $"au-track-pan-{i}", Minimum = -1, Maximum = 1, Value = t.Pan, Width = 64, Margin = new Thickness(0, -8, 0, -8) };
            ToolTip.SetTip(vol, "Lautstärke der Spur"); ToolTip.SetTip(pan, "Panorama links ↔ rechts");
            void Commit() => ProgramRun(AUK, () => d.Edit("Spur einstellen", x => AudioMulti.SetTrack(x, id, gainDb: Math.Round(vol.Value, 1), pan: Math.Round(pan.Value, 2))));
            vol.PointerCaptureLost += (_, _) => Commit(); pan.PointerCaptureLost += (_, _) => Commit();
            var del = new Button { Name = $"au-track-del-{i}", Content = "✕", Width = 24, Height = 24, Padding = new Thickness(0), FontSize = 11, Background = Brushes.Transparent, Foreground = Muted, HorizontalContentAlignment = HorizontalAlignment.Center };
            ToolTip.SetTip(del, "Spur entfernen"); del.Click += (_, _) => ProgramRun(AUK, () => d.Edit("Spur entfernen", x => AudioMulti.RemoveTrack(x, id)));
            var color = Color.Parse(AuTrackColors[i % AuTrackColors.Length]);
            var header = new Border { Name = $"au-track-{i}", Height = 84, Margin = new Thickness(0, 2, 6, 2), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 5), BorderThickness = new Thickness(3, 0, 0, 0),
                BorderBrush = new SolidColorBrush(color), Background = Brush.Parse(audible ? "#1A2230" : "#10151E"),
                Child = new StackPanel { Spacing = 3, Children = {
                    new DockPanel { Children = { Dock(del), new TextBlock { Text = t.Name, Foreground = audible ? Text : Muted, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center } } },
                    new StackPanel { Orientation = Orientation.Horizontal, Children = {
                        Tog($"au-track-mute-{i}", "M", t.Mute, "Stumm", () => d.Edit("Spur stumm", x => AudioMulti.SetTrack(x, id, mute: !t.Mute))),
                        Tog($"au-track-solo-{i}", "S", t.Solo, "Solo – nur Solo-Spuren sind zu hören", () => d.Edit("Spur solo", x => AudioMulti.SetTrack(x, id, solo: !t.Solo))),
                        vol, new Border { Width = 4 }, pan } },
                    new TextBlock { Text = $"{t.GainDb:+0.0;−0.0;0} dB · {(Math.Abs(t.Pan) < 0.05 ? "Mitte" : t.Pan < 0 ? $"L {-t.Pan * 100:0}" : $"R {t.Pan * 100:0}")} · ab {AudioEdit.Time(t.Start)}", Foreground = Muted, FontSize = 10.5 } } } };
            Grid.SetRow(header, row); rows.Children.Add(header);
            var lane = new ClipLane { Name = $"au-lane-{i}", Total = total, Start = t.Start, Length = a?.Seconds ?? 1, Peaks = a is null ? [] : AudioEdit.Peaks(a, 500), Label = a is null ? t.Name + " (Datei fehlt)" : t.Name,
                Tint = color, Dimmed = !audible, Marker = marker, Margin = new Thickness(0, 2, 0, 2) };
            lane.Moved = s => ProgramRun(AUK, () => { d.Edit("Spur verschieben", x => AudioMulti.SetTrack(x, id, start: s)); _progMsg[AUK] = $"„{t.Name}“ beginnt jetzt bei {AudioEdit.Time(s)}."; });
            lane.Clicked = s => { AuSel = (s, s); Show(AUK); };
            _auLanes.Add(lane);
            Grid.SetRow(lane, row); Grid.SetColumn(lane, 1); rows.Children.Add(lane);
        }
        Control body = tracks.Count > 0 ? new ScrollViewer { Content = rows } :
            new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Children = {
                new TextBlock { Text = "🎚", FontSize = 40, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = "Mehrspur-Projekt", Foreground = Text, FontSize = 18, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center },
                P("Rechts Tondateien als Spuren hinzufügen – Stimme, Musik, Geräusche. Clips mit der Maus verschieben, je Spur Lautstärke, Panorama, Stumm und Solo.", Muted, 12.5) } };
        var frame = new Border { Child = body, CornerRadius = new CornerRadius(10), ClipToBounds = true, BorderBrush = Brush.Parse("#33F5B83D"), BorderThickness = new Thickness(1), Background = Brush.Parse("#0B0E14"), Padding = new Thickness(6) };
        var bar = new StackPanel { Name = "au-transport", Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
        Button T(string name, string label, string tip, Action act, bool primary = false)
        {
            var b = new Button { Name = name, Content = label, FontSize = 14, MinWidth = 44, Margin = new Thickness(0, 0, 6, 0), CornerRadius = new CornerRadius(8) };
            if (primary) { b.Background = Accent; b.Foreground = Brushes.White; }
            ToolTip.SetTip(b, tip); b.Click += (_, _) => ProgramRun(AUK, act); return b;
        }
        bar.Children.Add(T("au-tp-start", "⏮", "Marke an den Anfang", () => AuSel = default));
        bar.Children.Add(T("au-tp-play", "▶", "Summe abspielen (ab der Marke)", () => AuPlay(d), true));
        bar.Children.Add(T("au-tp-stop", "■", "Stopp", AuStop));
        bar.Children.Add(T("au-mixdown", "⤓ Abmischen", "Alle Spuren zu einer Datei abmischen und als Spur öffnen", () => AuMixdown(d)));
        bar.Children.Add(new TextBlock { Name = "au-time", Text = $"Marke {AudioEdit.Time(marker)}", Foreground = Text, FontSize = 15, FontFamily = new FontFamily("Consolas, monospace"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) });
        var dawHead = new Border { Name = "au-daw-bar", CornerRadius = new CornerRadius(10), Background = DawPanel, BorderBrush = DawEdge, BorderThickness = new Thickness(1), Padding = new Thickness(10, 6), Margin = new Thickness(0, 0, 0, 8), Child = bar };
        bar.HorizontalAlignment = HorizontalAlignment.Left; bar.Margin = default;
        var g = new Grid { RowDefinitions = new RowDefinitions("Auto,*") }; // DAW-Stil: Transport oben
        g.Children.Add(dawHead); Grid.SetRow(frame, 1); g.Children.Add(frame);
        return g;
        static Control Dock(Control c) { DockPanel.SetDock(c, Avalonia.Controls.Dock.Right); return c; }
    }

    /// <summary>Spektral-Werkzeug in den Eigenschaften: Stärke wählen und anwenden.</summary>
    Control? AuSpectralPanel(ProgramDocument d)
    {
        if (!AuSpectro) return null;
        var sp = new StackPanel { Name = "au-spectral", Spacing = 4 };
        sp.Children.Add(H("Spektral retuschieren", 13));
        var (s, e) = AuSel; var (lo, hi) = AuFreq;
        if (e <= s || hi <= lo) { sp.Children.Add(P("Im Spektrogramm ein Rechteck um das Störgeräusch ziehen – Husten, Pfeifton, Handy. Nur dieser Zeit- und Frequenzbereich wird bearbeitet.", Muted, 12)); return sp; }
        sp.Children.Add(P($"{AudioEdit.Time(s)}–{AudioEdit.Time(e)} · {AuHz(lo)}–{AuHz(hi)}", Text, 12));
        var amount = new Slider { Name = "au-spectral-db", Minimum = -60, Maximum = -3, Value = -30 };
        var label = new TextBlock { Text = "Absenken um 30 dB", Foreground = Muted, FontSize = 11.5 };
        amount.PropertyChanged += (_, ev) => { if (ev.Property == Slider.ValueProperty) label.Text = $"Absenken um {-amount.Value:0} dB"; };
        sp.Children.Add(label); sp.Children.Add(amount);
        var go = new Button { Name = "au-spectral-apply", Content = "Bereich absenken", Background = Accent, Foreground = Brushes.White, FontSize = 12 };
        go.Click += (_, _) => ProgramRun(AUK, () => AuSpectral(d, Math.Round(amount.Value)));
        sp.Children.Add(go);
        return sp;
    }
}
