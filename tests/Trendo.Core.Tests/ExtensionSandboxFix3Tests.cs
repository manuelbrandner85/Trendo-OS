using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Trendo.Core;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0766 dritte Prüfrunde (fix3): N12 (Übersetzungs-Kontingent, Befehlsgrenze), N13 (genaue Grenzmeldungen), N14 (Wiederherstellen nach normalisiertem Pfad).</summary>
[Collection("T0766-Sandbox")] // N12: gemeinsames Übersetzungs-Kontingent – nicht parallel zu anderen Sandbox-Tests
public class T0766_Gegenpruefung3(ITestOutputHelper log)
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-gp3-t-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }
    static readonly SandboxLimits Fast = SandboxLimits.Default with { Time = TimeSpan.FromSeconds(10) };

    static byte[] Leb(uint v) { var l = new List<byte>(); do { var b = (byte)(v & 0x7F); v >>= 7; if (v != 0) b |= 0x80; l.Add(b); } while (v != 0); return [.. l]; }
    static IEnumerable<byte> Section(byte id, byte[] body) => [id, .. Leb((uint)body.Length), .. body];
    static byte[] LocalsModule(int funcs, uint locals, byte type)
    {
        byte[] body = [.. Leb(1), .. Leb(locals), type, 0x0B];
        var fs = new List<byte>(Leb((uint)funcs)); for (var i = 0; i < funcs; i++) fs.Add(0);
        var code = new List<byte>(Leb((uint)funcs)); for (var i = 0; i < funcs; i++) { code.AddRange(Leb((uint)body.Length)); code.AddRange(body); }
        return [0, (byte)'a', (byte)'s', (byte)'m', 1, 0, 0, 0, .. Section(1, [1, 0x60, 0, 0]), .. Section(3, [.. fs]), .. Section(10, [.. code])];
    }

    static void WaitIdle() { var end = Environment.TickCount64 + 30_000; while (ExtensionSandbox.CompilesRunning > 0 && Environment.TickCount64 < end) Thread.Sleep(50); }

    [Fact(DisplayName = "T-0766 N12: verworfene Übersetzungen belegen das Kontingent bis zum Ende – höchstens 2 gleichzeitig, sonst „übersetzt noch“; höchstens 10 Befehle")]
    public void N12_compile_slots()
    {
        WaitIdle();
        var heavy = LocalsModule(20_000, 10, 0x7B); // ~3 s Übersetzen
        var max = 0; var busy = 0; var stop = false;
        var sampler = new Thread(() => { while (!Volatile.Read(ref stop)) { max = Math.Max(max, ExtensionSandbox.CompilesRunning); Thread.Sleep(5); } });
        sampler.Start();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 10; i++)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
            var r = ExtensionSandbox.Run(heavy, [], [], new(), Fast with { Time = TimeSpan.FromMinutes(1) }, cts.Token);
            Assert.False(r.Ok);
            if (r.Error == ExtensionSandbox.CompileBusy) busy++;
        }
        // auch ein kleines Modul bekommt keinen dritten Platz, solange zwei Übersetzungen laufen
        if (ExtensionSandbox.CompilesRunning == ExtensionSandbox.MaxParallelCompiles)
        {
            var small = ExtensionSandbox.Run(ExtensionModuleSample.Wasm, [], "a\n"u8.ToArray(), new(), Fast);
            Assert.Equal(ExtensionSandbox.CompileBusy, small.Error); busy++;
        }
        var burst = sw.Elapsed;
        WaitIdle();
        Volatile.Write(ref stop, true); sampler.Join();
        log.WriteLine($"10 Not-Stopps in {burst.TotalSeconds:0.0} s, gleichzeitig höchstens {max} Übersetzungen, {busy}× „übersetzt noch“");
        Assert.True(max <= ExtensionSandbox.MaxParallelCompiles, max.ToString());
        Assert.True(busy >= 1);
        Assert.Equal(0, ExtensionSandbox.CompilesRunning);
        var ok = ExtensionSandbox.Run(ExtensionModuleSample.Wasm, [], "a\n"u8.ToArray(), new(), Fast);
        Assert.True(ok.Ok, ok.Error);

        var d = ExtensionModuleSample.Write(Tmp()); var mf = Path.Combine(d, Extensions.ManifestFile);
        var m = JsonSerializer.Deserialize<ExtManifest>(File.ReadAllText(mf))!;
        File.WriteAllText(mf, JsonSerializer.Serialize(m with { Commands = [.. Enumerable.Range(0, 11).Select(i => new ExtCommand("b" + i, "Befehl " + i, [".csv"], ".md"))] }));
        Assert.Contains(Extensions.Validate(d).Errors, e => e.Contains("Höchstens 10 Befehle"));
        File.WriteAllText(mf, JsonSerializer.Serialize(m with { Commands = [.. Enumerable.Range(0, 10).Select(i => new ExtCommand("b" + i, "Befehl " + i, [".csv"], ".md"))] }));
        Assert.Empty(Extensions.Validate(d).Errors);
    }

    static byte[] Writer(int bytes) => Wasmtime.Module.ConvertText($$"""
        (module
          (import "trendo" "daten_schreiben" (func $put (param i32 i32 i32 i32) (result i32)))
          (import "wasi_snapshot_preview1" "fd_read" (func $read (param i32 i32 i32 i32) (result i32)))
          (import "wasi_snapshot_preview1" "proc_exit" (func $exit (param i32)))
          (memory (export "memory") 20)
          (func (export "_start")
            (i32.store (i32.const 0) (i32.const 1000)) (i32.store (i32.const 4) (i32.const 600))
            (drop (call $read (i32.const 0) (i32.const 0) (i32.const 1) (i32.const 8)))
            (call $exit (call $put (i32.const 1000) (i32.load (i32.const 8)) (i32.const 65536) (i32.const {{bytes}})))))
        """);

    [Fact(DisplayName = "T-0766 N13: Eintragsgrenze und Größengrenze haben eigene Meldungen; „entfernt“ nur, wenn wirklich etwas entfernt wurde")]
    public void N13_messages()
    {
        var data = Path.Combine(Tmp(), "daten"); Directory.CreateDirectory(data);
        for (var i = 0; i < ExtensionSandbox.MaxDataFiles - 1; i++) Directory.CreateDirectory(Path.Combine(data, "d" + i));
        var entries = ExtensionSandbox.Run(Writer(5), [], "neu/datei.txt"u8.ToArray(), new(data), Fast).Error;
        Assert.Contains("höchstens 1 000 Dateien und Ordner", entries);
        Assert.DoesNotContain("MB", entries); Assert.DoesNotContain("entfernt", entries);

        var data2 = Path.Combine(Tmp(), "daten"); Directory.CreateDirectory(data2);
        var size = ExtensionSandbox.Run(Writer(5 << 20), [], "gross.bin"u8.ToArray(), new(data2), Fast with { DataQuota = 4L << 20 }).Error;
        Assert.Contains("Datenordner-Grenze (4 MB)", size);
        Assert.DoesNotContain("entfernt", size); // vorab geprüft – es wurde nichts zu Großes geschrieben
        Assert.DoesNotContain("Dateien und Ordner", size);
        Assert.False(File.Exists(Path.Combine(data2, "gross.bin")));
    }

    [Fact(DisplayName = "T-0766 N14: Wiederherstellen prüft Skip-Bereiche am normalisierten Zielpfad – „x/../erweiterungen-cache/…“, „./web-mixamo/…“, Groß/Klein")]
    public void N14_restore_normalized()
    {
        var data = Tmp(); var audit = new AuditLog(Path.Combine(data, "audit", "a.jsonl"));
        File.WriteAllText(Path.Combine(data, "notiz.txt"), "alt");
        var backup = new Backup(data, Path.Combine(data, "backups"), audit);
        var foreign = Path.Combine(Tmp(), "fremd.zip");
        string[] bad = ["x/../erweiterungen-cache/c.cwasm", "./web-mixamo/sitzung.json", "Erweiterungen-Cache/d.cwasm", "a/../AUDIT/neu.jsonl", "x\\..\\web-mixamo\\b.json"];
        using (var z = ZipFile.Open(foreign, ZipArchiveMode.Create))
        {
            using (var w = new StreamWriter(z.CreateEntry("notiz.txt").Open())) w.Write("neu");
            foreach (var b in bad) using (var w = new StreamWriter(z.CreateEntry(b).Open())) w.Write("fremd");
        }
        var plan = backup.Preview(foreign);
        Assert.Equal(1, plan.Replaced); Assert.Equal(0, plan.Added);
        backup.Restore(foreign, DateTime.Now);
        Assert.Equal("neu", File.ReadAllText(Path.Combine(data, "notiz.txt")));
        Assert.False(Directory.Exists(Path.Combine(data, "erweiterungen-cache")));
        Assert.False(Directory.Exists(Path.Combine(data, "web-mixamo")));
        Assert.False(File.Exists(Path.Combine(data, "audit", "neu.jsonl")));
        foreach (var b in bad) Assert.Throws<InvalidOperationException>(() => backup.RestoreOne(foreign, b, DateTime.Now.AddMinutes(1)));
    }
}
