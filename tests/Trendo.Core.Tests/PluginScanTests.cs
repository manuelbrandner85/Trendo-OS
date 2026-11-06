using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 4, S4-06 (06.11.2026): Plugin-Prüfung ohne Laden.</summary>
public class PluginScanTests
{
    static byte[] Pe(ushort machine)
    {
        var b = new byte[512]; b[0] = (byte)'M'; b[1] = (byte)'Z'; BitConverter.GetBytes(0x80).CopyTo(b, 0x3C);
        b[0x80] = (byte)'P'; b[0x81] = (byte)'E'; BitConverter.GetBytes(machine).CopyTo(b, 0x84); return b;
    }

    [Fact(DisplayName = "S4-06 (Teil Scan): VST3-Bündel mit moduleinfo.json, 32-Bit-Plugin, kaputte Beschreibung und CLAP werden erkannt – ohne Plugin-Code auszuführen; Bericht nennt die Sperre fürs Laden")]
    public void Scan_without_loading()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-plug-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            var vst = Path.Combine(dir, "VST3"); var clap = Path.Combine(dir, "CLAP");
            string Bundle(string name, string arch, string? json)
            {
                var b = Path.Combine(vst, name + ".vst3"); var bin = Path.Combine(b, "Contents", arch); Directory.CreateDirectory(bin);
                File.WriteAllBytes(Path.Combine(bin, name + ".vst3"), Pe(arch == "x86-win" ? (ushort)0x14C : (ushort)0x8664));
                if (json is not null) { var res = Path.Combine(b, "Contents", "Resources"); Directory.CreateDirectory(res); File.WriteAllText(Path.Combine(res, "moduleinfo.json"), json); }
                return b;
            }
            Bundle("Hall Muster", "x86_64-win", """{ "Name": "Hall Muster", "Version": "1.2.0", "Factory Info": { "Vendor": "Beispiel Audio" }, "Classes": [ { "Name": "Hall", "Sub Categories": [ "Fx", "Reverb" ] } ], }""");
            Bundle("Alt32", "x86-win", """{ "Name": "Alt32", "Version": "0.9" }""");
            Bundle("Kaputt", "x86_64-win", "{ das ist kein json");
            Directory.CreateDirectory(clap);
            File.WriteAllBytes(Path.Combine(clap, "Synth Beispiel.clap"), Pe(0x8664));
            File.WriteAllText(Path.Combine(clap, "Fake.clap"), "kein Programm");
            var list = PluginScan.Scan([vst, clap, Path.Combine(dir, "gibt-es-nicht")]);
            Assert.Equal(5, list.Count);
            var hall = list.Single(p => p.Name == "Hall Muster");
            Assert.Equal(("VST3", "Beispiel Audio", "1.2.0", "Fx|Reverb", "x64", "ok"), (hall.Format, hall.Vendor, hall.Version, hall.Category, hall.Arch, hall.Status));
            Assert.Contains("32 Bit", list.Single(p => p.Name == "Alt32").Status);
            Assert.StartsWith("defekt", list.Single(p => p.Name == "Kaputt").Status);
            Assert.Equal("x64", list.Single(p => p.Name == "Synth Beispiel").Arch);
            Assert.StartsWith("defekt", list.Single(p => p.Name == "Fake").Status);
            var rep = PluginScan.Report(list, [vst, clap]);
            Assert.Contains("| VST3 | Hall Muster | Beispiel Audio | 1.2.0 |", rep); Assert.Contains("ohne Laden", rep); Assert.Contains("gesperrt", rep);
            Assert.Contains("Keine VST3", PluginScan.Report([], [vst]));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
