using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Trendo.Core;

namespace Trendo.App;

/// <summary>X-03: Baukasten ohne Programmieren – als App-Designer (Masterprompt 131, Runde 7): Bausteine links per Ziehen (oder Klick) in die
/// Live-Vorschau legen, in der Vorschau anklicken und ziehen, rechts Eigenschaften einstellen; Vorschau als Handy oder Bildschirm, Rückgängig,
/// im Browser ausprobieren und als ZIP exportieren.</summary>
public partial class MainWindow
{
    public string BuilderFolder => WorkFolder("Baukasten");
    public string BuilderFile => Path.Combine(BuilderFolder, "seite.json");
    private SitePage? _page;
    public SitePage BuilderPage => _page ??= LoadBuilder();
    public int BuilderSel { get; set; } = -1;
    public bool BuilderPhone { get; set; } = true;
    readonly Stack<string> _builderUndo = new();
    public int BuilderUndoCount => _builderUndo.Count;

    private void InitW4Builder() => RegisterTab(KreativTabs, "baukasten", "Baukasten", "X-03", BuilderView, "no-code", "webseite", "homepage", "baukasten", "ohne programmieren", "app-designer", "app bauen");

    private void BuilderSave() { PersistBuilder(); Show("baukasten"); }

    private void ReloadBuilder() => _page = LoadBuilder();

    private SitePage LoadBuilder()
    {
        var project = NewestProject();
        if (project is not null)
        {
            try
            {
                var full = _core.Projects.ResolveInProject(project, "baukasten/seite.json");
                if (File.Exists(full)) return SitePage.Load(full);
            }
            catch (Exception ex) when (ex is ProjectIsolationException or IOException) { }
        }
        return File.Exists(BuilderFile) ? SitePage.Load(BuilderFile) : new SitePage { Blocks = [new("h1", "Willkommen"), new("p", "Schreib hier etwas über dich.")] };
    }

    private void PersistBuilder()
    {
        var page = BuilderPage;
        var project = NewestProject();
        if (project is null) { page.Save(BuilderFile); return; }
        var tmp = Path.Combine(Path.GetTempPath(), "trendo-seite-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            page.Save(tmp);
            _core.Projects.WriteFile(project, "baukasten/seite.json", File.ReadAllText(tmp));
            page.Save(BuilderFile);
        }
        finally { try { File.Delete(tmp); } catch (IOException) { } }
    }

    public string BuilderPublish()
    {
        var p = BuilderPage.Publish(Path.Combine(BuilderFolder, "fertig"), BuilderFolder);
        _core.Audit.Append(Principal.LocalOwner.Name, "builder.publish", $"{BuilderPage.Blocks.Count} Bausteine");
        return p;
    }

    /// <summary>Änderung als ein Schritt im Rückgängig, danach speichern und neu zeichnen.</summary>
    void BuilderChange(Action<SitePage> change)
    {
        var pg = BuilderPage; _builderUndo.Push(System.Text.Json.JsonSerializer.Serialize(pg));
        change(pg); BuilderSave();
    }
    public void BuilderUndo()
    {
        if (!_builderUndo.TryPop(out var json)) return;
        _page = System.Text.Json.JsonSerializer.Deserialize<SitePage>(json) ?? new(); BuilderSel = Math.Min(BuilderSel, _page.Blocks.Count - 1); BuilderSave();
    }

    static string BuilderDefault(string kind) => kind switch { "h1" => "Überschrift", "p" => "Neuer Text", "button" => "Mehr erfahren", "list" => "Punkt eins\nPunkt zwei", "contact" => "Schreib mir", "img" => "Bild", _ => "" };

    /// <summary>Baustein an einer Stelle einfügen (Ziehen aus der Leiste; öffentlich für Tests).</summary>
    public void BuilderInsert(string kind, int index)
    {
        var at = Math.Clamp(index, 0, BuilderPage.Blocks.Count);
        BuilderSel = at; BuilderChange(pg => pg.Blocks.Insert(at, new(kind, BuilderDefault(kind))));
    }
    /// <summary>Baustein in der Vorschau an eine andere Stelle ziehen (öffentlich für Tests).</summary>
    public void BuilderMove(int from, int to)
    {
        var n = BuilderPage.Blocks.Count; if (from < 0 || from >= n || to < 0 || to >= n || from == to) return;
        BuilderSel = to; BuilderChange(pg => { var b = pg.Blocks[from]; pg.Blocks.RemoveAt(from); pg.Blocks.Insert(to, b); });
    }

