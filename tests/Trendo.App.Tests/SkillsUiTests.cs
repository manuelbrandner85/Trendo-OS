using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// T-0317: Skills-Seite – mitgelieferte Skills ansehen, eigenen Skill anlegen (Version), Skill aus vertrauter Quelle mit Lizenz übernehmen;
/// Fachregeln gehen an die KI des Assistenten; „In Claude Code öffnen“ legt die Skills in den Projektordner.
[Collection("GitHubNetz")]
public class Skills_Ui
{
    sealed class FakeNet(Func<string, HttpResponseMessage> f) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) => Task.FromResult(f(r.RequestUri!.ToString()));
    }
    static HttpResponseMessage Ok(string s) => new(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8, "application/json") };

    [AvaloniaFact(DisplayName = "T-0317: Skills-Seite – ansehen, eigenen Skill anlegen und ändern (Version 2), passende Fachregeln gehen an die KI")]
    public void Page_and_own_skill()
    {
        var (w, _, _) = Ui.Open();
        w.Show("ki-skills");
        Assert.Contains(w.GetLogicalTexts(), t => t == "Webdesign auf Profi-Niveau");
        w.Find<Button>("skill-open-0").Press(); Assert.NotNull(w.TryFind<SelectableTextBlock>("skill-body"));
        w.Find<Button>("skill-new").Press();
        w.Find<TextBox>("skill-name").Text = "Unser Hausstil"; w.Find<TextBox>("skill-desc").Text = "Ton und Farben";
        w.Find<TextBox>("skill-studios").Text = "CORE-07"; w.Find<TextBox>("skill-body-edit").Text = "- Immer Du-Form\n- Akzentfarbe Petrol";
        w.Find<TextBox>("skill-check").Text = "Du-Form durchgehend\nPetrol als Akzent";
        w.Find<Button>("skill-save").Press();
        Assert.Contains("Version 1", w.Find<TextBlock>("skill-msg").Text);
        w.Find<Button>("skill-edit-0").Press(); w.Find<TextBox>("skill-body-edit").Text = "- Immer Du-Form\n- Akzentfarbe Petrol\n- Kurze Sätze"; w.Find<Button>("skill-save").Press();
        Assert.Contains("Version 2", w.Find<TextBlock>("skill-msg").Text);
        var text = w.SkillTextFor!("baukasten")!; // Baukasten gehört zu X-03 → Webdesign + Sauberer Code
        Assert.Contains("Webdesign auf Profi-Niveau", text);
        Assert.Contains("Du-Form durchgehend", w.Skills.PromptText(null, "CORE-07"));
    }

    [AvaloniaFact(DisplayName = "T-0317: Vertraute Quelle – Liste nur der SKILL.md-Dateien, Übernehmen speichert Text mit Lizenz und Herkunft (nichts wird ausgeführt)")]
    public async Task Trusted_source()
    {
        var (w, _, _) = Ui.Open();
        MainWindow.GitHubHandlerOverride = new FakeNet(u =>
            u.Contains("/git/trees/") ? Ok("""{"sha":"s1","tree":[{"path":"skills/frontend-design/SKILL.md","type":"blob"},{"path":"skills/frontend-design/LICENSE.txt","type":"blob"},{"path":"skills/frontend-design/script.py","type":"blob"},{"path":"README.md","type":"blob"}]}""")
            : u.EndsWith("/SKILL.md") ? Ok("---\nname: frontend-design\ndescription: Distinctive production-grade frontend interfaces\n---\n\n# Frontend design\nAvoid generic layouts.\n")
            : u.EndsWith("/LICENSE.txt") ? Ok("Apache License\nVersion 2.0") : new HttpResponseMessage(HttpStatusCode.NotFound));
        try
        {
            w.Show("ki-skills");
            await w.SkillListRemote("anthropics/skills");
            Assert.Contains(w.GetLogicalTexts(), t => t == "• frontend-design");
            Assert.DoesNotContain(w.GetLogicalTexts(), t => t.Contains("script.py"));
            await w.SkillImportRemote("anthropics/skills", "skills/frontend-design/SKILL.md");
            var s = Assert.Single(w.Skills.All(), x => x.Name == "frontend-design");
            Assert.Equal(("geladen", "Apache License", "https://github.com/anthropics/skills/blob/main/skills/frontend-design/SKILL.md"), (s.Origin, s.License, s.Source));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "skill.import");
        }
        finally { MainWindow.GitHubHandlerOverride = null; }
    }

    [AvaloniaFact(DisplayName = "T-0317: „In Claude Code öffnen“ legt die passenden Skills als .claude/skills/trendo-…/SKILL.md in den Ordner und nennt die Checklisten-Prüfung")]
    public async Task Claude_open_exports()
    {
        var (w, _, root) = Ui.Open();
        (string Folder, IReadOnlyList<string> Args)? opened = null;
        MainWindow.ClaudeOpenOverride = (f, a) => opened = (f, a);
        MainWindow.ClaudeStatusOverride = () => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", "c"));
        try
        {
            await w.ClaudeCheck(true);
            var dir = Path.Combine(root, "web-projekt");
            w.ClaudeOpen(dir, "Startseite bauen", w.ClaudeRoleFor("CORE-07"), "CORE-07");
            Assert.True(File.Exists(Path.Combine(dir, ".claude", "skills", "trendo-webdesign", "SKILL.md")));
            Assert.Contains("prüfe dein Ergebnis vor dem Abschluss gegen deren Checklisten", opened!.Value.Args[1]);
        }
        finally { MainWindow.ClaudeOpenOverride = null; MainWindow.ClaudeStatusOverride = null; }
    }
}
