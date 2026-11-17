using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0710: Regeln der klassischen Menüleiste – Reihenfolge, unterstrichene Buchstaben, Kürzel und ihre Konflikte.</summary>
public class T0710_ProgramMenuTests
{
    static MenuCmd C(string id, string label, string keys = "") => new(id, label, () => { }, keys);

    [Fact(DisplayName = "T-0710: Reihenfolge Datei · Bearbeiten · Ansicht · Fachmenüs · KI · Hilfe; gleiche Menüs zusammengelegt, leere und doppelte Trennlinien fallen weg")]
    public void Normalize_orders_and_cleans()
    {
        var menus = ProgramMenu.Normalize([
            new("Hilfe", [C("h", "Hilfe")]),
            new("Bild", [C("b1", "Drehen")]),
            new("Datei", [MenuCmd.Separator, C("n", "Neu"), MenuCmd.Separator, MenuCmd.Separator, C("s", "Speichern"), MenuCmd.Separator]),
            new("Leer", [MenuCmd.Separator]),
            new("KI", [C("k", "Auftrag")]),
            new("Ansicht", [C("v", "Zoom")]),
            new("Bearbeiten", [C("u", "Rückgängig")]),
            new("Datei", [C("x", "Schließen")]),
            new("Spur", [C("t", "Teilen"), new("sub", "Untermenü leer", Sub: [MenuCmd.Separator])]),
        ]);
        Assert.Equal(["Datei", "Bearbeiten", "Ansicht", "Bild", "Spur", "KI", "Hilfe"], menus.Select(m => m.Title));
        Assert.Equal(["n", "-", "s", "-", "x"], menus[0].Items.Select(i => i.Id));
        Assert.Equal(["t"], menus[4].Items.Select(i => i.Id)); // leeres Untermenü fällt weg
    }

    [Fact(DisplayName = "T-0710: unterstrichene Buchstaben – Alt+D Datei, Alt+B Bearbeiten, Alt+A Ansicht, Alt+H Hilfe; Fachmenüs bekommen den ersten freien Buchstaben, nie doppelt")]
    public void Access_keys_are_unique_and_windows_like()
    {
        var keys = ProgramMenu.AccessKeys(["Datei", "Bearbeiten", "Ansicht", "Bild", "Bearbeitung", "KI", "Hilfe"], fixedFirst: true);
        Assert.Equal('D', keys[0]); Assert.Equal('B', keys[1]); Assert.Equal('A', keys[2]); Assert.Equal('K', keys[5]); Assert.Equal('H', keys[6]);
        Assert.Equal('I', keys[3]); // „Bild“: B ist vergeben
        Assert.Equal('E', keys[4]);
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.Equal("_Datei", ProgramMenu.WithAccess("Datei", 'D'));
        Assert.Equal("B_ild", ProgramMenu.WithAccess("Bild", 'I'));
        Assert.Equal("Profi__Werkzeug", ProgramMenu.WithAccess("Profi_Werkzeug", '\0'));
    }

    [Fact(DisplayName = "T-0710: Kürzel lesen – Strg+Umschalt+S, F1, Strg+O; Unsinn wird nicht als Kürzel genommen")]
    public void Parse_shortcuts()
    {
        Assert.Equal((true, true, false, "S"), ProgramMenu.Parse("Strg+Umschalt+S"));
        Assert.Equal((false, false, false, "F1"), ProgramMenu.Parse("F1"));
        Assert.Equal((true, false, false, "O"), ProgramMenu.Parse(ProgramMenu.Keys.Oeffnen));
        Assert.Null(ProgramMenu.Parse(""));
        Assert.Null(ProgramMenu.Parse("Hyper+S"));
    }

    [Fact(DisplayName = "T-0710: Konflikte werden gefunden – zwei Einträge mit Strg+S, Strg+K für etwas anderes als die Befehlsleiste; erlaubt: Hilfe mit F1")]
    public void Conflicts_are_reported()
    {
        var ok = ProgramMenu.Conflicts([new("Datei", [C("prog-save", "Speichern", "Strg+S")]), new("Hilfe", [C("help-show", "Hilfe", "F1"), C("help-palette", "Befehl suchen", "Strg+K")])]);
        Assert.Empty(ok);
        var bad = ProgramMenu.Conflicts([new("Datei", [C("a", "Speichern", "Strg+S"), C("b", "Senden", "Strg+S")]), new("Bild", [C("c", "Kacheln", "Strg+K")])]);
        Assert.Contains(bad, x => x.StartsWith("Strg+S:"));
        Assert.Contains(bad, x => x.Contains("Befehlsleiste"));
    }

    [Fact(DisplayName = "T-0710: ausgegraut – ein Eintrag ohne Möglichkeit läuft nicht")]
    public void Disabled_entry_does_not_run()
    {
        var hits = 0;
        var c = new MenuCmd("x", "Wiederholen", () => hits++, Enabled: () => false);
        Assert.False(c.CanRun);
        if (c.CanRun) c.Run!();
        Assert.Equal(0, hits);
    }
}
