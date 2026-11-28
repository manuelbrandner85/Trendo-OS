using System.Text;
using Trendo.Core.Wissen;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0788, 3. Prüfrunde (Kern): Notizen des Einsenders fallen bei „Lebende ausblenden“ weg – samt allem, was nur darüber erreichbar war.</summary>
public class T0788Gegenpruefung4Tests
{
    [Fact(DisplayName = "T-0788 R3: Einsender-Notiz (SUBM → NOTE → SOUR) fällt bei „Lebende ausblenden“ transitiv weg, der Name bleibt")]
    public void Subm_notes_removed()
    {
        var t = FamilyKit.FromGedcom(Encoding.UTF8.GetBytes("0 HEAD\r\n1 CHAR UTF-8\r\n1 SUBM @U1@\r\n0 @U1@ SUBM\r\n1 NAME Erika Muster\r\n1 NOTE @N7@\r\n1 NOTE Inline: wohnt in der Gartenstraße\r\n"
            + "0 @N7@ NOTE Erika erreichbar unter Mobilnummer\r\n1 SOUR @S7@\r\n0 @S7@ SOUR\r\n1 TITL Adressbuch Erika\r\n0 @I1@ INDI\r\n1 NAME Max /Muster/\r\n1 DEAT Y\r\n0 TRLR\r\n"));
        var outp = FamilyKit.ToGedcom(t, hideLiving: true, now: T0788Fixtures.Now);
        Assert.Contains("0 @U1@ SUBM\r\n1 NAME Erika Muster\r\n", outp);
        foreach (var gone in new[] { "@N7@", "Mobilnummer", "@S7@", "Adressbuch", "Gartenstraße" }) Assert.DoesNotContain(gone, outp);
        Assert.Contains("Adressbuch", FamilyKit.ToGedcom(t, hideLiving: false, now: T0788Fixtures.Now));
    }
}
