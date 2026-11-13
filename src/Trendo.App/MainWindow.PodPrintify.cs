using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Pod;
using Trendo.Core.Programs;
using IoPath = System.IO.Path;

namespace Trendo.App;

/// <summary>Print on Demand: Printify als zweiter Anbieter. Bestellungen nur mit bestätigter manueller Freigabe im Printify-Shop, weil Printify sonst selbst in die Fertigung gibt.</summary>
public partial class MainWindow
{
    const string PfyKey = "pod-printify-token", PfyShopKey = "pod-printify-shop";
    IReadOnlyList<PfyBlueprint> _pfyCatalog = [];
    PfyBlueprint? _pfyBlueprint;
    IReadOnlyList<PfyProvider> _pfyProviders = [];
    PfyProvider? _pfyProvider;
    IReadOnlyList<PfyVariant> _pfyVariants = [];
    readonly HashSet<int> _pfyChosen = [];
    string _pfySearch = "", _pfyName = "", _pfyPrice = "";

    public bool PodPrintifyOn => PodSecret(PfyKey) is not null;
    string PfyManualFile => IoPath.Combine(_core.DataRoot, "print-on-demand", "printify-manuell.txt");
    /// <summary>Die Bestätigung gilt nur für genau den Shop, für den sie gegeben wurde.</summary>
    public bool PodPrintifyManual => File.Exists(PfyManualFile) && PodSecret(PfyShopKey) is { } shop && File.ReadAllText(PfyManualFile).Trim() == shop;
    public IReadOnlyList<string> PodPrintifyMockups { get; private set; } = [];

    public void PodPrintifySetManual(bool on)
    {
        if (on && PodSecret(PfyShopKey) is { } shop) { Directory.CreateDirectory(IoPath.GetDirectoryName(PfyManualFile)!); File.WriteAllText(PfyManualFile, shop); }
        else if (File.Exists(PfyManualFile)) File.Delete(PfyManualFile);
        _core.Audit.Append(Principal.LocalOwner.Name, "pod.printify", on ? "manuelle freigabe bestaetigt" : "manuelle freigabe zurueckgenommen");
    }

    PrintifyClient Pfy() => new(PodWeb, PodSecret(PfyKey) ?? throw new PodServiceException("Verbinde zuerst dein Printify-Konto."));
    long PfyShopId() => long.TryParse(PodSecret(PfyShopKey), out var id) ? id : throw new PodServiceException("Lege zuerst bei Printify einen Shop an.");

