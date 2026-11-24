using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Wasmtime;

namespace Trendo.Core;

/// <summary>T-0766 (X-04): Grenzen für einen Lauf einer Funktions-Erweiterung. Zeit (Epoche), Rechenschritte (Fuel), Arbeitsspeicher, Ein- und Ausgabegröße
/// und – Befund M1 der Gegenprüfung – Größe des eigenen Datenordners (wirkt während des Laufs).</summary>
public sealed record SandboxLimits(TimeSpan Time, ulong Fuel, long MemoryBytes, int MaxInput, int MaxOutput, long DataQuota = SandboxLimits.DefaultDataQuota)
{
    public const long DefaultDataQuota = 100L << 20;
    public static SandboxLimits Default { get; } = new(TimeSpan.FromSeconds(15), 4_000_000_000, 64L << 20, 16 << 20, 16 << 20);
}

/// <summary>Freigegebene Ordner für einen Lauf: eigener Datenordner (Lesen und Schreiben, mit Größengrenze) und – nur wenn freigegeben – der Projektordner.</summary>
public sealed record SandboxDirs(string? DataDir = null, string? ProjectDir = null, bool ProjectWritable = false);

/// <summary>Ergebnis eines Laufs: Ausgabe (stdout) nur bei Erfolg; sonst ein verständlicher Grund.</summary>
public sealed record SandboxResult(bool Ok, byte[] Output, string Error, string Log);

/// <summary>T-0766 (X-04): Funktions-Erweiterungen laufen als WebAssembly-Modul (WASI preview 1) in Wasmtime – nie als .NET-Code im Trendo-Prozess.
/// Sicherheit vor Umfang:
/// · vor dem Übersetzen: Modul höchstens <see cref="MaxModuleBytes"/>, höchstens <see cref="MaxFunctions"/> Funktionen, ein Speicher, Locals je Funktion
///   und gesamt sowie Körpergröße je Funktion begrenzt (Befunde M2, M3);
/// · Befund H1: kein Zwischenspeicher übersetzten Maschinencodes – jeder Lauf übersetzt aus dem installierten, prüfsummengeprüften .wasm
///   (übersetzter Maschinencode wird nie gespeichert oder geladen). Das Übersetzen zählt zur Zeitgrenze; Not-Stopp beendet den Lauf sofort (das Übersetzen selbst läuft im
///   Hintergrund zu Ende und wird verworfen);
/// · Befund N10: Läufe derselben Erweiterung (gleicher Datenordner) laufen nacheinander;
/// · keine Netz-Funktionen (Importe nur aus einer festen WASI-Liste ohne sock_* und ohne poll_oneoff), keine Umgebungsvariablen, keine geerbten Ein-/Ausgaben;
/// · Dateien nur in ausdrücklich übergebenen Ordnern, über WASI nur lesend. Befund M1: Schreiben in den eigenen Datenordner geht ausschließlich über
///   <c>trendo.daten_schreiben</c>/<c>trendo.daten_loeschen</c>; Trendo prüft die Größengrenze <b>vor</b> jedem Schreiben (nichts wird zu groß angelegt);
///   Vergrößern per fd_filestat_set_size/fd_allocate, Suchen und Schreiben über WASI scheitern an fehlenden Schreibrechten;
/// · Zeitgrenze per Epoche (unterbricht auch Endlosschleifen), Rechengrenze per Fuel, Speichergrenze per Store-Limits, Ein-/Ausgabe gedeckelt;
/// · Abbruch von außen (Not-Stopp) über das <see cref="CancellationToken"/>.
/// Schnittstelle („Trendo-Erweiterung v1“): Modul exportiert <c>_start</c>; argv = [Kennung, Befehl]; Eingabe auf stdin, Ergebnis auf stdout, Hinweise auf stderr;
/// eigener Datenordner lesend unter <c>/daten</c>, schreibend über <c>trendo.daten_schreiben(name_ptr, name_len, daten_ptr, daten_len) → Fehlernummer</c>
/// und <c>trendo.daten_loeschen(name_ptr, name_len) → Fehlernummer</c> (WASI-Fehlernummern, 0 = erledigt, 19 = Grenze erreicht).</summary>
public static class ExtensionSandbox
{
    public const string WasiModule = "wasi_snapshot_preview1";
    public const int MaxModuleBytes = 4 << 20;
    public const int MaxFunctions = 20_000;
    public const int MaxImports = 200;
    public const string TrendoModule = "trendo";
    /// <summary>Höchstzahl Dateien im eigenen Datenordner.</summary>
    public const int MaxDataFiles = 1000;
    /// <summary>Trendo-eigene Funktionen für Erweiterungen (Befund M1: Schreiben in den Datenordner nur hierüber, mit Größengrenze).</summary>
    public static readonly string[] TrendoImports = ["daten_schreiben", "daten_loeschen"];
    /// <summary>Befund M3: Locals je Funktion, Locals im ganzen Modul, Körpergröße je Funktion – geprüft vor dem Übersetzen.</summary>
    public const int MaxLocalsPerFunction = 2_000, MaxLocalsTotal = 200_000, MaxBodyBytes = 512 << 10;
    /// <summary>Befund N11: Namen im Datenordner – höchstens 255 Zeichen, höchstens 8 Ebenen.</summary>
    public const int MaxNameChars = 255, MaxNameDepth = 8;
    static readonly ConcurrentDictionary<string, SemaphoreSlim> DataLocks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Erlaubte WASI-Funktionen. Bewusst nicht dabei: sock_* (Netz), poll_oneoff (Warten ohne Zeitgrenze), path_symlink/path_link/path_readlink.</summary>
    public static readonly string[] AllowedImports =
    [
        "args_get", "args_sizes_get", "environ_get", "environ_sizes_get", "clock_res_get", "clock_time_get", "random_get", "sched_yield", "proc_exit",
        "fd_read", "fd_write", "fd_close", "fd_seek", "fd_tell", "fd_sync", "fd_datasync", "fd_advise", "fd_allocate", "fd_pread", "fd_pwrite",
        "fd_fdstat_get", "fd_fdstat_set_flags", "fd_filestat_get", "fd_filestat_set_size", "fd_filestat_set_times", "fd_prestat_get", "fd_prestat_dir_name", "fd_readdir", "fd_renumber",
        "path_open", "path_filestat_get", "path_filestat_set_times", "path_create_directory", "path_remove_directory", "path_unlink_file", "path_rename",
    ];

