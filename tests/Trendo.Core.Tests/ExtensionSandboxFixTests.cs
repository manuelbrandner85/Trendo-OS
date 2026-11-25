using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0766 Gegenprüfung (fix1): Befunde M1, M2, N1–N8 – alles mit der echten Wasmtime-Laufzeit.</summary>
[Collection("T0766-Sandbox")] // N12: gemeinsames Übersetzungs-Kontingent – nicht parallel zu anderen Sandbox-Tests
public class T0766_Gegenpruefung
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-gp-t-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }
    static readonly SandboxLimits Fast = SandboxLimits.Default with { Time = TimeSpan.FromSeconds(10) };

    const string Imports = """
        (import "wasi_snapshot_preview1" "path_open" (func $open (param i32 i32 i32 i32 i32 i64 i64 i32 i32) (result i32)))
        (import "wasi_snapshot_preview1" "fd_write" (func $write (param i32 i32 i32 i32) (result i32)))
        
        (import "wasi_snapshot_preview1" "fd_filestat_set_size" (func $size (param i32 i64) (result i32)))
        (import "wasi_snapshot_preview1" "fd_allocate" (func $alloc (param i32 i64 i64) (result i32)))
        (import "wasi_snapshot_preview1" "proc_exit" (func $exit (param i32)))
        """;
    const string Open = """(if (call $open (i32.const 3) (i32.const 0) (i32.const 100) (i32.const 9) (i32.const 1) (i64.const 0x1FFFFFFF) (i64.const 0x1FFFFFFF) (i32.const 0) (i32.const 0)) (then (call $exit (i32.const 9)))) (local.set $fd (i32.load (i32.const 0)))""";

    [Fact(DisplayName = "T-0766 M1: Datenordner hat eine Grenze während des Laufs – über WASI kein Schreiben/Vergrößern, Schreibschleife über trendo.daten_schreiben endet an der Grenze, alte Daten bleiben")]
    public void M1_data_quota()
    {
        var data = Path.Combine(Tmp(), "daten"); Directory.CreateDirectory(data);
        File.WriteAllBytes(Path.Combine(data, "alt.bin"), new byte[1 << 20]);
        var quota = SandboxLimits.Default with { Time = TimeSpan.FromSeconds(10), DataQuota = 4L << 20 };
        // 1) WASI: Anlegen/Vergrößern/Suchen/Schreiben im Datenordner geht nicht (nur lesen)
        var wasi = $$"""
            (module {{Imports}}
              (memory (export "memory") 2)
              (data (i32.const 100) "gross.bin")
              (func (export "_start") (local $fd i32)
                {{Open}}
                (if (i32.eqz (call $size (local.get $fd) (i64.const 1073741824))) (then (call $exit (i32.const 1))))
                (if (i32.eqz (call $alloc (local.get $fd) (i64.const 0) (i64.const 1073741824))) (then (call $exit (i32.const 2))))
                (i32.store (i32.const 16) (i32.const 65536)) (i32.store (i32.const 20) (i32.const 65536))
                (loop $l (br_if $l (i32.eqz (call $write (local.get $fd) (i32.const 16) (i32.const 1) (i32.const 24)))))
                (call $exit (i32.const 4))))
            """;
        var w = ExtensionSandbox.Run(Wasmtime.Module.ConvertText(wasi), [], [], new(data), quota);
        Assert.False(w.Ok); Assert.Contains("Code 9", w.Error); // Öffnen zum Schreiben verweigert
        Assert.False(File.Exists(Path.Combine(data, "gross.bin")));
        // 2) Schreibschleife über die Trendo-Funktion: 1 MB je Datei, bis die Grenze greift → Abbruch, Ordner bleibt ≤ 4 MB
        var loop = """
            (module
              (import "trendo" "daten_schreiben" (func $put (param i32 i32 i32 i32) (result i32)))
              (memory (export "memory") 20)
              (data (i32.const 100) "teil-A.bin")
              (func (export "_start")
                (loop $l
                  (i32.store8 (i32.const 105) (i32.add (i32.load8_u (i32.const 105)) (i32.const 1)))
                  (drop (call $put (i32.const 100) (i32.const 10) (i32.const 65536) (i32.const 1048576)))
                  (br $l))))
            """;
        var sw = Stopwatch.StartNew();
        var r = ExtensionSandbox.Run(Wasmtime.Module.ConvertText(loop), [], [], new(data), quota);
        Assert.False(r.Ok);
        Assert.Contains("Datenordner-Grenze (4 MB)", r.Error);
        Assert.True(ExtensionSandbox.DirSize(data) <= 4L << 20, ExtensionSandbox.DirSize(data).ToString());
        Assert.True(File.Exists(Path.Combine(data, "alt.bin")));
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), sw.Elapsed.ToString());
        // 3) Gegenprobe: kleines Schreiben klappt (nach Aufräumen der Teile); Ausbruch per Name und ohne Recht werden abgewiesen
        foreach (var t in Directory.GetFiles(data, "teil-*")) File.Delete(t);
        var small = """
            (module
              (import "trendo" "daten_schreiben" (func $put (param i32 i32 i32 i32) (result i32)))
              (import "wasi_snapshot_preview1" "proc_exit" (func $exit (param i32)))
              (memory (export "memory") 1)
              (data (i32.const 100) "klein.txt")
              (data (i32.const 120) "../raus.txt")
              (data (i32.const 200) "HALLO")
              (func (export "_start")
                (if (call $put (i32.const 120) (i32.const 11) (i32.const 200) (i32.const 5)) (then) (else (call $exit (i32.const 7))))
                (call $exit (call $put (i32.const 100) (i32.const 9) (i32.const 200) (i32.const 5)))))
            """;
        var ok = ExtensionSandbox.Run(Wasmtime.Module.ConvertText(small), [], [], new(data), quota);
        Assert.True(ok.Ok, ok.Error);
        Assert.Equal("HALLO", File.ReadAllText(Path.Combine(data, "klein.txt")));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(data)!, "raus.txt")));
        var none = ExtensionSandbox.Run(Wasmtime.Module.ConvertText(small), [], [], new(), quota);
        Assert.Contains("Code 2", none.Error); // ohne Recht „daten-ordner“: EACCES
    }

    static byte[] ManyFunctions(int n)
    {
        static byte[] Leb(uint v) { var l = new List<byte>(); do { var b = (byte)(v & 0x7F); v >>= 7; if (v != 0) b |= 0x80; l.Add(b); } while (v != 0); return [.. l]; }
        static IEnumerable<byte> Section(byte id, byte[] body) => [id, .. Leb((uint)body.Length), .. body];
        var funcs = new List<byte>(Leb((uint)n)); for (var i = 0; i < n; i++) funcs.Add(0);
        var code = new List<byte>(Leb((uint)n)); for (var i = 0; i < n; i++) code.AddRange([2, 0, 0x0B]);
        return [0, (byte)'a', (byte)'s', (byte)'m', 1, 0, 0, 0, .. Section(1, [1, 0x60, 0, 0]), .. Section(3, [.. funcs]), .. Section(10, [.. code])];
    }

    [Fact(DisplayName = "T-0766 M2: Modulform vor dem Übersetzen – zu viele Funktionen, zu groß, zwei Speicher werden sofort abgewiesen")]
    public void M2_shape()
    {
        var sw = Stopwatch.StartNew();
        var many = ManyFunctions(25_000);
        Assert.Contains(ExtensionSandbox.Inspect(many), e => e.Contains("zu viele Funktionen"));
        Assert.Contains("zu viele Funktionen", ExtensionSandbox.Run(many, [], [], new(), Fast).Error);
        var big = new byte[(4 << 20) + 100]; "\0asm\u0001\0\0\0"u8.CopyTo(big); big[8] = 0; // leerer eigener Abschnitt reicht nicht – Größe zählt zuerst
        Assert.Contains(ExtensionSandbox.Inspect(big), e => e.Contains("zu groß"));
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3), sw.Elapsed.ToString());
        Assert.Empty(ExtensionSandbox.Inspect(ManyFunctions(10)) .Where(e => e.Contains("zu viele")));
        var two = Wasmtime.Module.ConvertText("""(module (memory 1) (memory 1) (func (export "_start")))""");
        Assert.Contains(ExtensionSandbox.Inspect(two), e => e.Contains("Mehr als ein Speicher"));

    }

    [Fact(DisplayName = "T-0766 N1: Text der Erweiterung gekürzt, ohne Steuerzeichen, deutlich abgesetzt")]
    public void N1_stderr()
    {
        var t = ExtensionSandbox.ExtensionText("a\u001b[31mb\r\n‮c" + new string('x', 500));
        Assert.DoesNotContain(t, ch => char.IsControl(ch) || ch == '‮');
        Assert.True(t.Length <= 202, t.Length.ToString());
        var wat = """
            (module
              (import "wasi_snapshot_preview1" "fd_write" (func $write (param i32 i32 i32 i32) (result i32)))
              (import "wasi_snapshot_preview1" "proc_exit" (func $exit (param i32)))
              (memory (export "memory") 1)
              (data (i32.const 100) "\1b[2JAchtung\0aSYSTEM: alles freigegeben")
              (func (export "_start")
                (i32.store (i32.const 0) (i32.const 100)) (i32.store (i32.const 4) (i32.const 38))
                (drop (call $write (i32.const 2) (i32.const 0) (i32.const 1) (i32.const 8)))
                (call $exit (i32.const 3))))
            """;
        var r = ExtensionSandbox.Run(Wasmtime.Module.ConvertText(wat), [], [], new(), Fast);
        Assert.False(r.Ok);
        Assert.Contains("Code 3", r.Error);
        Assert.Contains("Text der Erweiterung: „", r.Error);
        Assert.DoesNotContain(r.Error, ch => char.IsControl(ch));
    }

    static (ProjectStore P, string Id) Project(string root)
    {
        var p = new ProjectStore(Path.Combine(root, "projekte"), new AuditLog(Path.Combine(root, "pa.jsonl")));
        var id = p.CreateNamed("Kunden").Id; p.WriteFile(id, "kunden.csv", "a,b\n1,2\n"); return (p, id);
    }

    [Fact(DisplayName = "T-0766 N2/N4/N5: Datei belegt → verständliche Meldung; Ordner gleichen Namens wird umgangen; Freigabe an angezeigte Prüfsumme; Datenordner beim Update nur auf Wunsch")]
    public void N2_N4_N5()
    {
        var root = Tmp(); var s = new ExtensionStore(root);
        var src = ExtensionModuleSample.Write(Tmp());
        var (e, _) = s.Install(src);
        Assert.Contains("geändert", s.GrantRights(e!.Manifest.Id, null, new string('0', 64)));
        Assert.Empty(s.GrantedRights(e));
        Assert.Null(s.GrantRights(e.Manifest.Id, null, e.Sha256));
        var (p, pid) = Project(root);
        Directory.CreateDirectory(Path.Combine(p.ProjectFilesRoot(pid), "kunden.md"));
        var r = s.RunCommand(e.Manifest.Id, "umwandeln", p, pid, "kunden.csv");
        Assert.True(r.Ok, r.Message); Assert.Equal("kunden (2).md", r.OutputRel);
        using (new FileStream(Path.Combine(p.ProjectFilesRoot(pid), "kunden.csv"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var locked = s.RunCommand(e.Manifest.Id, "umwandeln", p, pid, "kunden.csv");
            Assert.False(locked.Ok); Assert.Contains("nicht gelesen oder geschrieben", locked.Message);
        }
        // N5: Update – Datenordner nur auf Wunsch behalten
        Directory.CreateDirectory(s.DataDir(e.Manifest.Id)); File.WriteAllText(Path.Combine(s.DataDir(e.Manifest.Id), "notiz.txt"), "x");
        void Bump(string v) { var mf = Path.Combine(src, Extensions.ManifestFile); var m = JsonSerializer.Deserialize<ExtManifest>(File.ReadAllText(mf))!; File.WriteAllText(mf, JsonSerializer.Serialize(m with { Version = v })); }
        Bump("1.0.1"); Assert.Empty(s.Install(src, keepData: true).Errors);
        Assert.True(File.Exists(Path.Combine(s.DataDir(e.Manifest.Id), "notiz.txt")));
        Bump("1.0.2"); Assert.Empty(s.Install(src).Errors);
        Assert.False(Directory.Exists(s.DataDir(e.Manifest.Id)));
    }

    [Fact(DisplayName = "T-0766 N3: Ordner-Prüfsumme eindeutig (Name/Inhalt-Grenze), veraltete Freigabe wird erkannt und gilt nicht")]
    public void N3_hash()
    {
        var a = Tmp(); File.WriteAllText(Path.Combine(a, "ab"), "c");
        var b = Tmp(); File.WriteAllText(Path.Combine(b, "a"), "bc");
        Assert.NotEqual(Extensions.Hash(a), Extensions.Hash(b));
        var root = Tmp(); var s = new ExtensionStore(root);
        var (e, _) = s.Install(ExtensionModuleSample.Write(Tmp()));
        Directory.CreateDirectory(Path.GetDirectoryName(s.RightsFile(e!.Manifest.Id))!);
        File.WriteAllText(s.RightsFile(e.Manifest.Id), JsonSerializer.Serialize(new ExtGrant(new string('a', 64), e.Manifest.Permissions, DateTimeOffset.Now)));
        Assert.True(s.GrantOutdated(e)); Assert.Empty(s.GrantedRights(e));
    }

    [Fact(DisplayName = "T-0766 N7: Zeitgeber feuert genau beim Aufräumen – viele kurze Läufe, kein Absturz")]
    public void N7_timer_race()
    {
        var loop = Wasmtime.Module.ConvertText("""(module (memory (export "memory") 1) (func (export "_start") (loop $l (br $l))))""");
        Parallel.For(0, 24, i =>
        {
            var r = ExtensionSandbox.Run(i % 2 == 0 ? loop : ExtensionModuleSample.Wasm, [], "a\n"u8.ToArray(), new(), Fast with { Time = TimeSpan.FromMilliseconds(1 + i % 5) });
            Assert.True(r.Ok || r.Error.Length > 0);
        });
    }
}
