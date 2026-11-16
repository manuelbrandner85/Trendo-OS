using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core.Business;

// ===================== STUDIO-REVIEWS-01 Kundenmeinungen (T-0677) =====================
// Clean-Room nach dem Bedienkonzept von Bewertungs-Werkzeugen für Firmen (Trustpilot Business, Antworten im Unternehmensprofil, Judge.me):
// echte Bewertungen erfassen oder einlesen, Sterne-Statistik und Verlauf, Themen und Stimmung auswerten, Antwort-Entwürfe, Zitat-Grafiken,
// statisches Bewertungs-Widget für die eigene Seite. Trendo erzeugt keine Bewertungen und sendet keine Antworten.

public sealed class Kundenmeinung
{
    public string Id { get; set; } = "";
    public int Sterne { get; set; } = 5;
    public string Titel { get; set; } = "";
    public string Text { get; set; } = "";
    /// <summary>Anzeigename, wie auf der Plattform (am besten gekürzt, z. B. „Erika M.“).</summary>
    public string Autor { get; set; } = "";
    /// <summary>JJJJ-MM-TT.</summary>
    public string Datum { get; set; } = "";
    public string Quelle { get; set; } = "Eigene Seite";
    public bool Verifiziert { get; set; }
    public string Antwort { get; set; } = "";
    /// <summary>offen, entwurf, beantwortet (die Person hat die Antwort selbst veröffentlicht).</summary>
    public string Status { get; set; } = "offen";
    /// <summary>Die Person ist einverstanden, dass ihr Zitat auf Webseite/Social Media gezeigt wird.</summary>
    public bool ZitatErlaubt { get; set; }
}

public sealed class MeinungsSammlung
{
    public string Firma { get; set; } = "";
    public List<Kundenmeinung> Meinungen { get; set; } = [];
    /// <summary>Pflichtangabe für das Widget: ob und wie die Bewertungen geprüft werden (§ 5b Abs. 3 UWG).</summary>
    public string Pruefhinweis { get; set; } = "Wir zeigen alle Bewertungen ungefiltert. Geprüft wird nur, ob die Person bei uns gekauft hat (Kennzeichen „verifiziert“).";
    public string Ton { get; set; } = "freundlich";
    static readonly JsonSerializerOptions O = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public JsonObject ToJson() => JsonSerializer.SerializeToNode(this, O)!.AsObject();
    public static MeinungsSammlung FromJson(JsonNode? n)
    {
        if (n is not JsonObject o) return new();
        try { return o.Deserialize<MeinungsSammlung>(O) ?? new(); } catch (JsonException) { return new(); }
    }
}

public sealed record SterneStatistik(int Anzahl, double Schnitt, int[] Verteilung, double AnteilPositiv, int Offen);
public sealed record MonatsWert(string Monat, int Anzahl, double Schnitt);
public sealed record Stimmung(double Wert, string Art, IReadOnlyList<string> Plus, IReadOnlyList<string> Minus, IReadOnlyList<string> Themen, bool Widerspruch);
public sealed record ThemenWert(string Thema, int Nennungen, int Positiv, int Negativ, double Schnitt);
public sealed record ZitatLayout(int Breite, int Hoehe, IReadOnlyList<string> Zeilen, double Schrift, string Sterne, string Unterzeile, string Farbe, string Grund);

public static partial class Kundenmeinungen
{
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    public static readonly string[] Quellen = ["Eigene Seite", "Google", "Trustpilot", "Amazon", "Etsy", "eBay", "Shopify", "E-Mail", "Sonstige"];
    public static readonly (string Id, string Name)[] Toene = [("freundlich", "Freundlich"), ("herzlich", "Herzlich"), ("sachlich", "Sachlich")];

    // ---------- Einlesen ----------

    [GeneratedRegex(@"^\s*(?:(?<stars>[★⭐]{1,5})[☆✩]*|(?<n>[1-5])(?:[.,]\d)?\s*(?:/\s*5|von\s*5|sterne?n?|★)?)\s*[|:–—-]?\s*", RegexOptions.IgnoreCase)]
    private static partial Regex StarsRx();

