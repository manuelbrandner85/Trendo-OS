using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Trendo.Core;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0766 zweite Prüfrunde (fix2): H1 (kein Zwischenspeicher), M3 (Locals/Körpergröße), N10 (parallele Läufe), N11 (Namen im Datenordner).
/// Alles mit der echten Wasmtime-Laufzeit.</summary>
public class T0766_Gegenpruefung2(ITestOutputHelper log)
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-gp2-t-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }
    static readonly SandboxLimits Fast = SandboxLimits.Default with { Time = TimeSpan.FromSeconds(10) };

    static string Repo()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("Repo nicht gefunden");
    }

    [Fact(DisplayName = "T-0766 H1: getauschtes Kompilat im Zwischenspeicher (mit passender Prüfsummen-Datei) wird nie geladen; Ordner wird gelöscht, nicht gesichert und nicht wiederhergestellt; Deserialize kommt im Produkt nicht vor")]
    public void H1_no_cache()
    {
        var root = Tmp(); var s = new ExtensionStore(root);
        var (e, err) = s.Install(ExtensionModuleSample.Write(Tmp())); Assert.Empty(err);
        Assert.Null(s.GrantRights(e!.Manifest.Id));
        // Angreifer legt ein fremdes, gültiges Kompilat samt stimmiger Prüfsummen-Datei ab
        using (var engine = new Wasmtime.Engine(new Wasmtime.Config().WithFuelConsumption(true).WithEpochInterruption(true).WithWasmThreads(false).WithDebugInfo(false).WithGc(false).WithExceptions(false).WithMultiMemory(false).WithMemory64(false).WithComponentModel(false).WithParallelCompilation(false))) // gleiche Ausstattung wie die Sandbox – ein echter Angriff
        using (var evil = Wasmtime.Module.FromText(engine, "boese", """
            (module (import "wasi_snapshot_preview1" "fd_write" (func $w (param i32 i32 i32 i32) (result i32))) (memory (export "memory") 1)
              (data (i32.const 100) "BOESE") (func (export "_start") (i32.store (i32.const 0) (i32.const 100)) (i32.store (i32.const 4) (i32.const 5)) (drop (call $w (i32.const 1) (i32.const 0) (i32.const 1) (i32.const 8)))))
            """))
        {
            var compiled = evil.Serialize();
            var dir = s.LegacyCacheDir; Directory.CreateDirectory(dir);
            var cache = Path.Combine(dir, e.Manifest.Id + ".cwasm");
            File.WriteAllBytes(cache, compiled);
            File.WriteAllText(cache + ".sha256", ExtensionSandbox.Sha256(compiled) + " " + ExtensionSandbox.Sha256(ExtensionModuleSample.Wasm) + "\n");
        }
        var p = new ProjectStore(Path.Combine(root, "projekte"), new AuditLog(Path.Combine(root, "pa.jsonl")));
        var pid = p.CreateNamed("Kunden").Id; p.WriteFile(pid, "k.csv", "a,b\n");
        var r = s.RunCommand(e.Manifest.Id, "umwandeln", p, pid, "k.csv");
        Assert.True(r.Ok, r.Message);
        Assert.Equal("| a | b |\n| --- | --- |\n", p.ReadFile(pid, r.OutputRel!));
        // Entfernen räumt den alten Zwischenspeicher weg; Neustart der Verwaltung ebenso
        s.Remove(e.Manifest.Id); Assert.False(Directory.Exists(s.LegacyCacheDir));
        var root2 = Tmp(); Directory.CreateDirectory(Path.Combine(root2, ExtensionStore.LegacyCacheName)); File.WriteAllText(Path.Combine(root2, ExtensionStore.LegacyCacheName, "x.cwasm"), "x");
        _ = new ExtensionStore(root2).All();
        Assert.False(Directory.Exists(Path.Combine(root2, ExtensionStore.LegacyCacheName)));

        // Sicherung: Ordner wird nicht gesichert und beim Wiederherstellen einer fremden ZIP übersprungen
        var data = Tmp(); var audit = new AuditLog(Path.Combine(data, "audit", "a.jsonl"));
        File.WriteAllText(Path.Combine(data, "notiz.txt"), "n");
        Directory.CreateDirectory(Path.Combine(data, ExtensionStore.LegacyCacheName)); File.WriteAllText(Path.Combine(data, ExtensionStore.LegacyCacheName, "a.cwasm"), "alt");
        var backup = new Backup(data, Path.Combine(data, "backups"), audit);
        var zip = backup.Create(DateTime.Now);
        using (var z = ZipFile.OpenRead(zip)) Assert.DoesNotContain(z.Entries, x => x.FullName.StartsWith(ExtensionStore.LegacyCacheName));
        var foreign = Path.Combine(Tmp(), "fremd.zip");
        using (var z = ZipFile.Open(foreign, ZipArchiveMode.Create))
        {
            using (var w = new StreamWriter(z.CreateEntry("notiz.txt").Open())) w.Write("neu");
            using (var w = new StreamWriter(z.CreateEntry(ExtensionStore.LegacyCacheName + "/trendo.csv-tabelle.cwasm").Open())) w.Write("fremder Maschinencode");
        }
        Directory.Delete(Path.Combine(data, ExtensionStore.LegacyCacheName), true);
        backup.Restore(foreign, DateTime.Now.AddMinutes(1));
        Assert.Equal("neu", File.ReadAllText(Path.Combine(data, "notiz.txt")));
        Assert.False(Directory.Exists(Path.Combine(data, ExtensionStore.LegacyCacheName)));
        Assert.Throws<InvalidOperationException>(() => backup.RestoreOne(foreign, ExtensionStore.LegacyCacheName + "/trendo.csv-tabelle.cwasm", DateTime.Now.AddMinutes(2)));

        // Grep: im Produkt wird nie übersetzter Maschinencode geladen
        var hits = Directory.EnumerateFiles(Path.Combine(Repo(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f) is var t && (t.Contains("Module.Deserialize") || t.Contains("DeserializeFile(") || t.Contains(".Serialize()") && t.Contains("Wasmtime"))).ToList();
        Assert.Empty(hits);
    }

    static byte[] Leb(uint v) { var l = new List<byte>(); do { var b = (byte)(v & 0x7F); v >>= 7; if (v != 0) b |= 0x80; l.Add(b); } while (v != 0); return [.. l]; }
    static IEnumerable<byte> Section(byte id, byte[] body) => [id, .. Leb((uint)body.Length), .. body];

    /// <summary>Modul mit <paramref name="funcs"/> leeren Funktionen, jede mit <paramref name="locals"/> lokalen Variablen vom Typ <paramref name="type"/>.</summary>
    static byte[] LocalsModule(int funcs, uint locals, byte type, int nops = 0)
    {
        var body = new List<byte>(); body.AddRange(Leb(1)); body.AddRange(Leb(locals)); body.Add(type); for (var i = 0; i < nops; i++) body.Add(0x01); body.Add(0x0B);
        var fs = new List<byte>(Leb((uint)funcs)); for (var i = 0; i < funcs; i++) fs.Add(0);
        var code = new List<byte>(Leb((uint)funcs)); for (var i = 0; i < funcs; i++) { code.AddRange(Leb((uint)body.Count)); code.AddRange(body); }
        return [0, (byte)'a', (byte)'s', (byte)'m', 1, 0, 0, 0, .. Section(1, [1, 0x60, 0, 0]), .. Section(3, [.. fs]), .. Section(10, [.. code])];
    }

    [Fact(DisplayName = "T-0766 M3: Locals je Funktion und gesamt sowie Körpergröße werden vor dem Übersetzen begrenzt – Angriffsmodul (19 999 × 49 000 Locals) in < 3 s abgewiesen; schlimmster Fall knapp unter den Grenzen gemessen")]
    public void M3_locals()
    {
        foreach (var t in new byte[] { 0x7E, 0x7B })
        {
            var evil = LocalsModule(19_999, 49_000, t);
            log.WriteLine($"Angriffsmodul Typ 0x{t:X2}: {evil.Length >> 10} KB");
            var sw = Stopwatch.StartNew();
            Assert.Contains(ExtensionSandbox.Inspect(evil), e => e.Contains("lokale Variablen"));
            Assert.Contains("lokale Variablen", ExtensionSandbox.Run(evil, [], [], new(), Fast).Error);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3), sw.Elapsed.ToString());
        }
        Assert.Contains(ExtensionSandbox.Inspect(LocalsModule(101, 1_990, 0x7E)), e => e.Contains("Modul hat zu viele lokale Variablen"));
        Assert.Contains(ExtensionSandbox.Inspect(LocalsModule(1, 10, 0x7F, (512 << 10) + 10)), e => e.Contains("Funktion ist zu groß"));
        // Messung schlimmster Fall knapp unter den Grenzen (Übersetzen ohne Abbruchmöglichkeit, darum ehrlich gemessen)
        foreach (var (name, m) in new[] { ("100 × 2000 v128", LocalsModule(100, 2_000, 0x7B)), ("20 000 × 10 v128", LocalsModule(20_000, 10, 0x7B)), ("1 × 512 KB nop", LocalsModule(1, 10, 0x7B, (512 << 10) - 64)) })
        {
            Assert.Null(ExtensionSandbox.ShapeError(m));
            var sw = Stopwatch.StartNew(); var err = ExtensionSandbox.Inspect(m); sw.Stop();
            log.WriteLine($"Übersetzen {name} ({m.Length >> 10} KB): {sw.Elapsed.TotalSeconds:0.00} s – {string.Join(" ", err)}");
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(15), name + ": " + sw.Elapsed);
        }
    }

    [Fact(DisplayName = "T-0766 M3: Übersetzen zählt zur Zeitgrenze, Not-Stopp beendet den Lauf sofort auch während des Übersetzens")]
    public void M3_compile_in_time_budget()
    {
        var slow = LocalsModule(20_000, 10, 0x7B); // langsamster gemessener Fall unter den Grenzen
        var sw = Stopwatch.StartNew(); ExtensionSandbox.Inspect(slow); var compile = sw.Elapsed;
        log.WriteLine($"Übersetzen: {compile.TotalMilliseconds:0} ms");
        if (compile < TimeSpan.FromMilliseconds(300)) { log.WriteLine("zu schnell für einen Abbruch-Nachweis – nur Zeitgrenze geprüft"); }
        else
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            sw.Restart();
            var r = ExtensionSandbox.Run(slow, [], [], new(), Fast with { Time = TimeSpan.FromMinutes(5) }, cts.Token);
            Assert.Contains("Not-Stopp", r.Error); log.WriteLine($"Not-Stopp beendet den Lauf nach {sw.Elapsed.TotalMilliseconds:0} ms");
            Assert.True(sw.Elapsed < compile / 2 + TimeSpan.FromMilliseconds(200), $"{sw.Elapsed} bei Übersetzen {compile}");
        }
        var t = ExtensionSandbox.Run(slow, [], [], new(), Fast with { Time = TimeSpan.FromMilliseconds(1) });
        Assert.False(t.Ok); Assert.Contains("Zeitgrenze", t.Error);
    }

    /// <summary>Liest den Dateinamen von stdin und schreibt <paramref name="bytes"/> Bytes unter diesem Namen; Ausgang = Fehlernummer.</summary>
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

    [Fact(DisplayName = "T-0766 N10: parallele Läufe derselben Erweiterung – Überschreitung räumt nur eigene Dateien, fremde bleiben")]
    public void N10_parallel()
    {
        var data = Path.Combine(Tmp(), "daten"); Directory.CreateDirectory(data);
        var limits = Fast with { DataQuota = 6L << 20 };
        var mb = Writer(1 << 20);
        var loop = Wasmtime.Module.ConvertText("""
            (module
              (import "trendo" "daten_schreiben" (func $put (param i32 i32 i32 i32) (result i32)))
              (memory (export "memory") 20)
              (data (i32.const 100) "teil-A.bin")
              (func (export "_start")
                (loop $l
                  (i32.store8 (i32.const 105) (i32.add (i32.load8_u (i32.const 105)) (i32.const 1)))
                  (drop (call $put (i32.const 100) (i32.const 10) (i32.const 65536) (i32.const 1048576)))
                  (br $l))))
            """);
        for (var round = 0; round < 3; round++)
        {
            foreach (var f in Directory.GetFiles(data)) File.Delete(f);
            var results = new SandboxResult[5];
            Parallel.For(0, 5, i => results[i] = i == 0 ? ExtensionSandbox.Run(loop, [], [], new(data), limits)
                : ExtensionSandbox.Run(mb, [], Encoding.UTF8.GetBytes($"klein-{i}.bin"), new(data), limits));
            Assert.Contains("Datenordner-Grenze", results[0].Error);
            for (var i = 1; i < 5; i++)
                if (results[i].Ok) Assert.True(File.Exists(Path.Combine(data, $"klein-{i}.bin")), $"Runde {round}: klein-{i}.bin wurde vom Aufräumen eines anderen Laufs gelöscht");
            Assert.True(ExtensionSandbox.DirSize(data) <= limits.DataQuota);
        }
    }

    [Fact(DisplayName = "T-0766 N11: Gerätenamen, Punkt/Leerzeichen am Ende, zu tief/lang, Ordner zählen mit, Ordner und Junction als Ziel sofort abgewiesen")]
    public void N11_names()
    {
        foreach (var bad in new[] { "con", "CON", "aux", "lpt1", "nul.txt", "COM1.txt", "com¹", "LPT³.log", "Con .txt", "prn.tar.gz", "a.", "a ", "a/b./c", "a/../b",
                     "1/2/3/4/5/6/7/8/9", new string('x', 256), "x<y", "x|y", "x?y", "x\u0001", "conin$" })
            Assert.False(ExtensionSandbox.NameAllowed(bad), bad);
        foreach (var good in new[] { "notiz.txt", "a/b/c.json", "console.txt", "com10.txt", "lpt.txt", "nullen.csv", "1/2/3/4/5/6/7/8" })
            Assert.True(ExtensionSandbox.NameAllowed(good), good);

        var data = Path.Combine(Tmp(), "daten"); Directory.CreateDirectory(data);
        var w = Writer(5);
        foreach (var bad in new[] { "nul.txt", "COM1.txt", "aux", "tief/1/2/3/4/5/6/7/8/x.txt" })
        {
            var r = ExtensionSandbox.Run(w, [], Encoding.UTF8.GetBytes(bad), new(data), Fast);
            Assert.Contains("Code 28", r.Error);
        }
        Assert.Empty(Directory.EnumerateFileSystemEntries(data));
        // Ordner als Ziel
        Directory.CreateDirectory(Path.Combine(data, "ordner"));
        Assert.Contains("Code 28", ExtensionSandbox.Run(w, [], "ordner"u8.ToArray(), new(data), Fast).Error);
        // Junction als Ziel und auf dem Weg: sofort abgewiesen, außerhalb entsteht nichts
        var outside = Path.Combine(Path.GetDirectoryName(data)!, "draussen"); Directory.CreateDirectory(outside);
        var mk = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{Path.Combine(data, "j")}\" \"{outside}\"") { CreateNoWindow = true, UseShellExecute = false })!; mk.WaitForExit();
        if (Directory.Exists(Path.Combine(data, "j")))
        {
            var sw = Stopwatch.StartNew();
            Assert.Contains("Code 28", ExtensionSandbox.Run(w, [], "j"u8.ToArray(), new(data), Fast).Error);
            Assert.Contains("Code 28", ExtensionSandbox.Run(w, [], "j/x.txt"u8.ToArray(), new(data), Fast).Error);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), sw.Elapsed.ToString());
            Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
            Directory.Delete(Path.Combine(data, "j"));
        }
        else log.WriteLine("mklink /J nicht möglich – Junction-Teil übersprungen");
        // Ordner zählen mit: 999 Einträge vorhanden, neue Datei in neuem Ordner braucht zwei → Grenze
        Directory.Delete(Path.Combine(data, "ordner"));
        for (var i = 0; i < ExtensionSandbox.MaxDataFiles - 1; i++) Directory.CreateDirectory(Path.Combine(data, "d" + i));
        Assert.Contains("Datenordner-Grenze", ExtensionSandbox.Run(w, [], "neu/datei.txt"u8.ToArray(), new(data), Fast).Error);
        Assert.False(Directory.Exists(Path.Combine(data, "neu")));
    }
}
