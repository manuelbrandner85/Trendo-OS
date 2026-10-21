using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>X-07: Freie Assets.</summary>
public partial class MainWindow
{
    public string AssetFolder => WorkFolder("Assets");
    private IReadOnlyList<AssetHit> _assetHits = [];
    private int _assetType = 2; private string _assetQuery = "";
    public Func<string, Task<string>> AssetGet { get; set; } = async url => await OssHttp.GetStringAsync(url);
    public Func<string, string, Task> AssetDownload { get; set; } = async (url, file) => { await using var s = await OssHttp.GetStreamAsync(url); await using var f = File.Create(file); await s.CopyToAsync(f); };

    private void InitW4Assets() => RegisterTab(KreativTabs, "assets", "Freie Assets", "X-07", AssetView, "asset", "textur", "hdri", "modell", "cc0", "kostenlos", "poly haven");

    public async Task AssetSearch(int type, string q)
    {
        _assetType = type; _assetQuery = q;
        if (_core.Offline) { Say("Trendo ist offline. Die Suche braucht Internet.", "assets"); return; }
        try { _assetHits = FreeAssets.ParseList(await AssetGet(FreeAssets.ListUrl(FreeAssets.Types[type].Api)), q); _w45Msg = _assetHits.Count == 0 ? "Nichts gefunden. Versuch ein englisches Wort, z. B. „rock“." : ""; }
        catch (Exception ex) { _assetHits = []; _w45Msg = "Suche hat nicht geklappt: " + ex.Message; }
        Show("assets");
    }

    public async Task<string?> AssetFetch(AssetHit h)
    {
        var dir = Path.Combine(AssetFolder, DevStudio.SafeName(h.Id));
        try
        {
            var plan = FreeAssets.Plan(await AssetGet(FreeAssets.FilesUrl(h.Id)), h.Type);
            foreach (var f in plan)
            {
                var target = Path.GetFullPath(Path.Combine(dir, f.RelPath));
                if (!target.StartsWith(Path.GetFullPath(dir) + Path.DirectorySeparatorChar)) throw new InvalidDataException("Unsicherer Pfad");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await AssetDownload(f.Url, target);
                if (!FreeAssets.Md5Ok(target, f.Md5)) { File.Delete(target); throw new InvalidDataException("Datei beschädigt: " + f.RelPath); }
            }
            FreeAssets.WriteLicense(dir, h.Id, h.Name);
            _core.Audit.Append(Principal.LocalOwner.Name, "asset.fetch", $"{h.Id} {plan.Count} Dateien");
            Say($"„{h.Name}“ geladen ({plan.Count} Dateien, Lizenz CC0 liegt bei).", "assets");
            return dir;
        }
        catch (Exception ex) { Say("Laden hat nicht geklappt: " + ex.Message, "assets"); return null; }
    }

    private Control AssetView()
    {
        var sp = new StackPanel { Spacing = 8, Children = { P("Freie Modelle, Texturen und Himmel. Alles hier darfst du kostenlos nutzen, auch für Geld.") } };
        var type = new ComboBox { Name = "asset-type", ItemsSource = FreeAssets.Types.Select(t => t.Label).ToList(), SelectedIndex = _assetType, Width = 160 };
        var q = new TextBox { Name = "asset-query", Text = _assetQuery, PlaceholderText = "z. B. rock, wood, sky", Width = 220 };
        sp.Children.Add(new WrapPanel { Children = { type, q, Act("asset-search", "Suchen", () => _ = AssetSearch(Math.Max(0, type.SelectedIndex), q.Text ?? ""), true) } });
        var i = 0;
        foreach (var h in _assetHits)
        {
            var hit = h;
            sp.Children.Add(Card(Row(new StackPanel { Children = { new TextBlock { Text = h.Name, Foreground = Text, FontSize = 12.5 }, P($"Poly Haven · CC0 · {string.Join(", ", h.Categories.Take(3))}", null, 10.5) } },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children = {
                    Act($"asset-web-{i}", "Ansehen", () => Launch("https://polyhaven.com/a/" + hit.Id)), Act($"asset-get-{i}", "Laden", () => _ = AssetFetch(hit)) } })));
            i++;
        }
        sp.Children.Add(H("Weitere freie Quellen", 14));
        var j = 0;
        foreach (var s in FreeAssets.Sources)
        {
            var src = s;
            sp.Children.Add(Card(Row(new StackPanel { Children = { new TextBlock { Text = s.Name, Foreground = Text, FontSize = 12.5 }, P($"{s.What} · Lizenz: {s.License}", null, 10.5) } }, Act($"asset-src-{j++}", "Öffnen", () => Launch(src.Url)))));
        }
        sp.Children.Add(Act("asset-folder", "Assets-Ordner öffnen", () => Launch(AssetFolder)));
        return KreativPage("Freie Assets", sp);
    }
}
