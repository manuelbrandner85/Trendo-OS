using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Vorschlag 11: Testspieler-Bot – Bericht aus den Messzeilen des Unreal-Startlaufs.</summary>
public class GamePlaytestTests
{
    // Zeilen aus einem echten Lauf mit UE 5.8 (07.11.2026, reports/paket01/unreal-wegfindung-e2e.md)
    const string Good = """
        [2026.11.07-18.20.01:100][  2]LogTemp: Display: TRENDO-FIGUR MetaHuman=ja (BP_Waldlaeuferin_C) Mensch=ja
        [2026.11.07-18.20.09:100][  2]LogTrendo: Display: TRENDO-WEG Wegnetz=ja Pfade=33 davon mit Umweg=4
        [2026.11.07-18.20.09:200][  2]LogTrendo: Display: TRENDO-WEG Hindernis boulder_01_2k: gerade versperrt, Pfad 23.8 m statt 23.2 m mit 5 Punkten, führt drumherum
        [2026.11.07-18.20.09:300][  2]LogTrendo: Display: TRENDO-BODEN Gegner geht=ja Abstand Füße–Boden=2.1 cm
        [2026.11.07-18.20.09:400][  2]LogTrendo: Display: TRENDO-GEGNER alarmiert=ja Abstand vorher=1100 nachher=221 Leben 3→3 verfolgt
        [2026.11.07-18.20.09:500][  2]LogTrendo: Display: TRENDO-FLACKERN Gegner in der Ferne bis 52 m: 630 Bilder, ohne Posen-Update 19 (3%), Sprünge über 12 cm 0 (dazu 3 beim Erscheinen), größter Sprung je Bild 23.4 cm (ruhig)
        [2026.11.07-18.20.12:000][  2]LogTrendo: Display: TRENDO-KAMPF Gegner besiegt=ja Ragdoll=ja Becken über Boden=20 cm (liegt) Blut=9 abgetrennt=1 Gewalt=brutal
        [2026.11.07-18.20.14:000][  2]LogTrendo: Display: TRENDO-BLUT Zeitraffer +5 min: 9 Flecken gealtert
        [2026.11.07-18.20.20:000][  2]LogTrendo: Display: TRENDO-WAGEN gesteuert=ja Klasse=TrendoCar Tempo=26 km/h Strecke=42.6 m Raddrehung=303° (Reifen drehen sich) Gang=2 Neigung max=13°
        [2026.11.07-18.20.22:000][  2]LogTrendo: Display: TRENDO-OK Spiel läuft, Figur steht
        """;

    [Fact(DisplayName = "Testspieler-Bot: echter Lauf → alle Prüfpunkte ✓ mit Messwerten")]
    public void Good_run()
    {
        var f = GamePlaytest.Read(Good);
        Assert.Equal(["Spielfigur", "Boden", "Gegner-KI", "Wegfindung", "Darstellung", "Kampf", "Blut", "Fahrzeug", "Start"], f.Select(x => x.Area));
        Assert.All(f, x => Assert.True(x.Ok, x.Area + ": " + x.Text));
        Assert.Contains("2,1 cm", f.Single(x => x.Area == "Boden").Text);
        Assert.Contains("23.8 m statt 23.2 m", f.Single(x => x.Area == "Wegfindung").Text);
        Assert.Contains("43 m", f.Single(x => x.Area == "Fahrzeug").Text);
        Assert.Contains("Glied abgetrennt", f.Single(x => x.Area == "Kampf").Text);
    }

    [Fact(DisplayName = "Testspieler-Bot: Probleme werden ⚠ mit Grund – festgefahrener Wagen, Flackern, Sturz durch den Boden, untätiger Gegner")]
    public void Problems()
    {
        var f = GamePlaytest.Read("""
            LogTrendo: Display: TRENDO-GEGNER alarmiert=nein Abstand vorher=1100 nachher=1100 Leben 3→3 reagiert nicht
            LogTrendo: Display: TRENDO-FLACKERN Gegner in der Ferne bis 55 m: 1081 Bilder, ohne Posen-Update 27 (2%), Sprünge über 12 cm 11, größter Sprung je Bild 60.9 cm (flackert)
            LogTrendo: Display: TRENDO-KAMPF Gegner besiegt=ja Ragdoll=ja Becken über Boden=-80 cm (unter dem Boden) Blut=3 Gewalt=normal
            LogTrendo: Display: TRENDO-WAGEN gesteuert=ja Klasse=TrendoCar Tempo=1 km/h Strecke=2.6 m Raddrehung=336° (Reifen drehen sich) Gang=1 Neigung max=3°
            LogTrendo: Display: TRENDO-FEHLER Figur fehlt
            """);
        Assert.All(f, x => Assert.False(x.Ok));
        Assert.Contains("steckt fest", f.Single(x => x.Area == "Fahrzeug").Text); Assert.Contains("2,6 m", f.Single(x => x.Area == "Fahrzeug").Text);
        Assert.Contains("durch den Boden", f.Single(x => x.Area == "Kampf").Text);
        Assert.Contains("11 m", f.Single(x => x.Area == "Gegner-KI").Text);
        Assert.Contains("flackern", f.Single(x => x.Area == "Darstellung").Text);
        Assert.Equal("Figur fehlt", f.Single(x => x.Area == "Start").Text);
        Assert.Empty(GamePlaytest.Read("")); // ohne Messzeilen kein erfundener Bericht
    }

    [Fact(DisplayName = "Testspieler-Bot: Bilder aus dem Lauf in Spielreihenfolge")]
    public void Shots_in_order()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-playtest-" + Guid.NewGuid().ToString("N"));
        try
        {
            var s = Directory.CreateDirectory(Path.Combine(dir, "Saved", "Screenshots")).FullName;
            foreach (var n in new[] { "trendo-wagen.png", "trendo-kampf.png", "trendo-start.png", "fremd.png" }) File.WriteAllBytes(Path.Combine(s, n), [1]);
            Assert.Equal(["Start", "Kampf", "Wagen"], GamePlaytest.Shots(dir).Select(x => x.Label));
            Assert.Empty(GamePlaytest.Shots(""));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
