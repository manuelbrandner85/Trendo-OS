using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Trendo.Core.Arch;
using Trendo.Core.Bibliothek;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>T-0280: Foto-Ansicht im Grundriss – Blender rechnet den Plan mit Cycles fotorealistisch (Sonne und Himmel, echte Materialien,
/// Möbel in Render-Qualität): Vogelperspektive ohne Decke oder Raumansicht in Augenhöhe. Läuft im Hintergrund, abbrechbar, Bild speicherbar.</summary>
public partial class MainWindow
{
    private bool _archPhoto;
    public string ArchPhotoMode = "vogel";
    public string? ArchPhotoRoom;
    public bool ArchPhotoFinal;
    public string? ArchPhotoFile;
    public bool ArchPhotoBusy;
    private string _archPhotoMsg = "";
    private double _archPhotoProgress;
    private ProgressBar? _archPhotoBar;
    private CancellationTokenSource? _archPhotoCts;

    /// <summary>Für Tests: Ersatz für Blender (Szene, Zieldatei, Fortschritt, Abbruch → Bilddatei).</summary>
    public Func<JsonObject, string, IProgress<double>, CancellationToken, Task<string>>? ArchPhotoRenderer { get; set; }

    public bool ArchPhotoOpen => _archPhoto;
    public void ArchPhotoToggle() { _archPhoto = !_archPhoto; Show(AK); }

    /// <summary>Blender-Skripte aus dem Programm in den Datenordner legen (jedes Mal, damit sie zur Programmversion passen).</summary>
    string PhotoScripts()
    {
        var dir = Path.Combine(_core.DataRoot, "cache", "blender"); Directory.CreateDirectory(dir);
        foreach (var f in new[] { "trendo_foto.py", "trendo_moebel.py" })
        {
            using var s = AssetLoader.Open(new Uri($"avares://TrendoOS/Assets/blender/{f}"));
            using var o = File.Create(Path.Combine(dir, f)); s.CopyTo(o);
        }
        return dir;
    }

    /// <summary>Foto rechnen: Vorschau 960 × 600 / 64 Samples (Sekunden), fertig 1600 × 1000 / 256 Samples.</summary>
    public async Task ArchPhotoRender()
    {
        var d = ProgramActive(AK); if (d is null || ArchPhotoBusy) return;
        var levels = FloorPlan.Levels(d.Data);
        var (w, h, spp) = ArchPhotoFinal ? (1600, 1000, 256) : (960, 600, 64);
        var scene = PlanPhoto.Scene(levels, PlanModelFile, ArchPhotoMode, ArchPhotoMode == "raum" ? ArchPhotoRoom : null, w, h, spp);
        var outFile = Path.Combine(_core.DataRoot, "cache", "foto", $"grundriss-{ArchPhotoMode}-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        ArchPhotoBusy = true; _archPhotoProgress = 0; _archPhotoMsg = "Blender baut die Szene …"; _archPhotoCts = new CancellationTokenSource(); Show(AK);
        var progress = new Progress<double>(v => { _archPhotoProgress = v; if (_archPhotoBar is { } bar) bar.Value = v * 100; });
        try
        {
            string file;
            if (ArchPhotoRenderer is { } fake) file = await fake(scene, outFile, progress, _archPhotoCts.Token);
            else
            {
                if (_opt.Isolated) throw new InvalidOperationException("Im abgeschotteten Testmodus startet Trendo kein Blender.");
                var blender = ModelConverter.FindBlender() ?? throw new InvalidOperationException("Blender wurde nicht gefunden – die Foto-Ansicht braucht Blender (unter Programme einrichten).");
                file = await PlanPhoto.Render(scene, PhotoScripts(), outFile, blender, progress, ct: _archPhotoCts.Token);
            }
            if (ArchPhotoFile is { } old && old != file && old.StartsWith(Path.Combine(_core.DataRoot, "cache", "foto"))) try { File.Delete(old); } catch (IOException) { }
            ArchPhotoFile = file;
            _archPhotoMsg = $"✓ Foto fertig ({(ArchPhotoFinal ? "fertige Qualität" : "Vorschau")}, {(ArchPhotoMode == "raum" ? "Raumansicht" : "Vogelperspektive")}).";
        }
        catch (OperationCanceledException) { _archPhotoMsg = "⚠ Foto abgebrochen."; }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or TimeoutException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        { _archPhotoMsg = "⚠ " + ex.Message; }
        catch (Exception ex) { _archPhotoMsg = "⚠ Unerwarteter Fehler beim Foto: " + ex.Message; } // Hintergrundaufgabe: nie stumm hängen bleiben
        finally { ArchPhotoBusy = false; _archPhotoCts?.Dispose(); _archPhotoCts = null; }
        Show(AK);
    }

    public void ArchPhotoCancel() => _archPhotoCts?.Cancel();

    /// <summary>Foto neben den Plan in den Projektordner speichern.</summary>
    public string ArchPhotoSave()
    {
        if (ArchPhotoFile is null || !File.Exists(ArchPhotoFile)) throw new InvalidOperationException("Es gibt noch kein Foto – zuerst „Foto rechnen“.");
        var name = string.Concat((ProgramActive(AK)?.Title ?? "Grundriss").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var target = Path.Combine(WorkFolder("Grundrisse"), $"{name} – Foto {(ArchPhotoMode == "raum" ? "Raum" : "Vogel")} {DateTime.Now:yyyy-MM-dd HH-mm}.png");
        File.Copy(ArchPhotoFile, target, true);
        _core.Audit.Append(Trendo.Core.Principal.LocalOwner.Name, "arch.photo.save", Path.GetFileName(target));
        return target;
    }

    Control ArchPhotoView(ProgramDocument d)
    {
        var plan = FloorPlan.From(d.Data);
        Button Btn(string name, string label, Action go, bool on = false, bool accent = false)
        {
            var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 4, 4), FontSize = 12 };
            if (on || accent) { b.Background = accent ? Accent : Brush.Parse("#C9A227"); b.Foreground = Brushes.White; }
            b.Click += (_, _) => go(); return b;
        }
        var bar = new WrapPanel { Name = "arch-photo-bar" };
        bar.Children.Add(Btn("arch-photo-vogel", "Vogelperspektive", () => { ArchPhotoMode = "vogel"; Show(AK); }, ArchPhotoMode == "vogel"));
        bar.Children.Add(Btn("arch-photo-raum", "Raumansicht", () => { ArchPhotoMode = "raum"; Show(AK); }, ArchPhotoMode == "raum"));
        if (ArchPhotoMode == "raum" && plan.Rooms.Count > 0)
        {
            var rooms = plan.Rooms.Where(r => r.Points.Count >= 3).ToList();
            var cb = new ComboBox { Name = "arch-photo-room", Width = 170, FontSize = 12, Margin = new Thickness(0, 0, 4, 4), ItemsSource = rooms.Select(r => r.Name).ToList(),
                SelectedIndex = Math.Max(0, rooms.FindIndex(r => r.Id == ArchPhotoRoom)) };
            if (ArchPhotoRoom is null && rooms.Count > 0) ArchPhotoRoom = rooms.OrderByDescending(r => r.Area).First().Id;
            cb.SelectedIndex = Math.Max(0, rooms.FindIndex(r => r.Id == ArchPhotoRoom));
            cb.SelectionChanged += (_, _) => { if (cb.SelectedIndex >= 0) ArchPhotoRoom = rooms[cb.SelectedIndex].Id; };
            bar.Children.Add(cb);
        }
        bar.Children.Add(new TextBlock { Text = "Qualität", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 6, 4) });
        bar.Children.Add(Btn("arch-photo-q-preview", "Vorschau", () => { ArchPhotoFinal = false; Show(AK); }, !ArchPhotoFinal));
        bar.Children.Add(Btn("arch-photo-q-final", "Fertig (hohe Auflösung)", () => { ArchPhotoFinal = true; Show(AK); }, ArchPhotoFinal));
        if (ArchPhotoBusy) bar.Children.Add(Btn("arch-photo-cancel", "Abbrechen", ArchPhotoCancel));
        else bar.Children.Add(Btn("arch-photo-go", "Foto rechnen", () => { _ = ArchPhotoRender(); }, accent: true));
        if (ArchPhotoFile is not null && !ArchPhotoBusy) bar.Children.Add(Btn("arch-photo-save", "Speichern", () => { try { _archPhotoMsg = "✓ Gespeichert: " + Path.GetFileName(ArchPhotoSave()); } catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException) { _archPhotoMsg = "⚠ " + ex.Message; } Show(AK); }));
        bar.Children.Add(Btn("arch-photo-back", "Zurück zum Plan", ArchPhotoToggle));

        Control body;
        if (ArchPhotoBusy)
        {
            _archPhotoBar = new ProgressBar { Name = "arch-photo-progress", Minimum = 0, Maximum = 100, Value = _archPhotoProgress * 100, Height = 8, Width = 360, Foreground = Accent };
            body = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 10, Children = {
                new TextBlock { Text = "Blender rechnet das Foto (Cycles) …", Foreground = Text, FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center },
                _archPhotoBar,
                new TextBlock { Text = ArchPhotoFinal ? "Fertige Qualität dauert etwa eine halbe Minute mit Grafikkarte, mit Prozessor einige Minuten." : "Die Vorschau dauert einige Sekunden.", Foreground = Muted, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Center } } };
        }
        else if (ArchPhotoFile is { } f && File.Exists(f) && BibBitmap(f) is { } bmp)
            body = new Image { Name = "arch-photo-image", Source = bmp, Stretch = Stretch.Uniform };
        else
            body = new TextBlock { Name = "arch-photo-empty", Text = "Fotorealistische Ansicht des Plans – mit echten Materialien, Sonne durch die Fenster und den Möbeln in voller Qualität.\n„Foto rechnen“ startet Blender im Hintergrund.",
                Foreground = Muted, FontSize = 14, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 560, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var stage = new Border { Name = "arch-photo", Background = Brush.Parse("#0B1424"), CornerRadius = new CornerRadius(10), ClipToBounds = true, MinHeight = 160, Child = body };
        var root = new DockPanel(); DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top); root.Children.Add(bar);
        if (_archPhotoMsg.Length > 0)
        {
            var msg = new TextBlock { Name = "arch-photo-msg", Text = _archPhotoMsg, FontSize = 12, Margin = new Thickness(2, 0, 0, 6), TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse(_archPhotoMsg.StartsWith('⚠') ? "#F87171" : _archPhotoMsg.StartsWith('✓') ? "#4ADE80" : "#7FD3FF") };
            DockPanel.SetDock(msg, Avalonia.Controls.Dock.Top); root.Children.Add(msg);
        }
        root.Children.Add(stage);
        return root;
    }
}
