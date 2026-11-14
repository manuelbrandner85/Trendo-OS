using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Stufe 1 der KI-Grundlage im Studio „Webseite bauen“: Die KI liefert einen Plan aus Editor-Aktionen (A1), Trendo wählt die passende KI (A7), lädt die passenden Fachregeln (A3),
/// prüft das Ergebnis auf dem Export (A6). Neue Seite aus Satz oder Bild (K4), Änderung nur an der Auswahl (K1), Text direkt ändern (K2), Stände und Varianten (K5).
/// Jedes KI-Ergebnis bleibt eine Vorschau, bis es als ein Schritt übernommen wird.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: KI nachbilden (Anweisungen, Auftrag → Antwort).</summary>
    public static Func<string, string, Task<string>>? WebDesignKiOverride { get; set; }
    /// <summary>Für Tests: bildfähige KI nachbilden (Bilddatei, Frage → Beschreibung).</summary>
    public static Func<string, string, Task<string?>>? WebDesignVisionOverride { get; set; }
    /// <summary>Für Tests: Prüfung des Exports nachbilden.</summary>
    public static Func<string, Task<WebAuditResult>>? WebDesignAuditOverride { get; set; }
    /// <summary>Für Tests: Ist Claude über das Abo bereit, gibt es eine lokale KI?</summary>
    public static (bool Claude, bool Local)? WebDesignKiStateOverride { get; set; }

    string _wdAiText = "", _wdAiImage = "", _wdStandName = "", _wdVariantName = "", _wdKiWho = "", _wdLabel = "";
    bool _wdAiBusy; string? _wdDraft, _wdBase, _wdLastAuto, _wdVariant, _wdPendingVariant, _wdTempImage;
    WebPlanResult? _wdPending; WebPlanScope? _wdPendingScope; WebDesignCheck.LoopResult? _wdLoop; WebAuditResult? _wdAudit;
    List<string> _wdSelection = [];
    readonly Dictionary<string, string> _wdVariantImages = [];

    public WebPlanResult? WebDesignPending => _wdPending;
    public WebDesignCheck.LoopResult? WebDesignLoop => _wdLoop;
    public IReadOnlyList<string> WebDesignSelection => _wdSelection;
    public string WebDesignKiWho => _wdKiWho;
    public string? WebDesignVariant => _wdVariant;
    public void WebDesignSetSelection(params string[] ids) => _wdSelection = [.. ids];

    /// <summary>Was gerade gilt: ungespeicherter Entwurf aus dem Editor, sonst die gespeicherte Seite oder Variante.</summary>
    string WebDesignCurrent() => _wdDraft ?? WebDesignStates.Read(WebDesignDir, _wdVariant);
    // Daten gehen als JSON-Text ins Skript, nie als Skriptcode
    static string JsArg(string? s) => System.Text.Json.JsonSerializer.Serialize(s);
    void WebDesignReload(string json) => _wdView?.InvokeScript("window.trendoLoad(" + JsArg(json) + ")");
    void WebDesignShowPreview(string? json) => _wdView?.InvokeScript("window.trendoVorschau(" + JsArg(json) + ")");
    void WebDesignClearSelection() { _wdSelection = []; _wdView?.InvokeScript("window.trendoAuswahl([])"); }

    /// <summary>Solange die KI arbeitet, bleibt das Ziel fest. Liefert false und einen Hinweis.</summary>
    bool WebDesignIdle()
    {
        if (!_wdAiBusy) return true;
        _wdNote = "Die KI arbeitet noch. Bitte warte, bis sie fertig ist."; Show("dev"); return false;
    }

    void WebDesignDropPending()
    {
        if (_wdPending is null) return;
        _wdPending = null; _wdLoop = null; _wdAudit = null; WebDesignShowPreview(null);
    }

    /// <summary>Ungespeicherter Entwurf wird vor jedem Wechsel als automatischer Stand gesichert, damit er nicht verloren geht.</summary>
    void WebDesignKeepDraft(string label)
    {
        if (_wdDraft is { } d && d != WebDesignStates.Read(WebDesignDir, _wdVariant)) WebDesignStates.SaveContent(WebDesignDir, label, d, true, _wdVariant);
        _wdDraft = null;
    }

    async Task<(bool Claude, bool Local, IReadOnlyList<IKiBackend> Backends)> WebKiState()
    {
        if (WebDesignKiOverride is not null && WebDesignKiStateOverride is null) return (false, true, []);
        var all = WebDesignKi.Allowed(KiBackendsOverride?.Invoke() ?? DefaultKiBackends());
        if (WebDesignKiStateOverride is { } s) return (s.Claude, s.Local, all);
        var claude = false; var local = false;
        foreach (var b in all)
            try
            {
                if (!await b.IsUpAsync() || (await b.ModelsAsync()).Count == 0) continue;
                if (b is IMainKiBackend) claude = true; else local = true;
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException) { }
        return (claude, local, all);
    }

    async Task<string?> WebVisionModel()
    {
        foreach (var m in WebDesignKi.VisionModels) if (!_opt.Isolated && await _ollama.HasModelAsync(m)) return m;
        return null;
    }

    async Task<string> WebKiAsk(string system, string prompt, IReadOnlyList<IKiBackend> backends)
    {
        if (WebDesignKiOverride is { } o) { _wdKiWho = "Test-KI"; return await o(system, prompt); }
        var core = new IntelligenceCore(_core.DataRoot, _core.Audit, () => backends); // derselbe Kern wie überall, nur auf Claude über das Abo und KIs auf diesem PC beschränkt
        var a = await core.AskAsync(new KiRequest(prompt, system, UseKnowledge: false));
        if (!a.Ok) throw new InvalidOperationException(a.Text);
        _wdKiWho = backends.FirstOrDefault(b => b.Name == a.Backend) is IMainKiBackend ? WebDesignKi.ClaudeLabel : $"{WebDesignKi.LocalLabel} ({a.Model})";
        return a.Text;
    }

    string? WebSkills(string task, bool claude) => Skills.PromptText(null, "CORE-07", task, claude ? SkillLibrary.MaxPromptChars : SkillLibrary.MaxPromptCharsLocal);

    /// <summary>Beschreibung bzw. Hinweis zu einem eigenen Bild für die Text-KI. Null: es geht gerade nicht, der Grund steht dann in <see cref="WebDesignNote"/>.</summary>
    async Task<string?> WebImageNote(string image, bool claude)
    {
        const string question = "Beschreibe knapp den Aufbau dieser Webseite oder Skizze: Abschnitte von oben nach unten, Farben als #RRGGBB, Schriftcharakter, Texte. Nur die Beschreibung.";
        if (WebDesignVisionOverride is { } v) return await v(image, question);
        if (claude)
        {
            var dir = Path.Combine(Path.GetTempPath(), "trendo-claude-frage"); Directory.CreateDirectory(dir); // der Arbeitsordner von Claude Code im Frage-Modus
            var name = "bild-" + Guid.NewGuid().ToString("N")[..8] + Path.GetExtension(image);
            File.Copy(image, Path.Combine(dir, name), true); _wdTempImage = Path.Combine(dir, name);
            return $"Lies zuerst die Bilddatei „{name}“ im Arbeitsordner mit dem Read-Werkzeug und orientiere dich an ihrem Aufbau, ihren Farben und Texten.";
        }
        if (await WebVisionModel() is { } m && await _ollama.AskImagesAsync(m, question, [File.ReadAllBytes(image)]) is { Length: > 0 } text) return text;
        _wdNote = "Die KI konnte dein Bild nicht auswerten.";
        return null;
    }

    async Task<WebAuditResult> WebDesignProbe(string json)
    {
        if (WebDesignAuditOverride is { } o) return await o(json);
        if (_opt.Isolated) return new(WebDesignCheck.CheckData(json, WebDesignDir), [], "Testmodus: ohne Browser geprüft.");
        return await WebDesignAudit.RunAsync(json, WebDesignDir, WebDesignEditorDir, AxeRunOverride is not null ? "node" : Cli.Find("node"), AxeDir, AxeRunOverride);
    }

    /// <summary>K4: Neue Seite aus einem Satz und optional einem eigenen Bild. Das Ergebnis sind Editor-Daten, zuerst als Vorschau.</summary>
    public async Task WebDesignAiRun(string wish, string? image = null)
    {
        if (_wdAiBusy || string.IsNullOrWhiteSpace(wish)) return;
        _wdAiBusy = true; var handed = false; // sofort belegt: kein Doppelstart, solange noch geprüft wird, welche KI da ist
        try
        {
            _wdAiText = wish; image = string.IsNullOrWhiteSpace(image) ? null : image.Trim().Trim('"');
            var (claude, local, backends) = await WebKiState();
            var vision = WebDesignVisionOverride is not null || (image is not null && await WebVisionModel() is not null);
            var choice = WebDesignKi.Choose(image is null ? WebDesignKi.Tasks["seite_aus_text"] : WebDesignKi.Tasks["seite_aus_bild"], claude, local, vision);
            if (!choice.Ok) { _wdNote = choice.Note; _wdKiWho = ""; return; }
            if (image is not null && (!File.Exists(image) || Path.GetExtension(image).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg" or ".webp"))) { _wdNote = "Bitte ein vorhandenes Bild (PNG, JPG oder WebP) angeben."; return; }
            var task = "neue seite layout gestaltung responsiv " + wish;
            handed = true;
            await WebDesignAiPlan("Neue Seite", task, async () =>
            {
                string? note = null;
                if (image is not null) { note = await WebImageNote(image, claude); if (note is null) return null; }
                var ask = _wdTempImage is null ? backends : backends.Where(b => b is IMainKiBackend).ToList(); // nur Claude kann die Bilddatei lesen; eine lokale Text-KI bekäme den Hinweis ohne Bild
                try { return await WebKiAsk(WebDesignKi.Instructions(WebSkills(task, claude)), WebDesignKi.NewPagePrompt(wish, note), ask); }
                finally { if (_wdTempImage is { } t) { try { File.Delete(t); } catch (IOException) { } _wdTempImage = null; } } // die Kopie für Claude bleibt nicht liegen
            }, WebDesign.Empty(), null, backends, claude);
        }
        finally { if (!handed) { _wdAiBusy = false; Show("dev"); } }
    }

    /// <summary>K1: Nur die Auswahl per Satz ändern, mit Vorschau und einem Rückgängig-Schritt.</summary>
    public async Task WebDesignAiChange(string wish)
    {
        if (_wdAiBusy || string.IsNullOrWhiteSpace(wish)) return;
        _wdAiText = wish;
        if (_wdSelection.Count == 0) { _wdNote = "Wähle zuerst mit „Auswählen“ im Editor aus, was sich ändern soll."; Show("dev"); return; }
        _wdAiBusy = true; var handed = false;
        try
        {
            var (claude, local, backends) = await WebKiState();
            var choice = WebDesignKi.Choose(WebDesignKi.Tasks["auswahl_aendern"], claude, local, false);
            if (!choice.Ok) { _wdNote = choice.Note; return; }
            var current = WebDesignCurrent(); var scope = new WebPlanScope(_wdSelection.ToHashSet());
            handed = true;
            await WebDesignAiPlan($"{_wdSelection.Count} Bausteine ändern", wish, () => WebKiAsk(WebDesignKi.Instructions(WebSkills(wish, claude), scope), WebDesignKi.SelectionPrompt(wish, current, _wdSelection), backends)!, current, scope, backends, claude);
        }
        finally { if (!handed) { _wdAiBusy = false; Show("dev"); } }
    }

    /// <summary>Gemeinsamer Weg jeder KI-Änderung: Plan holen, prüfen, im Prüfkreislauf nachbessern, als Vorschau bereitstellen. Geschrieben wird erst beim Übernehmen.</summary>
    async Task WebDesignAiPlan(string label, string task, Func<Task<string?>> ask, string planBase, WebPlanScope? scope, IReadOnlyList<IKiBackend> backends, bool claude)
    {
        _wdAiBusy = true;
        try
        {
            WebDesignDropPending(); _wdLoop = null; _wdAudit = null; _wdNote = "Die KI arbeitet …"; Show("dev");
            var target = _wdVariant; var startBase = WebDesignCurrent();
            var answer = await ask();
            if (answer is null) return;
            _core.Audit.Append(Principal.LocalOwner.Name, "web.visuell.ki", label);
            var r = WebDesignActions.Apply(planBase, answer, scope);
            if (r.Json is null) { _wdNote = "Die Antwort der KI wurde abgelehnt, nichts wurde geändert: " + r.Error; return; }
            WebAuditResult? audit = null;
            var loop = await WebDesignCheck.RunAsync(r.Json, async j => { audit = await WebDesignProbe(j); return audit.Findings; }, async (doc, errors) =>
            {
                var ids = scope?.Ids.ToList();
                var relevant = ids is null ? errors : errors.Where(e => ids.Any(i => e.Text.Contains(i, StringComparison.Ordinal))).ToList(); // bei einer Auswahl nur Fehler an der Auswahl nachbessern
                if (relevant.Count == 0) return null;
                try { return await WebKiAsk(WebDesignKi.Instructions(WebSkills(task, claude), scope), WebDesignKi.RepairPrompt(doc, relevant, ids), backends); }
                catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException) { return null; }
            }, scope);
            if (_wdVariant != target || (scope is not null && WebDesignCurrent() != startBase)) { _wdNote = "Die Seite wurde geändert, während die KI gearbeitet hat. Der Vorschlag wurde verworfen, nichts wurde geändert."; return; }
            _wdBase = WebDesignCurrent(); _wdLabel = label; _wdPendingScope = scope; _wdPendingVariant = target; _wdLoop = loop; _wdAudit = audit;
            var changes = r.Changes.Concat(loop.Rounds.SelectMany(x => x.Repairs)).ToList();
            _wdPending = r with { Json = loop.Json, Changes = changes };
            if (await WebDesignVisionHint(audit) is { } hint) _wdAudit = audit! with { Note = (audit!.Note + " " + hint).Trim() };
            WebDesignShowPreview(loop.Json);
            _wdNote = loop.Clean ? "Vorschau fertig. Prüfen und übernehmen." : loop.LimitReached ? $"Vorschau fertig. Nach {loop.Repairs} Nachbesserungen sind noch Fehler offen." : "Vorschau fertig, es sind noch Fehler offen.";
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException or IOException or ArgumentException or UnauthorizedAccessException) { _wdNote = e.Message; }
        finally { _wdAiBusy = false; Show("dev"); }
    }

    /// <summary>Bildbewertung durch eine lokale KI, wenn eine da ist. Nur ein Hinweis, kein Nachweis.</summary>
    async Task<string?> WebDesignVisionHint(WebAuditResult? audit)
    {
        if (audit is null || audit.Images.Count == 0) return null;
        const string q = "Was wirkt an dieser Webseite auf dem Bild kaputt oder unordentlich? Höchstens zwei kurze Sätze.";
        try
        {
            var text = WebDesignVisionOverride is { } v ? await v(audit.Images[0], q) : await WebVisionModel() is { } m ? await _ollama.AskImagesAsync(m, q, [File.ReadAllBytes(audit.Images[0])]) : null;
            return text is { Length: > 0 } ? "KI-Bildbewertung (nur ein Hinweis, kein Nachweis): " + text.Trim() : null;
        }
        catch (IOException) { return null; }
    }

    /// <summary>Vorschau übernehmen: ein Schritt, vorher entsteht automatisch ein Stand.</summary>
    public void WebDesignAccept()
    {
        if (_wdPending is not { Json: { } json } p || _wdBase is null) return;
        if (!WebDesignIdle()) return;
        if (_wdPendingVariant != _wdVariant) { WebDesignDropPending(); _wdNote = "Der Vorschlag gehörte zu einer anderen Fassung und wurde verworfen."; Show("dev"); return; }
        var (auto, err) = WebDesignActions.Commit(WebDesignDir, p, _wdBase, _wdLabel, _wdVariant);
        if (auto is null) { _wdNote = err ?? "Übernehmen ging nicht."; Show("dev"); return; }
        _wdLastAuto = auto; _wdDraft = null; _wdPending = null; WebDesignClearSelection();
        _core.Audit.Append(Principal.LocalOwner.Name, "web.visuell.ki.uebernehmen", _wdLabel);
        WebDesignShowPreview(null); WebDesignReload(json);
        _wdNote = "Übernommen. Du kannst weiter im Editor ändern oder die KI-Änderung rückgängig machen.";
        Show("dev");
    }

    public void WebDesignDiscard() { WebDesignDropPending(); _wdNote = "Verworfen, die Seite ist unverändert."; Show("dev"); }

    /// <summary>Letzte KI-Änderung in einem Schritt zurücknehmen.</summary>
    public void WebDesignUndo()
    {
        if (_wdLastAuto is not { } id || !WebDesignIdle()) return;
        WebDesignDropPending(); WebDesignKeepDraft("Entwurf vor Rückgängig");
        var err = WebDesignStates.Restore(WebDesignDir, id);
        _wdNote = err ?? "KI-Änderung rückgängig gemacht.";
        _core.Audit.Append(Principal.LocalOwner.Name, "web.visuell.rueckgaengig", err ?? "ok");
        if (err is null) { _wdLastAuto = null; WebDesignReload(WebDesignStates.Read(WebDesignDir, _wdVariant)); }
        Show("dev");
    }

    /// <summary>K2 und alle anderen Aktionen des Menschen: derselbe geprüfte Weg wie bei der KI, ohne KI und ohne Kosten.</summary>
    public void WebDesignHumanAction(string? planJson)
    {
        var r = WebDesignActions.Apply(WebDesignCurrent(), planJson ?? "");
        var err = r.Error ?? WebDesignStates.Write(WebDesignDir, r.Json!, _wdVariant);
        if (err is null) { foreach (var n in r.States) WebDesignStates.SaveContent(WebDesignDir, n, r.Json!, false, _wdVariant); _wdDraft = null; WebDesignReload(r.Json!); }
        _wdNote = err ?? "Gespeichert: " + string.Join(", ", r.Changes);
        _core.Audit.Append(Principal.LocalOwner.Name, "web.visuell.aktion", err ?? "ok");
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Show("dev"));
    }

    /// <summary>A6 von Hand: gespeicherte Seite prüfen, ohne Änderung.</summary>
    public async Task WebDesignCheckNow()
    {
        if (_wdAiBusy) return;
        _wdAiBusy = true;
        try
        {
            _wdNote = "Die Seite wird geprüft …"; Show("dev");
            WebAuditResult? audit = null;
            _wdLoop = await WebDesignCheck.RunAsync(WebDesignStates.Read(WebDesignDir, _wdVariant), async j => { audit = await WebDesignProbe(j); return audit.Findings; });
            _wdAudit = audit;
            _wdNote = _wdLoop.Clean ? "Prüfung ohne Fehler." : "Die Prüfung hat Fehler gefunden.";
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or TaskCanceledException or UnauthorizedAccessException or ArgumentException) { _wdNote = e.Message; }
        finally { _wdAiBusy = false; Show("dev"); }
    }

    // ---------- K5: Stände und Varianten ----------

    public void WebDesignStandSave(string name)
    {
        var (s, err) = WebDesignStates.SaveContent(WebDesignDir, name, WebDesignCurrent(), false, _wdVariant);
        _wdNote = err ?? $"Stand „{s!.Name}“ gespeichert."; if (err is null) _wdStandName = ""; Show("dev");
    }

    public void WebDesignStandRestore(string id)
    {
        if (!WebDesignIdle()) return;
        WebDesignDropPending(); WebDesignKeepDraft("Entwurf vor Wiederherstellen");
        var err = WebDesignStates.Restore(WebDesignDir, id);
        _wdNote = err ?? "Stand wiederhergestellt. Der vorige Inhalt liegt als automatischer Stand bereit.";
        _core.Audit.Append(Principal.LocalOwner.Name, "web.visuell.stand.wiederherstellen", err ?? "ok");
        if (err is null) WebDesignReload(WebDesignStates.Read(WebDesignDir, _wdVariant));
        Show("dev");
    }

    public void WebDesignVariantCreate(string name)
    {
        var (v, err) = WebDesignStates.CreateVariant(WebDesignDir, name, _wdVariant);
        _wdNote = err ?? $"Variante „{v!.Name}“ angelegt."; if (err is null) _wdVariantName = ""; Show("dev");
    }

    /// <summary>Variante im Editor öffnen (null: zurück zur Seite). Ab jetzt wirken Speichern, KI und Stände auf diese Fassung.</summary>
    public void WebDesignVariantOpen(string? id)
    {
        if (!WebDesignIdle()) return;
        WebDesignDropPending(); WebDesignKeepDraft("Entwurf vor Wechsel der Fassung");
        _wdVariant = id; WebDesignClearSelection();
        WebDesignReload(WebDesignStates.Read(WebDesignDir, id));
        _wdNote = id is null ? "Du bearbeitest wieder die Seite." : "Du bearbeitest jetzt eine Variante. Die Seite bleibt unverändert.";
        Show("dev");
    }

    public void WebDesignVariantAdopt(string id)
    {
        if (!WebDesignIdle()) return;
        WebDesignDropPending(); WebDesignKeepDraft("Entwurf vor Übernahme einer Variante");
        var err = WebDesignStates.AdoptVariant(WebDesignDir, id);
        _wdNote = err ?? "Variante übernommen. Die bisherige Seite liegt als automatischer Stand bereit.";
        _core.Audit.Append(Principal.LocalOwner.Name, "web.visuell.variante.uebernehmen", err ?? "ok");
        if (err is null) { _wdVariant = null; WebDesignClearSelection(); WebDesignReload(WebDesignStates.Read(WebDesignDir)); }
        Show("dev");
    }

    /// <summary>Bilder aller Varianten erzeugen, um sie nebeneinander zu sehen.</summary>
    public async Task WebDesignVariantPreviews()
    {
        if (_opt.Isolated || WebsitePreview.FindBrowser() is null) { _wdNote = "Für die Bilder der Varianten wird Chrome oder Edge gebraucht."; Show("dev"); return; }
        _wdNote = "Bilder der Varianten entstehen …"; Show("dev");
        var root = Path.Combine(WebDesignDir, WebDesignAudit.WorkDir); var site = Path.Combine(root, "variante");
        foreach (var v in WebDesignStates.Variants(WebDesignDir))
            try
            {
                if (Directory.Exists(site)) Directory.Delete(site, true);
                var (dir, err) = WebDesign.ExportContent(WebDesignStates.Read(WebDesignDir, v.Id), WebDesignDir, site, WebDesignEditorDir);
                if (dir is null) { _wdNote = err ?? ""; continue; }
                var png = Path.Combine(root, "variante-" + v.Id + ".png");
                if ((await WebsitePreview.CaptureAsync(dir, 1280, 800, png)).Ok) _wdVariantImages[v.Id] = png;
            }
            catch (Exception e) when (e is InvalidOperationException or TimeoutException or IOException or OperationCanceledException) { _wdNote = e.Message; }
        try { if (Directory.Exists(site)) Directory.Delete(site, true); } catch (IOException) { /* Browser hält die Datei noch */ } catch (UnauthorizedAccessException) { }
        if (_wdNote.StartsWith("Bilder der", StringComparison.Ordinal)) _wdNote = "Bilder aktualisiert.";
        Show("dev");
    }

    // ---------- Oberfläche ----------

    static Image WebThumb(string file, double width, string? name = null)
    {
        var img = new Image { Width = width, Stretch = Stretch.Uniform, Name = name };
        try { using var ms = new MemoryStream(File.ReadAllBytes(file)); img.Source = new Bitmap(ms); } catch (Exception e) when (e is IOException or ArgumentException) { }
        return img;
    }

    Control WebDesignKiPane()
    {
        var sp = new StackPanel { Spacing = 8, Name = "web-ki" };
        var claudeReady = _claude.Ready && !CLimit.Active(DateTimeOffset.Now);
        sp.Children.Add(new TextBlock { Text = "Seite mit KI bauen und ändern", FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Text });
        sp.Children.Add(new TextBlock { Name = "web-ki-who", FontSize = 12, Foreground = claudeReady ? ClaudeAccent : Muted, TextWrapping = TextWrapping.Wrap,
            Text = claudeReady ? "Claude über dein Abo. Ohne Claude arbeitet eine KI auf diesem PC." : "KI auf diesem PC. Mit Claude (Seite „KI“) arbeitet hier dein Abo." });
        if (_wdVariant is { } activeVariant) sp.Children.Add(new WrapPanel { Children = { P("Du bearbeitest eine Variante, die Seite bleibt unverändert.", Brush.Parse("#F5A524"), 12.5), new Border { Width = 8 }, Act("web-var-back", "Zurück zur Seite", () => WebDesignVariantOpen(null)) } });
        var wish = new TextBox { Name = "web-ki-wish", Text = _wdAiText, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 56, FontSize = 14, PlaceholderText = "Beschreibe die Seite oder die Änderung, zum Beispiel: Eine ruhige Seite für eine Bäckerei mit Öffnungszeiten" };
        wish.TextChanged += (_, _) => _wdAiText = wish.Text ?? "";
        var image = new TextBox { Name = "web-ki-image", Text = _wdAiImage, PlaceholderText = "Optional: eigenes Bild als Vorlage (Pfad zu PNG, JPG oder WebP)", MinWidth = 360 };
        image.TextChanged += (_, _) => _wdAiImage = image.Text ?? "";
        sp.Children.Add(wish);
        sp.Children.Add(new WrapPanel { Children = { image, new Border { Width = 8 }, Act("web-ki-pick", "Bild wählen …", async () =>
        {
            var got = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Bild als Vorlage", FileTypeFilter = [new("Bilder") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp"] }] });
            if (got.Select(f => Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(f)).OfType<string>().FirstOrDefault() is { } path) image.Text = path;
        }) } });
        var btnNew = Act("web-ki-new", _wdAiBusy ? "KI arbeitet …" : "Neue Seite daraus", () => WebDesignAiRun(wish.Text ?? "", image.Text), _wdSelection.Count == 0);
        var btnChange = Act("web-ki-change", _wdSelection.Count == 0 ? "Auswahl ändern" : $"Nur Auswahl ändern ({_wdSelection.Count})", () => WebDesignAiChange(wish.Text ?? ""), _wdSelection.Count > 0);
        var btnCheck = Act("web-ki-check", "Seite prüfen", () => WebDesignCheckNow());
        btnNew.IsEnabled = !_wdAiBusy; btnChange.IsEnabled = !_wdAiBusy && _wdSelection.Count > 0; btnCheck.IsEnabled = !_wdAiBusy;
        var row = new WrapPanel { Children = { btnNew, new Border { Width = 8 }, btnChange, new Border { Width = 8 }, btnCheck } };
        if (_wdLastAuto is not null) { row.Children.Add(new Border { Width = 8 }); row.Children.Add(Act("web-ki-undo", "KI-Änderung rückgängig", WebDesignUndo)); }
        sp.Children.Add(row);
        sp.Children.Add(new TextBlock { Name = "web-ki-selection", FontSize = 12, Foreground = Muted, TextWrapping = TextWrapping.Wrap, Text = _wdSelection.Count == 0 ? "Im Editor unter „Auswählen“ Bausteine anklicken: Die KI ändert dann nur diese. Doppelklick auf einen Text ändert ihn direkt, ohne KI." : "Ausgewählt: " + string.Join(", ", _wdSelection) });

        if (_wdPending is { Json: not null } p)
        {
            var box = new StackPanel { Name = "web-ki-preview", Spacing = 3 };
            box.Children.Add(P($"Vorschau: {_wdLabel}" + (_wdKiWho.Length > 0 ? $" · erstellt von {_wdKiWho}" : ""), Text, 12.5));
            foreach (var l in p.Changes.Take(14)) box.Children.Add(P("• " + l, Muted, 12));
            if (p.Changes.Count > 14) box.Children.Add(P($"… und {p.Changes.Count - 14} weitere Änderungen", Muted, 12));
            sp.Children.Add(box);
            sp.Children.Add(new WrapPanel { Children = { Act("web-ki-accept", "✓ Übernehmen", WebDesignAccept, true), new Border { Width = 8 }, Act("web-ki-discard", "✕ Verwerfen", WebDesignDiscard) } });
        }
        if (_wdLoop is { } loop)
        {
            var box = new StackPanel { Name = "web-ki-check-result", Spacing = 3 };
            var errors = loop.Findings.Where(f => f.Level == "Fehler").ToList();
            var what = _wdAudit is { Images.Count: 0 } ? "Prüfung nur der Editor-Daten, nicht vollständig" : "Prüfung auf dem Export";
            box.Children.Add(P(loop.Clean ? $"{what}: keine Fehler{(loop.Repairs > 0 ? $" nach {loop.Repairs} Nachbesserung(en)" : "")}." : $"{what}: {errors.Count} Fehler offen{(loop.LimitReached ? $", Obergrenze von {WebDesignCheck.MaxRepairs} Nachbesserungen erreicht" : "")}.", loop.Clean ? Text : Brush.Parse("#F5A524"), 12.5));
            foreach (var f in errors.Take(10)) box.Children.Add(P("✗ " + f.Area + ": " + f.Text, Muted, 12));
            foreach (var f in loop.Findings.Where(f => f.Level == "Hinweis").Take(6)) box.Children.Add(P("• " + f.Area + ": " + f.Text, Muted, 12));
            if (_wdAudit is { Note.Length: > 0 } a) box.Children.Add(P(a.Note, Muted, 12));
            if (_wdAudit is { Images.Count: > 0 } imgs)
            {
                var strip = new WrapPanel { Name = "web-ki-images" };
                foreach (var f in imgs.Images) strip.Children.Add(new Border { Margin = new Thickness(0, 4, 8, 4), CornerRadius = new CornerRadius(8), ClipToBounds = true, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Child = WebThumb(f, 190) });
                box.Children.Add(strip);
            }
            sp.Children.Add(box);
        }

        var standName = new TextBox { Name = "web-stand-name", Text = _wdStandName, PlaceholderText = "Name des Stands", Width = 220 };
        standName.TextChanged += (_, _) => _wdStandName = standName.Text ?? "";
        var states = new StackPanel { Name = "web-staende", Spacing = 3, Children = { new TextBlock { Text = "Stände", FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Text }, new WrapPanel { Children = { standName, new Border { Width = 8 }, Act("web-stand-save", "Stand speichern", () => WebDesignStandSave(standName.Text ?? "")) } } } };
        var i = 0;
        var all = WebDesignStates.List(WebDesignDir);
        foreach (var s in all.Where(x => !x.Auto).Concat(all.Where(x => x.Auto).Take(8)).OrderByDescending(x => x.Id, StringComparer.Ordinal))
        {
            var id = s.Id;
            states.Children.Add(Row(P($"{(s.Auto ? "⟲ " : "")}{s.Name} · {s.Zeit:dd.MM. HH:mm}{(s.Variante is null ? "" : " · Variante")}", Muted, 12), Act($"web-stand-restore-{i++}", "Wiederherstellen", () => WebDesignStandRestore(id))));
        }
        sp.Children.Add(states);

        var variantName = new TextBox { Name = "web-var-name", Text = _wdVariantName, PlaceholderText = "Name der Variante", Width = 220 };
        variantName.TextChanged += (_, _) => _wdVariantName = variantName.Text ?? "";
        var variants = new StackPanel { Name = "web-varianten", Spacing = 4, Children = { new TextBlock { Text = "Varianten", FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Text }, new WrapPanel { Children = { variantName, new Border { Width = 8 }, Act("web-var-new", "Variante anlegen", () => WebDesignVariantCreate(variantName.Text ?? "")), new Border { Width = 8 }, Act("web-var-shots", "Nebeneinander ansehen", () => WebDesignVariantPreviews()) } } } };
        var cards = new WrapPanel(); var vi = 0;
        foreach (var v in WebDesignStates.Variants(WebDesignDir))
        {
            var id = v.Id; var n = vi++;
            var card = new StackPanel { Spacing = 4, Width = 230, Children = { P((id == _wdVariant ? "• " : "") + v.Name, Text, 12.5) } };
            if (_wdVariantImages.TryGetValue(id, out var png) && File.Exists(png)) card.Children.Add(WebThumb(png, 214));
            card.Children.Add(new WrapPanel { Children = { Act($"web-var-open-{n}", "Öffnen", () => WebDesignVariantOpen(id)), new Border { Width = 6 }, Act($"web-var-adopt-{n}", "Übernehmen", () => WebDesignVariantAdopt(id)) } });
            cards.Children.Add(new Border { Margin = new Thickness(0, 4, 10, 4), Padding = new Thickness(8), CornerRadius = new CornerRadius(10), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Child = card });
        }
        variants.Children.Add(cards);
        sp.Children.Add(variants);
        return new Border { Name = "web-ki-card", Padding = new Thickness(14), CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = claudeReady ? ClaudeAccent : EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Child = sp };
    }
}
