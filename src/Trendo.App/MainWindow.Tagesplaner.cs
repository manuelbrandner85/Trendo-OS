using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>T-0719 (Audit D4, Tagesplaner): Zeitleiste zum Anfassen statt Liste – Termine und Plan-Blöcke ziehen, Dauer an der Kante ziehen,
/// Doppelklick legt an, Rechtsklick-Menü, Umbenennen direkt im Block. Jede Aktion ist genau ein Rückgängig-Schritt der Programm-Hülle.
/// Das Dateiformat bleibt: Termine in „appts“ (HH:mm), Plan-Blöcke in „plan“ (s/e als Datum mit Uhrzeit), Aufgaben in „tasks“.</summary>
public partial class MainWindow
{
    bool _dayRefocus; // T-0730: Zeitleiste nach dem Umbenennen wieder fokussieren
    /// <summary>Gewählter Block der Zeitleiste („a:&lt;id&gt;“ Termin, „p:&lt;index&gt;“ Plan-Block).</summary>
    public string? DaySel { get; set; }
    /// <summary>Block, dessen Name gerade direkt in der Zeitleiste bearbeitet wird.</summary>
    public string? DayEditing { get; set; }

    static string DpIso(DateTime day, int min) => day.AddMinutes(min).ToString("s", CultureInfo.InvariantCulture);
    static int DpMin(DateTime t, DateTime day) => Math.Clamp((int)Math.Round((t - day).TotalMinutes), 0, 24 * 60);

    /// <summary>Blöcke der Zeitleiste: alle Termine plus Aufgaben- und Pausenblöcke des Plans (Termin-Kopien im Plan stehen schon als Termin da).</summary>
    public static List<TimelineBlock> DayBlocks(ProgramDocument d)
    {
        var day = DpDay(d); var items = DpItems(d).ToDictionary(i => i.Id); var res = new List<TimelineBlock>();
        foreach (var o in d.List("appts").OfType<JsonObject>())
            if (DayPlanner.ParseTime(Js(o["start"])) is { } s && DayPlanner.ParseTime(Js(o["end"])) is { } e && e > s && Js(o["id"]).Length > 0)
                res.Add(new TimelineBlock { Key = "a:" + Js(o["id"]), Title = Js(o["title"]), Kind = DayPlanner.Fixed, Start = (int)s.TotalMinutes, End = Math.Min(24 * 60, (int)e.TotalMinutes) });
        var plan = d.List("plan");
        for (var i = 0; i < plan.Count; i++)
        {
            if (plan[i] is not JsonObject o || Js(o["kind"]) == DayPlanner.Fixed) continue;
            if (!DateTime.TryParse(Js(o["s"]), CultureInfo.InvariantCulture, DateTimeStyles.None, out var s) || !DateTime.TryParse(Js(o["e"]), CultureInfo.InvariantCulture, DateTimeStyles.None, out var e) || e <= s) continue;
            var kind = Js(o["kind"]) == DayPlanner.Pause ? DayPlanner.Pause : DayPlanner.Work;
            var item = Js(o["item"]) is { Length: > 0 } it && items.TryGetValue(it, out var x) ? x : null;
            res.Add(new TimelineBlock { Key = "p:" + i, Title = item?.Title ?? Js(o["title"]), Kind = kind, Start = DpMin(s, day), End = DpMin(e, day), Done = item?.Done == true, HasTask = item is not null, Pinned = Js(o["pin"]) == "ja" });
        }
        return res;
    }

    /// <summary>Plan, wie er auf der Zeitleiste steht (Termine aus „appts“, Aufgaben/Pausen aus „plan“) – für Kalenderdatei und Text.</summary>
    static DayPlanner.Plan DpView(ProgramDocument d)
    {
        var day = DpDay(d); var stored = DpStored(d);
        var blocks = DayBlocks(d).Select(b => new DayPlanner.Block(day.AddMinutes(b.Start), day.AddMinutes(b.End), b.Title, b.Kind,
            b.Kind == DayPlanner.Fixed ? b.Key[2..] : b.Key.StartsWith("p:", StringComparison.Ordinal) && d.List("plan")[int.Parse(b.Key[2..], CultureInfo.InvariantCulture)] is JsonObject po && Js(po["item"]) is { Length: > 0 } it ? it : null))
            .OrderBy(b => b.Start).ThenBy(b => b.Kind == DayPlanner.Fixed ? 0 : 1).ToList();
        return new(day, blocks, stored.Tomorrow);
    }

