using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Media;

namespace Trendo.App;

/// <summary>Paket 01 – Studio 4, S4-04 (06.11.2026): Modus „Noten“ in Trendo Musik – Piano-Roll in der Mitte, Quantisieren, Transponieren,
/// Tempo, MIDI speichern und öffnen. Kaputte MIDI-Dateien ergeben eine Warnung, das offene Stück bleibt.</summary>
public partial class MainWindow
{
    bool _msNoten;
    public PianoRoll.Song MsRoll { get; set; } = new(120, 4, [], "");
    readonly Stack<PianoRoll.Song> _msRollUndo = new();
    public string MsRollMessage { get; private set; } = "";
    public PianoRollView? MsRollViewForTests { get; private set; }

    void MsRollEdit(PianoRoll.Song s) { _msRollUndo.Push(MsRoll); MsRoll = PianoRoll.Validate(s); }

    public void MsRollToggle(int pitch, int tick) => MsRollEdit(MsRoll with { Notes = PianoRoll.Toggle(MsRoll.Notes, pitch, tick, 240) });

    public string MsRollSave(string name)
    {
        var dir = Path.Combine(WorkFolder("Musik"), "Noten"); Directory.CreateDirectory(dir);
        var safe = DevStudio.SafeName(name.Length > 0 ? name : "Melodie");
        var f = Path.Combine(dir, safe + ".mid"); for (var i = 2; File.Exists(f); i++) f = Path.Combine(dir, $"{safe}-{i}.mid");
        File.WriteAllBytes(f, PianoRoll.ToMidi(MsRoll with { Name = name.Length > 0 ? name : MsRoll.Name }));
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.midi.save", Path.GetFileName(f));
        MsRollMessage = $"Gespeichert: {Path.GetFileName(f)} – {MsRoll.Notes.Count} Noten, öffnet in jedem Notenprogramm.";
        return f;
    }

    /// <summary>MIDI öffnen: ohne lesbaren Kopf bleibt das offene Stück unverändert; sonst wird geladen und jede Warnung genannt.</summary>
    public bool MsRollOpen(string file)
    {
        file = file.Trim().Trim('"');
        if (!File.Exists(file)) { MsRollMessage = "Datei nicht gefunden: " + Path.GetFileName(file); return false; }
        var r = PianoRoll.FromMidi(File.ReadAllBytes(file));
        if (!r.Ok) { MsRollMessage = "Nicht geöffnet – " + string.Join(" ", r.Warnings) + " Dein Stück bleibt unverändert."; return false; }
        MsRollEdit(r.Song!);
        MsRollMessage = $"Geöffnet: {Path.GetFileName(file)} – {r.Song!.Notes.Count} Noten" + (r.Warnings.Count > 0 ? $". Achtung: {r.Warnings.Count} Warnung(en): " + string.Join(" ", r.Warnings.Take(3)) : ".");
        return true;
    }

    /// <summary>Paket 01 S4-14: Tempo so einpassen, dass Takt <paramref name="bar"/> genau am Schnitt-Timecode beginnt; prüft in Bildern (25 fps).</summary>
    public ScoreSync.HitCheck MsRollFit(string timecode, int bar, int fps = 25)
    {
        var sec = ScoreSync.ParseTimecode(timecode, fps);
        var bpm = ScoreSync.FitBpm([new ScoreSync.TempoChange(1, MsRoll.Bpm, MsRoll.Numerator)], 1, bar, sec, MsRoll.Numerator);
        MsRollEdit(MsRoll with { Bpm = bpm });
        var c = ScoreSync.CheckHits([new ScoreSync.TempoChange(1, MsRoll.Bpm, MsRoll.Numerator)], [new ScoreSync.Hit("Schnitt", sec, bar)], fps)[0];
        MsRollMessage = $"Tempo {MsRoll.Bpm:0.###} BPM: Takt {bar} beginnt bei {FilmDelivery.Timecode(c.Actual, fps)} – {(c.Ok ? "trifft den Schnitt" : "verfehlt den Schnitt")} ({c.OffFrames:+0.00;-0.00;0} Bilder).";
        return c;
    }

