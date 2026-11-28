using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Programs;
using Trendo.Core.Wissen;

namespace Trendo.App;

/// <summary>T-0781 STUDIO-SCIENCE-01 „Naturwissenschaft“: Periodensystem mit eigenen Elementdaten, Molmasse, Reaktionsgleichungen ausgleichen,
/// Physik-Formeln mit Einheiten und Dimensionsprüfung, Rechnen mit Einheiten – alles wird als Eintrag im Laborheft (Dokument) gesammelt.
/// Messreihen auswerten (Statistik, Regression) bleibt im vorhandenen Programm „Messdaten & Auswertung“ (SPEC-05); die Fehlerfortpflanzung von dort rechnet hier mit.</summary>
public partial class MainWindow
{
    public const string NaturKey = "naturwissenschaft-programm", NaturStudio = "STUDIO-SCIENCE-01";
    /// <summary>Gewähltes Element und gewählter Laborheft-Eintrag (nur Ansicht).</summary>
    readonly Dictionary<string, int> _nwElement = [], _nwSel = [];

    static readonly (string Id, string Label)[] NwModi = [("pse", "Periodensystem"), ("molmasse", "Molmasse"), ("reaktion", "Reaktionen ausgleichen"), ("physik", "Physik-Formeln"), ("einheiten", "Einheiten")];

    static readonly Dictionary<string, string> NwFarbe = new()
    {
        ["Alkalimetall"] = "#7A2E2E", ["Erdalkalimetall"] = "#7A4A1E", ["Übergangsmetall"] = "#55502A", ["Lanthanoid"] = "#5C3A55", ["Actinoid"] = "#4A2E48",
        ["Metall"] = "#2F4A46", ["Halbmetall"] = "#46512C", ["Nichtmetall"] = "#2E5038", ["Halogen"] = "#6A5A24", ["Edelgas"] = "#4A3360",
    };

    static JsonObject NwEintrag(string art, string eingabe, string formel = "", string gesucht = "", string ziel = "", string quelle = "") =>
        new() { ["art"] = art, ["eingabe"] = eingabe, ["formel"] = formel, ["gesucht"] = gesucht, ["ziel"] = ziel, ["quelle"] = quelle };

    static JsonObject NwDoc(string title, string modus, params JsonObject[] e) => new()
    {
        ["title"] = title, ["modus"] = modus, ["faerben"] = "kategorie", ["molEingabe"] = "H2SO4", ["rxEingabe"] = "CH4 + O2 -> CO2 + H2O", ["einEingabe"] = "100 km/h in m/s",
        ["phFormel"] = "freier-fall", ["phGesucht"] = "t", ["phWerte"] = "h = 20 ± 0,1 m", ["phZiel"] = "", ["eintraege"] = new JsonArray([.. e]),
    };

