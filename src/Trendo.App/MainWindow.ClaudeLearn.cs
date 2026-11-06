using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Spiele mit GAMEFORGE-Regeln und selbstlernende Studio-Regeln (Wunsch 02.11.2026): im Spiele-Studio eingebaute Spielregeln für alle, GAMEFORGE wenn installiert;
/// in allen Studios lernt Trendo aus jedem Projekt – neue Regeln gelten erst nach deinem Ja.</summary>
public partial class MainWindow
{
    StudioLearning CLearn => new(_core.DataRoot);

    /// <summary>Für Tests: wo nach GAMEFORGE gesucht wird (statt der echten Claude-Ordner des PCs).</summary>
    public static Func<IEnumerable<string?>>? GameForgeDirs { get; set; }

    /// <summary>GAMEFORGE auf diesem PC (Claude-Ordner der Person und der normale Claude-Ordner) – im Testmodus nie der echte PC.</summary>
    GameForge.Install? FindGameForge() => GameForge.Find(GameForgeDirs is { } o ? o()
        : _opt.Isolated ? [] : [ClaudeCode.PersonConfigDir(_core.DataRoot), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude")]);

    /// <summary>Studio-Regeln für den Lauf: Spielregeln (+ GAMEFORGE) im Spiele-Studio, gelernte Regeln in jedem Studio. Im sicheren Modus kommen die GAMEFORGE-Skills als Projekt-Skills mit.</summary>
    string ClaudeStudioRules(ClaudeDock dock, string folder, bool full)
    {
        var studio = ChatStudio(dock);
        var text = "";
        if (dock.Studio == GameRules.Studio)
        {
            text += GameRules.Text;
            if (FindGameForge() is { } gf)
            {
                text += GameForge.Rule(gf);
                if (!full) GameForge.ExportSkills(gf, folder); // im sicheren Modus lädt Claude keine Plugins – die Regeln gehen als Projekt-Skills mit
            }
        }
        if (dock.Studio == WebRules.Studio) { text += WebRules.Text; try { WebRules.Prepare(folder); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } } // lokale Bibliotheken für hochwertige Seiten
        if (dock.Studio == Model3DRules.Studio) text += Model3DRules.Text;
        if (dock.Studio == (ModuleOf("ablaeufe") ?? "CORE-02")) text += Trendo.Core.Automation.N8nRules.Text; // n8n-Abläufe als Datei zur Prüfung
        return text + CLearn.Prompt(studio);
    }

    /// <summary>„Lernregel: …“-Zeilen aus einer Antwort als Vorschläge merken (Herkunft aus dem Zusammenhang). Gibt die neuen Vorschläge zurück.</summary>
    IReadOnlyList<StudioLearning.Rule> ClaudeLearnFrom(string studio, string answer, bool review, bool check, string? userText, string project)
    {
        var list = new List<StudioLearning.Rule>();
        foreach (var t in StudioLearning.Extract(answer))
            if (CLearn.Propose(studio, t, StudioLearning.SourceOf(review, check, userText), project) is { } r) { list.Add(r); _core.Audit.Append(Principal.LocalOwner.Name, "studio.regel.vorschlag", studio); }
        return list;
    }

    /// <summary>Vorschläge im Chat: „Neue Regel für alle Projekte in …?“ – Ja oder Nein.</summary>
    void AddLearnProposals(ClaudeDock dock, ChatUi ui)
    {
        var studio = ChatStudio(dock); var name = StudioCatalog.NameOf(dock.Studio ?? "", "Trendo");
        var pending = CLearn.Pending(studio);
        for (var i = 0; i < pending.Count; i++)
        {
            var r = pending[i];
            var yes = new Button { Name = $"claude-learn-yes-{i}", Content = "✓ Ja, gilt ab jetzt", FontSize = 12, Padding = new Thickness(12, 4), Margin = new Thickness(0, 4, 6, 0), Background = ClaudeAccent, Foreground = Brushes.White, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(0) };
            var no = new Button { Name = $"claude-learn-no-{i}", Content = "Nein", FontSize = 12, Padding = new Thickness(12, 4), Margin = new Thickness(0, 4, 0, 0), Background = Brushes.Transparent, Foreground = ClaudeFg, BorderBrush = GlassEdge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
            yes.Click += (_, _) => { CLearn.Approve(studio, r.Id); _core.Audit.Append(Principal.LocalOwner.Name, "studio.regel.an", studio); Toast($"Neue Regel für „{name}“ gilt ab jetzt für alle Projekte."); RenderChat(dock); };
            no.Click += (_, _) => { CLearn.Reject(studio, r.Id); _core.Audit.Append(Principal.LocalOwner.Name, "studio.regel.nein", studio); RenderChat(dock); };
            ui.Log.Children.Add(Appear(CGlass(new StackPanel { Spacing = 4, Children = {
                new TextBlock { Text = $"🎓 Neue Regel für alle Projekte in „{name}“?", Foreground = CGold, FontSize = 13.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Name = $"claude-learn-text-{i}", Text = r.Text, Foreground = ClaudeFg, FontSize = 13.5, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = $"Gelernt aus: {r.Source}" + (r.Project is { Length: > 0 } p ? $" · Projekt „{p}“" : ""), Foreground = ClaudeMuted, FontSize = 11 },
                new StackPanel { Orientation = Orientation.Horizontal, Children = { yes, no } } } }, name: "claude-learn")));
        }
    }

    /// <summary>Feld „📚 Regeln“: GAMEFORGE-Stand (Spiele-Studio), eingebaute Regeln, gelernte Regeln mit Herkunft – jede zurücknehmbar.</summary>
    Control RulesPanel(ClaudeDock dock)
    {
        var studio = ChatStudio(dock); var name = StudioCatalog.NameOf(dock.Studio ?? "", "Trendo");
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(new TextBlock { Text = $"📚 Regeln in „{name}“", Foreground = CGold, FontSize = 15, FontWeight = FontWeight.SemiBold });
        if (dock.Studio == GameRules.Studio)
        {
            var gf = FindGameForge();
            sp.Children.Add(new TextBlock { Name = "claude-rules-gameforge", Text = gf is null ? "GAMEFORGE ist auf diesem PC nicht eingerichtet – es gelten die eingebauten Spielregeln von Trendo." : $"✓ GAMEFORGE {gf.Version} erkannt: {gf.Skills.Count} Skills, {gf.Agents.Count} Agenten – Claude arbeitet hier nach seinen Regeln.", Foreground = gf is null ? ClaudeMuted : ClaudeFg, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
            sp.Children.Add(new Expander { Header = "Eingebaute Spielregeln", Foreground = ClaudeMuted, Background = Brushes.Transparent, Content = new TextBlock { Text = GameRules.Text.Trim(), Foreground = ClaudeFg, FontSize = 12, TextWrapping = TextWrapping.Wrap } });
        }
        var active = CLearn.Active(studio);
        sp.Children.Add(new TextBlock { Text = active.Count == 0 ? "Noch keine gelernten Regeln. Trendo schlägt welche vor, sobald es aus deinen Projekten etwas lernt – sie gelten erst nach deinem Ja." : $"Gelernte Regeln ({active.Count}) – gelten für alle Projekte in diesem Studio:", Foreground = ClaudeMuted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        for (var i = 0; i < active.Count; i++)
        {
            var r = active[i];
            var x = new Button { Name = $"claude-learn-retract-{i}", Content = "Zurücknehmen", FontSize = 11, Padding = new Thickness(8, 2), Background = Brushes.Transparent, Foreground = ClaudeMuted, BorderBrush = ClaudeLine, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), VerticalAlignment = VerticalAlignment.Top };
            x.Click += (_, _) => { CLearn.Retract(studio, r.Id); _core.Audit.Append(Principal.LocalOwner.Name, "studio.regel.zurueck", studio); RenderChat(dock); };
            sp.Children.Add(new DockPanel { Children = { Dk(x, Avalonia.Controls.Dock.Right), new StackPanel { Children = {
                new TextBlock { Text = "• " + r.Text, Foreground = ClaudeFg, FontSize = 13, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = $"{r.Source} · seit {r.Decided?.LocalDateTime:dd.MM.yyyy}", Foreground = ClaudeMuted, FontSize = 10.5 } } } } });
        }
        var gone = CLearn.All(studio).Count(r => r.Status is StudioLearning.State.Abgelehnt or StudioLearning.State.Zurueckgenommen);
        if (gone > 0) sp.Children.Add(new TextBlock { Text = $"Verlauf: {gone} Vorschläge abgelehnt oder zurückgenommen – sie werden nicht noch einmal vorgeschlagen.", Foreground = ClaudeMuted, FontSize = 11, TextWrapping = TextWrapping.Wrap });
        return Appear(CGlass(sp, name: "claude-rules"));
    }

    /// <summary>Für Tests.</summary>
    public IReadOnlyList<StudioLearning.Rule> ClaudeStudioRulesFor(string studio, bool pending = false) => pending ? CLearn.Pending(studio) : CLearn.Active(studio);
}
