using System.Text.RegularExpressions;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0777 (Q4 der Neubewertung T-0769, Bericht 3.7): Echtlauf-Tests, deren Voraussetzung fehlt (Belegordner, Netz, Werkzeug), melden
/// „übersprungen“ (App: <c>Assert.Skip</c>, Kern: <see cref="Ueberspringen.Weil"/>) – sie kehren nicht still mit <c>return</c> zurück und schlucken keine Netzfehler.
/// Diese Prüfung liest die Testquellen und hält das fest.</summary>
public class T0777StilleTests
{
    static string Repo() { var d = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(d, "Trendo.sln"))) d = Path.GetDirectoryName(d)!; return d; }

    static IEnumerable<string> Sources() => Directory.EnumerateFiles(Path.Combine(Repo(), "tests"), "*.cs", SearchOption.AllDirectories)
        .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(p => p is "obj" or "bin")).OrderBy(f => f, StringComparer.Ordinal);

    // Anfang eines Tests: Attribut, Kopf, die ersten fünf Zeilen des Rumpfs
    static readonly Regex TestHead = new(@"\[(?:Avalonia)?Fact\([^\n]*\]\s*\n\s*public [^\n]*\)\s*\n\s*\{\s*\n((?:[^\n]*\n){0,5})");
    // Umgebungs-Schalter eines Echtlaufs (Variable, Belegordner) oder fehlende Aufgabenplanung, danach still zurück
    static readonly Regex SilentGate = new(@"(is not \{ Length: > 0 \} \w+|(?:Beleg|Dir)\([^)]*\) is not \{ \} \w+|WorkflowTaskPlan\.Supported)\)\s*return;");
    // T-0777 Nachbesserung: „übersprungen“ nur in die Testausgabe schreiben und zurückkehren zählt als bestanden – überall im Test verboten
    static readonly Regex LogAndReturn = new(@"\{\s*\w+\.WriteLine\([^;]*\);\s*return;\s*\}");

    [Fact(DisplayName = "T-0777 Q4: kein Test schreibt „übersprungen/BLOCKIERT/NICHT GETESTET“ nur in die Ausgabe und kehrt still zurück")]
    public void No_log_and_return()
    {
        var bad = Sources().SelectMany(f => File.ReadAllLines(f).Select((l, i) => (l, i)).Where(x => LogAndReturn.IsMatch(x.l)).Select(x => $"{Path.GetFileName(f)}:{x.i + 1}: {x.l.Trim()}")).ToList();
        Assert.True(bad.Count == 0, "still bestehende Tests:\n" + string.Join("\n", bad));
    }
    static readonly Regex SwallowedNet = new(@"catch \((?:HttpRequestException|TaskCanceledException)\) \{ \}");
    // Ausnahme mit Grund: ein zu großer Upload darf die Verbindung beenden statt 413 zu melden; danach prüft der Test, dass keine Datei entstand
    static readonly string[] SwallowAllowed = ["PhoneSendT0767Tests.cs"];

    [Fact(DisplayName = "T-0777 Q4: kein Echtlauf-Test kehrt ohne Belegordner still zurück – er meldet „übersprungen“")]
    public void No_silent_env_gate()
    {
        var bad = new List<string>();
        foreach (var f in Sources())
        {
            var t = File.ReadAllText(f).Replace("\r\n", "\n");
            foreach (Match m in TestHead.Matches(t))
                foreach (var line in m.Groups[1].Value.Split('\n'))
                    if (SilentGate.IsMatch(line)) bad.Add($"{Path.GetFileName(f)}:{t[..m.Index].Count(c => c == '\n') + 1}: {line.Trim()}");
        }
        Assert.True(bad.Count == 0, "still bestehende Echtlauf-Tests:\n" + string.Join("\n", bad));
    }

    [Fact(DisplayName = "T-0777 Q4: Netz-Tests schlucken Verbindungsfehler nicht – ohne Netz melden sie „übersprungen“")]
    public void No_swallowed_network_errors()
    {
        var bad = Sources().Where(f => !SwallowAllowed.Contains(Path.GetFileName(f)))
            .SelectMany(f => File.ReadAllLines(f).Select((l, i) => (l, i)).Where(x => SwallowedNet.IsMatch(x.l)).Select(x => $"{Path.GetFileName(f)}:{x.i + 1}: {x.l.Trim()}")).ToList();
        Assert.True(bad.Count == 0, "geschluckte Netzfehler:\n" + string.Join("\n", bad));
    }
}
