using Trendo.Core.Programs;
using Xunit;
using P = Trendo.Core.Programs.AssistParam;
using K = Trendo.Core.Programs.AssistKind;

namespace Trendo.Core.Tests;

/// T-0315: Programm-Assistent – deutsche Aufträge ohne KI verstehen, Rückfragen als Knöpfe, Ja vor Aktionen mit Folgen, Vorlieben, KI-Pläne streng prüfen.
public class AssistTests
{
    static readonly DateTime Today = new(2026, 11, 1);
    static readonly AssistAction Buchen = new("buchen", "Buchung eintragen", ["buchen", "buchung", "ausgabe", "eintragen"],
        [new P("betrag", "Betrag", K.Number), new P("kategorie", "Kategorie", K.Choice, ["Lebensmittel", "Wohnen", "Auto", "Freizeit"], "Lebensmittel"), new P("datum", "Datum", K.Date, Default: "heute", Required: false)], Next: ["diagramm"]);
    static readonly AssistAction Sortieren = new("sortieren", "Sortieren", ["sortiere", "ordne", "reihenfolge"], [new P("nach", "Spalte", K.Choice, ["Datum", "Betrag", "Kategorie"])]);
    static readonly AssistAction Diagramm = new("diagramm", "Diagramm zeigen", ["diagramm", "grafik", "torte"]);
    static readonly AssistAction Senden = new("senden", "Per E-Mail senden", ["sende", "schicke", "mail", "email"], [new P("an", "Empfänger", Words: ["an"])], SideEffect: true);
    static readonly AssistAction Titel = new("titel", "Titel setzen", ["titel", "überschrift", "heißt"], [new P("text", "Titel", Words: ["titel", "überschrift"]), new P("fett", "Fett", K.YesNo, Default: "ja", Required: false, Words: ["fett"])]);
    static readonly AssistAction[] All = [Buchen, Sortieren, Diagramm, Senden, Titel];

    [Fact(DisplayName = "T-0315: „Buche 12,50 € für Lebensmittel morgen und sortiere nach Betrag“ → zwei Schritte mit Betrag, Auswahl und Datum")]
    public void Two_steps_with_values()
    {
        var p = AssistPlanner.Plan("Buche 12,50 € für Lebensmittel morgen und sortiere nach Betrag", All, today: Today);
        Assert.True(p.Ready, string.Join(" | ", p.Questions.Select(q => q.Text)));
        Assert.Equal(["buchen", "sortieren"], p.Steps.Select(s => s.Action.Id));
        Assert.Equal(("12.5", "Lebensmittel", "2026-11-02"), (p.Steps[0].Args["betrag"], p.Steps[0].Args["kategorie"], p.Steps[0].Args["datum"]));
        Assert.Equal("Betrag", p.Steps[1].Args["nach"]);
        Assert.Contains("Datum: 02.11.2026", p.Steps[0].Text);
    }

