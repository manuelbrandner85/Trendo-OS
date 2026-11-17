using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Trendo.Core.Office;

namespace Trendo.Core.Buero;

// ===================== STUDIO-CV-01 Bewerbung & Lebenslauf (T-0671) =====================
// Clean-Room nach dem Verhalten von Lebenslauf-Editoren (Reactive Resume – MIT, nur Bedienkonzept; Europass; Canva-Lebenslauf).
// Lebenslauf als strukturierte Daten, Layouts, Prüfung, ATS-Textfassung, Anschreiben, Bewerbungsmappe und Übersicht der Bewerbungen.

public sealed class CvPerson
{
    public string Name { get; set; } = "";
    /// <summary>Berufsbezeichnung unter dem Namen, z. B. „Kauffrau für Büromanagement“.</summary>
    public string Titel { get; set; } = "";
    public string Email { get; set; } = "";
    public string Telefon { get; set; } = "";
    public string Strasse { get; set; } = "";
    public string Ort { get; set; } = "";
    public string Web { get; set; } = "";
    public string Geburtsdatum { get; set; } = "";
    /// <summary>Bewerbungsfoto (Bilddatei), freiwillig.</summary>
    public string Foto { get; set; } = "";
    /// <summary>Kurzprofil – zwei bis vier Sätze.</summary>
    public string Profil { get; set; } = "";
}

/// <summary>Berufliche Station: <see cref="Art"/> beruf, praktikum, ehrenamt oder sonstiges. Datum als „MM.JJJJ“; <see cref="Bis"/> leer = bis heute.</summary>
public sealed class CvStation
{
    public string Id { get; set; } = "";
    public string Art { get; set; } = "beruf";
    public string Titel { get; set; } = "";
    public string Firma { get; set; } = "";
    public string Ort { get; set; } = "";
    public string Von { get; set; } = "";
    public string Bis { get; set; } = "";
    /// <summary>Aufgaben und Erfolge, eine je Zeile.</summary>
    public string Text { get; set; } = "";
}

public sealed class CvAusbildung
{
    public string Id { get; set; } = "";
    public string Abschluss { get; set; } = "";
    public string Einrichtung { get; set; } = "";
    public string Ort { get; set; } = "";
    public string Von { get; set; } = "";
    public string Bis { get; set; } = "";
    public string Note { get; set; } = "";
    public string Text { get; set; } = "";
}

/// <summary>Sprache mit Stufe nach dem Gemeinsamen Europäischen Referenzrahmen (GER): A1 bis C2 oder Muttersprache.</summary>
public sealed class CvSprache
{
    public string Name { get; set; } = "";
    public string Stufe { get; set; } = "B2";
}

public sealed class Lebenslauf
{
    public CvPerson Person { get; set; } = new();
    public List<CvStation> Stationen { get; set; } = [];
    public List<CvAusbildung> Ausbildung { get; set; } = [];
    public List<string> Kenntnisse { get; set; } = [];
    public List<CvSprache> Sprachen { get; set; } = [];
    public List<string> Interessen { get; set; } = [];
    /// <summary>klassisch, modern oder kompakt.</summary>
    public string Layout { get; set; } = "klassisch";
    public string Akzent { get; set; } = "#1F5FA8";
    /// <summary>Ort und Datum unter dem Lebenslauf (wie in Deutschland üblich), leer = ohne.</summary>
    public string Unterschrift { get; set; } = "";
}

/// <summary>Die Stelle, auf die man sich bewirbt.</summary>
public sealed class CvStelle
{
    public string Firma { get; set; } = "";
    public string Position { get; set; } = "";
    public string Ansprechpartner { get; set; } = "";
    public string Anschrift { get; set; } = "";
    /// <summary>Text der Stellenanzeige (eingefügt).</summary>
    public string Anzeige { get; set; } = "";
    public string Quelle { get; set; } = "";
    public string Eintritt { get; set; } = "";
}

/// <summary>Eine Bewerbung in der Übersicht – Status und Frist wie in einer Bewerbungs-Tabelle.</summary>
public sealed class BewerbungEintrag
{
    public string Id { get; set; } = "";
    public string Firma { get; set; } = "";
    public string Stelle { get; set; } = "";
    /// <summary>idee, versendet, gespraech, zusage, absage, zurueckgezogen.</summary>
    public string Status { get; set; } = "idee";
    /// <summary>Datum (JJJJ-MM-TT) der Bewerbung.</summary>
    public string Beworben { get; set; } = "";
    /// <summary>Frist (Bewerbungsschluss, Gesprächstermin, Rückmeldung bis) als JJJJ-MM-TT.</summary>
    public string Frist { get; set; } = "";
    public string Notiz { get; set; } = "";
}

/// <summary>Das ganze Dokument des Programms: Lebenslauf, aktuelle Stelle, Anschreiben, Anlagen und alle Bewerbungen.</summary>
public sealed class BewerbungsAkte
{
    public Lebenslauf Cv { get; set; } = new();
    public CvStelle Stelle { get; set; } = new();
    public string Anschreiben { get; set; } = "";
    /// <summary>Anlagen (PDF oder Bild) für die Mappe – Zeugnisse, Zertifikate.</summary>
    public List<string> Anlagen { get; set; } = [];
    public bool Deckblatt { get; set; } = true;
    public List<BewerbungEintrag> Bewerbungen { get; set; } = [];

    static readonly JsonSerializerOptions O = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public JsonObject ToJson() => JsonSerializer.SerializeToNode(this, O)!.AsObject();
    public static BewerbungsAkte FromJson(JsonNode? n)
    {
        if (n is not JsonObject o) return new();
        try { return o.Deserialize<BewerbungsAkte>(O) ?? new(); } catch (JsonException) { return new(); }
    }
    public BewerbungsAkte Copy() => FromJson(ToJson());
}

/// <summary>Datumsangaben im Lebenslauf: Monat und Jahr („03.2019“, „3/2019“, „2019-03“) oder nur das Jahr („2019“ = Januar bzw. Dezember als Ende).</summary>
public static partial class CvDate
{
    [GeneratedRegex(@"^\s*(\d{1,2})\s*[./-]\s*(\d{4})\s*$")] private static partial Regex MonthYear();
    [GeneratedRegex(@"^\s*(\d{4})\s*-\s*(\d{1,2})\s*$")] private static partial Regex Iso();
    [GeneratedRegex(@"^\s*(\d{4})\s*$")] private static partial Regex YearOnly();
    public static readonly string[] Heute = ["", "heute", "bis heute", "jetzt", "aktuell", "laufend"];

    public static bool IsOpen(string s) => Heute.Contains((s ?? "").Trim().ToLowerInvariant());

    /// <summary>Monatsnummer (Jahr·12 + Monat − 1) oder null, wenn das Format nicht stimmt. <paramref name="end"/>: nur ein Jahr zählt als Dezember.</summary>
    public static int? Parse(string s, bool end = false)
    {
        s ??= "";
        int y, m;
        if (MonthYear().Match(s) is { Success: true } a) { m = int.Parse(a.Groups[1].Value, CultureInfo.InvariantCulture); y = int.Parse(a.Groups[2].Value, CultureInfo.InvariantCulture); }
        else if (Iso().Match(s) is { Success: true } b) { y = int.Parse(b.Groups[1].Value, CultureInfo.InvariantCulture); m = int.Parse(b.Groups[2].Value, CultureInfo.InvariantCulture); }
        else if (YearOnly().Match(s) is { Success: true } c) { y = int.Parse(c.Groups[1].Value, CultureInfo.InvariantCulture); m = end ? 12 : 1; }
        else return null;
        if (m is < 1 or > 12 || y is < 1900 or > 2200) return null;
        return y * 12 + m - 1;
    }
    public static int Of(DateTime d) => d.Year * 12 + d.Month - 1;
    public static string Text(int month) => $"{month % 12 + 1:00}.{month / 12}";
    /// <summary>Einheitliche Schreibweise für Anzeige und Export: „03.2019 – heute“.</summary>
    public static string Range(string von, string bis)
    {
        var v = Parse(von) is { } a ? Text(a) : von.Trim();
        var b = IsOpen(bis) ? "heute" : Parse(bis, true) is { } e ? Text(e) : bis.Trim();
        return v.Length == 0 ? b : $"{v} – {b}";
    }
    /// <summary>Spätere Stationen zuerst (umgekehrt chronologisch, wie in Deutschland üblich).</summary>
    public static int SortKey(string von, string bis) => IsOpen(bis) ? int.MaxValue : Parse(bis, true) ?? Parse(von) ?? 0;
}

