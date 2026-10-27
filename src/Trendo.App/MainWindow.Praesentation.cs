using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Office;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0229: Trendo Präsentation – Folien erstellen, gestalten und vorführen (wie PowerPoint/Impress), leicht verständlich.
/// Startseite (G1), Menüband (G2), Eigenschaften (G4), Statusleiste (G5). Dateien: PPTX, ODP (lesen), PDF.</summary>
public partial class MainWindow
{
    public SlideDoc Deck { get; private set; } = SlideDoc.Empty();
    public string? DeckPath { get; private set; }
    public bool DeckDirty { get; private set; }
    public int DeckIndex { get; private set; }
    public Slide CurrentSlide => Deck.Slides[Math.Clamp(DeckIndex, 0, Deck.Slides.Count - 1)];
    public SlideView? DeckCanvas { get; private set; }
    public bool DeckStartVisible { get; private set; } = true;
    private readonly List<SlideDoc> _dkUndo = [], _dkRedo = [];
    private string _dkMsg = "", _dkRibbon = "start", _dkOutline = "";
    private int _dkSel = -1;
    private TextBox? _dkEditor; private Canvas? _dkOverlay;

    private void InitPraesentation()
    {
        _showMap["praesentation"] = DeckPageView;
        PageModules["praesentation"] = "SYS-10";
        ExtraPages.Add(("Trendo Präsentation", "praesentation", ["präsentation", "praesentation", "folien", "powerpoint", "impress", "pptx", "odp", "vortrag", "referat", "diashow"]));
    }

    // ---------- Öffentliche Wege (auch für Tests und die KI) ----------
    public void DeckNew(string template = "leer")
    {
        Deck = template switch
        {
            "vortrag" => SlideOutline.FromOutline("Titel des Vortrags\n  - Dein Name · Datum\nWorum es geht\n  - Ausgangslage\n  - Ziel\n  - Ablauf\nDie wichtigsten Punkte\n  - Erstens\n  - Zweitens\n  - Drittens\nZusammenfassung\n  - Das Wichtigste in einem Satz\nFragen?", SlideThemes.Get("ozean")),
            "projekt" => SlideOutline.FromOutline("Projektbericht\n  - Stand und nächste Schritte\nZiele\n  - Was wir erreichen wollen\nStand\n  - Erledigt\n  - In Arbeit\n  - Offen\nZeitplan\n  - Meilenstein 1\n  - Meilenstein 2\nRisiken\n  - Risiko und Gegenmaßnahme\nNächste Schritte", SlideThemes.Get("schiefer")),
            "fotoalbum" => PhotoAlbum(),
            _ => SlideDoc.Empty(),
        };
        DeckPath = null; DeckDirty = false; ResetDeckState();
        _dkMsg = template == "leer" ? "Neue Präsentation." : "Vorlage geöffnet – Texte einfach anklicken und überschreiben.";
    }

    static SlideDoc PhotoAlbum()
    {
        var d = new SlideDoc { Theme = SlideThemes.Get("nacht") };
        var t = d.AddSlide(SlideLayout.Titel); t.ByRole("title")!.Text = "Unser Fotoalbum"; t.ByRole("sub")!.Text = "Erinnerungen";
        for (var i = 1; i <= 3; i++) { var s = d.AddSlide(SlideLayout.Bild); s.ByRole("title")!.Text = $"Bild {i}"; s.Transition = Transition.Ueberblenden; }
        return d;
    }

    /// <summary>P7: Präsentation aus Stichpunkten (eine Zeile = Folie, eingerückt oder mit „-“ = Punkt, „&gt;“ = Notiz).</summary>
    public void DeckFromOutline(string outline)
    {
        Deck = SlideOutline.FromOutline(outline, Deck.Theme); DeckPath = null; DeckDirty = true; ResetDeckState();
        _dkMsg = $"{Deck.Slides.Count} Folien aus deinen Stichpunkten erstellt.";
    }

    public void DeckOpen(string path)
    {
        Deck = SlideFiles.Open(path); ResetDeckState();
        DeckPath = path.EndsWith(".pptx", StringComparison.OrdinalIgnoreCase) ? path : null; DeckDirty = false;
        _dkMsg = $"Geöffnet: {Path.GetFileName(path)}"; RememberDeckRecent(path);
        _core.Audit.Append(Principal.LocalOwner.Name, "praesentation.open", Path.GetFileName(path));
    }

    public void DeckSaveAs(string path)
    {
        SlideFiles.Save(Deck, path);
        if (path.EndsWith(".pptx", StringComparison.OrdinalIgnoreCase)) { DeckPath = path; DeckDirty = false; RememberDeckRecent(path); }
        _dkMsg = $"Gespeichert: {Path.GetFileName(path)}";
        _core.Audit.Append(Principal.LocalOwner.Name, "praesentation.save", Path.GetFileName(path));
    }

    public void DeckGo(int index) { CloseDeckEditor(true); DeckIndex = Math.Clamp(index, 0, Deck.Slides.Count - 1); _dkSel = -1; }

    public Slide DeckAddSlide(SlideLayout layout)
    {
        DeckSnapshot(); var s = Deck.AddSlide(layout, DeckIndex + 1); DeckIndex++; _dkSel = -1; DeckChanged();
        _dkMsg = $"Neue Folie „{SlideLayouts.Name(layout)}“."; return s;
    }

    public void DeckDuplicate() { DeckSnapshot(); Deck.Slides.Insert(DeckIndex + 1, CurrentSlide.Clone()); DeckIndex++; DeckChanged(); _dkMsg = "Folie dupliziert."; }

    public void DeckDelete()
    {
        if (Deck.Slides.Count <= 1) { _dkMsg = "Die letzte Folie bleibt stehen."; return; }
        DeckSnapshot(); Deck.Slides.RemoveAt(DeckIndex); DeckIndex = Math.Min(DeckIndex, Deck.Slides.Count - 1); _dkSel = -1; DeckChanged(); _dkMsg = "Folie gelöscht.";
    }

    public void DeckMove(int delta) { var to = Math.Clamp(DeckIndex + delta, 0, Deck.Slides.Count - 1); if (to == DeckIndex) return; DeckSnapshot(); Deck.Move(DeckIndex, to); DeckIndex = to; DeckChanged(); }

