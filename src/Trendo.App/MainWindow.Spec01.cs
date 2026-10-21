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
        return FachPage("Räume & Wohnen", sp);
    }
}