/// <summary>Ein Prüfhinweis. <see cref="Fehler"/> = muss vor dem Versand behoben werden.</summary>
public sealed record CvFinding(bool Fehler, string Feld, string Text);

public static partial class CvCheck
{
    public static readonly string[] Stufen = ["A1", "A2", "B1", "B2", "C1", "C2", "Muttersprache"];
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[a-zA-Z]{2,}$")] private static partial Regex Mail();
    [GeneratedRegex(@"\d")] private static partial Regex Digit();

    /// <summary>Zeiträume aller Stationen und Ausbildungen (Monatsnummern, einschließlich). Fehlerhafte Angaben fehlen hier – sie meldet <see cref="Run"/>.</summary>
    public static List<(int Von, int Bis, string Was)> Periods(Lebenslauf cv, DateTime today)
    {
        var now = CvDate.Of(today);
        var res = new List<(int, int, string)>();
        void Add(string von, string bis, string was)
        {
            if (CvDate.Parse(von) is not { } a) return;
            var b = CvDate.IsOpen(bis) ? now : CvDate.Parse(bis, true);
            if (b is null || b < a) return;
            res.Add((a, b.Value, was));
        }
        foreach (var s in cv.Stationen) Add(s.Von, s.Bis, s.Titel.Length > 0 ? s.Titel : s.Firma);
        foreach (var a in cv.Ausbildung) Add(a.Von, a.Bis, a.Abschluss.Length > 0 ? a.Abschluss : a.Einrichtung);
        return res.OrderBy(p => p.Item1).ToList();
    }

    /// <summary>Lücken im Zeitverlauf ab <paramref name="minMonths"/> Monaten – auch zwischen der letzten Station und heute.</summary>
    public static List<(int Von, int Bis, int Monate)> Gaps(Lebenslauf cv, DateTime today, int minMonths = 3)
    {
        var p = Periods(cv, today); var res = new List<(int, int, int)>();
        if (p.Count == 0) return res;
        var end = p[0].Bis;
        foreach (var x in p.Skip(1))
        {
            if (x.Von > end + 1 && x.Von - end - 1 >= minMonths) res.Add((end + 1, x.Von - 1, x.Von - end - 1));
            end = Math.Max(end, x.Bis);
        }
        var now = CvDate.Of(today);
        if (now - end >= minMonths + 1) res.Add((end + 1, now, now - end));
        return res;
    }

    /// <summary>Geschätzte Seitenzahl A4 (Zeilen grob wie im klassischen Layout gezählt).</summary>
    public static double Pages(Lebenslauf cv)
    {
        static int Lines(string t, int perLine = 80) => (t ?? "").Replace("\r", "").Split('\n').Sum(l => Math.Max(1, (int)Math.Ceiling(l.Length / (double)perLine)));
        var lines = 8 + Lines(cv.Person.Profil, 95) + 3;
        lines += cv.Stationen.Sum(s => 2 + (s.Text.Trim().Length == 0 ? 0 : Lines(s.Text, 70))) + 3;
        lines += cv.Ausbildung.Sum(a => 2 + (a.Text.Trim().Length == 0 ? 0 : Lines(a.Text, 70))) + 3;
        lines += (int)Math.Ceiling(cv.Kenntnisse.Count / 3.0) + cv.Sprachen.Count + (cv.Interessen.Count > 0 ? 3 : 0) + 6;
        return Math.Round(lines / 52.0, 1);
    }

    /// <summary>Prüft den Lebenslauf: Kontaktdaten, Datumsformat, Reihenfolge, Lücken, Länge, Sprachen nach GER.</summary>
    public static List<CvFinding> Run(Lebenslauf cv, DateTime today)
    {
        var f = new List<CvFinding>(); var p = cv.Person;
        if (p.Name.Trim().Length == 0) f.Add(new(true, "Name", "Dein Name fehlt."));
        if (p.Email.Trim().Length == 0) f.Add(new(true, "E-Mail", "Die E-Mail-Adresse fehlt – darüber melden sich Firmen meist."));
        else if (!Mail().IsMatch(p.Email.Trim())) f.Add(new(true, "E-Mail", $"„{p.Email}“ sieht nicht wie eine E-Mail-Adresse aus."));
        if (p.Telefon.Trim().Length == 0) f.Add(new(true, "Telefon", "Die Telefonnummer fehlt."));
        else if (Digit().Matches(p.Telefon).Count < 6) f.Add(new(true, "Telefon", $"„{p.Telefon}“ hat zu wenige Ziffern für eine Telefonnummer."));
        if (p.Ort.Trim().Length == 0) f.Add(new(false, "Wohnort", "Der Wohnort fehlt – für viele Stellen wichtig (Anfahrt)."));
        if (cv.Stationen.Count == 0 && cv.Ausbildung.Count == 0) f.Add(new(true, "Lebenslauf", "Noch keine Station und keine Ausbildung eingetragen."));
        var now = CvDate.Of(today);
        void Dates(string von, string bis, string was, string feld)
        {
            var a = CvDate.Parse(von);
            if (a is null) f.Add(new(true, feld, $"{was}: Beginn „{von}“ bitte als MM.JJJJ schreiben, z. B. 03.2019."));
            if (!CvDate.IsOpen(bis) && CvDate.Parse(bis, true) is null) f.Add(new(true, feld, $"{was}: Ende „{bis}“ bitte als MM.JJJJ schreiben oder leer lassen (= heute)."));
            if (a is { } x && !CvDate.IsOpen(bis) && CvDate.Parse(bis, true) is { } y && y < x) f.Add(new(true, feld, $"{was}: Das Ende liegt vor dem Beginn."));
            if (a is { } z && z > now) f.Add(new(false, feld, $"{was}: Der Beginn liegt in der Zukunft – stimmt das?"));
        }
        foreach (var s in cv.Stationen)
        {
            var was = s.Titel.Trim().Length > 0 ? s.Titel.Trim() : "Station ohne Titel";
            if (s.Titel.Trim().Length == 0) f.Add(new(true, "Station", "Eine Station hat keine Bezeichnung (z. B. „Verkäuferin“)."));
            if (s.Firma.Trim().Length == 0) f.Add(new(false, "Station", $"{was}: Firma oder Ort fehlt."));
            Dates(s.Von, s.Bis, was, "Station");
        }
        foreach (var a in cv.Ausbildung)
        {
            var was = a.Abschluss.Trim().Length > 0 ? a.Abschluss.Trim() : "Ausbildung ohne Abschluss";
            if (a.Abschluss.Trim().Length == 0) f.Add(new(true, "Ausbildung", "Eine Ausbildung hat keinen Abschluss (z. B. „Mittlere Reife“)."));
            Dates(a.Von, a.Bis, was, "Ausbildung");
        }
        foreach (var (von, bis, n) in Gaps(cv, today))
            f.Add(new(false, "Lücke", $"Lücke von {n} Monaten ({CvDate.Text(von)} – {CvDate.Text(bis)}) – kurz erklären, z. B. Weiterbildung, Elternzeit, Reise oder Jobsuche."));
        var open = cv.Stationen.Count(s => CvDate.IsOpen(s.Bis));
        if (open > 2) f.Add(new(false, "Station", $"{open} Stationen laufen „bis heute“ – stimmt das?"));
        var pages = Pages(cv);
        if (pages > 2.2) f.Add(new(false, "Länge", $"Etwa {pages:0.#} Seiten – üblich sind höchstens zwei. Ältere Stationen kürzen."));
        if (p.Profil.Trim().Length > 600) f.Add(new(false, "Profil", "Das Kurzprofil ist länger als 600 Zeichen – zwei bis vier Sätze reichen."));
        foreach (var s in cv.Sprachen)
        {
            if (s.Name.Trim().Length == 0) f.Add(new(true, "Sprachen", "Eine Sprache hat keinen Namen."));
            if (!Stufen.Contains(s.Stufe.Trim(), StringComparer.OrdinalIgnoreCase)) f.Add(new(true, "Sprachen", $"{s.Name}: Stufe „{s.Stufe}“ – bitte A1 bis C2 oder Muttersprache (GER)."));
        }
        if (cv.Kenntnisse.Count == 0) f.Add(new(false, "Kenntnisse", "Noch keine Kenntnisse eingetragen (z. B. Programme, Führerschein, Fachwissen)."));
        return f;
    }