    // WASI-Fehlernummern (preview 1)
    const int EACCES = 2, EDQUOT = 19, EFAULT = 21, EINVAL = 28, EIO = 29, ENOENT = 44;

    /// <summary>Feste Ausstattung: keine GC-/Ausnahme-/Mehrspeicher-/64-Bit-Speicher-/Thread-Erweiterungen – weniger Angriffsfläche, klarere Fehlermeldungen (Befund N8).</summary>
    static Config NewConfig() => new Config().WithFuelConsumption(true).WithEpochInterruption(true).WithWasmThreads(false).WithDebugInfo(false)
        .WithGc(false).WithExceptions(false).WithMultiMemory(false).WithMemory64(false).WithComponentModel(false).WithParallelCompilation(false);

    /// <summary>Befund M2: Prüfung der Modulform <b>vor</b> dem Übersetzen – Größe, Anzahl Funktionen und Importe, höchstens ein Speicher. Null = in Ordnung.</summary>
    public static string? ShapeError(byte[] wasm)
    {
        if (wasm.Length < 8 || wasm[0] != 0 || wasm[1] != (byte)'a' || wasm[2] != (byte)'s' || wasm[3] != (byte)'m') return "Das ist kein WebAssembly-Modul.";
        if (wasm.Length > MaxModuleBytes) return $"Modul ist zu groß ({wasm.Length >> 10} KB, erlaubt höchstens {MaxModuleBytes >> 20} MB).";
        var pos = 8;
        while (pos < wasm.Length)
        {
            var id = wasm[pos++];
            if (!Leb(wasm, ref pos, out var size) || pos + (long)size > wasm.Length) return "Modul ist beschädigt.";
            var p = pos; var end = pos + (int)size;
            if (id is 2 or 3 or 5 or 10 && Leb(wasm, ref p, out var count))
            {
                if (id == 2 && count > MaxImports) return $"Modul hat zu viele Importe ({count}).";
                if (id is 3 or 10 && count > MaxFunctions) return $"Modul hat zu viele Funktionen ({count}, erlaubt höchstens {MaxFunctions}).";
                if (id == 5 && count > 1) return "Mehr als ein Speicher ist für Erweiterungen nicht erlaubt.";
                if (id == 10 && CodeError(wasm, p, end, count) is { } code) return code;
            }
            pos = end;
        }
        return null;
    }

