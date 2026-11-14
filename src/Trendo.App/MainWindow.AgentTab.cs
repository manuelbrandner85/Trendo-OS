using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Trendo-Agent als Reiter im Claude-Bereich (T-0316; vereinfacht nach Wunsch 01.11.2026 „ich sage nur Ja oder Nein“):
/// Wunsch eintippen → „Los“ (oder Enter). Der Agent plant, setzt um, prüft und bessert bis zu dreimal selbst nach – alles sichtbar als Checkliste,
/// vorher wird der Projektordner gesichert. Rückfragen nur, wenn nötig, als Knöpfe. Am Ende nur: „Behalten?“ – Ja oder Nein (alles zurück).
/// Arbeitet mit der KI, die Trendo hat – auch ohne Claude-Abo.</summary>
public partial class MainWindow
{
    sealed class AgentState
    {
        public string Goal = "", Status = "", Model = ""; public AgentPlan? Plan; public readonly Dictionary<string, string> Answers = [];
        public readonly List<AgentChange> Applied = []; public List<string> Rejected = []; public List<AgentCheck> Checks = [];
        public string? Shot; public FolderSnapshot? Snapshot; public int Round; public bool Busy, Done; public string? Open; public readonly List<string> Log = [];
    }
    readonly Dictionary<string, AgentState> _trendoAgents = [];
    /// <summary>Für Tests: Antwort der KI auf einen Agent-Auftrag (statt Intel).</summary>
    public Func<string, Task<string>>? AgentAskOverride { get; set; }

    AgentState AgentOf(string winId) => _trendoAgents.TryGetValue(winId, out var a) ? a : _trendoAgents[winId] = new();
    public string AgentStatus(string winId) => AgentOf(winId).Status;
    public IReadOnlyList<string> AgentSteps(string winId) => AgentOf(winId).Plan?.Steps ?? [];
    public IReadOnlyList<string> AgentChangedFiles(string winId) => AgentOf(winId).Applied.Select(c => c.Path).Distinct().ToList();
    public bool AgentDone(string winId) => AgentOf(winId).Done;

    async Task<string> AgentAsk(AgentState a, string prompt)
    {
        if (AgentAskOverride is { } o) { a.Model = "Test-KI"; return await o(prompt); }
        var ans = await Intel.AskAsync(new KiRequest(prompt, PreferredModel: KiModelName, UseKnowledge: false, AllowRemote: true));
        a.Model = ans.Model is null ? "" : $"{ans.Model} · {ans.Backend}";
        if (!ans.Ok) throw new InvalidOperationException(ans.Text);
        return ans.Text;
    }

    /// <summary>Agent im aktiven Fenster starten (öffentlich für Tests und die Auftragsleiste).</summary>
    public async Task AgentGo(string goal)
    {
        if (Windows.Active?.Id is not { } id || !_claudeDocks.TryGetValue(id, out var dock)) return;
        var a = AgentOf(id);
        if (a.Busy) return;
        a.Goal = goal.Trim(); a.Plan = null; a.Answers.Clear(); a.Applied.Clear(); a.Checks = []; a.Rejected = []; a.Round = 0; a.Shot = null; a.Done = false; a.Log.Clear(); a.Open = null;
        if (a.Snapshot is not null) { a.Snapshot = null; } // vorige Runde ist behalten
        if (a.Goal.Length == 0) { a.Status = "Schreib zuerst, was der Agent tun soll."; BuildAgentPane(dock); return; }
        await AgentRun(a, dock);
    }

