using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Wunsch 03.11.2026: Leiste unten viel fotorealistischer und schöner. Fotorealistische Symbole (lokal mit FLUX.2 erzeugt, tools/dock_bilder.py)
/// auf Glas-Kacheln mit Lichtkante, Goldrand und Schatten; WhatsApp, Telegram & Co. mit ihrem Original-Symbol von der Seite des Dienstes.</summary>
public partial class MainWindow
{
    static readonly Dictionary<string, Bitmap?> _dockArt = [];
    readonly StackPanel _dockWeb = new() { Name = "dock-webapps", Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
    /// <summary>Tests: Symbol-Abruf ersetzen (Adresse → Bytes oder null).</summary>
    public Func<Uri, Task<byte[]?>>? WebIconFetchOverride { get; set; }

    /// <summary>Fotorealistisches Dock-Bild aus den Assets (oder null, wenn es keins gibt).</summary>
    static Bitmap? DockArt(string id)
    {
        if (_dockArt.TryGetValue(id, out var b)) return b;
        try { using var s = AssetLoader.Open(new Uri($"avares://TrendoOS/Assets/dock/{id}.png")); b = new Bitmap(s); }
        catch (Exception e) when (e is FileNotFoundException or InvalidOperationException or IOException or ArgumentException) { b = null; }
        return _dockArt[id] = b;
    }

    /// <summary>Glas-Kachel: Inhalt (Foto oder Zeichen), Lichtkante oben, feiner Goldrand, weicher Schatten.</summary>
    Control DockTile(Control content, double size = 46, bool photo = true)
    {
        var r = new CornerRadius(size * 0.26);
        var gloss = new Border
        {
            CornerRadius = r, IsHitTestVisible = false,
            Background = new LinearGradientBrush { StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0.5, 0.55, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#40FFFFFF"), 0), new GradientStop(Color.Parse("#10FFFFFF"), 0.45), new GradientStop(Color.Parse("#00FFFFFF"), 1) } },
        };
        var rim = new Border
        {
            CornerRadius = r, IsHitTestVisible = false, BorderThickness = new Thickness(1),
            BorderBrush = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#CCF3D58A"), 0), new GradientStop(Color.Parse("#40B8862B"), 0.5), new GradientStop(Color.Parse("#99F3D58A"), 1) } },
        };
        var bg = new Border
        {
            CornerRadius = r, ClipToBounds = true, Child = content,
            Background = photo ? Brushes.Black : new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#FF2E2E33"), 0), new GradientStop(Color.Parse("#FF121214"), 1) } },
        };
        return new Border
        {
            Width = size, Height = size, CornerRadius = r, BoxShadow = BoxShadows.Parse("0 6 14 0 #A0000000"),
            Child = new Grid { Children = { bg, gloss, rim } },
        };
    }

    /// <summary>Kachel für einen Dock-Eintrag: Foto, wenn es eins gibt, sonst das Zeichen auf dunklem Glas.</summary>
    Control DockFace(string id, Control glyph) =>
        DockArt(id) is { } art
            ? DockTile(new Image { Source = art, Stretch = Stretch.UniformToFill, RenderTransformOrigin = RelativePoint.Center, RenderTransform = new ScaleTransform(1.45, 1.45) }) // Objekt füllt die Kachel
            : DockTile(new Border { Padding = new Thickness(7), Child = glyph }, photo: false);

    /// <summary>Hover wie in einer echten Leiste: anheben und leicht vergrößern.</summary>
    void DockLift(Control c)
    {
        if (Motionless) return;
        c.RenderTransformOrigin = new RelativePoint(0.5, 1, RelativeUnit.Relative);
        c.Transitions = [new Avalonia.Animation.TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(150) }];
        c.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("translateY(0px) scale(1)");
        c.PointerEntered += (_, _) => c.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("translateY(-5px) scale(1.12)");
        c.PointerExited += (_, _) => c.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("translateY(0px) scale(1)");
    }

    /// <summary>Hinzugefügte Apps (WhatsApp, Telegram …) mit Original-Symbol in der Leiste; fehlt das Symbol, wird es einmal geholt.</summary>
    public void RefreshDockWebApps()
    {
        _dockWeb.Children.Clear();
        foreach (var id in WebApps.Installed(_core.DataRoot))
        {
            if (WebApps.Get(id) is not { } app) continue;
            var b = new Button { Name = "dock-app-" + id, Width = 48, Height = 48, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Content = WebAppTile(app, 46) };
            ToolTip.SetTip(b, app.Name); Avalonia.Automation.AutomationProperties.SetName(b, app.Name);
            ToolTip.SetPlacement(b, Avalonia.Controls.PlacementMode.Top); ToolTip.SetVerticalOffset(b, -6);
            var aid = id; b.Click += (_, _) => { if (!OpenWebApp(aid) && _hubMsg.Length > 0) Toast(_hubMsg); };
            DockLift(b); _dockWeb.Children.Add(b);
            // Symbol einmal holen – Tests (abgeschottet) gehen nie von selbst ins Netz
            if (WebAppIcons.Cached(_core.DataRoot, id) is null && !_core.Offline && (!_opt.Isolated || WebIconFetchOverride is not null)) _ = FetchWebAppIcon(id);
        }
        _dockWeb.IsVisible = _dockWeb.Children.Count > 0;
    }

    /// <summary>Kachel einer App: Original-Symbol, sonst Farbverlauf mit Zeichen.</summary>
    Control WebAppTile(WebApp a, double size)
    {
        if (WebAppIcons.Cached(_core.DataRoot, a.Id) is { } f)
            try { return DockTile(new Image { Source = new Bitmap(f), Stretch = Stretch.UniformToFill }, size); }
            catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or NotSupportedException) { }
        return DockTile(WebAppGlyph(a, size * 0.5), size);
    }

