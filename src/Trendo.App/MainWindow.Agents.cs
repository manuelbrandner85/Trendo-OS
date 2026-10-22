using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>CORE-03: 12 KI-Rollen, Team-Abläufe, Aufgaben je Projekt, Speichern nur mit Freigabe.</summary>
public partial class MainWindow
{
    private AgentCenter? _agents;
    public AgentCenter Agents => _agents ??= new AgentCenter(_core.DataRoot, _core.Audit, _core.Approvals, _core.Projects, _core.NotStopp);
    private string _agentRole = "planer", _agentMsg = ""; private string? _agentProject; private bool _agentBusy;

    private void InitCore03()
    {
        _showMap["ki-rollen"] = AgentsView; KiTabs.Add(("ki-rollen", "Rollen"));
        StudioPages["CORE-03"] = "ki-rollen";
        ExtraPages.Add(("KI-Rollen und Teams", "ki-rollen", ["agent", "rolle", "team", "planer", "texter", "grok build"]));
    }

    public async Task RunAgentAsync(string goal, string? team = null)
    {
        _agentBusy = true;
        try
        {
            if (team is not null) { var r = await Agents.RunTeamAsync(team, goal, Intel, _agentProject, KiModelName); _agentMsg = r.All(t => t.State == AgentTaskState.Fertig) ? $"Team fertig: {r.Count} Schritte." : "Team abgebrochen: " + r[^1].Result; }
            else { var t = await Agents.RunAsync(Agents.Create(_agentRole, goal, _agentProject), Intel, KiModelName); _agentMsg = t.State == AgentTaskState.Fertig ? "Fertig." : t.Result; }
        }
        catch (Exception ex) { _agentMsg = "Hat nicht geklappt: " + ex.Message; }
        _agentBusy = false;
    }