    /// <summary>Sterne aus „4“, „4,0“, „4/5“, „4 Sterne“, „★★★★☆“; null = nicht erkannt.</summary>
    public static int? SterneAus(string? s)
    {
        var t = (s ?? "").Trim(); if (t.Length == 0) return null;
        var full = t.Count(c => c is '★' or '⭐'); if (full is >= 1 and <= 5 && t.All(c => c is '★' or '⭐' or '☆' or '✩' or ' ')) return full;
        var m = Regex.Match(t, @"^([0-5])(?:[.,](\d))?\s*(?:/\s*5|von\s*5|sterne?n?)?$", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var v = double.Parse(m.Groups[1].Value + (m.Groups[2].Success ? "." + m.Groups[2].Value : ""), CultureInfo.InvariantCulture);
        return Math.Clamp((int)Math.Round(v, MidpointRounding.AwayFromZero), 1, 5);
    }

    public static string? DatumAus(string? s)
    {
        var t = (s ?? "").Trim(); if (t.Length >= 10 && t[4] == '-' && DateTime.TryParseExact(t[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso)) return iso.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return DateTime.TryParseExact(t, ["dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "MM/dd/yyyy", "yyyy/MM/dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
    }

    /// <summary>CSV einer Plattform einlesen: Trennzeichen (; , Tab) und Spalten werden erkannt (Sterne/rating, Text/review, Titel, Name, Datum, Quelle).</summary>
    public static (List<Kundenmeinung> Neu, List<string> Hinweise) CsvLesen(string csv, string quelle = "Sonstige")
    {
        var lines = Zeilen(csv); var hinweise = new List<string>(); var neu = new List<Kundenmeinung>();
        if (lines.Count < 2) { hinweise.Add("Die Datei hat keine Datenzeilen."); return (neu, hinweise); }
        var head = lines[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Col(params string[] names) => head.FindIndex(h => names.Any(n => h == n || h.StartsWith(n, StringComparison.Ordinal)));
        int cs = Col("sterne", "rating", "stars", "bewertung", "score"), ct = Col("text", "review", "kommentar", "body", "inhalt", "meinung"), cti = Col("titel", "title", "überschrift"),
            ca = Col("name", "autor", "author", "kunde", "reviewer"), cd = Col("datum", "date", "erstellt", "created"), cq = Col("quelle", "source", "plattform", "platform"),
            cv = Col("verifiziert", "verified");
        if (cs < 0 || ct < 0) { hinweise.Add("Spalten für Sterne und Text nicht gefunden – erste Zeile braucht z. B. „Sterne;Text;Name;Datum“."); return (neu, hinweise); }
        for (var r = 1; r < lines.Count; r++)
        {
            var c = lines[r]; string Get(int i) => i >= 0 && i < c.Count ? c[i].Trim() : "";
            if (c.All(x => x.Trim().Length == 0)) continue;
            var st = SterneAus(Get(cs));
            if (st is null) { hinweise.Add($"Zeile {r + 1}: Sterne „{Get(cs)}“ nicht erkannt – übersprungen."); continue; }
            neu.Add(new Kundenmeinung
            {
                Id = "m" + Guid.NewGuid().ToString("N")[..8], Sterne = st.Value, Text = Get(ct), Titel = Get(cti), Autor = Kuerzen(Get(ca)),
                Datum = DatumAus(Get(cd)) ?? "", Quelle = Get(cq).Length > 0 ? Get(cq) : quelle, Verifiziert = Get(cv).ToLowerInvariant() is "ja" or "yes" or "true" or "1" or "x",
            });
        }
        return (neu, hinweise);
    }

    /// <summary>Kopierten Text einlesen: je Bewertung ein Absatz (Leerzeile dazwischen); Sterne am Anfang („5 | …“, „★★★★☆ …“, „4/5 …“),
    /// optional Zeilen „Name: …“ und „Datum: …“.</summary>
    public static (List<Kundenmeinung> Neu, List<string> Hinweise) TextLesen(string text, string quelle = "Sonstige")
    {
        var neu = new List<Kundenmeinung>(); var hinweise = new List<string>(); var nr = 0;
        foreach (var block in Regex.Split((text ?? "").Replace("\r", ""), @"\n\s*\n"))
        {
            var b = block.Trim(); if (b.Length == 0) continue; nr++;
            var m = StarsRx().Match(b);
            int? st = null;
            if (m.Success && m.Length > 0) st = m.Groups["stars"].Success ? m.Groups["stars"].Value.Length : int.TryParse(m.Groups["n"].Value, out var n) ? n : null;
            if (st is null) { hinweise.Add($"Absatz {nr}: keine Sterne am Anfang gefunden – übersprungen."); continue; }
            var rest = b[m.Length..]; string autor = "", datum = "";
            var lines = rest.Split('\n').ToList();
            foreach (var l in lines.ToList())
            {
                var lm = Regex.Match(l.Trim(), @"^(name|autor|von|datum|date)\s*:\s*(.+)$", RegexOptions.IgnoreCase);
                if (!lm.Success) continue;
                if (lm.Groups[1].Value.ToLowerInvariant() is "datum" or "date") datum = DatumAus(lm.Groups[2].Value) ?? ""; else autor = Kuerzen(lm.Groups[2].Value);
                lines.Remove(l);
            }
            var body = string.Join("\n", lines).Trim();
            if (body.Length == 0) { hinweise.Add($"Absatz {nr}: nur Sterne, kein Text."); }
            neu.Add(new Kundenmeinung { Id = "m" + Guid.NewGuid().ToString("N")[..8], Sterne = Math.Clamp(st.Value, 1, 5), Text = body, Autor = autor, Datum = datum, Quelle = quelle });
        }
        return (neu, hinweise);
    }

    /// <summary>Namen für die Anzeige kürzen (Datensparsamkeit): „Erika Muster“ → „Erika M.“.</summary>
    public static string Kuerzen(string name)
    {
        var parts = (name ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1) return parts.FirstOrDefault() ?? "";
        return parts[0] + " " + char.ToUpperInvariant(parts[^1].TrimEnd('.')[0]) + ".";
    }

    /// <summary>CSV-Zeilen mit Anführungszeichen und erkanntem Trennzeichen.</summary>
    public static List<List<string>> Zeilen(string csv)
    {
        var text = (csv ?? "").TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n');
        var first = text.Split('\n').FirstOrDefault() ?? "";
        var sep = new[] { ';', '\t', ',' }.OrderByDescending(c => first.Count(x => x == c)).First();
        var rows = new List<List<string>>(); var row = new List<string>(); var cell = new StringBuilder(); var inQ = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQ) { if (ch == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; } else inQ = false; } else cell.Append(ch); continue; }
            if (ch == '"') inQ = true;
            else if (ch == sep) { row.Add(cell.ToString()); cell.Clear(); }
            else if (ch == '\n') { row.Add(cell.ToString()); cell.Clear(); rows.Add(row); row = []; }
            else cell.Append(ch);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        return rows;
    }

    /// <summary>Doppelte (gleicher Text und Name) – beim Einlesen werden sie nicht noch einmal aufgenommen.</summary>
    public static string Schluessel(Kundenmeinung m) => Regex.Replace((m.Text + "|" + m.Autor).ToLowerInvariant(), @"\s+", " ").Trim();

    public static int Uebernehmen(MeinungsSammlung s, IEnumerable<Kundenmeinung> neu)
    {
        var have = s.Meinungen.Select(Schluessel).ToHashSet(); var n = 0;
        foreach (var m in neu) if (have.Add(Schluessel(m))) { s.Meinungen.Add(m); n++; }
        return n;
    }

    // ---------- Statistik ----------

    public static SterneStatistik Statistik(IReadOnlyList<Kundenmeinung> l)
    {
        var dist = new int[5]; foreach (var m in l) dist[Math.Clamp(m.Sterne, 1, 5) - 1]++;
        var avg = l.Count == 0 ? 0 : Math.Round(l.Average(m => (double)Math.Clamp(m.Sterne, 1, 5)), 2);
        return new(l.Count, avg, dist, l.Count == 0 ? 0 : l.Count(m => m.Sterne >= 4) / (double)l.Count, l.Count(m => m.Status != "beantwortet"));
    }

    /// <summary>Verlauf je Monat (nur Bewertungen mit Datum), ältester zuerst.</summary>
    public static List<MonatsWert> Verlauf(IEnumerable<Kundenmeinung> l) =>
        l.Where(m => m.Datum.Length >= 7).GroupBy(m => m.Datum[..7]).OrderBy(g => g.Key, StringComparer.Ordinal)
         .Select(g => new MonatsWert(g.Key, g.Count(), Math.Round(g.Average(m => (double)m.Sterne), 2))).ToList();

    /// <summary>Schnitt der letzten 90 Tage gegen davor (null, wenn eine Seite leer ist).</summary>
    public static (double Neu, double Vorher)? Trend(IEnumerable<Kundenmeinung> l, DateTime heute)
    {
        var dated = l.Select(m => (m, d: DatumAus(m.Datum) is { } s ? DateTime.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture) : (DateTime?)null)).Where(x => x.d is not null).ToList();
        var neu = dated.Where(x => x.d >= heute.AddDays(-90)).ToList(); var alt = dated.Where(x => x.d < heute.AddDays(-90)).ToList();
        if (neu.Count == 0 || alt.Count == 0) return null;
        return (Math.Round(neu.Average(x => (double)x.m.Sterne), 2), Math.Round(alt.Average(x => (double)x.m.Sterne), 2));
    }

    // ---------- Stimmung und Themen (lokale Wortlisten) ----------

    static readonly string[] PlusWorte = ["gut", "super", "toll", "klasse", "prima", "schnell", "freundlich", "hilfsbereit", "zufrieden", "empfehlen", "empfehlenswert", "perfekt",
        "top", "hervorragend", "ausgezeichnet", "lecker", "schön", "gerne", "wieder", "danke", "liebevoll", "pünktlich", "zuverlässig", "kompetent", "einwandfrei", "begeistert",
        "großartig", "angenehm", "sauber", "günstig", "fair", "bestens", "spitze", "wunderbar", "unkompliziert", "stabil", "hochwertig", "prompt"];
    static readonly string[] MinusWorte = ["schlecht", "langsam", "kaputt", "defekt", "beschädigt", "unfreundlich", "enttäuscht", "enttäuschend", "teuer", "nie", "leider",
        "falsch", "fehlt", "fehlte", "verspätet", "spät", "zu spät", "warten", "gewartet", "ärgerlich", "unverschämt", "mangelhaft", "billig", "dreckig", "schmutzig", "laut",
        "katastrophe", "katastrophal", "frech", "unzuverlässig", "unbrauchbar", "reklamation", "retoure", "zurückgeschickt", "abzocke", "keine antwort", "nicht erreichbar", "minderwertig"];
    static readonly string[] Verneinung = ["nicht", "kein", "keine", "keinen", "keiner", "nie", "niemals", "kaum", "wenig"];

    /// <summary>Themen mit Erkennungswörtern (Wortanfang).</summary>
    public static readonly (string Thema, string[] Worte)[] ThemenListe =
    [
        ("Lieferung", ["liefer", "versand", "versendet", "paket", "zustell", "angekommen", "ankunft", "post", "dhl", "hermes", "dpd"]),
        ("Qualität", ["qualität", "verarbeit", "material", "hochwertig", "minderwertig", "kaputt", "defekt", "stabil", "hält", "haltbar"]),
        ("Preis", ["preis", "teuer", "günstig", "billig", "kosten", "geld", "preis-leistung", "wert"]),
        ("Service", ["service", "kundendienst", "support", "beratung", "freundlich", "unfreundlich", "hilfsbereit", "antwort", "erreichbar", "mitarbeit", "personal", "team"]),
        ("Verpackung", ["verpack", "karton", "verpackt", "plastik", "umweltfreundlich"]),
        ("Größe & Passform", ["größe", "passform", "passt", "klein", "groß", "eng", "weit", "maße"]),
        ("Bedienung", ["bedien", "anleitung", "einfach", "kompliziert", "einricht", "aufbau", "handhabung"]),
        ("Rückgabe", ["rückgabe", "retoure", "zurückgeschickt", "umtausch", "erstattung", "geld zurück", "reklamation"]),
        ("Geschmack", ["lecker", "geschmack", "schmeckt", "frisch", "essen", "portion"]),
        ("Wartezeit", ["warten", "gewartet", "wartezeit", "termin", "pünktlich", "verspätet", "lange"]),
    ];

    [GeneratedRegex(@"[\p{L}\p{N}-]+")] private static partial Regex WordRx();

    /// <summary>Stimmung eines Textes: Plus- und Minuswörter (Verneinung in den zwei Wörtern davor dreht um), Wert −1 … +1,
    /// Art positiv/neutral/negativ. Widerspruch = Sterne und Text passen nicht zusammen (z. B. 5 Sterne, aber klar negativ).</summary>
    public static Stimmung Bewerten(string text, int sterne = 0)
    {
        var low = (text ?? "").ToLowerInvariant();
        var words = WordRx().Matches(low).Select(m => m.Value).ToList();
        var plus = new List<string>(); var minus = new List<string>();
        for (var i = 0; i < words.Count; i++)
        {
            var w = words[i];
            var neg = words.Skip(Math.Max(0, i - 2)).Take(Math.Min(2, i)).Any(x => Verneinung.Contains(x));
            var isPlus = PlusWorte.Contains(w); var isMinus = MinusWorte.Contains(w) && !Verneinung.Contains(w);
            if (w == "nie" && i + 1 < words.Count) isMinus = false;
            if (isPlus) (neg ? minus : plus).Add(neg ? "nicht " + w : w);
            else if (isMinus) (neg ? plus : minus).Add(neg ? "nicht " + w : w);
        }
        foreach (var phrase in MinusWorte.Where(p => p.Contains(' '))) if (low.Contains(phrase)) minus.Add(phrase);
        double total = plus.Count + minus.Count; var wert = total == 0 ? 0 : (plus.Count - minus.Count) / total;
        var art = total == 0 ? "neutral" : wert >= 0.25 ? "positiv" : wert <= -0.25 ? "negativ" : "gemischt";
        var themen = ThemenListe.Where(t => t.Worte.Any(k => k.Contains(' ') ? low.Contains(k) : words.Any(w => w.StartsWith(k, StringComparison.Ordinal)))).Select(t => t.Thema).ToList();
        var widerspruch = sterne > 0 && ((sterne >= 4 && art == "negativ") || (sterne <= 2 && art == "positiv"));
        return new(Math.Round(wert, 2), art, plus, minus, themen, widerspruch);
    }

    /// <summary>Themen über alle Bewertungen: wie oft genannt, wie oft in positiver/negativer Bewertung, Sterneschnitt.</summary>
    public static List<ThemenWert> Themen(IEnumerable<Kundenmeinung> l)
    {
        var acc = new Dictionary<string, (int N, int P, int M, double S)>();
        foreach (var m in l)
        {
            var s = Bewerten(m.Titel + ". " + m.Text, m.Sterne);
            foreach (var t in s.Themen)
            {
                var a = acc.GetValueOrDefault(t);
                acc[t] = (a.N + 1, a.P + (s.Art == "positiv" || (s.Art != "negativ" && m.Sterne >= 4) ? 1 : 0), a.M + (s.Art == "negativ" || (s.Art != "positiv" && m.Sterne <= 2) ? 1 : 0), a.S + m.Sterne);
            }
        }
        return acc.Select(kv => new ThemenWert(kv.Key, kv.Value.N, kv.Value.P, kv.Value.M, Math.Round(kv.Value.S / kv.Value.N, 2))).OrderByDescending(t => t.Nennungen).ThenBy(t => t.Thema).ToList();
    }

    // ---------- Antwort-Entwürfe ----------

    /// <summary>Lokaler Antwort-Entwurf: Dank, Eingehen auf das wichtigste Thema, bei Kritik Entschuldigung und Kontaktangebot, Gruß.
    /// Ein Entwurf – veröffentlicht wird er von dir auf der Plattform.</summary>
    public static string Entwurf(Kundenmeinung m, string firma, string ton = "freundlich")
    {
        var s = Bewerten(m.Titel + ". " + m.Text, m.Sterne);
        var vorname = m.Autor.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var anrede = ton == "sachlich" ? (vorname.Length > 0 ? $"Guten Tag {vorname}," : "Guten Tag,") : vorname.Length > 0 ? $"Hallo {vorname}," : "Hallo,";
        var thema = s.Themen.FirstOrDefault();
        var sb = new StringBuilder(anrede).Append("\n\n");
        var gut = m.Sterne >= 4 && s.Art != "negativ";
        if (gut)
        {
            sb.Append(ton == "herzlich" ? "vielen herzlichen Dank für Ihre schöne Bewertung – darüber freuen wir uns sehr! " : "vielen Dank für Ihre Bewertung und das Vertrauen. ");
            sb.Append(thema switch
            {
                "Lieferung" => "Schön, dass alles schnell bei Ihnen angekommen ist.", "Qualität" => "Es freut uns, dass Sie mit der Qualität zufrieden sind.",
                "Service" => "Wir geben Ihr Lob gern an unser Team weiter.", "Preis" => "Schön, dass für Sie Preis und Leistung stimmen.",
                "Geschmack" => "Schön, dass es Ihnen geschmeckt hat.", _ => "Wir freuen uns, dass Sie zufrieden sind.",
            });
            sb.Append(ton == "sachlich" ? "" : " Wir freuen uns auf Ihren nächsten Besuch.");
        }
        else
        {
            sb.Append("vielen Dank, dass Sie sich die Zeit für Ihre ehrliche Rückmeldung genommen haben. ");
            sb.Append(thema switch
            {
                "Lieferung" or "Wartezeit" => "Es tut uns leid, dass Sie warten mussten – das entspricht nicht unserem Anspruch.",
                "Qualität" => "Es tut uns leid, dass das Produkt nicht Ihren Erwartungen entsprochen hat.",
                "Service" => "Es tut uns leid, dass Sie sich bei uns nicht gut betreut gefühlt haben.",
                "Rückgabe" => "Es tut uns leid, dass die Rückgabe nicht reibungslos lief.",
                "Preis" => "Wir nehmen Ihren Hinweis zum Preis ernst.",
                _ => "Es tut uns leid, dass Sie nicht zufrieden waren.",
            });
            sb.Append(" Bitte melden Sie sich direkt bei uns, damit wir eine Lösung für Sie finden. [Kontakt: E-Mail oder Telefon ergänzen]");
        }
        sb.Append("\n\n").Append(ton == "herzlich" ? "Herzliche Grüße" : "Viele Grüße").Append('\n').Append(firma.Trim().Length > 0 ? firma.Trim() : "[Ihr Name / Firma]");
        return sb.ToString();
    }

    /// <summary>Auftrag an die KI für einen Antwort-Entwurf (die KI erfindet nichts und verspricht nichts, was nicht da steht).</summary>
    public static string AntwortPrompt(Kundenmeinung m, string firma, string ton) =>
        $"Schreibe einen kurzen Antwort-Entwurf (höchstens 90 Wörter, Deutsch, Sie-Form, Ton: {ton}) des Unternehmens „{(firma.Length > 0 ? firma : "unser Unternehmen")}“ auf diese Kundenbewertung. " +
        "Bedanke dich, geh konkret auf den Inhalt ein, bei Kritik entschuldige dich ohne Ausreden und biete Kontakt an. Erfinde keine Fakten, keine Rabatte und keine Zusagen; " +
        "fehlende Angaben in [eckige Klammern]. Nenne keine Kundendaten. Nur den Antworttext ausgeben.\n\n" +
        $"Bewertung ({m.Sterne} von 5 Sternen): {(m.Titel.Length > 0 ? m.Titel + " – " : "")}{Cut(m.Text, 1500)}";

    /// <summary>KI-Antwort säubern: Anführungszeichen/Vorspann weg, auf 1200 Zeichen kürzen.</summary>
    public static string Saeubern(string text)
    {
        var t = (text ?? "").Trim().Trim('"', '„', '“');
        t = Regex.Replace(t, @"^(hier (ist|kommt) (ein|der|mein) (antwort-?)?entwurf[^\n]*:\s*)", "", RegexOptions.IgnoreCase).Trim();
        return Cut(t, 1200);
    }

    static string Cut(string s, int n) => s.Length <= n ? s : s[..n].TrimEnd() + "…";

    // ---------- Zitat-Grafik ----------

    public static readonly (string Id, string Name, int W, int H)[] ZitatFormate = [("quadrat", "Quadrat (Instagram, 1080 × 1080)", 1080, 1080), ("story", "Story (1080 × 1920)", 1080, 1920), ("web", "Webseite / Link-Vorschau (1200 × 630)", 1200, 630)];

    /// <summary>Text für die Grafik: ganz oder an einer Satzgrenze mit „[…]“ gekürzt – Wörter werden nie verändert.</summary>
    public static string ZitatText(string text, int max = 220)
    {
        var t = Regex.Replace(text ?? "", @"\s+", " ").Trim();
        if (t.Length <= max) return t;
        var cut = t[..max]; var dot = Math.Max(cut.LastIndexOf(". ", StringComparison.Ordinal), Math.Max(cut.LastIndexOf("! ", StringComparison.Ordinal), cut.LastIndexOf("? ", StringComparison.Ordinal)));
        return (dot > max / 3 ? cut[..(dot + 1)] : cut[..Math.Max(1, cut.LastIndexOf(' '))]) + " […]";
    }

    /// <summary>Aufteilung der Zitat-Grafik: Zeilen nach Zeichenbreite umgebrochen, Schriftgröße passend zur Länge.</summary>
    public static ZitatLayout Zitat(Kundenmeinung m, string format, string farbe = "#1F2937", string grund = "#FDF6E3")
    {
        if (string.IsNullOrWhiteSpace(m.Text)) throw new InvalidOperationException("Diese Bewertung hat keinen Text für ein Zitat.");
        var f = ZitatFormate.FirstOrDefault(x => x.Id == format); if (f.Id is null) f = ZitatFormate[0];
        var text = "„" + ZitatText(m.Text) + "“";
        var schrift = f.W * (text.Length < 80 ? 0.062 : text.Length < 150 ? 0.05 : 0.042);
        var perLine = Math.Max(10, (int)((f.W * 0.8) / (schrift * 0.52)));
        var zeilen = new List<string>(); var cur = new StringBuilder();
        foreach (var w in text.Split(' '))
        {
            if (cur.Length > 0 && cur.Length + 1 + w.Length > perLine) { zeilen.Add(cur.ToString()); cur.Clear(); }
            if (cur.Length > 0) cur.Append(' '); cur.Append(w);
        }
        if (cur.Length > 0) zeilen.Add(cur.ToString());
        var unter = "– " + (m.Autor.Length > 0 ? m.Autor : "Kundin/Kunde") + (m.Quelle.Length > 0 ? " · " + m.Quelle : "") + (DatumAus(m.Datum) is { } d ? " · " + DateTime.ParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("MM/yyyy", CultureInfo.InvariantCulture) : "");
        return new(f.W, f.H, zeilen, Math.Round(schrift, 1), new string('★', m.Sterne) + new string('☆', 5 - m.Sterne), unter, farbe, grund);
    }

    static string E(string s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>Zitat-Grafik als SVG (Text bleibt Text, für Webseite und zum Weiterbearbeiten).</summary>
    public static string ZitatSvg(ZitatLayout z)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{z.Breite}\" height=\"{z.Hoehe}\" viewBox=\"0 0 {z.Breite} {z.Hoehe}\">\n");
        sb.Append(CultureInfo.InvariantCulture, $"<rect width=\"100%\" height=\"100%\" fill=\"{E(z.Grund)}\"/>\n");
        var lineH = z.Schrift * 1.3; var block = z.Zeilen.Count * lineH + z.Schrift * 3.2;
        var y = (z.Hoehe - block) / 2 + z.Schrift;
        sb.Append(CultureInfo.InvariantCulture, $"<text x=\"50%\" y=\"{y:0.#}\" text-anchor=\"middle\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"{z.Schrift * 0.9:0.#}\" fill=\"#F59E0B\">{z.Sterne}</text>\n");
        y += z.Schrift * 1.6;
        foreach (var l in z.Zeilen) { sb.Append(CultureInfo.InvariantCulture, $"<text x=\"50%\" y=\"{y:0.#}\" text-anchor=\"middle\" font-family=\"Georgia, serif\" font-size=\"{z.Schrift:0.#}\" fill=\"{E(z.Farbe)}\">{E(l)}</text>\n"); y += lineH; }
        y += z.Schrift * 0.6;
        sb.Append(CultureInfo.InvariantCulture, $"<text x=\"50%\" y=\"{y:0.#}\" text-anchor=\"middle\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"{z.Schrift * 0.5:0.#}\" fill=\"{E(z.Farbe)}\" opacity=\"0.75\">{E(z.Unterzeile)}</text>\n");
        sb.Append("</svg>\n");
        return sb.ToString();
    }

    // ---------- Widget ----------

    /// <summary>Statisches Bewertungs-Widget (HTML mit eigenem CSS, ohne Skripte, ohne Einbindung fremder Dienste) für die eigene Webseite.
    /// Zeigt Schnitt, Anzahl, Verteilung, die neuesten Bewertungen (alle Sterne – keine Auswahl nur der guten) und den Prüfhinweis.</summary>
    public static string WidgetHtml(MeinungsSammlung s, int anzahl = 6)
    {
        var st = Statistik(s.Meinungen);
        var neueste = s.Meinungen.OrderByDescending(m => m.Datum, StringComparer.Ordinal).Take(Math.Clamp(anzahl, 1, 50)).ToList();
        var sb = new StringBuilder();
        sb.Append("<!-- Bewertungs-Widget (Trendo) – statisch, ohne Skripte und ohne Tracking -->\n");
        sb.Append("<section class=\"tw-reviews\" aria-label=\"Kundenbewertungen\">\n<style>.tw-reviews{font-family:system-ui,Segoe UI,Arial,sans-serif;max-width:720px;border:1px solid #e5e7eb;border-radius:12px;padding:16px;color:#111827;background:#fff}" +
                  ".tw-reviews .tw-head{display:flex;gap:12px;align-items:baseline}.tw-reviews .tw-avg{font-size:32px;font-weight:700}.tw-reviews .tw-stars{color:#f59e0b;letter-spacing:2px}" +
                  ".tw-reviews ol{list-style:none;padding:0;margin:12px 0 0}.tw-reviews li{border-top:1px solid #f3f4f6;padding:10px 0}.tw-reviews .tw-meta{color:#6b7280;font-size:12px}" +
                  ".tw-reviews .tw-reply{margin:6px 0 0 12px;padding-left:8px;border-left:3px solid #e5e7eb;font-size:14px}.tw-reviews .tw-note{color:#6b7280;font-size:12px;margin-top:10px}" +
                  ".tw-reviews .tw-bar{display:flex;gap:6px;align-items:center;font-size:12px}.tw-reviews .tw-bar span.b{display:inline-block;height:6px;background:#f59e0b;border-radius:3px}</style>\n");
        sb.Append(CultureInfo.InvariantCulture, $"<div class=\"tw-head\"><span class=\"tw-avg\">{st.Schnitt.ToString("0.0", De)}</span><span class=\"tw-stars\">{Sterne(st.Schnitt)}</span><span>{st.Anzahl} Bewertungen</span></div>\n");
        for (var i = 5; i >= 1; i--)
        {
            var share = st.Anzahl == 0 ? 0 : st.Verteilung[i - 1] / (double)st.Anzahl;
            sb.Append(CultureInfo.InvariantCulture, $"<div class=\"tw-bar\">{i} ★ <span class=\"b\" style=\"width:{share * 160:0}px\"></span> {st.Verteilung[i - 1]}</div>\n");
        }
        sb.Append("<ol>\n");
        foreach (var m in neueste)
        {
            sb.Append("<li><div class=\"tw-stars\" aria-label=\"").Append(m.Sterne).Append(" von 5 Sternen\">").Append(new string('★', m.Sterne)).Append(new string('☆', 5 - m.Sterne)).Append("</div>");
            if (m.Titel.Length > 0) sb.Append("<strong>").Append(E(m.Titel)).Append("</strong> ");
            sb.Append("<div>").Append(E(m.Text)).Append("</div><div class=\"tw-meta\">").Append(E(m.Autor.Length > 0 ? m.Autor : "Kundin/Kunde"));
            if (DatumAus(m.Datum) is { } d) sb.Append(" · ").Append(E(DateTime.ParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)));
            sb.Append(" · ").Append(E(m.Quelle)).Append(m.Verifiziert ? " · verifizierter Kauf" : "").Append("</div>");
            if (m.Status == "beantwortet" && m.Antwort.Trim().Length > 0) sb.Append("<div class=\"tw-reply\"><strong>Antwort:</strong> ").Append(E(m.Antwort)).Append("</div>");
            sb.Append("</li>\n");
        }
        sb.Append("</ol>\n<p class=\"tw-note\">").Append(E(s.Pruefhinweis)).Append("</p>\n</section>\n");
        return sb.ToString();
    }

    public static string Sterne(double avg) { var full = (int)Math.Round(avg, MidpointRounding.AwayFromZero); return new string('★', Math.Clamp(full, 0, 5)) + new string('☆', 5 - Math.Clamp(full, 0, 5)); }

    // ---------- Recht und Auffälligkeiten ----------

    public static readonly string[] Regeln =
    [
        "Nur echte Bewertungen echter Kundinnen und Kunden – Trendo erzeugt keine Bewertungen. Gekaufte, getauschte oder selbst geschriebene Bewertungen sind verboten (UWG, EU-Omnibus-Richtlinie).",
        "Angeben, ob und wie geprüft wird, dass Bewertungen von echten Käufern stammen (§ 5b Abs. 3 UWG) – steht als Prüfhinweis unter dem Widget.",
        "Nicht nur die guten zeigen: Eine Auswahl, die schlechte Bewertungen versteckt, kann irreführend sein. Das Widget zeigt die neuesten, egal mit wie vielen Sternen.",
        "Zitate unverändert übernehmen, kürzen nur mit „[…]“, Quelle und Datum nennen. Für Werbung mit dem Zitat am besten das Einverständnis der Person einholen.",
        "Namen kürzen („Erika M.“) – keine E-Mail-Adressen oder Bestellnummern veröffentlichen (DSGVO).",
        "Für Gegenleistungen (Rabatt, Gutschein) erhaltene Bewertungen müssen als solche erkennbar sein.",
        "Antworten sachlich und ohne Kundendaten; Trendo schickt nichts ab – du veröffentlichst die Antwort selbst auf der Plattform.",
    ];

    /// <summary>Auffälligkeiten, die Plattformen und Behörden als Hinweis auf unechte Bewertungen werten.</summary>
    public static List<string> Auffaelligkeiten(IReadOnlyList<Kundenmeinung> l)
    {
        var w = new List<string>();
        foreach (var g in l.Where(m => m.Text.Trim().Length > 15).GroupBy(m => Regex.Replace(m.Text.ToLowerInvariant(), @"\s+", " ").Trim()).Where(g => g.Count() > 1))
            w.Add($"{g.Count()}× fast gleicher Text („{Cut(g.First().Text, 40)}“) – doppelt eingelesen oder unecht?");
        foreach (var g in l.Where(m => m.Datum.Length == 10).GroupBy(m => m.Datum).Where(g => g.Count() >= 5 && g.Count() >= l.Count * 0.3))
            w.Add($"{g.Count()} Bewertungen am selben Tag ({g.Key}) – auffällige Häufung.");
        var ohne = l.Count(m => m.Quelle.Trim().Length == 0); if (ohne > 0) w.Add($"{ohne} Bewertung(en) ohne Quelle – für Zitate und Widget die Herkunft eintragen.");
        return w;
    }

    [GeneratedRegex(@"\b(erfinde|erfunden|fake|gefälscht|ausdenken|ausgedacht|generier\w*|schreib\w*|erstell\w*|verfass\w*)\b[^.\n]{0,40}\b(bewertungen?|rezensionen?|reviews?|kundenmeinungen?|sterne)\b|\b(bewertungen?|rezensionen?|reviews?)\b[^.\n]{0,30}\b(erfinden|fälschen|kaufen|ausdenken|schreiben)\b", RegexOptions.IgnoreCase)]
    private static partial Regex FakeRx();

    /// <summary>Wird nach erfundenen Bewertungen gefragt? (Antwort-Entwürfe auf echte Bewertungen sind erlaubt.)</summary>
    public static bool FragtNachFake(string? text) => FakeRx().IsMatch(text ?? "") && !Regex.IsMatch(text ?? "", @"\bantwort", RegexOptions.IgnoreCase);

    /// <summary>Bewertungen als CSV (Semikolon) – zum Sichern und für Trendo Tabelle.</summary>
    public static string Csv(IEnumerable<Kundenmeinung> l)
    {
        static string Q(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder("Sterne;Titel;Text;Name;Datum;Quelle;Verifiziert;Stimmung;Themen;Status;Antwort\n");
        foreach (var m in l)
        {
            var s = Bewerten(m.Titel + ". " + m.Text, m.Sterne);
            sb.Append(m.Sterne).Append(';').Append(Q(m.Titel)).Append(';').Append(Q(m.Text)).Append(';').Append(Q(m.Autor)).Append(';').Append(m.Datum).Append(';').Append(Q(m.Quelle)).Append(';')
              .Append(m.Verifiziert ? "ja" : "nein").Append(';').Append(s.Art).Append(';').Append(Q(string.Join(", ", s.Themen))).Append(';').Append(m.Status).Append(';').Append(Q(m.Antwort)).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Auswertung als Markdown (Statistik, Themen, Verlauf, offene Antworten).</summary>
    public static string Bericht(MeinungsSammlung s, DateTime heute)
    {
        var st = Statistik(s.Meinungen); var sb = new StringBuilder();
        sb.Append("# Kundenmeinungen").Append(s.Firma.Length > 0 ? " – " + s.Firma : "").Append("\n\n");
        sb.Append(De, $"**{st.Schnitt:0.00} von 5 Sternen** aus {st.Anzahl} Bewertungen · {st.AnteilPositiv:P0} mit 4 oder 5 Sternen · {st.Offen} ohne veröffentlichte Antwort\n\n");
        for (var i = 5; i >= 1; i--) sb.Append(De, $"- {i} Sterne: {st.Verteilung[i - 1]}\n");
        if (Trend(s.Meinungen, heute) is { } t) sb.Append(De, $"\nLetzte 90 Tage: {t.Neu:0.00} Sterne (davor {t.Vorher:0.00}) – {(t.Neu > t.Vorher + 0.05 ? "besser" : t.Neu < t.Vorher - 0.05 ? "schlechter" : "gleich")}\n");
        var th = Themen(s.Meinungen);
        if (th.Count > 0)
        {
            sb.Append("\n## Themen\n\n| Thema | Nennungen | positiv | negativ | Sterne |\n|---|---:|---:|---:|---:|\n");
            foreach (var x in th) sb.Append(De, $"| {x.Thema} | {x.Nennungen} | {x.Positiv} | {x.Negativ} | {x.Schnitt:0.0} |\n");
        }
        var v = Verlauf(s.Meinungen);
        if (v.Count > 0) { sb.Append("\n## Verlauf\n\n| Monat | Anzahl | Sterne |\n|---|---:|---:|\n"); foreach (var x in v) sb.Append(De, $"| {x.Monat} | {x.Anzahl} | {x.Schnitt:0.0} |\n"); }
        var auff = Auffaelligkeiten(s.Meinungen); if (auff.Count > 0) { sb.Append("\n## Auffälligkeiten\n\n"); foreach (var a in auff) sb.Append("- ").Append(a).Append('\n'); }
        sb.Append("\n_Auswertung mit Wortlisten (lokal) – Stimmung ist eine Schätzung. Keine Rechtsberatung._\n");
        return sb.ToString();
    }

    public static MeinungsSammlung Beispiel(DateTime heute) => new()
    {
        Firma = "Café Beispiel",
        Meinungen =
        [
            new() { Id = "m1", Sterne = 5, Titel = "Sehr lecker", Text = "Super leckerer Kuchen und sehr freundliches Personal. Gerne wieder!", Autor = "Erika M.", Datum = heute.AddDays(-5).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Quelle = "Google", ZitatErlaubt = true },
            new() { Id = "m2", Sterne = 2, Titel = "Zu lange gewartet", Text = "Wir haben 40 Minuten auf den Kaffee gewartet, leider kam keine Entschuldigung.", Autor = "Max M.", Datum = heute.AddDays(-12).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Quelle = "Google" },
            new() { Id = "m3", Sterne = 4, Text = "Schöne Atmosphäre, Preis ist etwas teuer, aber die Qualität stimmt.", Autor = "E. Muster", Datum = heute.AddDays(-40).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Quelle = "Eigene Seite", Verifiziert = true },
            new() { Id = "m4", Sterne = 5, Text = "Bestellung kam schnell und liebevoll verpackt. Klare Empfehlung!", Autor = "M. Beispiel", Datum = heute.AddDays(-120).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Quelle = "Eigene Seite", Verifiziert = true, Status = "beantwortet", Antwort = "Vielen Dank – wir freuen uns!" },
            new() { Id = "m5", Sterne = 1, Text = "Torte kam beschädigt an, auf meine E-Mail gab es keine Antwort.", Autor = "Gast", Datum = heute.AddDays(-150).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Quelle = "Trustpilot" },
        ],
    };
}