    /// <summary>Prüft den Schlüssel, merkt sich den ersten Shop und speichert nur, wenn beides gelingt.</summary>
    public Task<bool> PodPrintifyConnect(string token) => PodRun(async () =>
    {
        token = token.Trim();
        if (token.Length == 0) throw new PodServiceException("Füge zuerst deinen Printify-Schlüssel ein.");
        var shops = await new PrintifyClient(PodWeb, token).ShopsAsync();
        if (shops.Count == 0) throw new PodServiceException("In deinem Printify-Konto gibt es noch keinen Shop. Lege dort zuerst einen an.");
        _core.Secrets.Set(PfyKey, token); _core.Secrets.Set(PfyShopKey, shops[0].Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (File.Exists(PfyManualFile)) File.Delete(PfyManualFile); // eine neue Verbindung braucht eine neue Bestätigung
        _core.Audit.Append(Principal.LocalOwner.Name, "pod.printify", "verbunden");
        _progMsg[PodKey] = "Printify ist verbunden mit dem Shop „" + shops[0].Title + "“." + (shops.Count > 1 ? $" Dein Konto hat {shops.Count} Shops, Trendo nutzt diesen ersten." : "");
    });

    public void PodPrintifyDisconnect()
    {
        _core.Secrets.Delete(PfyKey); _core.Secrets.Delete(PfyShopKey);
        if (File.Exists(PfyManualFile)) File.Delete(PfyManualFile);
        _pfyCatalog = []; _pfyBlueprint = null; _pfyProviders = []; _pfyProvider = null; _pfyVariants = []; _pfyChosen.Clear(); PodPrintifyMockups = [];
        _core.Audit.Append(Principal.LocalOwner.Name, "pod.printify", "getrennt");
        Show(PodKey);
    }

    public Task<bool> PodPrintifyCatalog() => PodRun(async () => { _pfyCatalog = await Pfy().CatalogAsync(); _progMsg[PodKey] = $"{_pfyCatalog.Count} Produkte geladen."; });

    public Task<bool> PodPrintifyChoose(PfyBlueprint b) => PodRun(async () =>
    {
        var c = Pfy();
        var providers = await c.ProvidersAsync(b.Id);
        if (providers.Count == 0) throw new PodServiceException("Für dieses Produkt gibt es keine Druckerei.");
        var variants = await c.VariantsAsync(b.Id, providers[0].Id);
        _pfyBlueprint = b; _pfyProviders = providers; _pfyProvider = providers[0]; _pfyVariants = variants; _pfyChosen.Clear(); PodPrintifyMockups = [];
        if (variants.Count > 0) _pfyChosen.Add(variants[0].Id);
        _progMsg[PodKey] = "";
    });

    public Task<bool> PodPrintifyProvider(PfyProvider p) => PodRun(async () =>
    {
        if (_pfyBlueprint is not { } b) return;
        var variants = await Pfy().VariantsAsync(b.Id, p.Id);
        _pfyProvider = p; _pfyVariants = variants; _pfyChosen.Clear();
        if (variants.Count > 0) _pfyChosen.Add(variants[0].Id);
    });

    public void PodPrintifyToggle(int id) { if (!_pfyChosen.Remove(id)) _pfyChosen.Add(id); Show(PodKey); }

    /// <summary>Lädt das Design hoch, legt das Produkt an, zeigt die Mockups und übernimmt es in den Shop. Bestellt nichts.</summary>
    public Task<bool> PodPrintifyCreate(ProgramDocument d, string name, decimal retail) => PodRun(async () =>
    {
        if (_pfyBlueprint is not { } b || _pfyProvider is not { } p || _pfyChosen.Count == 0) throw new PodServiceException("Wähle zuerst Produkt und Variante.");
        if (string.IsNullOrWhiteSpace(name)) throw new PodServiceException("Gib dem Produkt einen Namen.");
        if (retail <= 0) throw new PodServiceException("Gib einen Verkaufspreis an.");
        if (retail != Math.Round(retail, 2)) throw new PodServiceException("Der Preis darf höchstens zwei Nachkommastellen haben.");
        var c = Pfy();
        var png = PodPng(d);
        string imageId;
        if (png.Length <= PrintifyClient.MaxBase64Bytes) imageId = await c.UploadAsync("design.png", png);
        else
        {
            await PodSettlePending();
            var (host, site, url) = await PodUpload(d);
            try { imageId = await c.UploadUrlAsync("design.png", url); }
            finally { await PodClearQuiet(host, site); }
        }
        var note = _progMsg.GetValueOrDefault(PodKey) ?? "";
        var chosen = _pfyVariants.Where(v => _pfyChosen.Contains(v.Id)).ToList();
        var product = await c.CreateProductAsync(PfyShopId(), name.Trim(), b.Id, p.Id, chosen.Select(v => v.Id).ToList(), retail, chosen[0].Position, imageId);
        PodPrintifyMockups = product.Images;
        PodShopItems.Add(new(Guid.NewGuid().ToString("N"), name.Trim(), retail, product.Images.FirstOrDefault() ?? "", PrintifyHash(product.Id), [.. chosen.Select(v => new PodShopVariant(v.Title, v.Id))], "printify", product.Id));
        _core.Audit.Append(Principal.LocalOwner.Name, "pod.printify", "produkt angelegt");
        _pfyName = ""; _pfyPrice = "";
        _progMsg[PodKey] = $"Produkt ist bei Printify angelegt und im Shop. {product.Images.Count} Mockups." + (note.Contains("Ablage", StringComparison.Ordinal) ? " " + note.Trim() : "");
    });

    /// <summary>Printify nutzt Text-Kennungen. Der Shop ersetzt gleiche Produkte über eine Zahl, dafür genügt ein stabiler Wert aus der Kennung.</summary>
    static long PrintifyHash(string id) => BitConverter.ToInt64(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(id)));