    /// <summary>Beschreibung der GER-Stufe in Alltagswörtern.</summary>
    public static string StufeText(string s) => s.Trim().ToUpperInvariant() switch
    {
        "A1" => "Anfänger", "A2" => "Grundkenntnisse", "B1" => "Gute Kenntnisse", "B2" => "Sehr gute Kenntnisse", "C1" => "Fließend", "C2" => "Verhandlungssicher",
        "MUTTERSPRACHE" => "Muttersprache", _ => s,
    };
}

/// <summary>Textfassungen: ATS-freundlich (für Bewerbungsportale, die Lebensläufe automatisch einlesen) und Stichwort-Abgleich mit der Anzeige.</summary>
public static partial class CvText
{
    static IEnumerable<CvStation> Stationen(Lebenslauf cv) => cv.Stationen.OrderByDescending(s => CvDate.SortKey(s.Von, s.Bis));
    static IEnumerable<CvAusbildung> Ausbildung(Lebenslauf cv) => cv.Ausbildung.OrderByDescending(a => CvDate.SortKey(a.Von, a.Bis));
    static IEnumerable<string> Lines(string t) => (t ?? "").Replace("\r", "").Split('\n').Select(l => l.Trim().TrimStart('-', '•', '*').Trim()).Where(l => l.Length > 0);

    /// <summary>Reiner Text ohne Spalten, Tabellen und Bilder: Standard-Überschriften in Großbuchstaben, Daten als MM/JJJJ, Aufzählungen mit „- “.</summary>
    public static string Ats(Lebenslauf cv)
    {
        var p = cv.Person; var sb = new StringBuilder();
        sb.Append(p.Name.Trim()).Append('\n');
        if (p.Titel.Trim().Length > 0) sb.Append(p.Titel.Trim()).Append('\n');
        foreach (var x in new[] { p.Strasse, p.Ort, p.Telefon, p.Email, p.Web }.Select(s => s.Trim()).Where(s => s.Length > 0)) sb.Append(x).Append('\n');
        string R(string von, string bis) => CvDate.Range(von, bis).Replace('.', '/').Replace(" – ", " - ");
        if (p.Profil.Trim().Length > 0) sb.Append("\nPROFIL\n").Append(p.Profil.Trim()).Append('\n');
        if (cv.Stationen.Count > 0)
        {
            sb.Append("\nBERUFSERFAHRUNG\n");
            foreach (var s in Stationen(cv))
            {
                sb.Append(s.Titel.Trim());
                if (s.Firma.Trim().Length > 0) sb.Append(", ").Append(s.Firma.Trim());
                if (s.Ort.Trim().Length > 0) sb.Append(", ").Append(s.Ort.Trim());
                sb.Append('\n').Append(R(s.Von, s.Bis)).Append('\n');
                foreach (var l in Lines(s.Text)) sb.Append("- ").Append(l).Append('\n');
            }
        }
        if (cv.Ausbildung.Count > 0)
        {
            sb.Append("\nAUSBILDUNG\n");
            foreach (var a in Ausbildung(cv))
            {
                sb.Append(a.Abschluss.Trim());
                if (a.Einrichtung.Trim().Length > 0) sb.Append(", ").Append(a.Einrichtung.Trim());
                if (a.Ort.Trim().Length > 0) sb.Append(", ").Append(a.Ort.Trim());
                sb.Append('\n').Append(R(a.Von, a.Bis));
                if (a.Note.Trim().Length > 0) sb.Append(", Note ").Append(a.Note.Trim());
                sb.Append('\n');
                foreach (var l in Lines(a.Text)) sb.Append("- ").Append(l).Append('\n');
            }
        }
        if (cv.Kenntnisse.Count > 0) { sb.Append("\nKENNTNISSE\n"); foreach (var k in cv.Kenntnisse.Where(k => k.Trim().Length > 0)) sb.Append("- ").Append(k.Trim()).Append('\n'); }
        if (cv.Sprachen.Count > 0) { sb.Append("\nSPRACHEN\n"); foreach (var s in cv.Sprachen) sb.Append("- ").Append(s.Name.Trim()).Append(": ").Append(s.Stufe.Trim()).Append(s.Stufe.Trim().Equals("Muttersprache", StringComparison.OrdinalIgnoreCase) ? "" : $" ({CvCheck.StufeText(s.Stufe)})").Append('\n'); }
        if (cv.Interessen.Count > 0) sb.Append("\nINTERESSEN\n").Append(string.Join(", ", cv.Interessen.Select(i => i.Trim()).Where(i => i.Length > 0))).Append('\n');
        return sb.ToString();
    }

    static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
    {
        "und", "oder", "der", "die", "das", "den", "dem", "des", "ein", "eine", "einen", "einer", "eines", "mit", "für", "von", "bei", "auf", "aus", "zum", "zur", "wir", "sie", "ihr", "ihre", "ihren", "ihrem",
        "uns", "unser", "unsere", "unseren", "sich", "sind", "ist", "wird", "werden", "haben", "hast", "hat", "als", "auch", "nach", "über", "unter", "dass", "eine", "sowie", "gerne", "gern", "sehr", "gute", "guten",
        "gutes", "erste", "ersten", "neue", "neuen", "jetzt", "bitte", "deine", "dein", "deinen", "dich", "du", "euch", "wenn", "dann", "noch", "mehr", "alle", "allen", "jede", "jeden", "jeder", "ohne", "durch",
        "m/w/d", "w/m/d", "vollzeit", "teilzeit", "stelle", "suchen", "bieten", "aufgaben", "profil", "bewerbung", "bewerben", "freuen", "einsatz", "bereich", "unternehmen", "team", "zeit", "sowohl",
    };
    [GeneratedRegex(@"[\p{L}][\p{L}\p{N}+#./-]{2,}")] private static partial Regex Word();

