using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Sys;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Paket 01 – Studio 4, S4-06 (07.11.2026): Plugin-Host im Audioeditor – prüfen, freigeben, anwenden, Absturz.</summary>
public class AudioPluginHost_Ui
{
    /// <summary>Minimale 64-Bit-Programmdatei (wird nie ausgeführt) mit Export „GetPluginFactory“ und Import ws2_32!connect.</summary>
    internal static byte[] FakeVst3()
    {
        var b = new byte[0x400]; void U16(int o, int v) => BitConverter.GetBytes((ushort)v).CopyTo(b, o); void U32(int o, uint v) => BitConverter.GetBytes(v).CopyTo(b, o);
        void Str(int o, string s) => Encoding.ASCII.GetBytes(s + "\0").CopyTo(b, o);
        b[0] = (byte)'M'; b[1] = (byte)'Z'; U32(0x3C, 0x40); Str(0x40, "PE\0");
        U16(0x44, 0x8664); U16(0x46, 1); U16(0x54, 240); // Maschine x64, 1 Sektion, Optional Header PE32+
        const int opt = 0x58; U16(opt, 0x20B); U32(opt + 108, 16);
        U32(opt + 112, 0x1000); U32(opt + 116, 40);       // Exportverzeichnis
        U32(opt + 120, 0x1100); U32(opt + 124, 40);       // Importverzeichnis
        const int sec = opt + 240; Str(sec, ".rdata"); U32(sec + 8, 0x200); U32(sec + 12, 0x1000); U32(sec + 16, 0x200); U32(sec + 20, 0x200);
        const int raw = 0x200; int R(uint va) => raw + (int)(va - 0x1000);
        U32(R(0x1000) + 24, 1); U32(R(0x1000) + 32, 0x1040); U32(R(0x1040), 0x1050); Str(R(0x1050), "GetPluginFactory");
        U32(R(0x1100), 0x1140); U32(R(0x1100) + 12, 0x1160); U32(R(0x1100) + 16, 0x1140); // Import-Deskriptor (zweiter bleibt 0)
        U32(R(0x1140), 0x1170); Str(R(0x1160), "WS2_32.dll"); Str(R(0x1170) + 2, "connect");
        return b;
    }

    [AvaloniaFact(DisplayName = "S4-06 Audioeditor: Plugin prüfen (Netz-Hinweis), ohne Freigabe kein Start, freigeben, anwenden → Schritt „Plugin: …“ (rückgängig); Absturz → Bericht, Projekt unverändert")]
    public async Task Host_flow()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            var bundle = Path.Combine(root, "Hall Muster.vst3"); var bin = Path.Combine(bundle, "Contents", "x86_64-win"); Directory.CreateDirectory(bin);
            File.WriteAllBytes(Path.Combine(bin, "Hall Muster.vst3"), FakeVst3());
            const int rate = 16000; var x = new float[rate * 2]; for (var i = 0; i < x.Length; i++) x[i] = (float)(0.4 * Math.Sin(2 * Math.PI * 220 * i / rate));
            var wav = Path.Combine(root, "stimme.wav"); AudioFile.WriteWav(new AudioBuf(rate, [x]), wav, 24);
            var mode = "ok"; var runs = new List<IReadOnlyList<string>>();
            w.AuPluginRunOverride = (args, box) =>
            {
                runs.Add(args);
                if (mode == "absturz") return new Sandbox.Result(unchecked((int)0xC0000005), false, true, false, 80 << 20, 0.3, "", "Fehler im Plugin", "abgestürzt (Zugriffsverletzung, Code 0xC0000005) – nur der abgeschottete Prozess ist betroffen");
                var cmd = args[7];
                if (cmd == "render") { var a = AudioFile.Read(args[9]); AudioFile.WriteWav(new AudioBuf(a.Rate, a.Data.Select(c => c.Select(v => v * 0.5f).ToArray()).ToArray()), args[10], 24); }
                File.WriteAllText(args[cmd == "render" ? 11 : 9], """{"ok": true, "name": "Hall Muster", "art": "Effekt", "parameter": [{"name": "mix", "wert": "0.3", "anzeige": "30 %"}], "spitze_ein_db": -8.0, "spitze_aus_db": -14.0, "veraenderung": 0.5, "sekunden": 0.2}""");
                return new Sandbox.Result(0, false, false, false, 60 << 20, 0.4, "", "", "ok");
            };
            w.AuOpen(wav); w.Show("ton"); Settle();
            w.Find<TextBox>("au-plug-path").Text = bundle;
            var check = w.Find<Button>("au-plug-check"); check.BringIntoView(); Settle(); check.Press(); Settle();
            Assert.True(w.AuPluginChecked!.Loadable, w.AuPluginChecked.Problem);
            Assert.Contains(w.AuPluginChecked.Risks, r => r.Code == "netz");
            Assert.Contains("Noch nicht freigegeben", w.ProgramMessage("ton"));
            var d = w.ProgramActive("ton")!;
            Assert.False(await w.AuPluginRun(d, "render")); Assert.Empty(runs); // ohne Freigabe startet nichts
            w.Show("ton"); Settle();
            w.Find<TextBox>("au-plug-note").Text = "?"; w.Find<Button>("au-plug-approve").Press(); Settle();
            Assert.Contains("woher", w.ProgramMessage("ton"));
            w.Show("ton"); Settle();
            w.Find<TextBox>("au-plug-note").Text = "offizielle Seite des Herstellers"; Settle(); w.Find<Button>("au-plug-approve").Press(); Settle();
            Assert.Contains("freigegeben", w.ProgramMessage("ton"));
            w.Show("ton"); Settle(); w.Find<TextBox>("au-plug-params").Text = "mix=0.3";
            var run = w.Find<Button>("au-plug-run"); run.BringIntoView(); Settle(); run.Press();
            for (var i = 0; i < 200 && d.UndoLabel != "Plugin: Hall Muster"; i++) { await Task.Delay(20); Settle(); }
            Assert.Equal("Plugin: Hall Muster", d.UndoLabel);
            Assert.Contains("--param", runs[^1]); Assert.Contains("mix=0.3", runs[^1]); Assert.Equal("-I", runs[^1][0]);
            Assert.Contains("-14,0 dB", w.ProgramMessage("ton").Replace("-14.0", "-14,0"));
            // Absturz: Bericht, keine Änderung am Projekt
            mode = "absturz"; var before = d.UndoLabel;
            Assert.False(await w.AuPluginRun(d, "render"));
            Assert.Equal(before, d.UndoLabel); Assert.True(File.Exists(w.AuPluginCrashReport!)); Assert.Contains("Zugriffsverletzung", File.ReadAllText(w.AuPluginCrashReport!));
            Assert.Contains("Projekt ist unverändert", w.ProgramMessage("ton"));
            // zurücknehmen → wieder gesperrt
            w.Show("ton"); Settle(); w.Find<Button>("au-plug-revoke").Press(); Settle();
            mode = "ok"; var n = runs.Count; Assert.False(await w.AuPluginRun(d, "info")); Assert.Equal(n, runs.Count);
        }
        finally { w.Close(); }
    }
}
