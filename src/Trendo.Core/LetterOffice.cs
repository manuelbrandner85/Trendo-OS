using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Briefe wie im Kanzlei-Alltag (Profi 5 + Claude 5, 06.11.2026): Wiedervorlage je Akte, Fristen- und Wiedervorlage-Übersicht über alle Akten,
/// Postausgangsbuch (was wann wohin ging), Briefart erkennen mit Frist, Pflichtangaben nach DIN 5008 und typischen Anlagen. Vorbild: Kanzleisoftware
/// (Fristenbuch, Wiedervorlage, Postausgangsbuch) – nur Funktionsideen. Rechtliche Fristen sind Richtwerte: Trendo nennt die Grundlage, prüfen muss der Mensch.</summary>
public static partial class LetterOffice
{
    // ───────────── Postausgangsbuch ─────────────
    public sealed record PostEntry(DateTime Date, string Recipient, string Subject, string Channel, string CaseId = "", string File = "", string Proof = "");
    public static readonly (string Id, string Label)[] Channels = [("post", "Brief (Post)"), ("einschreiben", "Einschreiben"), ("email", "E-Mail"), ("fax", "Fax"), ("persoenlich", "Persönlich abgegeben"), ("portal", "Online-Portal")];

    public sealed class PostBook(string projectsFolder)
    {
        string FileOf => Path.Combine(projectsFolder, "Briefe", "Postausgangsbuch.json");
        public List<PostEntry> List()
        {
            try { return File.Exists(FileOf) ? JsonSerializer.Deserialize<List<PostEntry>>(File.ReadAllText(FileOf)) ?? [] : []; } catch (JsonException) { return []; }
        }
        public PostEntry Add(PostEntry e)
        {
            if (string.IsNullOrWhiteSpace(e.Recipient)) throw new ArgumentException("An wen ging der Brief? (Empfänger fehlt)");
            if (!Channels.Any(c => c.Id == e.Channel)) throw new ArgumentException("Unbekannter Versandweg.");
            var l = List(); l.Add(e with { Recipient = FirstLine(e.Recipient), Subject = e.Subject.Trim() });
            Directory.CreateDirectory(Path.GetDirectoryName(FileOf)!);
            File.WriteAllText(FileOf, JsonSerializer.Serialize(l.OrderByDescending(x => x.Date).ToList(), new JsonSerializerOptions { WriteIndented = true }));
            return l[^1];
        }
        /// <summary>Als CSV (Semikolon, für Excel) – wie das Postausgangsbuch einer Kanzlei.</summary>
        public string Csv()
        {
            static string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
            var sb = new StringBuilder("Datum;Empfänger;Betreff;Versandweg;Akte;Nachweis\r\n");
            foreach (var e in List()) sb.Append($"{e.Date:dd.MM.yyyy};{Q(e.Recipient)};{Q(e.Subject)};{Channels.FirstOrDefault(c => c.Id == e.Channel).Label};{Q(e.CaseId)};{Q(e.Proof)}\r\n");
            return sb.ToString();
        }
    }
    static string FirstLine(string s) => s.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

    // ───────────── Fristen und Wiedervorlagen über alle Akten ─────────────
    public sealed record AgendaItem(string CaseId, string CaseTitle, DateTime Date, string Kind, string Text, bool Overdue, bool Soon);

    /// <summary>Was ansteht: bestätigte Fristen (ungeprüfte nur mit Hinweis) und Wiedervorlagen – überfällige zuerst, dann nach Datum.</summary>
    public static List<AgendaItem> Agenda(IEnumerable<LetterCase> cases, DateTime today, int soonDays = 7)
    {
        var res = new List<AgendaItem>();
        foreach (var c in cases)
        {
            foreach (var d in c.Deadlines.Where(d => d.Status != Verification.Abgelehnt))
                res.Add(new(c.Id, c.Title, d.Date.Date, d.Status == Verification.Bestaetigt ? "Frist" : "Frist (ungeprüft)", d.Text, d.Date.Date < today.Date, d.Date.Date <= today.Date.AddDays(soonDays)));
            if (c.ResubmitOn is { } r)
            {
                var date = r.Date;
                res.Add(new(c.Id, c.Title, date, "Wiedervorlage", c.ResubmitNote.Length > 0 ? c.ResubmitNote : "Akte wieder ansehen", date < today.Date, date <= today.Date.AddDays(soonDays)));
            }
        }
        return [.. res.OrderByDescending(a => a.Overdue).ThenBy(a => a.Date)];
    }

