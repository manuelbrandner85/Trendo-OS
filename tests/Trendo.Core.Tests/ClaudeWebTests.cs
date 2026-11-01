using System.IO.Compression;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// Claude im Web mit Trendo verbinden: Anmeldung erkennen, je Studio eigene Chats und Claude-Projekte, Anweisungen mit Rolle und Fachregeln, Skill-Paket zum Hochladen.
public class ClaudeWebTests
{
    static string Root() { var d = Path.Combine(Path.GetTempPath(), "trendo-claudeweb-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }
    static Skill Sk(string id, string name) => new(id, name, "", ["CORE-07"], "Mobil zuerst. Gute Kontraste.", ["Titel gesetzt", "Alt-Texte"], "trendo", "MIT", "");

    [Fact(DisplayName = "Claude-Verbindung: Anmeldeseiten zählen nicht, erste Seite danach = verbunden; fremde Seiten nie; Trennen vergisst alles")]
    public void Connect()
    {
        var w = new ClaudeWeb(Root());
        Assert.False(w.Connected); Assert.Equal(ClaudeWeb.Login, w.StartUrl("CORE-07"));
        foreach (var u in new[] { "https://claude.ai/login", "https://claude.ai/magic-link#x", "https://claude.ai/onboarding", "https://accounts.google.com/o/oauth2", "http://claude.ai/new", "https://claude.ai.boese.de/new" })
            Assert.False(w.Navigated(new Uri(u), null), u);
        Assert.False(w.Connected);
        Assert.True(w.Navigated(new Uri("https://claude.ai/new"), null)); Assert.True(w.Connected); Assert.NotNull(w.ConnectedAt);
        Assert.Equal(ClaudeWeb.NewChat, w.StartUrl("CORE-07"));
        w.Disconnect(); Assert.False(w.Connected);
    }

    [Fact(DisplayName = "Claude-Verbindung: jedes Studio merkt sich seinen Chat; neuer Chat vergisst ihn; Anweisungen mit Rolle, Fachregeln und Fachgebiet")]
    public void Studio_chats_and_brief()
    {
        var w = new ClaudeWeb(Root());
        w.Navigated(new Uri("https://claude.ai/new"), null);
        Assert.True(w.Navigated(new Uri("https://claude.ai/chat/0f3a9c2e-1111-2222-3333-444455556666"), "CORE-07"));
        w.Navigated(new Uri("https://claude.ai/chat/aaaabbbb-1111-2222-3333-444455556666"), "CORE-06");
        Assert.Equal("https://claude.ai/chat/0f3a9c2e-1111-2222-3333-444455556666", w.StartUrl("CORE-07"));
        Assert.Equal("https://claude.ai/chat/aaaabbbb-1111-2222-3333-444455556666", new ClaudeWeb(Path.GetDirectoryName(w.Profile)!).StartUrl("CORE-06")); // bleibt gespeichert
        var brief = ClaudeWeb.Brief("Webseiten bauen", "Du baust barrierearme Webseiten.", [Sk("web", "Webdesign")]);
        Assert.StartsWith("[Trendo OS · Studio „Webseiten bauen“]", brief);
        Assert.Contains("## Webdesign", brief); Assert.Contains("Checkliste: Titel gesetzt; Alt-Texte", brief); Assert.Contains("ausschließlich für das Studio", brief);
        w.NewStudioChat("CORE-07"); Assert.Equal(ClaudeWeb.NewChat, w.StartUrl("CORE-07"));
        Assert.Null(ClaudeWeb.ChatId(new Uri("https://claude.ai/project/123")));
    }

    [Fact(DisplayName = "Claude-Anmeldung: Google/Apple/SSO-Fenster bleiben in Trendo, andere Links nicht; Anmeldelink aus der E-Mail nur von claude.ai")]
    public void Auth_popups_and_login_link()
    {
        foreach (var u in new[] { "https://accounts.google.com/o/oauth2/v2/auth?x=1", "https://appleid.apple.com/auth/authorize", "https://claude.ai/login/sso", "https://console.anthropic.com/x" })
            Assert.True(ClaudeWeb.IsAuthPopup(new Uri(u)), u);
        foreach (var u in new[] { "https://www.google.com/search?q=x", "http://accounts.google.com/", "https://example.org/", "https://claude.ai.boese.de/" })
            Assert.False(ClaudeWeb.IsAuthPopup(new Uri(u)), u);
        Assert.Equal("https://claude.ai/magic-link#token123", ClaudeWeb.LoginLink("  https://claude.ai/magic-link#token123 ")!.AbsoluteUri);
        foreach (var t in new[] { null, "", "hallo", "http://claude.ai/magic-link#x", "https://claude.ai.boese.de/magic-link", "https://claude.ai/", "javascript:alert(1)" })
            Assert.Null(ClaudeWeb.LoginLink(t));
    }

    [Fact(DisplayName = "Claude-Projekte je Studio wie in Claude: neues Projekt bekommt den Namen aus Trendo, fremde Projekte nicht, fortsetzen, umbenennen, entfernen; Fachgebiet in den Anweisungen")]
    public void Studio_projects()
    {
        var w = new ClaudeWeb(Root());
        w.Navigated(new Uri("https://claude.ai/new"), null);
        Assert.False(w.Navigated(new Uri("https://claude.ai/project/eeeeeeee-1111-2222-3333-444455556666"), "CORE-06")); // fremdes Projekt kommt nicht von selbst in die Liste
        Assert.Empty(w.ProjectsOf("CORE-06"));
        w.BeginProject("CORE-06", "Mein Jump-and-Run"); Assert.Equal("Mein Jump-and-Run", w.PendingOf("CORE-06"));
        Assert.True(w.Navigated(new Uri("https://claude.ai/project/abcdef12-1111-2222-3333-444455556666"), "CORE-06"));
        Assert.Null(w.PendingOf("CORE-06"));
        Assert.Equal(["Mein Jump-and-Run"], w.ProjectsOf("CORE-06").Select(p => p.Name));
        Assert.Equal("https://claude.ai/project/abcdef12-1111-2222-3333-444455556666", w.StartUrl("CORE-06")); // fortsetzen
        w.BeginProject("CORE-06", "Rennspiel");
        w.Navigated(new Uri("https://claude.ai/project/bbbbbbbb-1111-2222-3333-444455556666"), "CORE-06");
        Assert.Equal(["Rennspiel", "Mein Jump-and-Run"], w.ProjectsOf("CORE-06").Select(p => p.Name)); // zuletzt geöffnet zuerst
        Assert.True(w.AddProject("CORE-06", new Uri("https://claude.ai/project/eeeeeeee-1111-2222-3333-444455556666"), "Altes Projekt"));
        Assert.False(w.AddProject("CORE-06", new Uri("https://claude.ai/new"), "x"));
        Assert.True(w.RenameProject("CORE-06", "eeeeeeee-1111-2222-3333-444455556666", "Alter Plattformer"));
        Assert.True(w.RemoveProject("CORE-06", "bbbbbbbb-1111-2222-3333-444455556666")); Assert.False(w.RemoveProject("CORE-06", "bbbbbbbb-1111-2222-3333-444455556666"));
        Assert.Equal(2, w.ProjectsOf("CORE-06").Count); Assert.Empty(w.ProjectsOf("CORE-07"));
        Assert.Null(ClaudeWeb.ProjectId(new Uri("https://boese.example/project/abcdef12-1111")));
        var instr = ClaudeWeb.ProjectInstructions("Spiele entwickeln", "Du entwickelst Spiele.", [Sk("game", "Spieldesign")]);
        Assert.Contains("ausschließlich für das Studio „Spiele entwickeln“", instr); Assert.Contains("lehne freundlich ab", instr); Assert.Contains("## Spieldesign", instr);
        Assert.Equal("Rennspiel · Trendo Spiele entwickeln", ClaudeWeb.ProjectName("Spiele entwickeln", "Rennspiel"));
        w.Disconnect(); Assert.Empty(w.ProjectsOf("CORE-06"));
    }

    [Fact(DisplayName = "Claude-Verbindung: Skill-Paket als ZIP mit SKILL.md je Fachregel; ohne Fachregeln klare Meldung")]
    public void Skill_package()
    {
        var zip = ClaudeWeb.SkillPackage([Sk("web", "Webdesign"), Sk("a11y", "Barrierefrei")], "CORE-07", Root());
        using (var z = ZipFile.OpenRead(zip)) Assert.Equal(["trendo-web/SKILL.md", "trendo-a11y/SKILL.md"], z.Entries.Select(e => e.FullName));
        Assert.EndsWith("trendo-skills-core-07.zip", zip);
        Assert.Throws<InvalidOperationException>(() => ClaudeWeb.SkillPackage([], "X-03", Root()));
    }
}
