using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Nur die abgestimmte Schnittstelle. Kein Download, kein Knopf.</summary>
public class Paket44_ProgramInstaller
{
    [Fact(DisplayName = "Paket 44: Angebot darf Prüfsumme und Größe in Bytes leer lassen")]
    public void Angebot_darf_sha_und_bytes_leer_lassen()
    {
        var leer = new ProgramOffer("Blender", "Offizielle Seite", null, "GPL-3.0", "Kostenlos", true, false, null, null, null);
        Assert.Null(leer.Sha256);
        Assert.Null(leer.SizeBytes);
        var voll = leer with { Sha256 = new string('a', 64), SizeBytes = 400_000_000L };
        Assert.Equal(64, voll.Sha256!.Length);
        Assert.Equal(400_000_000L, voll.SizeBytes);
    }

    [Fact(DisplayName = "Paket 44: ein Fake-Installer lädt von selbst nichts")]
    public async Task Fake_laedt_nichts_von_selbst()
    {
        var fake = new FakeInstaller();
        Assert.Empty(fake.Calls);
        Assert.Null(await fake.GetOfferAsync("blender", CancellationToken.None));
        Assert.Equal(["offer"], fake.Calls);
        Assert.Empty(fake.Installed);
    }

    sealed class FakeInstaller : IProgramInstaller
    {
        public List<string> Calls { get; } = [];
        public List<string> Installed { get; } = [];
        public Task<ProgramOffer?> GetOfferAsync(string catalogId, CancellationToken ct) { Calls.Add("offer"); return Task.FromResult<ProgramOffer?>(null); }
        public Task<InstallResult> InstallAsync(string catalogId, IProgress<InstallProgress> progress, CancellationToken ct)
        {
            Calls.Add("install");
            Installed.Add(catalogId);
            return Task.FromResult(new InstallResult(false, "nicht aufgerufen"));
        }
        public Task<bool> IsInstalledAsync(string catalogId, CancellationToken ct) { Calls.Add("is"); return Task.FromResult(false); }
    }
}
