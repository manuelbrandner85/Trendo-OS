using Trendo.Core.Wissen;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0787 vierte Prüfrunde. Nutzt nur Schnittstellen von 4b5760a98 – übersetzt dort und ist dort rot (reports/T-0787/runde4/rot-runde4.txt).
/// Ziel: die Sperre hat praktisch keine Fehlalarme; Lücken werden gesperrt oder als Hinweis erkannt. Alle Satzlisten der Runden 1–3 laufen in
/// <see cref="T0787_Runde3"/> und <see cref="T0787_Runde2"/> unverändert weiter.</summary>
public class T0787_Runde4
{
    /// <summary>Fehlalarme dieser Runde und zehn eigene – weder Sperre noch Hinweis.</summary>
    public static readonly string[] Frei =
    [
        "Herzhafte Linsensuppe mit Speck.", "Hühnerherzen kurz scharf anbraten.", "Den Deckel zum Abnehmen anheben.", "Topflappen zum Abnehmen bereitlegen.", "Ein Tuch hilft beim Abnehmen.",
        // eigene
        "Die Hitze senken und weiterköcheln.", "Ein herzhafter Eintopf für kalte Tage.", "Stärke unterrühren, damit die Soße bindet.", "Knochen und Gemüse für die Brühe rösten.",
        "Gegen Ende der Garzeit salzen.", "Bei schwacher Hitze ziehen lassen.", "Schutz vor Spritzern: Deckel auflegen.", "Mit einer Prise Salz die Süße unterstützen.",
        "Kalbsleber in Butter braten.", "Die Zange hilft beim Abnehmen der heißen Gläser.",
    ];
    /// <summary>Eindeutige Versprechen dieser Runde und eigene – gesperrt.</summary>
    public static readonly string[] Gesperrt =
    [
        "Hilft bei Halsschmerzen.", "Gut gegen Kopfweh.", "Lindert Magenbeschwerden.", "Ein natürliches Antibiotikum.", "Hält jung und fit.", "Verlängert das Leben.",
        "Zum Abnehmen ideal.", "Ein echtes Abnehmrezept.", "Die perfekte Diät zum Abnehmen.", "Perfekt zum Abnehmen.",
        // eigene
        "Hilft gegen Sodbrennen.", "Stärkt das Immunsystem.", "Senkt nachweislich den Cholesterinspiegel.", "Gegen Bluthochdruck.", "Wirkt bei Verstopfung.", "Schützt vor Herzinfarkt.",
        "Bekämpft Entzündungen.", "Herzstärkend und wärmend.", "Gut für das Herz.", "Hilft gegen Rückenschmerzen.",
    ];
    /// <summary>Weiche Lücken dieser Runde und eigene – nur „Bitte prüfen“, keine Sperre.</summary>
    public static readonly string[] Hinweis =
    [
        "Ideal bei Erkältung.", "Das beste Mittel gegen Kater.", "Schlank durch den Sommer.", "Gut für die Figur.",
        // eigene
        "Unterstützt den Stoffwechsel.", "Gut für die Leber.", "Hilft bei Stress.", "Stärkt die Nerven.", "Gut für Haut und Haare.", "Fördert die Konzentration.",
    ];

    [Fact(DisplayName = "T-0787 R4-1: harmlose Sätze (herzhaft, Hühnerherzen, Deckel/Topflappen/Tuch beim Abnehmen …) – weder Sperre noch Hinweis")]
    public void Frei_bleibt_frei()
    {
        var falsch = Frei.Where(t => RezeptKit.Gesundheitsversprechen(t).Count > 0).Select(t => t + " → " + string.Join(", ", RezeptKit.Gesundheitsversprechen(t))).ToList();
        Assert.True(falsch.Count == 0, "gesperrt: " + string.Join(" | ", falsch));
    }

    [Fact(DisplayName = "T-0787 R4-1: eindeutige Versprechen dieser Runde gesperrt; weichere Körperbegriffe (Stoffwechsel, Stress, Leber, Haut …) nicht gesperrt")]
    public void Gesperrt_und_weich()
    {
        var fehlt = Gesperrt.Where(t => RezeptKit.Gesundheitsversprechen(t).Count == 0).ToList();
        Assert.True(fehlt.Count == 0, "nicht gesperrt: " + string.Join(" | ", fehlt));
        var zuStreng = Hinweis.Where(t => RezeptKit.Gesundheitsversprechen(t).Count > 0).Select(t => t + " → " + string.Join(", ", RezeptKit.Gesundheitsversprechen(t))).ToList();
        Assert.True(zuStreng.Count == 0, "weich, aber gesperrt: " + string.Join(" | ", zuStreng));
    }

    [Fact(DisplayName = "T-0787 R4-2: englisch – „paprika“ mit Löffel ist das Gewürz; can, stick (Butter 113 g), pinch; „8 oz milk“ mit Hinweis oz/fl oz")]
    public void Englisch()
    {
        Assert.Equal("paprikapulver", RezeptKit.LiesZutat("1 tsp paprika").Lm);
        Assert.Equal("paprikapulver", RezeptKit.LiesZutat("1 TL Paprika").Lm);
        Assert.Equal("paprika-rot", RezeptKit.LiesZutat("2 Paprika").Lm); // ohne Löffel bleibt es die Schote
        var z = RezeptKit.LiesZutat("1 can tomatoes"); Assert.Equal("dose", z.Einheit); Assert.Equal("tomaten-dose", z.Lm); Assert.Equal(400, RezeptKit.Gramm(z));
        z = RezeptKit.LiesZutat("1 stick butter"); Assert.Equal("stick", z.Einheit); Assert.Equal(113.4, RezeptKit.Gramm(z)!.Value, 3);
        z = RezeptKit.LiesZutat("1 pinch salt"); Assert.Equal("prise", z.Einheit); Assert.Equal("salz", z.Lm);
        var (milk, h) = RezeptKit.Lies("8 oz milk"); Assert.Equal("oz", milk.Einheit); Assert.Contains("fl oz", h);
        Assert.Null(RezeptKit.Lies("8 oz butter").Hinweis);
    }
}

/// <summary>T-0787 vierte Prüfrunde: Hinweise (neue Stufe aus Runde 3).</summary>
public class T0787_Runde4Neu
{
    [Fact(DisplayName = "T-0787 R4-1: weiche Lücken ergeben „Bitte prüfen“; die harmlosen Sätze ergeben auch keinen Hinweis")]
    public void Hinweise()
    {
        var fehlt = T0787_Runde4.Hinweis.Where(t => RezeptKit.Gesundheitshinweise(t).Count == 0).ToList();
        Assert.True(fehlt.Count == 0, "kein Hinweis: " + string.Join(" | ", fehlt));
        var falsch = T0787_Runde4.Frei.Where(t => RezeptKit.Gesundheitshinweise(t).Count > 0).Select(t => t + " → " + string.Join(", ", RezeptKit.Gesundheitshinweise(t))).ToList();
        Assert.True(falsch.Count == 0, "Hinweis bei Kochsatz: " + string.Join(" | ", falsch));
    }
}
