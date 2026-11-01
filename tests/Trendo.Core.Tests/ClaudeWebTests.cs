using System.IO.Compression;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// Claude im Web mit Trendo verbinden: Anmeldung erkennen, je Studio eigener Chat, erster Auftrag mit Rolle und Fachregeln, Skill-Paket zum Hochladen.
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

    [Fact(DisplayName = "Claude-Verbindung: jedes Studio merkt sich seinen Chat; neuer Chat vergisst ihn; erster Auftrag mit Rolle und Fachregeln, danach nur der Auftrag")]
    public void Studio_chats_and_brief()
    {
        var w = new ClaudeWeb(Root());
        w.Navigated(new Uri("https://claude.ai/new"), null);
        Assert.True(w.Navigated(new Uri("https://claude.ai/chat/0f3a9c2e-1111-2222-3333-444455556666"), "CORE-07"));
        w.Navigated(new Uri("https://claude.ai/chat/aaaabbbb-1111-2222-3333-444455556666"), "CORE-06");
        Assert.Equal("https://claude.ai/chat/0f3a9c2e-1111-2222-3333-444455556666", w.StartUrl("CORE-07"));
        Assert.Equal("https://claude.ai/chat/aaaabbbb-1111-2222-3333-444455556666", new ClaudeWeb(Path.GetDirectoryName(w.Profile)!).StartUrl("CORE-06")); // bleibt gespeichert
        var first = w.Message("CORE-07", "Webseiten bauen", "Du baust barrierearme Webseiten.", [Sk("web", "Webdesign")], "Baue eine Kontaktseite");
        Assert.StartsWith("[Trendo OS · Studio „Webseiten bauen“]", first);
        Assert.Contains("## Webdesign", first); Assert.Contains("Checkliste: Titel gesetzt; Alt-Texte", first); Assert.EndsWith("Mein Auftrag: Baue eine Kontaktseite", first);
        Assert.Equal("Und jetzt die Farben", w.Message("CORE-07", "Webseiten bauen", "x", [], "Und jetzt die Farben"));
        w.NewStudioChat("CORE-07"); Assert.Equal(ClaudeWeb.NewChat, w.StartUrl("CORE-07"));
        Assert.StartsWith("[Trendo OS", w.Message("CORE-07", "Webseiten bauen", "x", [], "Neu"));
        Assert.Null(ClaudeWeb.ChatId(new Uri("https://claude.ai/project/123")));
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
