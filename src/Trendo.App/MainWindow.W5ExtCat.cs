using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>X-04 Katalog: lokaler (mitgelieferter) oder GitHub-Katalog. Download nur hier in der App (QA-019); Core prüft die Prüfsumme vor dem Installieren.</summary>
public partial class MainWindow
{
    public string ExtCatalogDir => Path.Combine(_core.DataRoot, "erweiterungen-katalog");
    public string? ExtCatalogUrl { get; set; }
    public List<CatalogEntry> ExtCatalog { get; private set; } = [];
    private List<string> _extCatErr = [];
    private string _extCatSrcDir = "";

    public void ExtCatalogLoadLocal()
    {
        var f = ExtensionCatalog.BuildLocal(ExtCatalogDir); _extCatSrcDir = ExtCatalogDir;
        (ExtCatalog, _extCatErr) = ExtensionCatalog.Parse(File.ReadAllText(f)); Show("erweiterungen");
    }

    public async Task ExtCatalogLoadRemote(string url)
    {
        if (!ExtensionCatalog.IsRemote(url)) { Say("Nur GitHub-Adressen über https sind erlaubt.", "erweiterungen"); return; }
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var json = await http.GetStringAsync(url);
            ExtCatalogUrl = url; _extCatSrcDir = "";
            (ExtCatalog, _extCatErr) = ExtensionCatalog.Parse(json);
            ExtCatalog = ExtCatalog.Where(e => ExtensionCatalog.IsRemote(e.Url)).ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { Say("Katalog nicht erreichbar: " + ex.Message, "erweiterungen"); return; }
        Show("erweiterungen");
    }

    public async Task ExtCatalogInstall(CatalogEntry e)
    {
        var file = ExtensionCatalog.LocalFile(_extCatSrcDir, e);
        string? tmp = null;
        if (file is null)
        {
            tmp = Path.Combine(Path.GetTempPath(), "trendo-dl-" + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                var bytes = await http.GetByteArrayAsync(e.Url);
                if (bytes.Length > 50 << 20) { Say("Erweiterung ist größer als 50 MB.", "erweiterungen"); return; }
                await File.WriteAllBytesAsync(tmp, bytes); file = tmp;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { Say("Herunterladen fehlgeschlagen: " + ex.Message, "erweiterungen"); return; }
        }
        try
        {
            var (x, err) = ExtensionCatalog.InstallVerified(Exts, e, file);
            Say(x is not null ? $"„{x.Manifest.Name}“ {x.Manifest.Version} installiert (Prüfsumme bestätigt)." : "Nicht installiert: " + string.Join(" ", err), "erweiterungen");
        }
        finally { if (tmp is not null) File.Delete(tmp); }
    }

    private Control ExtCatalogCard()
    {
        var sp = new StackPanel { Spacing = 6 };
        var url = new TextBox { Name = "ext-cat-url", PlaceholderText = "Optional: GitHub-Adresse eines Katalogs (https://raw.githubusercontent.com/…/katalog.json)", Width = 420, Text = ExtCatalogUrl };
        sp.Children.Add(new WrapPanel { Children = { Act("ext-cat-local", "Mitgelieferten Katalog zeigen", ExtCatalogLoadLocal, true), url, Act("ext-cat-remote", "GitHub-Katalog laden", () => _ = ExtCatalogLoadRemote(url.Text?.Trim() ?? "")) } });
        sp.Children.Add(P("Jede Erweiterung wird vor dem Installieren per SHA-256-Prüfsumme geprüft. Es gibt keinen offiziellen Online-Katalog – GitHub-Kataloge sind von Dritten und ungeprüft.", null, 10.5));
        foreach (var m in _extCatErr) sp.Children.Add(P("Übersprungen: " + m, Avalonia.Media.Brush.Parse("#F97316"), 10.5));
        var i = 0;
        foreach (var e in ExtCatalog)
        {
            var x = e;
            sp.Children.Add(Row(P($"{e.Name} {e.Version} ({e.Kind}) – {e.Description} · {e.Sha256[..12]}…", null, 11), Act($"ext-cat-install-{i++}", "Installieren", () => _ = ExtCatalogInstall(x))));
        }
        return Card(sp);
    }
}
