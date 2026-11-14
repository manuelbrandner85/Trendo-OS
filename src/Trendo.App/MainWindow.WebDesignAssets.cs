using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SkiaSharp;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Stufe 2, Teil 3 im Studio „Webseite bauen“: Medien je Projekt mit Herkunft und Lizenz (Z7), Schriften über Fontsource, und das lokale Modell für Webdesign mit Prüflauf (Z6).</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Netz für Fontsource nachbilden.</summary>
    public static HttpMessageHandler? WebDesignFontsourceHandler { get; set; }
    /// <summary>Für Tests: KI und Hardware für den Prüflauf nachbilden.</summary>
    public static IKiBackend? WebDesignBenchBackendOverride { get; set; }
    public static HardwareInfo? WebDesignHardwareOverride { get; set; }
    public const int WebImageMaxSide = 1920;

    string _wdAssetQuery = ""; bool _wdBenchBusy;
    public string WebDesignBenchNote { get; private set; } = "";

    /// <summary>Bilder für das Web verkleinern: längste Seite höchstens 1920 Pixel, gleiches Format. Liefert die Datei, die übernommen wird.</summary>
    public static string WebShrinkImage(string file)
    {
        var ext = Path.GetExtension(file).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp")) return file;
        using var src = SKBitmap.Decode(file);
        if (src is null || Math.Max(src.Width, src.Height) <= WebImageMaxSide) return file;
        var k = WebImageMaxSide / (double)Math.Max(src.Width, src.Height);
        using var dst = src.Resize(new SKImageInfo(Math.Max(1, (int)(src.Width * k)), Math.Max(1, (int)(src.Height * k))), new SKSamplingOptions(SKCubicResampler.Mitchell));
        if (dst is null) return file;
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-web-medien", Guid.NewGuid().ToString("N")[..8])).FullName;
        var outFile = Path.Combine(dir, Path.GetFileName(file));
        using var img = SKImage.FromBitmap(dst);
        using var data = img.Encode(ext == ".png" ? SKEncodedImageFormat.Png : ext == ".webp" ? SKEncodedImageFormat.Webp : SKEncodedImageFormat.Jpeg, 82);
        using (var fs = File.Create(outFile)) data.SaveTo(fs);
        return outFile;
    }

    /// <summary>Datei als Medium ins Projekt holen; Bilder werden dabei für das Web verkleinert.</summary>
    public void WebDesignAssetImport(string path, string art, string titel, string herkunft, string lizenz, string urheber = "", string quelle = "", string stichworte = "", string? familie = null)
    {
        path = path.Trim().Trim('"');
        string source = path;
        try { if (art == "bild" && File.Exists(path)) source = WebShrinkImage(path); }
        catch (Exception e) when (e is IOException or InvalidOperationException) { source = path; }
        var (a, err) = WebDesignAssets.Import(WebDesignDir, source, art, titel, herkunft, lizenz, urheber, quelle, stichworte, familie);
        if (source != path) try { Directory.Delete(Path.GetDirectoryName(source)!, true); } catch (IOException) { }
        _core.Audit.Append(Principal.LocalOwner.Name, "web.visuell.medium", err ?? a!.Datei);
        _wdNote = err ?? $"„{a!.Titel}“ liegt jetzt unter {a.Datei}" + (string.IsNullOrWhiteSpace(lizenz) ? ". Bitte noch die Lizenz eintragen, sonst meldet der Export einen Fehler." : ".");
        if (err is null) WebDesignReload(WebDesignCurrent());
        Show("dev");
    }

    /// <summary>Schrift über die offizielle Fontsource-Schnittstelle holen (nur nach Klick, mit Herkunft und Lizenz). Sie liegt danach lokal im Projekt.</summary>
    public async Task WebDesignFontFetch(string id)
    {
        id = id.Trim().ToLowerInvariant();
        if (!Regex.IsMatch(id, "^[a-z0-9-]{1,60}$")) { _wdNote = "Bitte die Kennung der Schrift von fontsource.org angeben, z. B. inter."; Show("dev"); return; }
        if (_opt.Isolated && WebDesignFontsourceHandler is null) { _wdNote = "Testmodus: keine Verbindung zu Fontsource."; Show("dev"); return; }
        _wdNote = "Schrift wird von Fontsource geholt …"; Show("dev");
        try
        {
            using var http = WebDesignFontsourceHandler is { } h ? new HttpClient(h, false) : new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(30);
            var meta = JsonNode.Parse(await http.GetStringAsync("https://api.fontsource.org/v1/fonts/" + id))!;
            var family = meta["family"]?.ToString() ?? "";
            var license = meta["license"] is JsonObject lo ? lo["type"]?.ToString() ?? "" : meta["license"]?.ToString() ?? "";
            var subset = meta["defSubset"]?.ToString() ?? "latin";
            var url = meta["variants"]?["400"]?["normal"]?[subset]?["url"]?["woff2"]?.ToString();
            if (url is null || !url.StartsWith("https://cdn.jsdelivr.net/fontsource/", StringComparison.Ordinal) || url.Contains("..", StringComparison.Ordinal) || url.Contains('%') || !url.EndsWith(".woff2", StringComparison.Ordinal))
                throw new InvalidOperationException("Fontsource hat keine Schriftdatei aus der offiziellen Quelle genannt.");
            if (!WebDesignAssets.ValidFamily(family)) throw new InvalidOperationException("Der Name der Schrift ist ungewöhnlich und wird nicht übernommen.");
            var tmp = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-web-medien", Guid.NewGuid().ToString("N")[..8])).FullName;
            var file = Path.Combine(tmp, id + "-400.woff2");
            using (var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                resp.EnsureSuccessStatusCode();
                if (resp.Content.Headers.ContentLength is > 5_000_000) throw new InvalidOperationException("Die Schriftdatei ist ungewöhnlich groß und wird nicht geladen.");
                var bytes = await resp.Content.ReadAsByteArrayAsync();
                if (bytes.Length > 5_000_000 || bytes.Length < 4 || bytes[0] != (byte)'w' || bytes[1] != (byte)'O' || bytes[2] != (byte)'F' || bytes[3] != (byte)'2') throw new InvalidOperationException("Die geladene Datei ist keine WOFF2-Schrift.");
                await File.WriteAllBytesAsync(file, bytes);
            }
            _core.Audit.Append(Principal.LocalOwner.Name, "web.visuell.fontsource", $"{id} lizenz={license}");
            WebDesignAssetImport(file, "schrift", family, "Fontsource", license, "", "https://fontsource.org/fonts/" + id, "schrift", family);
            try { Directory.Delete(tmp, true); } catch (IOException) { }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException or IOException) { _wdNote = "Schrift ließ sich nicht holen: " + e.Message; Show("dev"); }
    }

    public void WebDesignUseFont(string family) =>
        WebDesignHumanAction(new JsonObject { ["aktionen"] = new JsonArray(new JsonObject { ["aktion"] = "gestaltungswert_setzen", ["name"] = "font", ["wert"] = "eigene:" + family }) }.ToJsonString());

    HardwareInfo WebHardware => WebDesignHardwareOverride ?? (_opt.Isolated ? new HardwareInfo(16L << 30, null, 0) : HardwareProbe.Read());

    /// <summary>Z6: Prüflauf aller lokalen Modelle mit den festen Web-Aufgaben. Ergebnis wird gespeichert; danach empfiehlt Trendo nur, was bestanden hat.</summary>
    public async Task WebDesignBenchRun()
    {
        if (_wdBenchBusy) return;
        _wdBenchBusy = true; WebDesignBenchNote = "Prüflauf läuft – jedes Modell bekommt drei Web-Aufgaben …"; Show("dev");
        try
        {
            var backend = WebDesignBenchBackendOverride ?? WebDesignKi.Allowed(KiBackendsOverride?.Invoke() ?? DefaultKiBackends()).FirstOrDefault(b => b is not IRemoteKiBackend);
            if (backend is null || !await backend.IsUpAsync()) { WebDesignBenchNote = "Auf diesem PC läuft gerade keine lokale KI. Lade auf der KI-Seite ein Modell."; return; }
            var models = await backend.ModelsAsync();
            var results = new List<WebDesignModelBench.ModelResult>();
            foreach (var m in models)
            {
                WebDesignBenchNote = $"Prüflauf: {m} ({results.Count + 1} von {models.Count}) …"; Show("dev");
                results.Add(await WebDesignModelBench.RunAsync(backend, m, Skills.PromptText(null, "CORE-07", "neue seite", SkillLibrary.MaxPromptCharsLocal)));
            }
            WebDesignModelBench.Save(_core.DataRoot, results);
            _core.Audit.Append(Principal.LocalOwner.Name, "web.visuell.modellpruefung", string.Join(", ", results.Select(r => $"{r.Model}={(r.Passed ? "ok" : "nein")}")));
            WebDesignBenchNote = WebDesignModelBench.Recommend(WebHardware, WebDesignModelBench.Load(_core.DataRoot)).Note;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException) { WebDesignBenchNote = "Prüflauf abgebrochen: " + e.Message; }
        finally { _wdBenchBusy = false; Show("dev"); }
    }

    Control WebDesignAssetsPane()
    {
        var sp = new StackPanel { Spacing = 6, Name = "web-medien" };
        sp.Children.Add(new TextBlock { Text = "Medien des Projekts", FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Text });
        sp.Children.Add(P("Bilder, Videos, Schriften, Symbole und 3D-Modelle an einem Ort, jeweils mit Herkunft und Lizenz. Bilder werden für das Web verkleinert. Im Editor stehen sie bei Bild-Feldern zur Auswahl.", Muted, 12));
        var file = new TextBox { Name = "web-medium-datei", PlaceholderText = "Datei", Width = 240 };
        var art = new ComboBox { Name = "web-medium-art", ItemsSource = WebDesignAssets.Types.Keys.ToList(), SelectedIndex = 0, Width = 100 };
        var lic = new TextBox { Name = "web-medium-lizenz", PlaceholderText = "Lizenz, z. B. CC0 oder CC-BY-4.0", Width = 200 };
        var who = new TextBox { Name = "web-medium-urheber", PlaceholderText = "Urheber (bei CC-BY Pflicht)", Width = 180 };
        var origin = new TextBox { Name = "web-medium-herkunft", PlaceholderText = "Herkunft, z. B. eigenes Foto", Width = 180 };
        var pick = Act("web-medium-waehlen", "Datei wählen …", async () =>
        {
            var got = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Medium ins Projekt holen" });
            if (got.Select(f => Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(f)).OfType<string>().FirstOrDefault() is { } p) file.Text = p;
        });
        sp.Children.Add(new WrapPanel { Children = { file, new Border { Width = 6 }, pick, new Border { Width = 6 }, art } });
        sp.Children.Add(new WrapPanel { Children = { origin, new Border { Width = 6 }, lic, new Border { Width = 6 }, who, new Border { Width = 8 },
            Act("web-medium-holen", "Ins Projekt holen", () => WebDesignAssetImport(file.Text ?? "", art.SelectedItem?.ToString() ?? "bild", Path.GetFileNameWithoutExtension(file.Text ?? ""), origin.Text ?? "", lic.Text ?? "", who.Text ?? "", "", "", art.SelectedItem?.ToString() == "schrift" ? Path.GetFileNameWithoutExtension(file.Text ?? "") : null), true) } });
        var fontId = new TextBox { Name = "web-fontsource-id", PlaceholderText = "Schrift auf fontsource.org, z. B. inter", Width = 240 };
        sp.Children.Add(new WrapPanel { Children = { fontId, new Border { Width = 6 }, Act("web-fontsource-holen", "Schrift von Fontsource holen (kostenlos)", () => WebDesignFontFetch(fontId.Text ?? "")) } });
        var search = new TextBox { Name = "web-medium-suche", Text = _wdAssetQuery, PlaceholderText = "Suchen …", Width = 240 };
        search.TextChanged += (_, _) => _wdAssetQuery = search.Text ?? "";
        sp.Children.Add(new WrapPanel { Children = { search, new Border { Width = 6 }, Act("web-medium-suchen", "Suchen", () => Show("dev")) } });
        var list = new StackPanel { Name = "web-medien-liste", Spacing = 3 };
        var i = 0;
        foreach (var a in WebDesignAssets.Search(WebDesignDir, _wdAssetQuery).Take(40))
        {
            var fam = a.Familie; var n = i++;
            var warn = a.Lizenz.Length == 0 ? " · ⚠ Lizenz fehlt" : WebDesignAssets.NeedsAttribution(a.Lizenz) && a.Urheber.Length == 0 ? " · ⚠ Urheber fehlt" : "";
            Control right = a.Art == "schrift" && fam is not null ? Act($"web-medium-schrift-{n}", "Als Schrift verwenden", () => WebDesignUseFont(fam)) : new Border();
            list.Children.Add(Row(P($"{a.Art}: {a.Titel} · {a.Datei} · {(a.Lizenz.Length > 0 ? a.Lizenz : "ohne Lizenz")}{(a.Herkunft.Length > 0 ? " · " + a.Herkunft : "")}{warn}", warn.Length > 0 ? Brush.Parse("#F5A524") : Muted, 12), right));
        }
        sp.Children.Add(list);

        // Z6: lokales Modell für Webdesign
        sp.Children.Add(new TextBlock { Text = "KI auf diesem PC für Webdesign", FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Text, Margin = new Thickness(0, 8, 0, 0) });
        var rec = WebDesignModelBench.Recommend(WebHardware, WebDesignModelBench.Load(_core.DataRoot));
        sp.Children.Add(new TextBlock { Name = "web-modell-empfehlung", Text = WebDesignBenchNote.Length > 0 && _wdBenchBusy ? WebDesignBenchNote : rec.Note, FontSize = 12, Foreground = rec.Model is null ? Brush.Parse("#F5A524") : Text, TextWrapping = TextWrapping.Wrap });
        foreach (var r in WebDesignModelBench.Load(_core.DataRoot))
            sp.Children.Add(P($"{(r.Passed ? "✓" : "✗")} {r.Model}: {r.Tasks.Count(t => t.Passed)} von {r.Tasks.Count} Aufgaben, {r.Seconds:0} s · {string.Join("; ", r.Tasks.Where(t => !t.Passed).Select(t => t.Task + ": " + t.Note))}", Muted, 11.5));
        var run = Act("web-modell-pruefen", _wdBenchBusy ? "Prüflauf läuft …" : "Prüflauf starten", () => WebDesignBenchRun());
        run.IsEnabled = !_wdBenchBusy;
        sp.Children.Add(run);
        return new Border { Name = "web-medien-card", Padding = new Thickness(14), CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Child = sp };
    }
}
