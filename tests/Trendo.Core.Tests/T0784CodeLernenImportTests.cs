using Trendo.Core.Lernen;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0784 fix3: Der Python-Export trägt kein „Trendo“ mehr im Kopf; Dateien im alten Kopfformat („# Trendo – Programmieren lernen: …“) lassen sich weiter einlesen.</summary>
public class T0784CodeLernenImportTests
{
    const string Body = "def gruss(name):\n    return \"Hallo, \" + name + \"!\"\n";

    [Fact(DisplayName = "T-0784: alter Python-Kopf „# Trendo – Programmieren lernen“ wird beim Einlesen abgeschnitten, Tests darunter ebenso")]
    public void Old_header_is_stripped()
    {
        var alt = "# Trendo – Programmieren lernen: Begrüßung\n# Aufgabe: Schreibe eine Funktion gruss(name)\n\n" + Body + "\n\n# ---- Tests der Aufgabe ----\nimport unittest\n";
        Assert.Equal(Body, CodeLernen.ImportText(alt));
    }

    [Fact(DisplayName = "T-0784: neuer Python-Kopf „# Programmieren lernen“ wird ebenso abgeschnitten; eigener Code ohne Kopf bleibt unverändert")]
    public void New_header_and_plain_code()
    {
        var neu = "# Programmieren lernen: Begrüßung\n# Aufgabe: Schreibe eine Funktion gruss(name)\n\n" + Body;
        Assert.Equal(Body, CodeLernen.ImportText(neu));
        Assert.Equal(Body, CodeLernen.ImportText(Body));
    }
}