    public void DeckSetLayout(SlideLayout layout)
    {
        DeckSnapshot();
        var old = CurrentSlide; var neu = SlideLayouts.Create(layout);
        // Texte der Platzhalter übernehmen, eigene Objekte behalten
        foreach (var ph in neu.Shapes.Where(x => x.Role is not null)) if (old.ByRole(ph.Role!) is { } o) { ph.Text = o.Text; ph.Color = o.Color; }
        if (old.ByRole("body") is { } b && neu.ByRole("body") is null && neu.ByRole("sub") is { } sub) sub.Text = b.Text.Split('\n')[0];
        neu.Shapes.AddRange(old.Shapes.Where(x => x.Role is null));
        neu.Notes = old.Notes; neu.Transition = old.Transition; neu.Background = old.Background;
        Deck.Slides[DeckIndex] = neu; _dkSel = -1; DeckChanged(); _dkMsg = $"Layout: {SlideLayouts.Name(layout)}";
    }

    public void DeckSetTheme(string id) { DeckSnapshot(); Deck.Theme = SlideThemes.Get(id); DeckChanged(); _dkMsg = $"Design „{Deck.Theme.Name}“."; }

    public SlideShape DeckInsert(ShapeKind kind)
    {
        DeckSnapshot();
        var s = kind switch
        {
            ShapeKind.Text => new SlideShape { Kind = kind, X = 8, Y = 8, W = 16, H = 3, Text = "Neuer Text", FontSize = 24 },
            ShapeKind.Rechteck => new SlideShape { Kind = kind, X = 12, Y = 7, W = 9, H = 5, Fill = Deck.Theme.Accent, Color = "#FFFFFF", Text = "", Align = Align.Center, FontSize = 20 },
            ShapeKind.Ellipse => new SlideShape { Kind = kind, X = 13, Y = 6.5, W = 7, H = 6, Fill = Deck.Theme.Accent, Color = "#FFFFFF", Align = Align.Center, FontSize = 20 },
            ShapeKind.Pfeil => new SlideShape { Kind = kind, X = 12, Y = 8, W = 9, H = 3, Fill = Deck.Theme.Accent },
            ShapeKind.Tabelle => new SlideShape { Kind = kind, X = 4, Y = 5, W = 26, H = 7, FontSize = 18, Table = [["Spalte 1", "Spalte 2", "Spalte 3"], ["", "", ""], ["", "", ""]] },
            ShapeKind.Diagramm => ChartFromSheet(),
            _ => new SlideShape { Kind = kind, X = 8, Y = 5, W = 16, H = 9 },
        };
        CurrentSlide.Shapes.Add(s); _dkSel = CurrentSlide.Shapes.Count - 1; DeckChanged();
        _dkMsg = kind switch { ShapeKind.Tabelle => "Tabelle eingefügt – Doppelklick zum Bearbeiten (Zellen mit | trennen, Zeilen mit Enter).", ShapeKind.Diagramm => _dkMsg, _ => "Eingefügt – ziehen zum Verschieben, Ecke unten rechts für die Größe, Doppelklick für Text." };
        return s;
    }

    /// <summary>P3: Diagramm aus der offenen Trendo Tabelle (erstes Diagramm des Blatts, sonst Beispielwerte).</summary>
    SlideShape ChartFromSheet()
    {
        var shape = new SlideShape { Kind = ShapeKind.Diagramm, X = 6, Y = 4.5, W = 22, H = 13, ChartKind = ChartKind.Balken, Text = "Diagramm", ChartLabels = ["A", "B", "C"], ChartValues = [3, 5, 2] };
        if (!SheetStartVisible && ActiveSheet.Charts.FirstOrDefault() is { } ch)
        {
            var d = ChartData.From(ActiveSheet, new SheetCalc(SheetDocument), ch.Range);
            if (d.Series.Count > 0) { shape.ChartKind = ch.Kind; shape.Text = ch.Title; shape.ChartLabels = d.Labels.ToList(); shape.ChartValues = d.Series[0].Values.ToList(); _dkMsg = $"Diagramm „{ch.Title}“ aus Trendo Tabelle übernommen."; return shape; }
        }
        _dkMsg = "Beispieldiagramm eingefügt. Tipp: Ein Diagramm in Trendo Tabelle wird hier automatisch übernommen.";
        return shape;
    }