    private Control AgentsView()
    {
        var sp = new StackPanel { Spacing = 6, Children = { P("Wähle eine Rolle oder ein Team. Jede Rolle hat einen klaren Auftrag. Dateien schreibt eine Rolle nur nach deiner Freigabe.") } };
        var roles = new WrapPanel();
        foreach (var r in AgentCenter.Roles)
        {
            var b = new Button { Name = "role-" + r.Id, Content = r.Name, Margin = new Thickness(0, 0, 6, 6), Background = _agentRole == r.Id ? Accent : null };
            ToolTip.SetTip(b, r.Does);
            b.Click += (_, _) => { _agentRole = r.Id; Show("ki-rollen"); };
            roles.Children.Add(b);
        }
        sp.Children.Add(roles);
        sp.Children.Add(P($"{AgentCenter.Role(_agentRole).Name}: {AgentCenter.Role(_agentRole).Does}{(AgentCenter.Role(_agentRole).MayWriteFiles ? " · darf nach Freigabe Dateien speichern" : " · schreibt keine Dateien")}", Text, 12));
        var projects = _core.Projects.List();
        var proj = new ComboBox { Name = "agent-project", MinWidth = 220, PlaceholderText = "Projekt (optional)" };
        proj.Items.Add("(kein Projekt)"); foreach (var p in projects) proj.Items.Add(p.Name);
        proj.SelectedIndex = _agentProject is null ? 0 : 1 + projects.ToList().FindIndex(p => p.Id == _agentProject);
        proj.SelectionChanged += (_, _) => _agentProject = proj.SelectedIndex <= 0 ? null : projects[proj.SelectedIndex - 1].Id;
        var goal = new TextBox { Name = "agent-goal", PlaceholderText = "Was soll erledigt werden?", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 54 };
        var go = new Button { Name = "agent-run", Content = "Rolle starten", Background = Accent, IsEnabled = !_agentBusy };
        go.Click += async (_, _) => { if (string.IsNullOrWhiteSpace(goal.Text)) { _agentMsg = "Bitte zuerst ein Ziel eintragen."; Show("ki-rollen"); return; } go.IsEnabled = false; await RunAgentAsync(goal.Text); Show("ki-rollen"); };
        var teams = new WrapPanel();
        foreach (var (id, name, rs) in AgentCenter.Teams)
        {
            var b = new Button { Name = "team-" + id, Content = $"Team: {name}", Margin = new Thickness(0, 0, 6, 6), IsEnabled = !_agentBusy };
            ToolTip.SetTip(b, string.Join(" → ", rs.Select(x => AgentCenter.Role(x).Name)));
            b.Click += async (_, _) => { if (string.IsNullOrWhiteSpace(goal.Text)) { _agentMsg = "Bitte zuerst ein Ziel eintragen."; Show("ki-rollen"); return; } b.IsEnabled = false; await RunAgentAsync(goal.Text, id); Show("ki-rollen"); };
            teams.Children.Add(b);
        }
        sp.Children.Add(proj); sp.Children.Add(goal);
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { go } });
        sp.Children.Add(teams);
        sp.Children.Add(new TextBlock { Name = "agent-msg", Text = _agentMsg, Foreground = Text, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(H("Aufgaben", 14));
        var tasks = Agents.Tasks(_agentProject).Take(15).ToList();
        if (tasks.Count == 0) sp.Children.Add(P("Noch keine Aufgaben."));
        var i = 0;
        foreach (var t in tasks)
        {
            var n = i++;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            var role = AgentCenter.Role(t.RoleId);
            if (t.ProjectId is not null && role.MayWriteFiles && t.State == AgentTaskState.Fertig)
            {
                var save = new Button { Name = $"agent-save-{n}", Content = "Im Projekt speichern …", Padding = new Thickness(8, 2) };
                save.Click += (_, _) => { var r = Agents.RequestSave(t, $"{role.Id}-{t.Id}.md"); _agentMsg = "Freigabe angefragt – unter „Freigaben“ bestätigen."; Show("ki-rollen"); };
                row.Children.Add(save);
            }
            if (t.State == AgentTaskState.WartetAufFreigabe)
            {
                var ex = new Button { Name = $"agent-exec-{n}", Content = "Speichern (nach Freigabe)", Padding = new Thickness(8, 2) };
                ex.Click += (_, _) => { try { Agents.ExecuteSave(t, $"{role.Id}-{t.Id}.md"); _agentMsg = "Gespeichert."; } catch (ApprovalException) { _agentMsg = "Noch nicht freigegeben. Öffne „Freigaben“."; } Show("ki-rollen"); };
                row.Children.Add(ex);
            }
            if (t.ProjectId is not null && t.State is AgentTaskState.Fertig or AgentTaskState.Gespeichert)
            {
                var gb = new Button { Name = $"agent-grok-{n}", Content = "An Grok Build übergeben", Padding = new Thickness(8, 2) };
                ToolTip.SetTip(gb, "Legt einen Auftrag im Projekt ab. Grok Build ist noch nicht verbunden.");
                gb.Click += (_, _) => { _agentMsg = $"Auftrag abgelegt: {Agents.ExportGrokBuildJob(t)}. Grok Build ist noch nicht verbunden – er führt ihn aus, sobald er eingerichtet ist."; Show("ki-rollen"); };
                row.Children.Add(gb);
            }
            var del = new Button { Name = $"agent-del-{n}", Content = "Entfernen", Padding = new Thickness(8, 2) };
            del.Click += (_, _) => { Agents.Remove(t.Id); Show("ki-rollen"); };
            row.Children.Add(del);
            var state = t.State switch { AgentTaskState.Fertig => "fertig", AgentTaskState.Fehler => "nicht geklappt", AgentTaskState.Laeuft => "läuft", AgentTaskState.WartetAufFreigabe => "wartet auf Freigabe", AgentTaskState.Gespeichert => $"gespeichert als {t.SavedAs}", _ => "neu" };
            sp.Children.Add(WCard(new StackPanel { Spacing = 3, Children = {
                new TextBlock { Text = $"{role.Name}: {t.Goal}", Foreground = Text, FontSize = 13, TextWrapping = TextWrapping.Wrap },
                P($"{state} · {t.Created:dd.MM. HH:mm}{(t.TeamId is null ? "" : " · Team")}", null, 10.5),
                new SelectableTextBlock { Name = $"agent-result-{n}", Text = t.Result.Length > 600 ? t.Result[..600] + " …" : t.Result, Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap },
                row } }));
        }
        return KiPage("KI-Rollen", sp);
    }
}
