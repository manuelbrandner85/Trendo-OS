using Trendo.Core.Film;
using Xunit;

namespace Trendo.Core.Tests;

public class FountainTests
{
    const string Sample = """
Titel: Der letzte Zug
Autor: Erika Muster

INNEN. BAHNHOF – NACHT

Der Bahnsteig ist leer. Eine Uhr tickt.

ANNA
(leise)
Ist er schon weg?

BEN (V.O.)
Der letzte Zug fährt um Mitternacht.

SCHNITT AUF:

AUSSEN. GLEIS – NACHT

- Totale: Der Zug fährt ein (4 s)
- Nah: Anna hebt den Blick (2,5 s)

ANNA
Dann bleibt mir nur noch eins.
""";

    [Fact(DisplayName = "Film F2: Fountain erkennt Titelseite, deutsche Szenenköpfe, Figuren, Regie, Rede, Übergänge und Einstellungen")]
    public void Parses_elements()
    {
        var s = Fountain.Parse(Sample);
        Assert.Equal("Der letzte Zug", s.Title);
        Assert.Equal("Erika Muster", s.TitlePage["autor"]);
        Assert.Equal(2, s.Lines.Count(l => l.Kind == FountainKind.Heading));
        Assert.Equal(["ANNA", "BEN", "ANNA"], s.Lines.Where(l => l.Kind == FountainKind.Character).Select(l => l.Text));
        Assert.Single(s.Lines, l => l.Kind == FountainKind.Parenthetical && l.Text == "(leise)");
        Assert.Single(s.Lines, l => l.Kind == FountainKind.Transition && l.Text == "SCHNITT AUF:");
        Assert.Equal(2, s.Lines.Count(l => l.Kind == FountainKind.Shot));
    }

    [Fact(DisplayName = "Film F2: Normaler Text wird nicht zum Szenenkopf oder zur Figur; Notizen und Boneyard verschwinden, Zeilennummern bleiben")]
    public void Plain_text_stays_action()
    {
        var s = Fountain.Parse("\nInnen ist es kalt.\n\n/* gestrichen\nZWEITE ZEILE */\n\nSie geht. [[Notiz]]\n\n!LAUT RUFT ER\nweiter\n");
        Assert.DoesNotContain(s.Lines, l => l.Kind is FountainKind.Heading or FountainKind.Character);
        Assert.Equal("Innen ist es kalt.", s.Lines[0].Text);
        Assert.Equal(2, s.Lines[0].LineNo);
        var gone = s.Lines.Single(l => l.Text.StartsWith("Sie geht", StringComparison.Ordinal));
        Assert.Equal(7, gone.LineNo);
        Assert.Equal("Sie geht.", gone.Text);
        Assert.Equal(FountainKind.Action, s.Lines.Single(l => l.Text == "LAUT RUFT ER").Kind);
    }

    [Fact(DisplayName = "Film F2: Erzwungene Köpfe, Szenennummern und englische Schreibweise landen richtig im Shot-Plan")]
    public void Forced_and_foreign_heads()
    {
        var b = FilmBreakdowns.Of(Fountain.Parse(".RÜCKBLENDE\n\nText.\n\nINT. KITCHEN - DAY #7#\n\nText.\n\nEST. FARM - DAWN\n\nText.\n\nI/E CAR - NIGHT\n\nText."));
        Assert.Equal(["RÜCKBLENDE", "KITCHEN", "FARM", "CAR"], b.Scenes.Select(s => s.Location));
        Assert.Equal(["Innen/Außen", "Innen", "Außen", "Innen/Außen"], b.Scenes.Select(s => s.Place));
        Assert.Equal("SZ07", b.Scenes[1].Code);
        Assert.Equal("DAY", b.Scenes[1].Time);
    }

    [Fact(DisplayName = "Film F3: Auszug zählt Figuren, Orte, Wörter; geplante Einstellungen ergeben die Länge, sonst Schätzung")]
    public void Breakdown_counts()
    {
        var b = FilmBreakdowns.Of(Fountain.Parse(Sample));
        Assert.Equal(2, b.Scenes.Count);
        Assert.Equal(["ANNA", "BEN"], b.Roles.Select(r => r.Name));
        Assert.Equal(2, b.Roles[0].Scenes); Assert.Equal(2, b.Roles[0].Speeches); Assert.Equal(10, b.Roles[0].Words);
        Assert.Equal(["BAHNHOF", "GLEIS"], b.Locations.OrderBy(l => l.Name, StringComparer.Ordinal).Select(l => l.Name));
        Assert.False(b.Scenes[0].Planned);
        Assert.Equal((4 + 6) / 2.5 + 7 / 4.0, b.Scenes[0].Seconds, 3);
        Assert.True(b.Scenes[1].Planned);
        Assert.Equal(6.5, b.Scenes[1].Seconds, 3);
        Assert.Contains("| ANNA | 2 | 2 | 10 |", FilmBreakdowns.Report("Test", b));
    }

    [Fact(DisplayName = "Film F3: Fast gleiche Figurennamen werden gemeldet, Szenen ohne Einstellung nicht")]
    public void Similar_names_reported()
    {
        var b = FilmBreakdowns.Of(Fountain.Parse("INNEN. A – TAG\n\nMARIA\nHallo.\n\nMARTA\nHallo.\n"));
        var p = Assert.Single(b.Problems);
        Assert.Contains("MARIA", p); Assert.Contains("MARTA", p);
    }

    [Fact(DisplayName = "Film F3: Shot-Vorschlag ergänzt nur Szenen ohne Einstellungen, jede höchstens 5 s, und ist wiederholbar ohne Doppelung")]
    public void Suggested_shots()
    {
        var once = Fountain.InsertSuggestedShots(Sample);
        var b = FilmBreakdowns.Of(Fountain.Parse(once));
        Assert.Equal(3, b.Scenes[0].Shots.Count);
        Assert.Equal("Totale", b.Scenes[0].Shots[0].Type);
        Assert.Contains("Der Bahnsteig ist leer", b.Scenes[0].Shots[0].Description);
        Assert.Contains("ANNA sagt", b.Scenes[0].Shots[1].Description);
        Assert.All(b.Scenes.SelectMany(s => s.Shots), x => Assert.InRange(x.Seconds, 0.5, 5));
        Assert.Equal(2, b.Scenes[1].Shots.Count);
        Assert.Equal(once, Fountain.InsertSuggestedShots(once));
    }

    [Fact(DisplayName = "Film F2: Typst-Quelle hat Filmsatz und schützt Sonderzeichen; Einstellungen stehen nicht im Drehbuch")]
    public void Typst_source()
    {
        var t = Fountain.ToTypst(Fountain.Parse("Titel: A #1\n\nINNEN. B – TAG\n\n- Totale: Geheim (3 s)\n\nANNA\nDas kostet $5 *heute*.\n"), "x");
        Assert.Contains("DejaVu Sans Mono", t);
        Assert.Contains("A \\#1", t);
        Assert.Contains("\\$5", t);
        Assert.DoesNotContain("Geheim", t);
    }
}
