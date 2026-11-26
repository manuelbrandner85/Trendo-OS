using Trendo.Core.PromptAction;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0781: KI-Aufträge ohne offenes Dokument für Mathe-Helfer und Naturwissenschaft – echter Kern, Markdown mit Lösungsweg und Probe, keine Nebenwirkung.</summary>
public class WissenAdapterTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-wissen-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    static string Lauf(string studio, string prompt, string datei)
    {
        var root = Tmp();
        try
        {
            var all = PromptActions.CreateDefault();
            var core = new TrendoCore(Path.Combine(root, "daten"), new InMemorySecretStore(), offline: true);
            var project = core.Projects.Create("probe", "Probe").Id;
            var wf = new StudioWorkflow(core, all); var ctx = new StudioExecutionContext(project, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default);
            var req = new StudioPromptRequest("wissen-1", project, studio, prompt, [], "de", null, PrivacyMode.Local, new Provenance("test", null, null));
            var start = wf.Start(req, ctx);
            Assert.True(start.Errors.Count == 0 && start.Questions.Count == 0, $"Start {start.Phase}: {string.Join(" | ", start.Errors)} | {string.Join(" | ", start.Questions.Select(q => q.Question))}");
            var done = wf.Los(project, start.RunId, ctx);
            Assert.True(done.Status == RunStatus.Verified, $"{done.Status}: {string.Join(" | ", done.Errors)} | {string.Join(" | ", done.Questions.Select(q => q.Question))}");
            return File.ReadAllText(core.Projects.ResolveInProject(project, done.Artifacts.Single(a => a.RelativePath.EndsWith(datei)).RelativePath));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0781 Adapter Mathe: „Aufgabe: …“ → Lösungsweg mit Probe als Markdown, vom Kern gerechnet; unverständliche Aufgabe wird geprüft abgelehnt")]
    public void Mathe()
    {
        var all = PromptActions.CreateDefault(); var ad = all.Single(a => a.StudioId == "STUDIO-MATH-01");
        Assert.False(ad.CanPerformSideEffect); Assert.Equal(SideEffectClass.None, ad.Effect);
        Assert.NotEmpty(ad.Missing(new Dictionary<string, string>()));
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["aufgabe"] = "(x + 1" }));
        Assert.Equal(CapabilityLevel.Partial, CapabilityCatalog.Level("STUDIO-MATH-01", all));
        var md = Lauf("STUDIO-MATH-01", "Aufgabe: 2x + 3y = 12; x - y = 1\nArt: system", "loesungsweg.md");
        Assert.Contains("x = 3;  y = 2", md); Assert.Contains("Additionsverfahren", md); Assert.Contains("✓ Gleichung I", md);
    }

    [Fact(DisplayName = "T-0781 Adapter Naturwissenschaft: Reaktion ausgleichen und Molmasse als Markdown mit Rechenweg und Probe")]
    public void Natur()
    {
        var all = PromptActions.CreateDefault(); var ad = all.Single(a => a.StudioId == "STUDIO-SCIENCE-01");
        Assert.False(ad.CanPerformSideEffect);
        Assert.Equal(2, ad.Missing(new Dictionary<string, string>()).Count);
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["art"] = "reaktion", ["eingabe"] = "H2O -> CO2" }));
        Assert.NotEmpty(ad.Validate(new Dictionary<string, string> { ["art"] = "zaubern", ["eingabe"] = "x" }));
        var md = Lauf("STUDIO-SCIENCE-01", "Art: reaktion\nEingabe: Al + HCl -> AlCl3 + H2", "rechnung.md");
        Assert.Contains("2 Al + 6 HCl → 2 AlCl₃ + 3 H₂", md); Assert.Contains("✓ Cl: links 6, rechts 6", md);
        Assert.Contains("M = 98,072 g/mol", Lauf("STUDIO-SCIENCE-01", "Art: molmasse\nEingabe: H2SO4", "rechnung.md"));
    }
}
