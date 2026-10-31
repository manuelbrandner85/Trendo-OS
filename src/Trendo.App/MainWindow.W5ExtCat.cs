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

    /// <summary>T-0241: ein Client ohne automatische Weiterleitung – jede Weiterleitung wird einzeln geprüft (nur GitHub über https).</summary>
    static readonly HttpClient ExtHttp = new(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(60) };
    /// <summary>Für Tests: Inhalt einer Adresse statt Netz.</summary>
    public Func<string, Task<byte[]>>? ExtFetchOverride { get; set; }

    async Task<byte[]> ExtFetch(string url, long max)
    {
        if (_core.Offline) throw new InvalidOperationException("Trendo ist offline – zum Laden braucht es Internet.");
        _core.Audit.Append(Principal.LocalOwner.Name, "ext.download", url);
        if (ExtFetchOverride is { } o) { var b = await o(url); return b.Length > max ? throw new InvalidOperationException("Datei ist größer als erlaubt.") : b; }
        var u = new Uri(url);
        for (var i = 0; i < 6; i++)
        {
            if (!ExtensionCatalog.RedirectAllowed(u)) throw new InvalidOperationException("Unerwartete Adresse: " + u.Host);
            using var res = await ExtHttp.GetAsync(u, HttpCompletionOption.ResponseHeadersRead);
            if ((int)res.StatusCode is >= 300 and < 400 && res.Headers.Location is { } loc) { u = loc.IsAbsoluteUri ? loc : new Uri(u, loc); continue; }
            res.EnsureSuccessStatusCode();
            if (res.Content.Headers.ContentLength > max) throw new InvalidOperationException("Datei ist größer als erlaubt.");
            await using var s = await res.Content.ReadAsStreamAsync();
            using var ms = new MemoryStream();
            var buf = new byte[81920]; int n;
            while ((n = await s.ReadAsync(buf)) > 0) { if (ms.Length + n > max) throw new InvalidOperationException("Datei ist größer als erlaubt."); ms.Write(buf, 0, n); }
            return ms.ToArray();
        }
        throw new InvalidOperationException("Zu viele Weiterleitungen.");
    }

    public async Task ExtCatalogLoadRemote(string url)
    {
        if (!ExtensionCatalog.IsRemote(url)) { Say("Nur GitHub-Adressen über https sind erlaubt.", "erweiterungen"); return; }
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(await ExtFetch(url, 2 << 20));
            ExtCatalogUrl = url; _extCatSrcDir = "";
            (ExtCatalog, _extCatErr) = ExtensionCatalog.Parse(json);
            ExtCatalog = ExtCatalog.Where(e => ExtensionCatalog.IsRemote(e.Url)).ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException) { Say("Katalog nicht erreichbar: " + ex.Message, "erweiterungen"); return; }
        Show("erweiterungen");
    }

    /// <summary>Erst fragen (Name, Version, Herkunft, Prüfsumme aus dem Katalog), dann laden, prüfen und mit Freigabe installieren.</summary>
    public Task ExtCatalogInstall(CatalogEntry e)
    {
        var third = ExtensionCatalog.IsRemote(e.Url);
        _extAsk = ("Erweiterung aus dem Katalog installieren?",
            $"„{e.Name}“ {e.Version} · Art: {e.Kind} · {e.Description}\nQuelle: {(third ? e.Url + " – Katalog von Dritten, ungeprüft" : "mitgelieferter Katalog")}\nPrüfsumme {e.Sha256[..12]}… wird vor dem Installieren geprüft.",
            "Ja, installieren", () => ExtCatalogInstallNow(e));
        Show("erweiterungen");
        return Task.CompletedTask;
    }

    async Task<string> ExtCatalogInstallNow(CatalogEntry e)
    {
        var file = ExtensionCatalog.LocalFile(_extCatSrcDir, e);
        string? tmp = null;
        if (file is null)
        {
            tmp = Path.Combine(Path.GetTempPath(), "trendo-dl-" + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                var bytes = await ExtFetch(e.Url, ExtensionStore.MaxBytes);
                await File.WriteAllBytesAsync(tmp, bytes); file = tmp;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException) { return "Herunterladen fehlgeschlagen: " + ex.Message; }
        }
        try
        {
            if (!File.Exists(file)) return "Nicht installiert: Datei nicht gefunden.";
            // Ziel der Freigabe ist die Prüfsumme der geladenen Datei – stimmt sie nicht mit dem Katalog, scheitert die Prüfung ohnehin
            var (x, err) = ExtensionCatalog.InstallVerified(Exts, e, file, ExtApprove(ExtensionStore.InstallAction, $"Erweiterung {e.Name} {e.Version} installieren", ExtensionStore.SourceHash(file)));
            return x is not null ? $"„{x.Manifest.Name}“ {x.Manifest.Version} installiert (Prüfsumme bestätigt)." : "Nicht installiert: " + string.Join(" ", err);
        }
        finally { if (tmp is not null && File.Exists(tmp)) File.Delete(tmp); }
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