    public void DeckInsertImage(string file)
    {
        DeckSnapshot();
        var bytes = File.ReadAllBytes(file);
        var s = new SlideShape { Kind = ShapeKind.Bild, Image = bytes, X = 6.9, Y = 4.5, W = 20, H = 12.5 };
        try { using var ms = new MemoryStream(bytes); using var bmp = new Avalonia.Media.Imaging.Bitmap(ms); s.H = Math.Min(13, s.W * bmp.PixelSize.Height / Math.Max(1.0, bmp.PixelSize.Width)); s.W = s.H * bmp.PixelSize.Width / Math.Max(1.0, bmp.PixelSize.Height); s.X = (SlideDoc.WidthCm - s.W) / 2; }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or NotSupportedException) { }
        // Bildplatzhalter des Layouts ersetzen
        if (CurrentSlide.ByRole("pic") is { } ph) { s.X = ph.X; s.Y = ph.Y; s.W = ph.W; s.H = ph.H; CurrentSlide.Shapes.Remove(ph); }
        CurrentSlide.Shapes.Add(s); _dkSel = CurrentSlide.Shapes.Count - 1; DeckChanged(); _dkMsg = $"Bild „{Path.GetFileName(file)}“ eingefügt.";
    }

    public void DeckInsertMedia(string file)
    {
        DeckSnapshot();
        CurrentSlide.Shapes.Add(new SlideShape { Kind = ShapeKind.Medien, MediaPath = file, X = 8, Y = 5, W = 18, H = 10 });
        _dkSel = CurrentSlide.Shapes.Count - 1; DeckChanged(); _dkMsg = $"„{Path.GetFileName(file)}“ wird in der Vorführung abgespielt.";
    }

    public SlideShape? DeckSelected => _dkSel >= 0 && _dkSel < CurrentSlide.Shapes.Count ? CurrentSlide.Shapes[_dkSel] : null;
    public void DeckSelect(int i) { _dkSel = i; DeckCanvas?.Select(i); }

    public void DeckSetText(int i, string text)
    {
        if (i < 0 || i >= CurrentSlide.Shapes.Count) return;
        DeckSnapshot(); var s = CurrentSlide.Shapes[i];
        if (s.Kind == ShapeKind.Tabelle) s.Table = text.Replace("\r", "").Split('\n').Select(l => l.Split('|').Select(c => c.Trim()).ToList()).ToList();
        else s.Text = text;
        if (s.Role == "pic" && text.Length == 0) CurrentSlide.Shapes.Remove(s);
        DeckChanged();
    }

    void ForShape(Action<SlideShape> change) { if (DeckSelected is not { } s) { _dkMsg = "Erst ein Objekt auf der Folie anklicken."; return; } DeckSnapshot(); change(s); DeckChanged(); }

    public void DeckDeleteShape() { if (DeckSelected is null) return; DeckSnapshot(); CurrentSlide.Shapes.RemoveAt(_dkSel); _dkSel = -1; DeckChanged(); }

    public void DeckUndo() { if (_dkUndo.Count == 0) return; _dkRedo.Add(Deck); Deck = _dkUndo[^1]; _dkUndo.RemoveAt(_dkUndo.Count - 1); AfterDeckSwap(); }
    public void DeckRedo() { if (_dkRedo.Count == 0) return; _dkUndo.Add(Deck); Deck = _dkRedo[^1]; _dkRedo.RemoveAt(_dkRedo.Count - 1); AfterDeckSwap(); }

    // ---------- Innenleben ----------
    void ResetDeckState() { _dkUndo.Clear(); _dkRedo.Clear(); DeckIndex = 0; _dkSel = -1; DeckStartVisible = false; }
    void AfterDeckSwap() { DeckIndex = Math.Min(DeckIndex, Deck.Slides.Count - 1); _dkSel = -1; DeckDirty = true; }
    void DeckSnapshot() { _dkUndo.Add(Deck.Clone()); if (_dkUndo.Count > 80) _dkUndo.RemoveAt(0); _dkRedo.Clear(); }
    void DeckChanged() { DeckDirty = true; DeckCanvas?.InvalidateVisual(); }

    string DeckRecentFile => Path.Combine(_core.DataRoot, "praesentation", "zuletzt.txt");
    List<string> DeckRecent() => File.Exists(DeckRecentFile) ? File.ReadAllLines(DeckRecentFile).Where(File.Exists).Take(8).ToList() : [];
    void RememberDeckRecent(string path)
    {
        var list = DeckRecent().Where(p => !p.Equals(path, StringComparison.OrdinalIgnoreCase)).Prepend(Path.GetFullPath(path)).Take(8).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(DeckRecentFile)!); File.WriteAllLines(DeckRecentFile, list);
    }

    string DefaultDeckName(string ext)
    {
        var dir = Path.Combine(ProjectsRoot, "Präsentationen"); Directory.CreateDirectory(dir);
        var title = Deck.Slides.FirstOrDefault()?.Title is { Length: > 0 } t ? t : "Präsentation";
        var safe = string.Concat(title.Take(40).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c)).Trim();
        var f = Path.Combine(dir, safe + ext);
        for (var i = 2; File.Exists(f); i++) f = Path.Combine(dir, $"{safe} ({i}){ext}");
        return f;
    }

    async Task<string?> PickDeck(bool save)
    {
        var req = new PickRequest(save ? "praesentation-save" : "praesentation-open", false, false, [.. SlideFiles.Extensions]);
        if (PickOverride is { } fake) return (await fake(req)) is [var f, ..] ? f : null;
        if (save)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Speichern unter", SuggestedFileName = Path.GetFileName(DefaultDeckName(".pptx")), DefaultExtension = "pptx",
                FileTypeChoices = [new FilePickerFileType("PowerPoint-Präsentation") { Patterns = ["*.pptx"] }, new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }] });
            return file?.TryGetLocalPath();
        }
        var got = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Präsentation öffnen", FileTypeFilter = [new FilePickerFileType("Präsentationen") { Patterns = ["*.pptx", "*.odp"] }] });
        return got.Select(f => f.TryGetLocalPath()).OfType<string>().FirstOrDefault();
    }

    async Task<string?> PickMediaFile(string field, string title, string[] patterns)
    {
        var req = new PickRequest(field, false, false, patterns.Select(p => p.TrimStart('*')).ToList());
        if (PickOverride is { } fake) return (await fake(req)) is [var f, ..] ? f : null;
        var got = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, FileTypeFilter = [new FilePickerFileType(title) { Patterns = patterns }] });
        return got.Select(f => f.TryGetLocalPath()).OfType<string>().FirstOrDefault();
    }

    async Task DeckSaveClick(bool asNew)
    {
        var path = !asNew && DeckPath is not null ? DeckPath : await PickDeck(true);
        if (path is null) return;
        try { DeckSaveAs(path); Toast($"Gespeichert: {Path.GetFileName(path)}", ("Ordner öffnen", new Action(() => Launch(Path.GetDirectoryName(path)!)))); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _dkMsg = "Speichern ging nicht: " + ex.Message; }
        Show("praesentation");
    }

    async Task DeckOpenClick()
    {
        var path = await PickDeck(false); if (path is null) return;
        try { DeckOpen(path); } catch (Exception ex) when (ex is IOException or InvalidDataException or System.Xml.XmlException) { _dkMsg = "Öffnen ging nicht: " + ex.Message; }
        Show("praesentation");
    }

    public string DeckExportPdf(bool notes = false)
    {
        var f = DefaultDeckName(notes ? " – Handzettel.pdf" : ".pdf"); SlidePdf.Export(Deck, f, notes);
        _dkMsg = $"PDF gespeichert: {Path.GetFileName(f)}";
        _core.Audit.Append(Principal.LocalOwner.Name, "praesentation.pdf", Path.GetFileName(f));
        Toast(_dkMsg, ("Öffnen", new Action(() => Launch(f))), ("Ordner öffnen", new Action(() => Launch(Path.GetDirectoryName(f)!))));
        return f;
    }

    /// <summary>P6: jede Folie als PNG (1920 × 1080) in einen Ordner neben den Präsentationen.</summary>
    public string DeckExportImages(string? folder = null)
    {
        var dir = folder ?? DefaultDeckName(" – Bilder"); Directory.CreateDirectory(dir);
        for (var i = 0; i < Deck.Slides.Count; i++) RenderSlidePng(Deck.Slides[i], Path.Combine(dir, $"Folie-{i + 1:00}.png"));
        _dkMsg = $"{Deck.Slides.Count} Bilder gespeichert: {Path.GetFileName(dir)}";
        _core.Audit.Append(Principal.LocalOwner.Name, "praesentation.bilder", $"{Deck.Slides.Count}");
        return dir;
    }

    void RenderSlidePng(Slide s, string path, int w = 1920, int h = 1080)
    {
        using var bmp = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
        using (var ctx = bmp.CreateDrawingContext()) SlideView.Paint(ctx, Deck, s, new Rect(0, 0, w, h), w / SlideDoc.WidthCm);
        bmp.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }

    /// <summary>FFmpeg-Aufruf: Bilder Folie-01.png … als Video, je Folie <paramref name="seconds"/> Sekunden, H.264 für alle Player.</summary>
    public static string VideoArgs(string folder, string output, int seconds) =>
        $"-y -framerate 1/{Math.Clamp(seconds, 1, 60)} -i \"{Path.Combine(folder, "Folie-%02d.png")}\" -c:v libx264 -r 30 -pix_fmt yuv420p \"{output}\"";

    /// <summary>P6: Video (MP4) aus den Folien – braucht FFmpeg (unter „Programme“ mit einem Klick installierbar).</summary>
    public async Task<string?> DeckExportVideo(int seconds = 5)
    {
        var ffmpeg = ToolPath("ffmpeg") ?? Cli.Find("ffmpeg");
        if (ffmpeg is null || !File.Exists(ffmpeg)) { _dkMsg = "Für Videos braucht Trendo FFmpeg. Unter „Programme“ kannst du es mit einem Klick installieren."; return null; }
        var tmp = Path.Combine(Path.GetTempPath(), "trendo-video-" + Guid.NewGuid().ToString("N")[..8]);
        DeckExportImages(tmp);
        var outFile = DefaultDeckName(".mp4");
        _dkMsg = "Video wird erstellt …"; Show("praesentation");
        var (exit, output) = await new RealProcessRunner().RunAsync(ffmpeg, VideoArgs(tmp, outFile, seconds));
        try { Directory.Delete(tmp, true); } catch (IOException) { }
        if (exit != 0 || !File.Exists(outFile)) { _dkMsg = "Video ging nicht: " + output.Split('\n').LastOrDefault(l => l.Trim().Length > 0); Show("praesentation"); return null; }
        _dkMsg = $"Video gespeichert: {Path.GetFileName(outFile)}";
        _core.Audit.Append(Principal.LocalOwner.Name, "praesentation.video", Path.GetFileName(outFile));
        Toast(_dkMsg, ("Abspielen", new Action(() => Launch(outFile))), ("Ordner öffnen", new Action(() => Launch(Path.GetDirectoryName(outFile)!))));
        Show("praesentation");
        return outFile;
    }

    // ---------- Oberfläche ----------
    Control DeckPageView() => new Border { Name = "praesentation-page-root", Background = Brush.Parse(C("#F2081329", "#F7F4F7FC")), CornerRadius = new CornerRadius(10), Padding = new Thickness(10), Child = DeckStartVisible ? DeckStartPage() : DeckShell() };

    /// <summary>G1: Startseite – Neu, Vorlagen, aus Stichpunkten, mit der KI, Zuletzt, Öffnen.</summary>
    Control DeckStartPage()
    {
        var sp = new StackPanel { Spacing = 14 };
        sp.Children.Add(PageHead("Trendo Präsentation"));
        sp.Children.Add(new TextBlock { Text = "Neu", Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold });
        var tiles = new WrapPanel();
        foreach (var (id, icon, name, hint) in new[] { ("leer", "▭", "Leere Präsentation", "Eine Titelfolie zum Loslegen"), ("vortrag", "🎤", "Vortrag", "Titel, Ablauf, Kernpunkte, Fragen"), ("projekt", "📊", "Projektbericht", "Ziele, Stand, Zeitplan, Risiken"), ("fotoalbum", "🖼", "Fotoalbum", "Bilder mit sanften Übergängen") })
        {
            var tid = id;
            var b = new Button { Name = "praesentation-tpl-" + id, Width = 190, Height = 132, Margin = new Thickness(0, 0, 12, 12), Padding = new Thickness(12), CornerRadius = new CornerRadius(12), Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1),
                Content = new StackPanel { Spacing = 6, Children = { new TextBlock { Text = icon, FontSize = 34 }, new TextBlock { Text = name, Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold }, new TextBlock { Text = hint, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap } } } };
            b.Click += (_, _) => { DeckNew(tid); Show("praesentation"); };
            tiles.Children.Add(b);
        }
        sp.Children.Add(tiles);
        // P7: aus Stichpunkten oder mit der KI
        var outline = new TextBox { Name = "praesentation-outline", Text = _dkOutline, AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.Wrap, MinHeight = 140, Width = 760, HorizontalAlignment = HorizontalAlignment.Left,
            PlaceholderText = "Stichpunkte – eine Zeile je Folie, darunter eingerückt die Punkte:\nUnser Sommerfest\n  - Planung für alle Nachbarn\nAblauf\n  - 14 Uhr Begrüßung\n  - 15 Uhr Kuchen" };
        outline.TextChanged += (_, _) => _dkOutline = outline.Text ?? "";
        var fromOutline = new Button { Name = "praesentation-from-outline", Content = "✨ Folien aus Stichpunkten", Background = Accent };
        fromOutline.Click += (_, _) => { if ((outline.Text ?? "").Trim().Length == 0) { _dkMsg = "Bitte erst Stichpunkte eintragen."; Show("praesentation"); return; } DeckFromOutline(outline.Text!); Show("praesentation"); };
        var topic = new TextBox { Name = "praesentation-topic", PlaceholderText = "oder ein Thema, z. B. „Energiesparen im Haushalt“", Width = 420 };
        var ai = new Button { Name = "praesentation-ai", Content = "🤖 Mit der KI entwerfen" };
        ai.Click += async (_, _) =>
        {
            if ((topic.Text ?? "").Trim().Length == 0) { _dkMsg = "Bitte ein Thema eintragen."; Show("praesentation"); return; }
            ai.IsEnabled = false; ai.Content = "Die KI entwirft …";
            try
            {
                var a = await Intel.AskAsync(new KiRequest(SlideOutline.Prompt(topic.Text!, 6), PreferredModel: KiModelName, UseKnowledge: false));
                if (string.IsNullOrWhiteSpace(a.Text)) _dkMsg = "Die KI hat nichts geliefert. Ist ein Sprachmodell in der KI-Bibliothek geladen?";
                else { _dkOutline = a.Text.Trim(); DeckFromOutline(_dkOutline); _dkMsg = $"Die KI hat {Deck.Slides.Count} Folien entworfen – bitte prüfen und anpassen."; }
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException) { _dkMsg = "Die KI ist gerade nicht erreichbar: " + ex.Message; }
            Show("praesentation");
        };
        sp.Children.Add(new TextBlock { Text = "Aus Stichpunkten oder mit der KI", Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold });
        sp.Children.Add(outline);
        sp.Children.Add(new WrapPanel { Children = { fromOutline, new Border { Width = 16 }, topic, new Border { Width = 6 }, ai } });
        if (_dkMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "praesentation-start-msg", Text = _dkMsg, Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        var open = new Button { Name = "praesentation-open", Content = "📂  Datei öffnen … (PowerPoint, Impress)", Padding = new Thickness(14, 8) };
        open.Click += (_, _) => _ = DeckOpenClick();
        sp.Children.Add(open);
        sp.Children.Add(new TextBlock { Text = "Zuletzt geöffnet", Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
        var recent = DeckRecent();
        if (recent.Count == 0) sp.Children.Add(new TextBlock { Text = "Noch nichts – deine Präsentationen erscheinen hier, sobald du sie speicherst oder öffnest.", Foreground = Muted, FontSize = 13 });
        var i = 0;
        foreach (var f in recent)
        {
            var path = f;
            var b = new Button { Name = $"praesentation-recent-{i++}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 6),
                Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { new TextBlock { Text = "▭", FontSize = 18, Foreground = Brush.Parse("#F97316") },
                    new StackPanel { Children = { new TextBlock { Text = Path.GetFileName(path), Foreground = Text, FontSize = 13 }, new TextBlock { Text = $"{Path.GetDirectoryName(path)} · {File.GetLastWriteTime(path):dd.MM.yyyy HH:mm}", Foreground = Muted, FontSize = 12 } } } } } };
            b.Click += (_, _) => { try { DeckOpen(path); } catch (Exception ex) when (ex is IOException or InvalidDataException or System.Xml.XmlException) { _dkMsg = "Öffnen ging nicht: " + ex.Message; } Show("praesentation"); };
            sp.Children.Add(b);
        }
        return new ScrollViewer { Content = sp };
    }

    Control DeckShell()
    {
        var g = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto"), ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var head = PageHead("Trendo Präsentation"); Grid.SetColumnSpan(head, 3); g.Children.Add(head);
        var ribbon = DeckRibbon(); Grid.SetRow(ribbon, 1); Grid.SetColumnSpan(ribbon, 3); g.Children.Add(ribbon);
        var strip = DeckStrip(); Grid.SetRow(strip, 2); Grid.SetRowSpan(strip, 2); g.Children.Add(strip);
        var stage = DeckStage(); Grid.SetRow(stage, 2); Grid.SetColumn(stage, 1); g.Children.Add(stage);
        var notes = new TextBox { Name = "praesentation-notes", Text = CurrentSlide.Notes, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 74, Margin = new Thickness(8, 6, 8, 0), PlaceholderText = "Notizen für dich (nur in der Referentenansicht zu sehen)" };
        notes.LostFocus += (_, _) => { if ((notes.Text ?? "") != CurrentSlide.Notes) { DeckSnapshot(); CurrentSlide.Notes = notes.Text ?? ""; DeckDirty = true; } };
        Grid.SetRow(notes, 3); Grid.SetColumn(notes, 1); g.Children.Add(notes);
        var side = DeckSide(); Grid.SetRow(side, 2); Grid.SetRowSpan(side, 2); Grid.SetColumn(side, 2); g.Children.Add(side);
        var status = new TextBlock { Name = "praesentation-status", Foreground = Muted, FontSize = 12, Margin = new Thickness(4, 6, 0, 0), TextWrapping = TextWrapping.Wrap,
            Text = $"Folie {DeckIndex + 1} von {Deck.Slides.Count} · Design „{Deck.Theme.Name}“ · {(DeckPath is null ? "Noch nicht gespeichert" : Path.GetFileName(DeckPath) + (DeckDirty ? " · geändert" : " · gespeichert"))}{(_dkMsg.Length > 0 ? " · " + _dkMsg : "")}" };
        Grid.SetRow(status, 4); Grid.SetColumnSpan(status, 3); g.Children.Add(status);
        return g;
    }

    /// <summary>Menüband-Knopf (nimmt der Folie den Fokus nicht weg).</summary>
    Button DB(string name, string icon, string label, Action go, string tip, bool on = false) => SB(name, icon, label, go, tip, on, "praesentation");

    Control DeckRibbon()
    {
        var tabRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(0, 4, 0, 4) };
        var file = new Button { Name = "praesentation-file", Content = "Datei ▾", Focusable = false, Padding = new Thickness(10, 4), Background = Accent };
        var fm = new ContextMenu();
        MenuItem Mi(string name, string header, Action a) { var m = new MenuItem { Name = name, Header = header }; m.Click += (_, _) => a(); return m; }
        fm.Items.Add(Mi("praesentation-m-start", "Startseite (Neu, Vorlagen, KI, Zuletzt)", () => { DeckStartVisible = true; Show("praesentation"); }));
        fm.Items.Add(Mi("praesentation-m-open", "Öffnen …", () => _ = DeckOpenClick()));
        fm.Items.Add(Mi("praesentation-m-save", "Speichern  (Strg+S)", () => _ = DeckSaveClick(false)));
        fm.Items.Add(Mi("praesentation-m-saveas", "Speichern unter … (PowerPoint)", () => _ = DeckSaveClick(true)));
        fm.Items.Add(Mi("praesentation-m-pdf", "Als PDF", () => { DeckExportPdf(); Show("praesentation"); }));
        fm.Items.Add(Mi("praesentation-m-handout", "Handzettel mit Notizen (PDF)", () => { DeckExportPdf(true); Show("praesentation"); }));
        fm.Items.Add(Mi("praesentation-m-images", "Als Bilder (PNG, 1920 × 1080)", () => { var d = DeckExportImages(); Toast(_dkMsg, ("Ordner öffnen", new Action(() => Launch(d)))); Show("praesentation"); }));
        fm.Items.Add(Mi("praesentation-m-video", "Als Video (MP4, 5 s je Folie)", () => _ = DeckExportVideo()));
        file.ContextMenu = fm; file.Click += (_, _) => fm.Open(file);
        tabRow.Children.Add(file);
        foreach (var (id, label) in new[] { ("start", "Start"), ("einfuegen", "Einfügen"), ("entwurf", "Entwurf"), ("uebergaenge", "Übergänge"), ("animationen", "Animationen"), ("vorfuehren", "Vorführen") })
        {
            var key = id;
            var b = new Button { Name = "praesentation-rib-" + id, Content = label, Focusable = false, Padding = new Thickness(12, 4), Background = _dkRibbon == id ? Brush.Parse("#332F86F6") : Brushes.Transparent, FontWeight = _dkRibbon == id ? FontWeight.SemiBold : FontWeight.Normal };
            b.Click += (_, _) => { _dkRibbon = key; Show("praesentation"); };
            tabRow.Children.Add(b);
        }
        var sel = DeckSelected; var groups = new WrapPanel();
        switch (_dkRibbon)
        {
            case "einfuegen":
                groups.Children.Add(RibbonGroup("Einfügen",
                    DB("praesentation-ins-text", "T", "Textfeld", () => DeckInsert(ShapeKind.Text), "Textfeld einfügen"),
                    DB("praesentation-ins-image", "🖼", "Bild", () => _ = InsertImageClick(), "Bild aus einer Datei"),
                    DB("praesentation-ins-table", "▦", "Tabelle", () => DeckInsert(ShapeKind.Tabelle), "Tabelle 3 × 3"),
                    DB("praesentation-ins-chart", "📊", "Diagramm", () => DeckInsert(ShapeKind.Diagramm), "Diagramm – aus Trendo Tabelle, wenn dort eins ist"),
                    DB("praesentation-ins-media", "🎬", "Video/Audio", () => _ = InsertMediaClick(), "Video oder Musik, wird in der Vorführung abgespielt")));
                groups.Children.Add(RibbonGroup("Formen",
                    DB("praesentation-ins-rect", "▬", "Rechteck", () => DeckInsert(ShapeKind.Rechteck), "Rechteck"),
                    DB("praesentation-ins-ellipse", "●", "Kreis", () => DeckInsert(ShapeKind.Ellipse), "Kreis/Ellipse"),
                    DB("praesentation-ins-arrow", "➜", "Pfeil", () => DeckInsert(ShapeKind.Pfeil), "Pfeil")));
                break;
            case "entwurf":
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                foreach (var th in SlideThemes.All)
                {
                    var tid = th.Id;
                    var b = new Button { Name = "praesentation-theme-" + th.Id, Focusable = false, Width = 92, Height = 54, Padding = new Thickness(0), BorderThickness = new Thickness(Deck.Theme.Id == th.Id ? 3 : 1), BorderBrush = Deck.Theme.Id == th.Id ? Accent : Edge,
                        Content = new Border { Background = Brush.Parse(th.Background), Child = new StackPanel { Margin = new Thickness(6), Children = { new TextBlock { Text = th.Name, Foreground = Brush.Parse(th.Title), FontWeight = FontWeight.Bold, FontSize = 12 }, new Border { Height = 4, Width = 40, HorizontalAlignment = HorizontalAlignment.Left, Background = Brush.Parse(th.Accent), Margin = new Thickness(0, 4, 0, 0) } } } } };
                    ToolTip.SetTip(b, $"Design „{th.Name}“");
                    b.Click += (_, _) => { DeckSetTheme(tid); Show("praesentation"); };
                    row.Children.Add(b);
                }
                groups.Children.Add(RibbonGroup("Designs", row));
                groups.Children.Add(RibbonGroup("Hintergrund", BgPicker()));
                break;
            case "uebergaenge":
                foreach (var (t, icon, label) in new[] { (Transition.Keine, "⊘", "Keiner"), (Transition.Ueberblenden, "◐", "Überblenden"), (Transition.Schieben, "⇡", "Schieben"), (Transition.Wischen, "⇥", "Wischen") })
                {
                    var tt = t;
                    groups.Children.Add(DB("praesentation-tr-" + t.ToString().ToLowerInvariant(), icon, label, () => { DeckSnapshot(); CurrentSlide.Transition = tt; DeckChanged(); _dkMsg = $"Übergang „{label}“ für diese Folie."; }, $"Übergang beim Wechsel auf diese Folie: {label}", CurrentSlide.Transition == t));
                }
                groups.Children.Add(DB("praesentation-tr-all", "⧉", "Für alle", () => { DeckSnapshot(); foreach (var s in Deck.Slides) s.Transition = CurrentSlide.Transition; DeckChanged(); _dkMsg = "Übergang für alle Folien übernommen."; }, "Diesen Übergang für alle Folien"));
                break;
            case "animationen":
                foreach (var (a, icon, label) in new[] { (Animation.Keine, "⊘", "Keine"), (Animation.Einblenden, "◐", "Einblenden"), (Animation.Hereinfliegen, "⇡", "Hereinfliegen"), (Animation.Zoomen, "⤢", "Zoomen") })
                {
                    var aa = a;
                    groups.Children.Add(DB("praesentation-an-" + a.ToString().ToLowerInvariant(), icon, label, () => ForShape(s => s.Animation = aa), $"Das gewählte Objekt erscheint in der Vorführung per Klick: {label}", sel?.Animation == a && sel is not null));
                }
                groups.Children.Add(new TextBlock { Text = sel is null ? "Erst ein Objekt auf der Folie anklicken." : $"Reihenfolge: {CurrentSlide.Shapes.Where(x => x.Animation != Animation.Keine).ToList().IndexOf(sel) + 1}. Klick", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) });
                break;
            case "vorfuehren":
                groups.Children.Add(RibbonGroup("Bildschirmpräsentation",
                    DB("praesentation-show-start", "▶", "Von Anfang", () => StartShow(0), "Vorführung ab der ersten Folie (F5)"),
                    DB("praesentation-show-here", "⏵", "Ab hier", () => StartShow(DeckIndex), "Vorführung ab dieser Folie (Umschalt+F5)"),
                    DB("praesentation-show-presenter", "🗒", "Referentenansicht", () => StartShow(DeckIndex, presenter: true), "Vorführen mit Notizen, Uhr und nächster Folie")));
                groups.Children.Add(new TextBlock { Text = "In der Vorführung: Leertaste/→ weiter, ← zurück, L Laserpointer, S schwarz, Esc beenden.", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 420 });
                break;
            default:
                groups.Children.Add(RibbonGroup("Folien", LayoutPicker("praesentation-new", "＋", "Neue Folie", l => DeckAddSlide(l)),
                    DB("praesentation-dup", "⧉", "Duplizieren", DeckDuplicate, "Folie duplizieren"),
                    DB("praesentation-del", "✕", "Löschen", DeckDelete, "Folie löschen"),
                    LayoutPicker("praesentation-layout", "▤", "Layout", DeckSetLayout)));
                groups.Children.Add(RibbonGroup("Schrift",
                    DB("praesentation-bigger", "A+", "Größer", () => ForShape(s => s.FontSize = Math.Min(120, Math.Round(s.FontSize * 1.15))), "Schrift größer"),
                    DB("praesentation-smaller", "A−", "Kleiner", () => ForShape(s => s.FontSize = Math.Max(8, Math.Round(s.FontSize / 1.15))), "Schrift kleiner"),
                    DB("praesentation-bold", "𝐅", "Fett", () => ForShape(s => s.Bold = !s.Bold), "Fett", sel?.Bold == true),
                    ColorPicker()));
                groups.Children.Add(RibbonGroup("Absatz",
                    DB("praesentation-bullets", "•≡", "Aufzählung", () => ForShape(s => s.Bullets = !s.Bullets), "Aufzählungspunkte an/aus", sel?.Bullets == true),
                    DB("praesentation-left", "⯇", "Links", () => ForShape(s => s.Align = Align.Left), "Linksbündig", sel?.Align == Align.Left && sel is not null),
                    DB("praesentation-center", "≡", "Mitte", () => ForShape(s => s.Align = Align.Center), "Zentriert", sel?.Align == Align.Center),
                    DB("praesentation-right", "⯈", "Rechts", () => ForShape(s => s.Align = Align.Right), "Rechtsbündig", sel?.Align == Align.Right)));
                groups.Children.Add(RibbonGroup("Bearbeiten",
                    DB("praesentation-undo", "↶", "Zurück", DeckUndo, "Rückgängig (Strg+Z)"),
                    DB("praesentation-redo", "↷", "Vor", DeckRedo, "Wiederholen (Strg+Y)"),
                    DB("praesentation-show", "▶", "Vorführen", () => StartShow(0), "Vorführung starten (F5)")));
                break;
        }
        return new StackPanel { Children = { tabRow, groups } };
    }

    Control LayoutPicker(string name, string icon, string label, Action<SlideLayout> pick)
    {
        var b = DB(name, icon, label, () => { }, label + " – Layout wählen");
        var menu = new ContextMenu();
        foreach (var (l, n) in SlideLayouts.Names) { var ll = l; var mi = new MenuItem { Name = $"{name}-{l.ToString().ToLowerInvariant()}", Header = n }; mi.Click += (_, _) => { pick(ll); Show("praesentation"); }; menu.Items.Add(mi); }
        b.ContextMenu = menu; b.Click += (_, _) => menu.Open(b);
        return b;
    }

    Control ColorPicker()
    {
        var b = DB("praesentation-color", "🎨", "Farbe", () => { }, "Textfarbe des Objekts");
        var menu = new ContextMenu();
        foreach (var (n, hex) in new[] { ("Design", (string?)null), ("Schwarz", "#111827"), ("Weiß", "#FFFFFF"), ("Blau", "#2563EB"), ("Rot", "#DC2626"), ("Grün", "#16A34A"), ("Orange", "#EA580C") })
        { var h = hex; var mi = new MenuItem { Header = n }; mi.Click += (_, _) => { ForShape(s => s.Color = h); Show("praesentation"); }; menu.Items.Add(mi); }
        b.ContextMenu = menu; b.Click += (_, _) => menu.Open(b);
        return b;
    }

    Control BgPicker()
    {
        var b = DB("praesentation-bg", "🖌", "Farbe", () => { }, "Hintergrund dieser Folie");
        var menu = new ContextMenu();
        foreach (var (n, hex) in new[] { ("Wie Design", (string?)null), ("Weiß", "#FFFFFF"), ("Hellgrau", "#F3F4F6"), ("Nachtblau", "#0F172A"), ("Creme", "#FFFBEB"), ("Mint", "#ECFDF5") })
        { var h = hex; var mi = new MenuItem { Header = n }; mi.Click += (_, _) => { DeckSnapshot(); CurrentSlide.Background = h; DeckChanged(); Show("praesentation"); }; menu.Items.Add(mi); }
        b.ContextMenu = menu; b.Click += (_, _) => menu.Open(b);
        return b;
    }

    async Task InsertImageClick()
    {
        var f = await PickMediaFile("praesentation-image", "Bild", ["*.png", "*.jpg", "*.jpeg"]);
        if (f is null) return;
        try { DeckInsertImage(f); } catch (IOException ex) { _dkMsg = "Bild ging nicht: " + ex.Message; }
        Show("praesentation");
    }

    async Task InsertMediaClick()
    {
        var f = await PickMediaFile("praesentation-media", "Video oder Audio", ["*.mp4", "*.mov", "*.webm", "*.mp3", "*.wav", "*.ogg"]);
        if (f is null) return;
        DeckInsertMedia(f); Show("praesentation");
    }

    /// <summary>Folienleiste links: Vorschau je Folie, Klick wählt, ▲▼ sortieren.</summary>
    Control DeckStrip()
    {
        var sp = new StackPanel { Name = "praesentation-strip", Spacing = 8, Margin = new Thickness(0, 0, 8, 0) };
        for (var i = 0; i < Deck.Slides.Count; i++)
        {
            var idx = i;
            var thumb = new SlideView { Doc = Deck, Slide = Deck.Slides[i], Width = 176, Height = 99, IsHitTestVisible = false };
            var b = new Button { Name = $"praesentation-slide-{i}", Padding = new Thickness(2), Focusable = false, BorderThickness = new Thickness(i == DeckIndex ? 3 : 1), BorderBrush = i == DeckIndex ? Accent : Edge, Background = Brushes.Transparent, Content = thumb };
            b.Click += (_, _) => { DeckGo(idx); Show("praesentation"); };
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { new TextBlock { Text = (i + 1).ToString(), Foreground = Muted, FontSize = 12, Width = 18, TextAlignment = TextAlignment.Right }, b } });
        }
        var up = DB("praesentation-up", "▲", "Nach oben", () => DeckMove(-1), "Folie nach oben (Alt+↑)");
        var down = DB("praesentation-down", "▼", "Nach unten", () => DeckMove(1), "Folie nach unten (Alt+↓)");
        var add = LayoutPicker("praesentation-add", "＋", "Folie", l => DeckAddSlide(l));
        return new DockPanel { Children = { new StackPanel { Orientation = Orientation.Horizontal, [DockPanel.DockProperty] = Avalonia.Controls.Dock.Bottom, Margin = new Thickness(18, 6, 0, 0), Children = { up, down, add } }, new ScrollViewer { Content = sp } } };
    }

    Control DeckStage()
    {
        var view = new SlideView { Name = "praesentation-canvas", Doc = Deck, Slide = CurrentSlide, Editable = true, MinHeight = 300 };
        DeckCanvas = view; view.Select(_dkSel);
        _dkOverlay = new Canvas();
        view.SelectionChanged += () => { var before = _dkSel; _dkSel = view.Selected; CloseDeckEditor(true); if (before != _dkSel) Dispatcher.UIThread.Post(() => Show("praesentation")); };
        view.Begin += DeckSnapshot;
        view.Changed += () => { DeckDirty = true; };
        view.EditRequested += OpenDeckEditor;
        view.Command += e =>
        {
            var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control); e.Handled = true;
            switch (e.Key)
            {
                case Key.Delete or Key.Back: DeckDeleteShape(); Show("praesentation"); break;
                case Key.Z when ctrl: DeckUndo(); Show("praesentation"); break;
                case Key.Y when ctrl: DeckRedo(); Show("praesentation"); break;
                case Key.D when ctrl: if (DeckSelected is { } s) { DeckSnapshot(); var c = s.Clone(); c.X += 1; c.Y += 1; CurrentSlide.Shapes.Add(c); _dkSel = CurrentSlide.Shapes.Count - 1; DeckChanged(); Show("praesentation"); } break;
                case Key.S when ctrl: _ = DeckSaveClick(false); break;
                case Key.M when ctrl: DeckAddSlide(SlideLayout.TitelInhalt); Show("praesentation"); break;
                case Key.F5: StartShow(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? DeckIndex : 0); break;
                case Key.PageDown: DeckGo(DeckIndex + 1); Show("praesentation"); break;
                case Key.PageUp: DeckGo(DeckIndex - 1); Show("praesentation"); break;
                case Key.Up when e.KeyModifiers.HasFlag(KeyModifiers.Alt): DeckMove(-1); Show("praesentation"); break;
                case Key.Down when e.KeyModifiers.HasFlag(KeyModifiers.Alt): DeckMove(1); Show("praesentation"); break;
                default: e.Handled = false; break;
            }
        };
        Dispatcher.UIThread.Post(() => view.Focus());
        return new Border { Background = Brush.Parse("#33000000"), CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Child = new Panel { Children = { view, _dkOverlay } } };
    }

    void OpenDeckEditor(int index)
    {
        if (DeckCanvas is not { } v || _dkOverlay is null || index < 0 || index >= CurrentSlide.Shapes.Count) return;
        CloseDeckEditor(true);
        var s = CurrentSlide.Shapes[index];
        if (s.Kind is ShapeKind.Bild or ShapeKind.Diagramm or ShapeKind.Medien) { _dkMsg = s.Kind == ShapeKind.Diagramm ? "Diagramme übernimmst du am einfachsten aus Trendo Tabelle." : "Ziehen zum Verschieben, Ecke unten rechts für die Größe."; return; }
        var r = v.ShapeRect(s); var k = v.Frame().K;
        var tb = new TextBox
        {
            Name = "praesentation-editor", AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.Wrap, Width = Math.Max(120, r.Width), MinHeight = Math.Max(36, r.Height),
            FontSize = Math.Max(10, s.FontSize * 2.54 / 72 * k), Background = Brushes.White, Foreground = Brushes.Black, BorderBrush = Accent,
            Text = s.Kind == ShapeKind.Tabelle ? string.Join('\n', (s.Table ?? []).Select(row => string.Join(" | ", row))) : s.Text,
        };
        Canvas.SetLeft(tb, r.X); Canvas.SetTop(tb, r.Y);
        tb.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { CloseDeckEditor(false); v.Focus(); e.Handled = true; }
            else if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control)) { CloseDeckEditor(true); v.Focus(); e.Handled = true; }
        };
        tb.LostFocus += (_, _) => { if (_dkEditor == tb) { CloseDeckEditor(true); Show("praesentation"); } };
        tb.Tag = index;
        _dkEditor = tb; _dkOverlay.Children.Add(tb);
        Dispatcher.UIThread.Post(() => { tb.Focus(); if (s.Text is "Titel" or "Untertitel" or "Neuer Text" or "Titel der Präsentation" or "Erster Punkt\nZweiter Punkt\nDritter Punkt") tb.SelectAll(); else tb.CaretIndex = tb.Text?.Length ?? 0; });
    }

    void CloseDeckEditor(bool commit)
    {
        if (_dkEditor is not { } tb) return;
        _dkEditor = null; _dkOverlay?.Children.Remove(tb);
        if (!commit || tb.Tag is not int i || i >= CurrentSlide.Shapes.Count) return;
        var s = CurrentSlide.Shapes[i];
        var old = s.Kind == ShapeKind.Tabelle ? string.Join('\n', (s.Table ?? []).Select(row => string.Join(" | ", row))) : s.Text;
        if ((tb.Text ?? "") != old) DeckSetText(i, tb.Text ?? "");
    }

    /// <summary>Für Tests: Text eines Objekts wie per Doppelklick bearbeiten.</summary>
    public void DeckType(int index, string text) { OpenDeckEditor(index); if (_dkEditor is { } tb) { tb.Text = text; CloseDeckEditor(true); } }

    /// <summary>G4: Eigenschaften des gewählten Objekts bzw. der Folie, dazu „So geht's“.</summary>
    Control DeckSide()
    {
        var sp = new StackPanel { Name = "praesentation-side", Width = 230, Spacing = 6, Margin = new Thickness(10, 0, 0, 0) };
        var s = DeckSelected;
        if (s is null)
        {
            sp.Children.Add(new TextBlock { Text = $"Folie {DeckIndex + 1}", Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold });
            sp.Children.Add(new TextBlock { Text = $"Layout: {SlideLayouts.Name(CurrentSlide.Layout)}\nÜbergang: {CurrentSlide.Transition switch { Transition.Ueberblenden => "Überblenden", Transition.Schieben => "Schieben", Transition.Wischen => "Wischen", _ => "keiner" }}\nObjekte: {CurrentSlide.Shapes.Count}", Foreground = Muted, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
        }
        else
        {
            var kind = s.Kind switch { ShapeKind.Text => s.Role switch { "title" => "Titel", "sub" => "Untertitel", "body" or "body2" => "Inhalt", _ => "Textfeld" }, ShapeKind.Bild => "Bild", ShapeKind.Tabelle => "Tabelle", ShapeKind.Diagramm => "Diagramm", ShapeKind.Medien => "Video/Audio", ShapeKind.Ellipse => "Kreis", ShapeKind.Pfeil => "Pfeil", _ => "Rechteck" };
            sp.Children.Add(new TextBlock { Name = "praesentation-side-kind", Text = kind, Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold });
            sp.Children.Add(new TextBlock { Text = $"Lage: {s.X:0.0} × {s.Y:0.0} cm\nGröße: {s.W:0.0} × {s.H:0.0} cm" + (s.Kind is ShapeKind.Text or ShapeKind.Rechteck or ShapeKind.Ellipse or ShapeKind.Tabelle ? $"\nSchrift: {s.FontSize:0} pt" : "") + (s.Animation != Animation.Keine ? $"\nAnimation: {s.Animation}" : ""), Foreground = Muted, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
            var del = DB("praesentation-shape-del", "✕", "Entfernen", DeckDeleteShape, "Objekt entfernen (Entf)");
            sp.Children.Add(del);
        }
        sp.Children.Add(new TextBlock { Text = "So geht's: Objekt anklicken, ziehen zum Verschieben, die blaue Ecke für die Größe. Doppelklick bearbeitet den Text, Tab rückt Punkte ein. F5 startet die Vorführung.", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) });
        return new ScrollViewer { Content = sp };
    }
}
