using System.Globalization;

namespace Trendo.Core.Office;

/// <summary>T-0228 G1: Vorlagen für die Startseite von Trendo Tabelle (neutrale Beispieldaten, kein fester Nutzer).</summary>
public static class SheetTemplates
{
    public sealed record Info(string Id, string Name, string Icon, string Hint);

    public static readonly IReadOnlyList<Info> Builtin =
    [
        new("leer", "Leere Mappe", "▦", "Ein leeres Blatt"),
        new("haushalt", "Haushaltsbuch", "🏠", "Einnahmen, Ausgaben und was übrig bleibt"),
        new("rechnung", "Rechnung", "🧾", "Positionen, Netto, MwSt und Brutto"),
        new("stunden", "Stundenzettel", "⏱", "Arbeitszeit je Tag mit Summe"),
        new("einkauf", "Einkaufsliste", "🛒", "Menge, Preis und Gesamtsumme"),
    ];

    public static SheetDoc Create(string id)
    {
        var d = new SheetDoc(); var s = d.Sheets[0];
        void Head(params string[] t) { for (var c = 0; c < t.Length; c++) { var cell = s.GetOrAdd(0, c); cell.Raw = t[c]; cell.Bold = true; cell.Fill = "#FFDCE8FB"; } }
        void Row(int r, params string[] t) { for (var c = 0; c < t.Length; c++) if (t[c].Length > 0) s.Set(r, c, t[c]); }
        void Fmt(NumFmt f, int col, int from, int to) { for (var r = from; r <= to; r++) s.GetOrAdd(r, col).Format = f; }
        if (id != "leer") s.FreezeRows = 1; // Kopfzeile bleibt beim Blättern stehen
        switch (id)
        {
            case "haushalt":
                s.Name = "Haushalt"; Head("Posten", "Art", "Betrag");
                Row(1, "Gehalt", "Einnahme", "2450"); Row(2, "Miete", "Ausgabe", "780"); Row(3, "Strom", "Ausgabe", "65");
                Row(4, "Lebensmittel", "Ausgabe", "320"); Row(5, "Handy", "Ausgabe", "15"); Row(6, "Versicherungen", "Ausgabe", "110");
                Row(8, "Einnahmen", "", "=SUMMEWENN(B2:B7;\"Einnahme\";C2:C7)"); Row(9, "Ausgaben", "", "=SUMMEWENN(B2:B7;\"Ausgabe\";C2:C7)");
                Row(10, "Bleibt übrig", "", "=C9-C10"); s.GetOrAdd(10, 0).Bold = s.GetOrAdd(10, 2).Bold = true;
                Fmt(NumFmt.Euro, 2, 1, 10); s.ColWidths[0] = 18; s.ColWidths[1] = 12; s.ColWidths[2] = 14;
                break;
            case "rechnung":
                s.Name = "Rechnung"; Head("Pos.", "Beschreibung", "Menge", "Einzelpreis", "Gesamt");
                Row(1, "1", "Beratung (Stunden)", "3", "85", "=C2*D2"); Row(2, "2", "Material", "1", "49,90", "=C3*D3"); Row(3, "3", "Anfahrt", "1", "25", "=C4*D4");
                Row(5, "", "", "", "Netto", "=SUMME(E2:E4)"); Row(6, "", "", "", "MwSt 19 %", "=RUNDEN(E6*0,19;2)"); Row(7, "", "", "", "Brutto", "=E6+E7");
                s.GetOrAdd(7, 3).Bold = s.GetOrAdd(7, 4).Bold = true;
                Fmt(NumFmt.Euro, 3, 1, 3); Fmt(NumFmt.Euro, 4, 1, 7); s.ColWidths[1] = 24; s.ColWidths[3] = 13; s.ColWidths[4] = 13;
                break;
            case "stunden":
                s.Name = "Stunden"; Head("Datum", "Beginn", "Ende", "Pause (min)", "Stunden");
                var monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
                for (var i = 0; i < 5; i++)
                {
                    var r = i + 1;
                    Row(r, monday.AddDays(i).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), "8", "16,5", "30", $"=C{r + 1}-B{r + 1}-D{r + 1}/60");
                    s.GetOrAdd(r, 0).Format = NumFmt.Datum;
                }
                Row(7, "Woche", "", "", "", "=SUMME(E2:E6)"); s.GetOrAdd(7, 0).Bold = s.GetOrAdd(7, 4).Bold = true;
                Fmt(NumFmt.Zahl, 4, 1, 7); s.ColWidths[0] = 12; s.ColWidths[3] = 12;
                break;
            case "einkauf":
                s.Name = "Einkauf"; Head("Artikel", "Menge", "Preis", "Summe");
                Row(1, "Brot", "1", "2,49", "=B2*C2"); Row(2, "Milch", "2", "1,15", "=B3*C3"); Row(3, "Äpfel (kg)", "1,5", "2,99", "=B4*C4");
                Row(4, "Kaffee", "1", "6,99", "=B5*C5"); Row(6, "Gesamt", "", "", "=SUMME(D2:D5)"); s.GetOrAdd(6, 0).Bold = s.GetOrAdd(6, 3).Bold = true;
                Fmt(NumFmt.Euro, 2, 1, 4); Fmt(NumFmt.Euro, 3, 1, 6); s.ColWidths[0] = 16;
                break;
        }
        return d;
    }
}

