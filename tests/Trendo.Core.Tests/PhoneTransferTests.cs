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
    [InlineData("x\u202Egpj.exe", "xgpj.exe")] // Befund N1: Richtungszeichen tarnt „.exe“ als „.jpg“
    [InlineData("a\u200Bb\uFEFF.txt", "ab.txt")]
    [InlineData("Cafe\u0301.txt", "Caf\u00E9.txt")] // NFC
    [InlineData("Urlaub \U0001F334.jpg", "Urlaub \U0001F334.jpg")] // Befund R2-N2: Emoji bleiben
    [InlineData("\U0001F600.png", "\U0001F600.png")] // Endung bleibt
    [InlineData("a\uD800b.txt", "ab.txt")] // ungepaartes Surrogat fliegt raus
    public void Clean_names(string raw, string expected) => Assert.Equal(expected, PhoneTransfer.CleanName(raw, "ersatz"));

    [Fact(DisplayName = "T-0767 R2-N2: Kürzen schneidet kein Emoji in der Mitte durch")]
    public void Long_emoji_name()
    {
        var n = PhoneTransfer.CleanName(new string('x', 115) + "\U0001F334\U0001F334.jpg", "e");
        Assert.True(n.Length <= 120);
        Assert.EndsWith(".jpg", n);
        Assert.DoesNotContain(System.Text.Rune.ReplacementChar, n.EnumerateRunes());
    }

    [Fact(DisplayName = "T-0767 R2-M1: viele gleichnamige Dateien – ab 50 mit Zeitstempel, bleibt schnell")]
    public async Task Many_same_names_fast()
    {
        var d = Temp();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 300; i++) await PhoneTransfer.ReceiveAsync(new MemoryStream([]), d, "leer.txt", 10);
        Assert.Equal(300, Directory.GetFiles(d).Length);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(15), $"zu langsam: {sw.Elapsed}");
        Assert.Contains(Directory.GetFiles(d), f => Path.GetFileName(f) == "leer (50).txt");
        Assert.DoesNotContain(Directory.GetFiles(d), f => Path.GetFileName(f) == "leer (51).txt");
        Directory.Delete(d, true);
    }

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

    [Fact(DisplayName = "T-0767 N3: 20 gleichzeitige Übertragungen mit gleichem Namen ergeben 20 Dateien")]
    public async Task Parallel_same_name()
    {
        var d = Temp();
        var datas = Enumerable.Range(0, 20).Select(_ => RandomNumberGenerator.GetBytes(30_000)).ToArray();
        var got = await Task.WhenAll(datas.Select(b => Task.Run(() => PhoneTransfer.ReceiveAsync(new MemoryStream(b), d, "foto.jpg", 100_000))));
        Assert.Equal(20, got.Select(g => g.Path).Distinct().Count());
        Assert.Equal(20, Directory.GetFiles(d).Length);
        foreach (var (g, b) in got.Zip(datas)) Assert.Equal(b, File.ReadAllBytes(g.Path));
        Directory.Delete(d, true);
    }

    [Fact(DisplayName = "T-0767 N2: Empfangenes bekommt das Fremd-Kennzeichen (Zone 3); Programmdateien werden als riskant erkannt")]
    public async Task Mark_of_the_web()
    {
        Assert.True(PhoneTransfer.IsRisky("x.EXE")); Assert.True(PhoneTransfer.IsRisky("desktop.ini")); Assert.True(PhoneTransfer.IsRisky("a.lnk"));
        Assert.False(PhoneTransfer.IsRisky("urlaub.jpg"));
        // R3-N1: eine gemeinsame Liste mit Browsers.Risky; die alte Browser-Liste bleibt vollständig drin
        foreach (var ext in new[] { ".exe", ".msi", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".scr", ".com", ".pif", ".application", ".appref-ms", ".msp", ".mst", ".xll",
                     ".settingcontent-ms", ".wsc", ".sct", ".inf", ".msc", ".ps1xml", ".psd1", ".chm", ".iso", ".img", ".vhd", ".vhdx", ".diagcab", ".gadget", ".docm", ".xlsm", ".pptm" })
        {
            Assert.True(Browsers.Risky("x" + ext.ToUpperInvariant()), ext);
            Assert.True(PhoneTransfer.IsRisky("x" + ext), ext);
        }
        Assert.False(Browsers.Risky("foto.jpg")); Assert.False(Browsers.Risky("brief.docx"));
        var d = Temp();
        var got = await PhoneTransfer.ReceiveAsync(new MemoryStream([1, 2, 3]), d, "setup.exe", 100, markOutside: true);
        Assert.True(got.Risky);
        if (OperatingSystem.IsWindows()) Assert.Contains("ZoneId=3", File.ReadAllText(got.Path + ":Zone.Identifier"));
        // Befund R2-N1: Laufwerk ohne Datenströme (FAT32/exFAT) → riskante Datei wird entschärft (.txt angehängt), harmlose bleibt
        PhoneTransfer.MarkOverride = _ => false;
        try
        {
            var exe = await PhoneTransfer.ReceiveAsync(new MemoryStream([1]), d, "tool.exe", 100, markOutside: true);
            Assert.Equal("tool.exe.txt", Path.GetFileName(exe.Path)); Assert.True(exe.Risky);
            var jpg = await PhoneTransfer.ReceiveAsync(new MemoryStream([1]), d, "bild.jpg", 100, markOutside: true);
            Assert.Equal("bild.jpg", Path.GetFileName(jpg.Path));
            // Befund R3-N1: Erlaubt-Liste statt Risiko-Liste – Unbekanntes, Archive und Office mit Makros werden entschärft
            foreach (var (name, expected) in new[] { ("daten.xyz", "daten.xyz.txt"), ("archiv.zip", "archiv.zip.txt"), ("brief.docm", "brief.docm.txt"),
                         ("film.mp4", "film.mp4"), ("brief.pdf", "brief.pdf"), ("tabelle.xlsx", "tabelle.xlsx"), ("notiz.txt", "notiz.txt") })
                Assert.Equal(expected, Path.GetFileName((await PhoneTransfer.ReceiveAsync(new MemoryStream([1]), d, name, 100, markOutside: true)).Path));
        }
        finally { PhoneTransfer.MarkOverride = null; }
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
        var home = IPAddress.Parse("192.0.2.57"); // Dokumentationsadresse
        const string token = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        log.Record(PhoneRunLog.Steps.Paired, "dev123456789", home, utcNow: t0.AddSeconds(5));
        log.Record(PhoneRunLog.Steps.PhoneToPc, "dev123456789", home, bytes: 10, sha: new string('a', 64), note: "Prüfsumme gleich (am Handy und am PC gebildet)", utcNow: t0.AddSeconds(9));
        log.Record(PhoneRunLog.Steps.Provided, "dev123456789", null, bytes: 20, sha: new string('b', 64), utcNow: t0.AddSeconds(12));
        Assert.False(PhoneRunLog.Evaluate(log.Entries)[2].Ok); // bereitgestellt ist noch nicht abgeholt
        log.Record(PhoneRunLog.Steps.PcToPhone, "dev123456789", home, bytes: 20, sha: new string('b', 64), utcNow: t0.AddSeconds(15));
        log.RecordRejectedToken(token, home, utcNow: t0.AddSeconds(16)); // vor dem Entfernen: unbekannt, nur gezählt
        Assert.Equal(1, log.OtherRejected);
        log.RecordRemoved("dev123456789", [PhoneAccess.TokenHash(token)], utcNow: t0.AddSeconds(20));
        // Befund M2: ein fremder Schlüssel nach dem Entfernen belegt die Sperre NICHT
        for (var i = 0; i < 50; i++) log.RecordRejectedToken(new string('f', 64), home, note: "GET /api/files", utcNow: t0.AddSeconds(21));
        Assert.False(PhoneRunLog.Evaluate(log.Entries)[4].Ok);
        Assert.Equal(51, log.OtherRejected);
        Assert.DoesNotContain(log.Entries, e => e.Step == PhoneRunLog.Steps.Rejected); // fremde Abweisungen wachsen nicht in die Liste
        // derselbe (alte) Schlüssel des entfernten Handys → ✓, höchstens 20 Einträge
        for (var i = 0; i < 30; i++) log.RecordRejectedToken(token, home, note: "GET /api/files", utcNow: t0.AddSeconds(22));
        Assert.All(PhoneRunLog.Evaluate(log.Entries), c => Assert.True(c.Ok, c.Name + ": " + c.Detail));
        Assert.Equal(PhoneRunLog.MaxRejectedPerDevice, log.Entries.Count(e => e.Step == PhoneRunLog.Steps.Rejected && e.Device == "Gerät 1"));
        Assert.Equal("Gerät 1", log.Entries[0].Device);
        Assert.Equal("Heimnetz", PhoneRunLog.Network(IPAddress.Parse("10.0.0.5")));
        Assert.Equal("Tailscale", PhoneRunLog.Network(IPAddress.Parse("100.100.1.2")));

        var (md, raw, sums) = log.Stop(d, "0.0.0-test", t0.AddSeconds(30));
        Assert.False(log.Active);
        var text = File.ReadAllText(md) + File.ReadAllText(raw);
        Assert.DoesNotContain("192.0.2", text);
        Assert.DoesNotContain("dev123456789", text);
        Assert.DoesNotContain(token, text);
        Assert.Contains("✓ Sperre nach Entfernen", text);
        Assert.Contains("Sonstige Abweisungen (unbekannter Schlüssel, zählen nicht als Beleg): 51", text);
        Assert.Contains(new string('b', 64), text);
        Assert.Equal(5 + PhoneRunLog.MaxRejectedPerDevice, File.ReadAllLines(raw).Length); // je Schritt eine Zeile
        var lines = File.ReadAllLines(sums);
        Assert.Equal(PhoneTransfer.Sha256File(raw) + "  " + Path.GetFileName(raw), lines[0]);
        Directory.Delete(d, true);
    }
}