    private void InitNaturwissenschaft() => RegisterProgram(FachTabs, new ProgramSpec(NaturKey, NaturStudio, "Naturwissenschaft")
    {
        Sentence = "Periodensystem, Molmasse, Reaktionsgleichungen ausgleichen und Physik-Formeln mit Einheiten – mit Rechenweg, Dimensionsprüfung und Probe, gesammelt im Laborheft.",
        Folder = "Naturwissenschaft",
        ScrollWorkspace = true,
        Templates =
        [
            new("leer", "Leeres Laborheft", "Mit dem Periodensystem anfangen", () => NwDoc("", "pse")),
            new("chemie", "Beispiel: Chemie", "Molmassen und ausgeglichene Reaktionen", () => NwDoc("Chemie", "reaktion",
                NwEintrag("molmasse", "CuSO4·5H2O"), NwEintrag("reaktion", "Fe + O2 -> Fe2O3"), NwEintrag("reaktion", "C3H8 + O2 -> CO2 + H2O"))),
            new("physik", "Beispiel: Physik", "Freier Fall mit Messunsicherheit, Marathon-Tempo, Einheiten", () => NwDoc("Physik", "physik",
                NwEintrag("physik", "h = 20 ± 0,1 m", "freier-fall", "t"), NwEintrag("physik", "s = 42,195 km; t = 2,5 h", "geschwindigkeit", "v", "km/h"), NwEintrag("einheiten", "2 kWh in MJ"))),
        ],
        Ribbon =
        [
            ("Rechnen", [
                new ProgramCmd("nw-run-laborheft", "Ins Laborheft", d => NwRechnen(d), Primary: true, Tip: "Rechnet die Eingabe der gewählten Ansicht (oder nimmt den Steckbrief des gewählten Elements) und legt sie im Laborheft ab."),
            ]),
            ("Ansicht", [.. NwModi.Select(m => new ProgramCmd("nw-modus-" + m.Id, m.Label, d => NwModus(d, m.Id)))]),
            ("Periodensystem", [
                new ProgramCmd("nw-farbe-kategorie", "Nach Art färben", d => d.Set("faerben", "kategorie", "Färbung"), Level: 1), // T-0784 fix3: in „Einfach“ ohne Tabelle wirkungslos
                new ProgramCmd("nw-farbe-en", "Nach Elektronegativität", d => d.Set("faerben", "en", "Färbung"), Level: 1),
                new ProgramCmd("nw-farbe-masse", "Nach Atommasse", d => d.Set("faerben", "masse", "Färbung"), Level: 1),
            ]),
            ("Bearbeiten", [
                new ProgramCmd("nw-loeschen", "Eintrag löschen", d => NwLoeschen(d, NwAuswahl(d)), Tip: "Entfernt den gewählten Laborheft-Eintrag (Entf).") { NoKi = true },
            ]),
            ("Messwerte", [
                new ProgramCmd("nw-messwerte", "Messreihe auswerten …", d => Show(SK), Level: 1, Tip: "Öffnet „Messdaten & Auswertung“: Statistik, Ausgleichsgerade, Tests."),
            ]),
        ],
        GroupLevels = new() { ["Messwerte"] = 1 },
        Exports =
        [
            new("pdf", "Laborheft als PDF", ".pdf", (d, f) => { Rechenblatt.Pdf(f, NwTitel(d), NwBlatt(d)); return f; }),
            new("csv", "Ergebnisse als Tabelle (.csv)", ".csv", (d, f) => { AtomicFile.WriteAllBytes(f, [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(NwCsv(d))]); return f; }),
            new("md", "Markdown (.md)", ".md", (d, f) => { AtomicFile.WriteAllText(f, Rechenblatt.Markdown(NwTitel(d), NwBlatt(d))); return f; }),
            new("elemente", "Periodensystem als Tabelle (.csv)", ".csv", (d, f) => { AtomicFile.WriteAllBytes(f, [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(Elemente.Csv())]); return f; }),
        ],
        SimpleExports = 2,
        KiActions = [NwKiRechnung],
        KiLocked = ["eintraege"],
        KiHint = "Für jede Rechnung nimmst du genau EINEN Schritt „natur.rechnung“ (keine Menübefehle, kein „dokument.setzen“). Eine Textaufgabe übersetzt du so: „art“ ist molmasse (eingabe = Summenformel wie H2SO4), reaktion (eingabe wie Fe + O2 -> Fe2O3, ohne Faktoren), "
               + "einheiten (eingabe wie 100 km/h in m/s) oder physik (formel = Kennung aus der Formelsammlung, gesucht = Zeichen der gesuchten Größe, eingabe = bekannte Werte wie „s = 42,195 km; t = 2,5 h“, optional ziel = Einheit des Ergebnisses). "
               + "Formeln: " + string.Join(", ", Formelsammlung.Alle.Select(f => $"{f.Id} ({f.Gleichung})")) + ". Gib NUR die Aufgabe an, nie das Ergebnis und nie Faktoren einer Reaktion – das rechnet das Programm. "
               + "Beispiel für „Molmasse von Schwefelsäure“: {\"steps\":[{\"action\":\"natur.rechnung\",\"args\":{\"art\":\"molmasse\",\"eingabe\":\"H2SO4\"}}]}",
        Workspace = NwWorkspace,
        Properties = NwProperties,
        Status = d => { var n = d.List("eintraege").Count; var e = NwElement(d); return $"{e.Name} ({e.Symbol}), {e.MasseText} g/mol · Laborheft: {n} Eintrag/Einträge"; },
        Help = "Oben die Ansicht wählen. Im Periodensystem ein Element anklicken – rechts stehen seine Daten. Bei Molmasse eine Summenformel eintippen (Klammern und ·5H2O gehen), bei Reaktionen die Stoffe mit Pfeil (H2 + O2 -> H2O). "
             + "Physik: Formel wählen, gesuchte Größe wählen, bekannte Werte mit Einheit eintragen („h = 20 ± 0,1 m“) – Trendo rechnet in SI, prüft die Einheiten und gibt die Unsicherheit an. "
             + "„Ins Laborheft“ legt das Ergebnis ab; Export als PDF oder Tabelle. Elementdaten: eigene Tabelle nach IUPAC/CIAAW, abgeglichen mit PubChem.",
    }, "chemie", "physik", "naturwissenschaft", "periodensystem", "element", "molmasse", "molare masse", "reaktionsgleichung", "ausgleichen", "stöchiometrie", "formelsammlung", "einheiten", "umrechnen", "dimension", "si-einheiten");

    static string NwTitel(ProgramDocument d) => d.Text("title") is { Length: > 0 } t ? t : "Laborheft";
    public Element NwElement(ProgramDocument d) => Elemente.Nummer(Math.Clamp(_nwElement.GetValueOrDefault(d.Id, 26), 1, 118));
    public void NwWaehleElement(ProgramDocument d, int z) { _nwElement[d.Id] = z; Show(NaturKey); }
    public int NwAuswahl(ProgramDocument d) { var n = d.List("eintraege").Count; return n == 0 ? -1 : Math.Clamp(_nwSel.GetValueOrDefault(d.Id, n - 1), 0, n - 1); }

    void NwModus(ProgramDocument d, string m) { if (d.Text("modus") != m) d.Set("modus", m, "Ansicht"); }

    /// <summary>Eintrag aus der aktuellen Ansicht: was jetzt in den Feldern steht.</summary>
    JsonObject NwAusAnsicht(ProgramDocument d, Element el) => d.Text("modus", "pse") switch
    {
        "molmasse" => NwEintrag("molmasse", d.Text("molEingabe")),
        "reaktion" => NwEintrag("reaktion", d.Text("rxEingabe")),
        "einheiten" => NwEintrag("einheiten", d.Text("einEingabe")),
        "physik" => NwEintrag("physik", d.Text("phWerte"), d.Text("phFormel"), d.Text("phGesucht"), d.Text("phZiel")),
        _ => NwEintrag("element", el.Symbol),
    };

