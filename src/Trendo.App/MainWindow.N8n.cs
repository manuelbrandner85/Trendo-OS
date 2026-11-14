using System.Diagnostics;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Automation;

namespace Trendo.App;

/// <summary>n8n und Node-RED in „KI & Automatik → Abläufe“ als Profi-Ebene (Freigabe 06.11.2026, N1–N7). Aufbau wie ein Agenten-Arbeitsplatz:
/// links Status (Maschinen, lokale KIs, Ereignisse), Mitte der Ablauf als Fluss aus Bausteinen, rechts Vorlagen und Vorschläge von Claude.
/// n8n wird nicht mitgeliefert – nur auf Wunsch aus der offiziellen npm-Quelle geladen und nur auf 127.0.0.1 betrieben.</summary>
public partial class MainWindow
{
    public bool WfPro { get; set; }
    string _n8nMsg = "", _n8nAsk = "";
    Process? _n8nProc, _noderedProc;
    Trendo.Api.LocalApi? _autoApi;
    JsonObject? _n8nPreview; string _n8nPreviewName = "";
    List<(string Id, string Name, bool Active)> _n8nList = [];
    List<string> _kiUp = [];
    DispatcherTimer? _autoTimer; string _fristSentDay = "";
    /// <summary>Für Tests: statt npm (Paket, Zielordner) → erfolgreich?</summary>
    public Func<string, string, Task<bool>>? AutomationInstallOverride { get; set; }
    /// <summary>Für Tests: statt HTTP an n8n/Node-RED (Methode, Adresse, Inhalt) → Antwort-Text (null = Fehler).</summary>
    public Func<string, string, string?, Task<string?>>? AutomationHttpOverride { get; set; }
    public string N8nDir => Path.Combine(_deps.ToolsRoot, "n8n");
    public string NodeRedDir => Path.Combine(_deps.ToolsRoot, "node-red");
    string AutoData(string name) { var d = Path.Combine(_core.DataRoot, "automation", name); Directory.CreateDirectory(d); return d; }
    public bool N8nInstalled => File.Exists(N8n.Exe(N8nDir));
    public bool NodeRedInstalled => File.Exists(N8n.Exe(NodeRedDir, "node-red"));
    public bool N8nRunning => _n8nProc is { HasExited: false } || (AutomationHttpOverride is not null && _n8nFakeRunning);
    bool _n8nFakeRunning;
    N8n.Hooks Hooks => new(_core.DataRoot);
    string AutoToken => new Trendo.Api.ApiTokens(_core.Secrets).GetOrCreate(Role.User);
    static readonly HttpClient AutoHttp = new() { Timeout = TimeSpan.FromSeconds(20) };

