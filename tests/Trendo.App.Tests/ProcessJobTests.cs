using System.Diagnostics;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Website-Vorschau: Chrome-Hilfsprozesse bleiben nicht liegen – das Job-Objekt beendet Prozess und Unterprozesse beim Schließen.
public class ProcessJobTests
{
    [Fact(DisplayName = "Job-Objekt: beim Schließen enden der Prozess und seine Unterprozesse (Chrome-Reste nach der Vorschau)")]
    public void Kills_children()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Skip("Nur Windows."); return; }
        var psi = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[] { "/c", "ping", "-n", "60", "127.0.0.1" }) psi.ArgumentList.Add(a);
        using var job = new ProcessJob();
        using var p = Process.Start(psi)!;
        Assert.True(job.Add(p));
        Process? child = null;
        for (var i = 0; i < 50 && child is null; i++)
        {
            child = Process.GetProcessesByName("PING").FirstOrDefault(x => { try { return x.StartTime >= p.StartTime.AddMilliseconds(-50); } catch { return false; } });
            if (child is null) Thread.Sleep(100);
        }
        Assert.NotNull(child);
        job.Dispose();
        Assert.True(p.WaitForExit(5000)); Assert.True(child!.WaitForExit(5000));
    }
}