    public void NwRechnen(ProgramDocument d)
    {
        NwEingabenSichern(d);
        var e = NwAusAnsicht(d, NwElement(d));
        var b = NwRechne(e);
        if (b.Fehler is { } f) throw new ArgumentException(f);
        d.Edit("Ins Laborheft", x => ((JsonArray)(x["eintraege"] ??= new JsonArray())).Add(e));
        _nwSel[d.Id] = d.List("eintraege").Count - 1;
        _progMsg[NaturKey] = b.Aufgabe + ": " + b.Ergebnis + (b.Proben.Count == 0 || b.Proben.All(p => p.Ok) ? "" : " – Achtung: Probe nicht bestanden.");
    }

    /// <summary>Getippte, noch nicht übernommene Felder (Pause unter einer Sekunde) vor dem Rechnen übernehmen.</summary>
    void NwEingabenSichern(ProgramDocument d)
    {
        foreach (var (feld, wert) in _nwTyped.Where(t => t.Key.Doc == d.Id).Select(t => (t.Key.Feld, t.Value)).ToList())
            if (d.Text(feld) != wert) d.Set(feld, wert, "Eingabe");
        foreach (var k in _nwTyped.Keys.Where(k => k.Doc == d.Id).ToList()) _nwTyped.Remove(k);
    }
    readonly Dictionary<(string Doc, string Feld), string> _nwTyped = [];

    void NwLoeschen(ProgramDocument d, int i)
    {
        if (i < 0) throw new ArgumentException("Das Laborheft ist noch leer.");
        d.Edit("Eintrag löschen", x => ((JsonArray)x["eintraege"]!).RemoveAt(i));
        _nwSel[d.Id] = Math.Max(0, i - 1);
    }

    /// <summary>Werte wie „h = 20 ± 0,1 m; t = 2,5 h“ lesen.</summary>
    public static Dictionary<string, GroesseEingabe> NwWerte(string text)
    {
        var r = new Dictionary<string, GroesseEingabe>(StringComparer.Ordinal);
        foreach (var teil in (text ?? "").Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var m = Regex.Match(teil, @"^\s*([^\s=]+)\s*=\s*([-−]?[\d.,]+(?:[eE][-+]?\d+)?)\s*(?:(?:±|\+-|\+/-)\s*([\d.,]+(?:[eE][-+]?\d+)?))?\s*(.*)$");
            if (!m.Success) throw new FormatException($"„{teil}“ nicht verstanden – schreib z. B. „h = 20 m“ oder „h = 20 ± 0,1 m“.");
            r[m.Groups[1].Value] = new GroesseEingabe(m.Groups[2].Value.Replace('−', '-'), m.Groups[4].Value.Trim(), m.Groups[3].Value);
        }
        return r;
    }

    static readonly CultureInfo NwDe = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Einen Eintrag rechnen – immer frisch aus dem Kern.</summary>
    public static BlattEintrag NwRechne(JsonNode? e)
    {
        using var zeit = Rechenzeit.Grenze(Rechenzeit.Standard); // T-0781: nie einfrieren
        var art = Js(e?["art"]); var ein = Js(e?["eingabe"]);
        try
        {
            switch (art)
            {
                case "molmasse":
                    {
                        var m = Chemie.Molmasse(ein);
                        var summeAnteile = m.Zeilen.Sum(z => z.Anteil); var zeilen = m.Zeilen.Sum(z => z.Beitrag);
                        var ok1 = Math.Abs(summeAnteile - 1) < 1e-9; var ok2 = Math.Abs(zeilen - m.Summe) < 1e-9;
                        return new BlattEintrag("Molmasse", "Molare Masse von " + Chemie.Schoen(m.Formel.Text), Chemie.MolmasseWeg(m), $"M = {Chemie.G(m.Summe)} g/mol" + (m.Geschaetzt ? " (mit Massenzahl eines radioaktiven Elements)" : ""), "",
                            [new Probe(ok2, $"{(ok2 ? "✓" : "✗")} Gegenrechnung: Beiträge der Zeilen ergeben zusammen {Chemie.G(zeilen)} g/mol."), new Probe(ok1, $"{(ok1 ? "✓" : "✗")} Massenanteile ergeben zusammen {(summeAnteile * 100).ToString("0.00", NwDe)} %.")]);
                    }
                case "reaktion":
                    {
                        var r = Chemie.Ausgleichen(ein);
                        return new BlattEintrag("Reaktion ausgleichen", "Gleiche aus: " + ein, r.Schritte, r.Gleichung, "", r.Proben);
                    }
                case "einheiten":
                    {
                        var r = Einheiten.Ausrechnen(ein);
                        return new BlattEintrag("Einheiten", "Rechne: " + ein, r.Schritte, r.Text, "", r.Proben);
                    }
                case "physik":
                    {
                        var f = Formelsammlung.Finde(Js(e?["formel"])) ?? throw new ArgumentException("Bitte eine Formel wählen.");
                        var r = Formelsammlung.Rechne(f.Id, Js(e?["gesucht"]), NwWerte(ein), Js(e?["ziel"]) is { Length: > 0 } z ? z : null);
                        return new BlattEintrag("Physik: " + f.Name, $"{f.Name} ({f.Gleichung}): gesucht {r.Gesucht}, gegeben {ein}", r.Schritte, r.Text, "", r.Proben);
                    }
                case "element":
                    {
                        var el = Elemente.Finde(ein) ?? throw new ArgumentException($"„{ein}“ ist kein Element.");
                        var m = Chemie.Molmasse(el.Symbol);
                        var ok = Math.Abs(m.Summe - el.Masse) < 1e-12;
                        return new BlattEintrag("Element", $"{el.Name} ({el.Symbol})", [
                            new("Ordnungszahl, Periode, Gruppe, Block", $"Z = {el.Z}, Periode {el.Periode}, {(el.Gruppe is { } g ? "Gruppe " + g : "Lanthanoide/Actinoide")}, {el.Block}-Block"),
                            new("Art und Zustand bei 25 °C", $"{el.Kategorie}, {el.Zustand}"),
                            new("Atommasse", $"{el.MasseText} g/mol" + (el.MasseGeschaetzt ? " (Massenzahl des langlebigsten bekannten Isotops)" : " (IUPAC/CIAAW, gekürzt)")),
                            new("Elektronegativität (Pauling)", el.En?.ToString("0.00", NwDe) ?? "keine gesicherte Angabe"),
                            new("Elektronenkonfiguration", Elemente.Hochgestellt(el.Konfiguration)),
                        ], $"{el.Name}: {el.MasseText} g/mol", "", [new Probe(ok, $"{(ok ? "✓" : "✗")} Molmasse des Elements aus der Formel „{el.Symbol}“ = Atommasse der Tabelle.")]);
                    }
                default: throw new ArgumentException("Unbekannte Art des Eintrags.");
            }
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or DivideByZeroException or OverflowException or KeyNotFoundException or OperationCanceledException)
        {
            return new BlattEintrag(art, ein, [], "", "", [], ex.Message);
        }
    }

