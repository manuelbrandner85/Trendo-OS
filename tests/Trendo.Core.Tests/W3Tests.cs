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

public class X02_SelfHealing
{
    [Fact(DisplayName = "X-02: Fehlende Ordner, kaputte Dateien, verwaiste Pfade und alte Zwischendateien werden repariert")]
    public void Repairs()
    {
        using var f = new Fixture();
        File.WriteAllText(Path.Combine(f.Root, "settings.json"), "{kaputt");
        File.WriteAllText(Path.Combine(f.Root, "tools.json"), "{\"blender\":\"/gibt/es/nicht/blender.exe\"}");
        Directory.CreateDirectory(Path.Combine(f.Root, "tmp")); var old = Path.Combine(f.Root, "tmp", "alt.bin"); File.WriteAllText(old, "x"); File.SetLastWriteTime(old, DateTime.Now.AddDays(-30));
        var h = new SelfHealer(f.Root, f.Core.Audit) { FreeSpace = _ => 100L * 1024 * 1024 };
        var check = h.Run(false, DateTime.Now);
        Assert.True(File.Exists(Path.Combine(f.Root, "settings.json")));
        Assert.Equal(HealState.NeedsYou, check.First(r => r.Id == "json").State);
        var r = h.Run(true, DateTime.Now);
        Assert.All(new[] { "folders", "json", "tools", "tmp" }, id => Assert.Equal(HealState.Repaired, r.First(x => x.Id == id).State));
        Assert.Equal(HealState.NeedsYou, r.First(x => x.Id == "disk").State);
        Assert.False(File.Exists(Path.Combine(f.Root, "settings.json")));
        Assert.Single(Directory.GetFiles(Path.Combine(f.Root, "quarantaene"), "*", SearchOption.AllDirectories));
        Assert.Equal("{}", File.ReadAllText(Path.Combine(f.Root, "tools.json")));
        Assert.False(File.Exists(old));
        var again = new SelfHealer(f.Root, f.Core.Audit).Run(true, DateTime.Now);
        Assert.DoesNotContain(again, x => x.State == HealState.Repaired);
        Assert.NotNull(SelfHealer.Last(f.Root));
    }

    [Fact(DisplayName = "X-02: Verändertes Protokoll wird gemeldet, aber bewusst nicht „repariert“")]
    public void Audit_not_touched()
    {
        using var f = new Fixture();
        var lines = File.ReadAllLines(f.Core.Audit.FilePath); lines[0] = lines[0].Replace("core.start", "core.fake!"); File.WriteAllLines(f.Core.Audit.FilePath, lines);
        var before = File.ReadAllText(f.Core.Audit.FilePath);
        var r = new SelfHealer(f.Root, f.Core.Audit).Run(true, DateTime.Now);
        Assert.Equal(HealState.NeedsYou, r.First(x => x.Id == "audit").State);
        Assert.StartsWith(before, File.ReadAllText(f.Core.Audit.FilePath));
    }
}

public sealed class FakeRunner(Func<string, string, (int, string)> f) : IProcessRunner
{
    public List<string> Calls { get; } = [];
    public Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default) { Calls.Add($"{file} {args}"); return Task.FromResult(f(file, args)); }
}

public class SYS04_AppCenter
{
    public const string WingetSearch = "\b-\b\\\b|\bName                 Id                       Version   Source\n------------------------------------------------------------------\nVLC media player     VideoLAN.VLC             3.0.21    winget\nGIMP                 GIMP.GIMP                2.10.38   winget\n";
    public const string WingetUpgrade = "Name           Id                Version  Available  Source\n-----------------------------------------------------------\nGit            Git.Git           2.45.0   2.47.1     winget\n7-Zip          7zip.7zip         23.01    24.08      winget\n2 upgrades available.\n";

    [Fact(DisplayName = "SYS-04: winget- und flatpak-Ausgaben werden korrekt gelesen")]
    public void Parse()
    {
        var s = PackageParsers.ParseWinget(WingetSearch);
        Assert.Equal(2, s.Count); Assert.Equal(new PackageInfo("VLC media player", "VideoLAN.VLC", "3.0.21", null, "winget"), s[0]);
        var u = PackageParsers.ParseWinget(WingetUpgrade);
        Assert.Equal(2, u.Count); Assert.Equal("2.47.1", u[0].Available); Assert.Equal("2.45.0", u[0].Version);
        var f = PackageParsers.ParseFlatpak("VLC\torg.videolan.VLC\t3.0.21\nGIMP\torg.gimp.GIMP\t2.10\n");
        Assert.Equal("org.gimp.GIMP", f[1].Id);
        Assert.False(PackageParsers.SafeId("x; rm -rf /")); Assert.True(PackageParsers.SafeId("VideoLAN.VLC"));
    }

