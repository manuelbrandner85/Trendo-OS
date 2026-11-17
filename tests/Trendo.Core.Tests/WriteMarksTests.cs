using Trendo.Core.Text;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>V-12 (T-0700): Schreibzeichen per Knopf – Trendo setzt #, **, -, [[ ]], {{ }} selbst; zweimal drücken nimmt sie wieder weg.</summary>
public class WriteMarksTests
{
    [Fact(DisplayName = "V-12: Fett/Kursiv um die Markierung, ohne Markierung ein markiertes Beispielwort, zweites Drücken entfernt")]
    public void Wrap()
    {
        var r = WriteMarks.Apply("Hallo Welt", 0, 5, WriteMarks.Kind.Bold);
        Assert.Equal("**Hallo** Welt", r.Text); Assert.Equal((2, 7), (r.SelStart, r.SelEnd));
        var back = WriteMarks.Apply(r.Text, r.SelStart, r.SelEnd, WriteMarks.Kind.Bold);
        Assert.Equal("Hallo Welt", back.Text);
        var empty = WriteMarks.Apply("A ", 2, 2, WriteMarks.Kind.Italic);
        Assert.Equal("A *schräger Text*", empty.Text); Assert.Equal("schräger Text", empty.Text[empty.SelStart..empty.SelEnd]);
        Assert.Equal("*Hallo*", WriteMarks.Apply("Hallo", 0, 5, WriteMarks.Kind.Italic).Text);
        // „*“ in „**fett**“ ist nicht kursiv: Kursiv auf fett markiertem Text fügt hinzu statt die Fettung zu zerstören
        Assert.Equal("***x***", WriteMarks.Apply("**x**", 2, 3, WriteMarks.Kind.Italic).Text);
    }

    [Fact(DisplayName = "V-12: Überschrift und Aufzählung je Zeile, ersetzen einander, zweites Drücken entfernt")]
    public void Lines()
    {
        Assert.Equal("eins\n# zwei\ndrei", WriteMarks.Apply("eins\nzwei\ndrei", 6, 6, WriteMarks.Kind.Heading).Text);
        Assert.Equal("eins\nzwei", WriteMarks.Apply("# eins\nzwei", 1, 1, WriteMarks.Kind.Heading).Text);
        Assert.Equal("- a\n- b\nc", WriteMarks.Apply("a\nb\nc", 0, 3, WriteMarks.Kind.List).Text);
        Assert.Equal("# a", WriteMarks.Apply("- a", 0, 0, WriteMarks.Kind.Heading).Text);
        Assert.Equal("- a", WriteMarks.Apply("## a", 0, 0, WriteMarks.Kind.List).Text);
        Assert.Equal("# ", WriteMarks.Apply("", 0, 0, WriteMarks.Kind.Heading).Text);
    }

    [Fact(DisplayName = "V-12: Verweis [[…]] und Lücke {{…}}")]
    public void Links_and_gaps()
    {
        Assert.Equal("siehe [[Strom]]", WriteMarks.Apply("siehe Strom", 6, 11, WriteMarks.Kind.Link).Text);
        var gap = WriteMarks.Apply("Schreib über ", 13, 13, WriteMarks.Kind.Placeholder);
        Assert.Equal("Schreib über {{Thema}}", gap.Text); Assert.Equal("Thema", gap.Text[gap.SelStart..gap.SelEnd]);
        Assert.Equal("x", WriteMarks.Apply("x", 5, -3, WriteMarks.Kind.Bold).Text.Trim('*'));
    }
}
