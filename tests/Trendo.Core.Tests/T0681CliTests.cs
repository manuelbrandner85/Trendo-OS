using System.Diagnostics;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0681 (B1b): Programme im Hintergrund starten – Ausgabe laufend, Abbrechen beendet den eigenen Kindprozess samt Unterprozess.
public class T0681_Cli
{
    /// <summary>Ein Programm, das eine Zeile schreibt, selbst einen Unterprozess startet (dessen Nummer es nennt) und dann lange läuft.</summary>
    public static (string Exe, string[] Args) Langlaeufer() => OperatingSystem.IsWindows()
        ? ("powershell", ["-NoProfile", "-Command", "$k = Start-Process ping -ArgumentList '-n','300','127.0.0.1' -PassThru -WindowStyle Hidden; Write-Output ('kind ' + $k.Id); Write-Output 'Zeile eins'; Start-Sleep -Seconds 300"])
        : ("sh", ["-c", "sleep 300 & echo \"kind $!\"; echo 'Zeile eins'; wait"]);

    public static bool Lebt(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return !p.HasExited; }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    [Fact(DisplayName = "T-0681: RunStreamAsync blockiert nicht, liefert Zeilen laufend, Abbrechen beendet Kindprozess und dessen Unterprozess")]
    public async Task Laufend_und_abbrechbar()
    {
        var (exe, args) = Langlaeufer();
        using var cts = new CancellationTokenSource();
        var zeilen = new List<string>(); var ersteZeile = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var pid = 0;
        var sw = Stopwatch.StartNew();
        var lauf = Cli.RunStreamAsync(exe, args, null, l => { lock (zeilen) zeilen.Add(l); if (l.Contains("Zeile eins")) ersteZeile.TrySetResult(); }, cts.Token, 290_000, p => pid = p);
        await ersteZeile.Task.WaitAsync(TimeSpan.FromSeconds(60));
        Assert.False(lauf.IsCompleted); // die Zeile kam, während das Programm noch läuft
        Assert.True(pid > 0); Assert.True(Lebt(pid));
        int kind; lock (zeilen) kind = int.Parse(zeilen.First(l => l.StartsWith("kind ")).Split(' ')[1]);
        Assert.True(Lebt(kind));
        cts.Cancel();
        var r = await lauf.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(Cli.Cancelled, r.Exit); Assert.False(r.Ok); Assert.Contains("Zeile eins", r.Out); Assert.Contains("Abgebrochen", r.Err);
        for (var i = 0; i < 100 && (Lebt(pid) || Lebt(kind)); i++) await Task.Delay(50);
        Assert.False(Lebt(pid), "Der gestartete Prozess läuft nach dem Abbrechen weiter.");
        Assert.False(Lebt(kind), "Der Unterprozess läuft nach dem Abbrechen weiter.");
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(120));
    }

    [Fact(DisplayName = "T-0681: RunStreamAsync – normales Ende mit Exitcode und Ausgabe; unbekanntes Programm wird als Fehler gemeldet, kein Absturz")]
    public async Task Normales_Ende()
    {
        var (exe, args) = OperatingSystem.IsWindows() ? ("cmd", new[] { "/c", "echo eins& echo zwei 1>&2& exit 3" }) : ("sh", new[] { "-c", "echo eins; echo zwei 1>&2; exit 3" });
        var zeilen = new System.Collections.Concurrent.ConcurrentBag<string>();
        var r = await Cli.RunStreamAsync(exe, args, null, zeilen.Add, CancellationToken.None);
        Assert.Equal(3, r.Exit); Assert.Contains("eins", r.Out); Assert.Contains("zwei", r.Err);
        Assert.Contains(zeilen, z => z.Trim() == "eins"); Assert.Contains(zeilen, z => z.Trim() == "zwei");
        var fehlt = await Cli.RunStreamAsync("trendo-gibt-es-nicht-681", [], null, null, CancellationToken.None);
        Assert.Equal(-1, fehlt.Exit); Assert.NotEqual("", fehlt.Err);
    }
}