    Control PodPrintifyPanel(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "pod-printify", Spacing = 6, Margin = new Thickness(0, 12, 0, 0) };
        sp.Children.Add(H("Printify anbinden", 13));
        if (!PodPrintifyOn)
        {
            var token = PodSecretBox("pod-pfy-token", "Printify-Schlüssel einfügen");
            sp.Children.Add(PodKeyRow(PodBtn("pod-pfy-get", "Schlüssel bei Printify holen", () => { Launch(PrintifyClient.TokenPage); return Task.CompletedTask; }), token, PodBtn("pod-pfy-connect", "Verbinden", () => PodPrintifyConnect(token.Text ?? ""), true)));
            return sp;
        }
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Name = "pod-pfy-state", Text = "Printify verbunden", Foreground = Brush.Parse("#3DDC84"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, PodBtn("pod-pfy-load", _pfyCatalog.Count == 0 ? "Katalog laden" : "Katalog neu laden", PodPrintifyCatalog), PodBtn("pod-pfy-off", "Trennen", () => { PodPrintifyDisconnect(); return Task.CompletedTask; }) } });
        var manual = new CheckBox { Name = "pod-pfy-manual", IsChecked = PodPrintifyManual, Content = new TextBlock { Text = "Dieser Printify-Shop steht auf manuelle Freigabe. Sonst gibt Printify Bestellungen nach 24 Stunden selbst in die Fertigung, und das kostet Geld. Die Bestätigung gilt nur für diesen Shop.", TextWrapping = TextWrapping.Wrap, Foreground = Text, FontSize = 12 } };
        manual.IsCheckedChanged += (_, _) => { if ((manual.IsChecked == true) != PodPrintifyManual) PodPrintifySetManual(manual.IsChecked == true); };
        sp.Children.Add(manual);

        if (_pfyCatalog.Count > 0)
        {
            var search = new TextBox { Name = "pod-pfy-search", Text = _pfySearch, PlaceholderText = "Produkt suchen", MaxWidth = 320, HorizontalAlignment = HorizontalAlignment.Left };
            search.LostFocus += (_, _) => { if ((search.Text ?? "") != _pfySearch) { _pfySearch = search.Text ?? ""; Show(PodKey); } };
            sp.Children.Add(search);
            var grid = new WrapPanel { Name = "pod-pfy-catalog" };
            foreach (var b in _pfyCatalog.Where(x => _pfySearch.Length == 0 || x.Title.Contains(_pfySearch, StringComparison.OrdinalIgnoreCase)).Take(24))
            {
                var pick = b;
                var btn = PodBtn("pod-pfy-prod-" + b.Id, b.Title, () => PodPrintifyChoose(pick));
                if (_pfyBlueprint?.Id == b.Id) btn.Classes.Add("accent");
                btn.Margin = new Thickness(0, 0, 6, 6);
                grid.Children.Add(btn);
            }
            sp.Children.Add(grid);
        }

        if (_pfyBlueprint is { } chosen)
        {
            sp.Children.Add(H(chosen.Title, 13));
            var prov = new ComboBox { Name = "pod-pfy-provider", ItemsSource = _pfyProviders.Select(x => x.Title).ToList(), SelectedIndex = Math.Max(0, _pfyProviders.ToList().FindIndex(x => x.Id == _pfyProvider?.Id)) };
            prov.SelectionChanged += async (_, _) => { if (prov.SelectedIndex >= 0 && _pfyProviders[prov.SelectedIndex].Id != _pfyProvider?.Id) await PodPrintifyProvider(_pfyProviders[prov.SelectedIndex]); };
            sp.Children.Add(prov);
            var variants = new WrapPanel { Name = "pod-pfy-variants" };
            foreach (var v in _pfyVariants.Take(40))
            {
                var id = v.Id;
                var cb = new CheckBox { Name = "pod-pfy-var-" + v.Id, IsChecked = _pfyChosen.Contains(v.Id), Content = v.Title, FontSize = 11, Margin = new Thickness(0, 0, 10, 2) };
                cb.IsCheckedChanged += (_, _) => { if ((cb.IsChecked == true) != _pfyChosen.Contains(id)) PodPrintifyToggle(id); };
                variants.Children.Add(cb);
            }
            sp.Children.Add(variants);
            var name = new TextBox { Name = "pod-pfy-name", Text = _pfyName, PlaceholderText = "Name des Produkts", MinWidth = 240 };
            var price = new TextBox { Name = "pod-pfy-price", Text = _pfyPrice, PlaceholderText = "Verkaufspreis mit Versand", Width = 170 };
            name.TextChanged += (_, _) => _pfyName = name.Text ?? "";
            price.TextChanged += (_, _) => _pfyPrice = price.Text ?? "";
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { name, price, PodBtn("pod-pfy-create", "Produkt mit Mockups anlegen", () => PodPrintifyCreate(d, name.Text ?? "", PodParsePrice(price.Text))) } });
            if (PodPrintifyMockups.Count > 0) sp.Children.Add(P($"{PodPrintifyMockups.Count} Mockups von Printify erzeugt.", Muted, 11));
        }
        return sp;
    }
}
