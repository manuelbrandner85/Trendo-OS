using System.Security.Cryptography;
using Wasmtime;

namespace Trendo.Core;

/// <summary>T-0766 (X-04): Grenzen für einen Lauf einer Funktions-Erweiterung. Zeit (Epoche), Rechenschritte (Fuel), Arbeitsspeicher, Ein- und Ausgabegröße.</summary>
public sealed record SandboxLimits(TimeSpan Time, ulong Fuel, long MemoryBytes, int MaxInput, int MaxOutput)
{
    public static SandboxLimits Default { get; } = new(TimeSpan.FromSeconds(15), 4_000_000_000, 64L << 20, 16 << 20, 16 << 20);
}

/// <summary>Freigegebene Ordner für einen Lauf: eigener Datenordner (Lesen und Schreiben) und – nur wenn freigegeben – der Projektordner.</summary>
public sealed record SandboxDirs(string? DataDir = null, string? ProjectDir = null, bool ProjectWritable = false);

/// <summary>Ergebnis eines Laufs: Ausgabe (stdout) nur bei Erfolg; sonst ein verständlicher Grund.</summary>
public sealed record SandboxResult(bool Ok, byte[] Output, string Error, string Log);

/// <summary>T-0766 (X-04): Funktions-Erweiterungen laufen als WebAssembly-Modul (WASI preview 1) in Wasmtime – nie als .NET-Code im Trendo-Prozess.
/// Sicherheit vor Umfang:
/// · keine Netz-Funktionen (Importe nur aus einer festen WASI-Liste ohne sock_* und ohne poll_oneoff), keine Umgebungsvariablen, keine geerbten Ein-/Ausgaben;
/// · Dateien nur in ausdrücklich übergebenen Ordnern (<see cref="SandboxDirs"/>) – alles andere sieht das Modul gar nicht;
/// · Zeitgrenze per Epoche (unterbricht auch Endlosschleifen), Rechengrenze per Fuel, Speichergrenze per Store-Limits, Ein-/Ausgabe gedeckelt;
/// · Abbruch von außen (Not-Stopp) über das <see cref="CancellationToken"/>.
/// Schnittstelle („Trendo-Erweiterung v1“): Modul exportiert <c>_start</c>; argv = [Kennung, Befehl]; Eingabe auf stdin, Ergebnis auf stdout, Hinweise auf stderr.</summary>
public static class ExtensionSandbox
{
    public const string WasiModule = "wasi_snapshot_preview1";

    /// <summary>Erlaubte WASI-Funktionen. Bewusst nicht dabei: sock_* (Netz), poll_oneoff (Warten ohne Zeitgrenze), path_symlink/path_link/path_readlink.</summary>
    public static readonly string[] AllowedImports =
    [
        "args_get", "args_sizes_get", "environ_get", "environ_sizes_get", "clock_res_get", "clock_time_get", "random_get", "sched_yield", "proc_exit",
        "fd_read", "fd_write", "fd_close", "fd_seek", "fd_tell", "fd_sync", "fd_datasync", "fd_advise", "fd_allocate", "fd_pread", "fd_pwrite",
        "fd_fdstat_get", "fd_fdstat_set_flags", "fd_filestat_get", "fd_filestat_set_size", "fd_filestat_set_times", "fd_prestat_get", "fd_prestat_dir_name", "fd_readdir", "fd_renumber",
        "path_open", "path_filestat_get", "path_filestat_set_times", "path_create_directory", "path_remove_directory", "path_unlink_file", "path_rename",
    ];

    static Config NewConfig() => new Config().WithFuelConsumption(true).WithEpochInterruption(true).WithWasmThreads(false).WithDebugInfo(false);

