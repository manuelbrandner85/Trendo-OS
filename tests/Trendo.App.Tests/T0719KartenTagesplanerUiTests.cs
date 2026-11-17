using System.Text.Json.Nodes;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Trendo.Core;
using Trendo.Core.Karten;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0719 (Audit D4, Rest): Karten & Touren und Tagesplaner mit direkter Bearbeitung auf der Fläche – echte Maus-Ereignisse im Headless-Fenster
/// (Ziehen, Doppelklick, Rechtsklick, Tasten), jeder Schritt ein Rückgängig-Schritt der Hülle, Klick bis zur echten Datei.
/// Gegenprobe der Kalenderdatei mit Ical.Net (fremde Bibliothek, MIT). Belege: TRENDO_BELEG=&lt;Ordner&gt; legt die exportierten Dateien dort ab.</summary>
[Collection("Claude")]
public class T0719_KartenTagesplaner_Ui
{
    const string MK = "karten", DK = "tagesplaner-programm";

    static T V<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static T? TryV<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().LastOrDefault(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = V<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Button>(w, name)); Settle(); }
    static void Max(MainWindow w, string key) { if (w.Windows.Active is { Maximized: false } win) { w.Windows.ToggleMaximize(win.Id); w.Show(key); } Settle(); w.UpdateLayout(); Settle(); }
    static void Click(MainWindow w, Point p, int count = 1) { w.MouseMove(p); for (var i = 0; i < count; i++) { w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); } Settle(); }
    static void Drag(MainWindow w, Point a, Point b)
    {
        w.MouseMove(a); w.MouseDown(a, MouseButton.Left); Settle();
        for (var i = 1; i <= 6; i++) { w.MouseMove(a + (b - a) * (i / 6.0)); Settle(); }
        w.MouseUp(b, MouseButton.Left); Settle();
    }
    static void Key_(MainWindow w, Key k, PhysicalKey pk) { w.KeyPress(k, RawInputModifiers.None, pk, null); w.KeyRelease(k, RawInputModifiers.None, pk, null); Settle(); }
    /// <summary>Echter Rechtsklick an einer Stelle des Steuerelements – das Menü muss offen sein; dann den Eintrag wählen.</summary>
    static void RightMenu(MainWindow w, Control c, Point local, string item)
    {
        var p = c.TranslatePoint(local, w) ?? throw new InvalidOperationException("nicht sichtbar");
        w.MouseMove(p); w.MouseDown(p, MouseButton.Right); w.MouseUp(p, MouseButton.Right); Settle();
        var menu = c.ContextMenu ?? throw new InvalidOperationException("kein Rechtsklick-Menü");
        Assert.True(menu.IsOpen, "Rechtsklick hat das Menü nicht geöffnet");
        var mi = menu.Items.OfType<MenuItem>().Single(m => m.Name == item);
        Assert.True(mi.IsEnabled, item + " ist ausgegraut");
        mi.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Settle();
        menu.Close(); Settle();
    }
    static string Exported(MainWindow w, string key) { var m = w.ProgramMessage(key); Assert.StartsWith("Exportiert: ", m); return m["Exportiert: ".Length..].Trim(); }
    static void Beleg(string file)
    {
        if (Environment.GetEnvironmentVariable("TRENDO_BELEG") is not { Length: > 0 } dir) return;
        Directory.CreateDirectory(dir); File.Copy(file, Path.Combine(dir, Path.GetFileName(file)), true);
    }

    // ───────────────────────── Karten & Touren ─────────────────────────

    static MapDoc Map(MainWindow w) => MapDoc.From(w.ProgramActive(MK)!.Data);
    static MapCanvas Canvas(MainWindow w) => V<MapCanvas>(w, "mk-canvas");
    static Point Win(MainWindow w, Point local) { w.UpdateLayout(); Settle(); return Canvas(w).TranslatePoint(local, w)!.Value; }

    [AvaloniaFact(DisplayName = "T-0719 Karten: leere Karte → erster Schritt → Route mit der Maus, Punkt ziehen, Punkt aus der Mitte einfügen, Rechtsklick löschen, Wegpunkt setzen/ziehen/umbenennen, Rückgängig je Schritt, GPX-Datei")]
    public void Karte_direkt_bearbeiten()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            w.Show(MK); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer")); Max(w, MK);
            Assert.NotNull(TryV<Border>(w, "mk-first")); // erster Schritt im Leerzustand
            Assert.False(MainWindow.IsGold(V<Button>(w, "mk-first-route"))); // neutral – golden bleibt „Für Navi oder Uhr“
            Press(w, "mk-first-route");
            Assert.Equal("route", w.MapState.Tool); Assert.Null(TryV<Border>(w, "mk-first"));
            var c = Canvas(w); double W = c.Bounds.Width, H = c.Bounds.Height; Assert.True(W > 700 && H > 450, $"Karte zu klein: {W}×{H}");
            Point L(double fx, double fy) => new(W * fx, H * fy);
            Click(w, Win(w, L(.25, .3))); Click(w, Win(w, L(.5, .3))); Click(w, Win(w, L(.5, .6)));
            Click(w, Win(w, L(.5, .6)), 2); // Doppelklick beendet
            var doc = w.ProgramActive(MK)!;
            var route = Assert.Single(Map(w).Tracks); Assert.True(route.Planned); Assert.Equal(3, route.Points.Count);
            Press(w, "mk-tool-auswahl"); Assert.Equal(route.Id, w.MapState.Selected);

            // Punkt ziehen – ein Schritt
            var h = doc.History.Count;
            var exp = Canvas(w).Geo(L(.55, .4));
            Drag(w, Win(w, L(.5, .3)), Win(w, L(.55, .4)));
            var p1 = Map(w).Tracks[0].Points[1];
            Assert.Equal(exp.Lat, p1.Lat, 6); Assert.Equal(exp.Lon, p1.Lon, 6);
            Assert.Equal(h + 1, doc.History.Count); Assert.Equal("Punkt verschoben", doc.UndoLabel);

            // aus der Mitte ziehen fügt einen Punkt ein
            var mid = Canvas(w).MidOf(route.Id, 0); var ins = Canvas(w).Geo(L(.35, .15));
            Drag(w, Win(w, mid), Win(w, L(.35, .15)));
            var pts = Map(w).Tracks[0].Points; Assert.Equal(4, pts.Count); Assert.Equal(ins.Lat, pts[1].Lat, 6); Assert.Equal(ins.Lon, pts[1].Lon, 6);
            Assert.Equal("Punkt eingefügt", doc.UndoLabel);

            // Rechtsklick auf einen Punkt → löschen
            RightMenu(w, Canvas(w), L(.55, .4), "mk-menu-punkt-loeschen");
            Assert.Equal(3, Map(w).Tracks[0].Points.Count); Assert.Equal("Punkt gelöscht", doc.UndoLabel);

            // Rechtsklick ins Leere → Wegpunkt hier; dann ziehen (die Nadel am Kopf greifen – kein Sprung)
            RightMenu(w, Canvas(w), L(.8, .75), "mk-menu-wegpunkt-hier");
            var wp = Assert.Single(Map(w).Waypoints); Assert.Equal(wp.Id, w.MapState.Selected);
            var start = Canvas(w).Geo(L(.8, .75)); Assert.Equal(start.Lat, wp.Lat, 6);
            var moved = Canvas(w).Geo(L(.8, .75) + new Vector(60, 40));
            Drag(w, Win(w, L(.8, .75) + new Vector(0, -14)), Win(w, L(.8, .75) + new Vector(60, 26)));
            wp = Map(w).Waypoints[0];
            Assert.Equal(moved.Lat, wp.Lat, 6); Assert.Equal(moved.Lon, wp.Lon, 6); Assert.Equal("Wegpunkt verschoben", doc.UndoLabel);

            // Rückgängig über die Hülle: genau dieser eine Schritt
            Press(w, "prog-undo");
            wp = Map(w).Waypoints[0]; Assert.Equal(start.Lat, wp.Lat, 6); Assert.Equal(start.Lon, wp.Lon, 6);

            // Rechtsklick auf den Wegpunkt → Umbenennen (Eigenschaften gehen auch in „Einfach“ auf, Cursor im Namensfeld)
            RightMenu(w, Canvas(w), L(.8, .75) + new Vector(0, -12), "mk-menu-umbenennen");
            var name = V<TextBox>(w, "mk-p-name"); Settle(); Assert.True(name.IsFocused, "Namensfeld hat keinen Fokus");
            name.Text = "Hütte"; Key_(w, Key.Enter, PhysicalKey.Enter);
            Assert.Equal("Hütte", Map(w).Waypoints[0].Name);

            // Klick bis zur echten Datei: „Für Navi oder Uhr“ (GPX 1.1, Reihenfolge wpt → rte → trk)
            Press(w, "prog-export-gpx");
            var gpx = Exported(w, MK); Beleg(gpx);
            var x = XDocument.Load(gpx); XNamespace ns = "http://www.topografix.com/GPX/1/1";
            Assert.Equal(ns + "gpx", x.Root!.Name); Assert.Equal("1.1", x.Root.Attribute("version")!.Value);
            Assert.Equal(["metadata", "wpt", "rte"], x.Root.Elements().Select(e => e.Name.LocalName).ToArray());
            Assert.Equal("Hütte", x.Root.Element(ns + "wpt")!.Element(ns + "name")!.Value);
            Assert.Equal(3, x.Root.Element(ns + "rte")!.Elements(ns + "rtept").Count());
            var kml = MainWindow.Programs[MK].Exports.Single(e => e.Name == "kml").Write(doc, Path.Combine(root, "t0719-karte.kml")); Beleg(kml);
            Assert.Contains("<name>Hütte</name>", File.ReadAllText(kml)); XDocument.Load(kml);
        }
        finally { w.Close(); }
    }

    /// <summary>GPX im Aufbau, wie Garmin-Geräte und -Programme sie schreiben (zwei Segmente, Erweiterungen mit eigenem Namensraum, eine Route, zwei Spuren).
    /// Von Hand nach der GPX-1.1-Struktur geschrieben – keine aus dem Internet geladene Datei (ohne Freigabe wird nichts geladen).</summary>
    internal const string GarminLike = """
        <?xml version="1.0" encoding="UTF-8"?>
        <gpx creator="Garmin Connect" version="1.1" xmlns="http://www.topografix.com/GPX/1/1" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
             xmlns:gpxtpx="http://www.garmin.com/xmlschemas/TrackPointExtension/v1" xsi:schemaLocation="http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/1/1/gpx.xsd">
          <metadata><time>2026-09-12T07:58:02Z</time></metadata>
          <wpt lat="47.4211" lon="10.9853"><ele>2962</ele><name>Gipfel</name><sym>Summit</sym></wpt>
          <rte><name>Zustieg</name><rtept lat="47.4400" lon="10.9900"/><rtept lat="47.4350" lon="10.9880"/><rtept lat="47.4300" lon="10.9870"/></rte>
          <trk><name>Morgenrunde</name><type>hiking</type>
            <trkseg>
              <trkpt lat="47.4300" lon="10.9870"><ele>1500.4</ele><time>2026-09-12T08:00:00Z</time><extensions><gpxtpx:TrackPointExtension><gpxtpx:hr>101</gpxtpx:hr></gpxtpx:TrackPointExtension></extensions></trkpt>
              <trkpt lat="47.4280" lon="10.9865"><ele>1560.0</ele><time>2026-09-12T08:10:00Z</time><extensions><gpxtpx:TrackPointExtension><gpxtpx:hr>118</gpxtpx:hr></gpxtpx:TrackPointExtension></extensions></trkpt>
            </trkseg>
            <trkseg>
              <trkpt lat="47.4260" lon="10.9860"><ele>1640.2</ele><time>2026-09-12T08:30:00Z</time></trkpt>
              <trkpt lat="47.4240" lon="10.9858"><ele>1700.0</ele><time>2026-09-12T08:45:00Z</time></trkpt>
            </trkseg>
          </trk>
          <trk><name>Abstieg</name>
            <trkseg><trkpt lat="47.4211" lon="10.9853"><ele>2962</ele></trkpt><trkpt lat="47.4300" lon="10.9870"><ele>1500</ele></trkpt></trkseg>
          </trk>
        </gpx>
        """;

    [AvaloniaFact(DisplayName = "T-0719 Karten: GPX im Garmin-Aufbau auf die Karte ziehen – jede Spur/Route einzeln, ein Rückgängig-Schritt; alte Karten-Datei öffnen und bearbeiten; Assistenzweg schreibt einen Wegpunkt ins Dokument")]
    public async Task Karte_import_alt_und_assistenz()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        MainWindow.AssistStepDelayMs = 0;
        try
        {
            // alte Datei: im Format, das „Karten & Touren“ seit T-0260 schreibt (Beispieltour gespeichert, geschlossen, wieder geöffnet)
            w.Show(MK); Settle(); RealClick(w, V<Button>(w, "prog-tpl-tour")); Max(w, MK);
            var old = w.ProgramSave(MK, Path.Combine(root, "alte-tour.tdoc"));
            Assert.Contains("\"map\"", File.ReadAllText(old));
            w.ProgramClose(MK, 0); w.ProgramOpen(MK, old); w.Show(MK); Max(w, MK);
            Assert.Equal(2, Map(w).Waypoints.Count);
            var gipfel = Map(w).Waypoints.Single(x => x.Name == "Gipfelkreuz");
            var tip = Canvas(w).Screen(gipfel.Lat, gipfel.Lon); var exp = Canvas(w).Geo(tip + new Vector(-40, 30)); // Erwartung vor dem Ziehen (die Karte wird danach neu aufgebaut)
            Drag(w, Win(w, tip + new Vector(0, -12)), Win(w, tip + new Vector(-40, 18)));
            var nowAt = Map(w).Waypoints.Single(x => x.Name == "Gipfelkreuz");
            Assert.Equal(exp.Lat, nowAt.Lat, 6); Assert.Equal(exp.Lon, nowAt.Lon, 6);

            // fremd aufgebaute GPX ablegen
            var f = Path.Combine(root, "garmin-aufbau.gpx"); File.WriteAllText(f, GarminLike);
            var doc = w.ProgramActive(MK)!; var h = doc.History.Count;
            BueroAusbau_Ui.Drop(w, "mk-canvas", f);
            var m = Map(w);
            Assert.Equal(4, m.Tracks.Count); // Rundweg + Zustieg (Route) + Morgenrunde (zwei Segmente) + Abstieg
            var zu = m.Tracks.Single(t => t.Name == "Zustieg"); Assert.True(zu.Planned); Assert.Equal(3, zu.Points.Count);
            var mo = m.Tracks.Single(t => t.Name == "Morgenrunde"); Assert.False(mo.Planned); Assert.Equal(4, mo.Points.Count); Assert.Equal(1500.4, mo.Points[0].Ele);
            Assert.Contains(m.Waypoints, x => x.Name == "Gipfel" && x.Ele == 2962);
            Assert.Equal(h + 1, doc.History.Count); Assert.Equal("GPX laden", doc.UndoLabel);
            Assert.Contains("3 Spuren/Routen geladen", w.ProgramMessage(MK));
            Press(w, "prog-undo"); Assert.Single(Map(w).Tracks);

            // Assistenzweg (T-0707) schreibt weiter ins Dokument – KI ersetzt
            w.ProgramNew(MK, "leer"); w.ProgramSave(MK, Path.Combine(root, "karte-ki.tdoc")); w.Show(MK); Settle();
            w.KiBackendsOverride = () => [new FixedKi(_ => Edit("map/waypoints/0", new JsonObject { ["id"] = "k1", ["name"] = "Treffpunkt am See", ["lat"] = 47.85, ["lon"] = 12.35 }))];
            await Order(w, MK, "Setz bitte einen Wegpunkt mit dem Namen Treffpunkt am See auf meine Karte");
            Assert.Equal("Treffpunkt am See", Assert.Single(Map(w).Waypoints).Name);
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.Close(); }
    }

    // ───────────────────────── Tagesplaner ─────────────────────────

    static DayTimeline Tl(MainWindow w) => V<DayTimeline>(w, "dp-timeline");
    static Point TlWin(MainWindow w, double x, double y) { w.UpdateLayout(); Settle(); return Tl(w).TranslatePoint(new Point(x, y), w)!.Value; }
    static JsonObject Plan(ProgramDocument d, int i) => (JsonObject)d.List("plan")[i]!;
    static JsonObject TaskAt(ProgramDocument d, int i) => (JsonObject)d.List("tasks")[i]!;
    static JsonObject Appt(ProgramDocument d, int i) => (JsonObject)d.List("appts")[i]!;

    [AvaloniaFact(DisplayName = "T-0719 Tagesplaner: leerer Tag → erster Block, im Block benennen, ziehen (verschieben), Kante ziehen (Dauer), Doppelklick neu, Rechtsklick Termin/länger, Pfeiltaste, Entf, Rückgängig je Schritt, Kalenderdatei mit Ical.Net geprüft")]
    public void Tagesplaner_direkt_bearbeiten()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        MainWindow.DayNowOverride = () => new DateTime(2026, 11, 16, 7, 0, 0);
        try
        {
            w.Show(DK); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer")); Max(w, DK);
            var d = w.ProgramActive(DK)!;
            Assert.True(MainWindow.IsGold(V<Button>(w, "prog-first-step")));
            Press(w, "prog-first-step");
            var box = V<TextBox>(w, "dp-blk-edit"); Settle(); Assert.True(box.IsFocused, "Name im Block hat keinen Fokus");
            box.Text = "Angebot schreiben"; Key_(w, Key.Enter, PhysicalKey.Enter);
            Assert.Equal("Angebot schreiben", (string?)TaskAt(d, 0)["title"]); Assert.Equal("30", (string?)TaskAt(d, 0)["min"]);
            Assert.Equal("2026-11-16T08:00:00", (string?)Plan(d, 0)["s"]);
            Assert.Null(TryV<Border>(w, "prog-first"));

            // Block ziehen: +60 min
            var tl = Tl(w); var r = tl.BlockRect("p:0"); var h = d.History.Count;
            Drag(w, TlWin(w, r.Center.X, r.Y + 8), TlWin(w, r.Center.X, r.Y + 8 + 60 * tl.PxPerMin));
            Assert.Equal("2026-11-16T09:00:00", (string?)Plan(d, 0)["s"]); Assert.Equal("2026-11-16T09:30:00", (string?)Plan(d, 0)["e"]);
            Assert.Equal(h + 1, d.History.Count); Assert.Equal("Verschoben auf 09:00", d.UndoLabel);

            // Unterkante ziehen: Dauer 60 min, auch in der Aufgabenliste
            tl = Tl(w); r = tl.BlockRect("p:0");
            Drag(w, TlWin(w, r.Center.X, r.Bottom - 3), TlWin(w, r.Center.X, r.Bottom - 3 + 30 * tl.PxPerMin));
            Assert.Equal("2026-11-16T10:00:00", (string?)Plan(d, 0)["e"]); Assert.Equal("60", (string?)TaskAt(d, 0)["min"]); Assert.Equal("Dauer 60 min", d.UndoLabel);

            // Doppelklick ins Leere (11:00) → neuer Block, Esc behält „Neue Aufgabe“
            tl = Tl(w); Click(w, TlWin(w, r.Center.X, tl.Y(11 * 60) + 6), 2);
            Assert.Equal("2026-11-16T11:00:00", (string?)Plan(d, 1)["s"]);
            V<TextBox>(w, "dp-blk-edit").Focus(); Key_(w, Key.Escape, PhysicalKey.Escape);
            Assert.Null(w.DayEditing); Assert.Equal("Neue Aufgabe", (string?)TaskAt(d, 1)["title"]);

            // Rechtsklick ins Leere (13:00) → Termin; Rechtsklick auf den Termin → 15 Minuten länger
            tl = Tl(w); RightMenu(w, tl, new Point(r.Center.X, tl.Y(13 * 60) + 2), "dp-menu-neu-termin");
            Assert.Equal("13:00", (string?)Appt(d, 0)["start"]); Assert.Equal("14:00", (string?)Appt(d, 0)["end"]);
            var akey = "a:" + (string?)Appt(d, 0)["id"];
            Assert.Equal(akey, w.DayEditing); // neuer Termin: Name gleich im Block eintippen
            V<TextBox>(w, "dp-blk-edit").Text = "Zahnarzt"; Key_(w, Key.Enter, PhysicalKey.Enter);
            Assert.Equal("Zahnarzt", (string?)Appt(d, 0)["title"]); Assert.Null(w.DayEditing);
            tl = Tl(w); RightMenu(w, tl, tl.BlockRect(akey).Center, "dp-menu-laenger");
            Assert.Equal("14:15", (string?)Appt(d, 0)["end"]);

            // Termin anklicken (wählen) und mit Pfeil runter 5 Minuten später; Rückgängig nimmt genau das zurück
            tl = Tl(w); Click(w, TlWin(w, tl.BlockRect(akey).Center.X, tl.BlockRect(akey).Y + 8)); Assert.Equal(akey, w.DaySel);
            Key_(w, Key.Down, PhysicalKey.ArrowDown);
            Assert.Equal("13:05", (string?)Appt(d, 0)["start"]); Assert.Equal("14:20", (string?)Appt(d, 0)["end"]);
            Press(w, "prog-undo");
            Assert.Equal("13:00", (string?)Appt(d, 0)["start"]); Assert.Equal("14:15", (string?)Appt(d, 0)["end"]);

            // „Neue Aufgabe“ wählen, Entf nimmt sie aus dem Plan (die Aufgabe bleibt in der Liste)
            tl = Tl(w); var nr = tl.BlockRect("p:1"); Click(w, TlWin(w, nr.Center.X - 40, nr.Y + 8)); Assert.Equal("p:1", w.DaySel);
            Key_(w, Key.Delete, PhysicalKey.Delete);
            Assert.Single(d.List("plan")); Assert.Equal(2, d.List("tasks").Count); Assert.Equal("Aus dem Plan genommen", d.UndoLabel);

            // Klick bis zur echten Datei; Gegenprobe mit Ical.Net
            Press(w, "prog-export-ics");
            var ics = Exported(w, DK); Beleg(ics);
            var cal = Ical.Net.Calendar.Load(File.ReadAllText(ics))!;
            var ev = Assert.Single(cal.Events);
            Assert.Equal("Angebot schreiben", ev.Summary);
            Assert.Equal(new DateTime(2026, 11, 16, 9, 0, 0), ev.DtStart!.Value); Assert.Equal(new DateTime(2026, 11, 16, 10, 0, 0), ev.DtEnd!.Value);
            Press(w, "prog-cmd-dp-ics-alle"); var all = Exported(w, DK); Beleg(all);
            var cal2 = Ical.Net.Calendar.Load(File.ReadAllText(all))!;
            Assert.Equal(["Angebot schreiben", "Zahnarzt"], cal2.Events.OrderBy(e => e.DtStart!.Value).Select(e => e.Summary).ToArray());
            Assert.Equal(new DateTime(2026, 11, 16, 14, 15, 0), cal2.Events.Single(e => e.Summary == "Zahnarzt").DtEnd!.Value);
        }
        finally { MainWindow.DayNowOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0719 Tagesplaner: alte Datei (Plan mit Termin-Kopien) öffnen – Zeitleiste zeigt alles, Termin ziehen hält Plan und Termin gleich; Assistenzweg schreibt einen Block, der in der Zeitleiste steht")]
    public async Task Tagesplaner_alt_und_assistenz()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; Directory.CreateDirectory(root);
        MainWindow.DayNowOverride = () => new DateTime(2026, 11, 16, 7, 0, 0); MainWindow.AssistStepDelayMs = 0;
        try
        {
            // alte Datei: Beispieltag mit „Tag planen“ (unverändert seit T-0615), gespeichert, geschlossen, wieder geöffnet
            w.Show(DK); Settle(); RealClick(w, V<Button>(w, "prog-tpl-beispiel")); Max(w, DK);
            Press(w, "prog-cmd-dp-plan");
            var old = w.ProgramSave(DK, Path.Combine(root, "alter-tag.tdoc"));
            w.ProgramClose(DK, 0); w.ProgramOpen(DK, old); w.Show(DK); Max(w, DK);
            var d = w.ProgramActive(DK)!;
            Assert.Contains(d.List("plan").OfType<JsonObject>(), p => (string?)p["kind"] == "termin"); // Termin-Kopien im Plan wie bisher
            var tl = Tl(w);
            Assert.Equal(3, tl.Blocks.Count(b => b.Kind == "termin")); Assert.Equal(3, tl.Blocks.Count(b => b.Kind == "aufgabe")); Assert.Single(tl.Blocks, b => b.Kind == "pause");
            Assert.NotNull(TryV<CheckBox>(w, "dp-block-done-0"));
            var team = tl.Blocks.Single(b => b.Title == "Teamrunde"); var tr = tl.BlockRect(team.Key);
            var hitAt = TlWin(w, tr.Center.X, tr.Y + 8); var hit = w.InputHitTest(hitAt) as Visual;
            Assert.True(hit is not null && (hit == tl || hit.GetVisualAncestors().Contains(tl)), $"an der Stelle liegt {hit?.GetType().Name} {(hit as Control)?.Name} – Block {tr}, Fenster {w.Bounds}");
            Drag(w, TlWin(w, tr.Center.X, tr.Y + 8), TlWin(w, tr.Center.X, tr.Y + 8 + 30 * tl.PxPerMin));
            var appt = d.List("appts").OfType<JsonObject>().Single(a => (string?)a["title"] == "Teamrunde");
            Assert.True((string?)appt["start"] == "10:30", "Meldung: " + w.ProgramMessage(DK) + " | Verlauf: " + string.Join(", ", d.History) + " | " + string.Join("; ", Tl(w).Blocks.Select(b => $"{b.Key} {b.Title} {b.Start}-{b.End}"))); Assert.Equal("11:00", (string?)appt["end"]);
            var copy = d.List("plan").OfType<JsonObject>().Single(p => (string?)p["kind"] == "termin" && (string?)p["item"] == (string?)appt["id"]);
            Assert.Equal("2026-11-16T10:30:00", (string?)copy["s"]);
            var txt = MainWindow.Programs[DK].Exports.Single(e => e.Name == "txt").Write(d, Path.Combine(root, "tag.txt"));
            Assert.Contains("10:30–11:00  Termin: Teamrunde", File.ReadAllText(txt));

            // Assistenzweg (T-0707) – KI ersetzt; der neue Block steht danach in der Zeitleiste
            w.ProgramNew(DK, "leer"); w.ProgramSave(DK, Path.Combine(root, "tag-ki.tdoc")); w.Show(DK); Settle();
            w.KiBackendsOverride = () => [new FixedKi(_ => Edit("plan/0", new JsonObject { ["s"] = "2026-11-16T14:00:00", ["e"] = "2026-11-16T15:00:00", ["title"] = "Steuer vorbereiten", ["kind"] = "aufgabe", ["item"] = "" }))];
            await Order(w, DK, "Leg mir heute von 14 bis 15 Uhr einen Block zum Steuer vorbereiten an");
            Settle();
            Assert.Contains(Tl(w).Blocks, b => b.Title == "Steuer vorbereiten" && b.Start == 14 * 60 && b.End == 15 * 60);
        }
        finally { MainWindow.DayNowOverride = null; MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.Close(); }
    }

    // ───────────────────────── Assistenzweg (wie AssistChain_Ui, KI ersetzt) ─────────────────────────

    sealed class FixedKi(Func<string, string> reply) : IKiBackend
    {
        public string Name => "Ersatz-KI"; public string BaseUrl => "http://127.0.0.1:9";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["ersatz"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) => Task.FromResult(reply(messages[^1].Content));
    }

    static string Edit(string path, JsonNode value) => new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["action"] = "dokument.setzen", ["args"] = new JsonObject { ["pfad"] = path, ["wert"] = value } }) }.ToJsonString();
    static async Task Until(Func<bool> ok, int ms = 10000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }

    /// <summary>Auftrag → Vorschau (Original unverändert) → „Ja“ → ein Rückgängig-Schritt im offenen Dokument.</summary>
    static async Task Order(MainWindow w, string key, string prompt)
    {
        var doc = w.ProgramActive(key)!; var before = doc.Data.ToJsonString(); var history = doc.History.Count;
        w.Find<TextBox>("auftrag-prompt").Text = prompt;
        w.Find<Button>("auftrag-pruefen").Press();
        await Until(() => w.AssistLastKey == key && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen");
        Assert.True(w.AssistLast!.Value.Phase == "vorschau", $"{key}: {w.AssistLast.Value.Phase} – {w.AssistLast.Value.Message}");
        Assert.Equal(before, doc.Data.ToJsonString());
        w.Find<Button>("assist-apply").Press();
        await Until(() => w.AssistLast?.Phase == "fertig");
        Assert.Equal(history + 1, doc.History.Count);
        Assert.StartsWith(AssistChain.UndoPrefix, doc.UndoLabel);
        w.Show(key); Settle();
    }
}
