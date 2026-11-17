using Trendo.Core.Elektro;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0748: Fertigungsdaten mit dem eigenen Gerber/Excellon-Leser prüfen (kein Fremdwerkzeug auf dem PC – siehe Bericht) und KiCad-Netzlisten importieren.
/// Die Netzlisten unten sind selbst geschrieben im Format, das KiCad 5 („(version D)“, ohne Anführungszeichen) und KiCad 7 („(version "E")“) exportieren.</summary>
public class T0748_GerberKiCad
{
    static string Tmp(string name) { var d = Path.Combine(Path.GetTempPath(), "trendo-t0748-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return Path.Combine(d, name); }

    static Circuit Routed(string template, string? name = null)
    {
        var c = Circuit.Template(template); if (name is not null) c.Name = name;
        Pcb.AutoPlace(c); var (_, open) = Pcb.AutoRoute(c); Assert.Equal(0, open); return c;
    }

    [Theory(DisplayName = "T-0748: Gerber-ZIP jeder Vorlage wird gelesen – Kopf, Blenden, Koordinaten, M02, Bohrdatei; Bohrungen treffen Lötflächen oben und unten, alles im Umriss")]
    [InlineData("led")] [InlineData("teiler")] [InlineData("transistor")] [InlineData("rc")] [InlineData("gleichrichter")]
    public void Fab_zip_reads_clean(string template)
    {
        var c = Routed(template); var zip = Tmp("fab.zip"); Pcb.WriteFabZip(c, zip);
        var r = GerberCheck.ReadZip(zip);
        Assert.True(r.Ok, r.Text);
        Assert.Equal(5, r.Files.Count);
        var pads = Pcb.Pads(c).Count;
        Assert.Equal(pads, r.Files.Single(f => f.Kind == "Excellon").Flashes);
        Assert.Equal(pads, r.Files.Single(f => f.Name.EndsWith("-F_Cu.gbr")).Flashes);
        Assert.Equal(c.Pcb.Traces.Where(t => !t.Bottom).Sum(t => t.Pts.Count - 1), r.Files.Single(f => f.Name.EndsWith("-F_Cu.gbr")).Strokes);
        var edge = r.Files.Single(f => f.Name.Contains("Edge_Cuts"));
        Assert.Equal(c.Pcb.W, edge.MaxX - edge.MinX - 0.1, 3); // Umriss mit 0,1-mm-Linie
    }

    [Fact(DisplayName = "T-0748: Leser erkennt kaputte Dateien – Blende nicht definiert, M02 fehlt, Koordinate zu lang, Bohrung ohne Werkzeug, Bohrung neben der Lötfläche")]
    public void Reader_finds_errors()
    {
        Assert.Contains(GerberCheck.ReadGerber("a.gbr", "%FSLAX46Y46*%\n%MOMM*%\nD11*\nX0Y0D03*\nM02*\n").Errors, e => e.Contains("D11 benutzt, aber nicht definiert"));
        Assert.Contains(GerberCheck.ReadGerber("a.gbr", "%FSLAX46Y46*%\n%MOMM*%\n%ADD10C,0.5*%\nD10*\nX0Y0D03*\n").Errors, e => e.Contains("M02 fehlt"));
        Assert.Contains(GerberCheck.ReadGerber("a.gbr", "%FSLAX46Y46*%\n%MOMM*%\n%ADD10C,0.5*%\nD10*\nX12345678901Y0D03*\nM02*\n").Errors, e => e.Contains("mehr Stellen"));
        Assert.Contains(GerberCheck.ReadGerber("a.gbr", "%MOMM*%\nX0Y0D02*\nM02*\n").Errors, e => e.Contains("%FS"));
        Assert.Contains(GerberCheck.ReadDrill("a.drl", "M48\nMETRIC,TZ\nT1C0.800\n%\nX1.000Y1.000\nM30\n").Errors, e => e.Contains("ohne gewähltes Werkzeug"));
        var ok = GerberCheck.ReadGerber("x-F_Cu.gbr", "%FSLAX46Y46*%\n%MOMM*%\n%ADD10C,1.8*%\nD10*\nX1000000Y1000000D03*\nM02*\n");
        Assert.True(ok.Ok, string.Join(";", ok.Errors));
        var drill = GerberCheck.ReadDrill("x.drl", "M48\nMETRIC,TZ\nT1C0.800\n%\nG90\nG05\nT1\nX2.000Y1.000\nT0\nM30\n");
        Assert.True(drill.Ok, string.Join(";", drill.Errors));
        var edge = GerberCheck.ReadGerber("x-Edge_Cuts.gbr", "%FSLAX46Y46*%\n%MOMM*%\n%ADD10C,0.1*%\nD10*\nX0Y0D02*\nX5000000Y0D01*\nX5000000Y5000000D01*\nX0Y0D01*\nM02*\n");
        var pkg = GerberCheck.CrossLayer([ok, ok with { Name = "x-B_Cu.gbr" }, edge, drill]);
        Assert.False(pkg.Ok); Assert.Contains(pkg.Errors, e => e.Contains("trifft keine Lötfläche"));
    }

    [Fact(DisplayName = "T-0748: Schaltungsname mit * oder % zerstörte den Gerber-Kommentar – jetzt bleibt die Datei lesbar")]
    public void Name_with_special_chars()
    {
        var c = Routed("led", "LED *Test* 100%"); var zip = Tmp("fab.zip"); Pcb.WriteFabZip(c, zip);
        var r = GerberCheck.ReadZip(zip);
        Assert.True(r.Ok, r.Text);
    }

    const string LedNet7 = """
        (export (version "E")
          (design (source "led.kicad_sch") (tool "Eeschema 7.0.10"))
          (components
            (comp (ref "BT1") (value "9V") (footprint "Battery:BatteryHolder_Keystone_1058_1x2032") (libsource (lib "Device") (part "Battery") (description "Batterie")))
            (comp (ref "R1") (value "330") (footprint "Resistor_THT:R_Axial_DIN0207") (libsource (lib "Device") (part "R") (description "Widerstand")))
            (comp (ref "D1") (value "LED red") (footprint "LED_THT:LED_D5.0mm") (libsource (lib "Device") (part "LED") (description "Leuchtdiode"))))
          (nets
            (net (code "1") (name "GND") (node (ref "BT1") (pin "2") (pinfunction "-") (pintype "passive")) (node (ref "D1") (pin "1") (pinfunction "K") (pintype "passive")))
            (net (code "2") (name "Net-(BT1-+)") (node (ref "BT1") (pin "1") (pinfunction "+") (pintype "passive")) (node (ref "R1") (pin "1") (pintype "passive")))
            (net (code "3") (name "Net-(D1-A)") (node (ref "D1") (pin "2") (pinfunction "A") (pintype "passive")) (node (ref "R1") (pin "2") (pintype "passive")))))
        """;

    const string TransistorNet5 = """
        (export (version D)
          (components
            (comp (ref BT1) (value 9V) (libsource (lib Device) (part Battery)))
            (comp (ref R1) (value 470) (libsource (lib Device) (part R)))
            (comp (ref D1) (value LED_green) (libsource (lib Device) (part LED)))
            (comp (ref Q1) (value BC547) (libsource (lib Device) (part Q_NPN_EBC)))
            (comp (ref SW1) (value SW_Push) (libsource (lib Switch) (part SW_Push)))
            (comp (ref R2) (value 10k) (libsource (lib Device) (part R)))
            (comp (ref U1) (value NE555) (libsource (lib Timer) (part NE555P))))
          (libparts
            (libpart (lib Device) (part Battery) (pins (pin (num 1) (name +) (type passive)) (pin (num 2) (name -) (type passive))))
            (libpart (lib Device) (part LED) (pins (pin (num 1) (name K) (type passive)) (pin (num 2) (name A) (type passive)))))
          (nets
            (net (code 1) (name GND) (node (ref BT1) (pin 2)) (node (ref Q1) (pin 1)) (node (ref U1) (pin 1)))
            (net (code 2) (name +9V) (node (ref BT1) (pin 1)) (node (ref R1) (pin 1)) (node (ref SW1) (pin 1)) (node (ref U1) (pin 8)))
            (net (code 3) (name "Net-(D1-Pad2)") (node (ref R1) (pin 2)) (node (ref D1) (pin 2)))
            (net (code 4) (name "Net-(D1-Pad1)") (node (ref D1) (pin 1)) (node (ref Q1) (pin 3)))
            (net (code 5) (name "Net-(R2-Pad1)") (node (ref SW1) (pin 2)) (node (ref R2) (pin 1)))
            (net (code 6) (name "Net-(Q1-Pad2)") (node (ref R2) (pin 2)) (node (ref Q1) (pin 2)))))
        """;

    static double LedCurrent(Circuit c) { var s = new Simulator(c, 5e-5).Run(0.01); return s.Parts[c.Parts.Single(p => p.Kind == "led").Id].I; }

    [Fact(DisplayName = "T-0748: KiCad-7-Netzliste LED + 330 Ω + 9 V – Import simuliert denselben LED-Strom wie die gezeichnete Vorlage")]
    public void Import_led_kicad7()
    {
        var r = KiCadNetlist.Import(LedNet7, "LED aus KiCad");
        Assert.Empty(r.Skipped); Assert.Equal(3, r.Parts); Assert.Equal(3, r.Nets);
        var c = r.Circuit;
        Assert.Equal(330, c.Parts.Single(p => p.Name == "R1").Value); Assert.Equal(9, c.Parts.Single(p => p.Name == "BT1").Value);
        Assert.Single(c.Parts, p => p.Kind == "masse");
        var mine = LedCurrent(c); var reference = LedCurrent(Circuit.Template("led"));
        Assert.InRange(mine, 0.015, 0.025); Assert.Equal(reference, mine, 9);
        // Export der importierten Schaltung als SPICE-Netzliste enthält dieselben Bauteile
        var cir = c.Spice(); Assert.Contains("RR1 ", cir); Assert.Contains("DD1 ", cir); Assert.Contains("VBT1 ", cir);
    }

    [Fact(DisplayName = "T-0748: KiCad-5-Netzliste Transistorschalter (Q_NPN_EBC ohne Pin-Funktion) – gleicher LED-Strom wie die Vorlage; NE555 wird ehrlich als nicht übernommen gemeldet")]
    public void Import_transistor_kicad5()
    {
        var r = KiCadNetlist.Import(TransistorNet5);
        Assert.Equal(6, r.Parts); Assert.Contains(r.Skipped, s => s.StartsWith("U1"));
        var q = r.Circuit.Parts.Single(p => p.Kind == "npn"); Assert.Equal("Q1", q.Name);
        Assert.Equal("gruen", r.Circuit.Parts.Single(p => p.Kind == "led").Color);
        var tpl = Circuit.Template("transistor"); tpl.Parts.Single(p => p.Kind == "npn").Value = 100; // Import nimmt β = 100
        var mine = LedCurrent(r.Circuit); var reference = LedCurrent(tpl);
        Assert.True(mine > 0.01, $"LED-Strom {mine}"); Assert.Equal(reference, mine, 7);
    }

    [Theory(DisplayName = "T-0748: KiCad-Werte – 4k7, 10K, 100n, 2.2uF, 4R7, 1meg, 9V, dc 5")]
    [InlineData("4k7", "widerstand", 4700)] [InlineData("10K", "widerstand", 10000)] [InlineData("100n", "kondensator", 1e-7)] [InlineData("2.2uF", "kondensator", 2.2e-6)]
    [InlineData("4R7", "widerstand", 4.7)] [InlineData("1meg", "widerstand", 1e6)] [InlineData("9V", "spannung", 9)] [InlineData("dc 5", "spannung", 5)]
    public void Values(string text, string kind, double expected) => Assert.Equal(expected, KiCadNetlist.Number(text, kind)!.Value, 12);

    [Fact(DisplayName = "T-0748: keine Netzliste / kaputte Klammern – verständliche Meldung statt Absturz")]
    public void Bad_files()
    {
        Assert.Contains("keine KiCad-Netzliste", Assert.Throws<FormatException>(() => KiCadNetlist.Import("(kicad_sch (version 20230121))")).Message);
        Assert.Contains("unvollständig", Assert.Throws<FormatException>(() => KiCadNetlist.Import("(export (version \"E\") (components")).Message);
        Assert.Contains("beginnt nicht", Assert.Throws<FormatException>(() => KiCadNetlist.Import("hallo")).Message);
    }
    // ---------- Gegenprüfung behoben (T-0748-fix) ----------
    [Fact(DisplayName = "T-0748-fix: Fertigungs-ZIP – bei Prüffehler bleibt die alte Datei unverändert, keine Zwischendatei bleibt liegen; bei Erfolg atomar ersetzt")]
    public void Fab_zip_atomic()
    {
        var zip = Tmp("platine.zip"); var dir = Path.GetDirectoryName(zip)!;
        Pcb.WriteFabZip(Routed("led"), zip, Pcb.CheckFab);
        var good = File.ReadAllBytes(zip); Assert.True(GerberCheck.ReadZip(zip).Ok);
        var e = Assert.Throws<InvalidDataException>(() => Pcb.WriteFabZip(Routed("teiler"), zip, _ => "erzwungener Prüffehler"));
        Assert.Equal("erzwungener Prüffehler", e.Message);
        Assert.Equal(good, File.ReadAllBytes(zip));                       // alte gute Datei ist noch da
        Assert.Equal(["platine.zip"], Directory.GetFiles(dir).Select(Path.GetFileName)); // keine Reste
        Pcb.WriteFabZip(Routed("teiler"), zip, Pcb.CheckFab);
        Assert.NotEqual(good, File.ReadAllBytes(zip)); Assert.True(GerberCheck.ReadZip(zip).Ok);
        Assert.Single(Directory.GetFiles(dir));
    }

    [Fact(DisplayName = "T-0748-fix: kaputte Zahlen (Blende C,1..2, 25-stellige Koordinate, T1C0.8.0, X1..2) werden zum Befund statt zur Ausnahme")]
    public void Broken_numbers_are_findings()
    {
        var a = GerberCheck.ReadGerber("a.gbr", "%FSLAX46Y46*%\n%MOMM*%\n%ADD10C,1..2*%\nD10*\nX0Y0D03*\nM02*\n");
        Assert.Contains(a.Errors, e => e.Contains("Größe nicht lesbar"));
        var b = GerberCheck.ReadGerber("b.gbr", "%FSLAX46Y46*%\n%MOMM*%\n%ADD10C,0.5*%\nD10*\nX1234567890123456789012345Y0D03*\nM02*\n");
        Assert.Contains(b.Errors, e => e.Contains("mehr Stellen"));
        var c = GerberCheck.ReadGerber("c.gbr", "%FSLAX46Y46*%\n%MOMM*%\n%ADD99999999999C,0.5*%\nD99999999999*\nX0Y0D03*\nM02*\n");
        Assert.False(c.Ok);
        var d = GerberCheck.ReadDrill("d.drl", "M48\nMETRIC,TZ\nT1C0.8.0\n%\nT1\nX1..2Y1.0\nM30\n");
        Assert.Contains(d.Errors, e => e.Contains("T1C0.8.0") && e.Contains("nicht lesbar"));
        Assert.Contains(d.Errors, e => e.Contains("Zahl nicht lesbar") || e.Contains("nicht im Kopf"));
    }

    [Fact(DisplayName = "T-0748-fix: Erfolgsmeldung sagt, was geprüft ist – Format und Geometrie, nicht die Netze/Kurzschlüsse; Lötflächen ≠ Bohrungen nennt den THT-Grund")]
    public void Scope_in_message()
    {
        var zip = Tmp("fab.zip"); Pcb.WriteFabZip(Routed("led"), zip);
        var r = GerberCheck.ReadZip(zip); Assert.True(r.Ok, r.Text);
        Assert.Contains("nicht die Netze", r.Text); Assert.Contains("Kurzschlüsse", r.Text);
        var pad = GerberCheck.ReadGerber("x-F_Cu.gbr", "%FSLAX46Y46*%\n%MOMM*%\n%ADD10C,1.8*%\nD10*\nX1000000Y1000000D03*\nX3000000Y1000000D03*\nM02*\n");
        var drill = GerberCheck.ReadDrill("x.drl", "M48\nMETRIC,TZ\nT1C0.800\n%\nT1\nX1.000Y1.000\nM30\n");
        var edge = GerberCheck.ReadGerber("x-Edge_Cuts.gbr", "%FSLAX46Y46*%\n%MOMM*%\n%ADD10C,0.1*%\nD10*\nX0Y0D02*\nX5000000Y0D01*\nX5000000Y5000000D01*\nX0Y0D01*\nM02*\n");
        var pkg = GerberCheck.CrossLayer([pad, pad with { Name = "x-B_Cu.gbr" }, edge, drill]);
        Assert.Contains(pkg.Errors, e => e.Contains("2 Lötflächen, aber 1 Bohrungen") && e.Contains("Durchsteck"));
    }

    static string Resistors(int n)
    {
        var sb = new System.Text.StringBuilder("(export (version \"E\")\n (components\n");
        for (var i = 1; i <= n; i++) sb.Append($"  (comp (ref \"R{i}\") (value \"1k\") (libsource (lib \"Device\") (part \"R\")))\n");
        sb.Append(" )\n (nets\n");
        for (var i = 0; i <= n; i++) { sb.Append($"  (net (code \"{i + 1}\") (name \"N{i}\")"); if (i > 0) sb.Append($" (node (ref \"R{i}\") (pin \"2\"))"); if (i < n) sb.Append($" (node (ref \"R{i + 1}\") (pin \"1\"))"); sb.Append(")\n"); }
        return sb.Append(" ))\n").ToString();
    }

    [Fact(DisplayName = "T-0748-fix: KiCad-Import mit 3000 Widerständen unter 2 s (vorher 89–133 s)")]
    public void Import_3000_parts_fast()
    {
        KiCadNetlist.Import(Resistors(5)); // Aufwärmen
        var text = Resistors(3000); var sw = System.Diagnostics.Stopwatch.StartNew();
        var r = KiCadNetlist.Import(text);
        Assert.True(sw.Elapsed.TotalSeconds < 2, $"{sw.Elapsed.TotalSeconds:0.00} s");
        Assert.Equal(3000, r.Parts); Assert.Equal(3000, r.Circuit.Parts.Count(p => p.Kind == "widerstand"));
        Assert.Equal(3000, r.Circuit.Parts.Where(p => p.Kind == "widerstand").Select(p => p.Name).Distinct().Count());
    }

    [Fact(DisplayName = "T-0748-fix: nicht annotierte Netzliste („R?“ mehrfach) – verständliche Meldung „Schaltplan annotieren“ statt englischer ArgumentException")]
    public void Duplicate_refs()
    {
        var text = LedNet7.Replace("(ref \"R1\")", "(ref \"R?\")").Replace("(ref \"D1\")", "(ref \"R?\")");
        var e = Assert.Throws<FormatException>(() => KiCadNetlist.Import(text));
        Assert.Contains("nicht annotiert", e.Message); Assert.Contains("„R?“ (2-mal)", e.Message); Assert.Contains("Schaltplan annotieren", e.Message);
    }

    [Fact(DisplayName = "T-0748-fix: Netz mit nur einem Anschluss ist ein Hinweis (offenes Ende), kein „nicht übernommen“")]
    public void Single_pin_net_is_note()
    {
        // R2 hängt mit Pin 1 am Pluspol, Pin 2 an keinem anderen Bauteil (vergessener Draht)
        var text = LedNet7.Replace("(components", "(components (comp (ref \"R2\") (value \"1k\") (libsource (lib \"Device\") (part \"R\")))")
            .Replace("(node (ref \"R1\") (pin \"1\") (pintype \"passive\")))", "(node (ref \"R1\") (pin \"1\") (pintype \"passive\")) (node (ref \"R2\") (pin \"1\")))")
            .Replace("(nets", "(nets (net (code \"4\") (name \"Net-(R2-Pad2)\") (node (ref \"R2\") (pin \"2\")))");
        Assert.NotEqual(LedNet7, text);
        var r = KiCadNetlist.Import(text);
        Assert.Empty(r.Skipped);
        Assert.Equal(4, r.Parts);
        Assert.Contains(r.Notes!, n => n.Contains("Net-(R2-Pad2)") && n.Contains("nur einen Anschluss") && n.Contains("offenes Ende"));
        Assert.Contains("Hinweis: Netz „Net-(R2-Pad2)“", r.Text); Assert.DoesNotContain("nicht übernommen", r.Text);
    }

    [Theory(DisplayName = "T-0748-fix: KiCad-Werte – „k“ allein, ∞, Infinity, negative oder null Ohm werden abgelehnt")]
    [InlineData("k", "widerstand")] [InlineData("∞", "widerstand")] [InlineData("Infinity", "widerstand")] [InlineData("-10", "widerstand")] [InlineData("-4k7", "widerstand")]
    [InlineData("0", "kondensator")] [InlineData("1e400", "widerstand")] [InlineData("M", "spule")]
    public void Bad_values(string text, string kind) => Assert.Null(KiCadNetlist.Number(text, kind));

    [Fact(DisplayName = "T-0748-fix: unlesbarer Wert im Import wird als Hinweis gemeldet (Standardwert gesetzt)")]
    public void Bad_value_note()
    {
        var r = KiCadNetlist.Import(LedNet7.Replace("(value \"330\")", "(value \"k\")"));
        Assert.Contains(r.Notes!, n => n.StartsWith("R1: Wert „k“ nicht lesbar"));
    }
}