    static (string Kind, string Id, int Index) DpKey(string key) =>
        key.StartsWith("a:", StringComparison.Ordinal) ? ("a", key[2..], -1)
        : key.StartsWith("p:", StringComparison.Ordinal) && int.TryParse(key[2..], CultureInfo.InvariantCulture, out var i) ? ("p", "", i)
        : throw new ArgumentException("Diesen Block gibt es nicht.");

    /// <summary>Block verschieben oder seine Dauer ändern (Minuten ab Mitternacht). Eine Aufgabe übernimmt die neue Dauer auch in die Aufgabenliste.</summary>
    public void DayBlockChange(ProgramDocument d, string key, int start, int end)
    {
        if (end <= start || start < 0 || end > 24 * 60) throw new ArgumentException("Ein Block muss am selben Tag beginnen und enden.");
        var day = DpDay(d); var (kind, id, idx) = DpKey(key);
        var old = DayBlocks(d).FirstOrDefault(b => b.Key == key) ?? throw new ArgumentException("Diesen Block gibt es nicht mehr.");
        var label = old.End - old.Start == end - start ? $"Verschoben auf {TimelineHm(start)}" : $"Dauer {end - start} min";
        d.Edit(label, x =>
        {
            if (kind == "a")
            {
                foreach (var a in (x["appts"] as JsonArray ?? []).OfType<JsonObject>().Where(a => Js(a["id"]) == id)) { a["start"] = TimelineHm(start); a["end"] = TimelineHm(end); }
                foreach (var p in (x["plan"] as JsonArray ?? []).OfType<JsonObject>().Where(p => Js(p["kind"]) == DayPlanner.Fixed && Js(p["item"]) == id)) { p["s"] = DpIso(day, start); p["e"] = DpIso(day, end); }
                return;
            }
            if ((x["plan"] as JsonArray)?[idx] is not JsonObject po) return;
            po["s"] = DpIso(day, start); po["e"] = DpIso(day, end);
            po["pin"] = "ja"; // T-0728: von Hand verschoben – „Tag planen“ lässt ihn stehen
            if (end - start != old.End - old.Start && Js(po["item"]) is { Length: > 0 } item)
                foreach (var t in (x["tasks"] as JsonArray ?? []).OfType<JsonObject>().Where(t => Js(t["id"]) == item)) t["min"] = (end - start).ToString(CultureInfo.InvariantCulture);
        });
        DaySel = key;
        _progMsg[DayKey] = $"{(old.Kind == DayPlanner.Fixed ? "Termin" : old.Kind == DayPlanner.Pause ? "Pause" : "Aufgabe")} jetzt {TimelineHm(start)}–{TimelineHm(end)}.";
    }

    static string TimelineHm(int m) => DayTimeline.Hm(m);

    /// <summary>Neuen Block anlegen: „aufgabe“ (30 min, steht auch in der Aufgabenliste), „termin“ (1 h) oder „pause“ (15 min). Gibt den Schlüssel zurück.</summary>
    public string DayBlockNew(ProgramDocument d, int start, string kind = DayPlanner.Work)
    {
        var day = DpDay(d); var len = kind switch { DayPlanner.Fixed => 60, DayPlanner.Pause => 15, _ => 30 };
        start = Math.Clamp(start, 0, 24 * 60 - len); var end = start + len; string key = "";
        if (kind == DayPlanner.Fixed)
        {
            var id = NewId("a");
            d.Edit("Neuer Termin", x => { if (x["appts"] is not JsonArray a) x["appts"] = a = []; a.Add(new JsonObject { ["id"] = id, ["title"] = "Neuer Termin", ["start"] = TimelineHm(start), ["end"] = TimelineHm(end), ["uid"] = "" }); });
            key = "a:" + id;
        }
        else
        {
            var tid = kind == DayPlanner.Pause ? "" : NewId("t");
            d.Edit(kind == DayPlanner.Pause ? "Neue Pause" : "Neue Aufgabe", x =>
            {
                if (tid.Length > 0) { if (x["tasks"] is not JsonArray ts) x["tasks"] = ts = []; ts.Add(DpTaskJson(new DayPlanner.Item(tid, "Neue Aufgabe", len))); }
                if (x["plan"] is not JsonArray p) x["plan"] = p = [];
                p.Add(new JsonObject { ["s"] = DpIso(day, start), ["e"] = DpIso(day, end), ["title"] = tid.Length > 0 ? "Neue Aufgabe" : "Pause", ["kind"] = kind == DayPlanner.Pause ? DayPlanner.Pause : DayPlanner.Work, ["item"] = tid, ["pin"] = "ja" });
                key = "p:" + (p.Count - 1);
            });
        }
        DaySel = key; DayEditing = kind == DayPlanner.Pause ? null : key;
        _progMsg[DayKey] = kind == DayPlanner.Pause ? $"Pause {TimelineHm(start)}–{TimelineHm(end)} eingetragen." : "Namen eintippen und mit Enter bestätigen.";
        return key;
    }

