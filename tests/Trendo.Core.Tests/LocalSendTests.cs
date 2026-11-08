using System.Net;
using System.Text.Json.Nodes;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 9 – SYS-06: LocalSend-Protokoll v2 (Sendeseite), Felder wie in der Protokollbeschreibung.</summary>
public class LocalSendTests
{
    const string Own = "EIGEN";
    static readonly IPAddress Lan = IPAddress.Parse("192.168.1.42");
    const string Phone = """{"alias":"Nice Orange","version":"2.1","deviceModel":"Samsung","deviceType":"mobile","fingerprint":"ABC123","port":53317,"protocol":"https","download":true,"announce":false}""";

    [Fact(DisplayName = "LocalSend +1: Ankündigung mit allen Pflichtfeldern, announce=true, Standard-Port; Multicast-Adresse wie im Protokoll")]
    public void Announcement()
    {
        var o = JsonNode.Parse(LocalSend.Announcement(Own))!;
        Assert.Equal(("2.0", Own, 53317, "desktop", true), (o["version"]!.GetValue<string>(), o["fingerprint"]!.GetValue<string>(), o["port"]!.GetValue<int>(), o["deviceType"]!.GetValue<string>(), o["announce"]!.GetValue<bool>()));
        Assert.Equal(("224.0.0.167", 53317), (LocalSend.MulticastAddress, LocalSend.Port));
    }

    [Fact(DisplayName = "LocalSend +2: Gerät aus UDP lesen – Name, Adresse, HTTPS, Fingerabdruck; IPv4-gemappte Adresse wird normalisiert")]
    public void Peer()
    {
        var p = LocalSend.ParsePeer(Phone, Lan, Own)!;
        Assert.Equal(("Nice Orange (Samsung)", "https://192.168.1.42:53317", "ABC123"), (p.Label, p.BaseUrl, p.Fingerprint));
        Assert.Equal("192.168.1.42", LocalSend.ParsePeer(Phone, Lan.MapToIPv6(), Own)!.Address);
    }

    [Theory(DisplayName = "LocalSend −1: nur eigenes Netz – private Adressen ja, öffentliche nein")]
    [InlineData("192.168.0.5", true)] [InlineData("10.1.2.3", true)] [InlineData("172.16.0.1", true)] [InlineData("172.31.255.1", true)] [InlineData("169.254.1.1", true)] [InlineData("127.0.0.1", true)]
    [InlineData("8.8.8.8", false)] [InlineData("172.32.0.1", false)] [InlineData("193.99.144.80", false)]
    public void Local_only(string ip, bool ok) => Assert.Equal(ok, LocalSend.IsLocal(IPAddress.Parse(ip)));

    [Fact(DisplayName = "LocalSend −2: eigene Nachricht, fremdes Netz, kaputtes JSON, falsche Protokollfassung oder Port werden ignoriert")]
    public void Ignored()
    {
        Assert.Null(LocalSend.ParsePeer(Phone.Replace("ABC123", Own), Lan, Own)); Assert.Null(LocalSend.ParsePeer(Phone, IPAddress.Parse("8.8.8.8"), Own)); Assert.Null(LocalSend.ParsePeer("{kaputt", Lan, Own));
        Assert.Null(LocalSend.ParsePeer(Phone.Replace("\"2.1\"", "\"1.0\""), Lan, Own)); Assert.Null(LocalSend.ParsePeer(Phone.Replace("53317", "70000"), Lan, Own)); Assert.Null(LocalSend.ParsePeer(Phone.Replace("https", "ftp"), Lan, Own));
    }

    [Fact(DisplayName = "LocalSend +3: prepare-upload – info ohne announce, Dateien mit id, Name ohne Pfad, Größe, Typ, sha256; Adressen mit PIN und escapten Parametern")]
    public void Prepare()
    {
        var f = new LocalSend.SendFile("id1", @"C:\x\urlaub bild.jpg", "urlaub bild.jpg", 1234, "aabb", LocalSend.FileType("urlaub bild.jpg"));
        var o = JsonNode.Parse(LocalSend.PrepareBody(Own, [f]))!;
        Assert.Null(o["info"]!["announce"]); Assert.Equal("Trendo OS", o["info"]!["alias"]!.GetValue<string>());
        var j = o["files"]!["id1"]!; Assert.Equal(("urlaub bild.jpg", 1234L, "image/jpeg", "aabb"), (j["fileName"]!.GetValue<string>(), j["size"]!.GetValue<long>(), j["fileType"]!.GetValue<string>(), j["sha256"]!.GetValue<string>()));
        var p = LocalSend.ParsePeer(Phone, Lan, Own)!;
        Assert.Equal("https://192.168.1.42:53317/api/localsend/v2/prepare-upload?pin=12%2034", LocalSend.PrepareUrl(p, " 12 34 "));
        Assert.Equal("https://192.168.1.42:53317/api/localsend/v2/upload?sessionId=s%261&fileId=f&token=t", LocalSend.UploadUrl(p, "s&1", "f", "t"));
        var (sid, t) = LocalSend.ParsePrepare("""{"sessionId":"S","files":{"id1":"tok"}}"""); Assert.Equal(("S", "tok"), (sid, t["id1"]));
    }

    [Fact(DisplayName = "LocalSend +4: Zertifikat muss zum Fingerabdruck passen (SHA-256, Groß/Klein egal); HTTP-Fehler verständlich")]
    public void Certificate_and_errors()
    {
        var cert = new byte[] { 1, 2, 3 }; var fp = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(cert));
        Assert.True(LocalSend.CertificateMatches(cert, fp.ToLowerInvariant())); Assert.False(LocalSend.CertificateMatches([9], fp));
        Assert.Contains("PIN", LocalSend.Error(401)); Assert.Contains("abgelehnt", LocalSend.Error(403)); Assert.Contains("Prüfsumme", LocalSend.Error(422)); Assert.Contains("500", LocalSend.Error(500));
    }
}
