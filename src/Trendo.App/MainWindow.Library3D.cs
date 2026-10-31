using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core.Bibliothek;

namespace Trendo.App;

/// <summary>T-0279: 3D-Bibliothek – fotorealistische Modelle, Himmel (HDRI) und Materialien live aus freien Quellen (Poly Haven, ambientCG; CC0)
/// suchen und auf Klick laden, dazu Modelle aus anderen Programmen (FBX, OBJ, USD, Blender …) über Blender übernehmen. Reiter im Fenster des Modell-Prüfers.</summary>
public partial class MainWindow
{
    const string BIB = "bib3d";
    public HttpMessageHandler? BibHttpOverride { get; set; }
    static readonly HttpClient BibShared = MakeBibHttp(null);
    static HttpClient MakeBibHttp(HttpMessageHandler? h) { var c = h is null ? new HttpClient() : new HttpClient(h); c.Timeout = TimeSpan.FromSeconds(60); c.DefaultRequestHeaders.UserAgent.ParseAdd("Trendo-OS/0.9 (3D-Bibliothek)"); return c; }
    AssetLibrary Bib => new(BibHttpOverride is { } h ? MakeBibHttp(h) : BibShared, Path.Combine(_core.DataRoot, "bibliothek3d"));
    bool BibOnline => !_core.Offline && (!_opt.Isolated || BibHttpOverride is not null);

    public string BibKind = "modell";
    public string BibQuery = "";
    public List<AssetHit> BibHits = [];
    public bool BibBusy;
    private string _bibMsg = "";
    private readonly Dictionary<string, string?> _bibThumbs = [];

    private void InitLibrary3D() => RegisterTab(FachTabs, BIB, "3D-Bibliothek", "SPEC-04", BibView, "3d bibliothek", "modelle", "poly haven", "ambientcg", "hdri", "himmel", "materialien", "texturen", "fbx", "obj", "usd", "blend", "3d objekt");

    async Task BibGuard(string busy, Func<Task<string>> work)
    {
        BibBusy = true; _bibMsg = busy; Show(BIB);
        try { _bibMsg = await work(); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException or InvalidDataException or InvalidOperationException or IOException or ArgumentException
            or UnauthorizedAccessException or KeyNotFoundException or System.Text.Json.JsonException) { _bibMsg = "⚠ " + ex.Message; }
        BibBusy = false; Show(BIB);
    }