    Control MsNotenPanel()
    {
        var sp = new DockPanel { Name = "ms-noten" };
        var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        Button B(string name, string label, Action a, bool primary = false)
        {
            var b = new Button { Name = name, Content = label, FontSize = 12, Padding = new Thickness(9, 4), Margin = new Thickness(0, 0, 4, 4), CornerRadius = new CornerRadius(5), Background = primary ? Accent : DawButton, Foreground = primary ? Brushes.White : Text, BorderThickness = new Thickness(0) };
            b.Click += (_, _) => { try { a(); } catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { MsRollMessage = ex.Message; } Show(MS); };
            return b;
        }
        var bpm = new NumericUpDown { Name = "ms-roll-bpm", Value = (decimal)MsRoll.Bpm, Minimum = 20, Maximum = 400, Increment = 1, FormatString = "0", Width = 100, FontSize = 12 };
        bpm.ValueChanged += (_, _) => { if (bpm.Value is { } v && (double)v != MsRoll.Bpm) MsRollEdit(MsRoll with { Bpm = (double)v }); };
        var name = new TextBox { Name = "ms-roll-name", Text = MsRoll.Name, Watermark = "Name", Width = 140 };
        var path = new TextBox { Name = "ms-roll-file", Watermark = "MIDI-Datei zum Öffnen", Width = 200 };
        bar.Children.Add(new TextBlock { Text = "NOTEN  ", Foreground = Muted, FontSize = 10, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center });
        bar.Children.Add(bpm); bar.Children.Add(new Border { Width = 6 });
        bar.Children.Add(B("ms-roll-quant", "Quantisieren 1/16", () => MsRollEdit(MsRoll with { Notes = PianoRoll.Quantize(MsRoll.Notes, 120) })));
        bar.Children.Add(B("ms-roll-up", "＋1 Halbton", () => MsRollEdit(MsRoll with { Notes = PianoRoll.Transpose(MsRoll.Notes, 1) })));
        bar.Children.Add(B("ms-roll-down", "−1 Halbton", () => MsRollEdit(MsRoll with { Notes = PianoRoll.Transpose(MsRoll.Notes, -1) })));
        bar.Children.Add(B("ms-roll-undo", "Rückgängig", () => { if (_msRollUndo.Count > 0) MsRoll = _msRollUndo.Pop(); }));
        bar.Children.Add(B("ms-roll-clear", "Leeren", () => MsRollEdit(MsRoll with { Notes = [] })));
        bar.Children.Add(name);
        bar.Children.Add(B("ms-roll-save", "MIDI speichern", () => MsRollSave((name.Text ?? "").Trim()), true));
        bar.Children.Add(path);
        bar.Children.Add(B("ms-roll-open", "MIDI öffnen", () => MsRollOpen(path.Text ?? "")));
        // Filmmusik: Takt auf einen Schnitt-Timecode legen
        var tc = new TextBox { Name = "ms-roll-hit-tc", Watermark = "Schnitt bei TC 00:00:12:12", Width = 170 };
        var hitBar = new NumericUpDown { Name = "ms-roll-hit-bar", Value = 9, Minimum = 2, Maximum = 999, Increment = 1, FormatString = "0", Width = 96, FontSize = 12 };
        bar.Children.Add(new TextBlock { Text = "  FILM  Takt ", Foreground = Muted, FontSize = 10, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center });
        bar.Children.Add(hitBar); bar.Children.Add(tc);
        bar.Children.Add(B("ms-roll-fit", "Auf Schnitt einpassen", () => MsRollFit(tc.Text ?? "", (int)(hitBar.Value ?? 9))));
        DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top); sp.Children.Add(bar);
        if (MsRollMessage.Length > 0) { var m = P(MsRollMessage, Muted, 11.5); DockPanel.SetDock(m, Avalonia.Controls.Dock.Bottom); sp.Children.Add(m); }
        var view = new PianoRollView { Name = "ms-roll-view", Notes = MsRoll.Notes, Numerator = MsRoll.Numerator, Bars = Math.Max(4, (int)Math.Ceiling((MsRoll.Notes.Count == 0 ? 0 : MsRoll.Notes.Max(n => n.Start + n.Length)) / (double)(MsRoll.Numerator * PianoRoll.Ppq))), MinHeight = 420 };
        if (MsRoll.Notes.Count > 0) { view.Low = Math.Min(48, MsRoll.Notes.Min(n => n.Pitch) - 2); view.High = Math.Max(84, MsRoll.Notes.Max(n => n.Pitch) + 2); }
        view.Clicked = (p, t) => { MsRollToggle(p, t); Show(MS); };
        MsRollViewForTests = view;
        sp.Children.Add(new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true, BorderBrush = PaneEdge, BorderThickness = new Thickness(1), Child = view });
        return MsCard("ms-noten-card", sp);
    }
}