    /// <summary>Der ganze Ablauf ohne Zwischenfragen: planen → (nur bei Bedarf fragen) → umsetzen → prüfen → nachbessern → „Behalten?“.</summary>
    async Task AgentRun(AgentState a, ClaudeDock dock)
    {
        a.Busy = true;
        try
        {
            if (a.Plan is null)
            {
                Say("Ich plane …");
                a.Plan = TrendoAgent.ParsePlan(await AgentAsk(a, TrendoAgent.PlanPrompt(a.Goal, ClaudeRoleFor(dock.Studio), dock.Folder)));
                _core.Audit.Append(Principal.LocalOwner.Name, "agent.plan", $"{dock.Studio} schritte={a.Plan.Steps.Count}");
            }
            if (a.Plan.Questions.FirstOrDefault(q => !a.Answers.ContainsKey(q.Question)) is { } open)
            {
                a.Status = open.Question; a.Busy = false; BuildAgentPane(dock); return; // wartet auf einen Knopf
            }
            string? feedback = null;
            for (a.Round = 1; a.Round <= TrendoAgent.MaxRounds; a.Round++)
            {
                Say(a.Round == 1 ? "Ich setze um …" : $"Ich bessere nach (Runde {a.Round}) …");
                var rejected = new List<string>();
                var changes = TrendoAgent.ParseChanges(await AgentAsk(a, TrendoAgent.ChangePrompt(a.Goal, a.Plan, a.Answers, ClaudeRoleFor(dock.Studio), dock.Folder, feedback)), dock.Folder, rejected);
                a.Rejected.AddRange(rejected);
                if (changes.Count == 0) { a.Log.Add(a.Round == 1 ? "Die KI hat keine Änderung geliefert." : "Keine weitere Änderung."); break; }
                a.Snapshot ??= FolderSnapshot.Take(dock.Folder, Path.Combine(_core.DataRoot, "agent", "sicherung", Guid.NewGuid().ToString("N")[..8]));
                TrendoAgent.Apply(dock.Folder, changes); a.Applied.AddRange(changes);
                a.Log.Add($"Runde {a.Round}: {changes.Count} Dateien geändert ({string.Join(", ", changes.Select(c => c.Path).Take(4))}{(changes.Count > 4 ? " …" : "")})");
                _core.Audit.Append(Principal.LocalOwner.Name, "agent.apply", $"{dock.Studio} runde={a.Round} dateien={changes.Count}");
                Say("Ich prüfe …");
                a.Checks = TrendoAgent.Check(dock.Folder, a.Applied.Select(c => c.Path).Distinct());
                var bad = a.Checks.Count(c => !c.Ok);
                a.Log.Add(bad == 0 ? "Prüfung sauber." : $"Prüfung: {bad} Hinweise.");
                if (bad == 0) break;
                feedback = TrendoAgent.Feedback(a.Checks);
            }
            if (a.Round > TrendoAgent.MaxRounds) a.Round = TrendoAgent.MaxRounds;
            await AgentShot(a, dock);
            a.Done = a.Applied.Count > 0;
            var left = a.Checks.Count(c => !c.Ok);
            a.Status = !a.Done ? "Es wurde nichts geändert." : left == 0 ? "Fertig. Behalten?" : $"Fertig – {left} Hinweise sind noch offen (Details unten). Behalten?";
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or UnauthorizedAccessException or TaskCanceledException or HttpRequestException)
        {
            a.Status = "⚠ " + e.Message + (a.Snapshot is not null ? " – Bisheriges kannst du unten zurücknehmen." : "");
            a.Done = a.Snapshot is not null;
        }
        a.Busy = false; BuildAgentPane(dock);

        void Say(string s) { a.Status = s; BuildAgentPane(dock); }
    }

    async Task AgentShot(AgentState a, ClaudeDock dock)
    {
        a.Shot = null;
        if (_opt.Isolated || !a.Applied.Any(c => c.Path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) || !File.Exists(Path.Combine(dock.Folder, "index.html"))) return;
        var r = await WebsitePreview.CaptureAsync(dock.Folder, 1280, 800, Path.Combine(_core.DataRoot, "agent", "vorschau.png"));
        a.Shot = r.Ok ? r.Path : null;
    }

