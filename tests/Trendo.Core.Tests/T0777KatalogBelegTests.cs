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
}