    [Fact(DisplayName = "SYS-04: Installieren nur nach Freigabe, als Benutzer, gefährliche IDs abgelehnt")]
    public async Task Install_gated()
    {
        using var f = new Fixture();
        var run = new FakeRunner((_, a) => (0, a.StartsWith("search") ? WingetSearch : "ok"));
        var c = new AppCenter(new WingetBackend(run), f.Core.Approvals, f.Core.Audit);
        var vlc = (await c.Backend.SearchAsync("vlc"))[0];
        var r = c.Plan("install", vlc);
        await Assert.ThrowsAsync<ApprovalException>(() => c.ExecuteAsync(r));
        var r2 = c.Plan("install", vlc); f.Core.Approvals.Approve(r2.Id, Principal.LocalOwner);
        Assert.True((await c.ExecuteAsync(r2)).Ok);
        Assert.Contains(run.Calls, x => x.Contains("install --id VideoLAN.VLC -e --scope user"));
        Assert.False((await new WingetBackend(run).InstallAsync("a&b")).Item1);
        Assert.Contains(f.Core.Audit.ReadAll(), e => e.Action == "app.done");
    }
}

public class SYS07_Devices
{
    public static string FakeSys(string root)
    {
        var s = Path.Combine(root, "sys");
        void W(string p, string v) { var f = Path.Combine(s, p); Directory.CreateDirectory(Path.GetDirectoryName(f)!); File.WriteAllText(f, v); }
        W("bus/usb/devices/1-2/product", "HD Webcam\n"); W("bus/usb/devices/1-2/manufacturer", "Logitech"); W("bus/usb/devices/1-2/bDeviceClass", "0e");
        W("bus/usb/devices/usb1/product", "xHCI Host"); W("class/net/eth0/operstate", "up"); W("class/net/wlan0/operstate", "down"); W("class/net/lo/operstate", "unknown");
        W("class/drm/card0/dev", "1"); W("class/drm/card0-HDMI-A-1/status", "x"); W("class/sound/card0/id", "HDA Intel"); W("class/power_supply/BAT0/capacity", "87");
        return s;
    }

    [Fact(DisplayName = "SYS-07: Windows-Geräteliste (PnP) wird gelesen, Probleme erkannt, Systemeinträge ausgeblendet")]
    public void Pnp()
    {
        var csv = "\"Class\",\"FriendlyName\",\"Status\"\r\n\"Camera\",\"Integrated Camera\",\"OK\"\r\n\"Printer\",\"HP LaserJet, Büro\",\"Error\"\r\n\"System\",\"PCI Bus\",\"OK\"\r\n\"Display\",\"NVIDIA GeForce RTX 5070\",\"OK\"\r\n";
        var d = DeviceScanner.ParsePnpCsv(csv);
        Assert.Equal(3, d.Count);
        Assert.Contains(d, x => x is { Category: "Drucker", Name: "HP LaserJet, Büro", Problem: true });
        Assert.Contains(d, x => x is { Category: "Anzeige", Problem: false });
    }

    [Fact(DisplayName = "SYS-07: Linux-/sys wird gelesen; Neues und Verschwundenes wird erkannt")]
    public async Task Sys_and_diff()
    {
        using var f = new Fixture();
        var s = new DeviceScanner(new FakeRunner((_, _) => (1, "")), f.Root, FakeSys(f.Root));
        var all = s.ScanSys();
        Assert.Contains(all, x => x is { Category: "Kamera", Name: "HD Webcam (Logitech)" });
        Assert.Contains(all, x => x.Name == "WLAN wlan0" && x.Detail == "nicht verbunden");
        Assert.DoesNotContain(all, x => x.Name.Contains("lo") || x.Name.Contains("xHCI") || x.Name.Contains("HDMI"));
        Assert.Contains(all, x => x is { Category: "Akku", Name: "BAT0 87 %" });
        Assert.Empty(s.Diff(all).Added);
        Directory.Delete(Path.Combine(f.Root, "sys", "bus", "usb", "devices", "1-2"), true);
        var (add, rem) = s.Diff(s.ScanSys());
        Assert.Empty(add); Assert.Single(rem);
        Assert.NotEmpty(await s.ScanAsync());
    }
}

