using System.Net;
using System.Security.Cryptography;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0767: Datei senden zwischen Handy und PC – Namen, Größenlimit, Prüfsumme, Bereitstellung je Handy, Lauf-Protokoll.</summary>
public class PhoneTransferTests
{
    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0767-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Theory(DisplayName = "T-0767: Dateinamen vom Handy werden gesäubert (Pfad, Sonderzeichen, Windows-Gerätenamen, Länge, versteckt)")]
    [InlineData("../../urlaub.jpg", "urlaub.jpg")]
    [InlineData("C:\\Windows\\win.ini", "win.ini")]
    [InlineData("a<b>c:d|e?f*.txt", "abcdef.txt")]
    [InlineData("CON.txt", "_CON.txt")]
    [InlineData("nul", "_nul")]
    [InlineData("com1.tar.gz", "_com1.tar.gz")]
    [InlineData(".bashrc", "bashrc")]
    [InlineData("  ...  ", "ersatz")]
    [InlineData("zeile\u0001\u001f.pdf", "zeile.pdf")]
    public void Clean_names(string raw, string expected) => Assert.Equal(expected, PhoneTransfer.CleanName(raw, "ersatz"));

    [Fact(DisplayName = "T-0767: überlange Namen werden auf 120 Zeichen gekürzt, die Endung bleibt")]
    public void Long_name()
    {
        var n = PhoneTransfer.CleanName(new string('x', 400) + ".jpeg", "e");
        Assert.Equal(120, n.Length);
        Assert.EndsWith(".jpeg", n);
    }

    [Fact(DisplayName = "T-0767: Empfang bildet SHA-256, legt die Datei erst vollständig ab; zu groß → nichts bleibt liegen")]
    public async Task Receive_limit_and_hash()
    {
        var d = Temp();
        var data = RandomNumberGenerator.GetBytes(200_000);
        var got = await PhoneTransfer.ReceiveAsync(new MemoryStream(data), d, "foto.jpg", 300_000);
        Assert.Equal(Path.Combine(d, "foto.jpg"), got.Path);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(data)), got.Sha256);
        Assert.Equal(data, File.ReadAllBytes(got.Path));
        var again = await PhoneTransfer.ReceiveAsync(new MemoryStream(data), d, "foto.jpg", 300_000);
        Assert.Equal("foto (2).jpg", Path.GetFileName(again.Path));

        await Assert.ThrowsAsync<PhoneTransfer.TooLargeException>(() => PhoneTransfer.ReceiveAsync(new MemoryStream(data), d, "gross.bin", 100_000));
        Assert.Equal(["foto (2).jpg", "foto.jpg"], Directory.GetFiles(d).Select(Path.GetFileName).Order().ToArray()); // keine Teil-Datei, keine halbe Datei
        Directory.Delete(d, true);
    }

    [Fact(DisplayName = "T-0767: „Datei senden“ gilt nur für das gewählte Handy, 24 Stunden; Entfernen löscht sofort; keine Pfadtricks")]
    public async Task Offer_per_device()
    {
        var d = Temp(); var outbox = Path.Combine(d, "Zum Handy"); Directory.CreateDirectory(outbox);
        var src = Path.Combine(d, "Brief.pdf"); File.WriteAllText(src, "%PDF-1.4 Brief");
        File.WriteAllText(Path.Combine(outbox, "alle.txt"), "für alle");
        var got = await PhoneTransfer.OfferAsync(src, outbox, "aaaaaa111111");
        Assert.Equal(PhoneTransfer.Sha256File(src), got.Sha256);
        var now = DateTime.UtcNow;

        var a = PhoneTransfer.Visible(outbox, "aaaaaa111111", now);
        Assert.Equal(["Brief.pdf", "alle.txt"], a.Select(x => x.File.Name).ToArray());
        Assert.True(a[0].Personal);
        Assert.DoesNotContain(PhoneTransfer.Visible(outbox, "bbbbbb222222", now), x => x.File.Name == "Brief.pdf"); // anderes Handy sieht es nicht
        Assert.Null(PhoneTransfer.Resolve(outbox, "bbbbbb222222", "Brief.pdf", now));
        Assert.Equal(got.Path, PhoneTransfer.Resolve(outbox, "aaaaaa111111", "Brief.pdf", now));
        Assert.Null(PhoneTransfer.Resolve(outbox, "aaaaaa111111", "../Brief.pdf", now));
        Assert.Null(PhoneTransfer.Resolve(outbox, "aaaaaa111111", ".fuer-aaaaaa111111", now));
        Assert.Throws<ArgumentException>(() => PhoneTransfer.DeviceFolder(outbox, "../x"));

        Assert.Empty(PhoneTransfer.Visible(outbox, "aaaaaa111111", now.AddHours(25)).Where(x => x.Personal)); // nach 24 Stunden weg
        await PhoneTransfer.OfferAsync(src, outbox, "aaaaaa111111");
        PhoneTransfer.DropDevice(outbox, "aaaaaa111111");
        Assert.False(Directory.Exists(PhoneTransfer.DeviceFolder(outbox, "aaaaaa111111")));
        await Assert.ThrowsAsync<PhoneTransfer.TooLargeException>(() => PhoneTransfer.OfferAsync(src, outbox, "aaaaaa111111", 3));
        Directory.Delete(d, true);
    }

    [Fact(DisplayName = "T-0767: Lauf-Protokoll – anonym (Gerät 1, Netzart), belegt Kopplung, beide Richtungen, Entfernen und Sperre; Bericht mit SHA-256")]
    public void Run_log()
    {
        var d = Temp();
        var log = new PhoneRunLog();
        log.Record(PhoneRunLog.Steps.Paired, "x", IPAddress.Loopback); // vor dem Start: nichts
        Assert.Empty(log.Entries);
        var t0 = new DateTime(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc);
        log.Start(t0);
        var home = IPAddress.Parse("192.168.178.57");
        log.Record(PhoneRunLog.Steps.Paired, "dev123456789", home, utcNow: t0.AddSeconds(5));
        log.Record(PhoneRunLog.Steps.PhoneToPc, "dev123456789", home, bytes: 10, sha: new string('a', 64), note: "Prüfsumme gleich (am Handy und am PC gebildet)", utcNow: t0.AddSeconds(9));
        log.Record(PhoneRunLog.Steps.Provided, "dev123456789", null, bytes: 20, sha: new string('b', 64), utcNow: t0.AddSeconds(12));
        Assert.False(PhoneRunLog.Evaluate(log.Entries)[2].Ok); // bereitgestellt ist noch nicht abgeholt
        log.Record(PhoneRunLog.Steps.PcToPhone, "dev123456789", home, bytes: 20, sha: new string('b', 64), utcNow: t0.AddSeconds(15));
        log.Record(PhoneRunLog.Steps.Rejected, null, home, utcNow: t0.AddSeconds(16)); // vor dem Entfernen zählt nicht
        Assert.False(PhoneRunLog.Evaluate(log.Entries)[4].Ok);
        log.Record(PhoneRunLog.Steps.Removed, "dev123456789", null, utcNow: t0.AddSeconds(20));
        log.Record(PhoneRunLog.Steps.Rejected, null, home, note: "GET /api/files", utcNow: t0.AddSeconds(22));
        Assert.All(PhoneRunLog.Evaluate(log.Entries), c => Assert.True(c.Ok, c.Name + ": " + c.Detail));
        Assert.Equal("Gerät 1", log.Entries[0].Device);
        Assert.Equal("Heimnetz", log.Entries[0].Network);
        Assert.Equal("Tailscale", PhoneRunLog.Network(IPAddress.Parse("100.100.1.2")));

        var (md, raw, sums) = log.Stop(d, "0.0.0-test", t0.AddSeconds(30));
        Assert.False(log.Active);
        var text = File.ReadAllText(md) + File.ReadAllText(raw);
        Assert.DoesNotContain("192.168", text);
        Assert.DoesNotContain("dev123456789", text);
        Assert.Contains("✓ Sperre nach Entfernen", text);
        Assert.Contains(new string('b', 64), text);
        Assert.Equal(7, File.ReadAllLines(raw).Length); // 7 Schritte nach dem Start, je eine Zeile
        var lines = File.ReadAllLines(sums);
        Assert.Equal(PhoneTransfer.Sha256File(raw) + "  " + Path.GetFileName(raw), lines[0]);
        Directory.Delete(d, true);
    }
}