    /// <summary>Termin löschen; Aufgabe aus dem Plan nehmen (sie bleibt in der Liste); Pause löschen.</summary>
    public void DayBlockDelete(ProgramDocument d, string key)
    {
        var (kind, id, idx) = DpKey(key);
        var b = DayBlocks(d).FirstOrDefault(x => x.Key == key) ?? throw new ArgumentException("Diesen Block gibt es nicht mehr.");
        d.Edit(b.Kind == DayPlanner.Fixed ? "Termin gelöscht" : b.Kind == DayPlanner.Pause ? "Pause gelöscht" : "Aus dem Plan genommen", x =>
        {
            if (kind == "a")
            {
                x["appts"] = new JsonArray((x["appts"] as JsonArray ?? []).OfType<JsonObject>().Where(a => Js(a["id"]) != id).Select(a => (JsonNode)a.DeepClone()).ToArray());
                x["plan"] = new JsonArray((x["plan"] as JsonArray ?? []).OfType<JsonObject>().Where(p => !(Js(p["kind"]) == DayPlanner.Fixed && Js(p["item"]) == id)).Select(p => (JsonNode)p.DeepClone()).ToArray());
            }
            else if (x["plan"] is JsonArray p && idx < p.Count) p.RemoveAt(idx);
        });
        DaySel = null; DayEditing = null;
        _progMsg[DayKey] = b.Kind == DayPlanner.Work ? $"„{b.Title}“ ist aus dem Plan genommen und steht weiter links in den Aufgaben." : $"{(b.Kind == DayPlanner.Fixed ? "Termin" : "Pause")} gelöscht.";
    }

    /// <summary>Block umbenennen (Termin bzw. Aufgabe samt Eintrag in der Aufgabenliste).</summary>
    public void DayBlockRename(ProgramDocument d, string key, string title)
    {
        title = KreativSafe.Cut((title ?? "").Trim(), 200);
        DayEditing = null;
        if (title.Length == 0) return;
        var (kind, id, idx) = DpKey(key);
        var b = DayBlocks(d).FirstOrDefault(x => x.Key == key);
        if (b is null || b.Kind == DayPlanner.Pause || b.Title == title) return;
        d.Edit("Umbenannt", x =>
        {
            if (kind == "a")
            {
                foreach (var a in (x["appts"] as JsonArray ?? []).OfType<JsonObject>().Where(a => Js(a["id"]) == id)) a["title"] = title;
                foreach (var p in (x["plan"] as JsonArray ?? []).OfType<JsonObject>().Where(p => Js(p["kind"]) == DayPlanner.Fixed && Js(p["item"]) == id)) p["title"] = title;
                return;
            }
            if ((x["plan"] as JsonArray)?[idx] is not JsonObject po) return;
            po["title"] = title;
            if (Js(po["item"]) is { Length: > 0 } item) foreach (var t in (x["tasks"] as JsonArray ?? []).OfType<JsonObject>().Where(t => Js(t["id"]) == item)) t["title"] = title;
        });
    }

    string? DpItemOf(ProgramDocument d, string key) => DpKey(key) is ("p", _, var i) && i < d.List("plan").Count && d.List("plan")[i] is JsonObject o && Js(o["item"]) is { Length: > 0 } it ? it : null;

