using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using SkiaSharp;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Stufe 3, Teil 2 im Studio „Webseite bauen“: Code neben dem Design (K6), Import eigener HTML-Seiten und aus Figma (Z8), Skizze als Auftrag (K3), Veröffentlichen (K8).</summary>
public partial class MainWindow
{
    string _wdCodeNote = "", _wdImportNote = "", _wdPublishNote = "";
    bool _wdCodeOpen, _wdCodeBusy, _wdImportBusy, _wdPublishBusy;
    TextEditor? _wdCode;
    string? _wdCodeText, _wdCodeBase, _wdCodeWhere;
    int _wdCodeLine;
    (byte[] Png, int W, int H)? _wdSketch;
    WebDesignPublish.Plan? _wdPlan; string? _wdConsent; bool _wdAskConsent;
    IReadOnlyList<string> _wdExportFindings = [];

    /// <summary>Für Tests: HTML lesen ohne Browser (sonst GrapesJS-Kern in Chrome/Edge).</summary>
    public static Func<string, Task<(string? Json, string? Error)>>? WebDesignParseOverride { get; set; }
    /// <summary>Für Tests: Antworten von Figma nachbilden.</summary>
    public static HttpMessageHandler? FigmaHandlerOverride { get; set; }
    public string WebDesignCodeNote => _wdCodeNote;
    public string WebDesignImportNote => _wdImportNote;
    public string WebDesignPublishNote => _wdPublishNote;
    public string? WebDesignCodeText => _wdCode?.Text ?? _wdCodeText;
    public int WebDesignCodeLine => _wdCodeLine;
    public bool WebDesignHasSketch => _wdSketch is not null;

    Task<(string? Json, string? Error)> ParseHtml(string html) => WebDesignParseOverride is { } o ? o(html) : WebsitePreview.ParseHtmlAsync(WebDesignEditorDir, html);

    // ---------- K6: Code neben dem Design ----------

    public void WebDesignCodeShow()
    {
        _wdCodeOpen = true;
        _wdCodeBase = WebDesignCurrent(); _wdCodeWhere = CodeWhere();
        _wdCodeText = JsonNode.Parse(_wdCodeBase) is JsonObject doc ? WebDesignHtml.ToHtml(doc) : "";
        if (_wdCode is not null) _wdCode.Text = _wdCodeText;
        _wdCodeNote = "Code der Seite. Änderst du ihn hier, übernimmt „Code übernehmen“ ihn in den Editor. Ein Klick auf einen Baustein im Editor („Auswählen“) springt hierher.";
        Show("dev");
    }

    string CodeWhere() { var a = WebDesignSite.Active(WebDesignDir); return $"{a.Adresse}|{a.Sprache}|{_wdVariant}"; }

    /// <summary>Code schließen (beim Seitenwechsel): er gehört zur vorigen Seite.</summary>
    void WebDesignCodeClose() { _wdCodeOpen = false; _wdCodeText = null; _wdCodeBase = null; if (_wdCode is not null) _wdCode.Text = ""; }

