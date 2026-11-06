using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Briefe wie im Kanzlei-Alltag (Profi 5 + Claude 5): Briefart erkennen, Fristen, Pflichtangaben, Wiedervorlage, Fristen-Übersicht, Postausgangsbuch.</summary>
public class LetterOfficeTests
{
    [Fact(DisplayName = "STUDIO-LETTER-01: Briefart erkennen – Widerspruch, Widerruf, Kündigung, Mahnung, Reklamation, DSGVO-Auskunft; Frist mit Wochenend-Verschiebung; ohne Treffer nichts")]
    public void Detect_kind()
    {
        Assert.Equal("widerspruch", LetterOffice.Detect("Widerspruch gegen den Bescheid vom 03.11.2026", "")!.Id);
        Assert.Equal("widerruf", LetterOffice.Detect("Bestellung 4711", "hiermit widerrufe ich den Vertrag")!.Id);
        Assert.Equal("kuendigung", LetterOffice.Detect("Kündigung meines Vertrags", "")!.Id);
        Assert.Equal("mahnung", LetterOffice.Detect("Zahlungserinnerung", "")!.Id);
        Assert.Equal("beschwerde", LetterOffice.Detect("", "Das Gerät ist defekt, ich bitte um Nacherfüllung")!.Id);
        Assert.Equal("auskunft", LetterOffice.Detect("Auskunft nach Art. 15 DSGVO", "")!.Id);
        Assert.Null(LetterOffice.Detect("Grüße aus dem Urlaub", "Liebe Erika"));
        var w = LetterOffice.Kinds.First(k => k.Id == "widerspruch");
        Assert.Equal(new DateTime(2026, 12, 7), LetterOffice.KindDeadline(w, new DateTime(2026, 11, 6))!.Value.Date); // 06.12. ist Sonntag → Montag 07.12.
        Assert.Null(LetterOffice.KindDeadline(LetterOffice.Kinds.First(k => k.Id == "kuendigung"), DateTime.Today));
    }

    [Fact(DisplayName = "STUDIO-LETTER-01: Pflichtangaben nach DIN 5008 und Briefart – Absender, Anschrift, Betreff, Platzhalter, Aktenzeichen, Kündigungstermin und Bestätigung, Anlagen")]
    public void Missing()
    {
        var d = new LetterDraft { Subject = "Kündigung meines Vertrags [Vertragsnummer]", Body = "hiermit kündige ich." };
        var k = LetterOffice.Detect(d.Subject, d.Body);
        var m = LetterOffice.Missing(d, k);
        Assert.Contains(m, x => x.StartsWith("Absender")); Assert.Contains(m, x => x.StartsWith("Vollständige Empfänger")); Assert.Contains(m, x => x.StartsWith("Platzhalter"));
        Assert.Contains(m, x => x.StartsWith("Bitte um schriftliche Bestätigung")); Assert.Contains(m, x => x.StartsWith("Kündigungstermin"));
        var ok = new LetterDraft { SenderName = "Erika Muster", SenderCity = "12345 Musterstadt", Recipient = "Stadtwerke\nMusterweg 1\n12345 Musterstadt", Subject = "Kündigung Vertrag 4711",
            Body = "hiermit kündige ich zum nächstmöglichen Zeitpunkt. Bitte bestätigen Sie mir die Kündigung schriftlich." };
        Assert.Empty(LetterOffice.Missing(ok, LetterOffice.Detect(ok.Subject, ok.Body)));
        var ws = new LetterDraft { SenderName = "Erika Muster", SenderCity = "x", Recipient = "Amt\nWeg 1\n1 Ort", Subject = "Widerspruch gegen den Bescheid", Body = "gegen den Bescheid lege ich Widerspruch ein." };
        var wm = LetterOffice.Missing(ws, LetterOffice.Detect(ws.Subject, ws.Body));
        Assert.Contains(wm, x => x.StartsWith("Aktenzeichen")); Assert.Contains(wm, x => x.Contains("Kopie des Bescheids"));
        ws.Reference = "Az. 12-345"; Assert.DoesNotContain(LetterOffice.Missing(ws, LetterOffice.Detect(ws.Subject, ws.Body)), x => x.StartsWith("Aktenzeichen"));
        Assert.Contains(LetterOffice.Missing(new LetterDraft { Subject = "Widerspruch – Pflanzenbazar", Body = "Widerspruch" }, LetterOffice.Kinds[0]), x => x.StartsWith("Aktenzeichen")); // „baz“ in einem Wort ist kein Aktenzeichen
    }

    [Fact(DisplayName = "STUDIO-LETTER-01: Wiedervorlage und Fristen über alle Akten – überfällige zuerst, verworfene Fristen fehlen; Postausgangsbuch mit Empfänger-Kurzform, Pflichtfeldern und CSV")]
    public void Agenda_and_postbook()
    {
        var today = new DateTime(2026, 11, 6);
        var a = new LetterCase { Id = "a", Title = "Widerspruch Rente" }; a.Deadlines.Add(new(today.AddDays(3), "Widerspruchsfrist", "Bescheid", Verification.Bestaetigt));
        a.Deadlines.Add(new(today.AddDays(1), "verworfen", "x", Verification.Abgelehnt));
        var b = new LetterCase { Id = "b", Title = "Kündigung Strom", ResubmitOn = today.AddDays(-2), ResubmitNote = "Bestätigung da?" };
        var ag = LetterOffice.Agenda([a, b], today);
        Assert.Equal(["Wiedervorlage", "Frist"], ag.Select(x => x.Kind));
        Assert.True(ag[0].Overdue); Assert.Equal("Bestätigung da?", ag[0].Text); Assert.All(ag, x => Assert.True(x.Soon));
        var dir = Path.Combine(Path.GetTempPath(), "trendo-post-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            var pb = new LetterOffice.PostBook(dir);
            Assert.Throws<ArgumentException>(() => pb.Add(new(today, " ", "x", "post")));
            Assert.Throws<ArgumentException>(() => pb.Add(new(today, "Amt", "x", "brieftaube")));
            pb.Add(new(today.AddDays(-1), "Stadtwerke\nMusterweg 1\n12345 Musterstadt", "Kündigung", "einschreiben", "b", "", "RR123456789DE"));
            pb.Add(new(today, "Rentenversicherung", "Widerspruch", "fax", "a"));
            var l = pb.List(); Assert.Equal(["Rentenversicherung", "Stadtwerke"], l.Select(x => x.Recipient));
            var csv = pb.Csv(); Assert.StartsWith("Datum;Empfänger;Betreff;Versandweg;Akte;Nachweis", csv); Assert.Contains("05.11.2026;\"Stadtwerke\";\"Kündigung\";Einschreiben;\"b\";\"RR123456789DE\"", csv);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