    /// <summary>Fertige Seite als ZIP zum Hochladen (index.html und Bilder).</summary>
    public string BuilderExport()
    {
        var dir = Path.GetDirectoryName(BuilderPublish())!;
        var stem = Path.Combine(BuilderFolder, DevStudio.SafeName(BuilderPage.Title.Length > 0 ? BuilderPage.Title : "Seite")); var zip = stem + ".zip";
        for (var k = 2; File.Exists(zip); k++) zip = $"{stem} ({k}).zip";
        System.IO.Compression.ZipFile.CreateFromDirectory(dir, zip);
        _core.Audit.Append(Principal.LocalOwner.Name, "builder.export", Path.GetFileName(zip));
        return zip;
    }

    /// <summary>Welcher Vorschau-Baustein liegt unter dem Zeiger? -1 = keiner; bei Leerraum unten = Ende.</summary>
    int BuilderHit(PointerEventArgs e)
    {
        var hit = this.InputHitTest(e.GetPosition(this)) as Visual;
        while (hit is not null && !(hit is Control { Tag: int } c && c.Name?.StartsWith("builder-block-") == true)) { if (hit is Control { Name: "builder-preview-frame" }) return BuilderPage.Blocks.Count; hit = hit.GetVisualParent(); }
        return hit is Control { Tag: int i } ? i : -1;
    }

