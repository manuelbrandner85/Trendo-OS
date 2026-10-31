using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0245 (V111): „getestet“ nur mit Beleg – Prüfsumme, Datei, Zählung.</summary>
public class EvidenceTests
{
    [Fact(DisplayName = "T-0245: Beleg geprüft nur mit passender Prüfsumme; verändert, fehlend, ohne Prüfsumme und ohne Beleg werden getrennt erkannt")]
    public void States()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ev-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(root, "reports"));
        try
        {
            File.WriteAllText(Path.Combine(root, "reports", "log.txt"), "Bestanden!\r\nZeile 2\r\n"); // Windows-Zeilenenden
            var lf = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData("Bestanden!\nZeile 2\n"u8.ToArray()));
            Assert.Equal(EvidenceState.Verified, Evidence.Check(new("reports/log.txt", lf, null), root));
            Assert.Equal(EvidenceState.Recorded, Evidence.Check(new("reports/log.txt", lf, null), null)); // installiertes Trendo: Datei nicht da
            Assert.Equal(EvidenceState.PathOnly, Evidence.Check(new("reports/log.txt", null, null), root));
            Assert.Equal(EvidenceState.None, Evidence.Check(null, root));
            Assert.Equal(EvidenceState.Invalid, Evidence.Check(new("reports/fehlt.txt", null, null), root));
            File.AppendAllText(Path.Combine(root, "reports", "log.txt"), "nachträglich geändert");
            Assert.Equal(EvidenceState.Invalid, Evidence.Check(new("reports/log.txt", lf, null), root));

            var item = new ProgressItem("X", "x", "IMPLEMENTED", "PC_VERIFIED", new("reports/log.txt", lf, null));
            Assert.False(item.TestedWithEvidence(root)); // verändert → nicht belegt
            Assert.False(new ProgressItem("Y", "y", "IMPLEMENTED", "PC_VERIFIED").TestedWithEvidence(root)); // ohne Beleg
            Assert.Equal("kein Beleg", Evidence.Label(EvidenceState.None));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0245: Jeder im Register mit Prüfsumme eingetragene Beleg stimmt mit der Datei überein (keine stille Änderung von Rohbelegen)")]
    public void Register_evidence_unchanged()
    {
        var root = Evidence.RepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(root);
        var lines = File.ReadAllLines(Path.Combine(root!, "requirements.yaml"));
        var checkedCount = 0; var bad = new List<string>();
        for (var i = 0; i < lines.Length - 1; i++)
        {
            var l = lines[i].Trim();
            if (!l.StartsWith("evidence: ") || !lines[i + 1].Trim().StartsWith("evidence_sha256: ")) continue;
            var e = new EvidenceInfo(l["evidence: ".Length..].Trim(), lines[i + 1].Trim()["evidence_sha256: ".Length..].Trim(), null);
            checkedCount++;
            if (Evidence.Check(e, root) != EvidenceState.Verified) bad.Add(e.Path!);
        }
        Assert.True(checkedCount > 100, $"nur {checkedCount} Belege mit Prüfsumme gefunden");
        Assert.True(bad.Count == 0, "Beleg verändert oder fehlt: " + string.Join(", ", bad.Distinct()));
    }
}
