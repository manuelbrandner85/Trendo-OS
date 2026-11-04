using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Trendo.Core;

/// <summary>Adressbuch – Vorbild-Funktionen (Google Kontakte, Outlook, Thunderbird; nur Funktionen, Clean-Room, 04.11.2026):
/// CSV im Google- und Outlook-Format hinein und hinaus, Telefonnummern einheitlich (+49 …) wie „Korrigieren und verwalten“,
/// Adressetiketten als PDF (Zweckform-Formate) wie der Etikettendruck in Word/Outlook.</summary>
public sealed partial class MailCenter
{
    static readonly string[] CsvHead = ["Name", "Vorname", "Nachname", "E-Mail", "Telefon", "Mobil", "Firma", "Straße", "Ort", "Geburtstag", "Gruppen", "Webseite", "Notiz"];

    static string Q(string s) => s.IndexOfAny([',', '"', '\n', '\r', ';']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    /// <summary>CSV mit Kopfzeile (UTF-8, Komma); Gruppen mit „ ::: “ getrennt wie bei Google Kontakte.</summary>
    public string ExportCsv(IEnumerable<Contact>? only = null)
    {
        var sb = new StringBuilder(string.Join(",", CsvHead)).Append("\r\n");
        foreach (var c in only ?? Contacts())
        {
            var parts = c.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string[] row = [c.Name, parts.Length > 1 ? string.Join(' ', parts[..^1]) : "", parts.Length > 1 ? parts[^1] : c.Name, c.Email, c.Phone, c.Mobile, c.Company, c.Street, c.City,
                c.Birthday?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "", string.Join(" ::: ", c.Groups ?? []), c.Website, c.Note];
            sb.Append(string.Join(",", row.Select(Q))).Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>CSV-Zeilen lesen: Trennzeichen Komma oder Semikolon (deutsches Outlook), Anführungszeichen mit Zeilenumbrüchen.</summary>
    public static List<List<string>> ParseCsv(string text)
    {
        var first = text.Split('\n')[0]; var sep = first.Count(c => c == ';') > first.Count(c => c == ',') ? ';' : ',';
        var rows = new List<List<string>>(); var row = new List<string>(); var cur = new StringBuilder(); var q = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (q) { if (ch == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { cur.Append('"'); i++; } else q = false; } else cur.Append(ch); continue; }
            if (ch == '"') q = true;
            else if (ch == sep) { row.Add(cur.ToString()); cur.Clear(); }
            else if (ch == '\n') { row.Add(cur.ToString().TrimEnd('\r')); cur.Clear(); if (row.Any(x => x.Length > 0)) rows.Add(row); row = []; }
            else cur.Append(ch);
        }
        row.Add(cur.ToString().TrimEnd('\r')); if (row.Any(x => x.Length > 0)) rows.Add(row);
        return rows;
    }

    /// <summary>CSV einlesen: Trendo-, Google- („E-mail 1 - Value“, „Phone 1 - Value“) und Outlook-Spalten („Vorname“, „E-Mail-Adresse“, „Mobiltelefon“ …).
    /// Schon vorhandene (gleicher Name und gleiche E-Mail) werden übersprungen. Gibt die Zahl neuer Kontakte zurück.</summary>
    public int ImportCsv(string csv)
    {
        var rows = ParseCsv(csv.TrimStart('﻿'));
        if (rows.Count < 2) throw new InvalidOperationException("In der CSV-Datei stehen keine Kontakte (erste Zeile = Spaltennamen).");
        var head = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Col(params string[] names) { foreach (var n in names) { var i = head.IndexOf(n.ToLowerInvariant()); if (i >= 0) return i; } foreach (var n in names) { var i = head.FindIndex(h => h.StartsWith(n.ToLowerInvariant(), StringComparison.Ordinal)); if (i >= 0) return i; } return -1; }
        int cName = Col("name", "anzeigename", "display name"), cFirst = Col("vorname", "first name", "given name"), cLast = Col("nachname", "last name", "family name"),
            cMail = Col("e-mail", "e-mail-adresse", "e-mail address", "e-mail 1 - value", "email"), cPhone = Col("telefon", "telefon (privat)", "home phone", "business phone", "telefon geschäftlich", "phone 1 - value"),
            cMobile = Col("mobil", "mobiltelefon", "mobile phone", "phone 2 - value"), cOrg = Col("firma", "company", "organization 1 - name", "organization name"),
            cStreet = Col("straße", "straße privat", "home street", "address 1 - street", "business street"), cCity = Col("ort", "ort privat", "home city", "address 1 - city", "business city"),
            cBday = Col("geburtstag", "birthday"), cGroups = Col("gruppen", "group membership", "kategorien", "categories"), cWeb = Col("webseite", "web page", "website 1 - value"), cNote = Col("notiz", "notizen", "notes");
        string G(List<string> r, int i) => i >= 0 && i < r.Count ? r[i].Trim() : "";
        var n = 0;
        foreach (var r in rows.Skip(1))
        {
            var name = G(r, cName); if (name.Length == 0) name = $"{G(r, cFirst)} {G(r, cLast)}".Trim();
            var mail = G(r, cMail); if (mail.Length > 0 && !ValidEmail(mail)) mail = "";
            if (name.Length == 0) name = mail;
            if (name.Length == 0 || _contacts.Any(c => c.Name == name && c.Email == mail)) continue;
            DateOnly? bday = DateOnly.TryParseExact(G(r, cBday), ["yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy", "M/d/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
            var groups = G(r, cGroups).Split([":::", ";", ","], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(g => g != "* myContacts" && g != "* starred").ToList();
            _contacts.Add(new Contact(Guid.NewGuid().ToString("N")[..8], name, mail, G(r, cPhone), G(r, cNote), G(r, cMobile), G(r, cOrg), G(r, cStreet), G(r, cCity), bday, groups, "", false, G(r, cWeb)));
            n++;
        }
        Save(); _audit.Append(Principal.LocalOwner.Name, "contacts.import.csv", $"{n}");
        return n;
    }

    /// <summary>Telefonnummer einheitlich international: „0171 1234567“ → „+49 171 1234567“, „0049 30 …“ → „+49 30 …“. Unklare Eingaben bleiben unverändert.</summary>
    public static string NormalizePhone(string phone, string country = "49")
    {
        var t = phone.Trim(); if (t.Length == 0) return t;
        var groups = Regex.Matches(t, @"\d+").Select(m => m.Value).ToList();
        if (groups.Count == 0 || groups.Sum(g => g.Length) < 7) return t;
        string cc; List<string> rest;
        if (t.StartsWith('+') || t.StartsWith("00", StringComparison.Ordinal))
        {
            var g0 = t.StartsWith('+') ? groups[0] : groups[0][2..];
            if (groups.Count == 1 || g0.Length > 3) { cc = g0.StartsWith(country, StringComparison.Ordinal) ? country : g0[..Math.Min(2, g0.Length)]; rest = [g0[cc.Length..], .. groups.Skip(1)]; }
            else { cc = g0; rest = [.. groups.Skip(1)]; }
        }
        else if (t.StartsWith('0')) { cc = country; rest = [groups[0][1..], .. groups.Skip(1)]; }
        else return t; // ohne Vorwahl: nicht raten
        rest = [.. rest.Where(x => x.Length > 0)];
        if (rest.Count == 0) return t;
        return rest.Count == 1 ? $"+{cc} {rest[0]}" : $"+{cc} {rest[0]} {string.Concat(rest.Skip(1))}";
    }

    /// <summary>Alle Nummern vereinheitlichen; gibt die Zahl geänderter Kontakte zurück.</summary>
    public int FixPhones(string country = "49")
    {
        var n = 0;
        for (var i = 0; i < _contacts.Count; i++)
        {
            var c = _contacts[i]; var p = NormalizePhone(c.Phone, country); var m = NormalizePhone(c.Mobile, country);
            if (p != c.Phone || m != c.Mobile) { _contacts[i] = c with { Phone = p, Mobile = m }; n++; }
        }
        if (n > 0) { Save(); _audit.Append(Principal.LocalOwner.Name, "contacts.phones", $"{n}"); }
        return n;
    }

    /// <summary>Etikettenbogen A4 wie Zweckform: Spalten, Zeilen, Etikett B × H, Rand oben/links, Abstand (mm).</summary>
    public sealed record LabelSheet(string Id, string Name, int Cols, int Rows, double W, double H, double Top, double Left, double GapX, double GapY);
    public static readonly LabelSheet[] LabelSheets =
    [
        new("3475", "70 × 36 mm, 24 je Bogen (z. B. Zweckform 3475)", 3, 8, 70, 36, 4.5, 0, 0, 0),
        new("3422", "70 × 35 mm, 24 je Bogen (z. B. Zweckform 3422)", 3, 8, 70, 35, 8.5, 0, 0, 0),
        new("l7163", "99,1 × 38,1 mm, 14 je Bogen (z. B. L7163)", 2, 7, 99.1, 38.1, 15.15, 4.65, 2.5, 0),
        new("3424", "105 × 48 mm, 12 je Bogen (z. B. Zweckform 3424)", 2, 6, 105, 48, 4.5, 0, 0, 0),
    ];

    /// <summary>Anschrift für ein Etikett: Name, Firma, Straße, Ort – nur gefüllte Zeilen.</summary>
    public static IReadOnlyList<string> LabelLines(Contact c) => new[] { c.Name, c.Company, c.Street, c.City }.Where(x => x.Trim().Length > 0).Select(x => x.Trim()).ToList();

    /// <summary>Etiketten als PDF; überspringt Kontakte ohne Anschrift, beginnt auf Wunsch bei Etikett <paramref name="startAt"/> (angebrochener Bogen). Gibt die Seitenzahl zurück.</summary>
    public static int WriteLabels(IEnumerable<Contact> contacts, string path, string sheet = "3475", int startAt = 1)
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Sys.PdfTools).TypeHandle);
        var s = LabelSheets.FirstOrDefault(x => x.Id == sheet) ?? LabelSheets[0];
        var list = contacts.Where(c => c.Street.Trim().Length > 0 && c.City.Trim().Length > 0).ToList();
        if (list.Count == 0) throw new InvalidOperationException("Keiner der Kontakte hat eine Anschrift (Straße und Ort).");
        var per = s.Cols * s.Rows; var skip = Math.Clamp(startAt, 1, per) - 1;
        using var pdf = new PdfDocument(); pdf.Info.Title = "Adressetiketten";
        const double Mm = 72 / 25.4; var font = new XFont("Arial", 10); var bold = new XFont("Arial", 10, XFontStyleEx.Bold);
        XGraphics? g = null; var pages = 0;
        for (var i = 0; i < list.Count; i++)
        {
            var slot = (i + skip) % per;
            if (slot == 0 || g is null) { g?.Dispose(); var page = pdf.AddPage(); page.Width = XUnit.FromMillimeter(210); page.Height = XUnit.FromMillimeter(297); g = XGraphics.FromPdfPage(page); pages++; }
            int col = slot % s.Cols, row = slot / s.Cols;
            var x = s.Left + col * (s.W + s.GapX) + 6; var y = s.Top + row * (s.H + s.GapY) + 6;
            var lines = LabelLines(list[i]);
            for (var l = 0; l < lines.Count && l < 5; l++) g.DrawString(lines[l], l == 0 ? bold : font, XBrushes.Black, x * Mm, (y + 4 + l * 5) * Mm);
        }
        g?.Dispose();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        pdf.Save(path);
        return pages;
    }
}
