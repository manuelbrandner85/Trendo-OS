using Avalonia;
using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-01: Architektur & Innenraum – Räume erfassen, Material berechnen, Grundriss exportieren.</summary>
public partial class MainWindow
{
    private RoomPlanner? _rooms;
    public RoomPlanner Rooms => _rooms ??= new RoomPlanner(_core.DataRoot);

    private void InitSpec01() => RegisterTab(FachTabs, "architektur", "Räume & Wohnen", "SPEC-01", RoomsView, "architektur", "raum", "wohnung", "grundriss", "farbe", "streichen", "boden", "laminat", "innenraum");

    private Room FirstRoom() => Rooms.Project.Rooms.FirstOrDefault() ?? throw new InvalidOperationException("Lege zuerst oben einen Raum an.");

    private Control RoomsView()
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(P("Trage deine Räume ein (in Metern). Trendo rechnet aus, wie viel Farbe, Boden und Sockelleiste du brauchst, und zeichnet einen Grundriss.", Text));
        sp.Children.Add(FachTool("arch-add", "Raum hinzufügen", "Türen und Fenster werden von der Wandfläche abgezogen.",
            [("name", "Name", "Wohnzimmer"), ("l", "Länge (m)", "5"), ("w", "Breite (m)", "4"), ("h", "Höhe (m)", "2,5"), ("d", "Türen", "1"), ("f", "Fenster", "2")],
            v => { var r = Rooms.Add(v("name"), Fach.Num(v("l"), "Länge"), Fach.Num(v("w"), "Breite"), Fach.Num(v("h"), "Höhe"), (int)Fach.Num(v("d"), "Türen"), (int)Fach.Num(v("f"), "Fenster")); Show("architektur"); return $"„{r.Name}“ hinzugefügt: {Fach.F(r.Floor)} m² Boden."; }, "Hinzufügen"));
        var i = 0;
        foreach (var r in Rooms.Project.Rooms)
        {
            var n = i++;
            var del = new Button { Name = $"arch-del-{n}", Content = "Entfernen", Padding = new Thickness(8, 2) };
            del.Click += (_, _) => { Rooms.Remove(n); Show("architektur"); };
            sp.Children.Add(Row2(new StackPanel { Children = { new TextBlock { Text = r.Name, Foreground = Text, FontSize = 13 }, P($"{Fach.F(r.Length)} × {Fach.F(r.Width)} × {Fach.F(r.Height)} m · Boden {Fach.F(r.Floor)} m² · Wände {Fach.F(r.WallNet)} m² · Raum {Fach.F(r.Volume)} m³", null, 11) } }, del));
        }
        if (Rooms.Project.Rooms.Count > 0)
            sp.Children.Add(FachTool("arch-need", "Was brauche ich?", "Farbe für Wände und Decke, Bodenpakete mit 10 % Verschnitt.",
                [("pack", "m² je Bodenpaket", "2,2"), ("coats", "Anstriche", "2"), ("yield", "m² je Liter Farbe", "10")],
                v => { var m = RoomPlanner.Need(Rooms.Project.Rooms, Fach.Num(v("pack"), "Paket"), Fach.Num(v("coats"), "Anstriche"), Fach.Num(v("yield"), "Ergiebigkeit")); return $"Boden: {Fach.F(m.Floor)} m² → {m.FloorPacks} Pakete\nWände + Decke: {Fach.F(m.Walls)} m² → {Fach.F(m.PaintLiters, 1)} l Farbe ({m.PaintBuckets} Eimer à 10 l)\nSockelleisten: {Fach.F(m.SkirtingMeters)} m"; }));
        var svg = new Button { Name = "arch-svg", Content = "Grundriss speichern & öffnen", IsEnabled = Rooms.Project.Rooms.Count > 0 };
        svg.Click += (_, _) => { var p = Rooms.ExportSvg(); _w45Msg = "Grundriss gespeichert: " + p; Launch(p); Show("architektur"); };
        sp.Children.Add(svg);
        sp.Children.Add(FachGroup("arch-mat", "Material & Kosten", false,
            FachTool("arch-tile", "Fliesen", "Fläche in m² (z. B. Bad-Boden), Fliesenmaß in cm, Fuge in mm.",
                [("a", "Fläche (m²)", "6"), ("w", "Fliese Breite (cm)", "30"), ("h", "Fliese Höhe (cm)", "60"), ("j", "Fuge (mm)", "3"), ("p", "m² je Paket", "1,44")],
                v => { var t = Interior.Tiles(Fach.Num(v("a")), Fach.Num(v("w")), Fach.Num(v("h")), Fach.Num(v("j")), 10, Fach.Num(v("p"))); return $"{t.Tiles} Fliesen = {t.Packs} Pakete (10 % Verschnitt) · Fugenmörtel ca. {Fach.F(t.GroutKg, 1)} kg"; }),
            FachTool("arch-wall", "Tapete", "Für den ersten Raum der Liste. Standardrolle 10,05 × 0,53 m.",
                [("len", "Rollenlänge (m)", "10,05"), ("wid", "Rollenbreite (m)", "0,53"), ("rep", "Rapport (cm)", "0")],
                v => { var r = FirstRoom(); var t = Interior.Wallpaper(r, Fach.Num(v("len")), Fach.Num(v("wid")), Fach.Num(v("rep"))); return $"{r.Name}: {t.Strips} Bahnen, {t.StripsPerRoll} pro Rolle → {t.Rolls} Rollen"; }),
            FachTool("arch-cost", "Was kostet das?", "Preise aus dem Baumarkt eintragen.",
                [("bucket", "Farbe je 10-l-Eimer (€)", "45"), ("pack", "Boden je Paket (€)", "25"), ("sk", "Leiste je m (€)", "3")],
                v => { var c = Interior.Cost(RoomPlanner.Need(Rooms.Project.Rooms), Fach.Num(v("bucket")), Fach.Num(v("pack")), Fach.Num(v("sk"))); return $"Farbe {Fach.F(c.Paint)} € · Boden {Fach.F(c.Floor)} € · Leisten {Fach.F(c.Skirting)} € → gesamt {Fach.F(c.Total)} €"; }),
            FachTool("arch-csv", "Raumliste speichern", "Alle Räume mit Kennzahlen als CSV.", [], v => { var p = Path.Combine(Fach.StoreDir(_core.DataRoot, "architektur"), "raumliste.csv"); File.WriteAllText(p, Interior.RoomsCsv(Rooms.Project.Rooms)); return "Gespeichert: " + p; }, "Speichern")));
        sp.Children.Add(FachGroup("arch-plan", "Planen & prüfen", false,
            FachTool("arch-furn", "Passt das Möbel?", "Für den ersten Raum. Gehfreiraum davor: 0,9 m.",
                [("w", "Breite (m)", "2,2"), ("d", "Tiefe (m)", "0,9")],
                v => { var (ok, why) = Interior.FurnitureFits(FirstRoom(), Fach.Num(v("w")), Fach.Num(v("d"))); return (ok ? "✓ " : "⚠ ") + why; }, "Prüfen"),
            FachTool("arch-heat", "Heizleistung", "Grobe Schätzung. Standard: altbau, teilsaniert, neubau, kfw, passivhaus.",
                [("std", "Standard", "teilsaniert")],
                v => string.Join("\n", Rooms.Project.Rooms.Select(r => $"{r.Name}: ca. {Fach.F(Interior.HeatLoad(r, v("std")), 0)} W Heizleistung"))),
            FachTool("arch-light", "Licht", "Raumart: wohnzimmer, schlafzimmer, küche, bad, büro, flur, kinderzimmer, werkstatt.",
                [("kind", "Raumart", "wohnzimmer")],
                v => { var r = FirstRoom(); var l = Interior.Light(r, v("kind")); return $"{r.Name}: {Fach.F(l.Lux, 0)} lx → {Fach.F(l.Lumen, 0)} lm, z. B. {l.Bulbs806} LED-Lampen à 806 lm (60-W-Ersatz)"; }),
            FachTool("arch-stair", "Treppe", "Höhe von Fußboden zu Fußboden in cm.",
                [("h", "Geschosshöhe (cm)", "275"), ("run", "max. Lauflänge (cm, optional)", "")],
                v => { var s = Interior.Stairs(Fach.Num(v("h")), v("run").Trim() == "" ? null : Fach.Num(v("run"))); return $"{s.Steps} Steigungen à {Fach.F(s.RiseCm, 1)} cm, Auftritt {Fach.F(s.TreadCm, 1)} cm, Lauflänge {Fach.F(s.RunCm, 0)} cm, Neigung {Fach.F(s.AngleDeg, 1)}°\n{s.Verdict}"; }),
            FachTool("arch-wofl", "Wohnfläche (WoFlV)", "Flächen nach Raumhöhe getrennt, Balkon/Terrasse meist zu 25 %.",
                [("full", "ab 2 m Höhe (m²)", "60"), ("half", "1–2 m Höhe (m²)", "10"), ("zero", "unter 1 m (m²)", "4"), ("bal", "Balkon (m²)", "8")],
                v => $"Wohnfläche: {Fach.F(Interior.LivingArea(Fach.Num(v("full")), Fach.Num(v("half")), Fach.Num(v("zero")), Fach.Num(v("bal"))))} m²")));
        return FachPage("Räume & Wohnen", sp);
    }
}
