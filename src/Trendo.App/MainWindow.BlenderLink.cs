using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0785: Blender startet von selbst, wenn Trendo es braucht (Claude/KI mit Blender-MCP, „In Blender öffnen“, Programmstart), sichtbar mit Projekt
/// und MCP-Brücke, und die Verbindung wird gehalten (<see cref="BlenderLink"/>). Ein Statusfeld zeigt: verbunden, verbindet neu, getrennt mit Grund.
/// Jeder Abbruch und jedes Neuverbinden steht im Protokoll („blender.link.*“).</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: eigene Verbindung (Ersatz-MCP-Server, nachgebildeter Prozess) statt echtem Blender.</summary>
    public static Func<BlenderLink?>? BlenderLinkOverride { get; set; }

    BlenderLink? _blenderLink;
    IDisposable? _blenderStudioKeep;
    readonly List<WeakReference<TextBlock>> _blenderStatusFields = [];

    /// <summary>Die eine Blender-Verbindung dieser Trendo-Sitzung (null = Blender ist auf diesem PC nicht installiert oder Testmodus).</summary>
    public BlenderLink? BlenderLinkGet()
    {
        if (_blenderLink is not null) return _blenderLink;
        BlenderLink? link;
        if (BlenderLinkOverride is { } o) link = o();
        else if (_opt.Isolated) return null;
        else
        {
            var exe = EngineDetect().FirstOrDefault(e => e.Id == "blender" && e.Installed && e.Exe is not null)?.Exe;
            if (exe is null || !File.Exists(exe)) return null;
            link = new BlenderLink(exe);
        }
        if (link is null) return null;
        _blenderLink = link;
        link.Changed += st => _ui.Post(() => BlenderStatusRefresh(st));
        link.Logged += (kind, text) => _core.Audit.Append(Principal.LocalOwner.Name, "blender.link." + kind, text); // Protokoll: jeder Abbruch, jedes Neuverbinden
        link.AddonConsent = BlenderAddonConsentStored == true; // M2: gemerkte Zustimmung
        link.AskAddonConsent = () => _ui.InvokeAsync(async () =>
        {
            if (BlenderAddonConsentStored == false && !_blenderAskAgain) return false; // N10: gemerktes „Nein“ – erst „Neu verbinden“ fragt wieder
            _blenderAskAgain = false;
            var ok = await BlenderAsk("addon", "MCP-Add-on in Blender einschalten?",
                "Damit Claude in Blender arbeiten kann, braucht Blender das Add-on „MCP“ (Blender Lab). Es ist installiert, aber ausgeschaltet.\n\n" +
                BlenderAddonPermanentText, "Einschalten", "Nicht einschalten");
            BlenderAddonConsentStored = ok;
            _core.Audit.Append(Principal.LocalOwner.Name, "blender.link.addon-frage", ok ? "ja" : "nein");
            return ok;
        });
        link.AskRestart = () => _ui.InvokeAsync(() => BlenderAsk("neustart", "Blender schon wieder beendet",
            "Blender wurde kurz hintereinander zweimal beendet – vielleicht hast du es selbst beendet (z. B. im Task-Manager).\n\nSoll Trendo es noch einmal starten?", "Neu starten", "Nicht starten"));
        Closed += (_, _) => { _blenderStudioKeep?.Dispose(); link.Dispose(); }; // H2: beim Beenden von Trendo freigeben – kein Neustart beim Herunterfahren
        return link;
    }

    bool _blenderAskAgain;

    public const string BlenderAddonPermanentText = "Trendo schaltet es jetzt ein. Hinweis: Speichert dein Blender seine Einstellungen automatisch, bleibt das Add-on danach dauerhaft an.";

    /// <summary>Für Tests: Antworten auf Trendos Blender-Fragen („addon“, „projekt“) statt Dialog.</summary>
    public static Func<string, bool>? BlenderAskOverride { get; set; }

    /// <summary>M2/N10: Antwort zum Einschalten des MCP-Add-ons, gemerkt im Datenordner (null = noch nicht gefragt).</summary>
    public bool? BlenderAddonConsentStored
    {
        get { try { var f = Path.Combine(_core.DataRoot, "blender-link.json"); return File.Exists(f) && System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(f))?["addonOk"] is { } v ? v.GetValue<bool>() : null; } catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidOperationException or UnauthorizedAccessException) { return null; } }
        set { try { AtomicFile.WriteAllText(Path.Combine(_core.DataRoot, "blender-link.json"), new System.Text.Json.Nodes.JsonObject { ["addonOk"] = value }.ToJsonString()); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
    }

    /// <summary>Sichtbare Ja/Nein-Frage (erste Antwort = Ja).</summary>
    async Task<bool> BlenderAsk(string key, string title, string text, string yes, string no)
    {
        if (BlenderAskOverride is { } o) return o(key);
        if (_opt.Isolated) return false;
        var dlg = new Window { Title = title, Width = 480, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var y = new Button { Name = "blender-ask-ja", Content = yes, Background = Brush.Parse("#C9A227"), Foreground = Brushes.Black };
        var n = new Button { Name = "blender-ask-nein", Content = no, IsCancel = true };
        y.Click += (_, _) => dlg.Close(true); n.Click += (_, _) => dlg.Close(false);
        dlg.Content = new StackPanel { Margin = new Thickness(16), Spacing = 10, Children = {
            new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
            new TextBlock { Name = "blender-ask-text", Text = text, TextWrapping = TextWrapping.Wrap },
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { n, y } } } };
        return await dlg.ShowDialog<bool>(this);
    }

    /// <summary>Blender verbinden oder selbst starten (sichtbar, mit Projekt). Ehrlich: Fehlt Blender, kommt eine klare Meldung – nichts wird geladen.</summary>
    public async Task<BlenderLinkStatus> BlenderEnsure(string projectDir, CancellationToken ct = default)
    {
        var link = BlenderLinkGet();
        if (link is null)
        {
            var st = new BlenderLinkStatus(BlenderLinkState.Getrennt, BlenderMissingText, BlenderBridge.DefaultPort, null, false);
            BlenderStatusRefresh(st);
            return st;
        }
        if (!CanManageSoftware) // T-0764: Blender-Oberfläche nie für das Kinderprofil
        {
            var st = new BlenderLinkStatus(BlenderLinkState.Getrennt, Rbac.NurErwachseneKonsole, link.Port, null, false);
            BlenderStatusRefresh(st);
            return st;
        }
        _core.Audit.Append(Principal.LocalOwner.Name, "blender.link.bedarf", Path.GetFileName(projectDir));
        return await link.EnsureAsync(projectDir, ct);
    }

    public const string BlenderMissingText = "Blender ist auf diesem PC nicht installiert. „Blender einrichten“ im 3D-Studio richtet es ein – Trendo lädt nichts ohne deinen Klick.";

    /// <summary>Statusfeld „Blender: verbunden / verbindet neu / getrennt – Grund“ mit „Neu verbinden“.</summary>
    Control BlenderStatusField(string name = "blender-link-status")
    {
        var st = _blenderLink?.Status ?? new BlenderLinkStatus(BlenderLinkState.Aus, BlenderLinkGet() is null && !_opt.Isolated ? BlenderMissingText : "Blender ist noch nicht verbunden.", BlenderBridge.DefaultPort, null, false);
        var text = new TextBlock { Name = name, TextWrapping = TextWrapping.Wrap, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 520 };
        BlenderStatusApply(text, st);
        lock (_blenderStatusFields) { _blenderStatusFields.RemoveAll(w => !w.TryGetTarget(out _)); _blenderStatusFields.Add(new(text)); }
        var again = new Button { Name = name + "-neu", Content = "Neu verbinden", Padding = new Thickness(10, 3), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(again, "Blender-Verbindung neu aufbauen – startet Blender, falls es nicht offen ist");
        again.Click += async (_, _) => await BlenderReconnectClick();
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { text, again } };
        if (_blenderLink?.OrphanRecoveries() is { Count: > 0 } orphans) // N7: verwaiste Sicherung sichtbar anbieten, nie automatisch öffnen
        {
            var (src, _, at) = orphans[0];
            var open = new Button { Name = name + "-sicherung", Content = $"Sicherung von {Path.GetFileName(src)} öffnen", Padding = new Thickness(10, 3), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(open, $"Nach einem Absturz am {at:dd.MM. HH:mm} gesichert. Öffnet als eigene Datei – dein Original bleibt unverändert.");
            open.Click += async (_, _) => await BlenderOpenOrphan(src);
            row.Children.Add(open);
        }
        return new Border
        {
            Name = name + "-feld", CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 5), BorderThickness = new Thickness(1),
            BorderBrush = Brush.Parse("#55C9A227"), Background = Brush.Parse("#14C9A227"), HorizontalAlignment = HorizontalAlignment.Left,
            Child = row,
        };
    }

    /// <summary>Knopf „Neu verbinden“: Sperren aufheben, ein gemerktes „Nein“ zum Add-on noch einmal fragen (N10), verbinden bzw. starten.</summary>
    internal async Task BlenderReconnectClick()
    {
        if (BlenderLinkGet() is not { } l) { BlenderStatusRefresh(new(BlenderLinkState.Getrennt, BlenderMissingText, BlenderBridge.DefaultPort, null, false)); return; }
        if (!CanManageSoftware) return;
        _blenderAskAgain = true;
        using var keep = l.Keep();
        await l.ReconnectAsync();
    }

    /// <summary>N7: verwaiste Sicherung als eigene Datei anlegen und in Blender zeigen.</summary>
    public async Task<string?> BlenderOpenOrphan(string source)
    {
        if (BlenderLink.OpenOrphan(source) is not { } file) return "Die Sicherung ließ sich nicht öffnen.";
        _core.Audit.Append(Principal.LocalOwner.Name, "blender.link.sicherung-geoeffnet", Path.GetFileName(file));
        return await BlenderShowFile(file);
    }

    static void BlenderStatusApply(TextBlock t, BlenderLinkStatus st)
    {
        var dot = st.State switch
        {
            BlenderLinkState.Verbunden or BlenderLinkState.Arbeitet => "#FF4ADE80",
            BlenderLinkState.Startet or BlenderLinkState.VerbindetNeu => "#FFFACC15",
            BlenderLinkState.Getrennt => "#FFF87171",
            _ => "#FF9CA3AF",
        };
        t.Inlines = [new Avalonia.Controls.Documents.Run("● ") { Foreground = Brush.Parse(dot) }, new Avalonia.Controls.Documents.Run(st.Label + " – " + st.Text)];
        Avalonia.Automation.AutomationProperties.SetName(t, st.Label + " – " + st.Text);
    }

    void BlenderStatusRefresh(BlenderLinkStatus st)
    {
        List<TextBlock> live;
        lock (_blenderStatusFields) { live = [.. _blenderStatusFields.Select(w => w.TryGetTarget(out var t) ? t : null).OfType<TextBlock>()]; }
        foreach (var t in live) BlenderStatusApply(t, st);
    }

    /// <summary>Text des Statusfelds (für Tests und die Ansicht).</summary>
    public string BlenderStatusText => (_blenderLink?.Status ?? new BlenderLinkStatus(BlenderLinkState.Aus, "Blender ist noch nicht verbunden.", BlenderBridge.DefaultPort, null, false)) is var s ? s.Label + " – " + s.Text : "";

    /// <summary>Datei in Blender zeigen: verbundenes Blender weiternutzen (öffnet die Datei dort, wenn nichts Ungespeichertes verloren geht),
    /// sonst Blender mit dem Ordner der Datei als Projekt starten. Liefert null bei Erfolg, sonst den Grund.</summary>
    public async Task<string?> BlenderShowFile(string blend)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(blend))!;
        var link = BlenderLinkGet();
        if (link is null) return BlenderMissingText;
        using var keep = link.Keep();
        var st = await BlenderEnsure(dir);
        if (!st.Usable) return st.Text;
        if (link.OpenFile is { } of && string.Equals(Path.GetFullPath(of), Path.GetFullPath(blend), StringComparison.OrdinalIgnoreCase)) return null; // schon offen
        try { return await link.OpenFileAsync(blend); }
        catch (BlenderLinkException e) { return e.Message; }
    }

    sealed class BlenderRunKeep(IDisposable keep, Action undo) : IDisposable
    {
        public void Dispose() { undo(); keep.Dispose(); }
    }

    /// <summary>Vor einem Claude-Lauf mit Blender-MCP: Statusfeld in die Live-Antwort, Blender verbinden oder sichtbar starten, und während des Laufs jeden
    /// Abbruch und jedes Neuverbinden als Schritt zeigen (nie still). Rückgabe hält die Verbindung bis zum Ende des Laufs.</summary>
    async Task<IDisposable?> ClaudeBlenderReady(string folder, StackPanel live, List<ClaudeChats.Step> steps, StackPanel stepsPanel, CancellationToken ct) =>
        (await ClaudeBlenderPrepare(folder, live, steps, stepsPanel, ct)).Keep;

    /// <summary>Wie <see cref="ClaudeBlenderReady"/>; <c>MaySave</c>: Claude darf die offene Datei speichern (eigene bzw. Projektdatei). Im offenen Blender der
    /// Person mit einer Datei außerhalb des Projekts fragt Trendo: Projekt öffnen (nur ohne Ungespeichertes) oder in der offenen Datei arbeiten – dann ohne Speichern.</summary>
    internal async Task<(IDisposable? Keep, bool MaySave)> ClaudeBlenderPrepare(string folder, StackPanel live, List<ClaudeChats.Step> steps, StackPanel stepsPanel, CancellationToken ct)
    {
        void Step(string t, bool err = false) { var s = new ClaudeChats.Step(t, false, err); steps.Add(s); stepsPanel.Children.Add(TimelineRow(s, steps[0].At, true)); }
        var link = BlenderLinkGet();
        live.Children.Insert(0, BlenderStatusField("claude-blender-status"));
        if (link is null) { Step("Blender: " + BlenderMissingText, true); return (null, false); }
        var keep = link.Keep();
        var quiet = link.Quiet(); // N1: keine Kennungsabfragen, solange Claude über MCP arbeitet
        void OnLog(string kind, string text) { if (kind is "abbruch" or "neustart" or "wieder-verbunden" or "getrennt" or "fehler") _ui.Post(() => Step("Blender: " + text, kind is "abbruch" or "getrennt" or "fehler")); }
        link.Logged += OnLog;
        var maySave = true;
        try
        {
            var st = await BlenderEnsure(folder, ct);
            Step("Blender: " + st.Text, !st.Usable);
            if (st.Usable && !st.Own && !link.OpenFileInProject)
            {
                var name = link.OpenFile is { } f ? Path.GetFileName(f) : "eine ungespeicherte Szene";
                var proj = BlenderLink.ProjectArgs(folder).Args;
                var open = proj.Count > 0 && await BlenderAsk("projekt", "Welche Datei soll Claude bearbeiten?",
                    $"In deinem offenen Blender ist {name} offen – sie gehört nicht zu diesem Projekt.\n\nProjekt öffnen: {Path.GetFileName(proj[0])} (nur, wenn in Blender nichts Ungespeichertes ist).\nIn der offenen Datei arbeiten: Claude ändert sie und soll sie nicht ohne dich speichern – technisch sperren kann Trendo das in deinem Blender nicht.",
                    "Projekt öffnen", "In der offenen Datei arbeiten");
                if (open && await link.OpenFileAsync(proj[0], ct) is { } why) { Step("Blender: " + why, true); maySave = false; }
                else if (!open) { maySave = false; Step($"Blender: Claude arbeitet in deiner offenen Datei ({name}). Claude soll sie nicht speichern – technisch sperren kann Trendo das nicht."); }
                else Step($"Blender: Projekt {Path.GetFileName(proj[0])} geöffnet.");
            }
        }
        catch (OperationCanceledException) { link.Logged -= OnLog; quiet.Dispose(); keep.Dispose(); throw; }
        catch (BlenderLinkException e) { Step("Blender: " + e.Message, true); maySave = false; }
        return (new BlenderRunKeep(keep, () => { link.Logged -= OnLog; quiet.Dispose(); }), maySave);
    }
}
