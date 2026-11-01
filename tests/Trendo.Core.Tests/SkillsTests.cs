using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0317: Skills – mitgelieferte Fachregeln je Studio, eigene Skills mit Versionen (PromptLibrary), Laden aus vertrauten Quellen mit Herkunft und Lizenz,
/// Checkliste für die Qualitätsprüfung, Ablage als Claude-Code-Skill im Projektordner.
public class SkillsTests
{
    static (SkillLibrary Lib, PromptLibrary Prompts, string Root) New()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-skills-" + Guid.NewGuid().ToString("N"));
        var audit = new AuditLog(Path.Combine(root, "audit")); var prompts = new PromptLibrary(root, audit);
        return (new SkillLibrary(root, prompts, audit), prompts, root);
    }

    [Fact(DisplayName = "T-0317: Mitgelieferte Skills – neun Fachgebiete, jedes mit Beschreibung, Studios und mindestens fünf prüfbaren Checklisten-Punkten")]
    public void Built_in()
    {
        var all = SkillLibrary.BuiltIn;
        Assert.Equal(["brief-din5008", "code-qualitaet", "foto-bild", "praesentation", "spiele", "tabellen", "technik-cad", "texte-lektorat", "webdesign"], all.Select(s => s.Id).OrderBy(x => x));
        Assert.All(all, s => { Assert.True(s.Description.Length > 20, s.Id); Assert.NotEmpty(s.Studios); Assert.True(s.Checklist.Count >= 5, s.Id); Assert.True(s.BuiltIn); Assert.DoesNotContain("## Checkliste", s.Checklist); });
        var web = all.Single(s => s.Id == "webdesign");
        Assert.Contains("CORE-07", web.Studios); Assert.Contains(web.Checklist, c => c.Contains("4,5 : 1"));
    }

    [Fact(DisplayName = "T-0317: Passende Skills je Seite/Studio; Fachregeln für die KI gekürzt; Checkliste ohne Doppelte; eigene Skills zuerst")]
    public void For_and_prompt()
    {
        var (lib, _, root) = New();
        try
        {
            Assert.Equal(["Sauberer Code", "Webdesign auf Profi-Niveau"], lib.For("website", "CORE-07").Select(s => s.Name));
            Assert.Equal("brief-din5008", lib.For("brief", null).Single().Id); // auch über den Seitennamen
            Assert.Empty(lib.For("ki-fragen", "CORE-02"));
            var text = lib.PromptText(null, "CORE-07")!;
            Assert.Contains("### Skill: Webdesign auf Profi-Niveau", text); Assert.True(text.Length <= SkillLibrary.MaxPromptChars);
            Assert.Null(lib.PromptText(null, "CORE-02"));
            var own = lib.SaveOwn(null, "Unser Hausstil", "Farben und Ton von Erika Musters Laden", ["CORE-07"], "Immer Du-Form, Farbe Petrol.", ["Du-Form durchgehend", "Petrol als Akzent"]);
            Assert.Equal("Unser Hausstil", lib.For(null, "CORE-07")[0].Name); // eigene Regeln zuerst
            var check = lib.Checklist(null, "CORE-07");
            Assert.Equal(("Unser Hausstil", "Du-Form durchgehend"), check[0]);
            Assert.Equal(check.Count, check.Select(c => c.Item.ToLowerInvariant()).Distinct().Count());
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0317: Eigene Skills – anlegen, als neue Version ändern, wiederherstellen, löschen (über die vorhandene Prompt-Bibliothek)")]
    public void Own_skills_with_versions()
    {
        var (lib, prompts, root) = New();
        try
        {
            Assert.Throws<ArgumentException>(() => lib.SaveOwn(null, " ", "", [], "x", []));
            var s = lib.SaveOwn(null, "Rechnungen", "Rechnungen sauber", ["SPEC-11"], "Immer Rechnungsnummer und Leistungsdatum.", ["Rechnungsnummer vorhanden"]);
            lib.SaveOwn(s.Id, "Rechnungen", "Rechnungen sauber", ["SPEC-11"], "Immer Rechnungsnummer, Leistungsdatum und Steuernummer.", ["Rechnungsnummer vorhanden", "Steuernummer vorhanden"]);
            var t = prompts.Get(s.Id)!;
            Assert.Equal((2, SkillLibrary.Category), (t.Version, t.Category));
            Assert.Contains("Steuernummer", lib.For(null, "SPEC-11").Single(x => !x.BuiltIn).Body);
            prompts.Restore(s.Id, 1);
            Assert.DoesNotContain("Steuernummer", lib.For(null, "SPEC-11").Single(x => !x.BuiltIn).Body);
            Assert.True(lib.DeleteOwn(s.Id)); Assert.Empty(lib.For(null, "SPEC-11").Where(x => !x.BuiltIn));
            Assert.False(lib.DeleteOwn("einfach-erklaeren")); // normale Vorlage, kein Skill
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0317: Skill aus vertrauter Quelle – nur Text bis 100 KB, Herkunft und Lizenz bleiben am Skill, Audit; entfernbar")]
    public void Import_with_origin()
    {
        var (lib, _, root) = New();
        try
        {
            var md = "---\nname: Barrierefreie Formulare\ndescription: Formulare für alle\nstudios: CORE-07\n---\n\n# Formulare\n- Fehler am Feld\n\n## Checkliste\n- [ ] Jedes Feld hat ein label\n- [x] Fehlertext am Feld\n";
            var s = lib.Import(md, "https://github.com/beispiel/skills/blob/main/formulare/SKILL.md", "Apache-2.0");
            Assert.Equal(("geladen", "Apache-2.0", "https://github.com/beispiel/skills/blob/main/formulare/SKILL.md"), (s.Origin, s.License, s.Source));
            Assert.Equal(["Jedes Feld hat ein label", "Fehlertext am Feld"], s.Checklist);
            Assert.Contains(lib.All(), x => x.Name == "Barrierefreie Formulare" && x.License == "Apache-2.0");
            Assert.Throws<InvalidDataException>(() => lib.Import(new string('x', SkillLibrary.MaxImportBytes + 1), "u", "MIT"));
            Assert.True(lib.RemoveLoaded(s.Id)); Assert.DoesNotContain(lib.All(), x => x.Name == "Barrierefreie Formulare");
            Assert.False(lib.RemoveLoaded("..\\..\\etwas"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0317: Für Claude Code – passende Skills landen als .claude/skills/trendo-<id>/SKILL.md mit name und description im Projektordner")]
    public void Export_for_claude()
    {
        var (lib, _, root) = New();
        try
        {
            var proj = Path.Combine(root, "projekt");
            Assert.Equal(2, lib.ExportForClaude(proj, null, "CORE-07"));
            var f = File.ReadAllText(Path.Combine(proj, ".claude", "skills", "trendo-webdesign", "SKILL.md"));
            Assert.StartsWith("---\nname: webdesign\ndescription: Gestaltung", f);
            Assert.Contains("## Checkliste", f);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0317: Skill-Datei lesen – Kopf optional, Name aus der Überschrift, Studios-Liste, Checkliste endet an der nächsten Überschrift")]
    public void Parse()
    {
        var s = SkillFiles.Parse("# Meine Regeln\nText\n## Checkliste\n- Punkt A\n* Punkt B\n## Anhang\n- kein Punkt", "eigen");
        Assert.Equal(("Meine Regeln", "meine-regeln"), (s.Name, s.Id)); Assert.Equal(["*"], s.Studios); Assert.Equal(["Punkt A", "Punkt B"], s.Checklist);
        Assert.Equal("ueber-oeffnen", SkillFiles.Slug("Über öffnen!"));
        var md = SkillFiles.ToMarkdown("A", "B", ["X-04"], "Text", ["c1"], "MIT", "https://example.org");
        Assert.Equal(["c1"], SkillFiles.Parse(md, "geladen").Checklist); Assert.Contains("lizenz: MIT", md);
    }
}
