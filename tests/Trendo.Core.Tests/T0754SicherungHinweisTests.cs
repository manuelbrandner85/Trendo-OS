using System.IO.Compression;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0754 (CORE-11, audit-5): Sicherungen bleiben unverschlüsselt (Entscheidung des Nutzers) – das wird ehrlich gesagt:
/// im Ergebnis, im Vermerk jeder ZIP und in der Schutz-Ampel. Das Prüflesen aus T-0681 bleibt.</summary>
public class T0754SicherungHinweisTests
{
    [Fact(DisplayName = "T-0754: Ergebnis und Vermerk der ZIP sagen „nicht verschlüsselt“ und empfehlen ein verschlüsseltes Laufwerk; Prüflesen bleibt")]
    public void Hinweis_in_Ergebnis_und_Datei()
    {
        using var f = new Fixture(); var root = f.Core.DataRoot;
        File.WriteAllText(Path.Combine(root, "a.txt"), "Inhalt A");
        var b = new Backup(root, Path.Combine(root, "backups"), f.Core.Audit);
        var r = b.CreateChecked(new DateTime(2026, 11, 23, 10, 0, 0));
        Assert.Contains("geprüft", r.Say()); Assert.Contains("nicht verschlüsselt", r.Say());
        using (var z = ZipFile.OpenRead(r.Zip)) { Assert.Contains("nicht verschluesselt", z.Comment); Assert.Contains("BitLocker", z.Comment); Assert.All(z.Comment, c => Assert.True(c < 128, $"Nicht-ASCII im Vermerk: {c}")); } // steht in der Datei selbst, nur ASCII (T-0776: 7-Zip zeigte Umlaute im Kommentar falsch)
        var note = b.Describe(r.Zip)!; Assert.True(note.Complete); Assert.Contains("lesen", note.Hinweis); Assert.Contains("BitLocker", note.Hinweis);
        Assert.True(b.Verify(r.Zip).Ok); // Prüflesen (T-0681) unverändert
        Assert.Contains("nicht verschlüsselt", Backup.UnencryptedNotice); Assert.Contains("Jeder", Backup.UnencryptedNotice);
        Assert.Contains("ändert dafür nichts", Backup.EncryptAdvice);
    }

    [Fact(DisplayName = "T-0754: Die Schutz-Ampel zeigt unverschlüsselte Sicherungen gelb mit Ordner – nie grün")]
    public void Ampel_verspricht_nichts()
    {
        using var f = new Fixture();
        var b = new Backup(f.Root, Path.Combine(f.Root, "backups"), f.Core.Audit);
        Assert.DoesNotContain(SecurityCenter.Checks(f.Core, b, DateTime.Now), c => c.Id == "backup-crypt"); // ohne Sicherung: dort steht schon „keine Sicherung“
        b.Create(DateTime.Now);
        var c = SecurityCenter.Checks(f.Core, b, DateTime.Now).Single(x => x.Id == "backup-crypt");
        Assert.Equal(CheckLevel.Hinweis, c.Level); Assert.Contains("nicht verschlüsselt", c.Detail); Assert.Contains(b.Folder, c.Detail); Assert.Contains("BitLocker", c.Detail);
        Assert.Equal("sicherung", c.FixPage);
    }

    [Fact(DisplayName = "T-0754 Gegenprüfung 4: Studienwissen CORE-11 verspricht keine Passwortverwaltung")]
    public void Studienwissen_ehrlich()
    {
        var k = Trendo.Core.PromptAction.StudioKnowledgeBase.For("CORE-11")!;
        Assert.DoesNotContain("Passwörter und Sicherungen schützen", k.Purpose);
        Assert.Contains("Passwörter finden", k.Purpose);
    }
}
