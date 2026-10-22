using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>X-02: Trendo prüft sich beim Start selbst und repariert sichere Fälle ohne Nachfrage.</summary>
public partial class MainWindow
{
    public IReadOnlyList<HealResult> LastHeal { get; private set; } = [];
    private string _healNote = "";
    public Task? StartupHeal { get; private set; }

    private void InitX02()
    {
        _showMap["heilung"] = HealView; SysTabs.Add(("heilung", "Selbstheilung"));
        StudioPages["X-02"] = "heilung";
        ExtraPages.Add(("Selbstheilung", "heilung", ["reparieren", "kaputt", "fehler", "heilen", "selbst"]));
        var root = _core.DataRoot;
        StartupHeal = Task.Run(() => { var r = new SelfHealer(root, _core.Audit).Run(true, DateTime.Now); Dispatcher.UIThread.Post(() => LastHeal = r); });
    }

    private Control HealView()
    {
        var sp = new StackPanel { Spacing = 6, Children = { P("Bei jedem Start prüft Trendo sich selbst und repariert, was sicher geht – ohne dich zu fragen. Was du entscheiden musst, steht hier.") } };
        var last = SelfHealer.Last(_core.DataRoot);
        var res = LastHeal.Count > 0 ? LastHeal : last?.Results ?? [];
        sp.Children.Add(new TextBlock { Name = "heal-summary", Foreground = Text, FontSize = 14, Text = _healNote.Length > 0 ? _healNote : res.Count == 0 ? "Noch nicht geprüft." :
            res.All(r => r.State == HealState.Ok) ? "✓ Alles in Ordnung." : $"{res.Count(r => r.State == HealState.Repaired)} repariert · {res.Count(r => r.State == HealState.NeedsYou)} brauchen dich" });
        if (last is { } l) sp.Children.Add(P($"Zuletzt geprüft: {l.At:dd.MM.yyyy HH:mm}", null, 11));
        foreach (var r in res)
            sp.Children.Add(WCard(new StackPanel { Children = {
                new TextBlock { Text = r.State switch { HealState.Ok => "✓ ", HealState.Repaired => "🔧 ", HealState.NeedsYou => "• ", _ => "⚠ " } + r.Title, Foreground = Text, FontSize = 13 },
                P(r.Detail, r.State == HealState.NeedsYou ? Brush.Parse("#FACC15") : null, 11) } }));
        var go = new Button { Name = "heal-run", Content = "Jetzt prüfen und reparieren", Background = Accent };
        go.Click += async (_, _) => { _healNote = "Prüfung läuft …"; Show("heilung"); var root = _core.DataRoot; LastHeal = await Task.Run(() => new SelfHealer(root, _core.Audit).Run(true, DateTime.Now)); _healNote = ""; Show("heilung"); };
        sp.Children.Add(go);
        return SysPage("Selbstheilung", sp);
    }
}