    private Control BuilderView()
    {
        const string V = "baukasten";
        ReloadBuilder();
        var pg = BuilderPage;
        if (BuilderSel >= pg.Blocks.Count) BuilderSel = pg.Blocks.Count - 1;
        var acc = Brush.Parse(System.Text.RegularExpressions.Regex.IsMatch(pg.Accent, "^#[0-9A-Fa-f]{6}$") ? pg.Accent : "#2F86F6");

        // ── links: Bausteine (ziehen oder klicken) ──
        var palette = new StackPanel { Name = "builder-palette", Spacing = 4, Children = { H("Bausteine", 13), P("In die Vorschau ziehen oder antippen", Muted, 11) } };
        foreach (var (k, l) in SitePage.Kinds)
        {
            var kind = k;
            var b = new Button { Name = "builder-add-" + k, Content = "+ " + l, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 6), Cursor = new Cursor(StandardCursorType.DragMove) };
            Point? from = null; var dragged = false;
            b.AddHandler(PointerPressedEvent, (_, e) => { from = e.GetPosition(this); dragged = false; }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            b.AddHandler(PointerMovedEvent, (_, e) => { if (from is { } f && (Math.Abs(e.GetPosition(this).Y - f.Y) > 10 || Math.Abs(e.GetPosition(this).X - f.X) > 10)) { dragged = true; b.Opacity = 0.6; } }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            b.AddHandler(PointerReleasedEvent, (_, e) =>
            {
                if (from is null) return; from = null; b.Opacity = 1;
                if (!dragged) return; // Klick → Click-Ereignis
                e.Handled = true; var at = BuilderHit(e); if (at >= 0) BuilderInsert(kind, at);
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            b.Click += (_, _) => { if (!dragged) BuilderInsert(kind, BuilderSel >= 0 ? BuilderSel + 1 : pg.Blocks.Count); };
            palette.Children.Add(b);
        }

        // ── Mitte: Live-Vorschau ──
        var blocks = new StackPanel { Spacing = 10 };
        for (var i = 0; i < pg.Blocks.Count; i++)
        {
            var idx = i; var b = pg.Blocks[i];
            Control inner = b.Kind switch
            {
                "h1" => new TextBlock { Text = b.Text, Foreground = acc, FontSize = 26, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap },
                "p" => new TextBlock { Text = b.Text, Foreground = Brush.Parse("#1B2333"), FontSize = 14, TextWrapping = TextWrapping.Wrap, LineHeight = 21 },
                "button" => new Border { Background = acc, CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 9), HorizontalAlignment = HorizontalAlignment.Left, Child = new TextBlock { Text = b.Text, Foreground = Brushes.White, FontWeight = FontWeight.SemiBold } },
                "contact" => new Border { BorderBrush = acc, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 8), HorizontalAlignment = HorizontalAlignment.Left, Child = new TextBlock { Text = "✉ " + b.Text, Foreground = acc, FontWeight = FontWeight.SemiBold } },
                "list" => new StackPanel { Spacing = 3, Children = { } },
                "hr" => new Border { Height = 1, Background = Brush.Parse("#D5DAE3"), Margin = new Thickness(0, 6) },
                "img" => BuilderImage(b),
                _ => new TextBlock { Text = b.Text, Foreground = Brush.Parse("#1B2333") },
            };
            if (b.Kind == "list" && inner is StackPanel lp) foreach (var line in b.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries)) lp.Children.Add(new TextBlock { Text = "•  " + line.Trim(), Foreground = Brush.Parse("#1B2333"), FontSize = 14, TextWrapping = TextWrapping.Wrap });
            var on = idx == BuilderSel;
            var cell = new Border { Name = $"builder-block-{i}", Tag = idx, Padding = new Thickness(8, 6), CornerRadius = new CornerRadius(6), Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.Hand),
                BorderBrush = on ? acc : Brushes.Transparent, BorderThickness = new Thickness(on ? 2 : 1), Child = inner };
            Point? from = null; var dragged = false;
            cell.PointerPressed += (_, e) => { from = e.GetPosition(this); dragged = false; e.Pointer.Capture(cell); e.Handled = true; };
            cell.PointerMoved += (_, e) => { if (from is { } f && Math.Abs(e.GetPosition(this).Y - f.Y) > 8) { dragged = true; cell.Opacity = 0.55; } };
            cell.PointerReleased += (_, e) =>
            {
                if (from is null) return; from = null; e.Pointer.Capture(null); cell.Opacity = 1;
                if (!dragged) { BuilderSel = idx; Show(V); return; }
                var to = BuilderHit(e); if (to >= 0) BuilderMove(idx, Math.Min(to, pg.Blocks.Count - 1));
            };
            ToolTip.SetTip(cell, "Antippen: einstellen · ziehen: umsortieren");
            blocks.Children.Add(cell);
        }
        if (pg.Blocks.Count == 0) blocks.Children.Add(new TextBlock { Text = "Zieh einen Baustein hierher.", Foreground = Brush.Parse("#8A94A6"), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40) });
        var frame = new Border { Name = "builder-preview-frame", Width = BuilderPhone ? 390 : 760, MinHeight = 520, Background = Brushes.White, CornerRadius = new CornerRadius(BuilderPhone ? 28 : 10),
            BorderBrush = Brush.Parse("#3E3E3E"), BorderThickness = new Thickness(BuilderPhone ? 10 : 2), Padding = new Thickness(18, 22), HorizontalAlignment = HorizontalAlignment.Center, BoxShadow = CardShadow,
            Child = new StackPanel { Spacing = 10, Children = { new TextBlock { Text = pg.Title, Foreground = Brush.Parse("#8A94A6"), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center }, blocks } } };
        var undo = Act("builder-undo", "↶ Rückgängig", BuilderUndo); undo.IsEnabled = _builderUndo.Count > 0;
        var toolbar = new WrapPanel { Children = {
            Act("builder-view-phone", "📱 Handy", () => { BuilderPhone = true; Show(V); }, BuilderPhone), new Border { Width = 4 },
            Act("builder-view-desk", "🖥 Bildschirm", () => { BuilderPhone = false; Show(V); }, !BuilderPhone), new Border { Width = 12 }, undo, new Border { Width = 12 },
            Act("builder-preview", "▶ Ausprobieren im Browser", () => { PersistBuilder(); Launch(BuilderPublish()); }, true), new Border { Width = 4 },
            Act("builder-export", "Exportieren (ZIP)", () => Try(V, () => { var z = BuilderExport(); Say("Exportiert: " + z, V); })), new Border { Width = 4 },
            Act("builder-folder", "Ordner mit fertiger Seite", () => { BuilderPublish(); Launch(Path.Combine(BuilderFolder, "fertig")); }) } };
        var center = new StackPanel { Spacing = 10, Children = { toolbar, new ScrollViewer { Content = frame, MaxHeight = 760, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto } } };

        // ── rechts: Eigenschaften ──
        var props = new StackPanel { Name = "builder-props", Spacing = 6, Children = { H("Seite", 13) } };
        var title = new TextBox { Name = "builder-title", Text = pg.Title };
        void CommitTitle() { if ((title.Text ?? "") != pg.Title) BuilderChange(p => p.Title = title.Text ?? ""); }
        title.LostFocus += (_, _) => CommitTitle(); OnEnter(title, CommitTitle);
        props.Children.Add(P("Titel", Muted, 11)); props.Children.Add(title);
        var swatches = new WrapPanel { Name = "builder-accent" };
        foreach (var c in new[] { "#2F86F6", "#C8A04A", "#22B35E", "#E5484D", "#7C4DFF", "#1B2333" })
        {
            var col = c;
            var sw = new Button { Name = "builder-accent-" + c.TrimStart('#'), Width = 26, Height = 26, Margin = new Thickness(0, 0, 4, 4), CornerRadius = new CornerRadius(13), Background = Brush.Parse(c),
                BorderBrush = Brushes.White, BorderThickness = new Thickness(string.Equals(pg.Accent, c, StringComparison.OrdinalIgnoreCase) ? 3 : 0) };
            ToolTip.SetTip(sw, "Akzentfarbe"); sw.Click += (_, _) => BuilderChange(p => p.Accent = col);
            swatches.Children.Add(sw);
        }
        props.Children.Add(P("Akzentfarbe", Muted, 11)); props.Children.Add(swatches);
        if (BuilderSel >= 0 && BuilderSel < pg.Blocks.Count)
        {
            var idx = BuilderSel; var b = pg.Blocks[idx];
            var label = SitePage.Kinds.FirstOrDefault(x => x.Kind == b.Kind).Label ?? b.Kind;
            props.Children.Add(H($"Baustein: {label}", 13));
            if (b.Kind != "hr")
            {
                var t = new TextBox { Name = $"builder-text-{idx}", Text = b.Text, AcceptsReturn = b.Kind is "p" or "list", TextWrapping = TextWrapping.Wrap, MinHeight = b.Kind is "p" or "list" ? 80 : 0, PlaceholderText = b.Kind == "img" ? "Beschreibung des Bildes" : "Text" };
                void Commit() { if ((t.Text ?? "") != pg.Blocks[idx].Text) BuilderChange(p => p.Blocks[idx] = p.Blocks[idx] with { Text = t.Text ?? "" }); }
                t.LostFocus += (_, _) => Commit(); OnEnter(t, Commit);
                props.Children.Add(P(b.Kind == "list" ? "Punkte (je Zeile einer)" : "Text", Muted, 11)); props.Children.Add(t);
            }
            if (b.Kind is "img" or "button" or "contact")
            {
                var u = new TextBox { Name = $"builder-url-{idx}", Text = b.Url, PlaceholderText = b.Kind == "contact" ? "deine E-Mail-Adresse" : b.Kind == "img" ? "Bilddatei im Baukasten-Ordner oder https-Adresse" : "https://…" };
                void CommitUrl() { if ((u.Text ?? "") != pg.Blocks[idx].Url) BuilderChange(p => p.Blocks[idx] = p.Blocks[idx] with { Url = u.Text ?? "" }); }
                u.LostFocus += (_, _) => CommitUrl(); OnEnter(u, CommitUrl);
                props.Children.Add(P(b.Kind == "contact" ? "E-Mail" : b.Kind == "img" ? "Bild" : "Link", Muted, 11)); props.Children.Add(u);
            }
            props.Children.Add(new WrapPanel { Children = {
                Act($"builder-up-{idx}", "▲", () => { if (idx > 0) BuilderMove(idx, idx - 1); }), new Border { Width = 4 },
                Act($"builder-down-{idx}", "▼", () => { if (idx < pg.Blocks.Count - 1) BuilderMove(idx, idx + 1); }), new Border { Width = 4 },
                Act($"builder-dup-{idx}", "Verdoppeln", () => { BuilderSel = idx + 1; BuilderChange(p => p.Blocks.Insert(idx + 1, p.Blocks[idx])); }), new Border { Width = 4 },
                Act($"builder-del-{idx}", "Entfernen", () => { BuilderSel = Math.Min(idx, BuilderPage.Blocks.Count - 2); BuilderChange(p => p.Blocks.RemoveAt(idx)); }) } });
        }
        else props.Children.Add(P("Tippe einen Baustein in der Vorschau an, um ihn einzustellen.", Muted, 11.5));

        Border Col(Control c) => new() { CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(10), VerticalAlignment = VerticalAlignment.Top, Child = c };
        var grid = new Grid { Name = "builder-designer", ColumnDefinitions = new ColumnDefinitions("200,12,*,12,280"), Children = { Col(palette), Pos(center, 2), Pos(Col(props), 4) } };
        grid.KeyDown += (_, e) => { if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control) { BuilderUndo(); e.Handled = true; } };
        return KreativPage("Baukasten", new StackPanel { Spacing = 8, Children = { P("Bau deine Webseite oder kleine App aus Bausteinen – ohne Programmieren. Die Vorschau zeigt sofort, wie es aussieht."), grid } });
    }

    Control BuilderImage(Block b)
    {
        var local = b.Url.Length > 0 && !b.Url.Contains(':') && FreeAssets.SafeRel(b.Url) ? Path.Combine(BuilderFolder, b.Url) : null;
        if (local is not null && File.Exists(local))
            try { using var s = File.OpenRead(local); return new Image { Source = Avalonia.Media.Imaging.Bitmap.DecodeToWidth(s, 720), MaxHeight = 260, Stretch = Stretch.Uniform }; }
            catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or NotSupportedException or NullReferenceException) { }
        return new Border { Height = 140, Background = Brush.Parse("#EEF1F6"), CornerRadius = new CornerRadius(8),
            Child = new TextBlock { Text = "▧  " + (b.Text.Length > 0 ? b.Text : "Bild") + (b.Url.StartsWith("https://", StringComparison.Ordinal) ? "\n(Bild aus dem Internet – im Browser sichtbar)" : ""), Foreground = Brush.Parse("#8A94A6"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center } };
    }
}
