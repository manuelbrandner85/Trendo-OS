using System.Text;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>N5/N9/N11/N18/N20: Notizbuch, Editor, Rechner, Zettel, Uhr – Kern.</summary>
public class HelferTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-helfer-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    [Theory(DisplayName = "N11: Rechner – Grundrechenarten, Vorrang, Prozent wie bisher")]
    [InlineData("(12,5 + 7) × 3", 58.5)]
    [InlineData("19 % × 250", 47.5)]
    [InlineData("-2^2", -4)]
    [InlineData("2^3^2", 512)]
    [InlineData("2^-1", 0.5)]
    public void Calc_basic(string e, double v) => Assert.Equal(v, Calc.Eval(e), 9);

    [Theory(DisplayName = "N11: wissenschaftlich – Funktionen, π, e, n!, Grad/Bogenmaß, Malzeichen darf fehlen")]
    [InlineData("sin(30)", true, 0.5)]
    [InlineData("cos 60", true, 0.5)]
    [InlineData("sin(pi/2)", false, 1)]
    [InlineData("√16 + ln(e)", true, 5)]
    [InlineData("log(1000)", true, 3)]
    [InlineData("5!", true, 120)]
    [InlineData("2π", false, 2 * Math.PI)]
    [InlineData("3(4+1)", true, 15)]
    [InlineData("asin(1)", true, 90)]
    [InlineData("1,5e3", true, 1500)]
    public void Calc_scientific(string e, bool deg, double v) => Assert.Equal(v, Calc.Eval(e, deg, null), 9);

    [Fact(DisplayName = "N11: Fehler verständlich – Teilen durch 0, unbekannte Funktion, x außerhalb der Grafik, tan 90°")]
    public void Calc_errors()
    {
        Assert.Throws<DivideByZeroException>(() => Calc.Eval("1/0"));
        Assert.Contains("Unbekannte Funktion", Assert.Throws<FormatException>(() => Calc.Eval("foo(2)")).Message);
        Assert.Throws<FormatException>(() => Calc.Eval("x+1"));
        Assert.Throws<FormatException>(() => Calc.Eval("tan(90)", true, null));
        Assert.Throws<FormatException>(() => Calc.Eval("sqrt(-1)"));
    }

    [Fact(DisplayName = "N11: Grafik – f(x) abtasten, Lücken bei nicht definierten Stellen")]
    public void Calc_plot()
    {
        var p = Calc.Plot("x^2", -2, 2, 4);
        Assert.Equal([4, 1, 0, 1, 4], p.Select(q => q.Y));
        var h = Calc.Plot("1/x", -1, 1, 2);
        Assert.True(double.IsNaN(h[1].Y));
        Assert.Throws<FormatException>(() => Calc.Plot("x+", 0, 1));
    }

    [Fact(DisplayName = "N11: Programmierer – HEX/DEC/OCT/BIN und Bit-Operationen")]
    public void Prog()
    {
        Assert.Equal(255, ProgCalc.Parse("FF", 16));
        Assert.Equal(255, ProgCalc.Parse("0xff", 16));
        Assert.Equal(5, ProgCalc.Parse("0101", 2));
        Assert.Equal("1111 1111", ProgCalc.Show(255, 2));
        Assert.Equal("377", ProgCalc.Show(255, 8));
        Assert.Equal("FF", ProgCalc.Show(255, 16));
        Assert.Equal(0b1000, ProgCalc.Apply("AND", 0b1100, 0b1010));
        Assert.Equal(0b0110, ProgCalc.Apply("XOR", 0b1100, 0b1010));
        Assert.Equal(16, ProgCalc.Apply("<<", 1, 4));
        Assert.Throws<FormatException>(() => ProgCalc.Parse("G1", 16));
    }

    [Fact(DisplayName = "N11: Datum – Abstand in Jahren/Monaten/Tagen, Werktage, Tage addieren")]
    public void Dates()
    {
        var r = DateCalc.Between(new DateTime(2026, 1, 31), new DateTime(2026, 3, 1));
        Assert.Equal((0, 1, 1, 29), (r.Years, r.Months, r.Days, r.TotalDays));
        Assert.Equal(5, DateCalc.Between(new DateTime(2026, 10, 26), new DateTime(2026, 11, 2)).Workdays);
        Assert.Equal(new DateTime(2027, 2, 28), DateCalc.Add(new DateTime(2026, 11, 30), 0, 3, 0));
    }

    [Fact(DisplayName = "N9: Editor – Kodierung (UTF-8 mit/ohne BOM, UTF-16, ANSI) und Zeilenende erkennen und gleich zurückschreiben")]
    public void Codec_roundtrip()
    {
        var d = Tmp();
        try
        {
            System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var cases = new (byte[] Bytes, string Enc, bool Bom, string Eol)[]
            {
                ([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("Grüße\r\nZeile 2")], "UTF-8", true, "CRLF"),
                (Encoding.UTF8.GetBytes("Grüße\nZeile 2\n"), "UTF-8", false, "LF"),
                ([0xFF, 0xFE, .. Encoding.Unicode.GetBytes("Grüße\r\n")], "UTF-16 LE", true, "CRLF"),
                (Encoding.GetEncoding(1252).GetBytes("Grüße\r\nÄÖÜ"), "ANSI (Windows-1252)", false, "CRLF"),
            };
            foreach (var (bytes, enc, bom, eol) in cases)
            {
                var f = Path.Combine(d, "a.txt"); File.WriteAllBytes(f, bytes);
                var (text, info) = TextFileCodec.Load(f);
                Assert.StartsWith("Grüße", text);
                Assert.Equal((enc, bom, eol), (info.Encoding, info.Bom, info.Eol));
                TextFileCodec.Save(f, text, info);
                Assert.Equal(bytes, File.ReadAllBytes(f));
            }
            // Umstellen: LF -> CRLF, UTF-8 -> ANSI geht nur, wenn alle Zeichen darstellbar sind
            Assert.Equal("a\r\nb\r\n", Encoding.UTF8.GetString(TextFileCodec.Encode("a\nb\n", new("UTF-8", false, "CRLF"))));
            Assert.Throws<InvalidDataException>(() => TextFileCodec.Encode("Smiley 😀", new("ANSI (Windows-1252)", false, "LF")));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "N9: Suchen (mit Umlauf, Groß/klein, Regex), Alles ersetzen, Zeile/Spalte")]
    public void Find_replace()
    {
        const string t = "Apfel, Birne\nApfelkuchen";
        Assert.Equal((0, 5), TextFileCodec.Find(t, "apfel", 0, false, false));
        Assert.Equal((13, 5), TextFileCodec.Find(t, "apfel", 1, false, false));
        Assert.Equal((0, 5), TextFileCodec.Find(t, "apfel", 14, false, false)); // Umlauf
        Assert.Null(TextFileCodec.Find(t, "apfel", 0, true, false));
        Assert.Equal(("Obst, Birne\nObstkuchen", 2), TextFileCodec.ReplaceAll(t, "Apfel", "Obst", true, false));
        Assert.Equal(("Birne, Apfel\nApfelkuchen", 1), TextFileCodec.ReplaceAll(t, @"(\w+), (\w+)", "$2, $1", true, true));
        Assert.Throws<ArgumentException>(() => TextFileCodec.Find(t, "(", 0, true, true));
        Assert.Equal((2, 3), TextFileCodec.LineCol(t, 15));
        Assert.Equal(13, TextFileCodec.LineStart(t, 2));
    }

    [Fact(DisplayName = "N5: Notizbuch – Abschnitte, Seiten als Markdown, Schlagworte, Suche, Löschen in den Papierkorb")]
    public void Notebook_works()
    {
        var d = Tmp();
        try
        {
            var nb = new Notebook(d);
            nb.AddSection("Haushalt"); nb.Write("Haushalt", "Einkauf", "Milch, Brot #einkauf");
            nb.Write("Reisen", "Sommer/2027", "Hotel in Lissabon #urlaub #einkauf");
            Assert.Equal(["Haushalt", "Reisen"], nb.Sections());
            Assert.Equal(["Sommer-2027"], nb.Pages("Reisen"));
            Assert.Equal(2, nb.AllTags()["einkauf"]);
            Assert.Single(nb.Search("lissabon"));
            Assert.Equal(2, nb.Search("#einkauf").Count);
            Assert.Equal(["einkauf", "urlaub"], Notebook.Tags("a #einkauf b#nicht #urlaub #einkauf"));
            nb.RenamePage("Haushalt", "Einkauf", "Wocheneinkauf");
            Assert.Equal("Milch, Brot #einkauf", nb.Read("Haushalt", "Wocheneinkauf"));
            nb.DeletePage("Haushalt", "Wocheneinkauf");
            Assert.Empty(nb.Pages("Haushalt"));
            Assert.Single(Directory.GetFiles(Path.Combine(nb.Root, ".papierkorb")));
            Assert.DoesNotContain(".papierkorb", nb.Sections());
            Assert.Throws<ArgumentException>(() => nb.Write("Haushalt", "  ", "x"));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "N18: Zettel – anlegen, Farbe, anheften, löschen")]
    public void Zettel_works()
    {
        var d = Tmp();
        try
        {
            var s = new ZettelStore(d);
            var z = s.Add("Arzt anrufen", "Blau"); s.Add("");
            Assert.Single(s.Pinned());
            s.Update(z with { OnDesktop = false, Color = "Rosa" });
            Assert.Empty(s.Pinned()); Assert.Equal("Rosa", s.All().Single(x => x.Id == z.Id).Color);
            Assert.Equal("#FFC9DE", ZettelStore.HexOf("Rosa"));
            s.Delete(z.Id); Assert.Single(s.All());
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "N20: Wecker – einmalig, werktags, nächster Termin, klingelt genau einmal")]
    public void Alarms()
    {
        var d = Tmp();
        try
        {
            var s = new AlarmStore(d);
            var once = s.Add(7, 0, "Bus", 0);
            var work = s.Add(6, 30, "", 31);
            var fri = new DateTime(2026, 10, 30, 6, 0, 0); // Freitag
            Assert.Equal(new DateTime(2026, 10, 30, 6, 30, 0), AlarmStore.Next(work, fri));
            Assert.Equal(new DateTime(2026, 11, 2, 6, 30, 0), AlarmStore.Next(work, fri.AddHours(1))); // nächster Montag
            Assert.Equal("Mo–Fr", AlarmStore.DaysText(31));
            Assert.Empty(s.TakeDue(fri));
            Assert.Equal("Wecker", Assert.Single(s.TakeDue(fri.AddMinutes(30).AddSeconds(5))).Label);
            Assert.Empty(s.TakeDue(fri.AddMinutes(31))); // nicht doppelt
            Assert.Single(s.TakeDue(new DateTime(2026, 10, 30, 7, 0, 30)));
            Assert.False(s.All().Single(a => a.Id == once.Id).Enabled); // einmalig -> aus
            Assert.Empty(s.TakeDue(new DateTime(2026, 10, 31, 6, 30, 10))); // Samstag
            Assert.Throws<ArgumentException>(() => s.Add(25, 0, "", 0));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "N20: Weltuhr – Städte speichern, Ortszeit aus IANA-Zone")]
    public void World()
    {
        var d = Tmp();
        try
        {
            var w = new WorldClock(d);
            Assert.Contains("Tokio", w.Cities());
            w.Save(["Berlin"]); w.Add("Sydney"); w.Add("Atlantis");
            Assert.Equal(["Berlin", "Sydney"], w.Cities());
            var t = WorldClock.At("Tokio", new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));
            Assert.Equal(21, t!.Value.Local.Hour);
            Assert.Equal("gleiche Zeit", WorldClock.OffsetText(0));
        }
        finally { Directory.Delete(d, true); }
    }
}
