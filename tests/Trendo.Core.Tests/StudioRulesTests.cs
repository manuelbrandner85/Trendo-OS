using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// 0.9.41: Spielregeln, GAMEFORGE erkennen, selbstlernende Studio-Regeln (gelten erst nach Ja).
public class StudioRulesTests
{
    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-regeln-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    static void Plugin(string claudeDir, string version, params string[] skills)
    {
        var root = Path.Combine(claudeDir, "plugins", "cache", "quelle", "gameforge", version);
        foreach (var s in skills) { Directory.CreateDirectory(Path.Combine(root, "skills", s)); File.WriteAllText(Path.Combine(root, "skills", s, "SKILL.md"), "# " + s); File.WriteAllText(Path.Combine(root, "skills", s, "skript.py"), "x"); }
        Directory.CreateDirectory(Path.Combine(root, "agents")); File.WriteAllText(Path.Combine(root, "agents", "studio-director.md"), "a");
    }

    [Fact(DisplayName = "Spielregeln: neutral (ohne persönliche Projekte), mit Statusleiter, Engine nach Größe, Konventionen, Kanon und Sicherung")]
    public void Game_rules()
    {
        Assert.Contains("Idee → geplant → umgesetzt", GameRules.Text); Assert.Contains("Unreal Engine 5", GameRules.Text); Assert.Contains("Godot 4", GameRules.Text);
        Assert.Contains("1 Einheit = 1 cm", GameRules.Text); Assert.Contains("Kanon", GameRules.Text);
        foreach (var personal in new[] { "GENESIS", "AION", "Manuel", "GAMEFORGE" }) Assert.DoesNotContain(personal, GameRules.Text);
    }

    [Fact(DisplayName = "GAMEFORGE: neueste Version in den Claude-Ordnern finden, Regel nennt Skills und Agenten, Skills als Projekt-Skills (nur Texte)")]
    public void Find_gameforge()
    {
        var a = Temp(); var b = Temp(); var project = Temp();
        try
        {
            Assert.Null(GameForge.Find([a, null]));
            Plugin(a, "3.2.0", "gameforge-studio"); Plugin(b, "3.10.0", "gameforge-studio", "neues-spiel", "spielbibel");
            var gf = GameForge.Find([a, b])!;
            Assert.Equal("3.10.0", gf.Version); Assert.Equal(["gameforge-studio", "neues-spiel", "spielbibel"], gf.Skills); Assert.Equal(["studio-director"], gf.Agents);
            var rule = GameForge.Rule(gf);
            Assert.Contains("„gameforge-studio“", rule); Assert.Contains("„neues-spiel“", rule); Assert.Contains("gameforge:studio-director", rule); Assert.Contains("Programm „Spiele bauen“", rule);
            Assert.Equal(3, GameForge.ExportSkills(gf, project));
            Assert.True(File.Exists(Path.Combine(project, ".claude", "skills", "gameforge-spielbibel", "SKILL.md")));
            Assert.False(File.Exists(Path.Combine(project, ".claude", "skills", "gameforge-spielbibel", "skript.py"))); // keine Skripte
        }
        finally { foreach (var d in new[] { a, b, project }) Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Selbstlernend: „Lernregel:“ wird Vorschlag, gilt erst nach Ja; abgelehnte kommen nicht wieder; zurücknehmen; Herkunft aus dem Zusammenhang")]
    public void Learning()
    {
        var root = Temp();
        try
        {
            var l = new StudioLearning(root);
            var answer = "Fertig.\nLernregel: Immer zuerst ein spielbares Level mit Platzhaltern bauen\n**Lernregel:** Kurz";
            Assert.Equal(["Immer zuerst ein spielbares Level mit Platzhaltern bauen", "Kurz"], StudioLearning.Extract(answer));
            Assert.Equal("Fertig.", StudioLearning.Strip(answer));
            var r = l.Propose("CORE-06", StudioLearning.Extract(answer)[0], "Claudes Erkenntnis", "Frontlinie")!;
            Assert.Null(l.Propose("CORE-06", "Kurz", "x")); // zu kurz
            Assert.Null(l.Propose("CORE-06", "immer zuerst ein spielbares Level mit Platzhaltern bauen!", "x")); // schon vorgeschlagen
            Assert.Single(l.Pending("CORE-06")); Assert.Empty(l.Active("CORE-06"));
            Assert.DoesNotContain("spielbares Level", l.Prompt("CORE-06")); // gilt noch nicht
            l.Approve("CORE-06", r.Id);
            Assert.Contains("- Immer zuerst ein spielbares Level mit Platzhaltern bauen", l.Prompt("CORE-06")); Assert.Contains(StudioLearning.Mark, l.Prompt("CORE-06"));
            Assert.Empty(l.Active("CORE-07")); // je Studio getrennt
            var n = l.Propose("CORE-06", "Nie Gegner ohne Trefferzonen bauen", "Gutachter")!; l.Reject("CORE-06", n.Id);
            Assert.Null(l.Propose("CORE-06", "Nie Gegner ohne Trefferzonen bauen", "Gutachter")); // abgelehnt bleibt abgelehnt
            l.Retract("CORE-06", r.Id); Assert.Empty(l.Active("CORE-06"));
            Assert.Equal("Gutachter", StudioLearning.SourceOf(true, false, null));
            Assert.Equal("Prüfung (behobener Fehler)", StudioLearning.SourceOf(false, true, null));
            Assert.Equal("Deine Korrektur", StudioLearning.SourceOf(false, false, "Nein, mach die Gegner nie so schnell"));
            Assert.Equal("Claudes Erkenntnis", StudioLearning.SourceOf(false, false, "Baue ein Rennspiel"));
        }
        finally { Directory.Delete(root, true); }
    }
}