    [Fact(DisplayName = "T-0315: Fehlende Auswahl → Rückfrage mit Knöpfen, Empfehlung zuerst; Antwort füllt den Plan; „immer so“ wird gemerkt und beim nächsten Mal genutzt")]
    public void Question_answer_remember()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-assist-" + Guid.NewGuid().ToString("N"));
        try
        {
            var prefs = new AssistPrefs(dir, "haushaltsbuch");
            var p = AssistPlanner.Plan("Trag 30 Euro ein", All, prefs.Values, Today);
            var q = Assert.Single(p.Questions);
            Assert.Equal(["Lebensmittel", "Wohnen", "Auto", "Freizeit"], q.Options); Assert.Equal(0, q.Recommended); Assert.True(q.AllowOther);
            Assert.False(p.Ready);
            AssistPlanner.Answer(p, q.Id, "Auto", prefs, remember: true);
            Assert.True(p.Ready); Assert.Equal("Auto", p.Steps[0].Args["kategorie"]);
            var again = AssistPlanner.Plan("Buchung 8 eintragen", All, new AssistPrefs(dir, "haushaltsbuch").Values, Today);
            Assert.True(again.Ready); Assert.Equal("Auto", again.Steps[0].Args["kategorie"]); Assert.Equal(["kategorie"], again.Steps[0].FromMemory);
            Assert.Contains("Buchung eintragen: Kategorie = Auto", prefs.All.Select(i => i.Text));
            Assert.True(prefs.Forget("buchen.kategorie")); Assert.Empty(new AssistPrefs(dir, "haushaltsbuch").All);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0315: Aktion mit Folgen (senden) braucht ein Ja – Empfehlung ist „Nein“; Nein streicht den Schritt, Ja gibt ihn frei")]
    public void Side_effect_needs_yes()
    {
        var p = AssistPlanner.Plan("Zeig ein Diagramm und schicke es an erika@example.org", All, today: Today);
        Assert.Equal(["diagramm", "senden"], p.Steps.Select(s => s.Action.Id));
        Assert.Equal("erika@example.org", p.Steps[1].Args["an"]);
        var ok = Assert.Single(p.Questions); Assert.True(ok.Confirm); Assert.Equal(["Ja", "Nein"], ok.Options); Assert.Equal(1, ok.Recommended); Assert.False(ok.AllowOther);
        Assert.True(p.HasSideEffects);
        var copy = AssistPlanner.Plan("Zeig ein Diagramm und schicke es an erika@example.org", All, today: Today);
        AssistPlanner.Answer(copy, copy.Questions[0].Id, "Nein");
        Assert.Equal(["diagramm"], copy.Steps.Select(s => s.Action.Id)); Assert.True(copy.Ready);
        AssistPlanner.Answer(p, ok.Id, "Ja");
        Assert.True(p.Ready); Assert.Equal(2, p.Steps.Count);
    }

    [Fact(DisplayName = "T-0315: Text in Anführungszeichen oder nach dem Stichwort; „nicht fett“ = Nein; Unverstandenes wird ehrlich gemeldet")]
    public void Text_yesno_unclear()
    {
        var p = AssistPlanner.Plan("Setze den Titel „Sommerfest, im Verein“ nicht fett", All, today: Today);
        Assert.Equal(("Sommerfest, im Verein", "nein"), (p.Steps[0].Args["text"], p.Steps[0].Args["fett"]));
        var q = AssistPlanner.Plan("Überschrift: Jahresbericht 2026", All, today: Today);
        Assert.Equal(("Jahresbericht 2026", "ja"), (q.Steps[0].Args["text"], q.Steps[0].Args["fett"]));
        var u = AssistPlanner.Plan("Backe einen Kuchen", All, today: Today);
        Assert.False(u.Understood); Assert.Equal(["Backe einen Kuchen"], u.Unclear); Assert.Contains("nicht verstanden", u.Summary);
        Assert.False(AssistPlanner.Plan("   ", All).Understood);
    }

    [Fact(DisplayName = "T-0315: Datum 15.11. und Zahl getrennt erkannt; Zahl als Antwort geprüft; Vorschläge aus „Next“ und aus dem Inhalt")]
    public void Dates_numbers_suggestions()
    {
        var p = AssistPlanner.Plan("Buchung am 15.11. über 99 für Freizeit", All, today: Today);
        Assert.Equal(("99", "2026-11-15"), (p.Steps[0].Args["betrag"], p.Steps[0].Args["datum"]));
        var miss = AssistPlanner.Plan("Buchung für Wohnen", All, today: Today);
        Assert.Throws<ArgumentException>(() => AssistPlanner.Answer(miss, miss.Questions[0].Id, "viel"));
        AssistPlanner.Answer(miss, miss.Questions[0].Id, "750,00"); Assert.Equal("750", miss.Steps[0].Args["betrag"]);
        var s = AssistPlanner.Suggest([Buchen], All, [new AssistSuggestion("Ausgaben im November sind 20 % höher – Budget setzen?", "Budget setzen")]);
        Assert.Equal(["Budget setzen", "Diagramm zeigen"], s.Select(x => x.Prompt));
        Assert.Empty(AssistPlanner.Suggest([Diagramm], All));
    }

    [Fact(DisplayName = "T-0315: KI-Plan wird streng geprüft – nur bekannte Aktionen, gültige Auswahlwerte und Zahlen; Folgen brauchen trotzdem ein Ja")]
    public void Ki_plan_is_checked()
    {
        var prompt = AssistPlanner.KiPrompt("Haushaltsbuch", All, "Du bist ein genauer Buchhalter.", "Beträge immer mit zwei Nachkommastellen.");
        Assert.Contains("buchen: Buchung eintragen (betrag, kategorie=Lebensmittel|Wohnen|Auto|Freizeit, datum)", prompt);
        Assert.Contains("genauer Buchhalter", prompt); Assert.Contains("zwei Nachkommastellen", prompt);
        var ki = """Klar! {"steps":[{"action":"buchen","args":{"betrag":"12.5","kategorie":"Urlaub"}},{"action":"format_c","args":{}},{"action":"senden","args":{"an":"max@example.org"}}]}""";
        var p = AssistPlanner.FromKi("x", ki, All);
        Assert.Equal("KI", p.Source);
        Assert.Equal(["buchen", "senden"], p.Steps.Select(s => s.Action.Id)); // unbekannte Aktion verworfen
        Assert.False(p.Steps[0].Args.ContainsKey("kategorie")); // „Urlaub“ steht nicht in der Liste
        Assert.Contains(p.Questions, q => q.Param == "kategorie"); Assert.Contains(p.Questions, q => q.Confirm);
        Assert.False(AssistPlanner.FromKi("x", "keine Ahnung", All).Understood);
        Assert.False(AssistPlanner.FromKi("x", "{kaputt", All).Understood);
    }

    [Fact(DisplayName = "T-0315: Wiederholung – „füge 3 Zeilen hinzu“ bzw. „zwei Spalten“ läuft mehrfach (höchstens 50)")]
    public void Times()
    {
        var zeile = new AssistAction("ms-addrow", "Zeile +", ["Zeile"]); var spalte = new AssistAction("ms-addcol", "Spalte +", ["Spalte"]);
        var p = AssistPlanner.Plan("Füge 3 Zeilen hinzu und zwei Spalten", [zeile, spalte]);
        Assert.Equal([3, 2], p.Steps.Select(s => s.Times)); Assert.Equal("Zeile + (3×)", p.Steps[0].Text);
        Assert.Equal(1, AssistPlanner.Plan("Zeile 500 einfügen", [zeile]).Steps[0].Times);
    }

    [Fact(DisplayName = "T-0315: Wortstämme und Satzteile – „Zeilen“≈„Zeile“, Komma in Beträgen und Datum zerschneidet nichts")]
    public void Stems_and_clauses()
    {
        Assert.Equal(AssistPlanner.Stem("Zeile"), AssistPlanner.Stem("Zeilen"));
        Assert.Equal(AssistPlanner.Stem("sortiere"), AssistPlanner.Stem("sortieren"));
        Assert.Equal("ueberschrift", AssistPlanner.Norm("Überschrift!"));
        Assert.Single(AssistPlanner.Clauses("Buche 12,50 € am 15.11. 2026"));
        Assert.Equal(2, AssistPlanner.Clauses("Füge eine Zeile hinzu, sortiere dann nach Betrag").Count);
    }
}
