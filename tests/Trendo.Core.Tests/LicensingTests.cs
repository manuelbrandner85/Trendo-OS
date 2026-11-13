using System.Security.Cryptography;
using System.Text;
using Trendo.Core;
using Trendo.Core.Licensing;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Commercial Phase 2: signierte Berechtigungen und Editionsprüfung. Der Testschlüssel entsteht im Test und wird nirgends gespeichert.</summary>
public class LicensingTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    sealed class Server : IDisposable
    {
        public readonly ECDsa Key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        public Dictionary<string, string> Trusted => new() { ["test-1"] = Convert.ToBase64String(Key.ExportSubjectPublicKeyInfo()) };
        public string Sign(Entitlement e)
        {
            var head = LicenseVerifier.Prefix + "." + LicenseVerifier.EncodePayload(e);
            return head + "." + B64Url.Encode(Key.SignData(Encoding.ASCII.GetBytes(head), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        }
        public void Dispose() => Key.Dispose();
    }

    static Entitlement Complete(string? device = null, DateTimeOffset? until = null, string[]? features = null, LicenseType type = LicenseType.Complete) =>
        new(1, "L-0001", type, "Erika Muster", device, 1, Now.AddDays(-1), until, Now.AddYears(1), features ?? ["*"], "test-1");

    [Fact]
    public void Gueltiger_Code_wird_erkannt()
    {
        using var s = new Server();
        var (e, err) = new LicenseVerifier(s.Trusted).Verify(s.Sign(Complete()));
        Assert.Equal("", err);
        Assert.Equal(LicenseType.Complete, e!.Type);
        Assert.Equal("Erika Muster", e.Licensee);
    }

    [Fact]
    public void Veraenderter_Code_wird_abgelehnt()
    {
        using var s = new Server();
        var token = s.Sign(Complete(features: ["SYS-10"]));
        var parts = token.Split('.');
        var forged = parts[0] + "." + LicenseVerifier.EncodePayload(Complete(features: ["*"])) + "." + parts[2];
        Assert.Null(new LicenseVerifier(s.Trusted).Verify(forged).Value);
    }

    [Fact]
    public void Fremder_Schluessel_wird_abgelehnt()
    {
        using var echt = new Server();
        using var fremd = new Server();
        var (e, err) = new LicenseVerifier(echt.Trusted).Verify(fremd.Sign(Complete()));
        Assert.Null(e);
        Assert.Contains("Signatur", err);
    }

    [Theory]
    [InlineData("")]
    [InlineData("TRENDO1.abc")]
    [InlineData("XYZ.abc.def")]
    [InlineData("TRENDO1.@@@.def")]
    [InlineData("TRENDO1.e30.AAAA")]
    public void Kaputte_Codes_werfen_nicht(string token)
    {
        using var s = new Server();
        Assert.Null(new LicenseVerifier(s.Trusted).Verify(token).Value);
    }

    [Fact]
    public void Ohne_Serverschluessel_bleibt_alles_frei()
    {
        Assert.Empty(LicenseKeys.Production); // kein Schlüssel im Code, bis der Inhaber einen Server betreibt
        var st = EditionPolicy.Evaluate(null, "geraet", Now, null, enforced: false);
        Assert.False(st.Enforced);
        Assert.True(st.Allows("CORE-06"));
    }

    [Fact]
    public void Free_erlaubt_nur_freie_Studios()
    {
        var st = EditionPolicy.Evaluate(null, "geraet", Now, null, enforced: true);
        Assert.Equal(LicenseType.Free, st.Type);
        Assert.True(st.Allows("SYS-10"));
        Assert.False(st.Allows("CORE-06"));
    }

    [Fact]
    public void Complete_erlaubt_alles_und_Feature_Liste_begrenzt()
    {
        Assert.True(EditionPolicy.Evaluate(Complete(), "g", Now, null, true).Allows("CORE-06"));
        var teil = EditionPolicy.Evaluate(Complete(features: ["CORE-05"]), "g", Now, null, true);
        Assert.True(teil.Allows("CORE-05"));
        Assert.False(teil.Allows("CORE-06"));
    }

    [Fact]
    public void Abgelaufen_anderes_Geraet_und_zurueckgestellte_Uhr_fallen_auf_Free()
    {
        Assert.False(EditionPolicy.Evaluate(Complete(until: Now.AddDays(-1), type: LicenseType.Trial), "g", Now, null, true).Allows("CORE-06"));
        Assert.False(EditionPolicy.Evaluate(Complete(device: "anderes"), "g", Now, null, true).Allows("CORE-06"));
        Assert.False(EditionPolicy.Evaluate(Complete(until: Now.AddDays(10), type: LicenseType.Trial), "g", Now, Now.AddDays(5), true).Allows("CORE-06"));
        Assert.True(EditionPolicy.Evaluate(Complete(until: Now.AddDays(10), type: LicenseType.Trial), "g", Now, Now.AddHours(1), true).Allows("CORE-06"));
    }

    [Fact]
    public void Updates_nur_im_Zeitraum()
    {
        var st = EditionPolicy.Evaluate(Complete(), "g", Now, null, true);
        Assert.True(st.UpdatesCover(Now.AddMonths(6)));
        Assert.False(st.UpdatesCover(Now.AddYears(2)));
    }

    [Fact]
    public void Store_speichert_nur_gepruefte_Codes_mit_Protokoll()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-lic-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            using var s = new Server();
            var audit = new AuditLog(Path.Combine(root, "audit.jsonl"));
            var store = new LicenseStore(root, new LicenseVerifier(s.Trusted), audit);
            var id = store.DeviceId;
            Assert.Matches("^[0-9a-f]{32}$", id);
            Assert.Equal(id, store.DeviceId);

            Assert.False(store.Install("TRENDO1.kaputt.x", "test").Ok);
            Assert.False(store.Install(s.Sign(Complete(device: "anderes")), "test").Ok);
            Assert.Null(store.Current());

            Assert.True(store.Install(s.Sign(Complete(device: id)), "test").Ok);
            Assert.Equal(LicenseType.Complete, store.Status(Now).Type);
            store.Remove("test");
            Assert.Equal(LicenseType.Free, store.Status(Now).Type);
            Assert.True(audit.Verify());
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }

    [Fact]
    public void Keine_Owner_Mail_und_kein_privater_Schluessel_im_Client()
    {
        var src = File.ReadAllText(Path.Combine(Repo(), "src", "Trendo.Core", "Licensing", "Licensing.cs"));
        Assert.DoesNotContain("@", src.Replace("@\"", "").Replace("&lt;", ""));
        Assert.DoesNotContain("PRIVATE KEY", src);
        Assert.DoesNotContain("ImportECPrivateKey", src);
        Assert.DoesNotContain("ImportPkcs8PrivateKey", src);
    }

    [Fact]
    public void Version_kommt_aus_einer_Quelle()
    {
        // Lizenzen mit Update-Zeitraum brauchen eine eindeutige Version: Trendo.App.csproj muss zur neuesten Versionsnotiz passen (release.sh prüft dieselbe Datei)
        var csproj = File.ReadAllText(Path.Combine(Repo(), "src", "Trendo.App", "Trendo.App.csproj"));
        var m = System.Text.RegularExpressions.Regex.Match(csproj, "<Version>([0-9.]+)</Version>");
        Assert.True(m.Success);
        var newest = Directory.GetFiles(Path.Combine(Repo(), "installer", "neuerungen"), "*.md")
            .Select(f => Version.TryParse(Path.GetFileNameWithoutExtension(f), out var v) ? v : null).OfType<Version>().Max();
        Assert.Equal(newest, Version.Parse(m.Groups[1].Value));
    }

    static string Repo()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) d = d.Parent;
        return d!.FullName;
    }
}