public class SYS06_Share
{
    [Fact(DisplayName = "SYS-06: Freigabe nur mit Token, nur im Ordner, keine versteckten Dateien, läuft ab")]
    public void Rules()
    {
        using var f = new Fixture();
        var d = Path.Combine(f.Root, "fotos"); Directory.CreateDirectory(Path.Combine(d, "urlaub")); File.WriteAllText(Path.Combine(d, "urlaub", "a b.jpg"), "x"); File.WriteAllText(Path.Combine(d, ".geheim"), "x");
        File.WriteAllText(Path.Combine(f.Root, "draussen.txt"), "x");
        var s = new FolderShare(d, TimeSpan.FromHours(1), f.Time);
        Assert.Equal(Path.Combine(d, "urlaub", "a b.jpg"), s.Resolve($"/{s.Token}/urlaub/a%20b.jpg"));
        Assert.Equal(d, s.Resolve($"/{s.Token}/"));
        Assert.Null(s.Resolve("/falsch/urlaub/a%20b.jpg"));
        Assert.Null(s.Resolve($"/{s.Token}/../draussen.txt"));
        Assert.Null(s.Resolve($"/{s.Token}/%2E%2E/draussen.txt"));
        Assert.Null(s.Resolve($"/{s.Token}/.geheim"));
        Assert.Contains("urlaub", s.ListingHtml(d)); Assert.DoesNotContain(".geheim", s.ListingHtml(d));
        f.Time.Advance(TimeSpan.FromHours(2));
        Assert.False(s.Active); Assert.Null(s.Resolve($"/{s.Token}/"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FolderShare(d, TimeSpan.FromDays(3)));
    }

    [Fact(DisplayName = "SYS-06: Nur private Heimnetz-Adressen")]
    public void Lan()
    {
        Assert.True(FolderShare.IsPrivateLan(System.Net.IPAddress.Parse("192.168.1.20")));
        Assert.True(FolderShare.IsPrivateLan(System.Net.IPAddress.Parse("172.20.0.5")));
        Assert.False(FolderShare.IsPrivateLan(System.Net.IPAddress.Parse("8.8.8.8")));
        Assert.False(FolderShare.IsPrivateLan(System.Net.IPAddress.Parse("172.32.0.1")));
    }
}

public class SYS09_Mail
{
    [Fact(DisplayName = "SYS-09: Kontakte speichern, vCard ein- und ausgeben, ungültige Adresse abgelehnt")]
    public void Contacts()
    {
        using var f = new Fixture();
        var m = new MailCenter(f.Root, f.Core.Audit);
        m.AddContact("Anna Müller", "anna@example.de", "0171 1");
        Assert.Throws<ArgumentException>(() => m.AddContact("X", "kein-mail"));
        var vcf = m.ExportVcf();
        var m2 = new MailCenter(Path.Combine(f.Root, "b"), f.Core.Audit);
        Directory.CreateDirectory(Path.Combine(f.Root, "b"));
        Assert.Equal(2, m2.ImportVcf(vcf + "BEGIN:VCARD\nVERSION:3.0\nN:Schmidt;Bernd;;;\nEMAIL;TYPE=WORK:bernd@example.com\nEND:VCARD\n"));
        Assert.Equal(0, m2.ImportVcf(vcf)); // keine Doppelten
        Assert.Equal(["Anna Müller", "Schmidt Bernd"], m2.Contacts().Select(c => c.Name));
        Assert.Single(new MailCenter(f.Root, f.Core.Audit).Find("müller"));
    }