    /// <summary>Rechtsklick-Menü der Zeitleiste – auf einem Block bzw. an einer freien Stelle.</summary>
    ContextMenu DayMenu(ProgramDocument d, string? key, int minute)
    {
        var m = new ContextMenu { Name = "dp-menu" };
        void I(string id, string header, Action go, bool on = true, string gesture = "")
        {
            var i = new MenuItem { Name = "dp-menu-" + id, Header = header, IsEnabled = on, InputGesture = gesture.Length > 0 ? KeyGesture.Parse(gesture) : null };
            i.Click += (_, _) => ProgramRun(DayKey, go); m.Items.Add(i);
        }
        var b = key is null ? null : DayBlocks(d).FirstOrDefault(x => x.Key == key);
        if (b is not null)
        {
            var k = b.Key;
            if (b.Kind != DayPlanner.Pause) I("umbenennen", "Umbenennen …", () => { DaySel = k; DayEditing = k; }, gesture: "F2");
            if (b.HasTask && DpItemOf(d, k) is { } item) I("erledigt", b.Done ? "Wieder offen" : "Erledigt", () => DayPlanToggle(d, item, !b.Done));
            I("laenger", "15 Minuten länger", () => DayBlockChange(d, k, b.Start, Math.Min(24 * 60, b.End + 15)), b.End < 24 * 60);
            I("kuerzer", "15 Minuten kürzer", () => DayBlockChange(d, k, b.Start, b.End - 15), b.End - b.Start > 15);
            if (b.Pinned) I("loesen", "Beim Planen neu verteilen", () => DayBlockUnpin(d, k)); // T-0728
            m.Items.Add(new Separator());
            I("loeschen", b.Kind == DayPlanner.Work ? "Aus dem Plan nehmen" : "Löschen", () => DayBlockDelete(d, k), gesture: "Delete");
        }
        else
        {
            var at = minute / 15 * 15;
            I("neu-aufgabe", $"Aufgabe hier ({TimelineHm(at)}, 30 min)", () => DayBlockNew(d, at, DayPlanner.Work));
            I("neu-termin", $"Termin hier ({TimelineHm(at)}, 1 Stunde)", () => DayBlockNew(d, at, DayPlanner.Fixed));
            I("neu-pause", $"Pause hier ({TimelineHm(at)}, 15 min)", () => DayBlockNew(d, at, DayPlanner.Pause));
        }
        return m;
    }

