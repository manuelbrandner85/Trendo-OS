using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Trendo.Core.Business;

// ===================== Bankimport für „Geld“ (SPEC-11, T-0694) =====================
// Kontoauszüge einlesen: CSV (mehrere gängige Kopfzeilen, Spaltenzuordnung lernbar), CAMT.053 (ISO 20022, XML) und MT940 (SWIFT, Text).
// Geschrieben nach den öffentlichen Formatbeschreibungen (ISO 20022 camt.053, SWIFT MT940 mit den in Deutschland üblichen :86:-Feldern ?20–?33).
// Ehrlich: geprüft mit handgeschriebenen Beispieldateien nach Spezifikation, NICHT mit Exporten echter Banken.

/// <summary>Eine Zeile des Kontoauszugs. Betrag: Eingang positiv, Ausgang negativ.</summary>
public sealed record BankZeile(DateOnly Datum, decimal Betrag, string Gegenseite, string Zweck, string Iban = "", string Referenz = "", string Waehrung = "EUR")
{
    /// <summary>Buchungstext wie im Haushaltsbuch: „Gegenseite – Zweck“.</summary>
    public string Text => Gegenseite.Length == 0 ? Zweck : Zweck.Length == 0 || Zweck.StartsWith(Gegenseite, StringComparison.OrdinalIgnoreCase) ? (Zweck.Length == 0 ? Gegenseite : Zweck) : Gegenseite + " – " + Zweck;
}

/// <summary>Gelesener Auszug mit Kontrollwerten. <see cref="Stimmt"/>: Anfangssaldo + Summe = Endsaldo (null = der Auszug nennt keine Salden).</summary>
public sealed record BankAuszug(string Format, string Konto, IReadOnlyList<BankZeile> Zeilen, decimal? Anfang, decimal? Ende, IReadOnlyList<string> Hinweise)
{
    public decimal Summe => Zeilen.Sum(z => z.Betrag);
    public bool? Stimmt => Anfang is { } a && Ende is { } e ? a + Summe == e : null;
    public string Kontrolle => Stimmt switch
    {
        true => $"{Zeilen.Count} Buchungen, Summe {BizJson.Euro(Summe)} – passt zu Anfangs- und Endsaldo des Auszugs.",
        false => $"{Zeilen.Count} Buchungen, Summe {BizJson.Euro(Summe)} – passt NICHT zu den Salden des Auszugs ({BizJson.Euro(Anfang!.Value)} → {BizJson.Euro(Ende!.Value)}). Bitte prüfen.",
        _ => $"{Zeilen.Count} Buchungen, Summe {BizJson.Euro(Summe)} (der Auszug nennt keine Salden zum Gegenprüfen).",
    };
}

/// <summary>Welche Spalte der CSV-Datei was bedeutet (Index ab 0, −1 = gibt es nicht). Soll/Haben: getrennte Betragsspalten mancher Banken.</summary>
public sealed record SpaltenZuordnung(int Datum, int Betrag, int Zweck, int Gegenseite = -1, int Soll = -1, int Haben = -1, int Iban = -1, int Kennzeichen = -1, int Zahler = -1)
{
    public bool Brauchbar => Datum >= 0 && (Betrag >= 0 || Soll >= 0 || Haben >= 0);
}

/// <summary>Die Spalten ließen sich nicht sicher erkennen – die Oberfläche fragt nach und merkt sich die Antwort.</summary>
public sealed class SpaltenUnklarException(string[] kopf, string[] beispiel, SpaltenZuordnung vorschlag)
    : Exception("Die Spalten dieser Datei erkenne ich nicht sicher. Bitte zeig mir, in welcher Spalte Datum, Betrag und Text stehen – ich merke es mir für das nächste Mal.")
{
    public string[] Kopf { get; } = kopf;
    public string[] Beispiel { get; } = beispiel;
    public SpaltenZuordnung Vorschlag { get; } = vorschlag;
}

