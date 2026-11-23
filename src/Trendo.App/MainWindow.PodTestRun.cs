using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Pod;
using Trendo.Core.Programs;
using IoPath = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0753 (STUDIO-POD-01): Testlauf der Anbieter mit den eigenen Schlüsseln (ohne Schlüssel: BLOCKIERT mit Anleitung, nichts gesendet)
/// und Übernahme eines POD-Artikels in den Shop von „Online verkaufen“.</summary>
public partial class MainWindow
{
    IReadOnlyList<PodProbe> _podProbes = [];
    bool _podProbing;

    public IReadOnlyList<PodProbe> PodProbes => _podProbes;

    /// <summary>Nur lesende Abfragen mit den hinterlegten Schlüsseln; ohne Schlüssel geht keine Anfrage hinaus.</summary>
    public async Task<IReadOnlyList<PodProbe>> PodTestRunAsync()
    {
        if (_podProbing) return _podProbes;
        _podProbing = true;
        try
        {
            _podProbes = await PodTestRun.RunAsync(PodWeb, PodSecret);
            _core.Audit.Append(Principal.LocalOwner.Name, "pod.testlauf", string.Join(", ", _podProbes.Select(p => $"{p.Service}={p.State}")));
            return _podProbes;
        }
        finally { _podProbing = false; Show(CurrentView); }
    }

    Control PodTestRunPanel()
    {
        var sp = new StackPanel { Name = "pod-testrun", Spacing = 6, Margin = new Thickness(0, 12, 0, 0) };
        sp.Children.Add(H("Anbieter prüfen (Testlauf)", 13));
        sp.Children.Add(P("Prüft mit deinen eigenen Schlüsseln, ob Printful, Printify, Stripe (nur Testmodus) und Netlify antworten. Es wird nur gelesen – nichts angelegt, bestellt oder bezahlt. Ohne Schlüssel geht nichts hinaus.", Muted, 11));
        var run = new Button { Name = "pod-testrun-start", Content = _podProbing ? "Prüft …" : "Testlauf starten", Padding = new Thickness(10, 4), IsEnabled = !_podProbing };
        run.Click += async (_, _) => await PodTestRunAsync();
        sp.Children.Add(run);
        var i = 0;
        foreach (var p in _podProbes)
        {
            var (label, color) = p.State switch
            {
                PodProbeState.Bereit => ("BEREIT", "#3DDC84"), PodProbeState.Blockiert => ("BLOCKIERT", "#F5A524"), PodProbeState.Fehlt => ("FEHLT", "#98A2B3"),
                PodProbeState.Abgelehnt => ("NUR TESTMODUS", "#F5A524"), _ => ("FEHLER", "#FF6B6B"),
            };
            var box = new StackPanel { Name = "pod-probe-" + p.Service.ToLowerInvariant(), Spacing = 2 };
            box.Children.Add(new TextBlock { Text = $"{p.Service}: {label}", Foreground = Brush.Parse(color), FontSize = 12, FontWeight = FontWeight.SemiBold });
            box.Children.Add(new TextBlock { Text = p.Message, Foreground = Text, FontSize = 11, TextWrapping = TextWrapping.Wrap });
            var n = 1;
            foreach (var s in p.Steps) box.Children.Add(new TextBlock { Text = $"{n++}. {s}", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10, 0, 0, 0) });
            sp.Children.Add(box);
            i++;
        }
        return sp;
    }

    /// <summary>Eigenen Entwurf als Artikel in „Online verkaufen“ übernehmen: Druckdatei (PNG) wird das Artikelbild, Preis und Beschreibung kommen aus dem Dokument.</summary>
    public Product PodToShop(ProgramDocument d)
    {
        var png = IoPath.Combine(IoPath.GetTempPath(), "trendo-pod-" + Guid.NewGuid().ToString("N")[..8] + ".png");
        try
        {
            string? file = null;
            try { var bytes = PodPng(d); if (bytes.Length > 0) { File.WriteAllBytes(png, bytes); file = png; } } // leerer Entwurf: ohne Bild
            catch (Exception e) when (e is IOException or InvalidOperationException or ArgumentException) { }
            var p = BizShop.TakeOverDesign(d.Title, PodDec(d, "gross"), d.Text("description"), file, _settings.SmallBusiness);
            _core.Audit.Append(Principal.LocalOwner.Name, "pod.zum-shop", p.Name);
            return p;
        }
        finally { try { if (File.Exists(png)) File.Delete(png); } catch (IOException) { } }
    }

    Control PodToShopRow(ProgramDocument d)
    {
        var b = new Button { Name = "pod-to-shop", Content = "In den Shop übernehmen", Padding = new Thickness(10, 4) };
        b.Click += (_, _) =>
        {
            try { var p = PodToShop(d); _progMsg[PodKey] = $"„{p.Name}“ steht jetzt als Artikel unter „Online verkaufen“ – {(BizShop.ImagePath(p) is null ? "ohne Bild (der Entwurf ist leer)" : "mit der Druckdatei als Bild")}, {Eu((decimal)p.Price)} € netto."; }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or IOException) { _progMsg[PodKey] = e.Message; }
            Show(PodKey);
        };
        return new StackPanel { Spacing = 4, Margin = new Thickness(0, 10, 0, 0), Children = { H("In den eigenen Shop", 13),
            P("Legt den Entwurf als Artikel in „Online verkaufen“ an (oder aktualisiert ihn): Name, Verkaufspreis aus Schritt 3, Beschreibung und die Druckdatei als Bild.", Muted, 11), b } };
    }

    /// <summary>Ein Produkt aus dem POD-Shop (Printful/Printify) als Artikel übernehmen.</summary>
    public Product PodShopItemToShop(string itemId)
    {
        var item = PodShopItems.All().FirstOrDefault(x => x.Id == itemId) ?? throw new ArgumentException("Dieses Produkt gibt es nicht mehr.");
        var p = BizShop.TakeOverPod(item, _settings.SmallBusiness);
        _core.Audit.Append(Principal.LocalOwner.Name, "pod.zum-shop", p.Name);
        return p;
    }
}
