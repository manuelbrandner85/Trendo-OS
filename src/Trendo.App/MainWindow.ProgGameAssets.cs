using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Bibliothek;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>Freie Spiele-Assets im Programm „Spiele bauen“ (G1–G3): Godot Asset Library suchen und laden, Kenney/Quaternius/OpenGameArt/itch.io über den
/// Browser und den Download-Ordner, Assets direkt als Figur, Animation, Hintergrund oder Ton verwenden, Asset-Liste mit Lizenzen, Abspann, Prüfung.</summary>
public partial class MainWindow
{
    List<GameAssets.GodotHit> _gaHits = [];
    string _gaQuery = "", _gaMsg = "", _gaSite = GameAssets.Sites[0].Name;
    DateTime _gaSince = DateTime.Now.AddHours(-1);
    /// <summary>Geladene oder übernommene Dateien, die noch einer Verwendung warten (Datei, Quelle, Lizenz, Urheber, Adresse).</summary>
    readonly List<(string File, string Source, string License, string Author, string Url)> _gaPending = [];
    public IReadOnlyList<GameAssets.GodotHit> GameAssetHits => _gaHits;
    public IReadOnlyList<string> GameAssetPending => [.. _gaPending.Select(p => p.File)];
    HttpFetch GaHttp => new(BibHttpOverride is { } h ? MakeBibHttp(h) : BibShared);
    string GameAssetDir(ProgramDocument d) => Path.Combine(_core.DataRoot, "spiele-assets", DevStudio.SafeName(d.Text("title").Length > 0 ? d.Text("title") : "spiel"));

    public async Task GameAssetSearch(string query)
    {
        _gaQuery = query;
        if (_core.Offline || (_opt.Isolated && BibHttpOverride is null)) { _gaMsg = "Offline – die Suche braucht Internet."; Show(GK); return; }
        try { _gaHits = await GameAssets.SearchGodot(GaHttp, query); _gaMsg = _gaHits.Count == 0 ? "Nichts gefunden." : $"{_gaHits.Count} Treffer in der Godot Asset Library."; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException or System.Text.Json.JsonException) { _gaMsg = "" + ex.Message; }
        Show(GK);
    }

