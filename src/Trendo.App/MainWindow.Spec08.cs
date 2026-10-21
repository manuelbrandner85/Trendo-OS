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
        return FachPage("Mode & Produkt", sp);
    }
}
