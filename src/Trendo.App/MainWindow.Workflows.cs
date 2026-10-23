using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SYS-05: Abläufe – Trendo erledigt wiederkehrende Aufgaben automatisch.</summary>
public partial class MainWindow
{
    private WorkflowEngine? _wf;
    public static string WorkflowHome { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public WorkflowEngine Workflows => _wf ??= new WorkflowEngine(_core.DataRoot, _core.Audit, _core.Approvals, [WorkflowHome]) { Summarize = async t => { var a = await Intel.AskAsync(new KiRequest("Fasse diesen Text in 5 Stichpunkten zusammen:\n\n" + (t.Length > 8000 ? t[..8000] : t), PreferredModel: KiModelName, UseKnowledge: false)); return a.Ok ? a.Text : null; } };
    public WfRun? LastWfRun { get; private set; }
    private string _wfMsg = ""; private bool _wfNew;
    private readonly List<WfStep> _wfDraft = [];

    private void InitSys05()
    {
        _showMap["ablaeufe"] = WorkflowsView; KiTabs.Add(("ablaeufe", "Abläufe"));
        StudioPages["SYS-05"] = "ablaeufe";
        ExtraPages.Add(("Abläufe (Automatisch erledigen)", "ablaeufe", ["automatisch", "ablauf", "workflow", "aufraeumen", "zeitplan", "regel"]));
        var t = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        t.Tick += async (_, _) => { try { foreach (var r in await Workflows.RunDueAsync(DateTimeOffset.Now)) LastWfRun = r; } catch { } };
        OwnTimer(t);
    }

    private static string TriggerText(Workflow w) => w.Trigger switch
    {
        TriggerKind.Taeglich => $"jeden Tag um {w.At}", TriggerKind.Intervall => w.EveryMinutes % 1440 == 0 ? $"alle {w.EveryMinutes / 1440} Tag(e)" : $"alle {w.EveryMinutes} Minuten",
        TriggerKind.NeueDatei => $"wenn in {w.WatchFolder} eine neue Datei ({w.WatchPattern}) auftaucht", _ => "nur von Hand",
    };

    private Control WorkflowsView()
    {
        var sp = new StackPanel { Spacing = 6, Children = { P("Abläufe erledigen Wiederkehrendes von allein. Mit „Probelauf“ siehst du vorher, was passieren würde – ohne etwas zu ändern.") } };
        var tpl = new WrapPanel();
        foreach (var (id, name, make) in WorkflowEngine.Templates)
        {
            var b = new Button { Name = "wf-tpl-" + id, Content = "+ " + name, Margin = new Thickness(0, 0, 6, 6) };
            b.Click += (_, _) => { var w = Workflows.Add(make(WorkflowHome)); _wfMsg = $"„{w.Name}“ angelegt. Mach zuerst einen Probelauf."; Show("ablaeufe"); };
            tpl.Children.Add(b);
        }
        var nb = new Button { Name = "wf-new", Content = "+ Eigener Ablauf", Margin = new Thickness(0, 0, 6, 6) };
        nb.Click += (_, _) => { _wfNew = !_wfNew; _wfDraft.Clear(); Show("ablaeufe"); };
        tpl.Children.Add(nb);
        sp.Children.Add(tpl);
        if (_wfNew) sp.Children.Add(WorkflowEditor());
        sp.Children.Add(new TextBlock { Name = "wf-msg", Text = _wfMsg, Foreground = Text, TextWrapping = TextWrapping.Wrap });
        if (LastWfRun is { } lr)
        {
            var box = new StackPanel { Name = "wf-run", Spacing = 2, Children = { new TextBlock { Text = lr.DryRun ? "Probelauf – nichts wurde geändert:" : lr.Ok ? "Ausgeführt:" : "Nicht ganz geklappt:", Foreground = lr.Ok ? Text : Brush.Parse("#FACC15"), FontWeight = FontWeight.SemiBold } } };
            foreach (var l in lr.Lines.Take(30)) box.Children.Add(P("• " + l, null, 11));
            sp.Children.Add(WCard(box));
        }
        var i = 0;
        foreach (var w in Workflows.All())
        {
            var n = i++;
            var row = new WrapPanel();
            Button B(string name, string label, Func<Task> act) { var b = new Button { Name = $"wf-{name}-{n}", Content = label, Padding = new Thickness(8, 2), Margin = new Thickness(0, 4, 6, 0) }; b.Click += async (_, _) => { await act(); Show("ablaeufe"); }; row.Children.Add(b); return b; }
            B("dry", "Probelauf", async () => { LastWfRun = await Workflows.RunAsync(w, true); _wfMsg = ""; });
            B("run", "Jetzt ausführen", async () => { LastWfRun = await Workflows.RunAsync(w, false); _wfMsg = ""; });
            if (!w.IsApproved) B("approve", "Freigeben …", () => { var r = Workflows.RequestApproval(w); _core.Approvals.Approve(r.Id, Principal.LocalOwner); Workflows.ApplyApproval(w, r.Id); _wfMsg = $"„{w.Name}“ ist freigegeben. Änderst du den Ablauf, fragt Trendo erneut."; return Task.CompletedTask; });
            B("toggle", w.Enabled ? "Pausieren" : "Einschalten", () => { w.Enabled = !w.Enabled; Workflows.Update(w); return Task.CompletedTask; });
            B("del", "Löschen", () => { Workflows.Remove(w.Id); return Task.CompletedTask; });
            var last = Workflows.History(w.Id, 1).FirstOrDefault();
            sp.Children.Add(WCard(new StackPanel { Spacing = 2, Children = {
                new TextBlock { Text = w.Name + (w.Enabled ? "" : " (pausiert)"), Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold },
                P($"Startet {TriggerText(w)}{(w.NeedsApproval ? w.IsApproved ? " · freigegeben" : " · braucht Freigabe (verschiebt/benennt Dateien)" : "")}", w.IsApproved ? null : Brush.Parse("#FACC15"), 11),
                P(string.Join(" → ", w.Steps.Select(s => WorkflowEngine.StepLabel(s.Kind))), null, 11),
                P(last is null ? "Noch nie gelaufen." : $"Zuletzt {last.At:dd.MM. HH:mm}: {(last.Ok ? "ok" : "mit Fehler")}", null, 10.5), row } }));
        }
        if (Workflows.All().Count == 0) sp.Children.Add(P("Noch keine Abläufe. Starte mit einer Vorlage oben."));
        return KiPage("Abläufe", sp);
    }

    private Control WorkflowEditor()
    {
        var name = new TextBox { Name = "wfe-name", PlaceholderText = "Name des Ablaufs" };
        var trig = new ComboBox { Name = "wfe-trigger", SelectedIndex = 0, ItemsSource = new[] { "Nur von Hand", "Jeden Tag", "Alle X Minuten", "Neue Datei im Ordner" } };
        var at = new TextBox { Name = "wfe-at", PlaceholderText = "Uhrzeit oder Minuten oder Ordner", Width = 300 };
        var kind = new ComboBox { Name = "wfe-kind", SelectedIndex = 0, ItemsSource = Enum.GetValues<StepKind>().Select(WorkflowEngine.StepLabel).ToList() };
        var from = new TextBox { Name = "wfe-from", PlaceholderText = "Von Ordner", Width = 220 };
        var to = new TextBox { Name = "wfe-to", PlaceholderText = "Nach (Ordner/Datei), {datum} erlaubt", Width = 220 };
        var pat = new TextBox { Name = "wfe-pattern", PlaceholderText = "Muster, z. B. *.pdf", Width = 120 };
        var text = new TextBox { Name = "wfe-text", PlaceholderText = "Text (für „Textdatei schreiben“)" };
        var steps = new StackPanel { Spacing = 2 };
        foreach (var s in _wfDraft) steps.Children.Add(P($"• {WorkflowEngine.StepLabel(s.Kind)} {s.From} {s.To} {s.Pattern}", null, 11));
        var add = new Button { Name = "wfe-add", Content = "Schritt hinzufügen" };
        add.Click += (_, _) => { _wfDraft.Add(new WfStep { Kind = (StepKind)kind.SelectedIndex, From = from.Text ?? "", To = to.Text ?? "", Pattern = string.IsNullOrWhiteSpace(pat.Text) ? "*" : pat.Text, Text = text.Text ?? "" }); var nm = name.Text; Show("ablaeufe"); };
        var save = new Button { Name = "wfe-save", Content = "Ablauf speichern", Background = Accent };
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(name.Text) || _wfDraft.Count == 0) { _wfMsg = "Bitte Namen eintragen und mindestens einen Schritt hinzufügen."; Show("ablaeufe"); return; }
            var w = new Workflow { Name = name.Text.Trim(), Trigger = (TriggerKind)trig.SelectedIndex, Steps = _wfDraft.ToList() };
            var a = at.Text?.Trim() ?? "";
            if (w.Trigger == TriggerKind.Taeglich) w.At = TimeOnly.TryParse(a, out var t) ? t.ToString("HH:mm") : "08:00";
            if (w.Trigger == TriggerKind.Intervall) w.EveryMinutes = int.TryParse(a, out var m) && m > 0 ? m : 60;
            if (w.Trigger == TriggerKind.NeueDatei) { w.WatchFolder = a; w.WatchPattern = _wfDraft[0].Pattern; }
            Workflows.Add(w); _wfDraft.Clear(); _wfNew = false; _wfMsg = $"„{w.Name}“ gespeichert. Mach zuerst einen Probelauf."; Show("ablaeufe");
        };
        return WCard(new StackPanel { Spacing = 6, Children = { name, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { trig, at } },
            new WrapPanel { Children = { kind, from, to, pat } }, text, steps, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { add, save } } } });
    }
}

