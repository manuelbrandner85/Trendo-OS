using System.Net;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Handy verbinden – ganz einfach (Vorgabe 27.10.2026): Codes, Geräte, erlaubte Adressen.</summary>
public class PhoneAccessTests
{
    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-phone-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "Handy: Kopplungscode gilt einmal und 10 Minuten, Gerät bekommt eigenen Schlüssel, Entfernen sperrt es")]
    public void Offer_redeem_verify_remove()
    {
        var d = Temp();
        try
        {
            var a = new PhoneAccess(d); var t0 = new DateTime(2026, 10, 27, 10, 0, 0, DateTimeKind.Utc);
            var o = a.NewOffer(t0);
            Assert.Null(a.Redeem("falsch", "x", t0));
            Assert.Null(a.Redeem(o.Secret, "x", t0.AddMinutes(11)));
            o = a.NewOffer(t0);
            var r = a.Redeem(o.Secret, "Pixel 9\u0007", t0.AddMinutes(1));
            Assert.NotNull(r);
            Assert.Equal("Pixel 9", r!.Value.Device.Name);
            Assert.Null(a.Redeem(o.Secret, "noch einmal", t0.AddMinutes(1))); // nur einmal
            Assert.Equal(r.Value.Device.Id, new PhoneAccess(d).Verify(r.Value.Token)!.Id); // überlebt Neustart
            Assert.DoesNotContain(r.Value.Token, File.ReadAllText(Path.Combine(d, "handy", "verbundene-geraete.json"))); // nur der Hash steht in der Datei
            Assert.Null(a.Verify(new string('0', 64)));
            Assert.True(a.Remove(r.Value.Device.Id));
            Assert.Null(a.Verify(r.Value.Token));
        }
        finally { Directory.Delete(d, true); }
    }

    [Theory(DisplayName = "Handy: nur Heimnetz und Tailscale, nie öffentliche Adressen")]
    [InlineData("192.168.178.20", true)]
    [InlineData("10.0.0.5", true)]
    [InlineData("172.20.1.1", true)]
    [InlineData("100.101.102.103", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("100.20.1.1", false)]
    [InlineData("172.32.0.1", false)]
    [InlineData("2001:db8::1", false)]
    public void Allowed(string ip, bool ok) => Assert.Equal(ok, PhoneAccess.AllowedAddress(IPAddress.Parse(ip)));

    [Fact(DisplayName = "Handy: QR-Code passt auch mit Tailscale-Adresse in den Code")]
    public void Qr_fits()
    {
        var url = PhoneAccess.PairUrl(IPAddress.Parse("100.101.102.103"), PhoneAccess.DefaultPort, new string('a', 32));
        Assert.True(QrMatrix.Encode(url).GetLength(0) >= 21);
    }

    [Fact(DisplayName = "Handy (Befund 27.10.): QR-Code nimmt die echte WLAN-Karte – nicht Tailscale, nicht Hyper-V/WSL")]
    public void Home_address_choice()
    {
        PhoneAccess.Nic N(string ip, string name, bool gw, bool virt, bool wifi) => new(IPAddress.Parse(ip), name, gw, virt, wifi);
        var list = PhoneAccess.HomeAddresses([
            N("172.20.16.1", "vEthernet (WSL)", false, true, false),
            N("100.101.102.103", "Tailscale", false, false, false),
            N("10.0.0.5", "Ethernet 2", false, false, false),
            N("192.168.178.20", "WLAN", true, false, true),
        ]);
        Assert.Equal(["192.168.178.20", "10.0.0.5", "172.20.16.1"], list.Select(a => a.ToString()));
        Assert.True(PhoneAccess.LooksVirtual("Hyper-V Virtual Ethernet Adapter vEthernet (Default Switch)"));
        Assert.False(PhoneAccess.LooksVirtual("Intel(R) Wi-Fi 6E AX211 160MHz WLAN"));
        var fw = PhoneAccess.FirewallArgs(47_823);
        Assert.Contains("localport=47823", fw); Assert.Contains("remoteip=localsubnet,100.64.0.0/10", fw); Assert.Contains("dir=in", fw);
    }

    [Fact(DisplayName = "Handy: Unterwegs-Übergabe (einmal, 2 Minuten) gibt demselben Handy einen zweiten Schlüssel; Bedienen-Erlaubnis bleibt gespeichert")]
    public void Transfer_and_remote()
    {
        var d = Temp();
        try
        {
            var a = new PhoneAccess(d); var t0 = new DateTime(2026, 10, 27, 10, 0, 0, DateTimeKind.Utc);
            var r = a.Redeem(a.NewOffer(t0).Secret, "Pixel", t0)!.Value;
            Assert.False(r.Device.Remote);
            var code = a.NewTransfer(r.Device.Id, t0);
            Assert.Null(a.RedeemTransfer(code, t0.AddMinutes(3))); // abgelaufen
            code = a.NewTransfer(r.Device.Id, t0);
            var r2 = a.RedeemTransfer(code, t0.AddMinutes(1))!.Value;
            Assert.Null(a.RedeemTransfer(code, t0.AddMinutes(1))); // nur einmal
            Assert.Equal(r.Device.Id, a.Verify(r2.Token)!.Id);
            Assert.Equal(r.Device.Id, a.Verify(r.Token)!.Id); // alter Schlüssel gilt weiter
            Assert.Single(a.Devices());
            Assert.True(a.SetRemote(r.Device.Id, true));
            Assert.True(new PhoneAccess(d).Devices()[0].Remote);
            a.MarkReached(IPAddress.Parse("192.168.178.30"), t0);
            Assert.Equal("192.168.178.30", a.LastReach!.Value.From.ToString());
            // ältere Gerätedatei ohne die neuen Felder wird weiter gelesen
            File.WriteAllText(Path.Combine(d, "handy", "verbundene-geraete.json"), """[{"Id":"abc","Name":"Alt","TokenHash":"00","Paired":"2026-10-01T00:00:00Z","LastSeen":"2026-10-01T00:00:00Z"}]""");
            var old = Assert.Single(new PhoneAccess(d).Devices());
            Assert.False(old.Remote); Assert.Null(old.More);
        }
        finally { Directory.Delete(d, true); }
    }
}