    /// <summary>Stichwörter der Anzeige (häufige Fachwörter) – gefunden im Lebenslauf oder fehlend. Hilft, die eigenen Worte an die Anzeige anzupassen.</summary>
    public static (List<string> Gefunden, List<string> Fehlend) Abgleich(string anzeige, Lebenslauf cv, int max = 15)
    {
        var words = Word().Matches(anzeige ?? "").Select(m => m.Value.Trim('.', '/', '-')).Where(w => (w.Length >= 4 || w.All(char.IsUpper)) && !Stop.Contains(w)).ToList();
        var top = words.GroupBy(w => w.ToLowerInvariant()).Select(g => (Word: g.First(), N: g.Count(), Cap: g.Any(x => char.IsUpper(x[0]))))
            .OrderByDescending(x => x.N).ThenByDescending(x => x.Cap).ThenBy(x => words.FindIndex(w => w.Equals(x.Word, StringComparison.OrdinalIgnoreCase))).Take(max).Select(x => x.Word).ToList();
        var text = Ats(cv).ToLowerInvariant();
        return ([.. top.Where(w => text.Contains(w.ToLowerInvariant()))], [.. top.Where(w => !text.Contains(w.ToLowerInvariant()))]);
    }

    /// <summary>Lebenslauf als Trendo-Text-Dokument (für DOCX/ODT über den vorhandenen Weg von „Trendo Text“).</summary>
    public static TextDoc Doc(Lebenslauf cv)
    {
        var d = new TextDoc { Title = "Lebenslauf " + cv.Person.Name.Trim() }; var p = cv.Person;
        d.Blocks.Add(new Para(p.Name.Trim().Length > 0 ? p.Name.Trim() : "Lebenslauf", ParaStyle.Title));
        if (p.Titel.Trim().Length > 0) d.Blocks.Add(new Para(p.Titel.Trim(), ParaStyle.Subtitle));
        d.Blocks.Add(new Para(string.Join(" · ", new[] { p.Strasse, p.Ort, p.Telefon, p.Email, p.Web }.Select(s => s.Trim()).Where(s => s.Length > 0))));
        if (p.Profil.Trim().Length > 0) { d.Blocks.Add(new Para("Profil", ParaStyle.Heading1)); d.Blocks.Add(new Para(p.Profil.Trim())); }
        void Section(string title, IEnumerable<(string Range, string Head, string Sub, string Text)> rows)
        {
            var list = rows.ToList(); if (list.Count == 0) return;
            d.Blocks.Add(new Para(title, ParaStyle.Heading1));
            var t = new Table { Rows = [.. list.Select(r => new List<string> { r.Range, (r.Head + (r.Sub.Length > 0 ? "\n" + r.Sub : "") + (r.Text.Length > 0 ? "\n" + r.Text : "")).Trim() })] };
            d.Blocks.Add(t);
        }
        Section("Berufserfahrung", Stationen(cv).Select(s => (CvDate.Range(s.Von, s.Bis), s.Titel.Trim(), string.Join(", ", new[] { s.Firma, s.Ort }.Select(x => x.Trim()).Where(x => x.Length > 0)), string.Join("\n", Lines(s.Text).Select(l => "• " + l)))));
        Section("Ausbildung", Ausbildung(cv).Select(a => (CvDate.Range(a.Von, a.Bis), a.Abschluss.Trim() + (a.Note.Trim().Length > 0 ? $" (Note {a.Note.Trim()})" : ""), string.Join(", ", new[] { a.Einrichtung, a.Ort }.Select(x => x.Trim()).Where(x => x.Length > 0)), string.Join("\n", Lines(a.Text).Select(l => "• " + l)))));
        if (cv.Kenntnisse.Count > 0) { d.Blocks.Add(new Para("Kenntnisse", ParaStyle.Heading1)); foreach (var k in cv.Kenntnisse.Where(k => k.Trim().Length > 0)) d.Blocks.Add(new Para(k.Trim()) { List = ListKind.Bullet }); }
        if (cv.Sprachen.Count > 0) { d.Blocks.Add(new Para("Sprachen", ParaStyle.Heading1)); foreach (var s in cv.Sprachen) d.Blocks.Add(new Para($"{s.Name.Trim()}: {CvCheck.StufeText(s.Stufe)}" + (s.Stufe.Trim().Equals("Muttersprache", StringComparison.OrdinalIgnoreCase) ? "" : $" ({s.Stufe.Trim()})")) { List = ListKind.Bullet }); }
        if (cv.Interessen.Count > 0) { d.Blocks.Add(new Para("Interessen", ParaStyle.Heading1)); d.Blocks.Add(new Para(string.Join(", ", cv.Interessen.Select(i => i.Trim()).Where(i => i.Length > 0)))); }
        if (cv.Unterschrift.Trim().Length > 0) { d.Blocks.Add(new Para("")); d.Blocks.Add(new Para(cv.Unterschrift.Trim())); }
        return d;
    }
}

/// <summary>Lebenslauf als PDF in drei Layouts (klassisch mit Datumsspalte, modern mit farbiger Seitenleiste, kompakt) – gezeichnet mit PDFsharp (MIT), Maße in mm.</summary>
public static class CvPdf
{
    public static readonly (string Id, string Name, string Hint)[] Layouts =
    [
        ("klassisch", "Klassisch", "Datum links, Inhalt rechts – passt überall"),
        ("modern", "Modern", "Farbige Seitenleiste mit Kontakt, Kenntnissen und Sprachen"),
        ("kompakt", "Kompakt", "Kleinere Schrift, alles auf möglichst einer Seite"),
    ];
    const double Mm = 72 / 25.4;

    sealed class Pen(PdfDocument doc, Lebenslauf cv, double left, double right, double top, double bottom, double size)
    {
        public readonly List<XGraphics> Gs = [];
        public XGraphics G = null!;
        public double Y, Left = left, Right = right, Top = top, Bottom = bottom, Size = size;
        public Action<XGraphics>? OnPage;
        public readonly XColor Accent = Hex(cv.Akzent);
        readonly XGraphics _measure = XGraphics.CreateMeasureContext(new XSize(595, 842), XGraphicsUnit.Point, XPageDirection.Downwards);
        public XFont F(double pt, bool bold = false, bool italic = false) => new("Arial", pt, (bold ? XFontStyleEx.Bold : 0) | (italic ? XFontStyleEx.Italic : 0));
        public void Page()
        {
            var p = doc.AddPage(); p.Width = XUnit.FromMillimeter(210); p.Height = XUnit.FromMillimeter(297);
            G = XGraphics.FromPdfPage(p); Gs.Add(G); OnPage?.Invoke(G); Y = Gs.Count == 1 ? Y : Top;
        }
        public void Need(double mm) { if (Y + mm > Bottom) Page(); }
        public List<string> Wrap(string text, XFont f, double widthMm)
        {
            var res = new List<string>(); var max = widthMm * Mm;
            foreach (var para in (text ?? "").Replace("\r", "").Split('\n'))
            {
                if (para.Length == 0) { res.Add(""); continue; }
                var line = "";
                foreach (var word in para.Split(' '))
                {
                    var test = line.Length == 0 ? word : line + " " + word;
                    if (_measure.MeasureString(test, f).Width <= max || line.Length == 0) { line = test; continue; }
                    res.Add(line); line = word;
                }
                res.Add(line);
            }
            return res;
        }
        public double LineH(double pt) => pt * 0.3528 * 1.3;
        /// <summary>Text umbrechen und schreiben (mit Seitenumbruch), gibt die Höhe in mm zurück.</summary>
        public void Write(string text, double x, double width, double pt, bool bold = false, XColor? color = null, bool italic = false)
        {
            var f = F(pt, bold, italic); var brush = new XSolidBrush(color ?? XColor.FromArgb(0x22, 0x22, 0x22));
            foreach (var l in Wrap(text, f, width))
            {
                Need(LineH(pt));
                G.DrawString(l, f, brush, new XRect(x * Mm, Y * Mm, width * Mm, LineH(pt) * Mm), XStringFormats.TopLeft);
                Y += LineH(pt);
            }
        }
        public double Height(string text, double width, double pt, bool bold = false) => Wrap(text, F(pt, bold), width).Count * LineH(pt);
        public void Heading(string text, double x, double width)
        {
            Need(LineH(Size + 3) + 6); Y += 2.5;
            G.DrawString(text.ToUpperInvariant(), F(Size + 1.5, true), new XSolidBrush(Accent), new XRect(x * Mm, Y * Mm, width * Mm, 8 * Mm), XStringFormats.TopLeft);
            Y += LineH(Size + 1.5) + 0.6;
            G.DrawLine(new XPen(Accent, 0.35 * Mm), x * Mm, Y * Mm, (x + width) * Mm, Y * Mm);
            Y += 2;
        }
    }

