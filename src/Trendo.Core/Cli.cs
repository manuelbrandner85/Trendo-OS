using System.Diagnostics;

namespace Trendo.Core;

/// <summary>Startet ein Kommandozeilenprogramm ohne Shell, mit Zeitlimit. Gibt Exitcode und Ausgabe zurück.</summary>
public static class Cli
{
    public sealed record Result(int Exit, string Out, string Err) { public bool Ok => Exit == 0; }

    public static string? Find(string name)
    {
        if (Path.IsPathRooted(name)) return File.Exists(name) ? name : null;
        var exts = OperatingSystem.IsWindows() ? new[] { ".exe", ".cmd", ".bat", "" } : new[] { "" };
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            foreach (var e in exts) { var p = Path.Combine(dir, name + e); if (File.Exists(p)) return p; }
        return null;
    }

    /// <param name="encoding">Kodierung der Ausgabe; ohne Angabe die der Konsole. Python mit PYTHONIOENCODING=utf-8 braucht UTF-8, sonst werden Umlaute verstümmelt.</param>
    public static Result Run(string exe, IEnumerable<string> args, string? cwd = null, int timeoutMs = 60000, IDictionary<string, string>? env = null, System.Text.Encoding? encoding = null)
    {
        var psi = new ProcessStartInfo(Find(exe) ?? exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = cwd ?? Environment.CurrentDirectory };
        if (encoding is not null) { psi.StandardOutputEncoding = encoding; psi.StandardErrorEncoding = encoding; }
        foreach (var a in args) psi.ArgumentList.Add(a);
        if (env is not null) foreach (var kv in env) psi.Environment[kv.Key] = kv.Value;
        try
        {
            var started = Stopwatch.StartNew();
            using var p = Process.Start(psi)!;
            var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(timeoutMs)) { try { p.Kill(true); } catch { } return new(-2, o.IsCompleted ? o.Result : "", "Zeitüberschreitung"); }
            // Ein Kindprozess kann die geerbte Ausgabe offen halten: nicht unbegrenzt auf das Ende der Ausgabe warten
            // T-0620: auf langsamen oder ausgelasteten Rechnern braucht das Auslesen länger – der Rest des Zeitlimits, mindestens 10 s
            if (!Task.WaitAll([o, e], (int)Math.Max(10000, timeoutMs - started.ElapsedMilliseconds))) return new(p.ExitCode, o.IsCompleted ? o.Result : "", (e.IsCompleted ? e.Result + "\n" : "") + "Ausgabe unvollständig: ein gestartetes Unterprogramm läuft noch.");
            return new(p.ExitCode, o.Result, e.Result);
        }
        catch (Exception ex) { return new(-1, "", ex.Message); }
    }

    /// <summary>Exitcode von <see cref="RunStreamAsync"/> nach „Abbrechen“.</summary>
    public const int Cancelled = -3;

    /// <summary>T-0681: wie <see cref="Run"/>, aber ohne den Aufrufer zu blockieren: Die Ausgabe kommt Zeile für Zeile über <paramref name="onLine"/>
    /// (aus einem Hintergrund-Thread), und über <paramref name="ct"/> lässt sich der Lauf abbrechen – dann wird der gestartete Prozess samt seinen
    /// Unterprozessen beendet (Exit <see cref="Cancelled"/>). Zeitlimit wie bisher (Exit -2).</summary>
    /// <param name="onStart">Meldet die Prozessnummer des gestarteten Programms.</param>
    public static async Task<Result> RunStreamAsync(string exe, IEnumerable<string> args, string? cwd, Action<string>? onLine, CancellationToken ct, int timeoutMs = 120000, Action<int>? onStart = null, IDictionary<string, string>? env = null, System.Text.Encoding? encoding = null)
    {
        var psi = new ProcessStartInfo(Find(exe) ?? exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = cwd ?? Environment.CurrentDirectory };
        if (encoding is not null) { psi.StandardOutputEncoding = encoding; psi.StandardErrorEncoding = encoding; }
        foreach (var a in args) psi.ArgumentList.Add(a);
        if (env is not null) foreach (var kv in env) psi.Environment[kv.Key] = kv.Value;
        Process p;
        try { p = Process.Start(psi) ?? throw new InvalidOperationException("Das Programm ließ sich nicht starten."); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException) { return new(-1, "", ex.Message); }
        using (p)
        {
            onStart?.Invoke(p.Id);
            var o = new System.Text.StringBuilder(); var e = new System.Text.StringBuilder();
            async Task Pump(StreamReader r, System.Text.StringBuilder sb)
            {
                try { while (await r.ReadLineAsync().ConfigureAwait(false) is { } l) { lock (sb) sb.AppendLine(l); onLine?.Invoke(l); } }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
            }
            string Text(System.Text.StringBuilder sb) { lock (sb) return sb.ToString(); }
            var pumps = Task.WhenAll(Task.Run(() => Pump(p.StandardOutput, o)), Task.Run(() => Pump(p.StandardError, e)));
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limit.CancelAfter(timeoutMs);
            try { await p.WaitForExitAsync(limit.Token).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                try { p.Kill(entireProcessTree: true); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
                try { await p.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false); } catch (TimeoutException) { }
                await Task.WhenAny(pumps, Task.Delay(2000, CancellationToken.None)).ConfigureAwait(false);
                return ct.IsCancellationRequested ? new(Cancelled, Text(o), (Text(e) + "\nAbgebrochen").Trim()) : new(-2, Text(o), (Text(e) + "\nZeitüberschreitung").Trim());
            }
            // Ein Kindprozess kann die geerbte Ausgabe offen halten: nicht unbegrenzt auf das Ende der Ausgabe warten
            if (await Task.WhenAny(pumps, Task.Delay(10000, CancellationToken.None)).ConfigureAwait(false) != pumps) return new(p.ExitCode, Text(o), (Text(e) + "\nAusgabe unvollständig: ein gestartetes Unterprogramm läuft noch.").Trim());
            return new(p.ExitCode, Text(o), Text(e));
        }
    }
}
