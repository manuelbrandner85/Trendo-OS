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
    public AgentCenter Agents => _agents ??= new AgentCenter(_core.DataRoot, _core.Audit, _core.Approvals, _core.Projects);
    private string _agentRole = "planer", _agentMsg = ""; private string? _agentProject; private bool _agentBusy;
    /// <summary>Wunsch 03.11.2026: Rollen gehören fest zu einem Studio – hier gewählt (leer = allgemeine Rollen).</summary>
    private string? _agentStudio; private bool _agentNewRole;
    /// <summary>Team des Studios (Profi-Team und eigene Rollen) als Claude-Code-Agenten in den Projektordner legen.</summary>
    int ClaudeTeamExport(string folder, string studio) => StudioAgentsExport.Write(folder, Agents.RolesFor(studio), StudioCatalog.For(studio)?.Name ?? studio);

    /// <summary>„👥 Team“ im Studio: „KI einsetzen“ mit dem Team dieses Studios öffnen.</summary>
    public void AgentsForStudio(string? studioId)
    {
        _agentStudio = studioId;
        if (studioId is not null && Agents.RolesFor(studioId) is { Count: > 0 } rs) _agentRole = rs[0].Id;
        Show("ki-rollen");
    }

    private void InitCore03()
    {
        _showMap["ki-rollen"] = AgentsView; KiTabs.Add(("ki-rollen", "Rollen"));
        AgentCenter.StudioName = id => StudioCatalog.For(id)?.Name;
        StudioPages["CORE-03"] = "ki-rollen";
        ExtraPages.Add(("KI-Rollen und Teams", "ki-rollen", ["agent", "rolle", "team", "planer", "texter", "claude code"]));
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
        var sp = new StackPanel { Spacing = 6, Children = { P("Wähle ein Studio – dort arbeitet sein Profi-Team mit. Jede Rolle gehört fest zu einem Studio und hat einen klaren Auftrag. Dateien schreibt eine Rolle nur nach deiner Freigabe.") } };
        var studios = StudioCatalog.ById.Where(kv => StudioTeams.For(kv.Key) is not null || Agents.CustomRoles().Any(r => r.Studio == kv.Key)).OrderBy(kv => kv.Value.Name).ToList();
        var studioBox = new ComboBox { Name = "agent-studio", MinWidth = 260, ItemsSource = new[] { "Allgemeine Rollen (kein Studio)" }.Concat(studios.Select(kv => kv.Value.Name)).ToArray(),
            SelectedIndex = _agentStudio is null ? 0 : 1 + studios.FindIndex(kv => kv.Key == _agentStudio) };
        studioBox.SelectionChanged += (_, _) => { var i = studioBox.SelectedIndex; var ns = i <= 0 ? null : studios[i - 1].Key; if (ns != _agentStudio) AgentsForStudio(ns); }; // nur echte Wechsel
        var newRole = Act("agent-role-new", "+ Eigene Rolle", () => { _agentNewRole = !_agentNewRole; Show("ki-rollen"); });
        sp.Children.Add(new WrapPanel { Children = { studioBox, Spacer(newRole) } });
        if (_agentNewRole) sp.Children.Add(AgentNewRoleCard(studios));
        var roleList = _agentStudio is null ? AgentCenter.Roles.ToList() : Agents.RolesFor(_agentStudio).ToList();
        if (roleList.All(r => r.Id != _agentRole) && roleList.Count > 0) _agentRole = roleList[0].Id;
        if (_agentStudio is { } sid && StudioTeams.For(sid) is { } team0)
            sp.Children.Add(P($"{team0.Name} im Studio „{StudioCatalog.For(sid)?.Name}“: {string.Join(" → ", team0.Roles.Select(r => r.Name))}", Text, 12.5));
        var roles = new WrapPanel();
        foreach (var r in roleList)
        {
            // I2 (07.11.2026): Rollen als Karten mit Anfangsbuchstaben-Abzeichen, Auftrag und Schreibrecht
            var on = _agentRole == r.Id;
            var b = new Button { Name = "role-" + r.Id, Width = 196, Height = 92, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(12),
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Background = on ? Brush.Parse("#33C9A227") : TileBg, BorderBrush = on ? Accent : EdgeLit, BorderThickness = new Thickness(on ? 2 : 1),
                Content = new StackPanel { Spacing = 3, Children = {
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = {
                        new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Background = on ? Accent : Brush.Parse("#2AFFFFFF"),
                            Child = new TextBlock { Text = r.Name.Length > 0 ? r.Name[..1].ToUpperInvariant() : "?", Foreground = Brushes.White, FontWeight = FontWeight.Bold, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } },
                        new TextBlock { Text = (r.Id.StartsWith("eigen.") ? "✎ " : "") + r.Name, Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis } } },
                    new TextBlock { Text = r.Does, Foreground = Muted, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, MaxHeight = 28, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = r.MayWriteFiles ? "darf nach Freigabe speichern" : "nur Vorschläge", Foreground = r.MayWriteFiles ? Brush.Parse("#FACC15") : Muted, FontSize = 9.5 } } } };
            Hover(b);
            ToolTip.SetTip(b, r.Does + (r.Studio is { } rs ? $" · gehört zu „{StudioCatalog.For(rs)?.Name}“" : ""));
            b.Click += (_, _) => { _agentRole = r.Id; Show("ki-rollen"); };
            roles.Children.Add(b);
        }
        sp.Children.Add(roles);
        var cur = roleList.FirstOrDefault(r => r.Id == _agentRole) ?? Agents.RoleOf(_agentRole);
        sp.Children.Add(P($"{cur.Name}: {cur.Does}{(cur.MayWriteFiles ? " · darf nach Freigabe Dateien speichern" : " · schreibt keine Dateien")}", Text, 12));
        if (cur.Id.StartsWith("eigen.")) sp.Children.Add(Act("agent-role-del", "Eigene Rolle entfernen", () => { Agents.RemoveCustomRole(cur.Id); _agentMsg = $"Rolle „{cur.Name}“ entfernt."; Show("ki-rollen"); }));
        var projects = _core.Projects.List();
        var proj = new ComboBox { Name = "agent-project", MinWidth = 220, PlaceholderText = "Projekt (optional)" };
        proj.Items.Add("(kein Projekt)"); foreach (var p in projects) proj.Items.Add(p.Name);
        proj.SelectedIndex = _agentProject is null ? 0 : 1 + projects.ToList().FindIndex(p => p.Id == _agentProject);
        proj.SelectionChanged += (_, _) => _agentProject = proj.SelectedIndex <= 0 ? null : projects[proj.SelectedIndex - 1].Id;
        var goal = new TextBox { Name = "agent-goal", PlaceholderText = "Was soll erledigt werden?", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 54 };
        var go = new Button { Name = "agent-run", Content = "Rolle starten", Background = Accent, IsEnabled = !_agentBusy };
        go.Click += async (_, _) => { if (string.IsNullOrWhiteSpace(goal.Text)) { _agentMsg = "Bitte zuerst ein Ziel eintragen."; Show("ki-rollen"); return; } go.IsEnabled = false; await RunAgentAsync(goal.Text); Show("ki-rollen"); };
        var teams = new WrapPanel();
        List<(string Id, string Name, string[] Roles)> teamList = _agentStudio is { } ts && AgentCenter.Team(AgentCenter.TeamId(ts)) is { } st ? [st] : [.. AgentCenter.Teams];
        foreach (var (id, name, rs) in teamList)
        {
            var b = new Button { Name = "team-" + id, Content = $"Team: {name}", Margin = new Thickness(0, 0, 6, 6), IsEnabled = !_agentBusy };
            ToolTip.SetTip(b, string.Join(" → ", rs.Select(x => Agents.RoleOf(x).Name)));
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
            AgentRole role; try { role = Agents.RoleOf(t.RoleId); } catch (InvalidOperationException) { role = new(t.RoleId, "Entfernte Rolle", "", "", false); }
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
                var gb = new Button { Name = $"agent-claude-{n}", Content = "An Claude Code übergeben", Padding = new Thickness(8, 2) };
                ToolTip.SetTip(gb, "Legt einen Auftrag im Projekt ab – Claude Code arbeitet ihn im Claude-Chat des Studios ab.");
                gb.Click += (_, _) => { _agentMsg = $"Auftrag abgelegt: {Agents.ExportClaudeCodeJob(t)}. Im Claude-Chat des Studios kannst du Claude bitten, ihn abzuarbeiten."; Show("ki-rollen"); };
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

        static Control Spacer(Control c) { c.Margin = new Thickness(8, 0, 0, 0); return c; }
    }

    /// <summary>Eigene Rolle anlegen – das Studio ist Pflicht (Wunsch 03.11.2026).</summary>
    Control AgentNewRoleCard(List<KeyValuePair<string, StudioInfo>> studios)
    {
        var all = StudioCatalog.ById.OrderBy(kv => kv.Value.Name).ToList();
        var name = new TextBox { Name = "agent-role-name", Watermark = "Name, z. B. Steuerberater-Vorprüfer" };
        var does = new TextBox { Name = "agent-role-does", Watermark = "Was tut die Rolle? (ein Satz)" };
        var focus = new TextBox { Name = "agent-role-focus", Watermark = "Worauf achtet sie besonders? (optional)" };
        var studio = new ComboBox { Name = "agent-role-studio", MinWidth = 240, PlaceholderText = "Studio wählen (Pflicht)", ItemsSource = all.Select(kv => kv.Value.Name).ToArray(),
            SelectedIndex = _agentStudio is null ? -1 : all.FindIndex(kv => kv.Key == _agentStudio) };
        var write = new CheckBox { Name = "agent-role-write", Content = "Darf nach Freigabe Dateien im Projekt speichern", Foreground = Text };
        var save = Act("agent-role-save", "Rolle anlegen", () =>
        {
            try
            {
                var sid = studio.SelectedIndex < 0 ? "" : all[studio.SelectedIndex].Key;
                var r = Agents.AddCustomRole(name.Text ?? "", does.Text ?? "", focus.Text ?? "", sid, write.IsChecked == true, id => StudioCatalog.For(id)?.Name);
                _agentNewRole = false; _agentStudio = sid; _agentRole = r.Id; _agentMsg = $"✓ Rolle „{r.Name}“ gehört jetzt zum Studio „{StudioCatalog.For(sid)?.Name}“.";
            }
            catch (ArgumentException ex) { _agentMsg = ex.Message; }
            Show("ki-rollen");
        }, true);
        return WCard(new StackPanel { Name = "agent-role-card", Spacing = 6, Children = { H("Eigene Rolle", 14), name, does, focus, studio, write, save } });
    }
}