    static XColor Hex(string h)
    {
        h = (h ?? "").Trim().TrimStart('#');
        return h.Length == 6 && int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? XColor.FromArgb((v >> 16) & 255, (v >> 8) & 255, v & 255) : XColor.FromArgb(0x1F, 0x5F, 0xA8);
    }

    static IEnumerable<string> Lines(string t) => (t ?? "").Replace("\r", "").Split('\n').Select(l => l.Trim().TrimStart('-', '•', '*').Trim()).Where(l => l.Length > 0);

    /// <summary>Schreibt den Lebenslauf als PDF und gibt die Seitenzahl zurück.</summary>
    public static int Write(Lebenslauf cv, string path)
    {
        Sys.PdfTools.EnsureFonts();
        using var doc = new PdfDocument();
        doc.Info.Title = "Lebenslauf " + cv.Person.Name.Trim(); doc.Info.Author = cv.Person.Name.Trim();
        var layout = Layouts.Any(l => l.Id == cv.Layout) ? cv.Layout : "klassisch";
        var compact = layout == "kompakt"; var size = compact ? 9.0 : 10.0;
        var modern = layout == "modern";
        var pen = modern ? new Pen(doc, cv, 78, 192, 16, 282, size) : new Pen(doc, cv, 20, 190, compact ? 14 : 18, compact ? 284 : 280, size);
        var p = cv.Person; var grey = XColor.FromArgb(0x5C, 0x6B, 0x7A);
        if (modern) pen.OnPage = g => g.DrawRectangle(new XSolidBrush(pen.Accent), 0, 0, 64 * Mm, 297 * Mm);
        pen.Y = pen.Top; pen.Page();

        // Kopf
        double photoW = 0;
        if (p.Foto.Length > 0 && File.Exists(p.Foto))
        {
            try
            {
                using var img = XImage.FromFile(p.Foto);
                var w = 32.0; var h = w * img.PixelHeight / Math.Max(1, img.PixelWidth); h = Math.Min(h, 42);
                if (modern) pen.G.DrawImage(img, 14 * Mm, 14 * Mm, 36 * Mm, Math.Min(46, 36.0 * img.PixelHeight / Math.Max(1, img.PixelWidth)) * Mm);
                else { pen.G.DrawImage(img, (190 - w) * Mm, pen.Top * Mm, w * Mm, h * Mm); photoW = w + 4; }
            }
            catch (Exception e) when (e is IOException or InvalidOperationException or ArgumentException or NotSupportedException) { }
        }
        var headW = pen.Right - pen.Left - photoW;
        pen.Write(p.Name.Trim().Length > 0 ? p.Name.Trim() : "Lebenslauf", pen.Left, headW, compact ? 20 : 24, true, modern ? XColor.FromArgb(0x1A, 0x1A, 0x1A) : pen.Accent);
        if (p.Titel.Trim().Length > 0) pen.Write(p.Titel.Trim(), pen.Left, headW, size + 3, false, grey);
        var contact = new[] { p.Strasse, p.Ort, p.Telefon, p.Email, p.Web }.Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        if (!modern)
        {
            pen.Y += 1; pen.Write(string.Join("  ·  ", contact), pen.Left, headW, size - 0.5, false, grey);
            if (p.Geburtsdatum.Trim().Length > 0) pen.Write("Geboren am " + p.Geburtsdatum.Trim(), pen.Left, headW, size - 0.5, false, grey);
            if (photoW > 0) pen.Y = Math.Max(pen.Y, pen.Top + 44);
        }
        else
        {
            // Seitenleiste
            var y = p.Foto.Length > 0 && File.Exists(p.Foto) ? 66.0 : 18.0; var white = XColors.White; var soft = XColor.FromArgb(230, 255, 255, 255);
            void Side(string title, IEnumerable<string> rows)
            {
                var items = rows.Where(r => r.Trim().Length > 0).ToList(); if (items.Count == 0) return;
                pen.G.DrawString(title.ToUpperInvariant(), pen.F(size + 0.5, true), new XSolidBrush(white), new XRect(12 * Mm, y * Mm, 44 * Mm, 6 * Mm), XStringFormats.TopLeft);
                y += pen.LineH(size + 0.5) + 0.8; pen.G.DrawLine(new XPen(soft, 0.3 * Mm), 12 * Mm, y * Mm, 52 * Mm, y * Mm); y += 2;
                foreach (var r in items)
                    foreach (var l in pen.Wrap(r, pen.F(size - 1), 42))
                    { if (y > 285) return; pen.G.DrawString(l, pen.F(size - 1), new XSolidBrush(soft), new XRect(12 * Mm, y * Mm, 44 * Mm, 5 * Mm), XStringFormats.TopLeft); y += pen.LineH(size - 1); }
                y += 4;
            }
            Side("Kontakt", [.. contact, p.Geburtsdatum.Trim().Length > 0 ? "Geboren am " + p.Geburtsdatum.Trim() : ""]);
            Side("Kenntnisse", cv.Kenntnisse);
            Side("Sprachen", cv.Sprachen.Select(s => $"{s.Name.Trim()} – {CvCheck.StufeText(s.Stufe)}" + (s.Stufe.Trim().Equals("Muttersprache", StringComparison.OrdinalIgnoreCase) ? "" : $" ({s.Stufe.Trim()})")));
            Side("Interessen", cv.Interessen);
        }
        pen.Y += compact ? 2 : 4;
        var width = pen.Right - pen.Left;
        if (p.Profil.Trim().Length > 0) { pen.Heading("Profil", pen.Left, width); pen.Write(p.Profil.Trim(), pen.Left, width, size); }

        var dateCol = modern ? 0.0 : compact ? 30 : 36;
        void Entry(string range, string head, string sub, string text)
        {
            var cx = pen.Left + dateCol; var cw = width - dateCol;
            var h = pen.Height(head, cw, size, true) + (sub.Length > 0 ? pen.Height(sub, cw, size - 0.5) : 0) + (modern ? pen.LineH(size - 1) : 0);
            pen.Need(h + 2);
            var y0 = pen.Y;
            if (modern) pen.Write(range, cx, cw, size - 1, false, grey);
            else pen.G.DrawString(range, pen.F(size - 0.5), new XSolidBrush(grey), new XRect(pen.Left * Mm, y0 * Mm, dateCol * Mm, 5 * Mm), XStringFormats.TopLeft);
            pen.Write(head, cx, cw, size, true);
            if (sub.Length > 0) pen.Write(sub, cx, cw, size - 0.5, false, grey, true);
            foreach (var l in Lines(text))
            {
                var yy = pen.Y; pen.Write(l, cx + 3.5, cw - 3.5, size - 0.5);
                pen.G.DrawString("•", pen.F(size - 0.5), new XSolidBrush(pen.Accent), new XRect(cx * Mm, yy * Mm, 3 * Mm, 5 * Mm), XStringFormats.TopLeft);
            }
            pen.Y += compact ? 1.5 : 2.5;
        }
        if (cv.Stationen.Count > 0)
        {
            pen.Heading("Berufserfahrung", pen.Left, width);
            foreach (var s in cv.Stationen.OrderByDescending(s => CvDate.SortKey(s.Von, s.Bis)))
                Entry(CvDate.Range(s.Von, s.Bis), s.Titel.Trim(), string.Join(", ", new[] { s.Firma, s.Ort }.Select(x => x.Trim()).Where(x => x.Length > 0)), s.Text);
        }
        if (cv.Ausbildung.Count > 0)
        {
            pen.Heading("Ausbildung", pen.Left, width);
            foreach (var a in cv.Ausbildung.OrderByDescending(a => CvDate.SortKey(a.Von, a.Bis)))
                Entry(CvDate.Range(a.Von, a.Bis), a.Abschluss.Trim() + (a.Note.Trim().Length > 0 ? $" (Note {a.Note.Trim()})" : ""), string.Join(", ", new[] { a.Einrichtung, a.Ort }.Select(x => x.Trim()).Where(x => x.Length > 0)), a.Text);
        }
        if (!modern)
        {
            if (cv.Kenntnisse.Count > 0) { pen.Heading("Kenntnisse", pen.Left, width); pen.Write(string.Join("  ·  ", cv.Kenntnisse.Select(k => k.Trim()).Where(k => k.Length > 0)), pen.Left, width, size); }
            if (cv.Sprachen.Count > 0)
            {
                pen.Heading("Sprachen", pen.Left, width);
                foreach (var s in cv.Sprachen) Entry(s.Name.Trim(), CvCheck.StufeText(s.Stufe) + (s.Stufe.Trim().Equals("Muttersprache", StringComparison.OrdinalIgnoreCase) ? "" : $" ({s.Stufe.Trim()})"), "", "");
            }
            if (cv.Interessen.Count > 0) { pen.Heading("Interessen", pen.Left, width); pen.Write(string.Join(", ", cv.Interessen.Select(i => i.Trim()).Where(i => i.Length > 0)), pen.Left, width, size); }
        }
        if (cv.Unterschrift.Trim().Length > 0) { pen.Y += 8; pen.Need(12); pen.Write(cv.Unterschrift.Trim(), pen.Left, width, size); }
        // Seitenzahlen ab zwei Seiten
        if (pen.Gs.Count > 1)
            for (var i = 0; i < pen.Gs.Count; i++)
                pen.Gs[i].DrawString($"Seite {i + 1} von {pen.Gs.Count}", pen.F(8), new XSolidBrush(grey), new XRect(0, 289 * Mm, 200 * Mm, 5 * Mm), XStringFormats.TopRight);
        var n = doc.PageCount;
        foreach (var g in pen.Gs) g.Dispose();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        doc.Save(path);
        return n;
    }
}

