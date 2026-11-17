using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Trendo.Core.Business;

/// <summary>Angebot, Auftragsbestätigung, Rechnung oder Stornorechnung als PDF im Aufbau nach DIN 5008 (Anschriftfeld, Infoblock, Positionstabelle,
/// Summen je Steuersatz, Zahlungsbedingungen, Fußzeile mit Bank und Steuernummer). T-0694: alle Pflichtangaben nach § 14 Abs. 4 UStG stehen als Text im PDF.
/// Herkunft: Satz aus dem unfertigen Zweig sicherung/T-0675-business-a übernommen, um Logo, Steuernummer im Infoblock und ZUGFeRD-Vorbereitung erweitert.</summary>
public static class RechnungPdf
{
    const double Mm = 72 / 25.4;

    /// <summary>PDF schreiben (absturzsicher). Rückgabe: Seitenzahl.</summary>
    public static int Write(RechnungsBuch b, Vorgang v, string path, string? logo = null)
    {
        var (bytes, pages) = Build(b, v, logo, null);
        AtomicFile.WriteAllBytes(path, bytes); return pages;
    }

    /// <summary>PDF als Bytes; <paramref name="prepare"/> läuft vor dem Schreiben (ZUGFeRD schaltet hier PDF/A ein und hängt die XML-Datei an).</summary>
    public static (byte[] Bytes, int Pages) Build(RechnungsBuch b, Vorgang v, string? logo, Action<PdfDocument>? prepare)
    {
        var f = b.Firma; var name = Rechnungen.Name(v.Art); var s = Rechnungen.Summe(v);
        using var pdf = new BizPdf($"{name} {(v.Entwurf ? "(Entwurf)" : v.Nummer)}", f.Name)
        {
            Footer = string.Join("  ·  ", new[] { f.Name, f.Strasse, f.PlzOrt, f.Telefon, f.Email }.Where(x => x.Trim().Length > 0)) + "\n" +
                     string.Join("  ·  ", new[] { f.SteuerNr.Trim().Length > 0 ? "Steuernummer " + f.SteuerNr : "", f.UstId.Trim().Length > 0 ? "USt-IdNr. " + f.UstId : "", f.Bank, f.Iban.Trim().Length > 0 ? "IBAN " + f.Iban : "" }.Where(x => x.Length > 0)),
            Bottom = 272,
        };
        pdf.At(f.Name, 20, 15, 120, 16, true, pdf.Accent);
        pdf.At(string.Join(" · ", new[] { f.Strasse, f.PlzOrt }.Where(x => x.Trim().Length > 0)), 20, 23, 120, 9, false, BizPdf.Grey);
        if (!string.IsNullOrWhiteSpace(logo) && File.Exists(logo) && Path.GetExtension(logo).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg")
            try
            {
                using var img = XImage.FromFile(logo);
                var h = 18.0; var w = Math.Min(50, h * img.PixelWidth / Math.Max(1, img.PixelHeight)); h = w * img.PixelHeight / Math.Max(1, img.PixelWidth);
                pdf.G.DrawImage(img, (190 - w) * Mm, 12 * Mm, w * Mm, h * Mm);
            }
            catch (Exception e) when (e is IOException or InvalidOperationException or ArgumentException or NotSupportedException) { /* Logo nicht lesbar: Rechnung ohne Logo */ }
        pdf.At(string.Join(" · ", new[] { f.Name, f.Strasse, f.PlzOrt }.Where(x => x.Trim().Length > 0)), 25, 47, 85, 7, false, BizPdf.Grey);
        var y = 52.0; foreach (var l in v.KundeAnschrift.Split('\n')) { pdf.At(l, 25, y, 85, 10.5); y += BizPdf.LineH(10.5); }
        var info = new List<(string, string)> { ($"{name}snummer", v.Entwurf ? "Entwurf" : v.Nummer), ("Datum", BizJson.TextOf(v.Datum)) };
        if (v.Leistung.Trim().Length > 0) info.Add(("Leistungsdatum", v.Leistung.Trim()));
        if (v.KundeNummer.Trim().Length > 0) info.Add(("Kundennummer", v.KundeNummer));
        if (v.Herkunft.Length > 0) info.Add((v.Art == "storno" ? "Zu Rechnung" : "Bezug", v.Herkunft));
        if (v.Art == "angebot" && BizJson.Datum(v.Datum) is { } ad) info.Add(("Gültig bis", BizJson.Text(ad.AddDays(v.GueltigTage))));
        if (f.SteuerNr.Trim().Length > 0) info.Add(("Steuernummer", f.SteuerNr.Trim()));
        if (f.UstId.Trim().Length > 0) info.Add(("USt-IdNr.", f.UstId.Trim()));
        y = 50; foreach (var (k, val) in info) { pdf.At(k, 125, y, 32, 9, false, BizPdf.Grey); pdf.At(val, 157, y, 33, 9, true); y += BizPdf.LineH(9) + 0.4; }
        if (v.Entwurf) pdf.At("ENTWURF – nicht versenden", 125, 40, 65, 9, true, XColor.FromArgb(0xB4, 0x53, 0x09));

        pdf.Y = 100;
        pdf.Write(name + (v.Entwurf ? "" : " " + v.Nummer), 14, true, pdf.Accent); pdf.Y += 3;
        pdf.Write("Sehr geehrte Damen und Herren,", 10); pdf.Y += 1.5;
        if (v.Kopftext.Trim().Length > 0) { pdf.Write(v.Kopftext.Trim(), 10); pdf.Y += 3; }

        var rabatt = v.Positionen.Any(p => p.Rabatt != 0);
        var ust = !v.Kleinunternehmer;
        var head = new List<string> { "Pos.", "Bezeichnung", "Menge", "Einheit", "Einzelpreis" }; var w2 = new List<double> { 10, 0, 16, 16, 24 }; var al = "llrlr";
        if (rabatt) { head.Add("Rabatt"); w2.Add(14); al += "r"; }
        if (ust) { head.Add("USt"); w2.Add(11); al += "r"; }
        head.Add("Gesamt"); w2.Add(26); al += "r";
        w2[1] = 170 - w2.Sum();
        var rows = v.Positionen.Select((p, i) =>
        {
            var r = new List<string> { (i + 1).ToString(), p.Text, BizJson.Num(p.Menge), p.Einheit, BizJson.Euro(p.Preis) };
            if (rabatt) r.Add(p.Rabatt == 0 ? "" : BizJson.Num(p.Rabatt) + " %");
            if (ust) r.Add(BizJson.Num(p.Ust) + " %");
            r.Add(BizJson.Euro(Rechnungen.PosNetto(p))); return r.ToArray();
        });
        pdf.Table([.. head], [.. w2], al, rows);
        pdf.Y += 2; pdf.Need(30);
        void Sum(string label, string val, bool bold = false) { pdf.Need(6); pdf.At(label, 100, pdf.Y, 60, 10, bold); pdf.At(val, 160, pdf.Y, 30, 10, bold, null, XStringFormats.TopRight); pdf.Y += BizPdf.LineH(10) + 0.8; }
        Sum("Summe netto", BizJson.Euro(s.Netto));
        if (ust) foreach (var g in s.JeSatz) Sum(g.Satz == 0 ? $"0 % USt auf {BizJson.Euro(g.Netto)}" : $"zzgl. {BizJson.Num(g.Satz)} % USt auf {BizJson.Euro(g.Netto)}", BizJson.Euro(g.Steuer));
        pdf.G.DrawLine(new XPen(BizPdf.Dark, 0.3 * Mm), 100 * Mm, pdf.Y * Mm, 190 * Mm, pdf.Y * Mm); pdf.Y += 1;
        Sum(v.Art == "angebot" ? "Angebotssumme" : "Gesamtbetrag", BizJson.Euro(s.Brutto), true);
        if (v.Bezahlt > 0 && v.Art == "rechnung") { Sum("Bereits bezahlt", "− " + BizJson.Euro(v.Bezahlt)); Sum("Noch offen", BizJson.Euro(Rechnungen.Offen(v)), true); }
        pdf.Y += 4;
        if (v.Kleinunternehmer) pdf.Write(Rechnungen.KleinunternehmerHinweis, 9.5);
        else if (v.Positionen.Any(p => p.Ust == 0) && v.Befreiung.Trim().Length > 0) pdf.Write(v.Befreiung.Trim(), 9.5);
        if (v.Art == "rechnung" && Rechnungen.Faellig(v) is { } due)
            pdf.Write($"Bitte überweisen Sie den Betrag bis zum {BizJson.Text(due)} ohne Abzug" + (f.Iban.Trim().Length > 0 ? $" auf das Konto IBAN {f.Iban}" + (f.Bank.Trim().Length > 0 ? $" ({f.Bank})" : "") : "") +
                (v.Entwurf ? "." : $" unter Angabe der Rechnungsnummer {v.Nummer}."), 9.5);
        if (v.Art == "angebot") pdf.Write($"Dieses Angebot gilt {v.GueltigTage} Tage. Wir freuen uns auf Ihren Auftrag.", 9.5);
        if (v.Fusstext.Trim().Length > 0) { pdf.Y += 2; pdf.Write(v.Fusstext.Trim(), 9.5); }
        pdf.Y += 4; pdf.Write("Mit freundlichen Grüßen", 10); pdf.Write(f.Name, 10);
        var bytes = pdf.ToBytes(prepare);
        return (bytes, pdf.Pages);
    }
}