/// <summary>Gelernte Spaltenzuordnungen je Kopfzeile (finanzen/bank-spalten.json).</summary>
public sealed class SpaltenGedaechtnis(string dataRoot)
{
    string F => Path.Combine(dataRoot, "finanzen", "bank-spalten.json");
    Dictionary<string, SpaltenZuordnung> Load() { try { return File.Exists(F) ? JsonSerializer.Deserialize<Dictionary<string, SpaltenZuordnung>>(File.ReadAllText(F)) ?? [] : []; } catch (JsonException) { return []; } }
    public static string Schluessel(string[] kopf) => string.Join("|", kopf.Select(k => k.Trim().ToLowerInvariant()));
    public SpaltenZuordnung? Finden(string[] kopf) => Load().GetValueOrDefault(Schluessel(kopf));
    public void Merken(string[] kopf, SpaltenZuordnung z) { var d = Load(); d[Schluessel(kopf)] = z; AtomicFile.WriteAllText(F, JsonSerializer.Serialize(d)); }
    public int Anzahl => Load().Count;
}

public static partial class BankImport
{
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    public static readonly string[] Endungen = [".csv", ".txt", ".xml", ".sta", ".mta", ".940", ".mt940", ".camt", ".053"];

    /// <summary>Datei lesen – das Format wird am Inhalt erkannt, nicht an der Endung.</summary>
    public static BankAuszug Lesen(string path, SpaltenGedaechtnis? gedaechtnis = null)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length == 0) throw new InvalidDataException("Die Datei ist leer.");
        if (bytes.Length > 64 * 1024 * 1024) throw new InvalidDataException("Die Datei ist größer als 64 MB – das ist kein üblicher Kontoauszug.");
        return LesenText(Decode(bytes), gedaechtnis);
    }

    public static BankAuszug LesenText(string text, SpaltenGedaechtnis? gedaechtnis = null, SpaltenZuordnung? zuordnung = null)
    {
        var head = text.Length > 4000 ? text[..4000] : text;
        if (head.TrimStart().StartsWith('<') && head.Contains("BkToCstmrStmt", StringComparison.Ordinal)) return Camt053(text);
        if (head.TrimStart().StartsWith('<') && (head.Contains("BkToCstmrAcctRpt", StringComparison.Ordinal) || head.Contains("BkToCstmrDbtCdtNtfctn", StringComparison.Ordinal)))
            throw new InvalidDataException("Das ist ein CAMT.052 oder CAMT.054 (Vormerkung/Avis), kein Kontoauszug CAMT.053. Bitte den Auszug als CAMT.053 herunterladen.");
        if (head.TrimStart().StartsWith('<')) throw new InvalidDataException("Diese XML-Datei ist kein CAMT.053-Kontoauszug.");
        if (Tag61().IsMatch(text) && text.Contains(":20:", StringComparison.Ordinal)) return Mt940(text);
        return Csv(text, gedaechtnis, zuordnung);
    }

    /// <summary>UTF-8 (mit oder ohne Kennung), sonst Windows-1252 – so liefern deutsche Banken CSV und MT940 meist aus.</summary>
    public static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        try { return new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { return Encoding.Latin1.GetString(bytes).Replace('\u0080', '€'); }
    }

    // ---------------------------------------------------------------- CSV
    static string[] Split(string line, char sep)
    {
        var res = new List<string>(); var sb = new StringBuilder(); var q = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"') { if (q && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else q = !q; }
            else if (c == sep && !q) { res.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        res.Add(sb.ToString()); return [.. res.Select(x => x.Trim())];
    }

    /// <summary>Zeilen einer CSV-Datei; Zeilenumbrüche in Anführungszeichen gehören zum Feld.</summary>
    static List<string> Zeilen(string csv)
    {
        var res = new List<string>(); var sb = new StringBuilder(); var q = false;
        foreach (var c in csv.Replace("\r\n", "\n").Replace('\r', '\n'))
        {
            if (c == '"') q = !q;
            if (c == '\n' && !q) { if (sb.Length > 0) res.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c == '\n' ? ' ' : c);
        }
        if (sb.Length > 0) res.Add(sb.ToString());
        return res;
    }

    static readonly string[] DatumNamen = ["buchungstag", "buchungsdatum", "buchung", "datum", "booking date", "date", "valuta", "wertstellung"];
    static readonly string[] BetragNamen = ["betrag", "umsatz", "amount"];
    static readonly string[] ZweckNamen = ["verwendungszweck", "buchungstext", "payment reference", "beschreibung", "vorgang", "text", "description", "reference"];
    static readonly string[] GegenNamen = ["beguenstigter/zahlungspflichtiger", "begünstigter/zahlungspflichtiger", "auftraggeber/empfänger", "auftraggeber / empfänger", "name zahlungsbeteiligter", "zahlungsempfänger*in", "zahlungspflichtige*r",
        "empfänger", "auftraggeber", "begünstigter", "beguenstigter", "zahlungspflichtiger", "payee", "partner name", "name"];

    static readonly HashSet<string> GanzesWort = ["date", "name", "text", "buchung", "umsatz", "amount", "reference", "vorgang", "datum", "betrag", "valuta"];
    /// <summary>Passt der Spaltenname? Kurze Wörter nur als ganzes Wort oder Wortende („Buchungsdatum“ ja, „Datei“ nein), lange auch als Teil.</summary>
    static bool Passt(string spalte, string name) =>
        GanzesWort.Contains(name) ? Regex.IsMatch(spalte, name is "datum" or "betrag" ? $@"{Regex.Escape(name)}($|[^a-zäöüß])" : $@"(^|[^a-zäöüß]){Regex.Escape(name)}($|[^a-zäöüß])") : spalte.Contains(name, StringComparison.Ordinal);

    /// <summary>Kopfzeile finden: die erste Zeile, die eine Datums- und eine Betragsspalte benennt (Banken stellen oft Kontodaten voran).</summary>
    public static (int Zeile, char Trenner, string[] Kopf)? Kopf(string csv)
    {
        var lines = Zeilen(csv);
        for (var i = 0; i < Math.Min(lines.Count, 40); i++)
        {
            var l = lines[i]; var sep = new[] { ';', ',', '\t' }.OrderByDescending(c => l.Count(x => x == c)).First();
            var h = Split(l, sep); if (h.Length < 3) continue;
            var low = h.Select(x => x.ToLowerInvariant()).ToArray();
            if (low.Any(x => DatumNamen.Any(n => Passt(x, n))) && low.Any(x => BetragNamen.Any(n => Passt(x, n)) || x is "soll" or "haben" or "soll (eur)" or "haben (eur)")) return (i, sep, h);
        }
        return null;
    }

    /// <summary>Spalten aus der Kopfzeile raten (genaue Namen gängiger Bank-Exporte zuerst).</summary>
    public static SpaltenZuordnung Raten(string[] kopf)
    {
        var h = kopf.Select(x => x.Trim().ToLowerInvariant()).ToArray();
        int Col(string[] names, Func<string, bool>? not = null) { foreach (var n in names) { var k = Array.FindIndex(h, x => Passt(x, n) && (not is null || !not(x))); if (k >= 0) return k; } return -1; }
        var datum = Col(DatumNamen);
        var betrag = Col(BetragNamen, x => x.Contains("währung") || x.Contains("waehrung") || x.Contains("typ") || x.Contains("saldo") || x.Contains("ursprung") || x.Contains("original") || x.Contains("foreign"));
        var soll = Array.FindIndex(h, x => x == "soll" || x.StartsWith("soll ", StringComparison.Ordinal) || x.StartsWith("soll(", StringComparison.Ordinal));
        var haben = Array.FindIndex(h, x => x == "haben" || x.StartsWith("haben ", StringComparison.Ordinal) || x.StartsWith("haben(", StringComparison.Ordinal));
        var zweck = Col(["verwendungszweck", "payment reference", "beschreibung", "description", "reference"]);
        if (zweck < 0) zweck = Col(ZweckNamen, x => x.Contains("datum") || x.Contains("tag"));
        var gegen = Col(GegenNamen, x => x.Contains("iban") || x.Contains("bic") || x.Contains("konto") || x.Contains("blz"));
        if (gegen == zweck) gegen = -1;
        // DKB (neu) nennt Zahlungspflichtige*r und Zahlungsempfänger*in getrennt – als Gegenseite zählt, wer nicht man selbst ist; hier: Empfänger, sonst Pflichtige*r
        var zahler = -1;
        var pfl = Array.FindIndex(h, x => x.StartsWith("zahlungspflichtige", StringComparison.Ordinal)); var emp = Array.FindIndex(h, x => x.StartsWith("zahlungsempfänger", StringComparison.Ordinal));
        if (pfl >= 0 && emp >= 0) { gegen = emp; zahler = pfl; }
        var iban = Array.FindIndex(h, x => x.Contains("iban") && !x.Contains("auftrag"));
        if (iban < 0) iban = Array.FindIndex(h, x => x == "iban");
        var kz = Array.FindIndex(h, x => x is "s/h" or "soll/haben" or "soll/haben-kennzeichen" or "umsatzart");
        return new SpaltenZuordnung(datum, soll >= 0 || haben >= 0 ? -1 : betrag, zweck, gegen, soll, haben, iban, kz, zahler);
    }

    public static BankAuszug Csv(string csv, SpaltenGedaechtnis? gedaechtnis = null, SpaltenZuordnung? zuordnung = null)
    {
        var lines = Zeilen(csv);
        var kopf = Kopf(csv);
        if (kopf is null)
        {
            // keine erkennbare Kopfzeile: die erste Zeile mit mindestens drei Feldern als Kopf anbieten und nachfragen
            var first = lines.FindIndex(l => Split(l, l.Count(c => c == ';') >= l.Count(c => c == ',') ? ';' : ',').Length >= 3);
            if (first < 0) throw new InvalidDataException("Das ist kein Kontoauszug, den ich lesen kann (CSV mit Datum und Betrag, CAMT.053 oder MT940).");
            var sep0 = lines[first].Count(c => c == ';') >= lines[first].Count(c => c == ',') ? ';' : ',';
            var h0 = Split(lines[first], sep0);
            var learned0 = zuordnung ?? gedaechtnis?.Finden(h0);
            if (learned0 is null) throw new SpaltenUnklarException(h0, first + 1 < lines.Count ? Split(lines[first + 1], sep0) : [], new SpaltenZuordnung(-1, -1, -1));
            kopf = (first, sep0, h0); zuordnung = learned0;
        }
        var (hi, sep, head) = kopf.Value;
        var map = zuordnung ?? gedaechtnis?.Finden(head) ?? Raten(head);
        if (!map.Brauchbar) throw new SpaltenUnklarException(head, hi + 1 < lines.Count ? Split(lines[hi + 1], sep) : [], map);
        var res = new List<BankZeile>(); var skipped = 0;
        foreach (var l in lines.Skip(hi + 1))
        {
            var f = Split(l, sep);
            string At(int i) => i >= 0 && i < f.Length ? f[i] : "";
            if (!TryDatum(At(map.Datum), out var d)) { if (f.Any(x => x.Length > 0)) skipped++; continue; }
            decimal a;
            if (map.Betrag >= 0) { if (!BizJson.TryBetrag(At(map.Betrag), out a)) { skipped++; continue; } }
            else
            {
                var so = BizJson.TryBetrag(At(map.Soll), out var s1) ? Math.Abs(s1) : 0; var ha = BizJson.TryBetrag(At(map.Haben), out var h1) ? Math.Abs(h1) : 0;
                if (so == 0 && ha == 0) { skipped++; continue; }
                a = ha - so;
            }
            var kz = At(map.Kennzeichen).ToUpperInvariant();
            if (a > 0 && (kz is "S" or "SOLL" or "D" or "DBIT" || kz.StartsWith("AUSGANG", StringComparison.Ordinal))) a = -a;
            res.Add(new BankZeile(d, a, a > 0 && map.Zahler >= 0 ? At(map.Zahler) : At(map.Gegenseite), Regex.Replace(At(map.Zweck), @"\s+", " "), At(map.Iban).Replace(" ", "")));
        }
        var notes = new List<string>();
        if (skipped > 0) notes.Add($"{skipped} Zeilen ohne lesbares Datum oder Betrag übersprungen (z. B. Saldo- oder Fußzeilen).");
        return new BankAuszug("CSV", "", res, null, null, notes);
    }

    static bool TryDatum(string s, out DateOnly d)
    {
        s = s.Trim();
        return DateOnly.TryParseExact(s, ["dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy", "yyyy-MM-dd", "dd/MM/yyyy", "yyyyMMdd", "dd-MM-yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out d)
            || DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t) && s.Length >= 10 && s[4] == '-' && (d = DateOnly.FromDateTime(t)) != default;
    }

    // ---------------------------------------------------------------- CAMT.053 (ISO 20022)
    /// <summary>camt.053.001.xx – unabhängig von der Versionsnummer im Namensraum (02, 04, 08 …): gelesen werden nur gebuchte Umsätze (Status BOOK).</summary>
    public static BankAuszug Camt053(string xml)
    {
        XDocument doc;
        try { doc = XDocument.Parse(xml); } catch (System.Xml.XmlException e) { throw new InvalidDataException("Die CAMT-Datei ist kein gültiges XML: " + e.Message); }
        static IEnumerable<XElement> E(XElement? p, string name) => p?.Elements().Where(x => x.Name.LocalName == name) ?? [];
        static XElement? One(XElement? p, params string[] path) { foreach (var n in path) p = p?.Elements().FirstOrDefault(x => x.Name.LocalName == n); return p; }
        static decimal Amt(XElement? e) => e is null ? 0 : decimal.Parse(e.Value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture);
        static DateOnly? Day(XElement? e)
        {
            var v = One(e, "Dt")?.Value ?? One(e, "DtTm")?.Value;
            return v is not null && DateTime.TryParse(v.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t) ? DateOnly.FromDateTime(t) : null;
        }
        var stmts = doc.Descendants().Where(x => x.Name.LocalName == "Stmt").ToList();
        if (stmts.Count == 0) throw new InvalidDataException("Die CAMT-Datei enthält keinen Kontoauszug (Stmt).");
        var res = new List<BankZeile>(); var notes = new List<string>(); decimal? open = null, close = null; var konto = ""; var pending = 0;
        foreach (var st in stmts)
        {
            if (konto.Length == 0) konto = One(st, "Acct", "Id", "IBAN")?.Value.Trim() ?? One(st, "Acct", "Id", "Othr", "Id")?.Value.Trim() ?? "";
            foreach (var bal in E(st, "Bal"))
            {
                var code = One(bal, "Tp", "CdOrPrtry", "Cd")?.Value.Trim(); var v = Amt(One(bal, "Amt")); if (One(bal, "CdtDbtInd")?.Value.Trim() == "DBIT") v = -v;
                if (code is "OPBD" or "PRCD") open ??= v; // Anfangssaldo des ersten Auszugs
                if (code == "CLBD") close = v;             // Endsaldo des letzten Auszugs
            }
            foreach (var n in E(st, "Ntry"))
            {
                var status = One(n, "Sts", "Cd")?.Value.Trim() ?? One(n, "Sts")?.Value.Trim() ?? "BOOK";
                if (status != "BOOK") { pending++; continue; }
                var sign = One(n, "CdtDbtInd")?.Value.Trim() == "DBIT" ? -1 : 1;
                // Storno (RvslInd = true): Die Richtung steht nach ISO 20022 schon in CdtDbtInd (DBIT + Storno = eine frühere Gutschrift wird zurückgebucht) – nicht noch einmal drehen
                var storno = string.Equals(One(n, "RvslInd")?.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase);
                var total = Amt(One(n, "Amt")) * sign; var ccy = One(n, "Amt")?.Attribute("Ccy")?.Value ?? "EUR";
                var date = Day(One(n, "BookgDt")) ?? Day(One(n, "ValDt")) ?? throw new InvalidDataException("Ein Umsatz der CAMT-Datei hat kein Buchungsdatum.");
                var info = One(n, "AddtlNtryInf")?.Value.Trim() ?? "";
                var txs = E(n, "NtryDtls").SelectMany(d => E(d, "TxDtls")).ToList();
                BankZeile Row(XElement? tx, decimal amount)
                {
                    var parties = One(tx, "RltdPties");
                    string Name(string who) => One(parties, who, "Nm")?.Value.Trim() ?? One(parties, who, "Pty", "Nm")?.Value.Trim() ?? "";
                    var other = amount >= 0 ? Name("Dbtr") : Name("Cdtr"); if (other.Length == 0) other = amount >= 0 ? Name("UltmtDbtr") : Name("UltmtCdtr");
                    var iban = One(parties, amount >= 0 ? "DbtrAcct" : "CdtrAcct", "Id", "IBAN")?.Value.Trim() ?? "";
                    var purpose = string.Join(" ", E(One(tx, "RmtInf"), "Ustrd").Select(x => x.Value.Trim()).Where(x => x.Length > 0));
                    if (purpose.Length == 0) purpose = One(tx, "RmtInf", "Strd", "CdtrRefInf", "Ref")?.Value.Trim() ?? "";
                    if (purpose.Length == 0) purpose = One(tx, "AddtlTxInf")?.Value.Trim() ?? info;
                    if (storno && !purpose.StartsWith("Storno", StringComparison.OrdinalIgnoreCase)) purpose = ("Storno " + purpose).Trim();
                    var e2e = One(tx, "Refs", "EndToEndId")?.Value.Trim() ?? ""; if (e2e == "NOTPROVIDED") e2e = "";
                    return new BankZeile(date, amount, other, Regex.Replace(purpose, @"\s+", " "), iban, e2e.Length > 0 ? e2e : One(n, "AcctSvcrRef")?.Value.Trim() ?? "", ccy);
                }
                // Sammelbuchung: mehrere Einzelumsätze mit eigenem Betrag – nur aufteilen, wenn die Einzelbeträge die Summe ergeben
                var parts = txs.Select(tx => (tx, a: One(tx, "Amt") ?? One(tx, "AmtDtls", "TxAmt", "Amt"))).ToList();
                if (txs.Count > 1 && parts.All(p => p.a is not null) && parts.Sum(p => Amt(p.a)) == Math.Abs(total))
                    foreach (var (tx, a) in parts) res.Add(Row(tx, Amt(a) * Math.Sign(total)));
                else res.Add(Row(txs.FirstOrDefault(), total));
            }
        }
        if (pending > 0) notes.Add($"{pending} vorgemerkte Umsätze (noch nicht gebucht) wurden ausgelassen.");
        if (res.Select(r => r.Waehrung).Distinct().Count() > 1 || res.Any(r => r.Waehrung != "EUR")) notes.Add("Der Auszug enthält Beträge in fremder Währung – sie werden mit ihrem Zahlenwert übernommen, nicht umgerechnet.");
        return new BankAuszug("CAMT.053", konto, res, open, close, notes);
    }

    // ---------------------------------------------------------------- MT940 (SWIFT)
    [GeneratedRegex(@"(?m)^:61:")] private static partial Regex Tag61();
    [GeneratedRegex(@"^(\d{6})(\d{4})?(R?[CD])([A-Z])?(\d{1,12},\d{0,2})")] private static partial Regex Line61();
    [GeneratedRegex(@"^([CD])(\d{6})([A-Z]{3})(\d{1,12},\d{0,2})")] private static partial Regex Balance();
    [GeneratedRegex(@"(EREF|KREF|MREF|CRED|DEBT|SVWZ|ABWA|ABWE|IBAN|BIC|COAM|OAMT)\+")] private static partial Regex SepaTag();

    static decimal MtAmount(string s) => decimal.Parse(s.TrimEnd(',').Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture);
    static DateOnly MtDate(string yymmdd)
    {
        var yy = int.Parse(yymmdd[..2], CultureInfo.InvariantCulture);
        return new DateOnly(yy >= 80 ? 1900 + yy : 2000 + yy, int.Parse(yymmdd[2..4], CultureInfo.InvariantCulture), int.Parse(yymmdd[4..6], CultureInfo.InvariantCulture));
    }

    public static BankAuszug Mt940(string text)
    {
        // Felder einsammeln: eine Zeile, die mit „:NN:“ oder „:NNX:“ beginnt, startet ein Feld; Folgezeilen gehören dazu
        var fields = new List<(string Tag, string Value)>();
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line is "-" or "") continue;
            var m = Regex.Match(line, @"^:(\d{2}[A-Z]?):(.*)$");
            if (m.Success) fields.Add((m.Groups[1].Value, m.Groups[2].Value));
            else if (fields.Count > 0 && !line.StartsWith("{", StringComparison.Ordinal) && !line.StartsWith("-}", StringComparison.Ordinal)) fields[^1] = (fields[^1].Tag, fields[^1].Value + "\n" + line);
        }
        var res = new List<BankZeile>(); var notes = new List<string>(); decimal? open = null, close = null; var konto = ""; var ccy = "EUR";
        for (var i = 0; i < fields.Count; i++)
        {
            var (tag, val) = fields[i];
            if (tag == "25" && konto.Length == 0) konto = val.Split('\n')[0].Trim();
            else if (tag is "60F" or "60M" && Balance().Match(val) is { Success: true } b0) { open ??= MtAmount(b0.Groups[4].Value) * (b0.Groups[1].Value == "D" ? -1 : 1); ccy = b0.Groups[3].Value; }
            else if (tag is "62F" or "62M" && Balance().Match(val) is { Success: true } b1) close = MtAmount(b1.Groups[4].Value) * (b1.Groups[1].Value == "D" ? -1 : 1);
            else if (tag == "61")
            {
                var m = Line61().Match(val.Split('\n')[0]);
                if (!m.Success) throw new InvalidDataException("Eine Umsatzzeile (:61:) der MT940-Datei ist nicht lesbar: " + val.Split('\n')[0]);
                var valuta = MtDate(m.Groups[1].Value); var date = valuta;
                if (m.Groups[2].Success)
                {
                    // Buchungstag nur als MMTT: Jahr vom Valutadatum, Jahreswechsel berücksichtigen (Valuta 30.12., Buchung 02.01. → Folgejahr)
                    int mm = int.Parse(m.Groups[2].Value[..2], CultureInfo.InvariantCulture), dd = int.Parse(m.Groups[2].Value[2..], CultureInfo.InvariantCulture);
                    var year = valuta.Year + (valuta.Month == 12 && mm == 1 ? 1 : valuta.Month == 1 && mm == 12 ? -1 : 0);
                    try { date = new DateOnly(year, mm, dd); } catch (ArgumentOutOfRangeException) { date = valuta; }
                }
                var kind = m.Groups[3].Value; var amount = MtAmount(m.Groups[5].Value);
                // C = Gutschrift, D = Belastung, RC = Storno einer Gutschrift (Belastung), RD = Storno einer Belastung (Gutschrift)
                amount *= kind is "C" or "RD" ? 1 : -1;
                var (other, purpose, iban, reference) = i + 1 < fields.Count && fields[i + 1].Tag == "86" ? Feld86(fields[i + 1].Value) : ("", "", "", "");
                if (kind.Length == 2 && !purpose.StartsWith("Storno", StringComparison.OrdinalIgnoreCase)) purpose = ("Storno " + purpose).Trim();
                res.Add(new BankZeile(date, amount, other, purpose, iban, reference, ccy));
            }
        }
        if (res.Count == 0 && open is null) throw new InvalidDataException("Die MT940-Datei enthält keine Umsätze.");
        return new BankAuszug("MT940", konto, res, open, close, notes);
    }

    /// <summary>Feld :86: – strukturiert (deutsche Banken: Geschäftsvorfallcode, dann ?20–?29 und ?60–?63 Verwendungszweck, ?32/?33 Name, ?31 Konto/IBAN, ?30 BLZ/BIC)
    /// oder unstrukturierter Text. Im Verwendungszweck stehen SEPA-Angaben als EREF+/SVWZ+ … – als Zweck zählt der Teil nach SVWZ+.</summary>
    static (string Other, string Purpose, string Iban, string Reference) Feld86(string val)
    {
        var flat = val.Replace("\n", "");
        string purpose, other = "", iban = "";
        if (flat.Length > 4 && Regex.IsMatch(flat, @"^\d{3}\?"))
        {
            var subs = new Dictionary<int, string>();
            foreach (Match m in Regex.Matches(flat[3..], @"\?(\d{2})([^?]*)")) subs[int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)] = m.Groups[2].Value;
            purpose = string.Concat(Enumerable.Range(20, 10).Concat(Enumerable.Range(60, 4)).Select(k => subs.GetValueOrDefault(k, "")));
            other = (subs.GetValueOrDefault(32, "") + subs.GetValueOrDefault(33, "")).Trim();
            iban = subs.GetValueOrDefault(31, "").Trim();
            if (purpose.Length == 0) purpose = subs.GetValueOrDefault(0, "");
        }
        else purpose = val.Replace("\n", " ");
        var reference = "";
        var tags = SepaTag().Matches(purpose);
        if (tags.Count > 0)
        {
            var parts = new Dictionary<string, string>();
            for (var k = 0; k < tags.Count; k++)
            {
                var start = tags[k].Index + tags[k].Length; var end = k + 1 < tags.Count ? tags[k + 1].Index : purpose.Length;
                parts[tags[k].Groups[1].Value] = purpose[start..end].Trim();
            }
            reference = parts.GetValueOrDefault("EREF", "");
            if (iban.Length == 0) iban = parts.GetValueOrDefault("IBAN", "");
            var before = purpose[..tags[0].Index].Trim();
            purpose = parts.TryGetValue("SVWZ", out var svwz) ? svwz : before;
        }
        return (other, Regex.Replace(purpose, @"\s+", " ").Trim(), iban.Replace(" ", ""), reference is "NOTPROVIDED" ? "" : reference);
    }

    // ---------------------------------------------------------------- Übernahme ins Haushaltsbuch
    /// <summary>Auszugszeilen als Buchungen mit Kategorie-Vorschlag (Gelerntes zuerst, dann Stichwörter).</summary>
    public static List<Entry> Buchungen(IEnumerable<BankZeile> zeilen, CategoryMemory? memory) =>
        [.. zeilen.Select(z => new Entry(z.Datum, z.Text, z.Betrag, z.Betrag > 0 ? "Einnahmen" : FinanceExtra.Guess(z.Text, memory)))];

    static string Norm(string s) => Regex.Replace(s.ToLowerInvariant(), @"[^a-z0-9äöüß]+", "");

    /// <summary>Dubletten erkennen: gleiches Datum und gleicher Betrag und derselbe Text – auch wenn Leerzeichen, Groß-/Kleinschreibung oder Satzzeichen abweichen
    /// oder der eine Text den anderen enthält (CSV und CAMT derselben Bank schneiden den Zweck verschieden ab). Auch doppelte Zeilen innerhalb der Datei.</summary>
    public static List<(Entry Entry, bool Duplicate)> Vorschau(IEnumerable<Entry> existing, IEnumerable<Entry> incoming)
    {
        var have = existing.GroupBy(e => (e.Date, e.Amount)).ToDictionary(g => g.Key, g => g.Select(e => Norm(e.Text)).ToList());
        var res = new List<(Entry, bool)>();
        foreach (var e in incoming)
        {
            var n = Norm(e.Text);
            var list = have.TryGetValue((e.Date, e.Amount), out var l) ? l : have[(e.Date, e.Amount)] = [];
            var dup = list.Any(x => x == n || x.Length >= 8 && n.Length >= 8 && (x.Contains(n, StringComparison.Ordinal) || n.Contains(x, StringComparison.Ordinal)));
            if (!dup) list.Add(n);
            res.Add((e, dup));
        }
        return res;
    }

    /// <summary>Vorschlag „diese Zahlung gehört zu dieser Rechnung“. Sicher: Rechnungsnummer im Text und Betrag = offener Betrag. Teilzahlung: Nummer passt, Betrag kleiner.
    /// Nur Betrag: genau eine offene Rechnung hat diesen offenen Betrag (als „unsicher“ gekennzeichnet). Es wird nichts von selbst gebucht.</summary>
    public sealed record Zahlungsvorschlag(int Zeile, string Nummer, decimal Betrag, string Sicherheit, string Grund);

    public static List<Zahlungsvorschlag> Zahlungsvorschlaege(RechnungsBuch buch, IReadOnlyList<Entry> eingang)
    {
        var offen = buch.Vorgaenge.Where(v => v.Art == "rechnung" && !v.Entwurf && !v.Storniert && Rechnungen.Offen(v) > 0).ToDictionary(v => v.Nummer, v => Rechnungen.Offen(v));
        var res = new List<Zahlungsvorschlag>();
        for (var i = 0; i < eingang.Count; i++)
        {
            var e = eingang[i]; if (e.Amount <= 0) continue;
            var text = Norm(e.Text);
            var byNumber = offen.Where(o => o.Value > 0 && text.Contains(Norm(o.Key), StringComparison.Ordinal)).Select(o => o.Key).ToList();
            if (byNumber.Count == 1)
            {
                var nr = byNumber[0]; var rest = offen[nr];
                if (e.Amount == rest) { res.Add(new(i, nr, e.Amount, "sicher", $"Rechnungsnummer {nr} steht im Verwendungszweck und der Betrag stimmt.")); offen[nr] = 0; }
                else if (e.Amount < rest) { res.Add(new(i, nr, e.Amount, "teilzahlung", $"Rechnungsnummer {nr} steht im Verwendungszweck – {BizJson.Euro(e.Amount)} von {BizJson.Euro(rest)} offen.")); offen[nr] = rest - e.Amount; }
                else res.Add(new(i, nr, rest, "pruefen", $"Rechnungsnummer {nr} steht im Verwendungszweck, aber es kam mehr an ({BizJson.Euro(e.Amount)}) als offen ist ({BizJson.Euro(rest)})."));
                continue;
            }
            if (byNumber.Count > 1) continue; // mehrere Nummern in einem Text: nicht raten
            var byAmount = offen.Where(o => o.Value == e.Amount).Select(o => o.Key).ToList();
            if (byAmount.Count == 1) { res.Add(new(i, byAmount[0], e.Amount, "unsicher", $"Der Betrag passt genau zu Rechnung {byAmount[0]} – die Nummer steht aber nicht im Verwendungszweck.")); offen[byAmount[0]] = 0; }
        }
        return res;
    }
}