    IEnumerable<BlattEintrag> NwBlatt(ProgramDocument d) => d.List("eintraege").Select(NwRechne);

    string NwCsv(ProgramDocument d)
    {
        var sb = new StringBuilder("Nr.;Art;Aufgabe;Ergebnis;Probe\r\n");
        var i = 0;
        foreach (var b in NwBlatt(d))
            sb.Append(++i).Append(';').Append(Csv(b.Art)).Append(';').Append(Csv(b.Aufgabe)).Append(';').Append(Csv(b.Fehler ?? b.Ergebnis)).Append(';').Append(b.Fehler is not null ? "nicht gerechnet" : b.Proben.All(p => p.Ok) ? "bestanden" : "NICHT bestanden").Append("\r\n");
        return sb.ToString();
        static string Csv(string s) => s.Contains(';') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    // ---------- KI-Fachaktion ----------

    ProgramKiAction NwKiRechnung => new(new AssistAction("natur.rechnung", "Textaufgabe ins Laborheft rechnen", ["textaufgabe"], // wie beim Mathe-Helfer: nur über die KI
        [new AssistParam("art", "Art", AssistKind.Choice, ["molmasse", "reaktion", "einheiten", "physik"], Words: ["art"]),
         new AssistParam("eingabe", "Aufgabe (Formel, Reaktion, Einheiten-Rechnung oder bekannte Werte)"),
         new AssistParam("formel", "Formel der Formelsammlung (nur Physik)", AssistKind.Choice, [.. Formelsammlung.Alle.Select(f => f.Id)], Required: false, Words: ["formel"]),
         new AssistParam("gesucht", "gesuchte Größe (nur Physik)", Required: false, Words: ["gesucht"]),
         new AssistParam("ziel", "Einheit des Ergebnisses", Required: false, Words: ["in"])]),
        (d, a) =>
        {
            var art = (a.GetValueOrDefault("art") ?? "").Trim().ToLowerInvariant();
            var ein = (a.GetValueOrDefault("eingabe") ?? "").Trim();
            if (art == "reaktion" && Regex.IsMatch(ein, @"(^|[+>=→⇌⟶]\s*)\d+\s*[A-Z(\[]")) throw new InvalidOperationException("Die KI hat schon Faktoren vor die Stoffe geschrieben – ausgeglichen wird nur im Programm. Bitte die Reaktion ohne Faktoren angeben.");
            var e = NwEintrag(art, ein, a.GetValueOrDefault("formel") ?? "", a.GetValueOrDefault("gesucht") ?? "", a.GetValueOrDefault("ziel") ?? "", "KI: Textaufgabe");
            var b = NwRechne(e);
            if (b.Fehler is { } f) throw new InvalidOperationException($"Das versteht das Programm nicht ({art}: „{ein}“): {f}");
            d.Edit("Rechnung aus Textaufgabe", x => ((JsonArray)(x["eintraege"] ??= new JsonArray())).Add(e));
            _nwSel[d.Id] = d.List("eintraege").Count - 1;
        },
        (d, created) => [])
    {
        OnApplyNote = "Gerechnet hat das Programm, nicht die KI – „Ja, behalten“ übernimmt den Eintrag ins Laborheft (ein Rückgängig-Schritt).",
    };

    // ---------- Arbeitsfläche ----------

    Control NwWorkspace(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "nw-workspace", Spacing = 12, Margin = new Thickness(4, 0, 10, 4) };
        var modus = d.Text("modus", "pse");
        var tabs = new WrapPanel { Name = "nw-modi" };
        foreach (var (id, label) in NwModi)
        {
            var b = new Button { Name = "nw-tab-" + id, Content = label, Padding = new Thickness(12, 5), Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(13) };
            if (id == modus) QuietGold(b);
            var m = id; b.Click += (_, _) => ProgramRun(NaturKey, () => NwModus(d, m));
            tabs.Children.Add(b);
        }
        sp.Children.Add(tabs);
        sp.Children.Add(modus switch
        {
            "molmasse" => NwMolmasse(d),
            "reaktion" => NwReaktion(d),
            "physik" => NwPhysik(d),
            "einheiten" => NwEinheiten(d),
            _ => NwPse(d),
        });
        sp.Children.Add(NwLaborheft(d));
        return sp;
    }