    // ───────────── Briefart erkennen ─────────────
    /// <summary>Was eine Briefart braucht: Frist (Menge, Einheit, ab wann), Pflichtangaben, typische Anlagen, Hinweis mit Grundlage.</summary>
    public sealed record Kind(string Id, string Label, int Amount, string Unit, string From, string[] Need, string[] Attachments, string Hint);

    public static readonly Kind[] Kinds =
    [
        new("widerspruch", "Widerspruch gegen einen Bescheid", 1, "Monate", "Bekanntgabe des Bescheids",
            ["Aktenzeichen", "Datum des Bescheids", "Unterschrift"], ["Kopie des Bescheids"],
            "Widerspruchsfrist meist 1 Monat ab Bekanntgabe (§ 70 VwGO); ein per Post geschickter Bescheid gilt meist am 4. Tag nach Aufgabe zur Post als bekanntgegeben. Steht im Bescheid eine Rechtsbehelfsbelehrung, gilt die dort genannte Frist."),
        new("kuendigung", "Kündigung", 0, "", "",
            ["Vertrags- oder Kundennummer", "Kündigungstermin (z. B. „zum nächstmöglichen Zeitpunkt“)", "Bitte um schriftliche Bestätigung", "Unterschrift"], [],
            "Die Kündigungsfrist steht im Vertrag. Zugang nachweisbar machen (Einschreiben oder Fax mit Sendebericht) – entscheidend ist, wann die Kündigung ankommt."),
        new("widerruf", "Widerruf eines Kaufs oder Vertrags", 14, "Tage", "Erhalt der Ware bzw. Vertragsschluss",
            ["Bestell- oder Vertragsnummer", "Bestelldatum", "Erklärung „Hiermit widerrufe ich …“"], ["Rechnung oder Bestellbestätigung (Kopie)"],
            "Widerrufsfrist bei Online-Käufen 14 Tage ab Erhalt der Ware (§ 355 BGB); rechtzeitiges Absenden genügt."),
        new("mahnung", "Zahlungserinnerung / Mahnung", 14, "Tage", "heute",
            ["Rechnungsnummer und -datum", "Betrag", "Neues Zahlungsziel", "Bankverbindung"], ["Kopie der Rechnung"],
            "Ein klares Zahlungsziel setzen (üblich 10–14 Tage). Nach Fälligkeit und Mahnung ist der Schuldner im Verzug (§ 286 BGB)."),
        new("beschwerde", "Reklamation / Mangel", 14, "Tage", "heute",
            ["Bestell- oder Rechnungsnummer", "Kaufdatum", "Mangel genau beschrieben", "Gewünschte Lösung und Frist"], ["Kaufbeleg", "Fotos des Mangels"],
            "Für die Nacherfüllung eine angemessene Frist setzen (oft 14 Tage); Gewährleistung meist 2 Jahre ab Übergabe."),
        new("auskunft", "Auskunft nach DSGVO / Anfrage", 1, "Monate", "Zugang beim Empfänger",
            ["Name und Anschrift zur Identifizierung", "Konkrete Frage", "Gewünschter Antwortweg"], ["Kopie eines Ausweises nur auf Anforderung (geschwärzt)"],
            "Auskunft nach Art. 15 DSGVO: Antwort innerhalb eines Monats (Art. 12 Abs. 3). Wiedervorlage setzen, um nachzuhaken."),
        new("antrag", "Antrag an eine Behörde", 0, "", "",
            ["Was genau beantragt wird", "Begründung", "Persönliche Daten / Aktenzeichen (falls vorhanden)", "Unterschrift"], ["Nachweise laut Antragsformular"],
            "Viele Behörden haben Formulare – prüfen, ob eines vorgeschrieben ist. Eingang bestätigen lassen."),
        new("bewerbung", "Bewerbung", 0, "", "",
            ["Stelle und Kennziffer", "Kontaktdaten", "Frühester Eintritt"], ["Lebenslauf", "Zeugnisse"],
            "Unterlagen als ein PDF; Anlagen im Anschreiben aufzählen."),
    ];

