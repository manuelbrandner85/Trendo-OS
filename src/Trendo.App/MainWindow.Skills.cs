using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0317: Skills-Seite – mitgelieferte, eigene und geladene Fachregeln; eigene Skills mit Versionen (PromptLibrary);
/// Laden aus der offiziellen Skill-Sammlung (nur nach Klick, mit Lizenz und Herkunft); Verdrahtung in Assistent und „In Claude Code öffnen“.</summary>
public partial class MainWindow
{
    SkillLibrary? _skills;
    public SkillLibrary Skills => _skills ??= new SkillLibrary(_core.DataRoot, Prompts, _core.Audit);
    /// <summary>Vertraute Quellen für Skills (GitHub-Projekt, Ordner). Nur Text (SKILL.md) wird übernommen.</summary>
    public static readonly (string Repo, string Name)[] SkillSources = [("anthropics/skills", "Offizielle Skill-Sammlung von Anthropic")];

    string? _skillSel; bool _skillEdit; string _skillMsg = "";
    readonly List<(string Path, string Name)> _skillRemote = []; bool _skillRemoteBusy;

    void InitSkills()
    {
        _showMap["ki-skills"] = SkillsView; KiTabs.Add(("ki-skills", "Skills"));
        PageModules["ki-skills"] = "CORE-12";
        ExtraPages.Add(("Skills", "ki-skills", ["skill", "skills", "fachregeln", "checkliste", "qualität", "stil"]));
        SkillTextFor = key => Skills.PromptText(key, ModuleOf(key));
    }

    /// <summary>Checkliste der passenden Skills für eine Seite (für die Prüfung im Assistenten).</summary>
    public IReadOnlyList<(string Skill, string Item)> SkillChecklist(string key) => Skills.Checklist(key, ModuleOf(key));

    Control SkillsView()
    {
        var all = Skills.All();
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(P("Skills sind Fachregeln, nach denen die KI arbeitet – passend zum Programm (z. B. Webdesign, Brief nach DIN 5008). Vor „fertig“ prüft sie gegen die Checkliste. Eigene Skills haben Vorrang.", Muted, 12));
        var bar = new WrapPanel { Children = { Act("skill-new", "＋ Eigener Skill", () => { _skillSel = null; _skillEdit = true; Show("ki-skills"); }, true) } };
        sp.Children.Add(bar);
        if (_skillMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "skill-msg", Text = _skillMsg, Foreground = Text, TextWrapping = TextWrapping.Wrap });

        if (_skillEdit) sp.Children.Add(SkillEditor(_skillSel is null ? null : all.FirstOrDefault(s => s.Id == _skillSel)));

