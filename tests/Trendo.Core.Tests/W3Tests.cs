using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

public class CORE11_Security
{
    [Fact(DisplayName = "CORE-11: Schutz-Prüfung erkennt fehlende Sicherung und verändertes Protokoll")]
    public void Checks()
    {
        using var f = new Fixture();
        var b = new Backup(f.Root, Path.Combine(f.Root, "backups"), f.Core.Audit);
        var c = SecurityCenter.Checks(f.Core, b, DateTime.Now);
        Assert.Equal(CheckLevel.Risiko, c.First(x => x.Id == "backup").Level);
        Assert.Equal(CheckLevel.Gut, c.First(x => x.Id == "audit").Level);
        var s1 = SecurityCenter.Score(c);
        b.Create(DateTime.Now);
        Assert.True(SecurityCenter.Score(SecurityCenter.Checks(f.Core, b, DateTime.Now)) > s1);
        var lines = File.ReadAllLines(f.Core.Audit.FilePath); lines[0] = lines[0].Replace("core.start", "core.xxxxx"); File.WriteAllLines(f.Core.Audit.FilePath, lines);
        Assert.Equal(CheckLevel.Risiko, SecurityCenter.Checks(f.Core, b, DateTime.Now).First(x => x.Id == "audit").Level);
    }

    [Fact(DisplayName = "CORE-11: Ordnerprüfung findet Passwort, Schlüssel, gültige IBAN, Kartennummer – maskiert")]
    public void Privacy_scan()
    {
        using var f = new Fixture();
        var d = Path.Combine(f.Root, "scan"); Directory.CreateDirectory(d);
        File.WriteAllText(Path.Combine(d, "notiz.txt"), "Passwort: Sommer2026!\nIBAN DE89 3704 0044 0532 0130 00\nfalsch DE00 1234 5678 9012 3456 78\nKarte 4111 1111 1111 1111\nTelefon 0171 1234567");
        File.WriteAllText(Path.Combine(d, ".env"), "API_KEY=abcdefghijklmnop1234");
        File.WriteAllBytes(Path.Combine(d, "bild.png"), [1, 2]);
        var r = SecurityCenter.ScanFolder(d);
        Assert.Equal(["IBAN", "Kartennummer", "Passwort", "Schlüssel"], r.Select(x => x.Kind).Distinct().Order());
        Assert.Single(r, x => x.Kind == "IBAN");
        Assert.DoesNotContain(r, x => x.Masked.Contains("Sommer2026"));
        Assert.True(SecurityCenter.IbanOk("DE89370400440532013000")); Assert.True(SecurityCenter.Luhn("4111111111111111"));
    }
}
