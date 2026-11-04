using System.Security.Cryptography;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Trendo-OS-Abbild für Stick/SSD: Beschreibung, Teile prüfen und zusammensetzen, schreiben und zurücklesen – auf eine Datei als virtuelle Festplatte.</summary>
public class TrendoImageTests
{
    static string Sha(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));
    static string Json(long bytes, string sha, params (string F, string S, long B)[] parts) =>
        $$"""{"schema":1,"version":"0.9.71","bytes":{{bytes}},"sha256":"{{sha}}","min_gb":32,"parts":[{{string.Join(",", parts.Select(p => $$"""{"file":"{{p.F}}","sha256":"{{p.S}}","bytes":{{p.B}}}"""))}}]}""";

    [Fact(DisplayName = "Abbild-Beschreibung: gültige wird gelesen; falsche Summe, fehlende Teile oder unsichere Dateinamen werden abgelehnt")]
    public void Parse_manifest()
    {
        var s = new string('a', 64);
        var m = TrendoImage.Parse(Json(30, s, ("TrendoOS-Abbild.part00", s, 20), ("TrendoOS-Abbild.part01", s, 10)));
        Assert.NotNull(m); Assert.Equal("0.9.71", m!.Version); Assert.Equal(2, m.Parts.Count); Assert.Equal(32, m.MinGb);
        Assert.Null(TrendoImage.Parse(Json(31, s, ("TrendoOS-Abbild.part00", s, 20), ("TrendoOS-Abbild.part01", s, 10)))); // Summe passt nicht
        Assert.Null(TrendoImage.Parse(Json(20, s, ("../../evil.exe", s, 20))));
        Assert.Null(TrendoImage.Parse(Json(20, "kurz", ("TrendoOS-Abbild.part00", s, 20))));
        Assert.Null(TrendoImage.Parse("kein json"));
    }

    [Fact(DisplayName = "Teile prüfen und zusammensetzen, auf virtuelle Festplatte schreiben (Sektoren aufgefüllt), zurücklesen; beschädigtes Teil wird erkannt")]
    public async Task Join_write_verify()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-abbild-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var img = new byte[5_000_123]; new Random(7).NextBytes(img);
            var a = img[..3_000_000]; var b = img[3_000_000..];
            await File.WriteAllBytesAsync(Path.Combine(dir, "TrendoOS-Abbild.part00"), a);
            await File.WriteAllBytesAsync(Path.Combine(dir, "TrendoOS-Abbild.part01"), b);
            var m = TrendoImage.Parse(Json(img.Length, Sha(img), ("TrendoOS-Abbild.part00", Sha(a), a.Length), ("TrendoOS-Abbild.part01", Sha(b), b.Length)))!;
            var output = Path.Combine(dir, "abbild.img");
            Assert.Null(await TrendoImage.JoinAsync(m, dir, output));
            Assert.Equal(img, await File.ReadAllBytesAsync(output));

            var disk = Path.Combine(dir, "virtuell.disk");
            await using (var f = new FileStream(disk, FileMode.Create)) { f.SetLength(8_000_000); f.Write(Enumerable.Repeat((byte)0xEE, 8_000_000).ToArray()); } // „Stick“ mit alten Daten
            var steps = new List<string>();
            await using (var src = File.OpenRead(output))
            await using (var dst = new FileStream(disk, FileMode.Open, FileAccess.ReadWrite))
                Assert.Null(await TrendoImage.WriteAndVerifyAsync(src, dst, m.Bytes, m.Sha256, (s, _) => { if (!steps.Contains(s)) steps.Add(s); }));
            Assert.Equal(["schreiben", "prüfen"], steps);
            var written = await File.ReadAllBytesAsync(disk);
            Assert.Equal(img, written[..img.Length]);
            Assert.All(written[img.Length..((img.Length + 511) / 512 * 512)], x => Assert.Equal(0, x)); // letzter Sektor mit Nullen aufgefüllt

            // Ergebnis passt nicht zur Prüfsumme (z. B. defekter Stick) → Kontrolle schlägt fehl
            await using (var src = File.OpenRead(output))
            await using (var dst = new FileStream(disk, FileMode.Open, FileAccess.ReadWrite))
                Assert.Contains("Kontrolle fehlgeschlagen", await TrendoImage.WriteAndVerifyAsync(src, dst, m.Bytes, new string('0', 64)));

            b[10] ^= 0xFF; await File.WriteAllBytesAsync(Path.Combine(dir, "TrendoOS-Abbild.part01"), b);
            Assert.Contains("part01 ist beschädigt", await TrendoImage.JoinAsync(m, dir, Path.Combine(dir, "neu.img")));
            Assert.False(File.Exists(Path.Combine(dir, "neu.img")));
        }
        finally { Directory.Delete(dir, true); }
    }
}