    void BuildAgentPane(ClaudeDock dock)
    {
        if (dock.AgentPane is null) return;
        var id = _claudeDocks.FirstOrDefault(kv => kv.Value == dock).Key; if (id is null) return;
        var a = AgentOf(id);
        var sp = new StackPanel { Name = "agent-pane", Spacing = 8, Margin = new Thickness(12, 6) };
        var goal = new TextBox { Name = "agent-goal", Text = a.Goal.Length > 0 ? a.Goal : this.GetLogicalDescendantsOf<TextBox>().FirstOrDefault(t => t.Name == "auftrag-prompt")?.Text ?? "",
            PlaceholderText = "Was soll ich bauen oder ändern? z. B. „Kontaktseite mit Formular“", TextWrapping = TextWrapping.Wrap, MinHeight = 40, IsEnabled = !a.Busy };
        goal.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter && e.KeyModifiers == Avalonia.Input.KeyModifiers.None) { _ = AgentGo(goal.Text ?? ""); e.Handled = true; } };
        var go = Act("agent-go", a.Busy ? "Arbeitet …" : "Los", () => _ = AgentGo(goal.Text ?? ""), true); go.IsEnabled = !a.Busy;
        sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,Auto"), Children = { goal, Pos(go, 2) } });
        if (a.Goal.Length == 0) sp.Children.Add(P("Sag in deinen Worten, was du willst, und drück „Los“. Ich plane, setze um, prüfe und bessere selbst nach – am Ende fragst du nur: behalten oder nicht.", Muted, 11.5));
        if (a.Status.Length > 0) sp.Children.Add(new TextBlock { Name = "agent-status", Text = a.Status, Foreground = a.Status.StartsWith('⚠') ? Brush.Parse("#F97316") : GoldText(), FontSize = 15, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });

        // Rückfrage nur bei Bedarf: Knöpfe, danach geht es von selbst weiter
        if (!a.Busy && a.Plan?.Questions.FirstOrDefault(q => !a.Answers.ContainsKey(q.Question)) is { } q)
        {
            var row = new WrapPanel { Name = "agent-question" }; var oi = 0;
            foreach (var opt in q.Options)
            {
                var o = opt; var b = Act($"agent-answer-{oi}", (oi == 0 ? "★ " : "") + o, () => { a.Answers[q.Question] = o; _ = AgentRun(a, dock); }, oi == 0); b.Margin = new Thickness(0, 0, 6, 4); oi++;
                row.Children.Add(b);
            }
            sp.Children.Add(row);
        }

        // Ergebnis: eine Entscheidung
        if (a.Done && !a.Busy)
        {
            if (a.Shot is { } shot && File.Exists(shot))
                try { using var s = File.OpenRead(shot); sp.Children.Add(new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = new Image { Name = "agent-shot", Source = Avalonia.Media.Imaging.Bitmap.DecodeToWidth(s, 520), Stretch = Stretch.Uniform } }); }
                catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException) { }
            var keep = Act("agent-keep", "Ja, behalten", () => { a.Snapshot = null; a.Done = false; a.Status = "✓ Behalten."; _core.Audit.Append(Principal.LocalOwner.Name, "agent.keep", dock.Studio ?? ""); BuildAgentPane(dock); }, true);
            var undo = Act("agent-undo", "Nein, rückgängig", () =>
            {
                a.Snapshot?.Restore(); a.Snapshot = null; a.Done = false; a.Status = "Alles ist wieder wie vorher."; a.Applied.Clear(); a.Checks = [];
                _core.Audit.Append(Principal.LocalOwner.Name, "agent.undo", dock.Studio ?? ""); BuildAgentPane(dock);
            });
            keep.FontSize = 15; undo.FontSize = 15;
            sp.Children.Add(new WrapPanel { Children = { keep, new Border { Width = 8 }, undo } });
        }

        // Was passiert (mitlaufend): Plan als Checkliste, Protokoll, Details auf Wunsch
        if (a.Plan is { } p)
        {
            var list = new StackPanel { Name = "agent-steps", Spacing = 2 };
            var finished = a.Done || a.Status.StartsWith('✓');
            for (var i = 0; i < p.Steps.Count; i++)
                list.Children.Add(new TextBlock { Name = $"agent-step-{i}", Text = (finished ? "✓ " : a.Applied.Count > 0 ? "◐ " : "○ ") + p.Steps[i], Foreground = finished ? Brush.Parse("#86EFAC") : Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
            foreach (var l in a.Log) list.Children.Add(new TextBlock { Text = "· " + l, Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap });
            sp.Children.Add(Card(list));
        }
        if (a.Applied.Count > 0 || a.Checks.Count > 0)
        {
            var det = new StackPanel { Spacing = 4 };
            foreach (var c in a.Checks) det.Children.Add(new TextBlock { Text = (c.Ok ? "✓ " : "✕ ") + c.Text, Foreground = c.Ok ? Brush.Parse("#86EFAC") : Brush.Parse("#FCA5A5"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
            foreach (var r in a.Rejected) det.Children.Add(P("⚠ " + r, Brush.Parse("#F5A524"), 11));
            var k = 0;
            foreach (var c in a.Applied.GroupBy(c => c.Path).Select(g => new AgentChange(g.Key, g.First().Before, g.Last().After, 0, 0)))
            {
                var ch = c;
                var b = new Button { Name = $"agent-diff-{k++}", Content = (a.Open == c.Path ? "▾ " : "▸ ") + (c.IsNew ? "neu: " : "") + c.Path, Background = Brushes.Transparent, FontSize = 12, Padding = new Thickness(4, 1) };
                b.Click += (_, _) => { a.Open = a.Open == ch.Path ? null : ch.Path; BuildAgentPane(dock); };
                det.Children.Add(b);
                if (a.Open != c.Path) continue;
                var diff = new StackPanel();
                foreach (var line in TrendoAgent.Diff(c.Before ?? "", c.After, 300))
                    diff.Children.Add(new TextBlock { Text = line, FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap,
                        Foreground = line.StartsWith('+') ? Brush.Parse("#86EFAC") : line.StartsWith('-') ? Brush.Parse("#FCA5A5") : Muted });
                det.Children.Add(new Border { CornerRadius = new CornerRadius(8), Background = Brush.Parse("#FF0A0F1C"), Padding = new Thickness(8), MaxHeight = 300, Child = new ScrollViewer { Content = diff } });
            }
            sp.Children.Add(new Expander { Name = "agent-details", Header = "Details: Prüfung und Vorher/Nachher", Content = det, IsExpanded = false });
        }
        if (a.Model.Length > 0) sp.Children.Add(new TextBlock { Name = "agent-model", Text = "KI: " + a.Model + " · für anspruchsvolle Designs ist Claude Code (Reiter links) stärker.", Foreground = Muted, FontSize = 10.5, TextWrapping = TextWrapping.Wrap });
        dock.AgentPane.Child = new ScrollViewer { Content = sp };
    }
}
