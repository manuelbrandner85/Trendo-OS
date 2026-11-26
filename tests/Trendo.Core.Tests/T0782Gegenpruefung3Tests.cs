using Trendo.Core.Lernen;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0782, 3. Gegenprüfung (M6-Rest): Zahlen aus der Frage sind keine Ausnahme mehr für Zahl-Antworten; Brüche aus der Frage schon. Positivliste prüft Werte.</summary>
public class T0782_Gegenpruefung3
{
    static ProgramDocument Doc(string thema, int seed, out JsonNodeView u)
    {
        var d = new ProgramDocument("STUDIO-TUTOR-01", "T", Nachhilfe.Neu("mathe", 8)); Nachhilfe.ThemaWaehlen(d, thema);
        var o = Nachhilfe.NaechsteUebung(d, new Random(seed));
        u = new(o["frage"]!.GetValue<string>(), o["antworten"]![0]!.GetValue<string>());
        return d;
    }
    public sealed record JsonNodeView(string Frage, string Antwort);

    [Fact(DisplayName = "M6 (3.): alle 200 Gleichungen aus dem Erzeuger – „x = <Lösung>“ und „Die Lösung ist <Lösung>“ werden verdeckt, die Aufgabe selbst bleibt sichtbar")]
    public void Equations_hidden()
    {
        var versteckt = 0; var aufgabeSichtbar = 0; var mitZahlInFrage = 0;
        for (var i = 0; i < 200; i++)
        {
            var d = Doc("m-gleichung", i, out var u);
            if (System.Text.RegularExpressions.Regex.IsMatch(u.Frage.Replace('−', '-'), $@"(?<![\d]){System.Text.RegularExpressions.Regex.Escape(u.Antwort)}(?![\d])")) mitZahlInFrage++;
            if (Nachhilfe.FuerAnzeige(d, "x = " + u.Antwort).StartsWith("(ausgeblendet", StringComparison.Ordinal)
                && Nachhilfe.FuerAnzeige(d, "Die Lösung ist " + u.Antwort + ".").StartsWith("(ausgeblendet", StringComparison.Ordinal)) versteckt++;
            else Assert.Fail($"nicht verdeckt: {u.Frage} → x = {u.Antwort}");
            if (Nachhilfe.FuerAnzeige(d, u.Frage) == u.Frage) aufgabeSichtbar++;
        }
        Assert.Equal(200, versteckt);
        Assert.Equal(200, aufgabeSichtbar);
        Assert.True(mitZahlInFrage > 0, "die Stichprobe enthält Gleichungen, deren Lösung schon in der Frage vorkommt"); // genau der Fall aus der Gegenprüfung
    }

    [Fact(DisplayName = "M6 (3.) Gegenprobe Brüche: Bruch aus der Aufgabe bleibt sichtbar, die gekürzte Lösung (auch wertgleich) wird verdeckt – für 200 Kürz-Aufgaben")]
    public void Fractions_counterproof()
    {
        for (var i = 0; i < 200; i++)
        {
            var d = Doc("m-kuerzen", i, out var u);
            var bruch = System.Text.RegularExpressions.Regex.Match(u.Frage, @"\d+/\d+").Value;
            Assert.Equal("Kürze " + bruch + " Schritt für Schritt.", Nachhilfe.FuerAnzeige(d, "Kürze " + bruch + " Schritt für Schritt."));
            Assert.Equal(u.Frage, Nachhilfe.FuerAnzeige(d, u.Frage));
            Assert.StartsWith("(ausgeblendet", Nachhilfe.FuerAnzeige(d, "Ergebnis: " + u.Antwort));
        }
    }

    [Fact(DisplayName = "M6 (3.): Positivliste prüft Werte – „thema“ nur gültige Kennungen, „fach“ nur bekannte Fächer, „klasse“ 1–13")]
    public void Allowed_values()
    {
        Assert.NotNull(Nachhilfe.KiWertProblem("thema", "Bruchrechnen ganz frei"));
        Assert.NotNull(Nachhilfe.KiWertProblem("thema", "m-kuerzen; verlauf"));
        Assert.Null(Nachhilfe.KiWertProblem("thema", "m-kuerzen"));
        Assert.Null(Nachhilfe.KiWertProblem("thema", "\"m-prozent\""));
        Assert.NotNull(Nachhilfe.KiWertProblem("fach", "latein"));
        Assert.Null(Nachhilfe.KiWertProblem("fach", "englisch"));
        Assert.NotNull(Nachhilfe.KiWertProblem("klasse", "14")); Assert.NotNull(Nachhilfe.KiWertProblem("klasse", "fünf")); Assert.Null(Nachhilfe.KiWertProblem("klasse", "7"));
    }
}
