using System.Security.Cryptography;
using System.Text;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0777 (Q5 der Neubewertung T-0769): Der Programmkatalog nimmt den Teststand aus Belegen – Register-Status nur mit gültigem Rohbeleg
/// (SHA-256 unter reports/), dazu die Ergebnisse eines eingelesenen Testlaufs (TRX). PC_VERIFIED für SYS-04, SYS-07, CORE-04 und X-08 ist zurückgenommen.</summary>
public class T0777KatalogBeleg
{
    static string Repo() { var d = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(d, "Trendo.sln"))) d = Path.GetDirectoryName(d)!; return d; }

    static string[] Req(string id, string test, string? evidence = null, string? sha = null) =>
        [$"- id: {id}", "  implementation_status: IMPLEMENTED", $"  test_status: {test}", .. evidence is null ? Array.Empty<string>() : [$"  evidence: {evidence}"], .. sha is null ? Array.Empty<string>() : [$"  evidence_sha256: {sha}"], "- id: NEXT-01"];

    [Fact(DisplayName = "T-0777 Katalog: Register-Status zählt nur mit passendem Rohbeleg; ohne oder mit veränderter Datei steht NOT_RUN mit Grund")]
    public void Register_only_with_evidence()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-t0777-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(root, "reports"));
        try
        {
            File.WriteAllText(Path.Combine(root, "reports", "lauf.txt"), "Bestanden!\n");
            var sha = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("Bestanden!\n")));
            Assert.Equal("IMPLEMENTED / Test PC_VERIFIED (Beleg reports/lauf.txt)", ProgramCatalog.Status(Req("SPEC-99", "PC_VERIFIED", "reports/lauf.txt", sha), "SPEC-99", root));
            Assert.StartsWith("IMPLEMENTED / Test NOT_RUN (Register PC_VERIFIED, aber Beleg fehlt oder verändert", ProgramCatalog.Status(Req("SPEC-99", "PC_VERIFIED", "reports/lauf.txt", new string('0', 64)), "SPEC-99", root));
            Assert.StartsWith("IMPLEMENTED / Test NOT_RUN (Register TESTED, aber kein Beleg", ProgramCatalog.Status(Req("SPEC-99", "TESTED"), "SPEC-99", root));
            Assert.Equal("IMPLEMENTED / Test NOT_RUN (kein Testlauf eingelesen)", ProgramCatalog.Status(Req("SPEC-99", "NOT_RUN"), "SPEC-99", root));
            // zurückgenommen: gültiger Beleg, aber der Kernweg lief nie echt
            Assert.Equal("IMPLEMENTED / Test TESTED (PC_VERIFIED zurückgenommen: " + ProgramCatalog.RevokedPcVerified["SYS-04"] + ")",
                ProgramCatalog.Status(Req("SYS-04", "PC_VERIFIED", "reports/lauf.txt", sha), "SYS-04", root));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0777 Katalog: Testlauf (TRX) hebt NOT_RUN auf TESTED, ein Fehlschlag gibt FAIL, nur Übersprungenes zählt nicht als getestet")]
    public void Outcomes_from_trx()
    {
        var req = Req("SPEC-99", "NOT_RUN");
        var o = new Dictionary<string, string> { ["A"] = "Passed", ["B"] = "NotExecuted", ["C"] = "Failed" };
        Assert.Equal("IMPLEMENTED / Test TESTED (Lauf: 1 bestanden · 1 übersprungen · 0 fehlgeschlagen · 1 nicht im Lauf)", ProgramCatalog.Status(req, "SPEC-99", null, ["A", "B", "X"], o));
        Assert.Equal("IMPLEMENTED / Test FAIL (Lauf: 1 bestanden · 0 übersprungen · 1 fehlgeschlagen · 0 nicht im Lauf)", ProgramCatalog.Status(req, "SPEC-99", null, ["A", "C"], o));
        Assert.Equal("IMPLEMENTED / Test NOT_RUN (Lauf: 0 bestanden · 1 übersprungen · 0 fehlgeschlagen · 0 nicht im Lauf)", ProgramCatalog.Status(req, "SPEC-99", null, ["B"], o));
    }

    [Fact(DisplayName = "T-0777 Katalog: im echten Register stehen SYS-04, SYS-07, CORE-04 und X-08 nicht mehr als PC_VERIFIED im Katalog")]
    public void Revoked_in_real_register()
    {
        var req = File.ReadAllLines(Path.Combine(Repo(), "requirements.yaml"));
        foreach (var id in new[] { "SYS-04", "SYS-07", "CORE-04", "X-08" })
        {
            var s = ProgramCatalog.Status(req, id, Repo());
            Assert.DoesNotContain("Test PC_VERIFIED", s);
            Assert.Contains("PC_VERIFIED zurückgenommen", s);
        }
    }

    static string Trx(string dir, string file, params (string Name, string Outcome)[] rows)
    {
        var p = Path.Combine(dir, file);
        File.WriteAllText(p, "<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results>" +
            string.Concat(rows.Select(r => $"<UnitTestResult testName=\"{System.Security.SecurityElement.Escape(r.Name)}\" outcome=\"{r.Outcome}\" />")) + "</Results></TestRun>");
        return p;
    }

    static string TempDir() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "trendo-t0777-" + Guid.NewGuid().ToString("N")[..8])).FullName;

    [Fact(DisplayName = "T-0777 Nachbesserung: Theory-Fälle („Name(param: …)“) zählen für ihren DisplayName – ein fehlgeschlagener Fall gibt FAIL")]
    public void Theory_case_failure_counts()
    {
        var dir = TempDir();
        try
        {
            var o = ProgramCatalog.ReadTrx([Trx(dir, "a.trx", ("SPEC-99: Rechnen (nur mit X)(a: 1, b: \"x\")", "Passed"), ("SPEC-99: Rechnen (nur mit X)(a: 2, b: \"y\")", "Failed"), ("SPEC-99: Klar (nur mit X)", "Passed"))]);
            Assert.Equal("Failed", o["SPEC-99: Rechnen (nur mit X)"]);
            Assert.Equal("Passed", o["SPEC-99: Klar (nur mit X)"]); // Klammern im DisplayName sind kein Theory-Teil
            Assert.Equal("SPEC-99: Klar (nur mit X)", ProgramCatalog.TestBaseName("SPEC-99: Klar (nur mit X)"));
            Assert.StartsWith("IMPLEMENTED / Test FAIL (Lauf: 1 bestanden · 0 übersprungen · 1 fehlgeschlagen",
                ProgramCatalog.Status(Req("SPEC-99", "NOT_RUN"), "SPEC-99", null, ["SPEC-99: Rechnen (nur mit X)", "SPEC-99: Klar (nur mit X)"], o));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0777 Nachbesserung: mehrere TRX – FAIL gewinnt in jeder Reihenfolge; Error, Timeout, Aborted, Inconclusive zählen als Fehlschlag")]
    public void Several_trx_fail_wins()
    {
        var dir = TempDir();
        try
        {
            var a = Trx(dir, "a.trx", ("T", "Passed"), ("U", "NotExecuted"), ("E", "Error"), ("Ti", "Timeout"), ("Ab", "Aborted"), ("In", "Inconclusive"));
            var b = Trx(dir, "b.trx", ("T", "Failed"), ("U", "Passed"));
            foreach (var o in new[] { ProgramCatalog.ReadTrx([a, b]), ProgramCatalog.ReadTrx([b, a]) })
            {
                Assert.Equal("Failed", o["T"]); Assert.Equal("Passed", o["U"]);
                foreach (var k in new[] { "E", "Ti", "Ab", "In" }) Assert.Equal("Failed", o[k]);
            }
            Assert.StartsWith("IMPLEMENTED / Test FAIL", ProgramCatalog.Status(Req("SPEC-99", "NOT_RUN"), "SPEC-99", null, ["E"], new Dictionary<string, string> { ["E"] = "Error" }));
        }
        finally { Directory.Delete(dir, true); }
    }

    sealed class AllTools : IToolProbe { public bool IsAvailable(string tool) => true; }

    static string RegisterTest(string[] req, string id) =>
        req.SkipWhile(l => l.Trim() != $"- id: {id}").Skip(1).TakeWhile(l => !l.StartsWith("- id:")).First(l => l.Trim().StartsWith("test_status:")).Split(':', 2)[1].Trim();

    [Fact(DisplayName = "T-0777 Nachbesserung: Studio-Karten und Fortschritt zeigen SYS-04, SYS-07, CORE-04, X-08 nicht mehr als „auf dem PC geprüft“ (Register unverändert)")]
    public void Revoked_in_registry_and_progress()
    {
        string[] ids = ["SYS-04", "SYS-07", "CORE-04", "X-08"];
        var yaml = Path.Combine(Repo(), "requirements.yaml");
        var raw = File.ReadAllLines(yaml);
        foreach (var id in ids) Assert.Equal("PC_VERIFIED", RegisterTest(raw, id)); // das Register selbst bleibt unverändert
        var reg = new StudioRegistry().Load(yaml, new AllTools());
        foreach (var id in ids)
        {
            var s = reg.Single(x => x.Id == id);
            Assert.NotEqual("PC_VERIFIED", s.TestStatus);
            Assert.DoesNotContain("auf dem PC", s.Reason);
        }
        Assert.Contains(reg, x => x.TestStatus == "PC_VERIFIED" && x.Reason.Contains("auf dem PC")); // andere Module unverändert
        var items = Progress.Load(yaml, Path.Combine(Repo(), "docs", "buildplan.json")).SelectMany(m => m.Subs.Prepend(m.Module)).ToList();
        foreach (var id in ids.Where(i => items.Any(x => x.Id == i)))
            Assert.DoesNotContain("auf dem PC", Progress.GermanStatus(items.First(x => x.Id == id)));
        Assert.Equal("Fertig & getestet", Progress.GermanStatus(new ProgressItem("SYS-04", "x", "IMPLEMENTED", "PC_VERIFIED")));
        Assert.Equal("Fertig & auf dem PC geprüft", Progress.GermanStatus(new ProgressItem("SPEC-99", "x", "IMPLEMENTED", "PC_VERIFIED")));
    }

    [Fact(DisplayName = "T-0777 Nachbesserung: Belegpfade nur relativ im Projekt (kein absoluter Pfad, kein „..“); Testbelege nur unter reports/")]
    public void Evidence_paths_stay_in_reports()
    {
        var root = TempDir();
        Directory.CreateDirectory(Path.Combine(root, "reports")); Directory.CreateDirectory(Path.Combine(root, "docs"));
        try
        {
            File.WriteAllText(Path.Combine(root, "docs", "lauf.txt"), "Bestanden!\n");
            var sha = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("Bestanden!\n")));
            Assert.Equal(EvidenceState.Invalid, Evidence.Check(new(Path.Combine(root, "docs", "lauf.txt"), sha, null), root)); // absolut
            Assert.Equal(EvidenceState.Invalid, Evidence.Check(new("reports/../docs/lauf.txt", sha, null), root));
            Assert.Equal(EvidenceState.Invalid, Evidence.Check(new("../lauf.txt", sha, null), root));
            Assert.Equal(EvidenceState.Invalid, Evidence.Check(new("C:/Windows/win.ini", sha, null), root));
            Assert.False(Evidence.IsReportPath("docs/lauf.txt")); Assert.True(Evidence.IsReportPath("reports/T-1/lauf.txt"));
            Assert.False(new ProgressItem("X", "x", "IMPLEMENTED", "TESTED", new("docs/lauf.txt", sha, null)).TestedWithEvidence(root));
            Assert.StartsWith("IMPLEMENTED / Test NOT_RUN (Register TESTED, aber Beleg fehlt oder verändert", ProgramCatalog.Status(Req("SPEC-99", "TESTED", "docs/lauf.txt", sha), "SPEC-99", root));
        }
        finally { Directory.Delete(root, true); }
    }
}