    /// <summary>Befund M3: Funktionskörper durchgehen – Körpergröße, Locals je Funktion und gesamt (Übersetzungsaufwand wächst damit).</summary>
    static string? CodeError(byte[] b, int pos, int end, uint count)
    {
        long total = 0;
        for (var f = 0; f < count; f++)
        {
            if (!Leb(b, ref pos, out var size) || pos + (long)size > end) return "Modul ist beschädigt.";
            if (size > MaxBodyBytes) return $"Eine Funktion ist zu groß ({size >> 10} KB, erlaubt höchstens {MaxBodyBytes >> 10} KB).";
            var p = pos; var bodyEnd = pos + (int)size;
            if (!Leb(b, ref p, out var groups)) return "Modul ist beschädigt.";
            long locals = 0;
            for (var g = 0; g < groups; g++)
            {
                if (!Leb(b, ref p, out var n) || p >= bodyEnd) return "Modul ist beschädigt.";
                locals += n;
                var type = b[p++];
                if (type is 0x63 or 0x64 && !Leb(b, ref p, out _)) return "Modul ist beschädigt."; // (ref null? heaptyp)
                if (locals > MaxLocalsPerFunction) return $"Eine Funktion hat zu viele lokale Variablen (mehr als {MaxLocalsPerFunction}).";
            }
            if ((total += locals) > MaxLocalsTotal) return $"Modul hat zu viele lokale Variablen (mehr als {MaxLocalsTotal}).";
            pos = bodyEnd;
        }
        return null;
    }

    static bool Leb(byte[] b, ref int pos, out uint value)
    {
        value = 0;
        for (var shift = 0; shift < 35; shift += 7)
        {
            if (pos >= b.Length) return false;
            var x = b[pos++];
            value |= (uint)(x & 0x7F) << shift;
            if ((x & 0x80) == 0) return true;
        }
        return false;
    }

    /// <summary>Importe und Startpunkt eines übersetzten Moduls prüfen.</summary>
    static List<string> Check(Module module)
    {
        var err = new List<string>();
        foreach (var i in module.Imports)
        {
            if (i.ModuleName == TrendoModule && i is FunctionImport && TrendoImports.Contains(i.Name)) continue;
            if (i.ModuleName != WasiModule) { err.Add($"Nicht erlaubter Zugriff: {i.ModuleName}.{i.Name} – Erweiterungen bekommen nur die Trendo-Sandbox."); continue; }
            if (i is not FunctionImport) { err.Add($"Nicht erlaubter Import: {i.Name}."); continue; }
            if (!AllowedImports.Contains(i.Name))
                err.Add(i.Name.StartsWith("sock_", StringComparison.Ordinal) ? $"Netzzugriff ({i.Name}) ist für Erweiterungen gesperrt." : $"Nicht erlaubte Systemfunktion: {i.Name}.");
        }
        if (!module.Exports.Any(e => e.Name == "_start" && e is FunctionExport)) err.Add("Startpunkt _start fehlt.");
        return err;
    }

    /// <summary>Prüft ein Modul ohne es auszuführen: Form (vor dem Übersetzen), gültiges WebAssembly, nur erlaubte Importe, Startpunkt <c>_start</c>. Leere Liste = in Ordnung.</summary>
    public static List<string> Inspect(byte[] wasm)
    {
        if (ShapeError(wasm) is { } shape) return [shape];
        try
        {
            using var engine = new Engine(NewConfig());
            using var module = Module.FromBytes(engine, "erweiterung", wasm);
            return Check(module);
        }
        catch (WasmtimeException e) { return ["Modul ist ungültig: " + Reason(e.Message)]; }
    }

