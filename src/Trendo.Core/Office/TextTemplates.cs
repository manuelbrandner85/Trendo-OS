namespace Trendo.Core.Office;

/// <summary>T10: Vorlagen für Trendo Text. Die DIN-5008-Briefe kommen als .docx aus den App-Assets; Lebenslauf und Rechnung baut Trendo selbst –
/// mit Platzhaltern in [eckigen Klammern], die man einfach überschreibt.</summary>
public static class TextTemplates
{
    public static readonly (string Id, string Name, string Hint)[] Builtin =
    [
        ("leer", "Leeres Dokument", "Weißes Blatt"),
        ("lebenslauf", "Lebenslauf", "Tabellarisch, mit Abschnitten für Erfahrung, Ausbildung und Kenntnisse"),
        ("rechnung", "Rechnung", "Mit Rechnungsnummer, Positionen, Summe und Pflichtangaben"),
        ("protokoll", "Protokoll", "Für Besprechungen: Teilnehmer, Punkte, Aufgaben"),
    ];

    public static TextDoc Create(string id) => id switch
    {
        "lebenslauf" => Resume(),
        "rechnung" => Invoice(),
        "protokoll" => Minutes(),
        _ => TextDoc.Empty(),
    };

    static Para P(string t, ParaStyle s = ParaStyle.Text, Align a = Align.Left, ListKind l = ListKind.None) => new(t, s) { Align = a, List = l };

    static TextDoc Resume()
    {
        var d = new TextDoc { Title = "Lebenslauf" };
        d.Blocks.AddRange([
            P("Lebenslauf", ParaStyle.Title),
            P("[Vorname Nachname]", ParaStyle.Subtitle),
            P("[Straße Hausnummer] · [PLZ Ort] · [Telefon] · [E-Mail]"),
            P("Berufserfahrung", ParaStyle.Heading1),
            new Table { Rows = [["[MM/JJJJ] – heute", "[Position], [Firma], [Ort]\n[Aufgaben und Erfolge in 2–3 Stichpunkten]"], ["[MM/JJJJ] – [MM/JJJJ]", "[Position], [Firma], [Ort]"]] },
            P("Ausbildung", ParaStyle.Heading1),
            new Table { Rows = [["[MM/JJJJ] – [MM/JJJJ]", "[Abschluss], [Schule/Hochschule], [Ort]"]] },
            P("Kenntnisse", ParaStyle.Heading1),
            P("Sprachen: [Deutsch (Muttersprache), Englisch (fließend)]", l: ListKind.Bullet),
            P("EDV: [Programme]", l: ListKind.Bullet),
            P("Führerschein: [Klasse B]", l: ListKind.Bullet),
            P("Interessen", ParaStyle.Heading1),
            P("[Hobbys, Ehrenamt]"),
            P(""),
            P("[Ort], [Datum]"),
            P("[Unterschrift]"),
        ]);
        TextDoc.Format((Para)d.Blocks[1], 0, ((Para)d.Blocks[1]).Text.Length, Fmt.Bold, true);
        return d;
    }

    static TextDoc Invoice()
    {
        var d = new TextDoc { Title = "Rechnung" };
        d.Blocks.AddRange([
            P("[Dein Name / Firma] · [Straße] · [PLZ Ort]"),
            P(""),
            P("[Empfänger Name]\n[Straße Hausnummer]\n[PLZ Ort]"),
            P("[Ort], [Datum]", a: Align.Right),
            P("Rechnung Nr. [2026-001]", ParaStyle.Heading1),
            P("Leistungszeitraum: [TT.MM.JJJJ – TT.MM.JJJJ]"),
            new Table { Rows = [["Pos.", "Beschreibung", "Menge", "Einzelpreis", "Gesamt"], ["1", "[Leistung]", "[1]", "[0,00 €]", "[0,00 €]"], ["2", "[Leistung]", "[1]", "[0,00 €]", "[0,00 €]"], ["", "", "", "Summe netto", "[0,00 €]"], ["", "", "", "zzgl. 19 % USt.", "[0,00 €]"], ["", "", "", "Gesamtbetrag", "[0,00 €]"]] },
            P("Bitte überweise den Gesamtbetrag bis zum [TT.MM.JJJJ] auf das unten genannte Konto. Vielen Dank!"),
            P("[Bei Kleinunternehmen: Gemäß § 19 UStG wird keine Umsatzsteuer berechnet.]", ParaStyle.Quote),
            P("Bank: [Bank] · IBAN: [DE00 0000 0000 0000 0000 00] · BIC: [XXXXDEXX] · Steuernummer: [000/000/00000]"),
        ]);
        return d;
    }

    static TextDoc Minutes()
    {
        var d = new TextDoc { Title = "Protokoll" };
        d.Blocks.AddRange([
            P("Protokoll: [Thema]", ParaStyle.Title),
            P("Datum: [TT.MM.JJJJ] · Ort: [Ort] · Protokoll: [Name]"),
            P("Teilnehmende", ParaStyle.Heading2),
            P("[Name]", l: ListKind.Bullet),
            P("Besprochen", ParaStyle.Heading2),
            P("[Punkt 1]", l: ListKind.Number),
            P("[Punkt 2]", l: ListKind.Number),
            P("Aufgaben", ParaStyle.Heading2),
            new Table { Rows = [["Was", "Wer", "Bis wann"], ["[Aufgabe]", "[Name]", "[Datum]"]] },
        ]);
        return d;
    }
}