/// <summary>Anschreiben: Anweisung für die KI (vorhandener KI-Kern) und ein eigener Entwurf aus Stelle und Profil, wenn keine KI da ist. Nichts wird erfunden – fehlende Angaben bleiben als [Klammer] stehen.</summary>
public static class Anschreiben
{
    public static string Anrede(CvStelle s)
    {
        var a = s.Ansprechpartner.Trim();
        if (a.StartsWith("Frau ", StringComparison.OrdinalIgnoreCase)) return $"Sehr geehrte {a},";
        if (a.StartsWith("Herr ", StringComparison.OrdinalIgnoreCase)) return $"Sehr geehrter {a},";
        return a.Length > 0 ? $"Guten Tag {a}," : "Sehr geehrte Damen und Herren,";
    }

    public static string Betreff(CvStelle s) => $"Bewerbung als {(s.Position.Trim().Length > 0 ? s.Position.Trim() : "[Stelle]")}";

    /// <summary>Auftrag an die KI: nur Fakten aus Lebenslauf und Anzeige, keine erfundenen Stationen, Länge höchstens eine Seite.</summary>
    public static string Prompt(Lebenslauf cv, CvStelle s) =>
        "Schreibe ein Bewerbungsanschreiben auf Deutsch (nur den Brieftext ab der Anrede bis vor den Gruß, höchstens 250 Wörter, drei bis vier Absätze, keine Floskeln wie „hiermit bewerbe ich mich“). " +
        "Nutze ausschließlich die Angaben aus Lebenslauf und Stellenanzeige – erfinde keine Stationen, Abschlüsse oder Zahlen. Fehlt etwas, setze einen Platzhalter in [eckigen Klammern].\n\n" +
        $"Stelle: {s.Position}\nFirma: {s.Firma}\nAnsprechperson: {s.Ansprechpartner}\nEintritt: {s.Eintritt}\n\nStellenanzeige:\n{s.Anzeige.Trim()}\n\nLebenslauf:\n{CvText.Ats(cv)}";

    /// <summary>Eigener Entwurf ohne KI: Einstieg mit Stelle und Firma, aktuelle Station, passende Kenntnisse aus dem Abgleich mit der Anzeige, Ausbildung, Schluss mit Eintritt.</summary>
    public static string Entwurf(Lebenslauf cv, CvStelle s)
    {
        var pos = s.Position.Trim().Length > 0 ? s.Position.Trim() : "[Stelle]"; var firma = s.Firma.Trim().Length > 0 ? s.Firma.Trim() : "[Firma]";
        var sb = new StringBuilder();
        sb.Append(Anrede(s)).Append("\n\n");
        sb.Append($"Ihre Stellenanzeige als {pos} hat mich sofort angesprochen, denn genau in diesem Bereich möchte ich mich bei {firma} einbringen.");
        if (cv.Person.Profil.Trim().Length > 0) sb.Append(' ').Append(FirstSentence(cv.Person.Profil.Trim()));
        sb.Append("\n\n");
        var cur = cv.Stationen.OrderByDescending(x => CvDate.SortKey(x.Von, x.Bis)).FirstOrDefault();
        if (cur is not null)
        {
            var since = CvDate.Parse(cur.Von) is { } m ? CvDate.Text(m) : cur.Von.Trim();
            sb.Append(CvDate.IsOpen(cur.Bis) ? $"Seit {since} arbeite ich als {cur.Titel.Trim()}" : $"Zuletzt war ich als {cur.Titel.Trim()}");
            if (cur.Firma.Trim().Length > 0) sb.Append(" bei ").Append(cur.Firma.Trim());
            sb.Append(CvDate.IsOpen(cur.Bis) ? "." : " tätig.");
            var tasks = cur.Text.Replace("\r", "").Split('\n').Select(l => l.Trim().TrimStart('-', '•', '*').Trim()).Where(l => l.Length > 0).Take(2).ToList();
            if (tasks.Count > 0) sb.Append(" Dort ").Append(tasks.Count == 1 ? "gehört dazu: " : "gehören dazu: ").Append(string.Join(" und ", tasks.Select(t => t.TrimEnd('.')))).Append('.');
            sb.Append("\n\n");
        }
        var (found, _) = CvText.Abgleich(s.Anzeige, cv, 12);
        var skills = cv.Kenntnisse.Where(k => found.Any(f => k.Contains(f, StringComparison.OrdinalIgnoreCase))).Concat(cv.Kenntnisse).Distinct().Take(4).ToList();
        if (skills.Count > 0) sb.Append($"Mitbringen kann ich unter anderem {Join(skills)}.");
        var edu = cv.Ausbildung.OrderByDescending(a => CvDate.SortKey(a.Von, a.Bis)).FirstOrDefault();
        if (edu is not null) sb.Append($" Meine Grundlage ist {(edu.Abschluss.Trim().Length > 0 ? "der Abschluss „" + edu.Abschluss.Trim() + "“" : "meine Ausbildung")}{(edu.Einrichtung.Trim().Length > 0 ? " an der Einrichtung " + edu.Einrichtung.Trim() : "")}.");
        var langs = cv.Sprachen.Where(l => !l.Name.Trim().Equals("Deutsch", StringComparison.OrdinalIgnoreCase)).Select(l => $"{l.Name.Trim()} ({l.Stufe.Trim()})").ToList();
        if (langs.Count > 0) sb.Append($" Außerdem spreche ich {Join(langs)}.");
        if (skills.Count > 0 || edu is not null || langs.Count > 0) sb.Append("\n\n");
        sb.Append(s.Eintritt.Trim().Length > 0 ? $"Ich kann ab {s.Eintritt.Trim()} bei Ihnen beginnen. " : "Ich kann [ab wann?] bei Ihnen beginnen. ");
        sb.Append("Über eine Einladung zu einem persönlichen Gespräch freue ich mich.");
        return sb.ToString();
    }
    static string FirstSentence(string t) { var i = t.IndexOfAny(['.', '!', '?']); return i > 0 ? t[..(i + 1)] : t + "."; }
    static string Join(List<string> l) => l.Count switch { 0 => "", 1 => l[0], _ => string.Join(", ", l.Take(l.Count - 1)) + " und " + l[^1] };

