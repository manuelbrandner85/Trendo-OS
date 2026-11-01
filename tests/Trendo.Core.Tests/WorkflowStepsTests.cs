using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// Ablauf-Editor: Probelauf meldet jeden Schritt vorher (Baustein leuchtet) und liefert ein Ergebnis je Schritt – Anzahl, „nichts zu tun“, Fehler bricht ab.
public class WorkflowStepsTests
{
    [Fact(DisplayName = "Ablauf-Editor: Probelauf ruft vor jedem Schritt zurück, Ergebnis je Schritt mit Dateianzahl und „nichts zu tun“; nichts wird geändert")]
    public async Task Step_results()
    {
        var f = new Fixture(); var home = Path.Combine(f.Root, "home"); var dl = Path.Combine(home, "Downloads"); Directory.CreateDirectory(dl);
        foreach (var n in new[] { "a.pdf", "b.pdf", "c.txt" }) File.WriteAllText(Path.Combine(dl, n), n);
        var e = new WorkflowEngine(f.Root, f.Core.Audit, f.Core.Approvals, [home]);
        var w = e.Add(new Workflow { Name = "PDFs sammeln", Steps = [
            new() { Kind = StepKind.OrdnerAnlegen, To = Path.Combine(home, "PDF") },
            new() { Kind = StepKind.Kopieren, From = dl, To = Path.Combine(home, "PDF"), Pattern = "*.pdf" },
            new() { Kind = StepKind.Kopieren, From = dl, To = Path.Combine(home, "Bilder"), Pattern = "*.jpg" } ] });
        var seen = new List<int>();
        var r = await e.RunAsync(w, dryRun: true, onStep: i => { seen.Add(i); return Task.CompletedTask; });
        Assert.Equal([0, 1, 2], seen);
        Assert.Equal(3, r.Steps.Count); Assert.All(r.Steps, s => Assert.True(s.Ok));
        Assert.Equal(1, r.Steps[0].Count); Assert.StartsWith("Ordner anlegen", r.Steps[0].Summary);
        Assert.Equal((2, "2 Dateien"), (r.Steps[1].Count, r.Steps[1].Summary));
        Assert.Equal((0, "nichts zu tun"), (r.Steps[2].Count, r.Steps[2].Summary));
        Assert.Equal(r.Steps[1].First + 2, r.Steps[2].First);
        Assert.False(Directory.Exists(Path.Combine(home, "PDF"))); // Probelauf ändert nichts
    }

    [Fact(DisplayName = "Ablauf-Editor: Schritt außerhalb der erlaubten Ordner wird rot, danach läuft nichts mehr")]
    public async Task Failure_stops()
    {
        var f = new Fixture(); var home = Path.Combine(f.Root, "home"); Directory.CreateDirectory(home);
        var e = new WorkflowEngine(f.Root, f.Core.Audit, f.Core.Approvals, [home]);
        var w = e.Add(new Workflow { Name = "Falsch", Steps = [
            new() { Kind = StepKind.OrdnerAnlegen, To = Path.Combine(home, "ok") },
            new() { Kind = StepKind.OrdnerAnlegen, To = Path.Combine(f.Root, "fremd") },
            new() { Kind = StepKind.OrdnerAnlegen, To = Path.Combine(home, "nie") } ] });
        var r = await e.RunAsync(w, dryRun: true);
        Assert.False(r.Ok); Assert.Equal(2, r.Steps.Count);
        Assert.True(r.Steps[0].Ok); Assert.False(r.Steps[1].Ok); Assert.Contains("nicht erlaubt", r.Steps[1].Summary);
    }
}
