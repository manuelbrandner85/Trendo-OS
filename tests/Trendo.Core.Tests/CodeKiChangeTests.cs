using System.Text.Json.Nodes;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0723 (D6 „Code mit KI“): Vorschlag der KI lesen, Projektgrenzen, Diff je Datei mit Ja/Nein je Änderung, Anwenden als ein Schritt, Rückgängig/Wiederholen.
public class CodeKiChangeTests
{
    static AuditLog Au13(string d) => new(d.TrimEnd(Path.DirectorySeparatorChar) + "-audit13.jsonl"); // T-0764 (13. Prüfrunde): Kern verlangt die Rolle – hier Erwachsene (unverändert)
    static string Proj()
    {
        var d = Path.Combine(Path.GetTempPath(), "trendo-t0723-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d);
        File.WriteAllText(Path.Combine(d, "rechner.py"), "def addiere(a, b):\n    return a + b\n\n\ndef teile(a, b):\n    return a / b\n");
        File.WriteAllText(Path.Combine(d, "test_rechner.py"), "import unittest\n");
        return d;
    }

    const string Answer = "<think>erst überlegen … DATEI: falsch.py</think>\nPLAN:\n- Funktion multipliziere ergänzen\n- Test schreiben\n\n**DATEI: `rechner.py`**\n```python\ndef addiere(a, b):\n    return a + b\n\n\ndef teile(a, b):\n    if b == 0:\n        raise ValueError(\"null\")\n    return a / b\n\n\ndef multipliziere(a, b):\n    return a * b\n```\n\nDATEI: tests/test_neu.py\n```\nimport unittest\n```\n";

    [Fact(DisplayName = "T-0723: Antwort der KI – Plan, DATEI-Blöcke mit Zaun, Denkteil ignoriert, abgeschnittener Block verworfen")]
    public void Parse()
    {
        var p = CodeKiChange.Parse(Answer);
        Assert.Equal(["Funktion multipliziere ergänzen", "Test schreiben"], p.Plan);
        Assert.Equal(["rechner.py", "tests/test_neu.py"], p.Files.Select(f => f.Path));
        Assert.Contains("def multipliziere(a, b):\n    return a * b\n", p.Files[0].Content);
        Assert.DoesNotContain("```", p.Files[0].Content);
        var cut = CodeKiChange.Parse("PLAN:\n- x\nDATEI: a.py\n```\nprint(1)\n"); // ohne schließenden Zaun
        Assert.Empty(cut.Files);
    }

    [Fact(DisplayName = "T-0723: Projektgrenzen – nur relative Pfade im Projekt, kein .., kein .git/node_modules, keine Zugangsdaten, keine Binärdateien")]
    public void Boundaries()
    {
        var d = Proj();
        try
        {
            Assert.Null(CodeKiChange.Blocked(d, "rechner.py"));
            Assert.Null(CodeKiChange.Blocked(d, "tests/neu.py"));
            Assert.NotNull(CodeKiChange.Blocked(d, "../draussen.py"));
            Assert.NotNull(CodeKiChange.Blocked(d, "a/../../draussen.py"));
            Assert.NotNull(CodeKiChange.Blocked(d, Path.Combine(Path.GetTempPath(), "x.py")));
            Assert.NotNull(CodeKiChange.Blocked(d, "C:/Windows/x.py"));
            Assert.NotNull(CodeKiChange.Blocked(d, ".git/config"));
            Assert.NotNull(CodeKiChange.Blocked(d, "node_modules/a.js"));
            Assert.NotNull(CodeKiChange.Blocked(d, ".env"));
            Assert.NotNull(CodeKiChange.Blocked(d, "konfig/server.key"));
            File.WriteAllBytes(Path.Combine(d, "bild.bin"), [1, 0, 2]);
            Assert.NotNull(CodeKiChange.Blocked(d, "bild.bin"));
            var pv = CodeKiChange.Build(Principal.LocalOwner, d, new([], [new("../draussen.py", "x\n"), new("rechner.py", "neu\n")]));
            Assert.Single(pv.Files); Assert.Single(pv.Rejected);
            Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(d)!, "draussen.py")));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0723: Vorschau je Datei, Ja/Nein je Änderung, Anwenden als ein Schritt, Rückgängig und Wiederholen über vorher/nachher")]
    public void Preview_accept_apply_undo()
    {
        var d = Proj();
        try
        {
            var before = File.ReadAllText(Path.Combine(d, "rechner.py"));
            var pv = CodeKiChange.Build(Principal.LocalOwner, d, CodeKiChange.Parse(Answer));
            Assert.Equal(2, pv.Files.Count);
            var r = pv.Files.Single(f => f.Path == "rechner.py");
            Assert.False(r.IsNew); Assert.Equal(2, r.Hunks.Count); // Prüfung auf null und neue Funktion = zwei Änderungen
            Assert.True(pv.Files.Single(f => f.Path == "tests/test_neu.py").IsNew);
            // nur die zweite Änderung (neue Funktion) annehmen, Testdatei ablehnen
            var only = CodeKiChange.Merge(r, new HashSet<int> { 1 });
            Assert.Contains("multipliziere", only); Assert.DoesNotContain("ValueError", only);
            var acc = new Dictionary<string, IReadOnlySet<int>> { ["rechner.py"] = new HashSet<int> { 1 }, ["tests/test_neu.py"] = new HashSet<int>() };
            var step = CodeKiChange.Apply(Principal.LocalOwner, Au13(d), d, pv, acc, "multiplizieren", "Ersatz")!;
            Assert.Single(step.Files);
            Assert.Equal(only, File.ReadAllText(Path.Combine(d, "rechner.py")));
            Assert.False(File.Exists(Path.Combine(d, "tests", "test_neu.py")));
            // Rückgängig: alles des Schritts auf einmal
            Assert.Empty(CodeKiChange.Write(Principal.LocalOwner, Au13(d), step.Files, d, back: true));
            Assert.Equal(before, File.ReadAllText(Path.Combine(d, "rechner.py")));
            Assert.Empty(CodeKiChange.Write(Principal.LocalOwner, Au13(d), step.Files, d, back: false));
            Assert.Equal(only, File.ReadAllText(Path.Combine(d, "rechner.py")));
            // danach von Hand geändert → Rückgängig lässt die Datei in Ruhe und meldet sie
            File.AppendAllText(Path.Combine(d, "rechner.py"), "# eigene Zeile\n");
            Assert.Equal(["rechner.py"], CodeKiChange.Write(Principal.LocalOwner, Au13(d), step.Files, d, back: true));
            Assert.Contains("# eigene Zeile", File.ReadAllText(Path.Combine(d, "rechner.py")));
            // Schritt-Datei und Eintrag im Dokument
            var f = Path.Combine(d, "schritt.json"); CodeKiChange.Save(f, step);
            Assert.Equal(step.Files[0].After, CodeKiChange.Load(f)!.Files[0].After);
            var data = new JsonObject { [CodeKiChange.StepsField] = new JsonArray(CodeKiChange.DocEntry(step)) };
            Assert.Equal([step.Id], CodeKiChange.AppliedIds(data));
            Assert.Contains("+++ b/rechner.py", CodeKiChange.UnifiedAll(pv));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0723: Datei seit dem Vorschlag geändert → nichts wird geschrieben; CRLF bleibt CRLF; neue Datei ganz")]
    public void Stale_and_line_endings()
    {
        var d = Proj();
        try
        {
            File.WriteAllText(Path.Combine(d, "win.py"), "a = 1\r\nb = 2\r\n");
            var pv = CodeKiChange.Build(Principal.LocalOwner, d, new(["x"], [new("win.py", "a = 1\nb = 3\n"), new("rechner.py", "x = 1\n")]));
            var all = pv.Files.ToDictionary(f => f.Path, f => (IReadOnlySet<int>)Enumerable.Range(0, f.Hunks.Count).ToHashSet());
            File.AppendAllText(Path.Combine(d, "rechner.py"), "# inzwischen\n");
            Assert.Throws<InvalidOperationException>(() => CodeKiChange.Apply(Principal.LocalOwner, Au13(d), d, pv, all, "w", "m"));
            Assert.Equal("a = 1\r\nb = 2\r\n", File.ReadAllText(Path.Combine(d, "win.py"))); // nichts halb geschrieben
            var pv2 = CodeKiChange.Build(Principal.LocalOwner, d, new(["x"], [new("win.py", "a = 1\nb = 3\n")]));
            CodeKiChange.Apply(Principal.LocalOwner, Au13(d), d, pv2, new Dictionary<string, IReadOnlySet<int>> { ["win.py"] = new HashSet<int> { 0 } }, "w", "m");
            Assert.Equal("a = 1\r\nb = 3\r\n", File.ReadAllText(Path.Combine(d, "win.py")));
            Assert.Null(CodeKiChange.Apply(Principal.LocalOwner, Au13(d), d, pv2 with { Files = [] }, new Dictionary<string, IReadOnlySet<int>>(), "w", "m"));
            Assert.Contains("DATEI:", CodeKiChange.SystemPrompt("Python-Programm", "python -m unittest -v"));
            Assert.Contains("(gerade geöffnet)", CodeKiChange.UserPrompt("x", [("rechner.py", "a")], "rechner.py", ["rechner.py"]));
            Assert.Equal("KI: ", AssistChain.UndoPrefix);
        }
        finally { Directory.Delete(d, true); }
    }
}