    async Task<string?> AutoSend(string method, string url, string? body, string? apiKey = null)
    {
        if (AutomationHttpOverride is { } fake) return await fake(method, url, body);
        if (_opt.Isolated) return null;
        try
        {
            using var req = new HttpRequestMessage(new HttpMethod(method), url);
            if (body is not null) req.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            if (apiKey is not null) req.Headers.TryAddWithoutValidation("X-N8N-API-KEY", apiKey);
            using var r = await AutoHttp.SendAsync(req);
            return r.IsSuccessStatusCode ? await r.Content.ReadAsStringAsync() : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { return null; }
    }

    // ───────────── N1/N7: einrichten, starten, stoppen ─────────────
    /// <summary>n8n bzw. Node-RED aus der offiziellen npm-Quelle laden – nur nach ausdrücklichem Ja (Knopf in der Bestätigung).</summary>
    public async Task AutomationInstall(string which)
    {
        var (pkg, dir) = which == "node-red" ? ("node-red", NodeRedDir) : ("n8n", N8nDir);
        _n8nAsk = ""; _n8nMsg = $"{pkg} wird aus der offiziellen npm-Quelle geladen … (einige Minuten)"; Show("ablaeufe");
        bool ok;
        if (AutomationInstallOverride is { } fake) ok = await fake(pkg, dir);
        else if (_opt.Isolated) ok = false;
        else
        {
            var npm = Cli.Find("npm") ?? Cli.Find("npm.cmd");
            if (npm is null) { _n8nMsg = "⚠ Node.js mit npm fehlt – zuerst unter Programme „Node.js“ einrichten."; Show("ablaeufe"); return; }
            Directory.CreateDirectory(dir);
            var r = await Task.Run(() => Cli.Run(npm, N8n.InstallArgs(dir, pkg), dir, 1_800_000));
            ok = r.Ok && File.Exists(N8n.Exe(dir, pkg == "node-red" ? "node-red" : "n8n"));
            if (!ok) { _n8nMsg = "⚠ Einrichten ging nicht: " + Short(r.Err.Length > 0 ? r.Err : r.Out); Show("ablaeufe"); return; }
        }
        _core.Audit.Append(Principal.LocalOwner.Name, "automatik.eingerichtet", pkg + (ok ? "" : " (fehlgeschlagen)"));
        _n8nMsg = ok ? $"✓ {pkg} ist eingerichtet – „Starten“." : $"⚠ {pkg} konnte nicht eingerichtet werden."; Show("ablaeufe");
    }

    /// <summary>Lokale Trendo-Schnittstelle für die Automatik (Trendo KI und Aktionen) auf 127.0.0.1:47800.</summary>
    async Task EnsureAutoApi()
    {
        if (_autoApi is not null || _opt.Isolated) return;
        try
        {
            _autoApi = await Trendo.Api.LocalApi.StartAsync(_core, new Trendo.Api.ApiTokens(_core.Secrets), "", _probe, N8n.TrendoApiPort, new Trendo.Api.AutomationHost
            {
                Notify = t => _ui.Post(() => Toast("" + t)),
                ListWorkflows = () => [.. Workflows.All().Select(w => (object)new { id = w.Id, name = w.Name, enabled = w.Enabled, approved = w.IsApproved })],
                RunWorkflow = async (id, dry) =>
                {
                    var w = Workflows.Get(id) ?? throw new ArgumentException("Diesen Trendo-Ablauf gibt es nicht.");
                    if (!dry && !w.IsApproved) throw new InvalidOperationException("Der Ablauf ist noch nicht freigegeben – in Trendo „Freigeben …“.");
                    var run = await Workflows.RunAsync(w, dry); await RaiseEvent("ablauf-fertig", $"{w.Name}: {(run.Ok ? "ok" : "mit Fehler")}");
                    return new { ok = run.Ok, lines = run.Lines };
                },
                SaveLetterDraft = (to, subject, body) =>
                {
                    var dir = Path.Combine(ProjectsRoot, "Briefe", "Entwürfe"); Directory.CreateDirectory(dir);
                    var d = new LetterDraft { Recipient = to, Subject = subject, Body = body };
                    var f = Path.Combine(dir, $"{DateTime.Now:yyyy-MM-dd-HHmmss}-{DevStudio.SafeName(subject.Length > 0 ? subject : "Entwurf")}.json");
                    File.WriteAllText(f, System.Text.Json.JsonSerializer.Serialize(d)); return f;
                },
            });
        }
        catch (Exception e) when (e is IOException or InvalidOperationException) { _n8nMsg = "⚠ Trendo-Schnittstelle konnte nicht starten: " + e.Message; }
    }

    public async Task AutomationStart(string which)
    {
        await EnsureAutoApi();
        if (AutomationHttpOverride is not null && which == "n8n") { _n8nFakeRunning = true; StartAutoTimer(); _n8nMsg = "✓ n8n läuft."; Show("ablaeufe"); return; }
        var node = which == "node-red";
        var exe = N8n.Exe(node ? NodeRedDir : N8nDir, node ? "node-red" : "n8n");
        if (!File.Exists(exe)) { _n8nMsg = "Erst einrichten."; Show("ablaeufe"); return; }
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = node ? NodeRedDir : N8nDir };
        if (node)
        {
            var data = AutoData("node-red-daten"); File.WriteAllText(Path.Combine(data, "settings.js"), N8n.NodeRedSettings);
            foreach (var a in N8n.NodeRedArgs(data)) psi.ArgumentList.Add(a);
            _noderedProc = Process.Start(psi);
        }
        else
        {
            psi.ArgumentList.Add("start");
            foreach (var (k, v) in N8n.StartEnv(AutoData("n8n-daten"))) psi.Environment[k] = v;
            _n8nProc = Process.Start(psi);
        }
        _core.Audit.Append(Principal.LocalOwner.Name, "automatik.start", which);
        StartAutoTimer();
        _n8nMsg = $"{(node ? "Node-RED" : "n8n")} startet … gleich „Editor öffnen“."; Show("ablaeufe");
    }