    /// <summary>Prüft ein Modul ohne es auszuführen: gültiges WebAssembly, nur erlaubte Importe, Startpunkt <c>_start</c>. Leere Liste = in Ordnung.</summary>
    public static List<string> Inspect(byte[] wasm)
    {
        var err = new List<string>();
        if (wasm.Length < 8 || wasm[0] != 0 || wasm[1] != (byte)'a' || wasm[2] != (byte)'s' || wasm[3] != (byte)'m') return ["Das ist kein WebAssembly-Modul."];
        try
        {
            using var engine = new Engine(NewConfig());
            using var module = Module.FromBytes(engine, "erweiterung", wasm);
            foreach (var i in module.Imports)
            {
                if (i.ModuleName != WasiModule) { err.Add($"Nicht erlaubter Zugriff: {i.ModuleName}.{i.Name} – Erweiterungen bekommen nur die Trendo-Sandbox."); continue; }
                if (i is not FunctionImport) { err.Add($"Nicht erlaubter Import: {i.Name}."); continue; }
                if (!AllowedImports.Contains(i.Name))
                    err.Add(i.Name.StartsWith("sock_", StringComparison.Ordinal) ? $"Netzzugriff ({i.Name}) ist für Erweiterungen gesperrt." : $"Nicht erlaubte Systemfunktion: {i.Name}.");
            }
            if (!module.Exports.Any(e => e.Name == "_start" && e is FunctionExport)) err.Add("Startpunkt _start fehlt.");
        }
        catch (WasmtimeException e) { err.Add("Modul ist ungültig: " + FirstLine(e.Message)); }
        return err;
    }

    /// <summary>Baut ein Modul aus WebAssembly-Text (WAT) – mit dem Übersetzer aus der Wasmtime-Bibliothek, ohne weiteres Werkzeug. Danach <see cref="Inspect"/>.</summary>
    public static (byte[]? Wasm, List<string> Errors) BuildFromText(string wat)
    {
        if (string.IsNullOrWhiteSpace(wat)) return (null, ["Quelltext ist leer."]);
        byte[] wasm;
        try { wasm = Module.ConvertText(wat); }
        catch (WasmtimeException e) { return (null, ["Quelltext hat einen Fehler: " + FirstLine(e.Message)]); }
        var err = Inspect(wasm);
        return err.Count == 0 ? (wasm, []) : (null, err);
    }