    /// <summary>Text der KI aufräumen: Anrede und Gruß kommen vom Brief, nicht doppelt.</summary>
    public static string Clean(string ai, CvStelle s)
    {
        var lines = (ai ?? "").Replace("\r", "").Trim().Split('\n').ToList();
        // Gruß (und der Name darunter) in den letzten drei Zeilen fällt weg – den setzt der Brief selbst
        string[] greet = ["Mit freundlichen", "Freundliche Grüße", "Viele Grüße", "Beste Grüße", "Herzliche Grüße"];
        var g = lines.FindLastIndex(l => greet.Any(x => l.Trim().StartsWith(x, StringComparison.OrdinalIgnoreCase)));
        if (g >= 0 && g >= lines.Count - 3) lines = lines[..g];
        while (lines.Count > 0 && lines[^1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);
        var t = string.Join("\n", lines).Trim();
        return t.StartsWith("Sehr geehrt", StringComparison.OrdinalIgnoreCase) || t.StartsWith("Guten Tag", StringComparison.OrdinalIgnoreCase) || t.StartsWith("Hallo", StringComparison.OrdinalIgnoreCase) ? t : Anrede(s) + "\n\n" + t;
    }

    /// <summary>Anschreiben als Brief nach DIN 5008 (vorhandener Brief-Kern): Absender aus dem Lebenslauf, Empfänger aus der Stelle. Die erste Zeile (Anrede) wird zur Anrede des Briefs.</summary>
    public static LetterDraft AlsBrief(Lebenslauf cv, CvStelle s, string text, DateTime date)
    {
        var p = cv.Person; var body = (text ?? "").Replace("\r", "").Trim();
        var sal = Anrede(s);
        var first = body.Split('\n')[0].Trim();
        if (first.EndsWith(',') && (first.StartsWith("Sehr geehrt", StringComparison.OrdinalIgnoreCase) || first.StartsWith("Guten Tag", StringComparison.OrdinalIgnoreCase) || first.StartsWith("Hallo", StringComparison.OrdinalIgnoreCase)))
        { sal = first; body = body[first.Length..].TrimStart('\n', ' '); }
        var recipient = string.Join("\n", new[] { s.Firma.Trim(), s.Ansprechpartner.Trim(), s.Anschrift.Trim() }.Where(x => x.Length > 0));
        return new LetterDraft
        {
            Category = "bewerbung", SenderName = p.Name.Trim(), SenderStreet = p.Strasse.Trim(), SenderCity = p.Ort.Trim(),
            SenderContact = string.Join(" · ", new[] { p.Telefon, p.Email }.Select(x => x.Trim()).Where(x => x.Length > 0)),
            Recipient = recipient, Date = date, Subject = Betreff(s), Salutation = sal, Body = body, Closing = "Mit freundlichen Grüßen",
            Place = p.Ort.Trim().Split(' ').LastOrDefault() ?? "", Attachments = "Lebenslauf", Design = "klassisch", Accent = cv.Akzent,
        };
    }
}

/// <summary>Bewerbungsmappe: Deckblatt (freiwillig), Anschreiben, Lebenslauf und Anlagen als eine PDF. Bilder werden als eigene Seite eingefügt.</summary>
public static class Mappe
{
    const double Mm = 72 / 25.4;
    public static readonly string[] AnlageEndungen = [".pdf", ".png", ".jpg", ".jpeg"];

    /// <summary>Deckblatt: „Bewerbung als …“, Firma, Name, Kontakt, Inhalt.</summary>
    public static void Deckblatt(Lebenslauf cv, CvStelle s, IReadOnlyList<string> inhalt, string path)
    {
        Sys.PdfTools.EnsureFonts();
        using var doc = new PdfDocument(); var page = doc.AddPage(); page.Width = XUnit.FromMillimeter(210); page.Height = XUnit.FromMillimeter(297);
        using (var g = XGraphics.FromPdfPage(page))
        {
            var acc = cv.Akzent.Trim().TrimStart('#') is { Length: 6 } h && int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? XColor.FromArgb((v >> 16) & 255, (v >> 8) & 255, v & 255) : XColor.FromArgb(0x1F, 0x5F, 0xA8);
            var dark = new XSolidBrush(XColor.FromArgb(0x22, 0x22, 0x22)); var grey = new XSolidBrush(XColor.FromArgb(0x5C, 0x6B, 0x7A));
            g.DrawRectangle(new XSolidBrush(acc), 0, 0, 210 * Mm, 12 * Mm);
            void T(string t, double pt, bool bold, XBrush b, double y) => g.DrawString(t, new XFont("Arial", pt, bold ? XFontStyleEx.Bold : XFontStyleEx.Regular), b, new XRect(25 * Mm, y * Mm, 160 * Mm, 14 * Mm), XStringFormats.TopLeft);
            T("Bewerbung", 30, true, new XSolidBrush(acc), 70);
            T("als " + (s.Position.Trim().Length > 0 ? s.Position.Trim() : "[Stelle]"), 18, false, dark, 86);
            if (s.Firma.Trim().Length > 0) T("bei " + s.Firma.Trim(), 14, false, grey, 97);
            if (cv.Person.Foto.Length > 0 && File.Exists(cv.Person.Foto))
                try { using var img = XImage.FromFile(cv.Person.Foto); var w = 50.0; var hh = Math.Min(65, w * img.PixelHeight / Math.Max(1, img.PixelWidth)); g.DrawImage(img, 25 * Mm, 118 * Mm, w * Mm, hh * Mm); }
                catch (Exception e) when (e is IOException or InvalidOperationException or ArgumentException or NotSupportedException) { }
            var y = 200.0;
            T(cv.Person.Name.Trim(), 14, true, dark, y); y += 7;
            foreach (var l in new[] { cv.Person.Strasse, cv.Person.Ort, cv.Person.Telefon, cv.Person.Email }.Select(x => x.Trim()).Where(x => x.Length > 0)) { T(l, 10.5, false, grey, y); y += 5.2; }
            y += 6; T("Inhalt", 11, true, dark, y); y += 6;
            foreach (var l in inhalt) { T("– " + l, 10.5, false, grey, y); y += 5.2; }
        }
        doc.Save(path);
    }

