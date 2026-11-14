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
            using var p = Process.Start(psi)!;
            var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(timeoutMs)) { try { p.Kill(true); } catch { } return new(-2, o.IsCompleted ? o.Result : "", "Zeitüberschreitung"); }
            // Ein Kindprozess kann die geerbte Ausgabe offen halten: nicht unbegrenzt auf das Ende der Ausgabe warten
            if (!Task.WaitAll([o, e], 10000)) return new(p.ExitCode, o.IsCompleted ? o.Result : "", (e.IsCompleted ? e.Result + "\n" : "") + "Ausgabe unvollständig: ein gestartetes Unterprogramm läuft noch.");
            return new(p.ExitCode, o.Result, e.Result);
        }
        catch (Exception ex) { return new(-1, "", ex.Message); }
    }
}
