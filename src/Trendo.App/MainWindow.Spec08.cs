using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-08: Mode & Produkt.</summary>
public partial class MainWindow
{
    private void InitSpec08() => RegisterTab(FachTabs, "mode", "Mode & Produkt", "SPEC-08", FashionView, "mode", "kleidung", "größe", "schuhgröße", "nähen", "stoff", "produkt", "sku", "variante", "kalkulation", "preis");

    private Control FashionView()
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(FachTool("fa-body", "Welche Größe passt?", "Maße in cm am Körper (locker gemessen). Damen: d, Herren: h.",
            [("who", "Damen/Herren (d/h)", "d"), ("chest", "Brust (cm)", "90"), ("waist", "Taille (cm)", "74"), ("hip", "Hüfte (cm)", "98")],
            v => { var wmn = !v("who").Trim().StartsWith("h", StringComparison.OrdinalIgnoreCase); var hip = v("hip").Trim() == "" ? 0 : Fach.Num(v("hip"), "Hüfte"); var r = Fashion.FromBody(wmn, Fach.Num(v("chest"), "Brust"), Fach.Num(v("waist"), "Taille"), hip); return $"Größe EU {r.Eu} ({r.Letter}). {r.Note}" + (wmn && r.Eu <= 50 ? "\n" + Fashion.WomenSize(r.Eu) : ""); }, "Größe finden"));
        sp.Children.Add(FachTool("fa-shoe", "Schuhgröße umrechnen", "EU-Größe eingeben – oder die Fußlänge in cm mit „cm“ dahinter, z. B. „26 cm“.",
            [("eu", "EU-Größe oder Fußlänge", "42")],
            v => { var s = v("eu"); var eu = s.Contains("cm", StringComparison.OrdinalIgnoreCase) ? Fashion.ShoeFromFoot(Fach.Num(s.ToLowerInvariant().Replace("cm", ""), "Fußlänge")) : Fach.Num(s, "Größe"); var x = Fashion.Shoe(eu); return $"EU {Fach.F(x.Eu, 1)} · Fuß {Fach.F(x.Cm, 1)} cm · UK {Fach.F(x.Uk, 1)} · US Herren {Fach.F(x.UsMen, 1)} · US Damen {Fach.F(x.UsWomen, 1)}"; }, "Umrechnen"));
        sp.Children.Add(FachTool("fa-fabric", "Stoffbedarf", "Schnittteile als „Breite×Höhe×Anzahl“ in cm, mit | getrennt. Nahtzugabe 1,5 cm und 10 % Reserve sind drin.",
            [("pieces", "Schnittteile", "60x80x2 | 55x65x2 | 40x60x2"), ("width", "Stoffbreite (cm)", "140")],
            v => { var ps = v("pieces").Split('|', StringSplitOptions.RemoveEmptyEntries).Select(p => p.ToLowerInvariant().Replace('×', 'x').Split('x')).Select(a => a.Length >= 2 ? (Fach.Num(a[0], "Breite"), Fach.Num(a[1], "Höhe"), a.Length > 2 ? (int)Fach.Num(a[2], "Anzahl") : 1) : throw new FormatException("Schnittteile bitte als „60x80x2“ angeben.")).ToList(); var (m, rows) = Fashion.Fabric(ps, Fach.Num(v("width"), "Stoffbreite")); return $"Du brauchst {Fach.F(m, 1)} m Stoff ({rows} Lagen nebeneinander)."; }));
        sp.Children.Add(FachTool("fa-var", "Varianten & Artikelnummern", "Alle Kombinationen aus Größen und Farben, als CSV für Shop oder Lager.",
            [("sku", "Artikel-Kürzel", "SHIRT-01"), ("sizes", "Größen (Komma)", "S, M, L, XL"), ("colors", "Farben (Komma)", "Schwarz, Weiß"), ("mat", "Material", "Baumwolle")],
            v => { var vs = Fashion.Variants(v("sku"), v("sizes").Split(','), v("colors").Split(','), v("mat")); var p = Path.Combine(Fach.StoreDir(_core.DataRoot, "mode"), "varianten.csv"); File.WriteAllText(p, Fashion.VariantsCsv(vs)); return $"{vs.Count} Varianten, z. B. {vs[0].Sku}\nGespeichert: {p}"; }, "Erzeugen"));
        sp.Children.Add(FachTool("fa-price", "Preis kalkulieren", "Selbstkosten + Gewinn + 19 % MwSt, gerundet auf einen runden Ladenpreis.",
            [("mat", "Material (€)", "8"), ("lab", "Arbeit (€)", "12"), ("ovh", "Gemeinkosten (%)", "20"), ("pro", "Gewinn (%)", "40")],
            v => { var p = Fashion.Price(Fach.Num(v("mat")), Fach.Num(v("lab")), Fach.Num(v("ovh")), Fach.Num(v("pro"))); return $"Selbstkosten {Fach.F(p.Cost)} € → Ladenpreis {Fach.F(p.Gross)} € (netto {Fach.F(p.Net)} €, MwSt {Fach.F(p.Vat)} €, Marge {Fach.F(p.MarginPct, 1)} %)"; }));
        sp.Children.Add(FachGroup("fa-sizes", "Weitere Größen", false,
            FachTool("fa-kid", "Kindergröße", "Körpergröße des Kindes in cm.", [("h", "Körpergröße (cm)", "95")],
                v => { var k = FashionPlus.KidSize(Fach.Num(v("h"))); return $"Größe {k.Size} (etwa {k.Age})"; }, "Finden"),
            FachTool("fa-bra", "BH-Größe", "Unterbrust eng, Brust an der vollsten Stelle messen (cm).", [("ub", "Unterbrust (cm)", "75"), ("b", "Brust (cm)", "91")],
                v => $"Größe {FashionPlus.BraSize(Fach.Num(v("ub")), Fach.Num(v("b")))} (EU). Marken fallen unterschiedlich aus – im Zweifel anprobieren.", "Finden"),
            FachTool("fa-ring", "Ringgröße", "Faden um den Finger legen und messen (Innenumfang in mm).", [("c", "Umfang (mm)", "54")],
                v => { var r = FashionPlus.Ring(Fach.Num(v("c"))); return $"EU {Fach.F(r.Eu, 0)} · Ø {Fach.F(r.DiameterMm, 1)} mm · US {Fach.F(r.Us, 1)}"; }, "Umrechnen")));
        sp.Children.Add(FachGroup("fa-make", "Herstellen", false,
            FachTool("fa-grade", "Maße gradieren", "Maß der Basisgröße und Sprung pro Größe (z. B. Brustweite +4 cm).",
                [("sizes", "Größen", "XS, S, M, L, XL"), ("base", "Basisgröße", "M"), ("val", "Maß (cm)", "100"), ("step", "Sprung (cm)", "4")],
                v => string.Join(" · ", FashionPlus.Grade(v("sizes").Split(',').Select(x => x.Trim()).Where(x => x != "").ToList(), v("base"), Fach.Num(v("val")), Fach.Num(v("step"))).Select(g => $"{g.Size}: {Fach.F(g.Value, 1)} cm")), "Gradieren"),
            FachTool("fa-yarn", "Garnbedarf", "Maschenprobe auf 10 × 10 cm und Lauflänge von der Banderole.",
                [("w", "Breite (cm)", "50"), ("h", "Länge (cm)", "60"), ("s", "Maschen / 10 cm", "22"), ("r", "Reihen / 10 cm", "30"), ("m", "m je Knäuel", "125")],
                v => { var y = FashionPlus.Yarn(Fach.Num(v("w")), Fach.Num(v("h")), Fach.Num(v("s")), Fach.Num(v("r")), Fach.Num(v("m"))); return $"{y.Stitches} Maschen × {y.Rows} Reihen · ca. {Fach.F(y.Meters, 0)} m Garn → {y.Balls} Knäuel (mit 10 % Reserve)"; }),
            FachTool("fa-label", "Textiletikett prüfen", "Materialangaben wie „95 % Baumwolle, 5 % Elasthan“. Prüft Summe und erlaubte Bezeichnungen (EU-Textilverordnung).",
                [("t", "Angabe", "95 % Baumwolle, 5 % Elasthan")],
                v => { var r = FashionPlus.TextileLabel(v("t")); return r.Ok ? $"✓ Etikett: {r.Label}" : "⚠ " + string.Join("\n⚠ ", r.Problems) + (r.Label == "" ? "" : $"\nVorschlag: {r.Label}"); }, "Prüfen")));
        sp.Children.Add(FachGroup("fa-sell", "Verkaufen", false,
            FachTool("fa-ean", "EAN-Strichcode", "12 Ziffern → Prüfziffer, oder 13 Ziffern prüfen.", [("e", "EAN", "400638133393")],
                v => { var d = new string(v("e").Where(char.IsDigit).ToArray()); return d.Length == 13 ? (FashionPlus.EanValid(d) ? "✓ Gültige EAN-13." : $"⚠ Prüfziffer falsch – richtig wäre {FashionPlus.EanCheckDigit(d[..12])}.") : $"Vollständige EAN: {d}{FashionPlus.EanCheckDigit(d)}"; }, "Prüfen"),
            FachTool("fa-tier", "Staffelpreise", "Stufen als „ab Menge:Rabatt %“, mit Leerzeichen getrennt.",
                [("p", "Listenpreis (€)", "29,90"), ("q", "Menge", "50"), ("t", "Stufen", "10:5 25:10 100:15")],
                v => { var tiers = v("t").Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split(':')).Select(x => x.Length == 2 ? ((int)Fach.Num(x[0]), Fach.Num(x[1])) : throw new FormatException("Stufe bitte als „10:5“.")); var r = FashionPlus.Tier(Fach.Num(v("p")), (int)Fach.Num(v("q")), tiers); return $"{Fach.F(r.DiscountPct, 0)} % Rabatt → {Fach.F(r.Unit)} € je Stück, zusammen {Fach.F(r.Total)} €"; }),
            FachTool("fa-sheet", "Produktdatenblatt", "Nutzt die Varianten und den Preis von oben (sofern ausgefüllt).",
                [("name", "Produktname", "Basic T-Shirt"), ("desc", "Beschreibung", "Weiches Shirt aus Bio-Baumwolle."), ("care", "Pflege", "30 °C, nicht im Trockner")],
                v => { string In(string k, string d) => _fachIn.GetValueOrDefault(k, d); var vs = Fashion.Variants(In("fa-var-sku", "SHIRT-01"), In("fa-var-sizes", "S, M, L, XL").Split(','), In("fa-var-colors", "Schwarz, Weiß").Split(','), In("fa-var-mat", "Baumwolle")); var pr = Fashion.Price(Fach.Num(In("fa-price-mat", "8")), Fach.Num(In("fa-price-lab", "12")), Fach.Num(In("fa-price-ovh", "20")), Fach.Num(In("fa-price-pro", "40"))); var md = FashionPlus.DataSheet(v("name"), In("fa-var-sku", "SHIRT-01"), v("desc"), In("fa-var-mat", "Baumwolle"), vs, pr, v("care")); var p = Path.Combine(Fach.StoreDir(_core.DataRoot, "mode"), "datenblatt.md"); File.WriteAllText(p, md); Launch(p); return "Gespeichert: " + p; }, "Erstellen")));
        return FachPage("Mode & Produkt", sp);
    }
}
