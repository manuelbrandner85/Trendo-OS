using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 4, S4-06 (07.11.2026): Plugin-Host – Freigabe, Ablehnung, Aufruf, Ergebnis, Übernahme in den Audioeditor.</summary>
public class PluginHostTests
{
    static PluginInspect.Result Fake(string sha, string? problem = null, string format = "VST3", bool entry = true) =>
        new(@"C:\Plugins\Test.vst3", @"C:\Plugins\Test.vst3\Contents\x86_64-win\Test.vst3", format, "x64", sha, 1000, false, entry, ["KERNEL32.dll"], [], problem);

    [Fact(DisplayName = "S4-06: Freigabe gilt nur für genau diese Datei (SHA-256); ohne Herkunft, nicht ladbar oder CLAP wird abgelehnt; Freigabe zurücknehmen geht")]
    public void Approvals()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ph-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            var ap = new PluginHost.Approvals(root); var a = Fake(new string('a', 64));
            Assert.Contains("Noch nicht freigegeben", PluginHost.Refuse(a, ap));
            Assert.Contains("woher", Assert.Throws<InvalidOperationException>(() => ap.Approve(a, "?")).Message);
            ap.Approve(a, "offizielle Seite des Herstellers");
            Assert.Null(PluginHost.Refuse(a, ap));
            Assert.Contains("Noch nicht freigegeben", PluginHost.Refuse(a with { Sha256 = new string('b', 64) }, ap)); // Datei verändert → Freigabe gilt nicht
            Assert.Throws<InvalidOperationException>(() => ap.Approve(Fake(new string('c', 64), "Architektur x86"), "Herstellerseite"));
            Assert.Contains("CLAP", PluginHost.Refuse(Fake(new string('a', 64), format: "CLAP") with { Path = "x.clap" }, ap));
            Assert.Equal("x86", PluginHost.Refuse(Fake(new string('d', 64), "x86"), ap));
            Assert.True(ap.Revoke(a.Sha256)); Assert.Contains("Noch nicht freigegeben", PluginHost.Refuse(a, ap));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "S4-06: Parameter „name=wert“, Host-Aufruf mit -I und Site-Ordner, Ergebnis-JSON, Absturzbericht")]
    public void Args_parse_report()
    {
        Assert.Equal([("mix", "0.5"), ("type", "Hall")], PluginHost.ParseParams("mix=0.5; type=Hall"));
        Assert.Throws<ArgumentException>(() => PluginHost.ParseParams("nurtext"));
        var a = PluginHost.Args("p.py", "site", "render", "x.vst3", "erg.json", "ein.wav", "aus.wav", "voll.vstpreset", [("mix", "0.5")], 1.5);
        Assert.Equal(["-I", "-X", "utf8", "-B", "p.py", "--site", "site", "render", "x.vst3", "ein.wav", "aus.wav", "erg.json", "--preset", "voll.vstpreset", "--param", "mix=0.5", "--nachlauf", "1.5"], a);
        var o = PluginHost.Parse("""{"ok": true, "name": "Hall", "art": "Effekt", "parameter": [{"name": "mix", "wert": "0.5", "anzeige": "50 %"}], "spitze_ein_db": -6.0, "spitze_aus_db": -4.5, "veraenderung": 0.3, "sekunden": 1.2}""");
        Assert.True(o.Ok); Assert.Equal("Hall", o.Name); Assert.Equal("50 %", o.Params[0].Label); Assert.Equal(-4.5, o.OutPeakDb);
        Assert.Contains("CLAP", PluginHost.Parse("""{"ok": false, "fehler": "RuntimeError: CLAP lädt dieser Host noch nicht"}""").Error);
        var crash = PluginHost.CrashReport(Fake(new string('e', 64)), new Trendo.Core.Sys.Sandbox.Result(unchecked((int)0xC0000005), false, true, false, 50 << 20, 0.4, "", "Traceback …", "abgestürzt (Zugriffsverletzung)"), "Ton bearbeiten");
        Assert.Contains("Zugriffsverletzung", crash); Assert.Contains("nicht betroffen", crash); Assert.Contains(new string('e', 64), crash);
    }

    /// <summary>Echter Lauf nur mit TRENDO_PLUGINHOST_ENV (uv-Umgebung mit Pedalboard), TRENDO_PLUGINHOST_SCRIPT (trendo_plugin.py) und
    /// TRENDO_PLUGIN_CRASH (selbst gebautes Absturz-Plugin); optional TRENDO_PLUGIN_GOOD (freigegebenes VST3) und TRENDO_PLUGIN_BELEG.</summary>
    [Fact(DisplayName = "S4-06 echt: Host in der Abschottung – abstürzendes Plugin wird isoliert gemeldet (Absturzbericht, nichts übernommen); nicht freigegebenes Plugin startet gar nicht; freigegebenes VST3 verarbeitet Ton")]
    public void Real_sandbox_host()
    {
        string? env = Environment.GetEnvironmentVariable("TRENDO_PLUGINHOST_ENV"), script = Environment.GetEnvironmentVariable("TRENDO_PLUGINHOST_SCRIPT"), crash = Environment.GetEnvironmentVariable("TRENDO_PLUGIN_CRASH");
        if (env is null || script is null || crash is null || !OperatingSystem.IsWindows()) return;
        var keep = Environment.GetEnvironmentVariable("TRENDO_PLUGIN_BELEG"); var log = new List<string>();
        var root = Path.Combine(Path.GetTempPath(), "trendo-phr-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(root);
        try
        {
            var py = PluginHost.BasePython(env); Assert.NotNull(py);
            var ap = new PluginHost.Approvals(Path.Combine(root, "daten"));
            const int rate = 48000; var sine = new float[rate * 2]; for (var i = 0; i < sine.Length; i++) sine[i] = (float)(0.3 * Math.Sin(2 * Math.PI * 440 * i / rate));
            Trendo.Core.Sys.Sandbox.Result Host(PluginInspect.Result r, string cmd, string box, params (string, string)[] ps)
            {
                Directory.CreateDirectory(box); var inp = Path.Combine(box, "ein.wav"); AudioFile.WriteWav(new AudioBuf(rate, [sine, (float[])sine.Clone()]), inp, 24);
                return Trendo.Core.Sys.Sandbox.Run(py!, PluginHost.Args(script!, PluginHost.SitePackages(env!), cmd, r.Binary, Path.Combine(box, "ergebnis.json"), inp, Path.Combine(box, "aus.wav"), parameters: ps),
                    box, new Trendo.Core.Sys.Sandbox.Options(TimeoutMs: 120_000, MemoryBytes: 3L << 30));
            }
            // Ohne Freigabe startet gar nichts (selbst gebaute Testdatei mit VST3-Einstieg, statisch geprüft)
            var c = PluginInspect.Inspect(crash); Assert.True(c.Loadable, c.Problem); log.Add($"Testdatei geprüft: ladbar={c.Loadable}, SHA-256 {c.Sha256}");
            Assert.Contains("Noch nicht freigegeben", PluginHost.Refuse(c, ap)); log.Add("Ohne Freigabe: " + PluginHost.Refuse(c, ap));
            // Absturz im Host-Prozess (Selbsttest „absturztest“: echte Zugriffsverletzung, wie ein abstürzendes Plugin) → isoliert gemeldet
            var box = Path.Combine(root, "box-absturz"); Directory.CreateDirectory(box);
            var cr = Trendo.Core.Sys.Sandbox.Run(py!, PluginHost.Args(script!, PluginHost.SitePackages(env!), "absturztest", "-", Path.Combine(box, "ergebnis.json")), box, new Trendo.Core.Sys.Sandbox.Options(TimeoutMs: 60_000));
            log.Add($"Absturz im Host (abgeschottet): {cr.Text} (Code 0x{unchecked((uint)cr.Exit):X8}, {cr.Seconds:0.0} s)");
            Assert.True(cr.Crashed, cr.Text + " | " + cr.Err); Assert.Contains("Zugriffsverletzung", cr.Text);
            var report = PluginHost.CrashReport(c, cr, "Selbsttest Absturz");
            if (keep is not null) { Directory.CreateDirectory(keep); File.WriteAllText(Path.Combine(keep, "absturzbericht.md"), report); }
            // Echtes abstürzendes Plugin: die Testdatei meldet eine VST3-Fabrik, deren erster Aufruf auf Adresse 0 schreibt → Absturz im Host, isoliert
            ap.Approve(c, "selbst gebaute Testdatei (Absturztest)");
            var tr = Host(c, "render", Path.Combine(root, "box-testdatei"));
            log.Add($"Abstürzendes Plugin geladen (abgeschottet): {tr.Text} (Code 0x{unchecked((uint)tr.Exit):X8}, {tr.Seconds:0.0} s)");
            Assert.True(tr.Crashed, tr.Text + " | " + tr.Err); Assert.False(File.Exists(Path.Combine(root, "box-testdatei", "aus.wav")));
            if (keep is not null) File.WriteAllText(Path.Combine(keep, "absturzbericht-plugin.md"), PluginHost.CrashReport(c, tr, "Ton bearbeiten"));
            // gutes Plugin (nur wenn ausdrücklich angegeben)
            if (Environment.GetEnvironmentVariable("TRENDO_PLUGIN_GOOD") is { Length: > 0 } good)
            {
                var g = PluginInspect.Inspect(good); Assert.True(g.Loadable, g.Problem); ap.Approve(g, "offizielles GitHub-Release des Herstellers");
                var info = Host(g, "info", Path.Combine(root, "box-info"));
                var io = PluginHost.Parse(File.ReadAllText(Path.Combine(root, "box-info", "ergebnis.json")));
                log.Add($"Info: {info.Text}, Name {io.Name}, {io.Kind}, {io.Params.Count} Parameter: " + string.Join(", ", io.Params.Take(8).Select(p => $"{p.Name}={p.Value}")));
                Assert.True(info.Ok && io.Ok, info.Text + io.Error + info.Err);
                var paramText = Environment.GetEnvironmentVariable("TRENDO_PLUGIN_GOOD_PARAMS") ?? "";
                var rr = Host(g, "render", Path.Combine(root, "box-ton"), [.. PluginHost.ParseParams(paramText)]);
                var ro = PluginHost.Parse(File.ReadAllText(Path.Combine(root, "box-ton", "ergebnis.json")));
                log.Add($"Ton: {rr.Text} in {rr.Seconds:0.0} s, Spitze {ro.InPeakDb} → {ro.OutPeakDb} dB, Veränderung {ro.Changed}, Speicher {rr.PeakMemory >> 20} MB" + (ro.Error is null ? "" : ", Fehler " + ro.Error));
                Assert.True(rr.Ok && ro.Ok, rr.Text + ro.Error + rr.Err);
                var outBuf = AudioFile.Read(Path.Combine(root, "box-ton", "aus.wav")); Assert.Equal(rate, outBuf.Rate); Assert.True(outBuf.Length >= sine.Length);
                if (keep is not null) File.Copy(Path.Combine(root, "box-ton", "aus.wav"), Path.Combine(keep, "ton-nach-plugin.wav"), true);
            }
            if (keep is not null) File.WriteAllLines(Path.Combine(keep, "host-lauf.txt"), log);
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    [Fact(DisplayName = "S4-06: Audio-Schritt „Plugin angewendet“ ersetzt den Bereich durch die Plugin-Ausgabe (rückgängig über die Schrittliste)")]
    public void Edit_op()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-phe-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var x = new AudioBuf(8000, [Enumerable.Repeat(0.1f, 8000).ToArray()]);
            var f = Path.Combine(dir, "aus.wav"); AudioFile.WriteWav(new AudioBuf(8000, [Enumerable.Repeat(0.5f, 4000).ToArray()]), f, 24);
            AudioEdit.Apply(x, AudioEdit.Op("plugin", 0.25, 0.75, 0, f));
            Assert.Equal(8000, x.Length); Assert.Equal(0.1f, x.Data[0][100], 3); Assert.Equal(0.5f, x.Data[0][4000], 3); Assert.Equal(0.1f, x.Data[0][7000], 3);
            Assert.Equal("Plugin angewendet", AudioEdit.Label("plugin"));
            Assert.Throws<InvalidOperationException>(() => AudioEdit.Apply(x, AudioEdit.Op("plugin", 0, 0, 0, Path.Combine(dir, "fehlt.wav"))));
        }
        finally { Directory.Delete(dir, true); }
    }
}