    [Fact(DisplayName = "SYS-09: Entwurf als .eml (Umlaute korrekt, X-Unsent) und mailto; Prüfung findet Fehler")]
    public void Draft()
    {
        var d = new MailDraft("anna@example.de", "Grüße aus Köln", "Hallo Anna,\nanbei das Foto.");
        var eml = MailCenter.ToEml(d, new DateTimeOffset(2026, 10, 21, 20, 0, 0, TimeSpan.FromHours(2)));
        Assert.StartsWith("X-Unsent: 1\r\n", eml); Assert.Contains("+0200", eml);
        var back = MailCenter.ParseEml(eml);
        Assert.Equal("Grüße aus Köln", back.Subject); Assert.Equal("Hallo Anna,\r\nanbei das Foto.", back.Body);
        Assert.Contains("subject=Gr%C3%BC%C3%9Fe", MailCenter.MailTo(d));
        Assert.Contains(MailCenter.Check(d), x => x.Contains("Anhang"));
        Assert.Equal(2, MailCenter.Check(new MailDraft("x@", "", "")).Count);
        Assert.Empty(MailCenter.Check(new MailDraft("a@b.de, c@d.org", "Hi", "Text")));
    }

    [Fact(DisplayName = "SYS-09: Eingegangene E-Mail (multipart, quoted-printable) wird lesbar")]
    public void Read_eml()
    {
        var raw = "From: =?UTF-8?Q?J=C3=BCrgen?= <j@x.de>\r\nTo: me@x.de\r\nSubject: =?UTF-8?B?UmVjaG51bmcgw5xiZXJzaWNodA==?=\r\nContent-Type: multipart/alternative; boundary=\"b1\"\r\n\r\n--b1\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Transfer-Encoding: quoted-printable\r\n\r\nSch=C3=B6ne Gr=C3=BC=C3=9Fe\r\n--b1\r\nContent-Type: text/html\r\n\r\n<b>x</b>\r\n--b1--\r\n";
        var m = MailCenter.ParseEml(raw);
        Assert.Equal("Rechnung Übersicht", m.Subject); Assert.StartsWith("Jürgen", m.From); Assert.Equal("Schöne Grüße", m.Body);
    }
}

public class SPEC12_Forensics
{
    [Fact(DisplayName = "SPEC-12: Beweis wird unverändert mit SHA-256 und Kette gesichert, Veränderung wird erkannt")]
    public void Evidence()
    {
        using var f = new Fixture();
        var lab = new ForensicsLab(f.Root, f.Core.Audit, f.Time);
        var src = Path.Combine(f.Root, "mail.eml"); File.WriteAllText(src, "Betrug");
        var c = lab.Create("Test");
        var it = lab.Add(c, src);
        Assert.Equal(ForensicsLab.Sha256(src), it.Sha256);
        Assert.True(ForensicsLab.Matches(src, it.Sha256.ToUpperInvariant()));
        var (ok, items) = lab.Verify(c); Assert.True(ok); Assert.True(items.Single().Ok);
        var copy = Path.Combine(f.Root, "forensik", c.Id, "beweise", "mail.eml");
        Assert.True(File.GetAttributes(copy).HasFlag(FileAttributes.ReadOnly));
        File.SetAttributes(copy, FileAttributes.Normal); File.WriteAllText(copy, "gefälscht");
        Assert.False(lab.Verify(c).Items.Single().Ok);
        Assert.Contains("VERÄNDERT", File.ReadAllText(lab.Report(c)));
    }

    [Fact(DisplayName = "SPEC-12: Höchstens 120 h, Warnung 24 h vorher, danach vollständig gelöscht")]
    public void Expiry()
    {
        using var f = new Fixture();
        var lab = new ForensicsLab(f.Root, f.Core.Audit, f.Time);
        Assert.Throws<ArgumentOutOfRangeException>(() => lab.Create("x", TimeSpan.FromHours(121)));
        var c = lab.Create("x"); var src = Path.Combine(f.Root, "a.txt"); File.WriteAllText(src, "1"); lab.Add(c, src);
        Assert.Equal(CaseState.Aktiv, lab.State(c));
        f.Time.Advance(TimeSpan.FromHours(97)); Assert.Equal(CaseState.LaeuftBaldAb, lab.State(c));
        Assert.Equal(0, lab.PurgeExpired());
        f.Time.Advance(TimeSpan.FromHours(24)); Assert.Equal(CaseState.Abgelaufen, lab.State(c));
        Assert.Throws<InvalidOperationException>(() => lab.Add(c, src));
        Assert.Equal(1, lab.PurgeExpired());
        Assert.False(Directory.Exists(Path.Combine(f.Root, "forensik", c.Id))); Assert.Empty(lab.Cases());
    }
}
