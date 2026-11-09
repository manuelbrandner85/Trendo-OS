using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;
using Trendo.Core.Pod;
using Trendo.Core.Programs;
using IoPath = System.IO.Path;

namespace Trendo.App;

/// <summary>Print on Demand: eigenes Printful-Konto verbinden, Katalog, echte Mockups, Produkt anlegen. Designs liegen dafür kurz im eigenen Netlify-Konto. Bestellungen löst dieser Teil nicht aus.</summary>
public partial class MainWindow
{
    const string PfKey = "pod-printful-token", NfKey = "pod-netlify-token", NfSite = "pod-netlify-site", NfUrl = "pod-netlify-url";
    static readonly HttpFetch PodHttp = new(new HttpClient { Timeout = TimeSpan.FromMinutes(3) });
    IReadOnlyList<PfProduct> _pfCatalog = [];
    PfProduct? _pfProduct;
    IReadOnlyList<PfVariant> _pfVariants = [];
    IReadOnlyList<PfPlacement> _pfPlacements = [];
    readonly HashSet<int> _pfChosen = [];
    string _pfPlacement = "", _pfSearch = "";
    bool _pfBusy;
    DateTime _pfLastMockup = DateTime.MinValue;
    readonly List<Bitmap> _pfMockupImages = [];

    /// <summary>Für Tests: Netz, Wartezeit und Ausgabe der Druckdatei nachbilden.</summary>
    public IWebSend? PodWebOverride { get; set; }
    public Func<TimeSpan, CancellationToken, Task>? PodDelayOverride { get; set; }
    public Func<ProgramDocument, byte[]>? PodPngOverride { get; set; }
    public TimeSpan PodMockupGap { get; set; } = TimeSpan.FromSeconds(30);
    public IReadOnlyList<string> PodMockupUrls { get; private set; } = [];
    public long? PodLastProduct { get; private set; }