    public static string Sha256(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

    /// <summary>Führt ein Modul aus. Wirft nie wegen des Moduls – jedes Fehlverhalten wird zu <see cref="SandboxResult.Ok"/> = false mit Grund.</summary>
    public static SandboxResult Run(byte[] wasm, IReadOnlyList<string> args, byte[] input, SandboxDirs dirs, SandboxLimits limits, CancellationToken ct = default)
    {
        var bad = Inspect(wasm);
        if (bad.Count > 0) return new(false, [], string.Join(" ", bad), "");
        if (input.Length > limits.MaxInput) return new(false, [], $"Eingabe ist größer als {limits.MaxInput >> 20} MB.", "");
        if (ct.IsCancellationRequested) return new(false, [], "Abgebrochen (Not-Stopp).", "");

        var work = Path.Combine(Path.GetTempPath(), "trendo-sbx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var stdinFile = Path.Combine(work, "eingabe");
        File.WriteAllBytes(stdinFile, input);
        var output = new MemoryStream(); var log = new MemoryStream();
        var outOver = false;
        var timedOut = false;
        try
        {
            using var engine = new Engine(NewConfig());
            using var module = Module.FromBytes(engine, "erweiterung", wasm);
            using var linker = new Linker(engine);
            linker.DefineWasi();
            using var store = new Store(engine);
            store.SetLimits(memorySize: limits.MemoryBytes, tableElements: 100_000, instances: 1, tables: 4, memories: 1);
            store.Fuel = limits.Fuel;
            store.SetEpochDeadline(1);
            var wasi = new WasiConfiguration().WithArgs(args).WithStandardInput(stdinFile)
                .WithStandardOutputCallback(d => { lock (output) { if (output.Length + d.Length > limits.MaxOutput) { outOver = true; throw new IOException("Ausgabe zu groß"); } output.Write(d); } })
                .WithStandardErrorCallback(d => { lock (log) { if (log.Length + d.Length <= 8192) log.Write(d); } });
            if (dirs.DataDir is { } dd) { Directory.CreateDirectory(dd); wasi = wasi.WithPreopenedDirectory(dd, "/daten", true); }
            if (dirs.ProjectDir is { } pd && Directory.Exists(pd)) wasi = wasi.WithPreopenedDirectory(pd, "/projekt", dirs.ProjectWritable);
            store.SetWasiConfiguration(wasi);

            using var timer = new Timer(_ => { timedOut = true; engine.IncrementEpoch(); }, null, limits.Time, Timeout.InfiniteTimeSpan);
            using var reg = ct.Register(engine.IncrementEpoch);
            string? fail = null;
            try
            {
                var inst = linker.Instantiate(store, module);
                var start = inst.GetAction("_start") ?? throw new WasmtimeException("Startpunkt _start fehlt.");
                start();
            }
            catch (TrapException t)
            {
                fail = t.Type switch
                {
                    TrapCode.Interrupt => ct.IsCancellationRequested ? "Abgebrochen (Not-Stopp)." : timedOut ? $"Zeitgrenze erreicht ({limits.Time.TotalSeconds:0} s) – abgebrochen." : "Abgebrochen.",
                    TrapCode.OutOfFuel => "Rechengrenze erreicht (Endlosschleife?) – abgebrochen.",
                    TrapCode.StackOverflow => "Zu tiefe Verschachtelung – abgebrochen.",
                    TrapCode.MemoryOutOfBounds => "Unerlaubter Speicherzugriff – abgebrochen.",
                    _ => outOver ? $"Ausgabe größer als {limits.MaxOutput >> 20} MB – abgebrochen." : "Erweiterung ist mit einem Fehler abgebrochen (" + t.Type + ").",
                };
            }
            catch (WasmtimeException w) when (w.ExitCode is { } code)
            {
                if (code != 0) fail = $"Erweiterung meldet Fehler (Code {code}).";
            }
            catch (WasmtimeException w)
            {
                var m = w.Message;
                fail = m.Contains("memory", StringComparison.OrdinalIgnoreCase) && (m.Contains("limit", StringComparison.OrdinalIgnoreCase) || m.Contains("minimum", StringComparison.OrdinalIgnoreCase) || m.Contains("resource", StringComparison.OrdinalIgnoreCase))
                    ? $"Speichergrenze ({limits.MemoryBytes >> 20} MB) überschritten – nicht gestartet."
                    : "Erweiterung ließ sich nicht starten: " + FirstLine(m);
            }
            var logText = System.Text.Encoding.UTF8.GetString(log.ToArray());
            if (fail is null && outOver) fail = $"Ausgabe größer als {limits.MaxOutput >> 20} MB – abgebrochen.";
            if (fail is not null && logText.Length > 0) fail += " Hinweis der Erweiterung: " + FirstLine(logText);
            return fail is null ? new(true, output.ToArray(), "", logText) : new(false, [], fail, logText);
        }
        catch (WasmtimeException e) { return new(false, [], "Erweiterung ließ sich nicht starten: " + FirstLine(e.Message), ""); }
        finally { TempDir.Delete(work); }
    }

    /// <summary>Kernsatz einer Wasmtime-Meldung: die letzte „Caused by“-Zeile, sonst die erste Zeile.</summary>
    static string FirstLine(string s)
    {
        var l = s.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var cause = Array.FindLastIndex(l, x => x.StartsWith("Caused by", StringComparison.Ordinal));
        if (cause >= 0 && cause + 1 < l.Length) return System.Text.RegularExpressions.Regex.Replace(l[^1], @"^\d+:\s*", "");
        return l.Length == 0 ? s : l[0];
    }
}
