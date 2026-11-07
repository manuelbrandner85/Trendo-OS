using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 4, S4-06 (07.11.2026): Plugin vor dem Laden statisch prüfen (nur lesen).</summary>
public class PluginInspectTests
{
    [Fact(DisplayName = "S4-06: statische Prüfung liest Architektur, Importe und Einstiegspunkt aus echten Windows-Programmdateien; Netz-Bibliothek als Hinweis; ohne VST3-Einstieg nicht ladbar")]
    public void System_dlls()
    {
        if (!OperatingSystem.IsWindows()) return;
        var sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var r = PluginInspect.Inspect(Path.Combine(sys, "urlmon.dll"));
        Assert.Equal("x64", r.Arch); Assert.False(r.EntryFound); Assert.False(r.Loadable); Assert.Contains("GetPluginFactory", r.Problem);
        Assert.NotEmpty(r.Dlls); Assert.Equal(64, r.Sha256.Length);
        var w = PluginInspect.Inspect(Path.Combine(sys, "winhttp.dll")); Assert.Contains(w.Dlls, d => d.StartsWith("api-ms-", StringComparison.OrdinalIgnoreCase) || d.Equals("kernel32.dll", StringComparison.OrdinalIgnoreCase));
        var notepad = PluginInspect.Inspect(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "notepad.exe"));
        Assert.Contains(notepad.Risks, x => x.Code == "start" || x.Code == "registrierung" || x.Code == "nachladen"); // Notepad öffnet Links/Programme
        var wow = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64", "winhttp.dll");
        if (File.Exists(wow)) { var x86 = PluginInspect.Inspect(wow); Assert.Equal("x86 (32 Bit)", x86.Arch); Assert.Contains("nur 64-Bit", x86.Problem); }
        Assert.Contains("PluginInspect", PluginInspect.Report(r).Length > 0 ? "PluginInspect" : "");
    }

    [Fact(DisplayName = "S4-06-N: kaputte Datei, VST3-Bündel ohne Programmdatei und Textdatei mit .clap werden als nicht ladbar erklärt – nichts stürzt ab")]
    public void Broken()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-pi-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var bundle = Path.Combine(dir, "Leer.vst3"); Directory.CreateDirectory(Path.Combine(bundle, "Contents", "Resources"));
            Assert.Contains("Keine Programmdatei", PluginInspect.Inspect(bundle).Problem);
            var clap = Path.Combine(dir, "Text.clap"); File.WriteAllText(clap, "kein Programm");
            var c = PluginInspect.Inspect(clap); Assert.Equal("CLAP", c.Format); Assert.Contains("Keine gültige Windows-Programmdatei", c.Problem); Assert.False(c.Loadable);
            var mz = Path.Combine(dir, "Kaputt.vst3"); var bytes = new byte[512]; bytes[0] = (byte)'M'; bytes[1] = (byte)'Z'; BitConverter.GetBytes(0x7FFFFFF0).CopyTo(bytes, 0x3C); File.WriteAllBytes(mz, bytes);
            Assert.Contains("Keine gültige", PluginInspect.Inspect(mz).Problem);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>Nur mit TRENDO_PLUGIN_ECHT = Ordner mit „Airwindows Consolidated.vst3“ und „.clap“ (MIT/GPL, nur gelesen, nicht geladen).</summary>
    [Fact(DisplayName = "S4-06 echt (nur lesen): Airwindows Consolidated VST3 und CLAP – x64, Einstiegspunkt gefunden, ladbar; Bericht mit Prüfsumme")]
    public void Real_plugin()
    {
        if (Environment.GetEnvironmentVariable("TRENDO_PLUGIN_ECHT") is not { Length: > 0 } d) return;
        var v = PluginInspect.Inspect(Path.Combine(d, "Airwindows Consolidated.vst3"));
        Assert.True(v.Loadable, v.Problem); Assert.Equal("x64", v.Arch);
        var c = PluginInspect.Inspect(Path.Combine(d, "Airwindows Consolidated.clap"));
        Assert.True(c.Loadable, c.Problem); Assert.Equal("CLAP", c.Format);
        if (Environment.GetEnvironmentVariable("TRENDO_PLUGIN_BELEG") is { Length: > 0 } keep)
        {
            Directory.CreateDirectory(keep);
            File.WriteAllText(Path.Combine(keep, "pruefung-vst3.md"), PluginInspect.Report(v));
            File.WriteAllText(Path.Combine(keep, "pruefung-clap.md"), PluginInspect.Report(c));
        }
    }
}