    /// <summary>Live-Suche in der gewählten Quelle; danach Vorschaubilder nachladen.</summary>
    public Task BibSearch() => BibGuard("Suche läuft …", async () =>
    {
        if (!BibOnline) throw new InvalidOperationException("Offline – die Suche braucht eine Internetverbindung. Deine Bibliothek rechts bleibt nutzbar.");
        var lib = Bib; BibHits = await lib.Search(BibQuery, BibKind);
        _bibMsg = BibHits.Count == 0 ? "Nichts gefunden – anderes Wort versuchen (Deutsch oder Englisch)." : $"{BibHits.Count} Treffer"; Show(BIB);
        foreach (var h in BibHits.Take(48).Where(h => !_bibThumbs.ContainsKey(h.Source + h.Id)))
            try { _bibThumbs[h.Source + h.Id] = await lib.Thumb(h); } catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException or IOException) { _bibThumbs[h.Source + h.Id] = null; }
        return BibHits.Count == 0 ? _bibMsg : $"{BibHits.Count} Treffer";
    });

    public Task BibDownload(AssetHit hit) => BibGuard($"„{hit.Name}“ wird geladen …", async () =>
    {
        if (!BibOnline) throw new InvalidOperationException("Offline – Laden geht erst mit Internetverbindung.");
        var item = await Bib.Download(hit);
        _core.Audit.Append(Trendo.Core.Principal.LocalOwner.Name, "bibliothek3d.download", $"{hit.Source}/{hit.Id} ({hit.License})");
        return $"✓ „{item.Name}“ liegt in deiner Bibliothek ({item.License}, {item.Source}).";
    });

    /// <summary>Modell aus einem anderen Programm übernehmen: GLB/glTF direkt, alles andere über Blender nach GLB.</summary>
    public Task BibImport(string path) => BibGuard("Modell wird übernommen …", async () =>
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Diese Datei gibt es nicht.");
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!ModelConverter.Extensions.Contains(ext)) throw new ArgumentException($"Das Format {ext} kann Trendo nicht übernehmen. Möglich: {string.Join(", ", ModelConverter.Extensions)}.");
        var glb = path;
        if (ext != ".glb")
        {
            var tmp = Path.Combine(Path.GetTempPath(), $"trendo-bib-{Guid.NewGuid():N}.glb");
            glb = await ModelConverter.ToGlb(path, tmp);
        }
        var item = Bib.AddLocal(glb, Path.GetFileName(path), Path.GetFileName(path));
        if (!ReferenceEquals(glb, path)) try { File.Delete(glb); } catch (IOException) { }
        return $"✓ „{item.Name}“ übernommen{(ext == ".glb" ? "" : " (mit Blender nach GLB umgewandelt)")}.";
    });

    private Control BibView()
    {
        Button Btn(string name, string label, Action go, bool accent = false) { var b = new Button { Name = name, Content = label, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 4, 4), IsEnabled = !BibBusy }; if (accent) { b.Background = Accent; b.Foreground = Brushes.White; } b.Click += (_, _) => go(); return b; }
        var mine = Bib.Items();

        // Kopf: Art, Suchwort, Suchen
        var bar = new WrapPanel { Name = "bib-bar" };
        foreach (var (k, label) in new[] { ("modell", "Modelle"), ("himmel", "Himmel (HDRI)"), ("material", "Materialien") })
        {
            var kind = k; var b = Btn("bib-kind-" + k, label, () => { BibKind = kind; BibHits = []; _bibMsg = ""; Show(BIB); });
            if (BibKind == k) { b.Background = Brush.Parse("#2F86F6"); b.Foreground = Brushes.White; }
            bar.Children.Add(b);
        }
        var q = Field("bib-query", BibKind == "material" ? "z. B. Holz, Fliesen, Beton" : BibKind == "himmel" ? "z. B. Abend, innen, Wolken" : "z. B. Stuhl, Pflanze, Lampe", 260, BibQuery);
        q.Margin = new Thickness(8, 0, 4, 4);
        void Go() { BibQuery = q.Text ?? ""; _ = BibSearch(); }
        OnEnter(q, Go); bar.Children.Add(q);
        bar.Children.Add(Btn("bib-search", "Suchen", Go, true));
        bar.Children.Add(new TextBlock { Text = BibKind == "material" ? "Quelle: ambientCG · CC0" : "Quelle: Poly Haven · CC0", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 4) });
        if (_bibMsg.Length > 0) bar.Children.Add(new TextBlock { Name = "bib-msg", Text = _bibMsg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 4), FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxWidth = 520, Foreground = Brush.Parse(_bibMsg.StartsWith('⚠') ? "#F87171" : _bibMsg.StartsWith('✓') ? "#4ADE80" : "#7FD3FF") });

        // Mitte: Treffer als Kacheln mit Vorschau
        Control center;
        if (BibHits.Count == 0)
            center = new Border { Name = "bib-empty", Child = new TextBlock { Text = BibOnline ? "Suchwort eingeben und „Suchen“ – alle Treffer sind frei nutzbar (CC0), Laden erst auf Klick." : "Offline – Suche und Laden brauchen eine Internetverbindung. Deine Bibliothek rechts und die Übernahme aus anderen Programmen gehen trotzdem.",
                Foreground = Muted, FontSize = 15, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        else
        {
            var grid = new WrapPanel { Name = "bib-hits" };
            var i = 0;
            foreach (var h in BibHits.Take(48))
            {
                var hit = h; var have = mine.Any(m => m.Source == h.Source && m.Id == h.Id);
                Control pic = _bibThumbs.TryGetValue(h.Source + h.Id, out var tp) && tp is not null && BibBitmap(tp) is { } bmp
                    ? new Image { Source = bmp, Width = 156, Height = 156, Stretch = Stretch.UniformToFill }
                    : new Border { Width = 156, Height = 156, Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#1B2B47"), 0), new GradientStop(Color.Parse("#0E1A2E"), 1) } },
                        Child = new TextBlock { Text = "Vorschau lädt …", Foreground = Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                var info = new StackPanel { Spacing = 2, Margin = new Thickness(0, 6, 0, 0), Children = {
                    new TextBlock { Text = h.Name, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 156 },
                    new TextBlock { Text = h.SizeCm is { } s ? $"{s[0]:0} × {s[1]:0} × {s[2]:0} cm" : h.Category, Foreground = Muted, FontSize = 11 },
                    new TextBlock { Text = $"{h.License} · {h.Author}", Foreground = Muted, FontSize = 10.5, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 156 } } };
                var act = have ? (Control)new TextBlock { Name = $"bib-have-{i}", Text = "✓ in der Bibliothek", Foreground = Brush.Parse("#4ADE80"), FontSize = 11.5, Margin = new Thickness(0, 4, 0, 0) }
                    : Btn($"bib-get-{i}", "Laden", () => { _ = BibDownload(hit); }, true);
                grid.Children.Add(new Border { Name = $"bib-hit-{i}", Width = 176, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(10), CornerRadius = new CornerRadius(12), Background = Brush.Parse("#14233B"), BorderBrush = EdgeLit, BorderThickness = new Thickness(1),
                    Child = new StackPanel { Children = { new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = pic }, info, act } } });
                i++;
            }
            var more = BibHits.Count > 48 ? new TextBlock { Text = $"… und {BibHits.Count - 48} weitere – Suchwort genauer fassen.", Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 4, 0, 0) } : null;
            var sp = new StackPanel { Children = { grid } }; if (more is not null) sp.Children.Add(more);
            center = new ScrollViewer { Content = sp };
        }
        var mid = new Border { CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(12), Child = center };
        var right = new Border { Name = "bib-side", Width = 360, CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(12), Child = new ScrollViewer { Content = BibSide(mine, Btn) } };
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,Auto"), Children = { mid, Pos(right, 2) } };
        var root = new DockPanel { Name = "bib-root" }; DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top); root.Children.Add(bar); root.Children.Add(body);
        return FitPage("3D-Bibliothek", new Panel(), root);
    }

    StackPanel BibSide(List<LibraryItem> mine, Func<string, string, Action, bool, Button> btn)
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(H($"Meine Bibliothek ({mine.Count})", 13));
        if (mine.Count == 0) sp.Children.Add(P("Noch leer – links suchen und laden oder unten ein Modell aus einem anderen Programm übernehmen.", null, 11.5));
        var k = 0;
        foreach (var it in mine.OrderByDescending(m => m.Added))
        {
            var item = it; var row = new WrapPanel();
            if (it.Kind == "modell") row.Children.Add(btn($"bib-open-{k}", "Ansehen", () => { XrFile = item.File; _xrMsg = $"„{item.Name}“ aus der 3D-Bibliothek."; Show("xr"); }, false));
            row.Children.Add(btn($"bib-del-{k}", "Entfernen", () => { Bib.Remove(item); _bibMsg = $"„{item.Name}“ entfernt."; Show(BIB); }, false));
            sp.Children.Add(new Border { Name = $"bib-item-{k}", Background = Brush.Parse("#14233B"), CornerRadius = new CornerRadius(8), Padding = new Thickness(8), Child = new StackPanel { Spacing = 2, Children = {
                new TextBlock { Text = it.Name, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = (it.Kind switch { "himmel" => "Himmel", "material" => "Material", _ => "Modell" }) + $" · {it.Source} · {it.License}" + (it.Author.Length > 0 ? " · " + it.Author : ""), Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap },
                row } } });
            k++;
        }
        sp.Children.Add(H("Aus anderen Programmen", 13));
        sp.Children.Add(P("FBX, OBJ, USD/USDZ, Alembic, PLY, STL, glTF oder eine Blender-Datei – Trendo wandelt sie mit Blender nach GLB um (Meter, Materialien und Texturen eingebettet).", null, 11.5));
        var file = Field("bib-import-path", "z. B. sessel.fbx", 330); sp.Children.Add(file);
        sp.Children.Add(btn("bib-import", "Übernehmen", () => { try { _ = BibImport(Need(file, "die Datei")); } catch (ArgumentException ex) { _bibMsg = "⚠ " + ex.Message; Show(BIB); } }, true));
        if (ModelConverter.FindBlender() is null) sp.Children.Add(P("Blender ist nicht eingerichtet – GLB und glTF gehen trotzdem; für andere Formate Blender unter Programme einrichten.", Brush.Parse("#FACC15"), 11));
        if (mine.Count > 0)
        {
            sp.Children.Add(H("Namensnennung", 13));
            sp.Children.Add(new TextBox { Name = "bib-credits", Text = Bib.Credits(), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontSize = 11, MaxHeight = 140 });
        }
        return sp;
    }

    static Avalonia.Media.Imaging.Bitmap? BibBitmap(string path)
    {
        try { return File.Exists(path) ? new Avalonia.Media.Imaging.Bitmap(path) : null; }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or InvalidOperationException) { return null; }
    }
}