    /// <summary>Die Zeitleiste des Tages mit allen Bedienwegen.</summary>
    Control DayTimelineView(ProgramDocument d)
    {
        var o = DpOptionsSafe(d); var day = DpDay(d);
        var now = day == DayNow.Date ? (int)DayNow.TimeOfDay.TotalMinutes : -1;
        var blocks = DayBlocks(d);
        if (DaySel is { } s && blocks.All(b => b.Key != s)) DaySel = null;
        if (DayEditing is { } ed && blocks.All(b => b.Key != ed)) DayEditing = null;
        var tl = new DayTimeline(blocks, (int)o.DayStart.TotalMinutes, (int)o.DayEnd.TotalMinutes, now, DaySel, DayEditing)
        {
            Changed = (k, a, b) => ProgramRun(DayKey, () => DayBlockChange(d, k, a, b)),
            CreateAt = m => ProgramRun(DayKey, () => DayBlockNew(d, m, DayPlanner.Work)),
            Select = k => DaySel = k,
            Rename = k => { DaySel = k; DayEditing = k; Show(DayKey); },
            Renamed = (k, t) => { _dayRefocus = true; ProgramRun(DayKey, () => DayBlockRename(d, k, t)); },
            CancelEdit = () => { _dayRefocus = true; DayEditing = null; Show(DayKey); },
            Delete = k => ProgramRun(DayKey, () => DayBlockDelete(d, k)),
            Toggle = (k, done) => { if (DpItemOf(d, k) is { } item) ProgramRun(DayKey, () => DayPlanToggle(d, item, done)); },
        };
        // T-0730: nach Enter/Esc beim Umbenennen bekommt die neu aufgebaute Zeitleiste den Fokus zurück – sonst wirken Pfeile, Entf und F2 erst nach einem neuen Klick
        if (_dayRefocus) { _dayRefocus = false; tl.AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => tl.Focus()); }
        tl.MenuAt = (k, m) => DayMenu(d, k, m);
        return tl;
    }

    /// <summary>T-0728: Block wieder freigeben – „Tag planen“ darf ihn neu verteilen.</summary>
    public void DayBlockUnpin(ProgramDocument d, string key)
    {
        if (DpKey(key) is not ("p", _, var idx)) return;
        d.Edit("Wieder frei planen", x => { if ((x["plan"] as JsonArray)?[idx] is JsonObject po) po.Remove("pin"); });
        _progMsg[DayKey] = "„Tag planen“ verteilt diesen Block wieder neu.";
    }

    // ───────────── T-0728 (offen aus T-0719): Wochenansicht ─────────────

    /// <summary>Tag oder Woche rechts im Tagesplaner (gilt bis zum Neustart).</summary>
    public bool DayWeek { get; set; }

    Control DayViewSwitch()
    {
        Button B(string name, string text, bool on, bool week)
        {
            var b = new Button { Name = name, Content = text, Padding = new Thickness(12, 3), FontSize = 12, Background = Brushes.Transparent, Foreground = Text, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
            if (on) QuietGold(b);
            b.Click += (_, _) => { DayWeek = week; Show(DayKey); };
            return b;
        }
        return new StackPanel { Name = "dp-view", Orientation = Orientation.Horizontal, Spacing = 6, Children = { B("dp-view-tag", "Tag", !DayWeek, false), B("dp-view-woche", "Woche", DayWeek, true) } };
    }

    /// <summary>Montag der Woche, in der <paramref name="day"/> liegt.</summary>
    static DateTime DpMonday(DateTime day) => day.Date.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    /// <summary>Tagespläne der Woche: offene Dokumente zuerst, dann gespeicherte Tagesplaner-Dateien (Arbeitsordner und „Zuletzt“). Datum → (offenes Dokument, Datei, Blöcke).</summary>
    public Dictionary<DateTime, (ProgramDocument? Doc, string? File, List<TimelineBlock> Blocks)> DayWeekPlans(DateTime monday)
    {
        var res = new Dictionary<DateTime, (ProgramDocument?, string?, List<TimelineBlock>)>();
        var open = _progDocs.GetValueOrDefault(DayKey) ?? [];
        void Take(ProgramDocument doc, string? file)
        {
            var day = DpDay(doc);
            if (day < monday || day >= monday.AddDays(7) || res.ContainsKey(day)) return;
            res[day] = (file is null ? doc : null, file ?? doc.Path, DayBlocks(doc));
        }
        foreach (var doc in open) Take(doc, null);
        var files = new List<string>();
        try { var dir = WorkFolder("Tagesplaner"); if (Directory.Exists(dir)) files.AddRange(Directory.GetFiles(dir, "*" + ProgramDocument.Extension)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        files.AddRange(ProgRecent.Get("STUDIO-DAYPLAN-01"));
        foreach (var f in files.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (open.Any(o => o.Path is { } p && ProgramDocument.SameFile(p, f))) continue;
            try { Take(ProgramDocument.Load(f, "STUDIO-DAYPLAN-01"), f); }
            catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or System.Text.Json.JsonException) { } // fremde oder kaputte Datei: in der Woche nicht zeigen
        }
        return res;
    }

    /// <summary>Wochenansicht: sieben Spalten Montag bis Sonntag mit den Blöcken der Tagespläne; Tage ohne Plan zeigen die Termine aus „E-Mails &amp; Termine“.
    /// Klick auf einen Tag öffnet seinen Plan bzw. legt ihn an (danach wieder Tagesansicht).</summary>
    Control DayWeekView(ProgramDocument d)
    {
        var day = DpDay(d); var monday = DpMonday(day); var plans = DayWeekPlans(monday);
        var grid = new Grid { Name = "dp-week", ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*,*,*"), ColumnSpacing = 4, Margin = new Thickness(0, 4, 0, 0) };
        for (var i = 0; i < 7; i++)
        {
            var date = monday.AddDays(i); var col = new StackPanel { Name = "dp-week-day-" + i, Spacing = 3 };
            var openBtn = new Button { Name = "dp-week-open-" + i, Content = new TextBlock { Text = date.ToString("ddd d.M.", De), FontSize = 12, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, Padding = new Thickness(4, 3), Background = Brushes.Transparent, Foreground = Text, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
            if (date == day) QuietGold(openBtn);
            ToolTip.SetTip(openBtn, plans.ContainsKey(date) ? "Diesen Tag öffnen" : "Für diesen Tag einen Plan anlegen");
            var dd = date; openBtn.Click += (_, _) => ProgramRun(DayKey, () => DayOpenDate(dd));
            col.Children.Add(openBtn);
            List<(int S, int E, string Title, string Kind)> lines;
            if (plans.TryGetValue(date, out var pl)) lines = [.. pl.Blocks.OrderBy(b => b.Start).Select(b => (b.Start, b.End, b.Title, b.Kind))];
            else
            {
                lines = [];
                try { foreach (var (e, s, en) in Cal.Occurrences(date, date.AddDays(1)).Where(o => !o.Event.AllDay)) lines.Add((DpMin(s < date ? date : s, date), DpMin(en, date), e.Title, DayPlanner.Fixed)); }
                catch (Exception e) when (e is IOException or InvalidOperationException) { }
                lines.Sort((a, b) => a.S.CompareTo(b.S));
            }
            if (lines.Count == 0) col.Children.Add(new TextBlock { Text = "frei", Foreground = Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center });
            foreach (var (s, _, title, kind) in lines.Take(14))
            {
                var edge = kind == DayPlanner.Work ? GoldEdge : kind == DayPlanner.Pause ? Brush.Parse("#6B7280") : Brush.Parse("#9CA3AF");
                col.Children.Add(new Border { BorderBrush = edge, BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(5, 1), Background = Brush.Parse("#10FFFFFF"), CornerRadius = new CornerRadius(3),
                    Child = new StackPanel { Children = {
                        new TextBlock { Text = DayTimeline.Hm(s), Foreground = Muted, FontSize = 10 },
                        new TextBlock { Text = kind == DayPlanner.Pause ? "Pause" : kind == DayPlanner.Fixed ? "Termin: " + title : title, Foreground = Text, FontSize = 11, TextWrapping = TextWrapping.Wrap, MaxHeight = 30, TextTrimming = TextTrimming.CharacterEllipsis } } } });
            }
            if (lines.Count > 14) col.Children.Add(new TextBlock { Text = $"+ {lines.Count - 14} weitere", Foreground = Muted, FontSize = 10 });
            Grid.SetColumn(col, i); grid.Children.Add(col);
        }
        var minutes = plans.Values.Sum(p => p.Blocks.Where(b => b.Kind == DayPlanner.Work).Sum(b => b.End - b.Start));
        return new StackPanel { Spacing = 4, Children = { grid, new TextBlock { Name = "dp-week-sum", Text = $"Woche {monday:d.M.}–{monday.AddDays(6):d.M.yyyy}: {plans.Count} {(plans.Count == 1 ? "Tag" : "Tage")} mit Plan, {minutes / 60} h {minutes % 60} min Aufgaben", Foreground = Muted, FontSize = 11 } } };
    }

    /// <summary>Tag aus der Woche öffnen: vorhandenen Plan (offen oder gespeichert) zeigen, sonst einen leeren Tag mit diesem Datum anlegen.</summary>
    public ProgramDocument DayOpenDate(DateTime date)
    {
        date = date.Date; DayWeek = false;
        var plans = DayWeekPlans(DpMonday(date));
        ProgramDocument doc;
        var known = plans.TryGetValue(date, out var p);
        if (known && p.Doc is not null) { _progActive[DayKey] = _progDocs[DayKey].IndexOf(p.Doc); doc = p.Doc; }
        else if (known && p.File is { } f) doc = ProgramOpen(DayKey, f);
        else
        {
            doc = ProgramNew(DayKey, "leer");
            doc.Set("day", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "Tag");
            doc.Title = "Tagesplan " + date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        }
        DaySel = null; DayEditing = null;
        _progMsg[DayKey] = date.ToString("dddd, d. MMMM", De) + (known ? " ist offen." : " – neuer Tag angelegt. Aufgaben eintragen, dann „Tag planen“.");
        Show(DayKey);
        return doc;
    }

    /// <summary>T-0719: Kalenderdatei mit Aufgaben, Pausen und Terminen in den Arbeitsordner (die normale Kalenderdatei lässt Termine weg, weil sie meist schon im Kalender stehen).</summary>
    public string DayExportWithAppointments(ProgramDocument d)
    {
        var file = Path.Combine(WorkFolder("Tagesplaner"), Trendo.Core.DevStudio.SafeName(d.Title) + " mit Terminen.ics");
        Trendo.Core.AtomicFile.WriteAllText(file, DayPlanner.Ics(DpForExport(d), DateTime.UtcNow, withAppointments: true));
        _core.Audit.Append(Trendo.Core.Principal.LocalOwner.Name, "program.export", $"STUDIO-DAYPLAN-01 {Path.GetFileName(file)}");
        _progMsg[DayKey] = "Exportiert: " + file;
        return file;
    }

    /// <summary>Erster Schritt im leeren Tag: einen Block zur nächsten Viertelstunde (heute) bzw. zum Arbeitsbeginn anlegen und gleich benennen.</summary>
    void DayFirstBlock(ProgramDocument d)
    {
        var o = DpOptionsSafe(d); var start = (int)o.DayStart.TotalMinutes;
        if (DpDay(d) == DayNow.Date) start = Math.Max(start, ((int)DayNow.TimeOfDay.TotalMinutes + 14) / 15 * 15);
        DayBlockNew(d, Math.Min(start, 24 * 60 - 30), DayPlanner.Work);
    }
}
