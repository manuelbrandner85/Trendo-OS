using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 4, S4-06 (07.11.2026): abgeschottete Laufumgebung – echte Windows-Prozesse.</summary>
public class SandboxTests
{
    static string Sys(string exe) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), exe);
    static string Ps => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
    static void Log(params string[] lines) { if (Environment.GetEnvironmentVariable("TRENDO_SB_BELEG") is { Length: > 0 } f) File.AppendAllLines(f, lines); }
    /// <summary>Aufräumen mit kurzer Wiederholung (Virenscanner/Indexdienst halten Dateien manchmal kurz offen).</summary>
    static void Clean(string d) { for (var i = 0; i < 10 && Directory.Exists(d); i++) { try { Directory.Delete(d, true); } catch (IOException) { Thread.Sleep(200); } catch (UnauthorizedAccessException) { Thread.Sleep(200); } } }
    static string NewDir(string n) { var d = Path.Combine(Path.GetTempPath(), $"trendo-sb-{n}-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "S4-06 Abschottung: Schreiben nur im eigenen Arbeitsordner – Datei außerhalb (normaler Ordner) wird verweigert; Ausgabe kommt an")]
    public void Write_only_inside()
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(Ps)) return;
        var box = NewDir("box"); var outside = NewDir("draussen");
        try
        {
            var target = Path.Combine(outside, "eingriff.txt");
            // PowerShell statt cmd (cmd versteht keine \"-Maskierung): Schreiben innen, Integritätsstufe ausgeben, dann außerhalb schreiben versuchen
            var script = "Set-Content -Path innen.txt -Value innen; Write-Output hallo; " +
                $"try {{ Set-Content -Path '{target}' -Value boese -ErrorAction Stop; Write-Output 'AUSSEN-GESCHRIEBEN' }} catch {{ Write-Output ('verweigert: ' + $_.Exception.GetType().Name) }}";
            var r = Sandbox.Run(Ps, ["-NoProfile", "-NonInteractive", "-Command", script], box, new Sandbox.Options(TimeoutMs: 60000));
            // Der Ordner „draussen“ ist für den Nutzer normal beschreibbar – die Sperre kommt allein von der niedrigen Integritätsstufe
            Assert.Contains("verweigert: UnauthorizedAccessException", r.Out);
            Assert.False(r.TimedOut, r.Text);
            Assert.True(File.Exists(Path.Combine(box, "innen.txt")), r.Text + r.Err);
            Assert.Contains("hallo", r.Out);
            Assert.False(File.Exists(target), "Die Abschottung hat das Schreiben außerhalb zugelassen.");
            Log("Schreiben: " + r.Text + " | Ausgabe: " + r.Out.Trim() + " | Fehler: " + r.Err.Trim() + $" | außerhalb vorhanden: {File.Exists(target)}");
        }
        finally { Clean(box); Clean(outside); }
    }

    [Fact(DisplayName = "S4-06-N Abschottung: Absturz (Zugriffsverletzung) wird erkannt und erklärt; Zeitgrenze beendet hängende Prozesse; weitere Programme starten ist gesperrt")]
    public void Crash_timeout_children()
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(Ps)) return;
        var box = NewDir("crash");
        try
        {
            var c = Sandbox.Run(Ps, ["-NoProfile", "-NonInteractive", "-Command", "[Runtime.InteropServices.Marshal]::ReadInt32([IntPtr]::Zero)"], box, new Sandbox.Options(TimeoutMs: 60000));
            Assert.False(c.Ok); Assert.False(c.TimedOut); Assert.True(c.Crashed, c.Text); Assert.Contains("abgestürzt", c.Text);
            var t = Sandbox.Run(Ps, ["-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 60"], box, new Sandbox.Options(TimeoutMs: 3000));
            Assert.True(t.TimedOut, t.Text); Assert.Contains("Zeitgrenze", t.Text); Assert.InRange(t.Seconds, 2.5, 20);
            var k = Sandbox.Run(Sys("cmd.exe"), ["/c", "cmd /c echo kind-laeuft"], box, new Sandbox.Options(TimeoutMs: 20000));
            Assert.DoesNotContain("kind-laeuft", k.Out);
            Log("Absturz: " + c.Text + $" (Code {c.Exit})", "Zeitgrenze: " + t.Text + $" nach {t.Seconds:0.0} s", "Kindprozess: " + k.Text + " | Ausgabe: " + k.Out.Trim() + " | Fehler: " + k.Err.Trim());
        }
        finally { Clean(box); }
    }

    [Fact(DisplayName = "S4-06: Absturzcodes werden benannt; Argumente werden korrekt gequotet")]
    public void Names_and_quotes()
    {
        Assert.Equal("Zugriffsverletzung", Sandbox.CrashName(unchecked((int)0xC0000005)));
        Assert.Null(Sandbox.CrashName(1)); Assert.Null(Sandbox.CrashName(0));
        Assert.Equal("einfach", Sandbox.Quote("einfach"));
        Assert.Equal("\"mit leer\"", Sandbox.Quote("mit leer"));
        Assert.Equal("\"C:\\Pfad mit\\\\\"", Sandbox.Quote("C:\\Pfad mit\\"));
        Assert.Equal("\"a\\\"b\"", Sandbox.Quote("a\"b"));
    }
}