    /// <summary>Baut ein Modul aus WebAssembly-Text (WAT) – mit dem Übersetzer aus der Wasmtime-Bibliothek, ohne weiteres Werkzeug. Danach <see cref="Inspect"/>.</summary>
    public static (byte[]? Wasm, List<string> Errors) BuildFromText(string wat)
    {
        if (string.IsNullOrWhiteSpace(wat)) return (null, ["Quelltext ist leer."]);
        if (wat.Length > 4 * MaxModuleBytes) return (null, ["Quelltext ist zu lang."]);
        byte[] wasm;
        try { wasm = Module.ConvertText(wat); }
        catch (WasmtimeException e) { return (null, ["Quelltext hat einen Fehler: " + Reason(e.Message)]); }
        var err = Inspect(wasm);
        return err.Count == 0 ? (wasm, []) : (null, err);
    }

    public static string Sha256(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

    /// <summary>Größe eines Ordners (alle Dateien), 0 wenn er fehlt.</summary>
    public static long DirSize(string dir) => Directory.Exists(dir) ? new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0;

    /// <summary>Führt ein Modul aus. Wirft nie wegen des Moduls – jedes Fehlverhalten wird zu <see cref="SandboxResult.Ok"/> = false mit Grund.
    /// Übersetzt wird bei jedem Lauf aus <paramref name="wasm"/> (Befund H1); die Zeit dafür zählt zur Zeitgrenze.</summary>
    public static SandboxResult Run(byte[] wasm, IReadOnlyList<string> args, byte[] input, SandboxDirs dirs, SandboxLimits limits, CancellationToken ct = default)
    {
        var clock = Stopwatch.StartNew();
        TimeSpan Left() { var l = limits.Time - clock.Elapsed; return l > TimeSpan.FromMilliseconds(1) ? l : TimeSpan.FromMilliseconds(1); }
        if (input.Length > limits.MaxInput) return new(false, [], $"Eingabe ist größer als {limits.MaxInput >> 20} MB.", "");
        if (ct.IsCancellationRequested) return new(false, [], "Abgebrochen (Not-Stopp).", "");
        if (ShapeError(wasm) is { } shape) return new(false, [], shape, "");

        var work = Path.Combine(Path.GetTempPath(), "trendo-sbx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var stdinFile = Path.Combine(work, "eingabe");
        File.WriteAllBytes(stdinFile, input);
        var output = new MemoryStream(); var log = new MemoryStream();
        var outOver = false; var timedOut = false; var quotaHit = false; var started = false;
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        SemaphoreSlim? dataLock = null;
        var quotaMsg = $"Datenordner-Grenze ({limits.DataQuota >> 20} MB) erreicht – abgebrochen, zu große Dateien entfernt.";
        try
        {
            // H1/M3: immer aus dem .wasm übersetzen; das Übersetzen zählt zur Zeitgrenze, Not-Stopp wartet nicht darauf
            var engine = new Engine(NewConfig());
            var compile = Task.Run(() => Module.FromBytes(engine, "erweiterung", wasm));
            bool compiled;
            try { compiled = compile.Wait(Left(), ct); }
            catch (OperationCanceledException) { compiled = false; }
            catch (AggregateException a) when (a.InnerException is WasmtimeException w) { engine.Dispose(); return new(false, [], "Modul ist ungültig: " + Reason(w.Message), ""); }
            if (!compiled)
            {
                // läuft im Hintergrund zu Ende und wird verworfen – das Ergebnis wird nie ausgeführt
                compile.ContinueWith(t => { if (t.IsCompletedSuccessfully) t.Result.Dispose(); engine.Dispose(); }, TaskScheduler.Default);
                return new(false, [], ct.IsCancellationRequested ? "Abgebrochen (Not-Stopp) – noch beim Übersetzen." : $"Zeitgrenze erreicht ({limits.Time.TotalSeconds:0} s) schon beim Übersetzen – abgebrochen.", "");
            }
            using var _e = engine;
            void Interrupt() { try { engine.IncrementEpoch(); } catch (ObjectDisposedException) { } } // N7: nie gegen eine freigegebene Engine
            using var module = compile.Result;
            if (Check(module) is { Count: > 0 } bad) return new(false, [], string.Join(" ", bad), "");
            using var linker = new Linker(engine);
            linker.DefineWasi();
            using var store = new Store(engine);
            store.SetLimits(memorySize: limits.MemoryBytes, tableElements: 100_000, instances: 1, tables: 4, memories: 1);
            store.Fuel = limits.Fuel;
            store.SetEpochDeadline(1);
            var wasi = new WasiConfiguration().WithArgs(args).WithStandardInput(stdinFile)
                .WithStandardOutputCallback(d => { lock (output) { if (output.Length + d.Length > limits.MaxOutput) { outOver = true; throw new IOException("Ausgabe zu groß"); } output.Write(d); } })
                .WithStandardErrorCallback(d => { lock (log) { if (log.Length + d.Length <= 8192) log.Write(d); } });
            // M1: über WASI nur lesend – geschrieben wird nur über trendo.daten_schreiben mit Größengrenze
            if (dirs.DataDir is { } dd) { Directory.CreateDirectory(dd); wasi = wasi.WithPreopenedDirectory(dd, "/daten", false); }
            if (dirs.ProjectDir is { } pd && Directory.Exists(pd)) wasi = wasi.WithPreopenedDirectory(pd, "/projekt", dirs.ProjectWritable);
            store.SetWasiConfiguration(wasi);
            // N10: Läufe mit demselben Datenordner nacheinander – Grenze und Aufräumen sehen nur den eigenen Lauf
            if (dirs.DataDir is { } lockDir)
            {
                dataLock = DataLocks.GetOrAdd(Path.GetFullPath(lockDir), _ => new SemaphoreSlim(1, 1));
                bool got;
                try { got = dataLock.Wait(Left(), ct); } catch (OperationCanceledException) { got = false; }
                if (!got) { dataLock = null; return new(false, [], ct.IsCancellationRequested ? "Abgebrochen (Not-Stopp)." : "Diese Erweiterung läuft gerade noch – bitte gleich noch einmal versuchen.", ""); }
            }
            DefineDataApi(linker, dirs.DataDir, limits.DataQuota, written, () => { quotaHit = true; Interrupt(); });

            var timer = new Timer(_ => { timedOut = true; Interrupt(); }, null, Left(), Timeout.InfiniteTimeSpan);
            string? fail = null;
            try
            {
                using (ct.Register(Interrupt))
                {
                    try
                    {
                        var inst = linker.Instantiate(store, module);
                        var start = inst.GetAction("_start") ?? throw new WasmtimeException("Startpunkt _start fehlt.");
                        started = true;
                        start();
                    }
                    catch (TrapException t)
                    {
                        fail = quotaHit ? quotaMsg : t.Type switch
                        {
                            TrapCode.Interrupt => ct.IsCancellationRequested ? "Abgebrochen (Not-Stopp)." : timedOut ? $"Zeitgrenze erreicht ({limits.Time.TotalSeconds:0} s) – abgebrochen." : "Abgebrochen.",
                            TrapCode.OutOfFuel => "Rechengrenze erreicht (Endlosschleife?) – abgebrochen.",
                            TrapCode.StackOverflow => "Zu tiefe Verschachtelung – abgebrochen.",
                            TrapCode.MemoryOutOfBounds => "Unerlaubter Speicherzugriff – abgebrochen.",
                            TrapCode.AllocationTooLarge => $"Speichergrenze ({limits.MemoryBytes >> 20} MB) erreicht – abgebrochen.",
                            _ => outOver ? $"Ausgabe größer als {limits.MaxOutput >> 20} MB – abgebrochen." : "Erweiterung ist mit einem Fehler abgebrochen (" + t.Type + ").",
                        };
                    }
                    catch (WasmtimeException w) when (w.ExitCode is { } code)
                    {
                        if (quotaHit) fail = quotaMsg;
                        else if (code != 0) fail = $"Erweiterung meldet Fehler (Code {code}).";
                    }
                    catch (WasmtimeException w) { fail = quotaHit ? quotaMsg : Explain(w.Message, started, limits); }
                }
            }
            finally
            {
                // N7: Zeitgeber anhalten und auf einen laufenden Rückruf warten, bevor die Engine freigegeben wird
                using var done = new ManualResetEvent(false);
                if (timer.Dispose(done)) done.WaitOne(TimeSpan.FromSeconds(5));
            }
            if (dirs.DataDir is { } q && (quotaHit || DirSize(q) > limits.DataQuota)) { TrimDataDir(q, limits.DataQuota, written); fail = quotaMsg; }
            var logText = Encoding.UTF8.GetString(log.ToArray());
            if (fail is null && outOver) fail = $"Ausgabe größer als {limits.MaxOutput >> 20} MB – abgebrochen.";
            if (fail is not null && ExtensionText(logText) is { Length: > 0 } said) fail += " Text der Erweiterung: „" + said + "“";
            return fail is null ? new(true, output.ToArray(), "", logText) : new(false, [], fail, logText);
        }
        catch (WasmtimeException e) { return new(false, [], Explain(e.Message, started, limits), ""); }
        finally { dataLock?.Release(); TempDir.Delete(work); }
    }

    /// <summary>Befund M1: Schreiben in den eigenen Datenordner. Name: relativer Pfad ohne „..“, ohne Laufwerk, ohne Verknüpfungen auf dem Weg.
    /// Die Grenze wird <b>vor</b> dem Schreiben geprüft (Ordnergröße ohne die alte Fassung der Datei + neue Länge); bei Überschreitung bekommt das Modul
    /// EDQUOT und der Lauf wird abgebrochen. Geschrieben wird atomar (erst daneben, dann ersetzen). Ohne Recht „daten-ordner“: EACCES.</summary>
    static void DefineDataApi(Linker linker, string? dir, long quota, HashSet<string> written, Action exceeded)
    {
        static bool Fault(Exception e) => e is ArgumentOutOfRangeException or IndexOutOfRangeException or InvalidOperationException or ArgumentException;
        string? Resolve(Caller c, int np, int nl)
        {
            if (dir is null || nl is <= 0 or > 255) return null;
            var mem = c.GetMemory("memory") ?? throw new InvalidOperationException();
            var name = mem.ReadString(np, nl, Encoding.UTF8).Replace('\\', '/');
            if (!NameAllowed(name)) return null;
            var root = Path.GetFullPath(dir) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(Path.Combine(root, name));
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || full.Length >= 260) return null;
            // N11: Ordner als Ziel, Verknüpfungen/Junctions auf dem Weg oder als Ziel → sofort ablehnen
            if (Directory.Exists(full)) return null;
            for (var d = new DirectoryInfo(Path.GetDirectoryName(full)!); d is not null && d.FullName.Length + 1 >= root.Length; d = d.Parent)
                if (d.Exists && (d.LinkTarget is not null || d.Attributes.HasFlag(FileAttributes.ReparsePoint))) return null;
            if (File.Exists(full) && (new FileInfo(full).LinkTarget is not null || File.GetAttributes(full).HasFlag(FileAttributes.ReparsePoint))) return null;
            return full;
        }
        linker.DefineFunction(TrendoModule, "daten_schreiben", (Caller c, int np, int nl, int dp, int dl) =>
        {
            if (dir is null) return EACCES;
            try
            {
                if (dl < 0) return EINVAL;
                if (Resolve(c, np, nl) is not { } full) return EINVAL;
                var old = File.Exists(full) ? new FileInfo(full).Length : 0;
                if (DirSize(dir) - old + dl > quota) { exceeded(); return EDQUOT; }
                // N11: Ordner zählen mit – neue Datei und alle neu anzulegenden Ordner zusammen höchstens MaxDataFiles Einträge
                if (!File.Exists(full))
                {
                    var newDirs = 0;
                    for (var d = new DirectoryInfo(Path.GetDirectoryName(full)!); d is not null && !d.Exists; d = d.Parent) newDirs++;
                    var entries = Directory.Exists(dir) ? Directory.EnumerateFileSystemEntries(dir, "*", SearchOption.AllDirectories).Count() : 0;
                    if (entries + newDirs + 1 > MaxDataFiles) { exceeded(); return EDQUOT; }
                }
                var bytes = c.GetMemory("memory")!.GetSpan(dp, dl).ToArray();
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                AtomicFile.WriteAllBytes(full, bytes);
                written.Add(full);
                return 0;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return EIO; }
            catch (Exception e) when (Fault(e)) { return EFAULT; }
        });
        linker.DefineFunction(TrendoModule, "daten_loeschen", (Caller c, int np, int nl) =>
        {
            if (dir is null) return EACCES;
            try
            {
                if (Resolve(c, np, nl) is not { } full) return EINVAL;
                if (!File.Exists(full)) return ENOENT;
                File.Delete(full); return 0;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return EIO; }
            catch (Exception e) when (Fault(e)) { return EFAULT; }
        });
    }

    /// <summary>Befund N11: erlaubter Name im Datenordner – relativ, höchstens 255 Zeichen und 8 Ebenen, ohne Windows-Gerätenamen (auch mit Endung,
    /// auch COM¹–³/LPT¹–³), ohne Punkt oder Leerzeichen am Ende eines Teils, ohne verbotene Zeichen.</summary>
    public static bool NameAllowed(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > MaxNameChars || !FreeAssets.SafeRel(name)) return false;
        var parts = name.Split('/');
        if (parts.Length > MaxNameDepth) return false;
        foreach (var part in parts)
        {
            if (part is "" or "." or ".." || part.EndsWith('.') || part.EndsWith(' ') || part.StartsWith(' ')) return false;
            if (part.Any(ch => ch < 32 || "<>:\"|?*\\".Contains(ch))) return false;
            var stem = part.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
            if (stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" or "CLOCK$") return false;
            if (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && "0123456789¹²³".Contains(stem[3])) return false;
        }
        return true;
    }

    /// <summary>Nach einer Überschreitung: nur in <b>diesem</b> Lauf geschriebene Dateien, größte zuerst, löschen, bis der Ordner wieder unter der Grenze ist
    /// (Befund N10 – nie Dateien anderer Läufe).</summary>
    static void TrimDataDir(string dir, long quota, HashSet<string> written)
    {
        try
        {
            var files = written.Where(File.Exists).Select(f => new FileInfo(f)).OrderByDescending(f => f.Length).ToList();
            foreach (var f in files) { if (DirSize(dir) <= quota) break; f.Delete(); }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Befund N1: Text der Erweiterung (stderr) gekürzt auf 200 Zeichen, ohne Steuer- und Formatzeichen.</summary>
    public static string ExtensionText(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s) { if (sb.Length >= 200) break; sb.Append(char.IsControl(ch) || char.GetUnicodeCategory(ch) is System.Globalization.UnicodeCategory.Format ? ' ' : ch); }
        var t = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        return s.Length > 200 && t.Length > 0 ? t + " …" : t;
    }

    /// <summary>Befund N8: genaue Meldung je nach Ursache – Speicher-Anzahl, Speichergrenze, Fehler beim Laufen statt „ließ sich nicht starten“.</summary>
    static string Explain(string m, bool started, SandboxLimits limits)
    {
        if (m.Contains("multi-memory", StringComparison.OrdinalIgnoreCase) || m.Contains("multiple memories", StringComparison.OrdinalIgnoreCase) || m.Contains("memory count", StringComparison.OrdinalIgnoreCase) || m.Contains("count of memories", StringComparison.OrdinalIgnoreCase))
            return "Mehr als ein Speicher ist für Erweiterungen nicht erlaubt.";
        if (m.Contains("memory", StringComparison.OrdinalIgnoreCase) && (m.Contains("limit", StringComparison.OrdinalIgnoreCase) || m.Contains("minimum", StringComparison.OrdinalIgnoreCase)))
            return $"Speichergrenze ({limits.MemoryBytes >> 20} MB) überschritten – {(started ? "abgebrochen" : "nicht gestartet")}.";
        if (m.Contains("gc heap", StringComparison.OrdinalIgnoreCase) || m.Contains("out of memory", StringComparison.OrdinalIgnoreCase))
            return "Speicher der Laufzeit erschöpft – abgebrochen.";
        return (started ? "Erweiterung ist mit einem Fehler abgebrochen: " : "Erweiterung ließ sich nicht starten: ") + Reason(m);
    }

    /// <summary>Kernsatz einer Wasmtime-Meldung: die letzte „Caused by“-Zeile, sonst die erste Zeile (gekürzt, ohne Steuerzeichen).</summary>
    static string Reason(string s)
    {
        var l = s.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var cause = Array.FindLastIndex(l, x => x.StartsWith("Caused by", StringComparison.Ordinal));
        var line = cause >= 0 && cause + 1 < l.Length ? System.Text.RegularExpressions.Regex.Replace(l[^1], @"^\d+:\s*", "") : l.Length == 0 ? s : l[0];
        return ExtensionText(line);
    }
}