    [GeneratedRegex(@"(?i)\b(widerspruch|widerspreche|lege ich widerspruch)\b")] private static partial Regex RxWiderspruch();
    [GeneratedRegex(@"(?i)\b(kündig\w*)\b")] private static partial Regex RxKuendigung();
    [GeneratedRegex(@"(?i)\b(widerruf\w*)\b")] private static partial Regex RxWiderruf();
    [GeneratedRegex(@"(?i)\b(mahnung|zahlungserinnerung|zahlungsziel|offene rechnung)\b")] private static partial Regex RxMahnung();
    [GeneratedRegex(@"(?i)\b(reklamation|mangel|defekt|nacherfüllung|gewährleistung)\b")] private static partial Regex RxMangel();
    [GeneratedRegex(@"(?i)\b(auskunft|art\.?\s*15|dsgvo)\b")] private static partial Regex RxAuskunft();
    [GeneratedRegex(@"(?i)\b(beantrage|antrag auf)\b")] private static partial Regex RxAntrag();
    [GeneratedRegex(@"(?i)\b(bewerbung|bewerbe mich|stellenanzeige)\b")] private static partial Regex RxBewerbung();

    /// <summary>Briefart aus Betreff und Text erkennen (Widerspruch vor Widerruf vor Kündigung …); null, wenn nichts passt.</summary>
    public static Kind? Detect(string subject, string body)
    {
        var t = subject + "\n" + body;
        var id = RxWiderspruch().IsMatch(t) ? "widerspruch" : RxWiderruf().IsMatch(t) ? "widerruf" : RxKuendigung().IsMatch(t) ? "kuendigung" : RxMahnung().IsMatch(t) ? "mahnung"
            : RxMangel().IsMatch(t) ? "beschwerde" : RxAuskunft().IsMatch(t) ? "auskunft" : RxBewerbung().IsMatch(t) ? "bewerbung" : RxAntrag().IsMatch(t) ? "antrag" : null;
        return id is null ? null : Kinds.First(k => k.Id == id);
    }

    /// <summary>Frist dieser Briefart ab einem Tag (Wochenende → nächster Werktag); null, wenn die Art keine feste Frist hat.</summary>
    public static (DateTime Date, bool Shifted)? KindDeadline(Kind k, DateTime from) => k.Amount <= 0 ? null : Letters.Deadline(from, k.Amount, k.Unit);

    /// <summary>Pflichtangaben nach DIN 5008 und der Briefart: was im Brief noch fehlt.</summary>
    public static List<string> Missing(LetterDraft d, Kind? k)
    {
        var m = new List<string>();
        if (string.IsNullOrWhiteSpace(d.SenderName) || string.IsNullOrWhiteSpace(d.SenderCity)) m.Add("Absender mit Anschrift (DIN 5008)");
        if (d.Recipient.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length < 3) m.Add("Vollständige Empfänger-Anschrift (Name, Straße, PLZ Ort)");
        if (string.IsNullOrWhiteSpace(d.Subject)) m.Add("Betreff");
        if (Regex.IsMatch(d.Subject + d.Body, @"\[[^\]]+\]")) m.Add("Platzhalter in eckigen Klammern ausfüllen");
        if (k is null) return m;
        var all = (d.Subject + "\n" + d.Body + "\n" + d.Reference).ToLowerInvariant();
        if (k.Need.Any(n => n.StartsWith("Aktenzeichen", StringComparison.Ordinal)) && string.IsNullOrWhiteSpace(d.Reference) && !Regex.IsMatch(all, @"\baz\.?\s*[\w/]") && !all.Contains("aktenzeichen")) m.Add("Aktenzeichen (Feld „Zeichen“ oder im Betreff)");
        if (k.Id is "kuendigung" && !all.Contains("bestätig")) m.Add("Bitte um schriftliche Bestätigung der Kündigung");
        if (k.Id is "kuendigung" && !Regex.IsMatch(all, @"zum \d|nächstmöglich")) m.Add("Kündigungstermin („zum …“ oder „zum nächstmöglichen Zeitpunkt“)");
        if (k.Attachments.Length > 0 && string.IsNullOrWhiteSpace(d.Attachments)) m.Add("Anlagen aufführen: " + string.Join(", ", k.Attachments));
        return m;
    }
}
