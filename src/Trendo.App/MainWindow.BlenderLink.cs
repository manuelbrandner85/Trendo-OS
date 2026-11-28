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
        return link;
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
        again.Click += async (_, _) =>
        {
            if (BlenderLinkGet() is not { } l) { BlenderStatusRefresh(new(BlenderLinkState.Getrennt, BlenderMissingText, BlenderBridge.DefaultPort, null, false)); return; }
            if (!CanManageSoftware) return;
            using var keep = l.Keep();
            await l.ReconnectAsync();
        };
        return new Border
        {
            Name = name + "-feld", CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 5), BorderThickness = new Thickness(1),
            BorderBrush = Brush.Parse("#55C9A227"), Background = Brush.Parse("#14C9A227"), HorizontalAlignment = HorizontalAlignment.Left,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { text, again } },
        };
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
        var before = link.Status.Usable;
        var st = await BlenderEnsure(dir);
        if (!st.Usable) return st.Text;
        if (!before && link.Starts > 0 && st.Own && link.ProjectDir == dir && BlenderLink.ProjectArgs(dir).Args is [var opened] && Path.GetFullPath(opened) == Path.GetFullPath(blend)) return null; // gerade mit genau dieser Datei gestartet
        try { return await link.OpenFileAsync(blend); }
        catch (BlenderLinkException e) { return e.Message; }
    }

    sealed class BlenderRunKeep(IDisposable keep, Action undo) : IDisposable
    {
        public void Dispose() { undo(); keep.Dispose(); }
    }

    /// <summary>Vor einem Claude-Lauf mit Blender-MCP: Statusfeld in die Live-Antwort, Blender verbinden oder sichtbar starten, und während des Laufs jeden
    /// Abbruch und jedes Neuverbinden als Schritt zeigen (nie still). Rückgabe hält die Verbindung bis zum Ende des Laufs.</summary>
    async Task<IDisposable?> ClaudeBlenderReady(string folder, StackPanel live, List<ClaudeChats.Step> steps, StackPanel stepsPanel, CancellationToken ct)
    {
        void Step(string t, bool err = false) { var s = new ClaudeChats.Step(t, false, err); steps.Add(s); stepsPanel.Children.Add(TimelineRow(s, steps[0].At, true)); }
        var link = BlenderLinkGet();
        live.Children.Insert(0, BlenderStatusField("claude-blender-status"));
        if (link is null) { Step("Blender: " + BlenderMissingText, true); return null; }
        var keep = link.Keep();
        void OnLog(string kind, string text) { if (kind is "abbruch" or "neustart" or "wieder-verbunden" or "getrennt" or "fehler") _ui.Post(() => Step("Blender: " + text, kind is "abbruch" or "getrennt" or "fehler")); }
        link.Logged += OnLog;
        try
        {
            var st = await BlenderEnsure(folder, ct);
            Step("Blender: " + st.Text, !st.Usable);
        }
        catch (OperationCanceledException) { link.Logged -= OnLog; keep.Dispose(); throw; }
        return new BlenderRunKeep(keep, () => link.Logged -= OnLog);
    }
}