    public async Task GameAssetGodotLoad(ProgramDocument d, GameAssets.GodotHit hit)
    {
        try
        {
            var dir = await GameAssets.DownloadGodot(GaHttp, hit, Path.Combine(GameAssetDir(d), "geladen"));
            foreach (var f in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories).Where(f => GameAssets.IsImage(f) || GameAssets.IsSound(f) || f.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)).Take(60))
                _gaPending.Add((f, "Godot Asset Library", hit.License, hit.Author, $"https://godotengine.org/asset-library/asset/{hit.Id}"));
            _core.Audit.Append(Principal.LocalOwner.Name, "spiele.asset.download", $"Godot {hit.Id} ({hit.License})");
            _gaMsg = $"„{hit.Title}“ geladen – unten die Dateien einer Verwendung zuordnen.";
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or System.Text.Json.JsonException or KeyNotFoundException) { _gaMsg = "" + ex.Message; }
        Show(GK);
    }

    /// <summary>Neue Dateien aus dem Download-Ordner (Bilder, Töne, ZIP) zur Verwendung vormerken; ZIPs werden entpackt.</summary>
    public void GameAssetPickup()
    {
        var site = GameAssets.Sites.First(s => s.Name == _gaSite);
        foreach (var f in DownloadPickup.NewFiles(BibDownloads, _gaSince, [".png", ".jpg", ".jpeg", ".webp", ".gif", ".wav", ".ogg", ".mp3", ".zip", ".glb"]))
        {
            if (_gaPending.Any(p => p.File == f)) continue;
            if (f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var dir = Path.Combine(_core.DataRoot, "spiele-assets", "entpackt", Path.GetFileNameWithoutExtension(f) + "-" + DateTime.Now.ToString("HHmmss"));
                GameAssets.Unzip(f, dir);
                foreach (var g in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories).Where(g => GameAssets.IsImage(g) || GameAssets.IsSound(g) || g.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)).Take(80))
                    _gaPending.Add((g, site.Name, site.License, "", site.Url));
            }
            else _gaPending.Add((f, site.Name, site.License, "", site.Url));
        }
        _gaMsg = _gaPending.Count == 0 ? "Noch nichts Neues im Download-Ordner." : $"{_gaPending.Count} Datei(en) bereit – Verwendung wählen.";
        Show(GK);
    }

    /// <summary>G2: Datei im Spiel verwenden (Figur, Animation, Hintergrund, Ton) und in die Asset-Liste eintragen (öffentlich für Tests und Claude).</summary>
    public void GameUseAsset(ProgramDocument d, string file, string source, string license, string author, string url, string use)
    {
        // ein einziger Schritt im Rückgängig: Eintrag in der Asset-Liste und die Verwendung im Spiel
        var frames = 1;
        if (use.StartsWith("anim-", StringComparison.Ordinal))
        {
            frames = GameAssets.ImageSize(file) is { } sz ? GameAssets.FramesOf(sz.W, sz.H) : 1;
            if (GameAssets.ImageSize(file) is null) try { using var bmp = new Avalonia.Media.Imaging.Bitmap(file); frames = GameAssets.FramesOf(bmp.PixelSize.Width, bmp.PixelSize.Height); } catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or InvalidOperationException) { }
        }
        d.Edit("Asset verwenden", x =>
        {
            var target = GameAssets.Add(x, GameAssetDir(d), file, source, license, author, url, use)["file"]!.GetValue<string>();
            GameAssets.Assign(x, target, use, frames);
        });
        _gaPending.RemoveAll(p => p.File == file);
        _core.Audit.Append(Principal.LocalOwner.Name, "spiele.asset.verwendet", $"{Path.GetFileName(file)} als {use} ({license})");
        _gaMsg = $"{Path.GetFileName(file)}: {GameAssets.Uses.First(u => u.Id == use).Label}.";
    }

    private Control GameAssetsView(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "game-assets", Spacing = 8 };
        sp.Children.Add(H("Freie Assets", 14));
        sp.Children.Add(P("Grafik, Figuren, Töne und Musik aus freien Quellen – nur kostenlose, jede Datei mit Lizenz. Trendo schreibt daraus den Abspann und prüft vor dem Veröffentlichen.", null, 12));
        // Godot Asset Library
        var q = new TextBox { Name = "game-asset-query", Text = _gaQuery, PlaceholderText = "z. B. platformer, tileset, sound", Width = 240 };
        void Go() => _ = GameAssetSearch(q.Text ?? "");
        OnEnter(q, Go);
        sp.Children.Add(new WrapPanel { Children = { P("Godot Asset Library", Text, 12.5), new Border { Width = 8 }, q, new Border { Width = 4 }, Act("game-asset-search", "Suchen", Go, true) } });
        foreach (var (h, i) in _gaHits.Take(20).Select((h, i) => (h, i)))
        {
            var hh = h;
            sp.Children.Add(new DockPanel { Children = { Dk(Act($"game-asset-get-{i}", "Laden", () => _ = GameAssetGodotLoad(d, hh)), Avalonia.Controls.Dock.Right),
                P($"{h.Title} · {h.Category} · {h.License} · {h.Author}", FreeModels.Commercial(h.License) || h.License.Contains("MIT") ? Text : Brush.Parse("#F5A524"), 12) } });
        }
        // Seiten ohne Schnittstelle
        var sites = new WrapPanel { Name = "game-asset-sites" };
        foreach (var (s, i) in GameAssets.Sites.Select((s, i) => (s, i)))
        {
            var ss = s; var b = Act($"game-asset-site-{i}", s.Name + " öffnen", () => { _gaSite = ss.Name; _gaSince = DateTime.Now; Launch(ss.Url + Uri.EscapeDataString(_gaQuery)); _gaMsg = $"{ss.Name} geöffnet – kostenloses Asset laden, dann „Neue Downloads“."; Show(GK); });
            ToolTip.SetTip(b, s.Hint); sites.Children.Add(b);
        }
        sites.Children.Add(Act("game-asset-pickup", "Neue Downloads", GameAssetPickup, true));
        sp.Children.Add(sites);
        if (_gaMsg.Length > 0) sp.Children.Add(P(_gaMsg, _gaMsg.StartsWith('⚠') ? Brush.Parse("#F5A524") : Muted, 12));
        // Verwendung zuordnen
        if (_gaPending.Count > 0)
        {
            sp.Children.Add(H("Verwenden als …", 13));
            foreach (var (p, i) in _gaPending.Take(30).Select((p, i) => (p, i)))
            {
                var pp = p;
                var uses = GameAssets.Uses.Where(u => GameAssets.Fits(u.Id, p.File)).ToList();
                var use = new ComboBox { Name = $"game-asset-use-{i}", ItemsSource = uses.Select(u => u.Label).ToList(), SelectedIndex = 0, Width = 170 };
                var lic = new TextBox { Name = $"game-asset-lic-{i}", Text = p.License, PlaceholderText = "Lizenz (z. B. CC0 1.0)", Width = 130 };
                var au = new TextBox { Name = $"game-asset-author-{i}", Text = p.Author, PlaceholderText = "Urheber", Width = 120 };
                var go = Act($"game-asset-use-go-{i}", "Verwenden", () => ProgramRun(GK, () => GameUseAsset(d, pp.File, pp.Source, (lic.Text ?? "").Trim(), (au.Text ?? "").Trim(), pp.Url, uses[Math.Max(0, use.SelectedIndex)].Id)), true);
                sp.Children.Add(new WrapPanel { Children = { new TextBlock { Text = Path.GetFileName(p.File), Width = 170, Foreground = Text, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center }, use, new Border { Width = 4 }, lic, new Border { Width = 4 }, au, new Border { Width = 4 }, go } });
            }
        }
        // Asset-Liste und Abspann
        var list = d.List("assets").OfType<JsonObject>().ToList();
        sp.Children.Add(H($"Asset-Liste ({list.Count})", 13));
        foreach (var (a, i) in list.Select((a, i) => (a, i)))
        {
            var idx = i;
            sp.Children.Add(new DockPanel { Children = { Dk(Act($"game-asset-del-{i}", "✕", () => ProgramRun(GK, () => d.Edit("Asset entfernen", x => (x["assets"] as JsonArray)?.RemoveAt(idx)))), Avalonia.Controls.Dock.Right),
                P($"{a["name"].AsText()} · {GameAssets.Uses.FirstOrDefault(u => u.Id == a["use"].AsText()).Label} · {a["source"].AsText()} · {a["license"].AsText()}" + (a["author"].AsText() is { Length: > 0 } au ? " · " + au : ""), Text, 12) } });
        }
        foreach (var w in GameAssets.Check(d.Data, commercial: true)) sp.Children.Add(P("" + w, Brush.Parse("#F5A524"), 12));
        if (GameAssets.Credits(d.Data) is { Length: > 0 } credits)
            sp.Children.Add(new TextBox { Name = "game-asset-credits", Text = credits, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontSize = 11, MaxHeight = 160 });
        return sp;
    }
}
