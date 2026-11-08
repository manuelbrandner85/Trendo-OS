using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Trendo.Core;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>SPEC-08 (Freigabe 08.11.2026): echter Schnitt, echte Stoffe, Zuschnitt, 3D-Anprobe und Produktion im Programm „Kleidung &amp; Produkte“.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Stoffphysik nachbilden. Argumente: Auftragsdatei; Rückgabe: Meldung bei Fehler, sonst null.</summary>
    public Func<string, Task<string?>>? GarmentRunOverride { get; set; }
    public string GarmentMessage { get; private set; } = "";
    public IReadOnlyList<string> GarmentImages { get; private set; } = [];
    private bool _garmentBusy;

    private static BodyMeasures GarmentBody(ProgramDocument d)
    {
        var women = d.Flag("women");
        return new BodyMeasures(d.Number("height", women ? 168 : 180), d.Number("chest", women ? 92 : 100), d.Number("waist", women ? 74 : 86), d.Number("hip", women ? 98 : 102), women);
    }

    private static string GarmentKind(ProgramDocument d) => d.Text("gKind") is { Length: > 0 } k ? k : d.Text("name").Contains("rock", StringComparison.OrdinalIgnoreCase) ? "rock" : "tshirt";

    private Garment? GarmentOf(ProgramDocument d)
    {
        try { return GarmentBlocks.Build(GarmentKind(d), GarmentBody(d)); }
        catch (ArgumentException e) { GarmentMessage = e.Message; return null; }
    }

    private static string GarmentColor(ProgramDocument d) => d.Text("gColor") is { Length: > 0 } c ? c : Strings(d, "colors").FirstOrDefault() ?? "Grau";

    private Control GarmentPatternCanvas(Garment g)
    {
        const double scale = 3.4, gap = 14;
        var canvas = new Canvas { Name = "garment-pattern", Height = 0, HorizontalAlignment = HorizontalAlignment.Left };
        var x = 6.0; var maxH = 0.0; var i = 0;
        foreach (var p in g.Parts)
        {
            var cut = GarmentProduction.WithAllowance(p);
            double minX = cut.Min(q => q.X), maxX = cut.Max(q => q.X), maxY = cut.Max(q => q.Y), minY = cut.Min(q => q.Y);
            var ox = x - minX * scale; const double top = 6;
            Point Map(PatPt q) => new(ox + q.X * scale, top + (maxY - q.Y) * scale);
            canvas.Children.Add(new Polygon { Name = $"garment-part-{i}", Points = new Points(cut.Select(Map)), Fill = Brush.Parse("#22C9A227"), Stroke = Brush.Parse("#C9A227"), StrokeThickness = 1.4 });
            canvas.Children.Add(new Polygon { Points = new Points(p.Outline.Select(Map)), Stroke = Muted, StrokeThickness = 0.9, StrokeDashArray = [4, 3] });
            double yLow = p.Outline.Min(q => q.Y) + 6, yHigh = p.Outline.Max(q => q.Y) - 6;
            canvas.Children.Add(new Line { StartPoint = Map(new PatPt(0, yLow)), EndPoint = Map(new PatPt(0, yHigh)), Stroke = Text, StrokeThickness = 1 });
            var head = Map(new PatPt(0, yHigh));
            canvas.Children.Add(new Polygon { Points = new Points([head, head + new Point(-4, 8), head + new Point(4, 8)]), Fill = Text });
            canvas.Children.Add(new TextBlock { Text = $"{p.Name} ({p.Qty}×)", Foreground = Text, FontSize = 11.5, [Canvas.LeftProperty] = Map(new PatPt(0, 0)).X - 34, [Canvas.TopProperty] = top + (maxY - (maxY + minY) / 2) * scale });
            x += (maxX - minX) * scale + gap; maxH = Math.Max(maxH, (maxY - minY) * scale + 14); i++;
        }
        canvas.Width = x; canvas.Height = maxH;
        return canvas;
    }

    private Control GarmentNestCanvas(Garment g, NestResult nest, double fabricWidth)
    {
        const double scale = 3.0;
        var canvas = new Canvas { Name = "garment-nest", Width = nest.LengthCm * scale, Height = fabricWidth * scale, Background = Brush.Parse("#18FFFFFF"), HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var n in nest.Placed)
        {
            var pts = GarmentProduction.WithAllowance(g.Parts[n.Part]).Select(q => GarmentProduction.Place(q, n)).Select(q => new Point(q.Y * scale, q.X * scale));
            canvas.Children.Add(new Polygon { Points = new Points(pts), Fill = Brush.Parse("#44C9A227"), Stroke = Brush.Parse("#C9A227"), StrokeThickness = 1 });
        }
        return new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = canvas };
    }

    private Control GarmentPanel(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "garment-panel", Spacing = 8 };
        var g = GarmentOf(d);
        var kinds = new WrapPanel();
        foreach (var (id, label) in GarmentBlocks.Kinds)
        {
            var pick = id;
            kinds.Children.Add(MSp(Act("garment-kind-" + id, label, () => ProgramRun("mode-programm", () => d.Set("gKind", pick, "Schnitt wählen")), GarmentKind(d) == id)));
        }
        var fabric = Fabrics.Of(d.Text("gFabric"));
        var combo = new ComboBox { Name = "garment-fabric", ItemsSource = Fabrics.All.Select(f => f.Name).ToList(), SelectedIndex = Array.IndexOf(Fabrics.All, fabric), Width = 220 };
        combo.SelectionChanged += (_, _) => { if (combo.SelectedIndex >= 0 && Fabrics.All[combo.SelectedIndex] != fabric) ProgramRun("mode-programm", () => d.Set("gFabric", Fabrics.All[combo.SelectedIndex].Id, "Stoff wählen")); };
        sp.Children.Add(H("Schnitt und Stoff", 14)); sp.Children.Add(kinds);
        sp.Children.Add(new WrapPanel { Children = { MSp(combo), new TextBlock { Name = "garment-fabric-facts", Text = $"{fabric.Note} · {fabric.Gsm:0} g/m² · Bahn {fabric.WidthCm:0} cm · Dehnung {fabric.StretchPct:0} % · {fabric.PricePerM:0} €/m", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 } } });
        if (g is null) { sp.Children.Add(P(GarmentMessage, Brush.Parse("#FACC15"), 12)); return WCard(sp); }

        sp.Children.Add(H("Schnittteile", 13));
        sp.Children.Add(P("Durchgezogen: Schnittkante · gestrichelt: Nahtlinie · Pfeil: Fadenlauf", Muted, 11));
        sp.Children.Add(new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = GarmentPatternCanvas(g) });
        try
        {
            var nest = GarmentProduction.Nest(g, fabric.WidthCm);
            var meters = nest.LengthCm / 100;
            sp.Children.Add(H("Zuschnitt auf der Stoffbahn", 13));
            sp.Children.Add(new TextBlock { Name = "garment-nest-facts", Text = $"{meters:0.00} m Stoff · Ausnutzung {nest.Efficiency * 100:0} % · Stoffkosten {meters * fabric.PricePerM:0.00} €", Foreground = Text, FontSize = 12.5 });
            sp.Children.Add(GarmentNestCanvas(g, nest, fabric.WidthCm));
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { sp.Children.Add(P("Zuschnitt nicht möglich: " + e.Message, Brush.Parse("#FACC15"), 12)); }

        sp.Children.Add(H("3D-Anprobe", 13));
        var run = Act("garment-tryon", _garmentBusy ? "Rechnet …" : "In 3D anprobieren", () => _ = GarmentTryOn(d), true); run.IsEnabled = !_garmentBusy;
        var actions = new WrapPanel { Children = { MSp(run) } };
        if (BlenderPath is null && GarmentRunOverride is null) actions.Children.Add(new TextBlock { Text = "Dafür braucht Trendo Blender. Es lässt sich im Studio „3D-Modelle erstellen“ einrichten.", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
        sp.Children.Add(actions);
        if (GarmentMessage.Length > 0) sp.Children.Add(new TextBlock { Name = "garment-msg", Text = GarmentMessage, Foreground = Text, TextWrapping = TextWrapping.Wrap });
        if (GarmentImages.Count > 0)
        {
            var row = new WrapPanel { Name = "garment-images" };
            foreach (var f in GarmentImages.Where(File.Exists))
                try { row.Children.Add(new Image { Source = new Bitmap(f), Width = 300, Margin = new Thickness(0, 0, 8, 8) }); } catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException) { }
            sp.Children.Add(row);
        }

        sp.Children.Add(H("Für die Produktion", 13));
        sp.Children.Add(new WrapPanel { Children = {
            MSp(Act("garment-pdf", "Schnitt als PDF (A4, 1:1)", () => GarmentExport(d, ".pdf"))),
            MSp(Act("garment-dxf", "Plotter-Datei (DXF)", () => GarmentExport(d, ".dxf"))),
            MSp(Act("garment-techpack", "Technisches Datenblatt", () => GarmentExport(d, ".md"))) } });
        return WCard(sp);
    }

    /// <summary>Produktionsdatei schreiben (öffentlich für Tests). Rückgabe: Pfad oder null.</summary>
    public string? GarmentExport(ProgramDocument d, string ext)
    {
        var g = GarmentOf(d); if (g is null) { Show("mode-programm"); return null; }
        var folder = Path.Combine(WorkFolder("Mode"), "Produktion"); Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, DevStudio.SafeName(d.Text("name", "Entwurf")) + "-" + g.Kind + ext);
        try
        {
            var note = "";
            if (ext == ".pdf") note = $" ({GarmentProduction.PdfTiled(file, g, d.Text("name", "Entwurf"))} Seiten, mit 100 % drucken)";
            else if (ext == ".dxf") File.WriteAllText(file, GarmentProduction.Dxf(g));
            else File.WriteAllText(file, GarmentProduction.TechPack(d.Text("name", "Entwurf"), g, GarmentBody(d), Fabrics.Of(d.Text("gFabric")), GarmentColor(d), Strings(d, "sizes")));
            GarmentMessage = "Gespeichert: " + file + note;
            _core.Audit.Append(Principal.LocalOwner.Name, "mode.produktion", Path.GetFileName(file));
            NotifyFileResult(GarmentMessage);
            Show("mode-programm");
            return file;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { GarmentMessage = "Speichern ging nicht: " + e.Message; Show("mode-programm"); return null; }
    }

    private async Task<string?> GarmentTextures(FabricInfo fabric)
    {
        if (_opt.Isolated || _core.Offline) return null;
        var existing = Bib.Items().FirstOrDefault(i => i.Source == Trendo.Core.Bibliothek.AssetLibrary.AmbientCg && i.Id == fabric.TextureId && Directory.Exists(i.File));
        if (existing is not null) return existing.File;
        var item = await Bib.Download(new Trendo.Core.Bibliothek.AssetHit(Trendo.Core.Bibliothek.AssetLibrary.AmbientCg, fabric.TextureId, fabric.TextureId, "material", "Stoff", "", "CC0", "ambientCG", "https://ambientcg.com/a/" + fabric.TextureId));
        return item.File;
    }

    /// <summary>Anprobe starten (öffentlich für Tests): Auftrag schreiben, Stoffoberfläche laden, Blender ohne Fenster rechnen lassen.</summary>
    public async Task GarmentTryOn(ProgramDocument d)
    {
        if (_garmentBusy) return;
        var g = GarmentOf(d); if (g is null) { Show("mode-programm"); return; }
        var blender = BlenderPath;
        if (blender is null && GarmentRunOverride is null) { GarmentMessage = "Für die 3D-Anprobe wird Blender gebraucht. Richte es im Studio „3D-Modelle erstellen“ ein."; Show("mode-programm"); return; }
        _garmentBusy = true; GarmentMessage = "Stoff wird berechnet und fotografiert. Das dauert einige Minuten."; GarmentImages = []; Show("mode-programm");
        try
        {
            var dir = Path.Combine(WorkFolder("Mode"), "Anprobe", DevStudio.SafeName(d.Text("name", "Entwurf"))); Directory.CreateDirectory(dir);
            var fabric = Fabrics.Of(d.Text("gFabric"));
            var job = GarmentBlocks.ToJson(g, GarmentBody(d));
            job["fabric"] = Fabrics.ToJob(fabric, GarmentColor(d), await GarmentTextures(fabric));
            job["frames"] = 110; job["samples"] = 200; job["out"] = Path.Combine(dir, "bild");
            var jobFile = Path.Combine(dir, "auftrag.json"); File.WriteAllText(jobFile, job.ToJsonString());
            _core.Audit.Append(Principal.LocalOwner.Name, "mode.anprobe", g.Kind + " " + fabric.Id);
            string? problem;
            if (GarmentRunOverride is { } fake) problem = await fake(jobFile);
            else
            {
                var script = Path.Combine(dir, "trendo_anprobe.py");
                using (var s = AssetLoader.Open(new Uri("avares://TrendoOS/Assets/ki/trendo_anprobe.py"))) using (var t = File.Create(script)) await s.CopyToAsync(t);
                var r = await Task.Run(() => Cli.Run(blender!, ["--python", script, "--", jobFile], dir, 1_800_000));
                problem = r.Ok && r.Out.Contains("TRENDO_FERTIG") ? null : (r.Err.Length > 0 ? r.Err : r.Out).Trim().Split('\n').LastOrDefault(l => l.Trim().Length > 0) ?? "Blender hat abgebrochen.";
            }
            GarmentImages = new[] { "vorn", "schraeg", "hinten" }.Select(v => Path.Combine(dir, $"bild-{v}.png")).Where(File.Exists).ToList();
            GarmentMessage = problem is not null ? "Anprobe abgebrochen: " + problem : GarmentImages.Count == 0 ? "Blender hat kein Bild geliefert." : "Fertig. Die Bilder liegen in: " + dir;
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or HttpRequestException or UnauthorizedAccessException or System.Text.Json.JsonException) { GarmentMessage = "Anprobe ging nicht: " + e.Message; }
        finally { _garmentBusy = false; Show("mode-programm"); }
    }
}