    public void AutomationStop(string which)
    {
        var p = which == "node-red" ? _noderedProc : _n8nProc; // nur die eigenen Prozesse
        try { if (p is { HasExited: false }) p.Kill(true); } catch (InvalidOperationException) { }
        if (which == "node-red") _noderedProc = null; else { _n8nProc = null; _n8nFakeRunning = false; }
        _core.Audit.Append(Principal.LocalOwner.Name, "automatik.stopp", which);
        _n8nMsg = "Gestoppt."; Show("ablaeufe");
    }

    // ───────────── N3: Ereignisse ─────────────
    /// <summary>Ereignis an alle Webhooks auf diesem PC melden (n8n/Node-RED).</summary>
    public async Task<int> RaiseEvent(string ev, string text)
    {
        var n = 0;
        foreach (var url in Hooks.For(ev))
            if (await AutoSend("POST", url, new JsonObject { ["event"] = ev, ["text"] = text, ["at"] = DateTime.Now.ToString("s") }.ToJsonString()) is not null) n++;
        if (n > 0) _core.Audit.Append(Principal.LocalOwner.Name, "automatik.ereignis", $"{ev} → {n}");
        return n;
    }
    void StartAutoTimer()
    {
        if (_autoTimer is not null || _opt.Isolated) return;
        _autoTimer = new DispatcherTimer(TimeSpan.FromMinutes(30), DispatcherPriority.Background, async (_, _) => await AutoCheckDeadlines());
        OwnTimer(_autoTimer); _autoTimer.Start();
    }
    /// <summary>Einmal am Tag: Fristen und Wiedervorlagen der nächsten 3 Tage als Ereignis „frist-bald“.</summary>
    public async Task AutoCheckDeadlines()
    {
        var day = DateTime.Today.ToString("yyyy-MM-dd"); if (_fristSentDay == day) return; _fristSentDay = day;
        foreach (var a in LetterOffice.Agenda(Cases.List(Principal.LocalOwner), DateTime.Today, 3).Where(a => a.Soon))
            await RaiseEvent("frist-bald", $"{a.Date:dd.MM.} {a.Kind} – {a.CaseTitle}: {a.Text}");
    }

    // ───────────── N2/N4/N5/N6: n8n-Schnittstelle ─────────────
    string? N8nKey => _core.Secrets.Get("n8n-api-schluessel");
    public void N8nSetKey(string key) { if (string.IsNullOrWhiteSpace(key)) _core.Secrets.Delete("n8n-api-schluessel"); else _core.Secrets.Set("n8n-api-schluessel", key.Trim()); }

    /// <summary>N2: Zugang „Trendo KI“ in n8n anlegen (braucht den n8n-API-Schlüssel).</summary>
    public async Task N8nAddKiCredential()
    {
        if (N8nKey is not { } key) { _n8nMsg = "Für das automatische Eintragen braucht Trendo deinen n8n-API-Schlüssel (n8n → Einstellungen → n8n API → Schlüssel erstellen)."; Show("ablaeufe"); return; }
        var res = await AutoSend("POST", N8n.EditorUrl + "api/v1/credentials", N8n.KiCredential(AutoToken).ToJsonString(), key);
        _n8nMsg = res is null ? "⚠ n8n hat den Zugang nicht angenommen (läuft n8n? Schlüssel richtig?)." : "✓ Zugang „Trendo KI (lokal)“ ist in n8n eingetragen – in jedem KI-Baustein auswählbar.";
        if (res is not null) _core.Audit.Append(Principal.LocalOwner.Name, "automatik.trendo-ki", "Zugang in n8n");
        Show("ablaeufe");
    }

