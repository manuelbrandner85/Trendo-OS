using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0766 (X-04): Funktions-Erweiterungen in der WebAssembly-Sandbox – alles mit der echten Wasmtime-Laufzeit, nichts ersetzt.
/// Feindliche Module (Datei außerhalb lesen, Netz, Endlosschleife, Speicher sprengen) werden abgewiesen bzw. abgebrochen; Trendo läuft danach weiter.</summary>
public class T0766_ExtensionSandbox
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-sbx-t-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }
    static byte[] Wat(string wat) => ExtensionSandbox.BuildFromText(wat).Wasm ?? Wasmtime.Module.ConvertText(wat);
    static readonly SandboxLimits Fast = SandboxLimits.Default with { Time = TimeSpan.FromSeconds(10) };

    /// <summary>Versucht, <paramref name="path"/> über die vorgeöffnete Mappe fd 3 zum Lesen (Recht FD_READ) zu öffnen und auszugeben; sonst „VERWEIGERT“.</summary>
    static string Reader(string path) => $$"""
        (module
          (import "wasi_snapshot_preview1" "path_open" (func $open (param i32 i32 i32 i32 i32 i64 i64 i32 i32) (result i32)))
          (import "wasi_snapshot_preview1" "fd_read" (func $read (param i32 i32 i32 i32) (result i32)))
          (import "wasi_snapshot_preview1" "fd_write" (func $write (param i32 i32 i32 i32) (result i32)))
          (memory (export "memory") 1)
          (data (i32.const 100) "{{path.Replace("\\", "\\\\")}}")
          (data (i32.const 600) "VERWEIGERT")
          (func (export "_start")
            (if (i32.eqz (call $open (i32.const 3) (i32.const 1) (i32.const 100) (i32.const {{Encoding.UTF8.GetByteCount(path)}}) (i32.const 0) (i64.const 2) (i64.const 2) (i32.const 0) (i32.const 0)))
              (then
                (i32.store (i32.const 16) (i32.const 1000)) (i32.store (i32.const 20) (i32.const 4000))
                (drop (call $read (i32.load (i32.const 0)) (i32.const 16) (i32.const 1) (i32.const 24)))
                (i32.store (i32.const 16) (i32.const 1000)) (i32.store (i32.const 20) (i32.load (i32.const 24)))
                (drop (call $write (i32.const 1) (i32.const 16) (i32.const 1) (i32.const 28))))
              (else
                (i32.store (i32.const 16) (i32.const 600)) (i32.store (i32.const 20) (i32.const 10))
                (drop (call $write (i32.const 1) (i32.const 16) (i32.const 1) (i32.const 28)))))))
        """;

    static string Out(SandboxResult r) => Encoding.UTF8.GetString(r.Output);

    [Fact(DisplayName = "T-0766: Beispielmodul baut byte-gleich aus der Quelle im Repo (Prüfsumme belegt) und wandelt echte CSV in eine Tabelle")]
    public void Sample_reproducible_and_converts()
    {
        var (wasm, err) = ExtensionSandbox.BuildFromText(ExtensionModuleSample.Source);
        Assert.Empty(err);
        Assert.Equal(ExtensionModuleSample.Wasm, wasm);
        Assert.Equal(ExtensionModuleSample.WasmSha256, ExtensionSandbox.Sha256(wasm!));
        var shaFile = Path.Combine(Repo(), "src", "Trendo.Core", "Erweiterungen", "csv-tabelle.wasm.sha256");
        Assert.StartsWith(ExtensionModuleSample.WasmSha256, File.ReadAllText(shaFile));
        var csv = "﻿Name,Ort,Notiz\r\nErika Muster,Köln,\"sagt \"\"hallo\"\", gern\"\r\n\r\nMax,Berlin|Mitte,\"zwei\nZeilen\"\r\n";
        var r = ExtensionSandbox.Run(wasm!, ["t", "umwandeln"], Encoding.UTF8.GetBytes(csv), new(), Fast);
        Assert.True(r.Ok, r.Error);
        Assert.Equal("| Name | Ort | Notiz |\n| --- | --- | --- |\n| Erika Muster | Köln | sagt \"hallo\", gern |\n| Max | Berlin\\|Mitte | zwei Zeilen |\n", Out(r));
    }

    [Fact(DisplayName = "T-0766 feindlich: Datei außerhalb des eigenen Ordners lesen wird abgewiesen – im eigenen Ordner geht es (Gegenprobe)")]
    public void Hostile_read_outside()
    {
        var root = Tmp(); var data = Path.Combine(root, "daten"); Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(root, "geheim.txt"), "GEHEIMNIS");
        File.WriteAllText(Path.Combine(data, "erlaubt.txt"), "ERLAUBT");
        var dirs = new SandboxDirs(data);
        // Gegenprobe: dasselbe Modul liest im eigenen Ordner
        var ok = ExtensionSandbox.Run(Wat(Reader("erlaubt.txt")), [], [], dirs, Fast);
        Assert.True(ok.Ok, ok.Error); Assert.Equal("ERLAUBT", Out(ok));
        foreach (var p in new[] { "../geheim.txt", "../../" + Path.GetFileName(root) + "/geheim.txt", Path.Combine(root, "geheim.txt"), "/" + Path.Combine(root, "geheim.txt").Replace('\\', '/') })
        {
            var r = ExtensionSandbox.Run(Wat(Reader(p)), [], [], dirs, Fast);
            Assert.True(r.Ok, r.Error);
            Assert.Equal("VERWEIGERT", Out(r));
        }
        // ohne freigegebenen Ordner gibt es gar keine Mappe
        Assert.Equal("VERWEIGERT", Out(ExtensionSandbox.Run(Wat(Reader("erlaubt.txt")), [], [], new SandboxDirs(), Fast)));
    }

    [Fact(DisplayName = "T-0766 feindlich: Netz-Funktionen und fremde Importe werden schon bei der Prüfung abgewiesen; Recht „netz“ gibt es nicht")]
    public void Hostile_network()
    {
        var sock = Wasmtime.Module.ConvertText("""(module (import "wasi_snapshot_preview1" "sock_accept" (func (param i32 i32 i32) (result i32))) (memory (export "memory") 1) (func (export "_start")))""");
        Assert.Contains(ExtensionSandbox.Inspect(sock), e => e.Contains("Netzzugriff"));
        var r = ExtensionSandbox.Run(sock, [], [], new(), Fast); Assert.False(r.Ok); Assert.Contains("Netzzugriff", r.Error);
        var env = Wasmtime.Module.ConvertText("""(module (import "env" "http_get" (func (param i32 i32) (result i32))) (memory (export "memory") 1) (func (export "_start")))""");
        Assert.Contains(ExtensionSandbox.Inspect(env), e => e.Contains("env.http_get"));
        var poll = Wasmtime.Module.ConvertText("""(module (import "wasi_snapshot_preview1" "poll_oneoff" (func (param i32 i32 i32 i32) (result i32))) (memory (export "memory") 1) (func (export "_start")))""");
        Assert.NotEmpty(ExtensionSandbox.Inspect(poll));
        Assert.Contains("Das ist kein WebAssembly-Modul.", ExtensionSandbox.Inspect(Encoding.ASCII.GetBytes("MZ-kein-modul")));
        // Manifest mit Recht „netz“
        var d = ExtensionModuleSample.Write(Tmp()); var mf = Path.Combine(d, Extensions.ManifestFile);
        File.WriteAllText(mf, File.ReadAllText(mf).Replace("\"befehl\",", "\"befehl\", \"netz\","));
        Assert.Contains(Extensions.Validate(d).Errors, e => e.Contains("Internet gibt es für Erweiterungen nicht"));
    }

    [Fact(DisplayName = "T-0766 feindlich: Endlosschleife wird per Zeitgrenze, Rechengrenze und Not-Stopp abgebrochen")]
    public void Hostile_endless_loop()
    {
        var loop = Wasmtime.Module.ConvertText("""(module (memory (export "memory") 1) (func (export "_start") (loop $l (br $l))))""");
        var sw = Stopwatch.StartNew();
        var t = ExtensionSandbox.Run(loop, [], [], new(), Fast with { Time = TimeSpan.FromSeconds(1), Fuel = ulong.MaxValue });
        Assert.False(t.Ok); Assert.Contains("Zeitgrenze", t.Error); Assert.True(sw.Elapsed < TimeSpan.FromSeconds(8), sw.Elapsed.ToString());
        var f = ExtensionSandbox.Run(loop, [], [], new(), Fast with { Fuel = 5_000_000 });
        Assert.False(f.Ok); Assert.Contains("Rechengrenze", f.Error);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
        sw.Restart();
        var s = ExtensionSandbox.Run(loop, [], [], new(), Fast with { Time = TimeSpan.FromMinutes(5), Fuel = ulong.MaxValue }, cts.Token);
        Assert.False(s.Ok); Assert.Contains("Not-Stopp", s.Error); Assert.True(sw.Elapsed < TimeSpan.FromSeconds(8), sw.Elapsed.ToString());
    }

    [Fact(DisplayName = "T-0766 feindlich: Speicher sprengen – zu großer Startspeicher wird nicht gestartet, Wachsen endet an der Grenze; Trendo arbeitet danach normal weiter")]
    public void Hostile_memory()
    {
        var big = Wasmtime.Module.ConvertText("""(module (memory (export "memory") 2000) (func (export "_start")))"""); // 125 MB > 64 MB
        var b = ExtensionSandbox.Run(big, [], [], new(), Fast);
        Assert.False(b.Ok); Assert.Contains("Speichergrenze", b.Error);
        // wächst so lange, bis memory.grow −1 liefert; dann meldet es die erreichte Seitenzahl
        var grow = Wasmtime.Module.ConvertText("""
            (module
              (import "wasi_snapshot_preview1" "proc_exit" (func $exit (param i32)))
              (memory (export "memory") 1)
              (func (export "_start")
                (loop $l (br_if $l (i32.ne (memory.grow (i32.const 16)) (i32.const -1))))
                (call $exit (i32.div_u (memory.size) (i32.const 16)))))
            """);
        var before = GC.GetTotalMemory(true);
        var g = ExtensionSandbox.Run(grow, [], [], new(), Fast);
        Assert.False(g.Ok);
        Assert.Contains("Code 63)", g.Error); // 1 + 63 × 16 = 1009 Seiten; 1025 Seiten wären mehr als 64 MB (1024 Seiten) → die Grenze hält genau
        Assert.True(GC.GetTotalMemory(true) - before < 32L << 20);
        // danach läuft das Beispielmodul normal
        var ok = ExtensionSandbox.Run(ExtensionModuleSample.Wasm, [], "a,b\n1,2\n"u8.ToArray(), new(), Fast);
        Assert.True(ok.Ok, ok.Error); Assert.Contains("| 1 | 2 |", Out(ok));
        // zu große Ausgabe wird abgebrochen
        var spam = ExtensionSandbox.Run(ExtensionModuleSample.Wasm, [], Encoding.ASCII.GetBytes(new string('x', 4000)), new(), Fast with { MaxOutput = 1000 });
        Assert.False(spam.Ok); Assert.Contains("Ausgabe", spam.Error);
    }

    static string Repo()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("Repo nicht gefunden");
    }
}