    /// <summary>Feld, dessen Wert beim Tippen sofort live gerechnet und nach einer Pause ins Dokument übernommen wird.</summary>
    TextBox NwFeld(ProgramDocument d, string feld, string name, string platz, Action<string> live)
    {
        var t = KrBox(d, feld, name, platz, false, s => { _nwTyped[(d.Id, feld)] = s; live(s); });
        t.FontFamily = Mono; t.FontSize = 16;
        t.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; ProgramRun(NaturKey, () => NwRechnen(d)); } };
        return t;
    }

    Control NwErgebnis(BlattEintrag b, string name)
    {
        var sp = new StackPanel { Name = name, Spacing = 6 };
        if (b.Fehler is { } f) { sp.Children.Add(P(f, K4Warn, 13)); return sp; }
        sp.Children.Add(new Border { Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(8), Background = Brush.Parse("#1AD4A017"), BorderBrush = Brush.Parse("#55D4A017"), BorderThickness = new Thickness(1),
            Child = new TextBlock { Name = name + "-wert", Text = b.Ergebnis, Foreground = Accent, FontSize = 19, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap } });
        var k = 0;
        foreach (var s in b.Schritte)
            sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("24,*"), Children = {
                new TextBlock { Text = ++k + ".", Foreground = Accent, FontSize = 12.5 },
                Pos(new StackPanel { Children = { new TextBlock { Text = s.Text, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap }, new TextBlock { Text = s.Formel, Foreground = Text, FontFamily = Mono, FontSize = 13, TextWrapping = TextWrapping.Wrap } } }, 1) } });
        foreach (var p in b.Proben) sp.Children.Add(new TextBlock { Text = p.Text, Foreground = p.Unsicher ? K4Warn : p.Ok ? K4Good : K4Bad, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        return sp;
    }

    Control NwLive(ProgramDocument d, string feld, string name, string platz, string erklaerung, Func<string, BlattEintrag> rechne)
    {
        var holder = new ContentControl { Name = name + "-ergebnis" };
        void Zeige(string s) => holder.Content = s.Trim().Length == 0 ? P("Noch keine Eingabe.", Muted, 12) : NwErgebnis(rechne(s), name + "-live");
        var box = NwFeld(d, feld, name, platz, Zeige);
        Zeige(d.Text(feld));
        return KrCard(new StackPanel { Spacing = 8, Children = { P(erklaerung, Muted, 12), box, holder } }, name + "-karte");
    }

    Control NwMolmasse(ProgramDocument d) => NwLive(d, "molEingabe", "nw-mol", "Summenformel, z. B. Ca(OH)2 oder CuSO4·5H2O", "Summenformel eintippen – Groß- und Kleinschreibung zählt (Co = Cobalt, CO = Kohlenstoffmonoxid). Klammern, eckige Klammern und Kristallwasser (·5H2O) gehen.",
        s => NwRechne(NwEintrag("molmasse", s)));

    Control NwReaktion(ProgramDocument d) => NwLive(d, "rxEingabe", "nw-rx", "z. B. Fe + O2 -> Fe2O3", "Ausgangsstoffe und Produkte ohne Faktoren eingeben, getrennt durch einen Pfeil (->, →, =). Ionen mit Ladung: Fe^3+, SO4^2-.",
        s => NwRechne(NwEintrag("reaktion", s)));

    Control NwEinheiten(ProgramDocument d) => NwLive(d, "einEingabe", "nw-ein", "z. B. 3 km / 20 min in km/h", "Mit Einheiten rechnen und umrechnen: „100 km/h in m/s“, „12 V · 0,5 A“, „20 °C in K“. Ungleiche Größen (Meter plus Sekunden) lehnt die Dimensionsprüfung ab.",
        s => NwRechne(NwEintrag("einheiten", s)));

    Control NwPhysik(ProgramDocument d)
    {
        var f = Formelsammlung.Finde(d.Text("phFormel")) ?? Formelsammlung.Alle[0];
        var formeln = Formelsammlung.Alle.ToList();
        var wahl = new ComboBox { Name = "nw-ph-formel", HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = formeln.Select(x => $"{x.Bereich}: {x.Name}  ({x.Gleichung})").ToList(), SelectedIndex = formeln.IndexOf(f) };
        wahl.SelectionChanged += (_, _) => { if (wahl.SelectedIndex >= 0 && formeln[wahl.SelectedIndex].Id != d.Text("phFormel")) ProgramRun(NaturKey, () => NwFormelWaehlen(d, formeln[wahl.SelectedIndex])); };
        var gesucht = new WrapPanel { Name = "nw-ph-gesucht" };
        foreach (var g in f.Groessen.Where(g => f.Umgestellt.ContainsKey(g.Zeichen)))
        {
            var b = new Button { Name = "nw-ph-ges-" + Formelsammlung.Ersatz(g.Zeichen).Replace("Δ", "d"), Content = $"{g.Zeichen} – {g.Bedeutung}".Replace("_", "__"), FontSize = 12, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 6, 6) };
            if (g.Zeichen == d.Text("phGesucht")) QuietGold(b);
            var z = g.Zeichen; b.Click += (_, _) => ProgramRun(NaturKey, () => NwGesucht(d, z));
            gesucht.Children.Add(b);
        }
        var bekannt = string.Join("; ", f.Groessen.Where(g => g.Zeichen != d.Text("phGesucht")).Select(g => $"{g.Zeichen} = … {g.Einheit}" + (g.Konstante is { } k ? $" (Vorgabe {k})" : "")));
        var holder = new ContentControl { Name = "nw-ph-ergebnis" };
        void Zeige(string werte) => holder.Content = NwErgebnis(NwRechne(NwEintrag("physik", werte, f.Id, d.Text("phGesucht"), d.Text("phZiel"))), "nw-ph-live");
        var werte = NwFeld(d, "phWerte", "nw-ph-werte", "bekannte Werte, z. B. " + bekannt, Zeige);
        var ziel = KrBox(d, "phZiel", "nw-ph-ziel", "Einheit des Ergebnisses (leer = " + (f.Groessen.FirstOrDefault(g => g.Zeichen == d.Text("phGesucht"))?.Einheit ?? "SI") + ")", false, s => { _nwTyped[(d.Id, "phZiel")] = s; holder.Content = NwErgebnis(NwRechne(NwEintrag("physik", werte.Text ?? "", f.Id, d.Text("phGesucht"), s)), "nw-ph-live"); });
        Zeige(d.Text("phWerte"));
        return KrCard(new StackPanel { Spacing = 8, Children = {
            P("Formel wählen", Muted, 11), wahl, P("Gesucht ist …", Muted, 11), gesucht,
            P("Gegeben (Werte mit Einheit; Unsicherheit mit ±; mehrere mit „;“ trennen): " + bekannt, Muted, 11), werte, ziel, holder,
        } }, "nw-ph-karte");
    }

    void NwFormelWaehlen(ProgramDocument d, PhysikFormel f)
    {
        var ges = f.Umgestellt.Keys.First();
        var werte = string.Join("; ", f.Groessen.Where(g => g.Zeichen != ges && g.Konstante is null).Select(g => $"{g.Zeichen} = 1 {g.Einheit}"));
        d.Edit("Formel wählen", x => { x["phFormel"] = f.Id; x["phGesucht"] = ges; x["phWerte"] = werte; x["phZiel"] = ""; });
    }

    void NwGesucht(ProgramDocument d, string z)
    {
        var f = Formelsammlung.Finde(d.Text("phFormel")) ?? Formelsammlung.Alle[0];
        // die bisher gesuchte Größe wird zu einer bekannten, die neue gesuchte fällt aus den Werten heraus
        var alt = NwWerte(d.Text("phWerte"));
        alt.Remove(z);
        var neu = string.Join("; ", f.Groessen.Where(g => g.Zeichen != z && g.Konstante is null || alt.ContainsKey(g.Zeichen) && g.Zeichen != z)
            .Select(g => alt.TryGetValue(g.Zeichen, out var w) ? $"{g.Zeichen} = {w.Wert}{(w.Unsicherheit.Length > 0 ? " ± " + w.Unsicherheit : "")} {w.Einheit}".TrimEnd() : $"{g.Zeichen} = 1 {g.Einheit}"));
        d.Edit("Gesuchte Größe", x => { x["phGesucht"] = z; x["phWerte"] = neu; x["phZiel"] = ""; });
    }

    Control NwPse(ProgramDocument d)
    {
        var sel = NwElement(d);
        var faerben = d.Text("faerben", "kategorie");
        var grid = new Grid { Name = "nw-pse", ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", 18))), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto,10,Auto,Auto") };
        double enMax = 3.98, mMax = Elemente.Alle.Max(e => e.Masse);
        foreach (var e in Elemente.Alle)
        {
            var (r, c) = Elemente.Raster(e);
            string farbe = faerben switch
            {
                "en" => e.En is { } en ? Mische("#2A2418", "#D4A017", en / enMax) : "#2A2A2A",
                "masse" => Mische("#2A2418", "#D4A017", e.Masse / mMax),
                _ => NwFarbe.GetValueOrDefault(e.Kategorie, "#333333"),
            };
            var b = new Button { Name = "nw-el-" + e.Symbol, Padding = new Thickness(2), Margin = new Thickness(1.5), MinHeight = 46, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
                Background = Brush.Parse(farbe), BorderBrush = e.Z == sel.Z ? Accent : Brush.Parse("#22FFFFFF"), BorderThickness = new Thickness(e.Z == sel.Z ? 2 : 1), CornerRadius = new CornerRadius(4),
                Content = new StackPanel { Spacing = 0, HorizontalAlignment = HorizontalAlignment.Center, Children = {
                    new TextBlock { Text = e.Z.ToString(CultureInfo.InvariantCulture), FontSize = 8.5, Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock { Text = e.Symbol, FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = Text, HorizontalAlignment = HorizontalAlignment.Center },
                } } };
            ToolTip.SetTip(b, $"{e.Name} – {e.MasseText} g/mol");
            Avalonia.Automation.AutomationProperties.SetName(b, $"{e.Name}, Ordnungszahl {e.Z}");
            var z = e.Z; b.Click += (_, _) => NwWaehleElement(d, z);
            b.ContextMenu = new ContextMenu { Items = { NwMenu($"nw-ctx-mol-{e.Symbol}", "Molmasse rechnen", () => d.Edit("Molmasse", x => { x["modus"] = "molmasse"; x["molEingabe"] = e.Symbol; })),
                                                        NwMenu($"nw-ctx-heft-{e.Symbol}", "Steckbrief ins Laborheft", () => { _nwElement[d.Id] = z; d.Edit("Ins Laborheft", x => ((JsonArray)(x["eintraege"] ??= new JsonArray())).Add(NwEintrag("element", e.Symbol))); _nwSel[d.Id] = d.List("eintraege").Count - 1; }) } };
            Grid.SetRow(b, r >= 8 ? r : r); Grid.SetColumn(b, c);
            grid.Children.Add(b);
        }
        var legende = new WrapPanel { Name = "nw-legende", Margin = new Thickness(0, 8, 0, 0) };
        if (faerben == "kategorie")
            foreach (var (k, f) in NwFarbe) legende.Children.Add(new Border { Background = Brush.Parse(f), CornerRadius = new CornerRadius(3), Padding = new Thickness(7, 2), Margin = new Thickness(0, 0, 6, 4), Child = new TextBlock { Text = k, FontSize = 11, Foreground = Text } });
        else legende.Children.Add(P(faerben == "en" ? "Je heller, desto höher die Elektronegativität (grau: keine gesicherte Angabe)." : "Je heller, desto schwerer das Atom.", Muted, 11));
        var suche = new TextBox { Name = "nw-suche", PlaceholderText = "Element suchen: Name, Symbol oder Ordnungszahl", Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
        suche.KeyDown += (_, e) => { if (e.Key == Key.Enter && Elemente.Finde(suche.Text ?? "") is { } el) { e.Handled = true; NwWaehleElement(d, el.Z); } };
        var detail = NwSteckbrief(sel, d);
        // T-0784 (V10 „Einfach“): statt 118 einzelner Knöpfe eine Auswahl mit allen Elementen; die Tabelle, Färbung und das Rechtsklick-Menü je Element zeigt „Erweitert“
        if (IsSimple)
        {
            var alle = Elemente.Alle.ToList();
            var wahl = new ComboBox { Name = "nw-el-wahl", HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 320, ItemsSource = alle.Select(x => $"{x.Z} {x.Symbol} – {x.Name}").ToList(), SelectedIndex = alle.FindIndex(x => x.Z == sel.Z) };
            Avalonia.Automation.AutomationProperties.SetName(wahl, "Element wählen");
            wahl.SelectionChanged += (_, _) => { if (wahl.SelectedIndex >= 0 && alle[wahl.SelectedIndex].Z != sel.Z) NwWaehleElement(d, alle[wahl.SelectedIndex].Z); };
            var einfach = new StackPanel { Spacing = 6, Children = { suche, new TextBlock { Text = "Element wählen", Foreground = Muted, FontSize = 12 }, wahl,
                P("Das ganze Periodensystem als Tabelle zeigt die Bedienstufe „Erweitert“ („Mehr Werkzeuge“).", Muted, 11) } };
            return new Grid { ColumnDefinitions = new ColumnDefinitions("*,14,300"), Children = { KrCard(einfach, "nw-pse-karte"), Pos(detail, 2) } };
        }
        var tabelle = new StackPanel { Spacing = 6, Children = { suche, grid, legende } };
        return new Grid { ColumnDefinitions = new ColumnDefinitions("*,14,300"), Children = { KrCard(tabelle, "nw-pse-karte"), Pos(detail, 2) } };
    }

    static MenuItem NwMenu(string name, string text, Action a) { var m = new MenuItem { Name = name, Header = text }; m.Click += (_, _) => a(); return m; }

    static string Mische(string a, string b, double t)
    {
        t = Math.Clamp(t, 0, 1); var ca = Color.Parse(a); var cb = Color.Parse(b);
        byte L(byte x, byte y) => (byte)Math.Round(x + (y - x) * t);
        return $"#{L(ca.R, cb.R):X2}{L(ca.G, cb.G):X2}{L(ca.B, cb.B):X2}";
    }

    Control NwSteckbrief(Element e, ProgramDocument d)
    {
        var sp = new StackPanel { Name = "nw-steckbrief", Spacing = 6 };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = {
            new Border { Width = 64, Height = 64, CornerRadius = new CornerRadius(8), Background = Brush.Parse(NwFarbe.GetValueOrDefault(e.Kategorie, "#333")), BorderBrush = Accent, BorderThickness = new Thickness(1),
                Child = new TextBlock { Text = e.Symbol, FontSize = 26, FontWeight = FontWeight.Bold, Foreground = Text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } },
            new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Name = "nw-el-name", Text = e.Name, FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = Text }, new TextBlock { Text = e.Kategorie, Foreground = Muted, FontSize = 12 } } } } });
        void Zeile(string k, string v) => sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("120,*"), Children = { new TextBlock { Text = k, Foreground = Muted, FontSize = 12 }, Pos(new TextBlock { Text = v, Foreground = Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap }, 1) } });
        Zeile("Ordnungszahl", e.Z.ToString(CultureInfo.InvariantCulture));
        Zeile("Atommasse", e.MasseText + " g/mol" + (e.MasseGeschaetzt ? " (Massenzahl)" : ""));
        Zeile("Elektronegativität", e.En?.ToString("0.00", NwDe) ?? "—");
        Zeile("Periode / Gruppe", $"{e.Periode} / {(e.Gruppe is { } g ? g.ToString(CultureInfo.InvariantCulture) : "f-Block")}");
        Zeile("Block", e.Block + "-Block");
        Zeile("Zustand (25 °C)", e.Zustand);
        Zeile("Elektronen", Elemente.Hochgestellt(e.Konfiguration));
        var mol = new Button { Name = "nw-el-molmasse", Content = "Molmasse mit diesem Element …", Margin = new Thickness(0, 6, 0, 0) };
        mol.Click += (_, _) => ProgramRun(NaturKey, () => d.Edit("Molmasse", x => { x["modus"] = "molmasse"; x["molEingabe"] = e.Symbol; }));
        sp.Children.Add(mol);
        sp.Children.Add(P("Quelle: eigene Tabelle nach IUPAC/CIAAW (Atomgewichte 2021, Änderungen 2024), Elektronegativität nach Pauling; abgeglichen mit PubChem.", Muted, 10.5));
        return KrCard(sp, "nw-steckbrief-karte");
    }

    Control NwLaborheft(ProgramDocument d)
    {
        var l = d.List("eintraege");
        var sp = new StackPanel { Name = "nw-heft", Spacing = 6 };
        sp.Children.Add(H($"Laborheft ({l.Count})", 14));
        if (l.Count == 0) { sp.Children.Add(P("Noch leer. Rechne etwas und drück „Ins Laborheft“ (oben links, Enter im Eingabefeld) – oder gib der KI oben in der Auftragszeile eine Textaufgabe.", Muted, 12)); return KrCard(sp, "nw-heft-karte"); }
        var sel = NwAuswahl(d);
        var liste = new StackPanel { Name = "nw-heft-liste", Spacing = 3, Focusable = true };
        for (var i = 0; i < l.Count; i++)
        {
            var b = NwRechne(l[i]);
            var ok = b.Fehler is null && b.Proben.All(p => p.Ok);
            var zeile = new Button { Name = $"nw-heft-{i}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(8, 4),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("20,*,*"), Children = {
                    new TextBlock { Text = ok ? "✓" : "!", Foreground = ok ? K4Good : K4Warn, FontWeight = FontWeight.SemiBold },
                    Pos(new TextBlock { Text = b.Aufgabe, Foreground = Text, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis }, 1),
                    Pos(new TextBlock { Text = b.Fehler ?? b.Ergebnis, Foreground = b.Fehler is null ? Accent : K4Warn, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis }, 2) } } };
            if (i == sel) QuietGold(zeile);
            var k = i;
            zeile.Click += (_, _) => { _nwSel[d.Id] = k; Show(NaturKey); };
            zeile.ContextMenu = new ContextMenu { Items = { NwMenu($"nw-heft-ctx-loeschen-{i}", "Löschen", () => ProgramRun(NaturKey, () => NwLoeschen(d, k))) } };
            liste.Children.Add(zeile);
        }
        liste.KeyDown += (_, e) => { if (e.Key == Key.Delete) { e.Handled = true; ProgramRun(NaturKey, () => NwLoeschen(d, NwAuswahl(d))); } };
        sp.Children.Add(liste);
        if (sel >= 0) sp.Children.Add(new Expander { Name = "nw-heft-detail", Header = H("Rechenweg des gewählten Eintrags", 13), IsExpanded = true, HorizontalAlignment = HorizontalAlignment.Stretch, Content = NwErgebnis(NwRechne(l[sel]), "nw-heft-weg") });
        return KrCard(sp, "nw-heft-karte");
    }

    Control NwProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 8, Name = "nw-props" };
        sp.Children.Add(H("Laborheft", 13));
        sp.Children.Add(KrBox(d, "title", "nw-titel", "Titel (für den Export)"));
        sp.Children.Add(H("Formelsammlung", 13));
        foreach (var bereich in Formelsammlung.Bereiche)
        {
            sp.Children.Add(P(bereich, Muted, 11));
            foreach (var f in Formelsammlung.Alle.Where(x => x.Bereich == bereich))
            {
                var b = new Button { Name = "nw-fs-" + f.Id, Content = $"{f.Name}: {f.Gleichung}".Replace("_", "__"), FontSize = 11.5, Padding = new Thickness(6, 2), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent };
                var ff = f; b.Click += (_, _) => ProgramRun(NaturKey, () => { NwFormelWaehlen(d, ff); NwModus(d, "physik"); });
                sp.Children.Add(b);
            }
        }
        return sp;
    }
}