    /// <summary>Ablauf zur Vorschau öffnen (Vorlage, Umwandlung, Claude-Datei) – importiert wird erst nach „Ja“.</summary>
    public void N8nPreview(JsonObject wf, string from) { _n8nPreview = wf; _n8nPreviewName = from; _n8nMsg = ""; Show("ablaeufe"); }

    /// <summary>N4/N5/N6: Ablauf in n8n anlegen (über die n8n-Schnittstelle) oder als Datei zum Importieren speichern.</summary>
    public async Task N8nImport()
    {
        if (_n8nPreview is not { } wf) return;
        var clean = (JsonObject)wf.DeepClone(); clean.Remove("active"); clean.Remove("id");
        var file = Path.Combine(WorkFolder("Abläufe"), DevStudio.SafeName(clean["name"]?.GetValue<string>() ?? "ablauf") + ".n8n.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, clean.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        string? res = N8nKey is { } key ? await AutoSend("POST", N8n.EditorUrl + "api/v1/workflows", clean.ToJsonString(), key) : null;
        _core.Audit.Append(Principal.LocalOwner.Name, "automatik.import", $"{clean["name"]} ({_n8nPreviewName}){(res is null ? " als Datei" : "")}");
        _n8nMsg = res is not null ? $"✓ „{clean["name"]}“ ist in n8n angelegt (noch ausgeschaltet – in n8n prüfen und einschalten)." : $"Gespeichert als {Path.GetFileName(file)} – in n8n über „…“ → „Import from File“ öffnen (für den direkten Weg den n8n-API-Schlüssel eintragen).";
        _n8nPreview = null; await N8nRefresh();
    }

    public async Task N8nRefresh()
    {
        _kiUp = [];
        foreach (var b in N8n.Backends) if (await AutoSend("GET", b.BaseUrl + "/models", null) is not null) _kiUp.Add(b.Id);
        _n8nList = [];
        if (N8nKey is { } key && await AutoSend("GET", N8n.EditorUrl + "api/v1/workflows", null, key) is { } json && JsonNode.Parse(json)?["data"] is JsonArray arr)
            _n8nList = [.. arr.OfType<JsonObject>().Select(o => (o["id"]?.ToString() ?? "", o["name"]?.ToString() ?? "", o["active"]?.GetValue<bool>() == true))];
        Show("ablaeufe");
    }

    /// <summary>N5: Abläufe, die Claude als Datei („*.n8n.json“) im Projektordner abgelegt hat.</summary>
    public List<string> N8nClaudeFiles()
    {
        var roots = new[] { ClaudeChatRoot(ModuleOf("ablaeufe") ?? "CORE-02"), WorkFolder("Abläufe") };
        return [.. roots.Where(Directory.Exists).SelectMany(r => Directory.EnumerateFiles(r, "*.n8n.json", SearchOption.AllDirectories)).Distinct().OrderByDescending(File.GetLastWriteTime).Take(20)];
    }

    // ───────────── Oberfläche: Agenten-Arbeitsplatz ─────────────
    private Control N8nWorkspace()
    {
        Button B(string name, string label, Action go, bool accent = false) { var b = Act(name, label, go, accent); b.Margin = new Thickness(0, 0, 4, 4); return b; }
        // Fach-Profi-Look (Agenten-Arbeitsplatz): dunkle Paneele im DAW-Ton, nur im dunklen Trendo-Stil (PaneBg/PaneEdge)
        Border Pane(Control c) => new() { Child = c, Background = PaneBg, BorderBrush = PaneEdge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 10), Margin = new Thickness(0, 0, 0, 8), BoxShadow = CardShadow };
        Control Dot(bool on) => new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(5), Background = on ? Brush.Parse("#2FBF71") : Brush.Parse("#6B7280"), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        Control Card(string title, bool on, string sub, params Control[] body)
        {
            var sp = new StackPanel { Spacing = 4, Children = { new StackPanel { Orientation = Orientation.Horizontal, Children = { Dot(on), new TextBlock { Text = title, Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 13 } } }, P(sub, Muted, 11) } };
            foreach (var x in body) sp.Children.Add(x);
            return Pane(sp);
        }
        // links: Status
        var left = new StackPanel { Name = "n8n-status", Spacing = 8 };
        left.Children.Add(Card("n8n", N8nRunning, N8nInstalled ? (N8nRunning ? $"läuft auf 127.0.0.1:{N8n.Port}" : "eingerichtet, gestoppt") : "nicht eingerichtet (wird nicht mitgeliefert)",
            new WrapPanel { Children = {
                N8nInstalled ? (N8nRunning ? B("n8n-stop", "Stoppen", () => AutomationStop("n8n")) : B("n8n-start", "Starten", () => _ = AutomationStart("n8n"), true)) : B("n8n-install", "Einrichten …", () => { _n8nAsk = "n8n"; Show("ablaeufe"); }, true),
                B("n8n-editor", "Editor öffnen", () => Launch(N8n.EditorUrl)) } }));
        left.Children.Add(Card("Node-RED", _noderedProc is { HasExited: false }, NodeRedInstalled ? "frei (Apache-2.0) – darf mitgeliefert werden" : "freie Alternative, nicht eingerichtet",
            new WrapPanel { Children = {
                NodeRedInstalled ? (_noderedProc is { HasExited: false } ? B("nodered-stop", "Stoppen", () => AutomationStop("node-red")) : B("nodered-start", "Starten", () => _ = AutomationStart("node-red"))) : B("nodered-install", "Einrichten …", () => { _n8nAsk = "node-red"; Show("ablaeufe"); }),
                B("nodered-editor", "Editor", () => Launch($"http://127.0.0.1:{N8n.NodeRedPort}/")) } }));
        var ki = new StackPanel { Spacing = 2 };
        foreach (var b in N8n.Backends) ki.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { Dot(_kiUp.Contains(b.Id)), new TextBlock { Text = b.Label, Foreground = Text, FontSize = 12 } } });
        var key = new TextBox { Name = "n8n-key", PasswordChar = '•', PlaceholderText = N8nKey is null ? "n8n-API-Schlüssel (optional)" : "Schlüssel gespeichert", Width = 200 };
        left.Children.Add(Card("Trendo KI", _kiUp.Count > 0, $"Eine Adresse für alle lokalen KIs: {N8n.TrendoKiBase}", ki,
            new WrapPanel { Children = { B("n8n-ki-check", "Prüfen", () => _ = N8nRefresh()), B("n8n-ki-cred", "In n8n eintragen", () => _ = N8nAddKiCredential()) } },
            new WrapPanel { Children = { key, B("n8n-key-save", "Speichern", () => { N8nSetKey(key.Text ?? ""); _n8nMsg = "Schlüssel geschützt gespeichert."; Show("ablaeufe"); }) } }));
        var evs = new StackPanel { Spacing = 2 };
        foreach (var e in N8n.Events) evs.Children.Add(P($"• {e.Label}: {Hooks.For(e.Id).Count()} Abo(s)", Text, 11.5));
        left.Children.Add(Card("Ereignisse", Hooks.List().Count > 0, "Trendo meldet sie an Webhooks auf diesem PC", evs, B("n8n-event-test", "Probe-Ereignis senden", async () => { var n = await RaiseEvent("test", "Probe aus Trendo"); _n8nMsg = n == 0 ? "Kein Webhook abonniert." : $"An {n} Webhook(s) gesendet."; Show("ablaeufe"); })));

        // Mitte: Bestätigung, Meldung, Vorschau als Fluss
        var center = new StackPanel { Name = "n8n-center", Spacing = 8 };
        if (_n8nAsk.Length > 0)
        {
            var pkg = _n8nAsk == "node-red" ? "Node-RED" : "n8n";
            center.Children.Add(Pane(new StackPanel { Name = "n8n-ask", Spacing = 6, Children = {
                new TextBlock { Text = $"{pkg} einrichten?", Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold },
                P(pkg == "n8n" ? "Lädt n8n aus der offiziellen npm-Quelle (npmjs.com) in den Trendo-Werkzeugordner – ohne Admin-Rechte, nur für diesen PC (127.0.0.1). Lizenz: Sustainable Use License – für private und interne Nutzung kostenlos; Trendo liefert n8n deshalb nicht mit. Braucht Node.js und einige hundert MB."
                    : "Lädt Node-RED (Apache-2.0, frei) aus der offiziellen npm-Quelle in den Trendo-Werkzeugordner – nur für diesen PC (127.0.0.1).", null, 12),
                new WrapPanel { Children = { B("n8n-ask-yes", "Ja, einrichten", () => _ = AutomationInstall(_n8nAsk), true), B("n8n-ask-no", "Nein", () => { _n8nAsk = ""; Show("ablaeufe"); }) } } } }));
        }
        if (_n8nMsg.Length > 0) center.Children.Add(new TextBlock { Name = "n8n-msg", Text = _n8nMsg, Foreground = _n8nMsg.StartsWith('⚠') ? Brush.Parse("#F5A524") : Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
        if (_n8nPreview is { } pv)
        {
            var rv = N8n.Check(pv.ToJsonString());
            var flow = new WrapPanel { Name = "n8n-flow" };
            for (var i = 0; i < rv.Nodes.Count; i++)
            {
                var (nm, tp) = rv.Nodes[i]; var ext = rv.External.Any(x => x.StartsWith($"„{nm}“"));
                flow.Children.Add(new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 6), Margin = new Thickness(0, 0, 0, 6), BorderThickness = new Thickness(1.5), BorderBrush = ext ? Brush.Parse("#F5A524") : PaneEdge, Background = GroupBg, BoxShadow = CardShadow,
                    Child = new StackPanel { Children = { new TextBlock { Text = nm, Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 12.5 }, new TextBlock { Text = tp.Split('.').Last(), Foreground = Muted, FontSize = 10.5 } } } });
                if (i < rv.Nodes.Count - 1) flow.Children.Add(new TextBlock { Text = "  →  ", Foreground = GoldText(), FontSize = 16, VerticalAlignment = VerticalAlignment.Center });
            }
            var box = new StackPanel { Name = "n8n-preview", Spacing = 6, Children = { new TextBlock { Text = $"Vorschau: {rv.Name}", Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold }, P("Aus: " + _n8nPreviewName, Muted, 11), flow } };
            foreach (var x in rv.External) box.Children.Add(P("⚠ Wirkt nach außen: " + x, Brush.Parse("#F5A524"), 11.5));
            foreach (var x in rv.Problems) box.Children.Add(P("⚠ " + x, Brush.Parse("#F87171"), 11.5));
            box.Children.Add(new WrapPanel { Children = { B("n8n-import", rv.External.Count > 0 ? "Ja, anlegen (wirkt nach außen)" : "Ja, in n8n anlegen", () => _ = N8nImport(), true), B("n8n-import-no", "Nein", () => { _n8nPreview = null; Show("ablaeufe"); }) } });
            center.Children.Add(Pane(box));
        }
        if (_n8nList.Count > 0)
        {
            var list = new StackPanel { Name = "n8n-list", Spacing = 2, Children = { new TextBlock { Text = "Abläufe in n8n", Foreground = Text, FontWeight = FontWeight.SemiBold } } };
            foreach (var (id, name, active) in _n8nList) list.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { Dot(active), new TextBlock { Text = name, Foreground = Text, FontSize = 12 } } });
            center.Children.Add(Pane(list));
        }
        if (_n8nPreview is null && _n8nAsk.Length == 0)
            center.Children.Add(P("Profi-Ebene: n8n für große Abläufe mit hunderten Bausteinen, eingebunden in Trendo. Vorlage oder Vorschlag rechts wählen – Trendo zeigt den Ablauf als Fluss, und erst dein „Ja“ legt ihn an. Verschickt wird nur, was du in n8n selbst einschaltest.", Muted, 12));

        // rechts: Vorlagen, Trendo-Abläufe übernehmen, Claude
        var right = new StackPanel { Name = "n8n-right", Spacing = 6 };
        right.Children.Add(new TextBlock { Text = "Vorlagen je Studio", Foreground = Text, FontWeight = FontWeight.SemiBold });
        foreach (var t in N8n.Templates)
        {
            var tt = t; var b = B("n8n-tpl-" + t.Id, $"{t.Studio}: {t.Name}" + (t.External ? " ↗" : ""), () => N8nPreview(tt.Build(AutoToken), "Vorlage"));
            b.Content = new TextBlock { Text = (string)b.Content!, TextWrapping = TextWrapping.Wrap }; ToolTip.SetTip(b, t.Hint); b.HorizontalAlignment = HorizontalAlignment.Stretch; b.HorizontalContentAlignment = HorizontalAlignment.Left; right.Children.Add(b);
        }
        right.Children.Add(new TextBlock { Text = "Trendo-Abläufe nach n8n", Foreground = Text, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
        var k = 0;
        foreach (var w in Workflows.All()) { var ww = w; right.Children.Add(B($"n8n-from-{k++}", "↦ " + w.Name, () => N8nPreview(N8n.FromTrendo(ww, AutoToken), "Trendo-Ablauf"))); }
        right.Children.Add(new TextBlock { Text = "Von Claude gebaut", Foreground = Text, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
        var files = N8nClaudeFiles(); var ci = 0;
        foreach (var f in files) { var ff = f; right.Children.Add(B($"n8n-claude-{ci++}", "✳ " + Path.GetFileName(f), () => { try { N8nPreview(JsonNode.Parse(File.ReadAllText(ff).Replace("TRENDO_SCHLUESSEL", AutoToken)) as JsonObject ?? [], "Claude: " + Path.GetFileName(ff)); } catch (System.Text.Json.JsonException e) { _n8nMsg = "⚠ Claudes Datei ist kein gültiger Ablauf: " + e.Message; Show("ablaeufe"); } })); }
        if (files.Count == 0) right.Children.Add(P("Sag Claude, welchen Ablauf du brauchst – er legt ihn als „….n8n.json“ ab und er erscheint hier zur Prüfung.", Muted, 11));

        var grid = new Grid { Name = "n8n-workspace", ColumnDefinitions = new ColumnDefinitions("260,10,*,10,290") };
        grid.Children.Add(new ScrollViewer { Content = left }); grid.Children.Add(Pos(new ScrollViewer { Content = center }, 2)); grid.Children.Add(Pos(new ScrollViewer { Content = right }, 4));
        // Anzeigefeld oben wie bei Agenten-Werkstätten: Zustand auf einen Blick (LCD-Stil)
        TextBlock Dig(string name, string t) => new() { Name = name, Text = t, Foreground = DawLook ? DawDigits : Text, FontFamily = new FontFamily("Consolas, monospace"), FontWeight = FontWeight.SemiBold, FontSize = 13, Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center };
        var lcd = new Border { Name = "n8n-lcd", Background = DawLook ? DawLcd : TileBg, BorderBrush = PaneEdge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6), Margin = new Thickness(0, 0, 16, 10),
            Child = new WrapPanel { Children = {
                Dig("n8n-lcd-n8n", "N8N " + (N8nRunning ? "LÄUFT" : N8nInstalled ? "AUS" : "– FEHLT")),
                Dig("n8n-lcd-red", "NODE-RED " + (_noderedProc is { HasExited: false } ? "LÄUFT" : NodeRedInstalled ? "AUS" : "– FEHLT")),
                Dig("n8n-lcd-ki", $"KI {_kiUp.Count}/{N8n.Backends.Length}"),
                Dig("n8n-lcd-ev", $"EREIGNISSE {Hooks.List().Count}"),
                Dig("n8n-lcd-wf", $"ABLÄUFE {_n8nList.Count}") } } };
        return new DockPanel { Children = { Dk(lcd, Avalonia.Controls.Dock.Top), grid } };
    }
}