        var i = 0;
        foreach (var grp in all.GroupBy(s => s.Origin).OrderBy(g => g.Key == "eigen" ? 0 : g.Key == "geladen" ? 1 : 2))
        {
            sp.Children.Add(H(grp.Key switch { "eigen" => "Eigene Skills", "geladen" => "Geladene Skills", _ => "Mitgeliefert" }, 14));
            foreach (var s in grp)
            {
                var sk = s; var open = _skillSel == s.Id && !_skillEdit;
                var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
                void Add(Control c) { c.Margin = new Thickness(0, 0, 6, 0); buttons.Children.Add(c); }
                Add(Act($"skill-open-{i}", open ? "Zuklappen" : "Ansehen", () => { _skillSel = open ? null : sk.Id; _skillEdit = false; Show("ki-skills"); }));
                if (s.Origin == "eigen")
                {
                    Add(Act($"skill-edit-{i}", "Bearbeiten", () => { _skillSel = sk.Id; _skillEdit = true; Show("ki-skills"); }));
                    Add(RemoveButton("skill:" + s.Id, $"skill-del-{i}", () => { Skills.DeleteOwn(sk.Id); _skillMsg = $"„{sk.Name}“ gelöscht."; }));
                }
                if (s.Origin == "geladen") Add(RemoveButton("skill:" + s.Id, $"skill-del-{i}", () => { Skills.RemoveLoaded(sk.Id); _skillMsg = $"„{sk.Name}“ entfernt."; }));
                var info = $"{(s.Studios.Contains("*") ? "alle Programme" : string.Join(", ", s.Studios.Select(x => StudioCatalog.NameOf(x, x))))} · {s.Checklist.Count} Prüfpunkte" +
                    (s.License.Length > 0 ? " · Lizenz: " + s.License : "") + (s.Source.Length > 0 ? " · Quelle: " + s.Source : "");
                var card = new StackPanel { Spacing = 4, Children = {
                    Row(new StackPanel { Children = { new TextBlock { Text = s.Name, Foreground = Text, FontWeight = FontWeight.SemiBold }, P(s.Description, null, 11.5), P(info, Muted, 10.5) } }, buttons) } };
                if (open) card.Children.Add(new SelectableTextBlock { Name = "skill-body", Text = s.Body, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
                sp.Children.Add(Card(card));
                i++;
            }
        }

        // Vertraute Quellen
        sp.Children.Add(H("Skills aus vertrauten Quellen", 14));
        sp.Children.Add(P("Nur nach deinem Klick: Trendo lädt die Textdatei des Skills (SKILL.md), zeigt Lizenz und Herkunft und speichert beides mit. Programme oder Skripte aus den Sammlungen werden nie ausgeführt.", Muted, 11.5));
        foreach (var (repo, name) in SkillSources)
        {
            var r = repo;
            sp.Children.Add(new WrapPanel { Children = { P($"{name} ({repo})", Text, 12), Act("skill-src-list", _skillRemoteBusy ? "Lädt …" : "Liste holen", () => _ = SkillListRemote(r)) } });
        }
        var k = 0;
        foreach (var (path, name) in _skillRemote)
        {
            var pth = path;
            sp.Children.Add(new WrapPanel { Children = { P("• " + name, Text, 12), Act($"skill-src-get-{k++}", "Übernehmen", () => _ = SkillImportRemote(SkillSources[0].Repo, pth)) } });
        }
        return KiPage("Skills", sp);
    }

    Control SkillEditor(Skill? s)
    {
        var name = new TextBox { Name = "skill-name", Text = s?.Name ?? "", PlaceholderText = "Name, z. B. „Unser Hausstil“", Width = 420 };
        var desc = new TextBox { Name = "skill-desc", Text = s?.Description ?? "", PlaceholderText = "Wofür? (eine Zeile)", Width = 420 };
        var studios = new TextBox { Name = "skill-studios", Text = s is null ? ModuleOf(CurrentView) ?? "" : string.Join(", ", s.Studios), PlaceholderText = "Studios, z. B. CORE-07, STUDIO-LETTER-01 (leer = alle)", Width = 420 };
        var bodyText = s is null ? "" : System.Text.RegularExpressions.Regex.Replace(s.Body, @"\n## Checkliste[\s\S]*$", "").Trim();
        var body = new TextBox { Name = "skill-body-edit", Text = bodyText, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 160, PlaceholderText = "Fachregeln in Stichpunkten …" };
        var check = new TextBox { Name = "skill-check", Text = s is null ? "" : string.Join("\n", s.Checklist), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 80, PlaceholderText = "Checkliste – ein Prüfpunkt je Zeile" };
        var save = Act("skill-save", "Speichern", () =>
        {
            try
            {
                var saved = Skills.SaveOwn(s?.Id, name.Text ?? "", desc.Text ?? "", (studios.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    body.Text ?? "", (check.Text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                _skillMsg = $"✓ „{saved.Name}“ gespeichert (Version {Prompts.Get(saved.Id)?.Version})."; _skillSel = saved.Id; _skillEdit = false;
            }
            catch (ArgumentException e) { _skillMsg = e.Message; }
            Show("ki-skills");
        }, true);
        return Card(new StackPanel { Name = "skill-editor", Spacing = 6, Children = { H(s is null ? "Neuer Skill" : "Skill bearbeiten", 14), name, desc, studios, body, check,
            new WrapPanel { Children = { save, new Border { Width = 6 }, Act("skill-cancel", "Abbrechen", () => { _skillEdit = false; Show("ki-skills"); }) } } } });
    }

    /// <summary>Liste der Skills in der Sammlung (eine GitHub-Abfrage: Dateibaum, nur SKILL.md).</summary>
    public async Task SkillListRemote(string repo)
    {
        if (_core.Offline) { _skillMsg = "Trendo ist offline."; Show("ki-skills"); return; }
        _skillRemoteBusy = true; Show("ki-skills");
        try
        {
            var (files, _, _) = SourceApps.ParseTree(await GitHubJson(SourceApps.TreeUrl(repo), CancellationToken.None));
            _skillRemote.Clear();
            foreach (var f in files.Where(f => f.EndsWith("/SKILL.md", StringComparison.Ordinal)).OrderBy(f => f).Take(80))
                _skillRemote.Add((f, f.Split('/')[^2]));
            _skillMsg = _skillRemote.Count == 0 ? "In der Sammlung wurden keine Skills gefunden." : $"{_skillRemote.Count} Skills gefunden – „Übernehmen“ lädt nur die Textdatei.";
        }
        catch (Exception e) when (e is not OutOfMemoryException) { _skillMsg = "Liste ließ sich nicht laden: " + GitHubWhy(e); }
        finally { _skillRemoteBusy = false; Show("ki-skills"); }
    }

    /// <summary>Einen Skill übernehmen: SKILL.md und Lizenz (LICENSE im Skill-Ordner, sonst im Projekt) lesen, mit Herkunft speichern.</summary>
    public async Task SkillImportRemote(string repo, string path)
    {
        try
        {
            var md = await GitHubText(SourceApps.RawUrl(repo, path), CancellationToken.None);
            var dir = path.Contains('/') ? path[..path.LastIndexOf('/')] : "";
            string license;
            try { license = FirstLicenseLine(await GitHubText(SourceApps.RawUrl(repo, (dir.Length > 0 ? dir + "/" : "") + "LICENSE.txt"), CancellationToken.None)); }
            catch (Exception e) when (e is HttpRequestException or InvalidOperationException) { license = "siehe Projekt " + repo; }
            var s = Skills.Import(md, $"https://github.com/{repo}/blob/main/{path}", license);
            _skillMsg = $"✓ „{s.Name}“ übernommen · Lizenz: {s.License}. Er gilt für: {(s.Studios.Contains("*") ? "alle Programme – unter „Bearbeiten“ nicht änderbar; als eigenen Skill kopieren, um ihn zuzuordnen" : string.Join(", ", s.Studios))}.";
        }
        catch (Exception e) when (e is not OutOfMemoryException) { _skillMsg = "Übernehmen hat nicht geklappt: " + (e is InvalidDataException ? e.Message : GitHubWhy(e)); }
        Show("ki-skills");
    }

    static string FirstLicenseLine(string text) => text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) is { } l ? (l.Length > 80 ? l[..80] : l) : "unbekannt";
}