    /// <summary>Original-Symbol von der Seite des Dienstes holen: Link-Tags, Manifest, apple-touch-icon, favicon – nur von den eigenen Domains des Dienstes.</summary>
    public async Task<string?> FetchWebAppIcon(string id)
    {
        if (WebApps.Get(id) is not { } app || _core.Offline) return null;
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130 Safari/537.36");
        async Task<byte[]?> Get(Uri u)
        {
            if (!WebAppIcons.Allowed(id, u)) return null;
            if (WebIconFetchOverride is { } o) return await o(u);
            try { using var r = await http.GetAsync(u); return r.IsSuccessStatusCode && WebAppIcons.Allowed(id, r.RequestMessage?.RequestUri ?? u) ? await r.Content.ReadAsByteArrayAsync() : null; }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException) { return null; }
        }
        var start = new Uri(app.Url);
        var cands = new List<Uri>();
        if (await Get(start) is { } html)
        {
            var (icons, manifest) = WebAppIcons.FromHtml(System.Text.Encoding.UTF8.GetString(html), start);
            cands.AddRange(icons);
            if (manifest is not null && await Get(manifest) is { } mj) cands.AddRange(WebAppIcons.FromManifest(System.Text.Encoding.UTF8.GetString(mj), manifest));
        }
        if (cands.Count == 0 && await Get(new Uri(start, "/manifest.json")) is { } m2) cands.AddRange(WebAppIcons.FromManifest(System.Text.Encoding.UTF8.GetString(m2), new Uri(start, "/manifest.json")));
        cands.AddRange(WebAppIcons.Fallbacks(start));
        // Bestes Bild: das größte gültige unter den ersten Kandidaten
        byte[]? best = null; var bestScore = 0;
        foreach (var u in cands.Distinct().Take(6))
        {
            if (await Get(u) is not { } data || WebAppIcons.ImageKind(data) is null) continue;
            var (w, _) = XrPlus.ImageSize(data); var score = w > 0 ? w * 100 : data.Length / 40;
            if (score > bestScore) { best = data; bestScore = score; }
            if (w >= 180) break;
        }
        if (best is null) return null;
        var file = WebAppIcons.Store(_core.DataRoot, id, best);
        _core.Audit.Append(Principal.LocalOwner.Name, "app.icon", $"{id} {Path.GetFileName(file)}");
        _ui.Post(() => { RefreshDockWebApps(); });
        return file;
    }
}