/// <summary>E1: Ausfüllen wie mit dem Ausfüllkästchen – erkennt Zahlenreihen, Wochentage, Monate, „Text 1, Text 2“ und Datumsreihen.</summary>
public static class SheetSeries
{
    static readonly string[] Days = ["Montag", "Dienstag", "Mittwoch", "Donnerstag", "Freitag", "Samstag", "Sonntag"];
    static readonly string[] DaysShort = ["Mo", "Di", "Mi", "Do", "Fr", "Sa", "So"];
    static readonly string[] Months = ["Januar", "Februar", "März", "April", "Mai", "Juni", "Juli", "August", "September", "Oktober", "November", "Dezember"];
    static readonly string[] MonthsShort = ["Jan", "Feb", "Mär", "Apr", "Mai", "Jun", "Jul", "Aug", "Sep", "Okt", "Nov", "Dez"];

    /// <summary>Setzt die Reihe aus den gegebenen Startwerten um <paramref name="count"/> weitere Werte fort.</summary>
    public static IReadOnlyList<string> Continue(IReadOnlyList<string> seed, int count)
    {
        var res = new List<string>();
        if (seed.Count == 0 || count <= 0) return res;
        foreach (var list in new[] { Days, DaysShort, Months, MonthsShort })
        {
            var idx = seed.Select(v => Array.FindIndex(list, x => x.Equals(v.Trim(), StringComparison.OrdinalIgnoreCase))).ToList();
            if (idx.All(i => i >= 0))
            {
                var step = idx.Count > 1 ? idx[1] - idx[0] : 1;
                for (var k = 1; k <= count; k++) res.Add(list[((idx[^1] + step * k) % list.Length + list.Length) % list.Length]);
                return res;
            }
        }
        var nums = seed.Select(v => SheetFormat.TryNumber(v, out var d) ? d : (double?)null).ToList();
        if (nums.All(n => n is not null))
        {
            var step = nums.Count > 1 ? nums[^1]!.Value - nums[^2]!.Value : 1;
            for (var k = 1; k <= count; k++) res.Add(SheetFormat.General(nums[^1]!.Value + step * k));
            return res;
        }
        var dates = seed.Select(v => DateTime.TryParseExact(v.Trim(), ["dd.MM.yyyy", "d.M.yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : (DateTime?)null).ToList();
        if (dates.All(d => d is not null))
        {
            var step = dates.Count > 1 ? (dates[^1]!.Value - dates[^2]!.Value).Days : 1;
            for (var k = 1; k <= count; k++) res.Add(dates[^1]!.Value.AddDays(step * k).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
            return res;
        }
        // „Raum 1“ → „Raum 2“
        var last = seed[^1]; var j = last.Length; while (j > 0 && char.IsDigit(last[j - 1])) j--;
        if (j < last.Length && j > 0)
        {
            var n = long.Parse(last[j..], CultureInfo.InvariantCulture);
            for (var k = 1; k <= count; k++) res.Add(last[..j] + (n + k).ToString(CultureInfo.InvariantCulture));
            return res;
        }
        for (var k = 0; k < count; k++) res.Add(seed[k % seed.Count]);
        return res;
    }
}