    /// <summary>Bild als eigene A4-Seite (eingepasst, Rand 15 mm).</summary>
    public static void BildSeite(string image, string path)
    {
        using var doc = new PdfDocument(); var page = doc.AddPage(); page.Width = XUnit.FromMillimeter(210); page.Height = XUnit.FromMillimeter(297);
        using (var g = XGraphics.FromPdfPage(page))
        {
            using var img = XImage.FromFile(image);
            var s = Math.Min(180.0 / img.PixelWidth, 267.0 / img.PixelHeight); var w = img.PixelWidth * s; var h = img.PixelHeight * s;
            g.DrawImage(img, (210 - w) / 2 * Mm, (297 - h) / 2 * Mm, w * Mm, h * Mm);
        }
        doc.Save(path);
    }

    /// <summary>Mappe zusammenstellen. <paramref name="anschreibenPdf"/> und <paramref name="lebenslaufPdf"/> sind fertige PDFs; Anlagen PDF oder Bild. Gibt die Seitenzahl zurück.</summary>
    public static int Erstellen(BewerbungsAkte a, string anschreibenPdf, string lebenslaufPdf, string output, string workDir)
    {
        Directory.CreateDirectory(workDir);
        var anlagen = a.Anlagen.Where(f => f.Trim().Length > 0).ToList();
        foreach (var f in anlagen)
        {
            if (!File.Exists(f)) throw new FileNotFoundException($"Die Anlage „{Path.GetFileName(f)}“ gibt es nicht mehr – bitte neu hinzufügen oder entfernen.", f);
            if (!AnlageEndungen.Contains(Path.GetExtension(f).ToLowerInvariant())) throw new InvalidDataException($"„{Path.GetFileName(f)}“: Anlagen bitte als PDF, PNG oder JPG.");
        }
        var parts = new List<string>();
        var inhalt = new List<string> { "Anschreiben", "Lebenslauf" };
        inhalt.AddRange(anlagen.Select(f => Path.GetFileNameWithoutExtension(f)));
        if (a.Deckblatt) { var cover = Path.Combine(workDir, "00-deckblatt.pdf"); Deckblatt(a.Cv, a.Stelle, inhalt, cover); parts.Add(cover); }
        parts.Add(anschreibenPdf); parts.Add(lebenslaufPdf);
        var i = 0;
        foreach (var f in anlagen)
        {
            i++;
            if (Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase)) parts.Add(f);
            else { var pg = Path.Combine(workDir, $"anlage-{i:00}.pdf"); BildSeite(f, pg); parts.Add(pg); }
        }
        return Sys.PdfTools.Merge(parts, output);
    }
}

/// <summary>Übersicht der Bewerbungen: Status, Fristen, Zählung, CSV.</summary>
public static class BewerbungsListe
{
    public static readonly (string Id, string Name)[] Status =
    [
        ("idee", "Vorgemerkt"), ("versendet", "Versendet"), ("gespraech", "Gespräch"), ("zusage", "Zusage"), ("absage", "Absage"), ("zurueckgezogen", "Zurückgezogen"),
    ];
    public static string StatusName(string id) => Status.FirstOrDefault(s => s.Id == id).Name ?? id;
    public static bool Offen(BewerbungEintrag e) => e.Status is "idee" or "versendet" or "gespraech";
    static DateTime? D(string s) => DateTime.TryParseExact((s ?? "").Trim(), ["yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy"], CultureInfo.GetCultureInfo("de-DE"), DateTimeStyles.None, out var d) ? d.Date : null;
    public static DateTime? Frist(BewerbungEintrag e) => D(e.Frist);

    /// <summary>Offene Bewerbungen mit Frist in den nächsten <paramref name="tage"/> Tagen (oder schon vorbei), die nächste zuerst.</summary>
    public static List<(BewerbungEintrag E, int Tage)> Faellig(IEnumerable<BewerbungEintrag> list, DateTime today, int tage = 7) =>
        [.. list.Where(Offen).Select(e => (E: e, F: Frist(e))).Where(x => x.F is { } f && (f - today.Date).TotalDays <= tage).Select(x => (x.E, (int)(x.F!.Value - today.Date).TotalDays)).OrderBy(x => x.Item2)];

    /// <summary>Nachfassen: versendet und seit mindestens <paramref name="tage"/> Tagen ohne Antwort.</summary>
    public static List<BewerbungEintrag> Nachfassen(IEnumerable<BewerbungEintrag> list, DateTime today, int tage = 14) =>
        [.. list.Where(e => e.Status == "versendet" && D(e.Beworben) is { } b && (today.Date - b).TotalDays >= tage)];

    public static Dictionary<string, int> Zaehlen(IEnumerable<BewerbungEintrag> list) => Status.ToDictionary(s => s.Id, s => list.Count(e => e.Status == s.Id));

    public static string Csv(IEnumerable<BewerbungEintrag> list)
    {
        static string Q(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder("Firma;Stelle;Status;Beworben am;Frist;Notiz\r\n");
        foreach (var e in list) sb.Append(string.Join(";", Q(e.Firma), Q(e.Stelle), Q(StatusName(e.Status)), Q(e.Beworben), Q(e.Frist), Q(e.Notiz))).Append("\r\n");
        return sb.ToString();
    }
}

/// <summary>Beispiel mit neutralen Daten (für Vorlagen und Tests).</summary>
public static class CvBeispiel
{
    public static BewerbungsAkte Akte() => new()
    {
        Cv = new Lebenslauf
        {
            Person = new() { Name = "Erika Muster", Titel = "Kauffrau für Büromanagement", Email = "erika.muster@example.org", Telefon = "0171 2345678", Strasse = "Musterweg 12", Ort = "12345 Musterstadt",
                Profil = "Organisierte Bürokauffrau mit fünf Jahren Erfahrung in Buchhaltung und Kundenservice. Ich arbeite gern strukturiert und behalte auch bei vielen Terminen den Überblick." },
            Stationen =
            [
                new() { Id = "s1", Titel = "Sachbearbeiterin Buchhaltung", Firma = "Beispiel GmbH", Ort = "Musterstadt", Von = "01.2021", Bis = "", Text = "Rechnungen prüfen und buchen\nMahnwesen und Zahlungsverkehr\nMonatsabschluss vorbereiten" },
                new() { Id = "s2", Titel = "Kauffrau für Büromanagement", Firma = "Muster & Co. KG", Ort = "Musterstadt", Von = "08.2019", Bis = "12.2020", Text = "Kundenanfragen per Telefon und E-Mail\nAngebote und Auftragsbestätigungen" },
            ],
            Ausbildung =
            [
                new() { Id = "a1", Abschluss = "Ausbildung Kauffrau für Büromanagement", Einrichtung = "Muster & Co. KG / Berufsschule Musterstadt", Von = "08.2016", Bis = "07.2019", Note = "2,1" },
                new() { Id = "a2", Abschluss = "Mittlere Reife", Einrichtung = "Realschule am Park", Von = "09.2010", Bis = "07.2016" },
            ],
            Kenntnisse = ["DATEV", "Microsoft Excel", "Buchhaltung", "Mahnwesen", "Führerschein Klasse B"],
            Sprachen = [new() { Name = "Deutsch", Stufe = "Muttersprache" }, new() { Name = "Englisch", Stufe = "B2" }],
            Interessen = ["Laufen", "Kochen"],
        },
        Stelle = new() { Firma = "Musterwerke AG", Position = "Sachbearbeiterin Finanzbuchhaltung (m/w/d)", Ansprechpartner = "Frau Beispiel", Anschrift = "Industriestraße 5\n54321 Beispielhausen",
            Anzeige = "Wir suchen eine Sachbearbeiterin Finanzbuchhaltung (m/w/d). Ihre Aufgaben: Kreditorenbuchhaltung, Zahlungsverkehr, Mahnwesen und Unterstützung beim Monatsabschluss. Ihr Profil: abgeschlossene kaufmännische Ausbildung, sicherer Umgang mit DATEV und Excel, gute Englischkenntnisse.",
            Eintritt = "01.02.2027" },
    };
}