    /// <summary>K6: Klick auf ein Element springt zur passenden Stelle im Code.</summary>
    public void WebDesignCodeJump(string id)
    {
        var text = WebDesignCodeText; if (text is null) return;
        var line = WebDesignHtml.LineOf(text, id); if (line == 0) return;
        _wdCodeLine = line;
        if (_wdCode is { } ed) Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (line <= ed.Document.LineCount) { ed.TextArea.Caret.Line = line; ed.TextArea.Caret.Column = 1; ed.ScrollToLine(line); } });
    }

    /// <summary>K6: Code zurück in den Editor. Eigene Bausteine kommen verlustfrei zurück; was nicht abbildbar ist, bleibt als HTML-Block und steht in der Meldeliste.</summary>
    public async Task WebDesignCodeApply(string? code = null)
    {
        if (_wdCodeBusy) return;
        var html = code ?? WebDesignCodeText ?? "";
        // Nur übernehmen, was zum aktuellen Stand passt: sonst gingen Änderungen im Editor oder eine andere Seite verloren
        if (_wdCodeBase is null || _wdCodeWhere != CodeWhere()) { _wdCodeNote = "Der Code gehört zu einer anderen Seite oder Variante. Bitte „Code neu aus dem Editor“ holen."; Show("dev"); return; }
        if (WebDesignCurrent() != _wdCodeBase) { _wdCodeNote = "Seit „Code zeigen“ wurde die Seite im Editor geändert. Nicht übernommen, damit nichts verloren geht. Bitte „Code neu aus dem Editor“ holen und die Änderung dort machen."; Show("dev"); return; }
        if (_wdAiBusy) { _wdCodeNote = "Die KI arbeitet gerade an der Seite. Bitte warten."; Show("dev"); return; }
        if (html.Trim().Length == 0) { _wdCodeNote = "Der Code ist leer. Nicht übernommen, sonst wäre die Seite leer."; Show("dev"); return; }
        _wdCodeBusy = true;
        try
        {
            _wdCodeNote = "Trendo liest den Code …"; Show("dev");
            var (json, err) = await ParseHtml(html);
            if (json is null) { _wdCodeNote = err ?? "Der Code ließ sich nicht lesen."; return; }
            var tree = (JsonNode.Parse(json) as JsonObject)?["knoten"] as JsonArray ?? [];
            var r = WebDesignHtml.FromTree(tree, new WebDesignHtml.Context(WebDesignDir, null));
            if (JsonNode.Parse(WebDesignCurrent()) is not JsonObject doc) return;
            doc["content"] = r.Content;
            var (ok, verr) = WebDesign.Validate(doc.ToJsonString());
            if (ok is null) { _wdCodeNote = "Nicht übernommen: " + verr; return; }
            WebDesignKeepDraft("Vor Code-Übernahme");
            WebDesignStates.SaveContent(WebDesignDir, "Vor Code-Übernahme", WebDesignStates.Read(WebDesignDir, _wdVariant), true, _wdVariant);
            var werr = WebDesignStates.Write(WebDesignDir, ok, _wdVariant);
            if (werr is not null) { _wdCodeNote = werr; return; }
            WebDesignReload(WebDesignStates.Read(WebDesignDir, _wdVariant));
            _wdCodeText = WebDesignHtml.ToHtml(JsonNode.Parse(ok)!.AsObject()); if (_wdCode is not null) _wdCode.Text = _wdCodeText;
            _wdCodeBase = WebDesignCurrent(); _wdCodeWhere = CodeWhere();
            _wdCodeNote = $"Übernommen: {r.Mapped} Bausteine{(r.HtmlBlocks > 0 ? $", {r.HtmlBlocks} als HTML-Block erhalten" : "")}. Der vorige Stand liegt bereit."
                + (r.Report.Count > 0 ? "\n" + string.Join("\n", r.Report.Take(8).Select(x => "• " + x)) : "");
            _core.Audit.Append(Principal.LocalOwner.Name, "web.code.uebernehmen", $"{r.Mapped}/{r.HtmlBlocks}");
        }
        catch (Exception e) when (e is TimeoutException or InvalidOperationException or IOException or System.Text.Json.JsonException) { _wdCodeNote = "Der Code ließ sich nicht lesen: " + e.Message; }
        finally { _wdCodeBusy = false; Show("dev"); }
    }

    // ---------- Z8: Import ----------

    /// <summary>Z8: eigene HTML-Datei als neue Seite. Eine Internet-Adresse wird abgelehnt.</summary>
    public async Task WebDesignImportHtml(string path)
    {
        if (_wdImportBusy) return;
        var p = (path ?? "").Trim().Trim('"');
        if (WebDesignImport.CheckSource(p) is { } err) { _wdImportNote = err; Show("dev"); return; }
        _wdImportBusy = true;
        try
        {
            _wdImportNote = "Trendo liest die Datei …"; Show("dev");
            var (json, perr) = await ParseHtml(await File.ReadAllTextAsync(p));
            if (json is null) { _wdImportNote = perr ?? "Die Datei ließ sich nicht lesen."; return; }
            WebDesignKeepDraft("Vor Import");
            var o = WebDesignImport.CreatePage(WebDesignDir, p, json);
            if (o.Error is not null) { _wdImportNote = o.Error; return; }
            _wdVariant = null; WebDesignClearSelection(); WebDesignReload(WebDesignStates.Read(WebDesignDir));
            _wdImportNote = $"Importiert als /{o.Adresse}: {o.Mapped} Bausteine{(o.HtmlBlocks > 0 ? $", {o.HtmlBlocks} als HTML-Block erhalten" : "")}." + (o.Report.Count > 0 ? "\nMeldeliste:\n" + string.Join("\n", o.Report.Select(x => "• " + x)) : "");
            _core.Audit.Append(Principal.LocalOwner.Name, "web.import.html", o.Adresse ?? "");
        }
        catch (Exception e) when (e is TimeoutException or InvalidOperationException or IOException) { _wdImportNote = "Import abgebrochen: " + e.Message; }
        finally { _wdImportBusy = false; Show("dev"); }
    }

    /// <summary>Z8 mit Figma: eigene Datei über den eigenen Zugriffsschlüssel. Der Schlüssel lebt nur für diesen Abruf und wird nirgends gespeichert oder protokolliert.</summary>
    public async Task WebDesignImportFigma(string link, string token)
    {
        if (_wdImportBusy) return;
        var (key, kerr) = WebDesignFigma.KeyOf(link);
        if (key is null) { _wdImportNote = kerr!; Show("dev"); return; }
        if (WebDesignFigma.CheckToken(token) is { } terr) { _wdImportNote = terr; Show("dev"); return; }
        _wdImportBusy = true;
        try
        {
            _wdImportNote = "Trendo holt die Figma-Datei …"; Show("dev");
            // Keine Weiterleitungen: der Schlüssel geht nur an api.figma.com, nie an ein Weiterleitungsziel
            using var http = FigmaHandlerOverride is null ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) : new HttpClient(FigmaHandlerOverride, false);
            http.Timeout = TimeSpan.FromSeconds(60); http.MaxResponseContentBufferSize = 50_000_000;
            http.DefaultRequestHeaders.Add("X-Figma-Token", token.Trim());
            http.DefaultRequestHeaders.UserAgent.ParseAdd("TrendoOS");
            using var r = await http.GetAsync($"https://{WebDesignFigma.ApiHost}/v1/files/{Uri.EscapeDataString(key)}");
            if ((int)r.StatusCode is >= 300 and < 400) { _wdImportNote = "Figma hat auf eine andere Adresse umgeleitet. Aus Sicherheitsgründen bricht Trendo ab; dein Schlüssel ging nur an api.figma.com."; return; }
            if (!r.IsSuccessStatusCode) { _wdImportNote = (int)r.StatusCode is 401 or 403 ? "Figma hat den Schlüssel abgelehnt oder die Datei gehört nicht zu deinem Konto." : $"Figma meldet Fehler {(int)r.StatusCode}."; return; }
            if (r.Content.Headers.ContentLength > 50_000_000) { _wdImportNote = "Die Figma-Datei ist zu groß für den Import."; return; }
            if (JsonNode.Parse(await r.Content.ReadAsStringAsync()) is not JsonObject file) { _wdImportNote = "Figma hat keine lesbare Datei geliefert."; return; }
            var map = WebDesignFigma.Map(file);
            var report = map.Report.ToList();
            var files = new Dictionary<string, string>();
            if (map.ImageNodes.Count > 0)
            {
                using var ir = await http.GetAsync($"https://{WebDesignFigma.ApiHost}/v1/images/{Uri.EscapeDataString(key)}?ids={WebDesignFigma.IdsParam(map.ImageNodes)}&format=png&scale=2");
                var urls = ir.IsSuccessStatusCode ? JsonNode.Parse(await ir.Content.ReadAsStringAsync())?["images"] as JsonObject : null;
                var dir = Path.Combine(WebDesignDir, "bilder", "figma"); Directory.CreateDirectory(dir);
                using var plain = FigmaHandlerOverride is null ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) : new HttpClient(FigmaHandlerOverride, false); // Bild-Adressen ohne den Schlüssel abrufen
                plain.Timeout = TimeSpan.FromSeconds(60); plain.MaxResponseContentBufferSize = 15_000_000;
                foreach (var id in map.ImageNodes)
                {
                    var url = urls?[id]?.ToString();
                    if (!WebDesignFigma.IsImageUrl(url)) { report.Add($"Bild {id}: keine gültige Adresse von Figma."); continue; }
                    using var img = await plain.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    if (!img.IsSuccessStatusCode || img.Content.Headers.ContentLength > 15_000_000) { report.Add($"Bild {id} ließ sich nicht laden."); continue; }
                    var bytes = await img.Content.ReadAsByteArrayAsync();
                    if (bytes.Length > 15_000_000 || bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != 0x50) { report.Add($"Bild {id} ist kein PNG."); continue; }
                    var name = WebDesignFigma.ImageName(key, id);
                    await File.WriteAllBytesAsync(Path.Combine(dir, name), bytes);
                    files[id] = "bilder/figma/" + name;
                }
            }
            WebDesignFigma.SetImages(map.Content, files);
            WebDesignKeepDraft("Vor Figma-Import");
            var slug = WebDesignSite.SlugOf(map.Title); if (WebDesignSite.CheckSlug(slug) is not null) slug = "figma";
            var pages = WebDesignSite.Pages(WebDesignDir); var basis = slug; for (var i = 2; pages.Contains(slug); i++) slug = $"{basis}-{i}";
            if (WebDesignSite.Create(WebDesignDir, slug, map.Title) is { } cerr) { _wdImportNote = cerr; return; }
            var doc = new JsonObject { ["root"] = new JsonObject { ["props"] = new JsonObject { ["title"] = map.Title, ["description"] = "" } }, ["content"] = map.Content, ["zones"] = new JsonObject() };
            if (WebDesign.Save(WebDesignDir, doc.ToJsonString()) is { } serr) { _wdImportNote = serr; return; }
            _wdVariant = null; WebDesignClearSelection(); WebDesignReload(WebDesignStates.Read(WebDesignDir));
            _wdImportNote = $"Aus Figma importiert als /{slug} ({files.Count} Bilder). Dein Schlüssel wurde nicht gespeichert.\nMeldeliste:\n" + string.Join("\n", report.Select(x => "• " + x));
            _core.Audit.Append(Principal.LocalOwner.Name, "web.import.figma", slug); // ohne Schlüssel und ohne Link
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or IOException or InvalidOperationException) { _wdImportNote = "Figma-Import abgebrochen: " + e.Message.Replace(token.Trim(), "…"); }
        finally { _wdImportBusy = false; Show("dev"); }
    }

    Control WebDesignCodePane()
    {
        var sp = new StackPanel { Spacing = 6, Name = "web-code" };
        sp.Children.Add(new TextBlock { Text = "⟨/⟩ Code und Import", FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Text });
        sp.Children.Add(P("Die aktive Seite als HTML: ändern und übernehmen, oder eigene HTML-Seiten und Figma-Dateien einlesen. Was Trendo nicht als Baustein abbilden kann, bleibt als HTML-Block erhalten und steht in der Meldeliste.", Muted, 12));
        var row = new WrapPanel();
        row.Children.Add(Act("web-code-zeigen", _wdCodeOpen ? "Code neu aus dem Editor" : "Code zeigen", WebDesignCodeShow));
        if (_wdCodeOpen) { row.Children.Add(new Border { Width = 8 }); var apply = Act("web-code-uebernehmen", _wdCodeBusy ? "Liest …" : "Code übernehmen", () => { _ = WebDesignCodeApply(); }, true); apply.IsEnabled = !_wdCodeBusy; row.Children.Add(apply); }
        sp.Children.Add(row);
        if (_wdCodeOpen && !_opt.Isolated)
        {
            _wdCode ??= new TextEditor { Name = "web-code-editor", ShowLineNumbers = true, FontFamily = new FontFamily("Cascadia Code, Consolas, Menlo, monospace"), FontSize = 13, Height = 360, WordWrap = false,
                SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("HTML"), Document = new TextDocument(_wdCodeText ?? "") };
            if (_wdCode.Parent is Decorator old) old.Child = null;
            sp.Children.Add(new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Child = _wdCode });
        }
        else if (_wdCodeOpen) sp.Children.Add(new TextBox { Name = "web-code-text", Text = _wdCodeText ?? "", AcceptsReturn = true, Height = 200, IsReadOnly = true });
        if (_wdCodeNote.Length > 0) sp.Children.Add(new TextBlock { Name = "web-code-note", Text = _wdCodeNote, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Muted });

        sp.Children.Add(new TextBlock { Text = "HTML-Seite importieren", FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Text, Margin = new Thickness(0, 8, 0, 0) });
        var path = new TextBox { Name = "web-import-pfad", PlaceholderText = "Eigene HTML-Datei auf diesem PC (keine Internet-Adresse)", Width = 420 };
        var pick = Act("web-import-waehlen", "Datei wählen …", async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "HTML-Seite wählen", AllowMultiple = false, FileTypeFilter = [new FilePickerFileType("HTML-Seite") { Patterns = ["*.html", "*.htm"] }] });
            if (files.Count > 0 && files[0].TryGetLocalPath() is { } lp) path.Text = lp;
        });
        var imp = Act("web-import-start", _wdImportBusy ? "Liest …" : "Als neue Seite importieren", () => { _ = WebDesignImportHtml(path.Text ?? ""); });
        imp.IsEnabled = !_wdImportBusy;
        sp.Children.Add(new WrapPanel { Children = { path, new Border { Width = 6 }, pick, new Border { Width = 6 }, imp } });
        sp.Children.Add(new TextBlock { Text = "Aus Figma holen", FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Text, Margin = new Thickness(0, 8, 0, 0) });
        sp.Children.Add(P("Nur eigene Dateien mit deinem persönlichen Zugriffsschlüssel aus Figma. Trendo nutzt ihn nur für diesen Abruf und speichert ihn nicht.", Muted, 11.5));
        var flink = new TextBox { Name = "web-figma-link", PlaceholderText = "https://www.figma.com/design/…", Width = 330 };
        var ftoken = new TextBox { Name = "web-figma-schluessel", PlaceholderText = "Zugriffsschlüssel", PasswordChar = '•', Width = 220 };
        var fgo = Act("web-figma-start", _wdImportBusy ? "Holt …" : "Aus Figma importieren", () => { var t = ftoken.Text ?? ""; ftoken.Text = ""; _ = WebDesignImportFigma(flink.Text ?? "", t); });
        fgo.IsEnabled = !_wdImportBusy;
        sp.Children.Add(new WrapPanel { Children = { flink, new Border { Width = 6 }, ftoken, new Border { Width = 6 }, fgo } });
        if (_wdImportNote.Length > 0) sp.Children.Add(new TextBlock { Name = "web-import-note", Text = _wdImportNote, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = AccentText });
        return Card("web-code-card", sp);
    }

    // ---------- K3: Skizze ----------

    /// <summary>K3: Striche aus dem Editor (PNG als data-URL) entgegennehmen.</summary>
    void WebDesignSketchReceived(string? data)
    {
        try
        {
            var o = JsonNode.Parse(data ?? "{}") as JsonObject;
            var url = o?["bild"]?.ToString() ?? "";
            const string prefix = "data:image/png;base64,";
            if (!url.StartsWith(prefix, StringComparison.Ordinal) || url.Length > 12_000_000) { _wdNote = "Die Skizze ließ sich nicht übernehmen."; return; }
            var bytes = Convert.FromBase64String(url[prefix.Length..]);
            var w = (int)Math.Clamp(WebDesign.Number(o!["breite"]) ?? 0, 0, 4000); var h = (int)Math.Clamp(WebDesign.Number(o["hoehe"]) ?? 0, 0, 8000);
            if (w < 50 || h < 50 || bytes.Length < 8 || bytes[0] != 0x89) { _wdNote = "Die Skizze ist leer oder zu klein."; return; }
            using (var codec = SKCodec.Create(new MemoryStream(bytes)))
                if (codec is null || codec.EncodedFormat != SKEncodedImageFormat.Png || codec.Info.Width > w || codec.Info.Height > h) { _wdNote = "Die Skizze hat ein unerwartetes Format."; return; } // schützt vor riesigen Bildern im kleinen PNG
            _wdSketch = (bytes, w, h);
            _wdNote = "Skizze bereit. Schreib dazu, was passieren soll, und klicke „Mit Skizze ändern“.";
        }
        catch (FormatException) { _wdNote = "Die Skizze ließ sich nicht lesen."; }
        catch (System.Text.Json.JsonException) { _wdNote = "Die Skizze ließ sich nicht lesen."; }
        finally { Avalonia.Threading.Dispatcher.UIThread.Post(() => Show("dev")); }
    }

    /// <summary>Für Tests: Skizze direkt setzen (sonst kommt sie aus dem Editor).</summary>
    public void WebDesignSetSketch(byte[] png, int w, int h) => _wdSketch = (png, w, h);

    /// <summary>K3: Skizze und Satz an eine bildfähige KI. Unter die Striche legt Trendo ein Bild der aktuellen Seite (über Chrome/Edge); ohne Browser gehen nur die Striche mit.
    /// Ohne bildfähige KI kommt ein klarer Hinweis, kein Fehler.</summary>
    public async Task WebDesignAiSketch(string wish)
    {
        if (_wdAiBusy) return;
        if (_wdSketch is not { } sk) { _wdNote = "Zeichne zuerst im Editor unter „Skizze“."; Show("dev"); return; }
        _wdAiBusy = true; var handed = false; string? image = null;
        try
        {
            var (claude, local, backends) = await WebKiState();
            var vision = WebDesignVisionOverride is not null || await WebVisionModel() is not null;
            var choice = WebDesignKi.Choose(WebDesignKi.Tasks["skizze"], claude, local, vision);
            if (!choice.Ok) { _wdNote = choice.Note; _wdKiWho = ""; return; }
            image = await WebDesignSketchImage(sk.Png, sk.W, sk.H);
            var current = WebDesignCurrent();
            var task = "skizze änderung layout " + wish;
            handed = true;
            var img = image;
            await WebDesignAiPlan("Skizze umsetzen", task, async () =>
            {
                var note = await WebImageNote(img, claude); if (note is null) return null;
                var ask = _wdTempImage is null ? backends : backends.Where(b => b is IMainKiBackend).ToList();
                try { return await WebKiAsk(WebDesignKi.Instructions(WebSkills(task, claude)), WebDesignKi.SketchPrompt(wish, current, note), ask); }
                finally { if (_wdTempImage is { } t) { try { File.Delete(t); } catch (IOException) { } _wdTempImage = null; } try { File.Delete(img); } catch (IOException) { } }
            }, current, null, backends, claude);
            _wdSketch = null;
        }
        finally { if (!handed) { _wdAiBusy = false; if (image is not null) try { File.Delete(image); } catch (IOException) { } Show("dev"); } }
    }

    /// <summary>Bild für die KI: aktuelle Seite in der Breite der Skizze, darüber die Striche.</summary>
    async Task<string> WebDesignSketchImage(byte[] strokes, int w, int h)
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-skizze-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var shot = Path.Combine(dir, "seite.png"); var result = Path.Combine(Path.GetTempPath(), "trendo-skizze-" + Guid.NewGuid().ToString("N")[..8] + ".png");
        try
        {
            if (!_opt.Isolated && WebDesign.ExportContent(WebDesignCurrent(), WebDesignDir, Path.Combine(dir, "seite"), WebDesignEditorDir).Error is null)
                await WebsitePreview.CaptureAsync(Path.Combine(dir, "seite"), w, Math.Min(h, 8000), shot);
        }
        catch (Exception e) when (e is TimeoutException or InvalidOperationException or IOException or System.Net.WebSockets.WebSocketException) { }
        using (var surface = SKSurface.Create(new SKImageInfo(w, h)))
        {
            var c = surface.Canvas; c.Clear(SKColors.White);
            if (File.Exists(shot)) using (var page = SKBitmap.Decode(shot)) if (page is not null) c.DrawBitmap(page, 0, 0);
            using (var st = SKBitmap.Decode(strokes)) if (st is not null) c.DrawBitmap(st, new SKRect(0, 0, w, h));
            using var img = surface.Snapshot(); using var png = img.Encode(SKEncodedImageFormat.Png, 90);
            await File.WriteAllBytesAsync(result, png.ToArray());
        }
        try { Directory.Delete(dir, true); } catch (IOException) { }
        return result;
    }

    // ---------- K8: Veröffentlichen ----------

    /// <summary>K8: Prüfen und Dateiliste zeigen. Grundlage ist der letzte Export samt seiner Prüfung.</summary>
    public void WebDesignPublishPrepare()
    {
        _wdPlan = WebDesignPublish.Prepare(WebDesignDir, _wdExportFindings);
        _wdConsent = null; _wdAskConsent = false;
        _wdPublishNote = _wdPlan.Ok ? $"Prüfung bestanden: {_wdPlan.Files.Count} Dateien, {_wdPlan.Bytes / 1024.0:0.#} KB." : "Prüfung nicht bestanden. Erst beheben, dann veröffentlichen.";
        Show("dev");
    }

    public void WebDesignPublishPackage()
    {
        if (_wdPlan is null) { WebDesignPublishPrepare(); if (_wdPlan is null) return; }
        var (zip, err) = WebDesignPublish.Package(WebDesignDir, _wdPlan!);
        _wdPublishNote = err ?? $"Paket gespeichert: {zip}. Es wurde nichts hochgeladen.";
        _core.Audit.Append(Principal.LocalOwner.Name, "web.paket", err ?? Path.GetFileName(zip!));
        Show("dev");
    }

    /// <summary>K8: Zustimmung für genau diese Dateien geben (ein Klick, gilt nur für diesen Stand).</summary>
    public void WebDesignPublishConsent() { if (_wdPlan is { Ok: true } p) { _wdConsent = p.Fingerprint; } }

    /// <summary>K8: Hochladen mit dem eigenen Netlify-Konto. Ohne Zustimmung für genau diese Dateien passiert nichts.</summary>
    public async Task WebDesignPublishUpload()
    {
        if (_wdPublishBusy) return;
        if (_wdPlan is null) { _wdPublishNote = "Erst prüfen und die Dateiliste ansehen."; Show("dev"); return; }
        var plan = _wdPlan;
        if (WebDesignPublish.CheckConsent(WebDesignDir, plan, _wdConsent) is { } err) { _wdPublishNote = err; _wdConsent = null; Show("dev"); return; }
        _wdConsent = null; _wdAskConsent = false; // Zustimmung gilt einmal
        if (!Netlify.Connected) { _wdPublishNote = "Nicht hochgeladen: Erst unter „Verbindungen“ mit deinem Netlify-Konto verbinden."; Show("dev"); return; }
        _wdPublishBusy = true;
        try
        {
            _wdPublishNote = "Lädt hoch …"; Show("dev");
            var (zip, zerr) = WebDesignPublish.ZipExactly(WebDesignDir, plan); // genau die gezeigten Dateien, Fingerabdruck über dieselben Bytes
            if (zip is null) { _wdPublishNote = "Nicht hochgeladen: " + zerr; return; }
            var url = await Netlify.DeployZipAsync(Path.Combine(WebDesignDir, WebDesign.ExportDir), zip, WebDesignSite.TitleOf(WebDesignDir, WebDesignSite.Index));
            _wdPublishNote = WebDesignPublish.Report(plan, url.Length > 0 ? url : "Netlify");
            _core.Audit.Append(Principal.LocalOwner.Name, "web.hochladen", $"{plan.Files.Count} Dateien → {url}");
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException or IOException) { _wdPublishNote = "Nicht hochgeladen: " + e.Message; }
        finally { _wdPublishBusy = false; _wdPlan = null; Show("dev"); }
    }

    Control WebDesignPublishPane()
    {
        var sp = new StackPanel { Spacing = 6, Name = "web-veroeffentlichen" };
        sp.Children.Add(new TextBlock { Text = "🚀 Veröffentlichen", FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Text });
        sp.Children.Add(P("Erst exportieren, dann prüfen. Danach als Paket speichern oder mit deinem eigenen Netlify-Konto hochladen. Hochgeladen wird nur nach deiner Zustimmung für genau die gezeigten Dateien.", Muted, 12));
        sp.Children.Add(new WrapPanel { Children = { Act("web-pub-pruefen", "Prüfen und Dateiliste zeigen", WebDesignPublishPrepare) } });
        if (_wdPlan is { } plan)
        {
            foreach (var b in plan.Blockers.Take(10)) sp.Children.Add(P("✗ " + b, Brush.Parse("#F5A524"), 12));
            foreach (var n in plan.Notes.Take(5)) sp.Children.Add(P("• " + n, Muted, 12));
            if (plan.Ok)
            {
                // Alle Dateien sichtbar, für die zugestimmt wird (in einer scrollbaren Liste)
                sp.Children.Add(new ScrollViewer { Name = "web-pub-liste", MaxHeight = 220, Content = P(string.Join("\n", plan.Files.Select(f => $"• {f.Rel} ({f.Bytes / 1024.0:0.#} KB)")), Muted, 11.5) });
                var row = new WrapPanel();
                row.Children.Add(Act("web-pub-paket", "Als Paket speichern (ZIP)", WebDesignPublishPackage));
                row.Children.Add(new Border { Width = 8 });
                if (!Netlify.Connected) row.Children.Add(P("Hochladen: erst unter „Verbindungen“ mit Netlify verbinden.", Muted, 12));
                else if (!_wdAskConsent) row.Children.Add(Act("web-pub-hochladen", "Zu Netlify hochladen …", () => { _wdAskConsent = true; Show("dev"); }));
                else
                {
                    var site = NetlifyLink.SiteOf(Path.Combine(WebDesignDir, WebDesign.ExportDir));
                    row.Children.Add(Act("web-pub-ja", $"Ja, genau diese {plan.Files.Count} Dateien zu {(site.Url is { Length: > 0 } u ? u : "einer neuen Netlify-Seite")} hochladen", () => { WebDesignPublishConsent(); _ = WebDesignPublishUpload(); }, true));
                    row.Children.Add(new Border { Width = 8 });
                    row.Children.Add(Act("web-pub-nein", "Abbrechen", () => { _wdAskConsent = false; _wdConsent = null; Show("dev"); }));
                }
                sp.Children.Add(row);
            }
        }
        if (_wdPublishNote.Length > 0) sp.Children.Add(new TextBlock { Name = "web-pub-note", Text = _wdPublishNote, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = AccentText });
        return Card("web-veroeffentlichen-card", sp);
    }
}
