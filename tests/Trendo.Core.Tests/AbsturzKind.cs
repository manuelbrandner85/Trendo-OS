using System.Diagnostics;
using Trendo.Core;

namespace Trendo.Core.Tests;

/// <summary>T-0681: Einstieg dieser Test-DLL als eigener Kindprozess. Ein Test startet ihn, wartet auf die Marke „bereit“ und beendet ihn
/// dann hart (Kill) – so wird ein echter Absturz mitten im Schreiben geprüft, nicht nur ein Fehler im selben Prozess.
/// Aufruf: dotnet Trendo.Core.Tests.dll &lt;art&gt; &lt;ordner&gt; …  (unter „dotnet test“ wird dieser Einstieg nicht benutzt).</summary>
public static class AbsturzKind
{
    public const int Ablaeufe = 150;

    public static int Main(string[] args)
    {
        if (args.Length < 2) return 0;
        var root = args[1];
        switch (args[0])
        {
            case "ablauf-speichern":
            {
                var home = args[2];
                var core = new TrendoCore(Path.Combine(root, "kern"), new InMemorySecretStore(), new FakeLauncher());
                var e = new WorkflowEngine(root, core.Audit, core.Approvals, [home]);
                for (var i = e.All().Count; i < Ablaeufe; i++)
                    e.Add(new Workflow { Name = "Ablauf " + i, Trigger = TriggerKind.Manuell, Steps = [new() { Kind = StepKind.OrdnerAnlegen, To = Path.Combine(home, "ordner-" + i) }, new() { Kind = StepKind.TextSchreiben, To = Path.Combine(home, "notiz-" + i + ".txt"), Text = new string('x', 20000) }] }); // große Datei: das Schreiben dauert
                File.WriteAllText(Path.Combine(root, "bereit"), "");
                while (true) e.Remove("gibt-es-nicht"); // speichert die ganze Datei immer wieder, bis der Test den Prozess beendet
            }
            case "sicherung":
            {
                var core = new TrendoCore(Path.Combine(root, "kern"), new InMemorySecretStore(), new FakeLauncher());
                var b = new Backup(Path.Combine(root, "daten"), Path.Combine(root, "sicherungen"), core.Audit);
                File.WriteAllText(Path.Combine(root, "bereit"), "");
                while (true) b.Create(DateTime.Now, keep: 3);
            }
            default: return 2;
        }
    }

    /// <summary>Startet diese DLL als Kindprozess und wartet, bis er „bereit“ meldet.</summary>
    public static Process Start(string root, params string[] args)
    {
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } h && File.Exists(h) ? h : Cli.Find("dotnet") ?? throw new InvalidOperationException("dotnet nicht gefunden");
        var marke = Path.Combine(root, "bereit"); File.Delete(marke);
        var psi = new ProcessStartInfo(dotnet) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        psi.ArgumentList.Add(typeof(AbsturzKind).Assembly.Location);
        foreach (var a in args) psi.ArgumentList.Add(a);
        var p = Process.Start(psi)!;
        var sw = Stopwatch.StartNew();
        while (!File.Exists(marke))
        {
            if (p.HasExited) throw new InvalidOperationException("Kindprozess endete vorzeitig: " + p.StandardError.ReadToEnd());
            if (sw.Elapsed > TimeSpan.FromSeconds(60)) { p.Kill(true); throw new TimeoutException("Kindprozess wurde nicht bereit."); }
            Thread.Sleep(20);
        }
        return p;
    }

    /// <summary>Harter Abbruch wie bei einem Absturz oder Stromausfall des Programms.</summary>
    public static void Kill(Process p) { p.Kill(true); p.WaitForExit(10000); p.Dispose(); }
}