/// <summary>T-0766 (X-04): Funktions-Erweiterungen im Erweiterungsspeicher – Rechte-Manifest, eigene Freigabe, Befehl ins Projekt, Rückgängig, Werkstatt, Aktualisieren.</summary>
public class T0766_ExtensionModules
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-mod-t-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    static (ProjectStore P, string Id) Project(string root, string csv)
    {
        var p = new ProjectStore(Path.Combine(root, "projekte"), new AuditLog(Path.Combine(root, "pa.jsonl")));
        var id = p.CreateNamed("Kunden").Id;
        p.WriteFile(id, "kunden.csv", csv);
        return (p, id);
    }

    [Fact(DisplayName = "T-0766: Modul installieren – Rechte erst nach eigener Freigabe, Befehl legt Tabelle ins Projekt, Rückgängig entfernt sie, Entziehen sperrt")]
    public void Install_grant_run_undo()
    {
        var root = Tmp();
        var s = new ExtensionStore(root);
        var (e, err) = s.Install(ExtensionModuleSample.Write(Tmp()));
        Assert.Empty(err); Assert.Equal(Extensions.ModuleKind, e!.Manifest.Kind);
        var (p, pid) = Project(root, "Name,Ort\nErika Muster,Köln\n");
        // installiert heißt nicht freigegeben
        Assert.Empty(s.GrantedRights(e)); Assert.Empty(s.ReadyCommands());
        var no = s.RunCommand(e.Manifest.Id, "umwandeln", p, pid, "kunden.csv");
        Assert.False(no.Ok); Assert.Contains("Rechte fehlen", no.Message);
        Assert.DoesNotContain(p.Files(pid), f => f.Name.EndsWith(".md"));
        Assert.Null(s.GrantRights(e.Manifest.Id));
        Assert.Single(s.ReadyCommands());
        var r = s.RunCommand(e.Manifest.Id, "umwandeln", p, pid, "kunden.csv");
        Assert.True(r.Ok, r.Message); Assert.Equal("kunden.md", r.OutputRel);
        Assert.Equal("| Name | Ort |\n| --- | --- |\n| Erika Muster | Köln |\n", p.ReadFile(pid, "kunden.md"));
        // nie überschreiben: zweiter Lauf → neue Datei
        var r2 = s.RunCommand(e.Manifest.Id, "umwandeln", p, pid, "kunden.csv");
        Assert.Equal("kunden (2).md", r2.OutputRel);
        var undo = new ExtensionResultUndo(); undo.Record(pid, r); undo.Record(pid, r2);
        Assert.Contains("entfernt", undo.Undo(p)); Assert.DoesNotContain(p.Files(pid), f => f.Name == "kunden (2).md");
        p.WriteFile(pid, "kunden.md", "von Hand geändert");
        Assert.Contains("geändert", undo.Undo(p)); Assert.Equal("von Hand geändert", p.ReadFile(pid, "kunden.md"));
        // falsche Endung, Ausbruch aus dem Projekt
        Assert.Contains("nimmt nur", s.RunCommand(e.Manifest.Id, "umwandeln", p, pid, "kunden.md").Message);
        Assert.False(s.RunCommand(e.Manifest.Id, "umwandeln", p, pid, "../../x.csv").Ok);
        // ausgeschaltet, dann Rechte entzogen
        s.SetEnabled(e.Manifest.Id, false); Assert.Contains("ausgeschaltet", s.RunCommand(e.Manifest.Id, "umwandeln", p, pid, "kunden.csv").Message);
        s.SetEnabled(e.Manifest.Id, true); s.RevokeRights(e.Manifest.Id);
        Assert.Contains("Rechte fehlen", s.RunCommand(e.Manifest.Id, "umwandeln", p, pid, "kunden.csv").Message);
        // Entfernen nimmt Rechte und Datenordner mit
        Assert.Null(s.GrantRights(e.Manifest.Id)); Directory.CreateDirectory(s.DataDir(e.Manifest.Id));
        s.Remove(e.Manifest.Id); Assert.False(File.Exists(s.RightsFile(e.Manifest.Id))); Assert.False(Directory.Exists(s.DataDir(e.Manifest.Id)));
    }

    [Fact(DisplayName = "T-0766: Mit ApprovalGate – Installieren und Rechte freigeben sind zwei getrennte Freigaben; Not-Stopp hält Befehle an")]
    public void Separate_approvals_and_halt()
    {
        var root = Tmp(); var audit = new AuditLog(Path.Combine(root, "a.jsonl")); var gate = new ApprovalGate(audit);
        var s = new ExtensionStore(root, audit, gate);
        string Approved(string action, string target) { var r = gate.Request(Principal.LocalOwner, action, "Test", target); gate.Approve(r.Id, Principal.LocalOwner); return r.Id; }
        var src = ExtensionModuleSample.Write(Tmp());
        var inst = Approved(ExtensionStore.InstallAction, ExtensionStore.SourceHash(src));
        var (e, err) = s.Install(src, inst); Assert.Empty(err);
        // die Installations-Freigabe gibt keine Rechte frei
        Assert.Contains("Freigabe", s.GrantRights(e!.Manifest.Id));
        Assert.NotNull(s.GrantRights(e.Manifest.Id, Approved(ExtensionStore.InstallAction, ExtensionStore.GrantTarget(e))));
        Assert.Empty(s.GrantedRights(e));
        Assert.Null(s.GrantRights(e.Manifest.Id, Approved(ExtensionStore.GrantAction, ExtensionStore.GrantTarget(e))));
        var (p, pid) = Project(root, "a,b\n1,2\n");
        gate.Halt(Principal.LocalOwner, "Test");
        var h = s.RunCommand(e.Manifest.Id, "umwandeln", p, pid, "kunden.csv"); Assert.False(h.Ok); Assert.Contains("Not-Stopp", h.Message);
        gate.Resume(Principal.LocalOwner);
        Assert.True(s.RunCommand(e.Manifest.Id, "umwandeln", p, pid, "kunden.csv").Ok);
    }

    [Fact(DisplayName = "T-0766: Veränderte ZIP wird abgewiesen – Modul getauscht (Prüfsumme im Manifest), fremder Code als .wasm in Daten-Erweiterung, .wasm neben dem Einstieg")]
    public void Tampered_rejected()
    {
        var s = new ExtensionStore(Tmp());
        var a = ExtensionModuleSample.Write(Tmp());
        var bytes = File.ReadAllBytes(Path.Combine(a, "modul.wasm")); bytes[^1] ^= 1; File.WriteAllBytes(Path.Combine(a, "modul.wasm"), bytes);
        var zip = Path.Combine(Tmp(), "x.zip"); ZipFile.CreateFromDirectory(a, zip);
        Assert.Contains(s.Install(zip).Errors, x => x.Contains("Prüfsumme des Moduls stimmt nicht"));
        var b = ExtensionSamples.WriteSample(Tmp()); File.WriteAllBytes(Path.Combine(b, "x.wasm"), ExtensionModuleSample.Wasm);
        Assert.Contains(s.Install(b).Errors, x => x.Contains("kein Programmcode"));
        var c = ExtensionModuleSample.Write(Tmp()); File.WriteAllBytes(Path.Combine(c, "zweites.wasm"), ExtensionModuleSample.Wasm);
        Assert.Contains(s.Install(c).Errors, x => x.Contains("kein Programmcode"));
        // gültiges Manifest, aber das Modul will ins Netz → beim Installieren abgewiesen
        var d = ExtensionModuleSample.Write(Tmp());
        var sock = Wasmtime.Module.ConvertText("""(module (import "wasi_snapshot_preview1" "sock_send" (func (param i32 i32 i32 i32 i32) (result i32))) (memory (export "memory") 1) (func (export "_start")))""");
        File.WriteAllBytes(Path.Combine(d, "modul.wasm"), sock);
        var mf = Path.Combine(d, Extensions.ManifestFile);
        File.WriteAllText(mf, File.ReadAllText(mf).Replace(ExtensionModuleSample.WasmSha256, ExtensionSandbox.Sha256(sock)));
        Assert.Contains(s.Install(d).Errors, x => x.Contains("Netzzugriff"));
        Assert.Empty(s.All());
    }

    [Fact(DisplayName = "T-0766: Werkstatt – eigene Erweiterung aus Vorlage, Quelltext ändern, bauen, als ZIP ausgeben, auf leerem Datenordner installieren, wirken lassen, aktualisieren")]
    public void Workshop_roundtrip_and_update()
    {
        var dev = Tmp();
        var dir = ExtensionWorkshop.Create(dev, "Meine Tabelle");
        var m = ExtensionWorkshop.Load(dir); Assert.Equal("eigene.meine-tabelle", m.Id);
        // Quelltext ändern: Trennzeile wird „ :-: “ (zentriert) – sichtbar andere Wirkung als das Beispiel
        ExtensionWorkshop.SaveSource(dir, ExtensionWorkshop.LoadSource(dir).Replace("(data (i32.const 40) \" --- |\")", "(data (i32.const 40) \" :-: |\")"));
        ExtensionWorkshop.Save(dir, m with { Description = "Zentrierte Tabelle", Commands = [new("umwandeln", "Als zentrierte Tabelle", [".csv", ".txt"], ".md")] });
        Assert.Empty(ExtensionWorkshop.Build(dir));
        var (zip, xerr) = ExtensionWorkshop.Export(dir, ExtensionWorkshop.ExportDir(dev)); Assert.Empty(xerr);
        // fehlerhafter Quelltext baut nicht und lässt das letzte Modul stehen
        var good = File.ReadAllBytes(Path.Combine(dir, "modul.wasm"));
        ExtensionWorkshop.SaveSource(dir, "(module (func (export \"_start\") (kaputt)))");
        Assert.Contains(ExtensionWorkshop.Build(dir), x => x.Contains("Quelltext hat einen Fehler"));
        Assert.Equal(good, File.ReadAllBytes(Path.Combine(dir, "modul.wasm")));

        // leerer Datenordner = frische Trendo-Installation
        var root = Tmp(); var s = new ExtensionStore(root);
        var (e, err) = s.Install(zip!); Assert.Empty(err);
        Assert.Null(s.GrantRights(e!.Manifest.Id));
        var (p, pid) = Project(root, "A,B\n1,2\n");
        var r = s.RunCommand(e.Manifest.Id, "umwandeln", p, pid, "kunden.csv");
        Assert.True(r.Ok, r.Message);
        Assert.Equal("| A | B |\n| :-: | :-: |\n| 1 | 2 |\n", p.ReadFile(pid, r.OutputRel!));

        // Aktualisieren: 1.1.0 ersetzt 1.0.0, Rechte müssen neu freigegeben werden; ältere Fassung wird nicht darübergelegt
        ExtensionWorkshop.SaveSource(dir, ExtensionModuleSample.Source);
        ExtensionWorkshop.Save(dir, ExtensionWorkshop.Load(dir) with { Version = "1.1.0" });
        Assert.Empty(ExtensionWorkshop.Build(dir));
        var (zip2, _) = ExtensionWorkshop.Export(dir, ExtensionWorkshop.ExportDir(dev));
        var (e2, err2) = s.Install(zip2!); Assert.Empty(err2);
        Assert.Equal("1.1.0", e2!.Manifest.Version);
        Assert.Empty(s.GrantedRights(e2));
        Assert.Contains("Rechte fehlen", s.RunCommand(e2.Manifest.Id, "umwandeln", p, pid, "kunden.csv").Message);
        Assert.Contains(s.Install(zip!).Errors, x => x.Contains("neuere Version 1.1.0"));
        Assert.Null(s.GrantRights(e2.Manifest.Id));
        var r3 = s.RunCommand(e2.Manifest.Id, "umwandeln", p, pid, "kunden.csv");
        Assert.Contains("| --- | --- |", p.ReadFile(pid, r3.OutputRel!));
        Assert.Equal(1, Extensions.CompareVersion("1.2.10", "1.2.9")); Assert.Equal(0, Extensions.CompareVersion("1.0.0", "1.0.0"));
    }

    [Fact(DisplayName = "T-0766: Bestehende Daten-Erweiterungen (seite/vorhaben/links/text) bleiben gültig; Befehle nur bei Art „modul“")]
    public void Data_kinds_unchanged()
    {
        var s = new ExtensionStore(Tmp());
        var kat = Path.Combine(Tmp(), "kat"); var (list, err) = ExtensionCatalog.Parse(File.ReadAllText(ExtensionCatalog.BuildLocal(kat)));
        Assert.Empty(err);
        Assert.Equal(["links", "modul", "seite", "text"], list.Select(e => e.Kind).Order());
        foreach (var e in list) Assert.NotNull(ExtensionCatalog.InstallVerified(s, e, ExtensionCatalog.LocalFile(kat, e)!).Ext);
        var d = ExtensionSamples.WriteSample(Tmp()); var mf = Path.Combine(d, Extensions.ManifestFile);
        var m = JsonSerializer.Deserialize<ExtManifest>(File.ReadAllText(mf))!;
        Assert.Null(m.Commands); Assert.DoesNotContain("Commands", File.ReadAllText(mf));
        File.WriteAllText(mf, JsonSerializer.Serialize(m with { Commands = [new("x", "X", [".csv"], ".md")] }));
        Assert.Contains(Extensions.Validate(d).Errors, x => x.Contains("nur bei Funktions-Erweiterungen"));
    }
}
