using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;
namespace Trendo.Core.Tests;

public class Sys12_Barrierefreiheit
{
    [Fact(DisplayName = "SYS-12-P07: WCAG-Kontrast (Schwarz/Weiß 21:1, AA-Grenzen)")]
    public void Contrast()
    {
        Assert.Equal(21, A11y.Contrast("#000000", "#FFFFFF"), 1);
        Assert.True(A11y.MeetsAA("#767676", "#FFFFFF")); Assert.False(A11y.MeetsAA("#777777", "#FFFFFF")); Assert.True(A11y.MeetsAA("#777777", "#FFFFFF", large: true));
        foreach (var (fg, bg) in new[] { ("#EEF3FB", "#0B1730"), ("#9DB0CF", "#0B1730"), ("#0F1B2E", "#F4F7FC"), ("#41526E", "#F4F7FC"), ("#FFFFFF", "#000000") }) Assert.True(A11y.MeetsAA(fg, bg), fg + " auf " + bg);
    }

    [Fact(DisplayName = "SYS-12-P14: Jede Akzentfarbe wird für Text auf jedem Design AA-tauglich")]
    public void Accent()
    {
        foreach (var a in new[] { "#2F86F6", "#22C3A6", "#8B5CF6", "#F472B6", "#F59E0B", "#22C55E", "#FFD400" })
            foreach (var bg in new[] { "#0B1730", "#F4F7FC", "#000000" }) Assert.True(A11y.MeetsAA(A11y.AccentForText(a, bg), bg), a + " auf " + bg);
        Assert.Equal("#FFD400", A11y.AccentForText("#FFD400", "#000000"));
    }

    [Fact(DisplayName = "SYS-12-P03/P09/P18: Mindestschrift, Status mit Zeichen, leise Meldungen")]
    public void Small()
    {
        Assert.Equal(12, A11y.EffectiveFont(10.5, 12)); Assert.Equal(20, A11y.EffectiveFont(20, 12)); Assert.Equal(10.5, A11y.EffectiveFont(10.5, 0));
        Assert.Equal(3, new[] { A11y.Level.Gut, A11y.Level.Hinweis, A11y.Level.Problem }.Select(A11y.Symbol).Distinct().Count());
        Assert.True(A11y.MayInterrupt("normal", false)); Assert.False(A11y.MayInterrupt("leise", false)); Assert.False(A11y.MayInterrupt("normal", true));
    }

    [Fact(DisplayName = "SYS-12-P10: Leichte Sprache – höchstens 2 kurze Sätze, einfache Wörter")]
    public void Easy()
    {
        var e = A11y.Easy("Zusatzprogramme, die Trendo für einige Studios braucht. Trendo kann sie nach deiner Zustimmung installieren – ohne Admin, wo das geht. Dritter Satz.");
        var s = e.Split('\n'); Assert.Equal(2, s.Length); Assert.All(s, x => Assert.True(x.Split(' ').Length <= 12));
        Assert.Contains("einrichten", e); Assert.DoesNotContain("Admin", e);
    }

    [Fact(DisplayName = "SYS-12-P15/P16: Navigation >2 Ebenen und englische Beschriftungen werden gefunden")]
    public void Rules()
    {
        Assert.Equal(["tief"], A11y.NavTooDeep([("a", 1), ("b", 2), ("tief", 3)]));
        Assert.Equal(["Save", "Datei öffnen (Cancel)"], A11y.EnglishLabels(["Save", "Speichern", "PDF teilen", "Datei öffnen (Cancel)", "KI Studio"]));
    }

    [Fact(DisplayName = "SYS-12-P20: Vorlesen – Windows-SAPI-Befehl, Linux espeak oder nichts")]
    public void Speak()
    {
        var w = A11y.SpeakCommand("Hallo 'Welt'", true, _ => null)!.Value; Assert.Equal("powershell", w.Exe); Assert.Contains("System.Speech", w.Args[^1]); Assert.Contains("''Welt''", w.Args[^1]);
        Assert.Null(A11y.SpeakCommand("Hallo", false, _ => null)); Assert.Null(A11y.SpeakCommand("  ", true, _ => "x"));
        Assert.Equal(["-v", "de", "Hallo"], A11y.SpeakCommand("Hallo", false, n => n == "espeak-ng" ? "/usr/bin/espeak-ng" : null)!.Value.Args);
    }

    [Fact(DisplayName = "SYS-12-P23: Fensterposition nur auf vorhandenem Bildschirm wiederherstellen")]
    public void Place()
    {
        A11y.Rect[] two = [new(0, 0, 1920, 1080), new(1920, 0, 2560, 1440)];
        Assert.Equal(new A11y.Rect(2000, 100, 1400, 900), A11y.RestorePosition(new(2000, 100, 1400, 900), two));
        Assert.Null(A11y.RestorePosition(new(5000, 100, 1400, 900), two)); Assert.Null(A11y.RestorePosition(null, two));
        Assert.Equal(1920, A11y.RestorePosition(new(0, 0, 4000, 900), [two[0]])!.W);
    }

    [Fact(DisplayName = "SYS-12-P22: Einstellungen exportieren/importieren verlustfrei, Unsinn abgelehnt")]
    public void Transfer()
    {
        var d = Path.Combine(Path.GetTempPath(), "trendo-s12-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d);
        var s = new UserSettings { Theme = "hell", FontMin = 14, EasyLanguage = true, NotifyStyle = "leise", TileOrder = ["SYS-10"], Accent = "#22C3A6", WindowRect = [1, 2, 800, 600] };
        var f = Path.Combine(d, "s.json"); A11y.ExportSettings(s, f); var r = A11y.ImportSettings(f);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(s), System.Text.Json.JsonSerializer.Serialize(r));
        File.WriteAllText(f, "kaputt"); Assert.Throws<InvalidDataException>(() => A11y.ImportSettings(f));
        File.WriteAllText(f, "{\"Theme\":\"lila\"}"); Assert.Throws<InvalidDataException>(() => A11y.ImportSettings(f));
    }

    [Fact(DisplayName = "SYS-12-P25: Bericht listet Befunde je Seite")]
    public void Report()
    {
        var r = A11y.Report([new("start", 10, [], [], [], 1), new("x", 3, ["b1"], ["Save"], [], 3)], "dunkel");
        Assert.Contains("⚠ 1 Seiten mit Befunden", r); Assert.Contains("| x | 3 | ⚠ b1 | ⚠ Save | ✓ | 3 |", r);
    }
}