    IWebSend PodWeb => PodWebOverride ?? PodHttp;
    Task PodWait(TimeSpan t, CancellationToken ct = default) => (PodDelayOverride ?? Task.Delay)(t, ct);
    string? PodSecret(string key) { try { return _core.Secrets.Get(key) is { Length: > 0 } v ? v : null; } catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException) { return null; } }
    public bool PodPrintfulOn => PodSecret(PfKey) is not null;
    public bool PodNetlifyOn => PodSecret(NfKey) is not null;

    void PodSay(string text) { _progMsg[PodKey] = text; Show(PodKey); }

    /// <summary>Führt eine Aktion aus, zeigt Fehler verständlich und sperrt Doppelklicks.</summary>
    async Task<bool> PodRun(Func<Task> work)
    {
        if (_pfBusy) return false;
        _pfBusy = true;
        try { await work(); return true; }
        catch (PodServiceException e) { _progMsg[PodKey] = e.Message; return false; }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException) { _progMsg[PodKey] = "Keine Verbindung. Ist das Internet erreichbar?"; return false; }
        finally { _pfBusy = false; Show(PodKey); }
    }

    /// <summary>Prüft den Schlüssel und speichert ihn nur, wenn er gilt.</summary>
    public Task<bool> PodPrintfulConnect(string token) => PodRun(async () =>
    {
        token = token.Trim();
        if (token.Length == 0) throw new PodServiceException("Füge zuerst deinen Printful-Schlüssel ein.");
        await new PrintfulClient(PodWeb, token).CheckAsync();
        _core.Secrets.Set(PfKey, token);
        _core.Audit.Append(Principal.LocalOwner.Name, "pod.printful", "verbunden");
        _progMsg[PodKey] = "Printful ist verbunden.";
    });

    public void PodPrintfulDisconnect()
    {
        _core.Secrets.Delete(PfKey);
        _pfCatalog = []; _pfProduct = null; _pfVariants = []; _pfPlacements = []; _pfChosen.Clear(); PodMockupUrls = []; _pfMockupImages.Clear();
        _core.Audit.Append(Principal.LocalOwner.Name, "pod.printful", "getrennt");
        Show(PodKey);
    }

    public Task<bool> PodNetlifyConnect(string token) => PodRun(async () =>
    {
        token = token.Trim();
        if (token.Length == 0) throw new PodServiceException("Füge zuerst deinen Netlify-Schlüssel ein.");
        var (id, url) = await new NetlifyDesignHost(PodWeb, token).CreateSiteAsync();
        _core.Secrets.Set(NfKey, token); _core.Secrets.Set(NfSite, id); _core.Secrets.Set(NfUrl, url);
        _core.Audit.Append(Principal.LocalOwner.Name, "pod.netlify", "verbunden");
        _progMsg[PodKey] = "Design-Ablage ist eingerichtet.";
    });

    PrintfulClient Pf() => new(PodWeb, PodSecret(PfKey) ?? throw new PodServiceException("Verbinde zuerst dein Printful-Konto."));

    public Task<bool> PodLoadCatalog() => PodRun(async () => { _pfCatalog = await Pf().CatalogAsync(); _progMsg[PodKey] = $"{_pfCatalog.Count} Produkte geladen."; });

    public Task<bool> PodChooseProduct(PfProduct p) => PodRun(async () =>
    {
        var c = Pf();
        var variants = await c.VariantsAsync(p.Id);
        var placements = await c.PlacementsAsync(p.Id);
        _pfProduct = p; _pfVariants = variants; _pfPlacements = placements; _pfChosen.Clear(); PodMockupUrls = []; _pfMockupImages.Clear();
        _pfPlacement = placements.FirstOrDefault()?.Key ?? "";
        if (variants.FirstOrDefault(v => v.InStock) is { } first) _pfChosen.Add(first.Id);
        _progMsg[PodKey] = "";
    });

    public void PodToggleVariant(int id) { if (!_pfChosen.Remove(id)) _pfChosen.Add(id); Show(PodKey); }

    byte[] PodPng(ProgramDocument d)
    {
        if (PodPngOverride is { } fake) return fake(d);
        var f = IoPath.Combine(IoPath.GetTempPath(), "trendo-pod-" + Guid.NewGuid().ToString("N") + ".png");
        try { PodExportPng(d, f); return File.ReadAllBytes(f); }
        finally { try { File.Delete(f); } catch (IOException) { } }
    }

    /// <summary>Stellt das Design unter einer nicht erratbaren Adresse bereit.</summary>
    async Task<(NetlifyDesignHost Host, string Site, string Url)> PodUpload(ProgramDocument d)
    {
        if (PodSecret(NfKey) is not { } token || PodSecret(NfSite) is not { } site || PodSecret(NfUrl) is not { } baseUrl)
            throw new PodServiceException("Richte zuerst die Design-Ablage ein.");
        var host = new NetlifyDesignHost(PodWeb, token);
        var name = NetlifyDesignHost.RandomName();
        await host.DeployAsync(site, NetlifyDesignHost.Zip([(name, PodPng(d))]), PodWait);
        _core.Audit.Append(Principal.LocalOwner.Name, "pod.netlify", "design bereitgestellt");
        return (host, site, baseUrl.TrimEnd('/') + "/" + name);
    }

    static async Task PodClear(NetlifyDesignHost host, string site, Func<TimeSpan, CancellationToken, Task> wait) => await host.DeployAsync(site, NetlifyDesignHost.Zip([]), wait);

    /// <summary>Erzeugt echte Mockups bei Printful und leert danach die Ablage.</summary>
    public Task<bool> PodMakeMockups(ProgramDocument d) => PodRun(async () =>
    {
        if (_pfProduct is not { } product || _pfChosen.Count == 0) throw new PodServiceException("Wähle zuerst Produkt und Variante.");
        var left = PodMockupGap - (DateTime.UtcNow - _pfLastMockup);
        if (left > TimeSpan.Zero) throw new PodServiceException($"Printful erlaubt nur wenige Mockups pro Minute. Noch {Math.Ceiling(left.TotalSeconds)} Sekunden.");
        var (host, site, url) = await PodUpload(d);
        try
        {
            var c = Pf();
            _pfLastMockup = DateTime.UtcNow;
            var task = await c.StartMockupAsync(product.Id, _pfChosen, _pfPlacement, url);
            for (var i = 0; i < 40 && !task.Done && !task.Failed; i++) { await PodWait(TimeSpan.FromSeconds(3), default); task = await c.MockupAsync(task.Key); }
            if (task.Failed) throw new PodServiceException("Printful konnte kein Mockup erzeugen: " + (task.Error ?? "unbekannter Grund"));
            if (!task.Done) throw new PodServiceException("Printful braucht zu lange. Versuche es gleich noch einmal.");
            PodMockupUrls = task.Urls;
            _pfMockupImages.Clear();
            foreach (var u in task.Urls.Take(6))
            {
                var r = await PodWeb.SendAsync("GET", u, new Dictionary<string, string>());
                if (!r.Ok) continue;
                try { using var ms = new MemoryStream(r.Body); _pfMockupImages.Add(Bitmap.DecodeToWidth(ms, 420)); }
                catch (Exception e) when (e is ArgumentException or InvalidOperationException or NullReferenceException) { }
            }
            _progMsg[PodKey] = $"{task.Urls.Count} Mockups erzeugt.";
        }
        finally { await PodClear(host, site, PodWait); }
    });

    /// <summary>Legt das Design als Produkt im eigenen Printful-Bestand an. Bestellt nichts. Die Ablage wird erst geleert, wenn Printful die Datei übernommen hat.</summary>
    public Task<bool> PodCreateProduct(ProgramDocument d, string name, decimal retail) => PodRun(async () =>
    {
        if (_pfProduct is null || _pfChosen.Count == 0) throw new PodServiceException("Wähle zuerst Produkt und Variante.");
        if (string.IsNullOrWhiteSpace(name)) throw new PodServiceException("Gib dem Produkt einen Namen.");
        if (retail <= 0) throw new PodServiceException("Gib einen Verkaufspreis an.");
        var (host, site, url) = await PodUpload(d);
        var c = Pf();
        var ready = false;
        try
        {
            PodLastProduct = await c.CreateProductAsync(name.Trim(), _pfChosen.Select(id => (id, retail)), url, PodMockupUrls.FirstOrDefault());
            _core.Audit.Append(Principal.LocalOwner.Name, "pod.printful", "produkt angelegt");
            for (var i = 0; i < 24 && !ready; i++) { ready = await c.ProductFilesReadyAsync(PodLastProduct.Value); if (!ready) await PodWait(TimeSpan.FromSeconds(5), default); }
            _progMsg[PodKey] = ready ? "Produkt ist bei Printful angelegt." : "Produkt ist angelegt, Printful verarbeitet die Datei noch.";
        }
        finally { if (ready || PodLastProduct is null) await PodClear(host, site, PodWait); }
    });

    TextBox PodSecretBox(string name, string hint) => new() { Name = name, PasswordChar = '•', PlaceholderText = hint, MinWidth = 280 };

    Button PodBtn(string name, string text, Func<Task> act, bool accent = false)
    {
        var b = new Button { Name = name, Content = text, Padding = new Thickness(10, 4), IsEnabled = !_pfBusy };
        if (accent) b.Classes.Add("accent");
        b.Click += async (_, _) => await act();
        return b;
    }

    Control PodPrintfulPanel(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "pod-printful", Spacing = 6, Margin = new Thickness(0, 12, 0, 0) };
        sp.Children.Add(H("Printful anbinden", 13));
        if (!PodPrintfulOn)
        {
            sp.Children.Add(P("Mit deinem eigenen Printful-Konto siehst du echte Produkte und Mockups. Der Schlüssel liegt verschlüsselt in Trendo.", Muted, 11));
            var token = PodSecretBox("pod-pf-token", "Printful-Schlüssel einfügen");
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { PodBtn("pod-pf-get", "Schlüssel bei Printful holen", () => { Launch(PrintfulClient.TokenPage); return Task.CompletedTask; }), token, PodBtn("pod-pf-connect", "Verbinden", () => PodPrintfulConnect(token.Text ?? ""), true) } });
            return sp;
        }
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Name = "pod-pf-state", Text = "✓ Printful verbunden", Foreground = Brush.Parse("#3DDC84"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, PodBtn("pod-pf-load", _pfCatalog.Count == 0 ? "Katalog laden" : "Katalog neu laden", PodLoadCatalog), PodBtn("pod-pf-off", "Trennen", () => { PodPrintfulDisconnect(); return Task.CompletedTask; }) } });

        if (!PodNetlifyOn)
        {
            sp.Children.Add(P("Printful holt Designs nur von einer Internetadresse ab. Dafür liegt das Design kurz in deinem eigenen Netlify-Konto unter einem nicht erratbaren Namen und wird danach wieder entfernt.", Muted, 11));
            var token = PodSecretBox("pod-nf-token", "Netlify-Schlüssel einfügen");
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { PodBtn("pod-nf-get", "Schlüssel bei Netlify holen", () => { Launch(NetlifyDesignHost.TokenPage); return Task.CompletedTask; }), token, PodBtn("pod-nf-connect", "Ablage einrichten", () => PodNetlifyConnect(token.Text ?? ""), true) } });
        }

        if (_pfCatalog.Count > 0)
        {
            var search = new TextBox { Name = "pod-pf-search", Text = _pfSearch, PlaceholderText = "Produkt suchen", MaxWidth = 320, HorizontalAlignment = HorizontalAlignment.Left };
            search.LostFocus += (_, _) => { if ((search.Text ?? "") != _pfSearch) { _pfSearch = search.Text ?? ""; Show(PodKey); } };
            sp.Children.Add(search);
            var grid = new WrapPanel { Name = "pod-pf-catalog" };
            foreach (var p in _pfCatalog.Where(x => _pfSearch.Length == 0 || x.Title.Contains(_pfSearch, StringComparison.OrdinalIgnoreCase) || x.Type.Contains(_pfSearch, StringComparison.OrdinalIgnoreCase)).Take(24))
            {
                var pick = p;
                var b = PodBtn("pod-pf-prod-" + p.Id, p.Title, () => PodChooseProduct(pick));
                if (_pfProduct?.Id == p.Id) b.Classes.Add("accent");
                b.Margin = new Thickness(0, 0, 6, 6);
                grid.Children.Add(b);
            }
            sp.Children.Add(grid);
        }

        if (_pfProduct is { } chosen)
        {
            sp.Children.Add(H(chosen.Title, 13));
            if (_pfPlacements.Count > 0)
            {
                var place = new ComboBox { Name = "pod-pf-placement", ItemsSource = _pfPlacements.Select(x => x.Label).ToList(), SelectedIndex = Math.Max(0, _pfPlacements.ToList().FindIndex(x => x.Key == _pfPlacement)) };
                place.SelectionChanged += (_, _) => { if (place.SelectedIndex >= 0) _pfPlacement = _pfPlacements[place.SelectedIndex].Key; };
                sp.Children.Add(place);
            }
            var variants = new WrapPanel { Name = "pod-pf-variants" };
            foreach (var v in _pfVariants.Take(40))
            {
                var id = v.Id;
                var cb = new CheckBox { Name = "pod-pf-var-" + v.Id, IsChecked = _pfChosen.Contains(v.Id), IsEnabled = v.InStock, Content = $"{v.Size} {v.Color} · {v.Price:0.00}", FontSize = 11, Margin = new Thickness(0, 0, 10, 2) };
                cb.IsCheckedChanged += (_, _) => { if ((cb.IsChecked == true) != _pfChosen.Contains(id)) PodToggleVariant(id); };
                variants.Children.Add(cb);
            }
            sp.Children.Add(variants);
            sp.Children.Add(PodBtn("pod-pf-mockup", "Mockups erzeugen", () => PodMakeMockups(d), true));
            if (_pfMockupImages.Count > 0)
            {
                var shots = new WrapPanel { Name = "pod-pf-shots" };
                foreach (var img in _pfMockupImages) shots.Children.Add(new Image { Source = img, Width = 200, Margin = new Thickness(0, 0, 8, 8) });
                sp.Children.Add(shots);
            }
            var name = new TextBox { Name = "pod-pf-name", PlaceholderText = "Name des Produkts", MinWidth = 240 };
            var price = new TextBox { Name = "pod-pf-price", PlaceholderText = "Verkaufspreis", Width = 110 };
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { name, price, PodBtn("pod-pf-create", "Als Produkt anlegen", () => PodCreateProduct(d, name.Text ?? "", decimal.TryParse((price.Text ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 0m)) } });
            sp.Children.Add(P("Das Anlegen bestellt nichts und kostet nichts. Kosten entstehen erst bei einer von dir bestätigten Bestellung.", Muted, 11));
        }
        return sp;
    }
}
