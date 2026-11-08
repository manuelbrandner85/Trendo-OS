using System.Globalization;
using System.Text.Json;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 6 – SYS-10: Geschäftsbrief nach DIN 5008 mit typst (Konzept typst-letter-pro, eigener Code).
/// Echte Läufe setzen PDF/PNG und lassen typst die Lage von Anschrift, Informationsblock und Betreff selbst messen.</summary>
public class DinLetterTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "trendo-din-" + Guid.NewGuid().ToString("N"));
    public DinLetterTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    static TypstDoc.DinLetter Brief(bool formA = false, string body = "Sehr geehrte Damen und Herren,\n\nanbei erhalten Sie die gewünschten Unterlagen.") => new(
        "Erika Muster\nMusterweg 1\n12345 Musterstadt\nTel. 01234 56789", "Max Beispiel GmbH\nz. Hd. Frau Beispiel\nHauptstraße 5\n54321 Beispielort",
        "Ihre Anfrage vom 3. November 2026", body, new DateOnly(2026, 11, 8), formA, "MB-17", "EM/2026-41", Footer: "Bank: Musterbank · IBAN DE02 1203 0000 0000 2020 51");

    [Fact(DisplayName = "DIN 5008 +1: Maße Form B (Anschrift 45 mm, Info 50 mm, Betreff 98,46 mm, Falz 105/210) und Form A (27/32/80,46, Falz 87/192)")]
    public void Geometry()
    {
        Assert.Equal((45, 50, 98.46, 105, 210), TypstDoc.DinGeometry(false));
        Assert.Equal((27, 32, 80.46, 87, 192), TypstDoc.DinGeometry(true));
        var src = TypstDoc.Din5008(Brief());
        Assert.Contains("dy: 18mm, block(width: 85mm, height: 45mm)", src); // 45 − 27 mm Rand
        Assert.Contains("dy: 105mm, line", src); Assert.Contains("dy: 148.5mm, line", src); Assert.Contains("dy: 210mm, line", src);
        Assert.Contains("#v(71.46mm)", src);
        Assert.Contains("#v(53.46mm)", TypstDoc.Din5008(Brief(formA: true)));
    }

    [Fact(DisplayName = "DIN 5008 +2: Inhalt – Rücksendeangabe einzeilig mit ·, Ihr/Unser Zeichen, Datum ausgeschrieben, Gruß und Name, Fußzeile")]
    public void Content()
    {
        var src = TypstDoc.Din5008(Brief());
        Assert.Contains("Erika Muster · Musterweg 1 · 12345 Musterstadt · Tel. 01234 56789", src);
        Assert.Contains("[Ihr Zeichen:], [MB\\-17]", src); Assert.Contains("[Unser Zeichen:], [EM\\/2026\\-41]", src);
        Assert.Contains("[Datum:], [8\\. November 2026]", src); // ohne Schutz würde typst „8.“ als nummerierte Liste setzen
        Assert.Equal("3\\. Mahnung\nam 12. Mai, Punkt 1.5", TypstDoc.Escape("3. Mahnung\nam 12. Mai, Punkt 1.5"));
        Assert.Contains("Mit freundlichen Grüßen", src); Assert.EndsWith("Erika Muster\n", src);
        Assert.Contains("Bank: Musterbank", src); Assert.Contains("Seite #counter(page).display() von #n", src);
    }

    [Fact(DisplayName = "DIN 5008 +3: Ohne Zeichen keine leeren Zeilen im Informationsblock; eigener Unterzeichner")]
    public void Optional_fields()
    {
        var src = TypstDoc.Din5008(Brief() with { YourRef = "", OurRef = " ", Signer = "Dr. Erika Muster" });
        Assert.DoesNotContain("Zeichen:", src); Assert.EndsWith("Dr. Erika Muster\n", src);
    }

    [Fact(DisplayName = "DIN 5008 −1: Pflichtangaben – Absender, Empfänger (höchstens 6 Zeilen), Betreff, Text; sonst keine Quelle")]
    public void Check()
    {
        var e = TypstDoc.DinCheck(new("", "", " ", "", new DateOnly(2026, 11, 8)));
        Assert.Equal(["Absender fehlt.", "Empfänger fehlt.", "Betreff fehlt.", "Brieftext fehlt."], e);
        Assert.Contains("Die Anschrift hat höchstens 6 Zeilen (DIN 5008).", TypstDoc.DinCheck(Brief() with { Recipient = "1\n2\n3\n4\n5\n6\n7" }));
        Assert.Throws<ArgumentException>(() => TypstDoc.Din5008(Brief() with { Subject = "" }));
    }

    [Fact(DisplayName = "DIN 5008 −2: Einschleusen über # oder ] im Text ist geschützt")]
    public void Injection()
    {
        var src = TypstDoc.Din5008(Brief(body: "#read(\"/etc/passwd\") ] #panic()"));
        Assert.Contains("\\#read", src); Assert.Contains("\\]", src); Assert.DoesNotContain(" #panic", src);
    }

    // ---------- echte Läufe ----------
    public sealed class TypstFactAttribute : FactAttribute { public TypstFactAttribute() { if (EchtWerkzeuge.Tool("typst") is null) Skip = "Übersprungen – typst nicht gefunden."; } }

    const string Mass = "\n#context [#metadata((a: locate(<din-anschrift>).position(), i: locate(<din-info>).position(), b: locate(<din-betreff>).position())) <din-mass>]\n";
    static double MmOf(JsonElement p, string k) => double.Parse(p.GetProperty(k).GetString()!.Replace("pt", ""), CultureInfo.InvariantCulture) * 25.4 / 72;

    (double Ax, double Ay, double Ix, double Iy, double By) Measure(TypstDoc.DinLetter l, string name)
    {
        var exe = EchtWerkzeuge.Tool("typst")!; var typ = Path.Combine(_dir, name + ".typ");
        File.WriteAllText(typ, TypstDoc.Din5008(l) + Mass);
        var (ok, msg) = TypstDoc.Compile(exe, typ, Path.Combine(_dir, name + ".pdf"), _dir); Assert.True(ok, msg);
        var q = Cli.Run(exe, ["query", "--root", _dir, typ, "<din-mass>", "--field", "value", "--one"], timeoutMs: 60000);
        Assert.True(q.Ok, q.Err);
        var v = JsonDocument.Parse(q.Out).RootElement;
        return (MmOf(v.GetProperty("a"), "x"), MmOf(v.GetProperty("a"), "y"), MmOf(v.GetProperty("i"), "x"), MmOf(v.GetProperty("i"), "y"), MmOf(v.GetProperty("b"), "y"));
    }

    [TypstFact(DisplayName = "DIN 5008 echt: typst setzt Form B als PDF; gemessen: Anschrift bei 25/62,7 mm, Info bei 125/50 mm, Betreff bei 98,46 mm (±0,5 mm)")]
    public void Real_form_b()
    {
        var m = Measure(Brief(), "form-b");
        Assert.InRange(m.Ax, 24.5, 25.5); Assert.InRange(m.Ay, 62.2, 63.2);
        Assert.InRange(m.Ix, 124.5, 125.5); Assert.InRange(m.Iy, 49.5, 50.5);
        Assert.InRange(m.By, 97.96, 98.96);
        Assert.True(new FileInfo(Path.Combine(_dir, "form-b.pdf")).Length > 5000);
    }

    [TypstFact(DisplayName = "DIN 5008 echt: Form A gemessen (Anschrift 44,7 mm, Info 32 mm, Betreff 80,46 mm); langer Text → zweite Seite mit „Seite 2 von 2“")]
    public void Real_form_a_and_pages()
    {
        var m = Measure(Brief(formA: true), "form-a");
        Assert.InRange(m.Ay, 44.2, 45.2); Assert.InRange(m.Iy, 31.5, 32.5); Assert.InRange(m.By, 79.96, 80.96);
        var lang = Brief(body: string.Join("\n\n", Enumerable.Range(1, 30).Select(i => $"Absatz {i}: Dies ist ein längerer Absatz, damit der Brief über die erste Seite hinausgeht und die Seitenzählung greift.")));
        var exe = EchtWerkzeuge.Tool("typst")!; var typ = Path.Combine(_dir, "lang.typ"); File.WriteAllText(typ, TypstDoc.Din5008(lang));
        Assert.True(TypstDoc.Compile(exe, typ, Path.Combine(_dir, "lang.pdf"), _dir).Ok);
        var pages = Cli.Run(exe, ["query", "--root", _dir, typ, "<din-betreff>", "--field", "location"], timeoutMs: 60000); Assert.True(pages.Ok, pages.Err);
        var svg = Cli.Run(exe, ["compile", "--root", _dir, typ, Path.Combine(_dir, "lang-{p}.svg")], timeoutMs: 60000); Assert.True(svg.Ok, svg.Err);
        Assert.True(File.Exists(Path.Combine(_dir, "lang-2.svg"))); Assert.False(File.Exists(Path.Combine(_dir, "lang-3.svg")));
    }
}
