using Trendo.Core.ConsolePort;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Studio „Konsolenspiele portieren“.</summary>
public class ConsolePortTests
{
    static string Iso(string dir, string name = "spiel.iso", bool valid = true)
    {
        var b = new byte[0x8010];
        if (valid) "CD001"u8.CopyTo(b.AsSpan(0x8001));
        var f = Path.Combine(dir, name); File.WriteAllBytes(f, b); return f;
    }
    static void In(Action<string> a) { var d = Directory.CreateTempSubdirectory("trendo-konsole").FullName; try { a(d); } finally { Directory.Delete(d, true); } }

    [Fact(DisplayName = "Konsole: Nur ein gültiges lokales ISO-Abbild wird angenommen; Adressen, andere Formate und kaputte Dateien nicht")]
    public void Image_check() => In(d =>
    {
        Assert.Null(ConsolePortKit.CheckImage(Iso(d)));
        Assert.Contains("Internet", ConsolePortKit.CheckImage("https://example.org/spiel.iso"));
        Assert.Contains(".iso", ConsolePortKit.CheckImage(Path.Combine(d, "spiel.zip")));
        Assert.Contains("nicht gefunden", ConsolePortKit.CheckImage(Path.Combine(d, "weg.iso")));
        Assert.Contains("kein gültiges", ConsolePortKit.CheckImage(Iso(d, "x.iso", valid: false)));
        var kurz = Path.Combine(d, "kurz.iso"); File.WriteAllBytes(kurz, [1]);
        Assert.Contains("vollständiges", ConsolePortKit.CheckImage(kurz));
        Assert.NotNull(ConsolePortKit.CheckImage(""));
    });

    [Fact(DisplayName = "Konsole: Ohne Name, Besitz-Bestätigung und gültiges Abbild startet nichts")]
    public void Ready() => In(d =>
    {
        var iso = Iso(d);
        Assert.Contains("Namen", ConsolePortKit.ReadyToStart("", iso, true));
        Assert.Contains("besitzt", ConsolePortKit.ReadyToStart("Spiel", iso, false));
        Assert.NotNull(ConsolePortKit.ReadyToStart("Spiel", "x.iso", true));
        Assert.Null(ConsolePortKit.ReadyToStart("Spiel", iso, true));
    });

    [Fact(DisplayName = "Konsole: Skills kommen in fester Fassung aus dem genannten Repository")]
    public void Pinned()
    {
        var steps = ConsolePortKit.InstallSteps();
        Assert.Contains(steps, s => s.Contains(ConsolePortKit.SkillCommit) && s.Contains(ConsolePortKit.SkillRepo));
        Assert.Equal(40, ConsolePortKit.SkillCommit.Length);
    }

    [Fact(DisplayName = "Konsole: Aufträge nennen den Schritt und verbieten das Beschaffen von Spielen und das Hochladen; unbekannte Schritte werden abgelehnt")]
    public void Prompts()
    {
        var p = ConsolePortKit.Prompt("console-reverse", "Spiel", "C:/a.iso", "C:/p");
        Assert.StartsWith("/console-reverse", p); Assert.Contains("lade oder empfiehl keine Spiele", p); Assert.Contains("nirgends hochgeladen", p);
        Assert.Contains("--target steamdeck", ConsolePortKit.PackagePrompt("steamdeck", "Spiel", "C:/p"));
        Assert.Contains("guard", ConsolePortKit.PackagePrompt("android", "Spiel", "C:/p"));
        Assert.Throws<ArgumentException>(() => ConsolePortKit.Prompt("rm", "a", "b", "c"));
        Assert.Throws<ArgumentException>(() => ConsolePortKit.PackagePrompt("toaster", "a", "b"));
    }

    [Fact(DisplayName = "Konsole: Fortschritt kommt aus STATE.md und wird gekürzt; ohne Datei null")]
    public void State() => In(d =>
    {
        Assert.Null(ConsolePortKit.State(d));
        Directory.CreateDirectory(Path.Combine(d, "console")); File.WriteAllText(Path.Combine(d, "console", "STATE.md"), new string('a', 50));
        Assert.Equal(50, ConsolePortKit.State(d)!.Length);
        Assert.EndsWith("…", ConsolePortKit.State(d, 10));
    });
}
