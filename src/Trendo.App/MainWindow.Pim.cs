using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Sys;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>O1–O6 Trendo Post & Kalender: Posteingang (nur lesen), Senden nur nach Bestätigung je Nachricht, Kalender mit Tag/Woche/Monat,
/// Wiederholung und Erinnerung, Aufgaben, Regelvorschläge (aktiv erst nach Freigabe, nur in Trendos Ansicht).</summary>
public partial class MainWindow
{
    public MailAccount? MailAccountCfg { get; private set; }
    public List<MailHeader> Inbox { get; set; } = [];
    public ParsedMail? InboxOpen { get; private set; }
    public List<MailRule> MailRules { get; private set; } = [];
    public bool SendConfirmOpen { get; private set; }
    public DateTime CalDay { get; set; } = DateTime.Today;
    public string CalMode { get; set; } = "monat";
    private string _pimMsg = "", _pwDraft = "";
    private MailDraft _sendDraft = new("", "", "");
    // Runde 7: Entwürfe, Gesendet, Unterhaltungen
    MailFolders? _mailFolders; public MailFolders MailBoxes => _mailFolders ??= new MailFolders(_core.DataRoot);
    string? _draftId; MailFolders.Stored? _sentOpen; bool _inboxThreads;
    /// <summary>Ordner für gespeicherte Anhänge (im Testmodus im Datenordner).</summary>
    string MailAttachmentFolder => _opt.Isolated ? Directory.CreateDirectory(Path.Combine(_core.DataRoot, "test-anhaenge")).FullName : WorkFolder("Mail-Anhänge");
    private DateTime _calLastCheck = DateTime.Now;
    private string? _inboxFolder; private bool _composeOpen;
    private string _inboxSearch = ""; private bool _inboxAccountOpen;
    public long? InboxOpenUid { get; private set; }
    public Func<Task<Stream>>? ImapStreamOverride { get; set; }
    public Func<MailDraft, Task>? SendOverride { get; set; }


    private void InitPim()
    {
        foreach (var (key, label, view, words) in new (string, string, Func<Control>, string[])[]
        {
            ("posteingang", "Posteingang", InboxView, ["posteingang", "imap", "mails lesen", "e-mails abrufen", "antworten"]),
            ("kalender", "Kalender", CalendarView, ["kalender", "termine", "woche", "monat", "ics", "geburtstag", "erinnerung"]),
            ("aufgaben", "Aufgaben", TodoView, ["aufgaben", "to-do", "todo", "erledigen", "liste", "fällig"]),
        })
        {
            _showMap[key] = view; SysTabs.Add((key, label)); PageModules[key] = "SYS-09";
            ExtraPages.Add((label, key, words));
        }
        PageModules["post"] = "SYS-09";
        MailAccountCfg = LoadMailAccount();
        MailLoadCache();
        MailRules = LoadRules();
    }

    string PostDir { get { var d = Path.Combine(_core.DataRoot, "post"); Directory.CreateDirectory(d); return d; } }
    MailAccount? LoadMailAccount() { try { var f = Path.Combine(_core.DataRoot, "post", "konto.json"); return File.Exists(f) ? JsonSerializer.Deserialize<MailAccount>(File.ReadAllText(f)) : null; } catch (JsonException) { return null; } }
    List<MailRule> LoadRules() { try { var f = Path.Combine(_core.DataRoot, "post", "regeln.json"); return File.Exists(f) ? JsonSerializer.Deserialize<List<MailRule>>(File.ReadAllText(f)) ?? [] : []; } catch (JsonException) { return []; } }
    void SaveRules() => AtomicFile.WriteAllText(Path.Combine(PostDir, "regeln.json"), JsonSerializer.Serialize(MailRules)); // T-0714: atomar

    /// <summary>O3: Erinnerungen aus dem Kalender (alle 30 Sekunden geprüft) als Benachrichtigung.</summary>
    public List<(CalEvent Event, DateTime Start)> CheckCalendarReminders(DateTime now)
    {
        var due = Cal.DueReminders(_calLastCheck, now); _calLastCheck = now;
        foreach (var (e, start) in due) Toast($"{e.Title} – {(e.AllDay ? start.ToString("dddd, d. MMMM", De) : start.ToString("HH:mm", De) + " Uhr")}{(e.Location.Length > 0 ? " · " + e.Location : "")}");
        if (due.Count > 0) _core.Audit.Append("app", "kalender.erinnerung", string.Join(", ", due.Select(d => d.Event.Title)));
        return due;
    }

    // ---------- Kalender ----------
    Control CalendarView()
    {
        var sp = new StackPanel { Spacing = 8 };
        var nav = new WrapPanel();
        void Btn(string name, string label, Action a, bool on = false, string? tip = null) { var b = new Button { Name = name, Content = label, Margin = new Thickness(0, 0, 4, 0) }; if (tip is not null) SimpleWords.Named(b, tip); if (on) b.Background = Accent; /* ohne eigenen Hintergrund (null) wäre der Knopf nur auf der Schrift anklickbar */ b.Click += (_, _) => { a(); Show("kalender"); }; nav.Children.Add(b); }
        // V-05 (T-0684): die Pfeile sagen, wohin sie blättern – nicht „Abspielen“
        Btn("cal-prev", "◀", () => CalDay = CalMode switch { "tag" => CalDay.AddDays(-1), "woche" => CalDay.AddDays(-7), _ => CalDay.AddMonths(-1) }, tip: CalMode switch { "tag" => "Voriger Tag", "woche" => "Vorige Woche", _ => "Voriger Monat" });
        Btn("cal-today", "Heute", () => CalDay = DateTime.Today);
        Btn("cal-next", "▶", () => CalDay = CalMode switch { "tag" => CalDay.AddDays(1), "woche" => CalDay.AddDays(7), _ => CalDay.AddMonths(1) }, tip: CalMode switch { "tag" => "Nächster Tag", "woche" => "Nächste Woche", _ => "Nächster Monat" });
        foreach (var (m, l) in new[] { ("tag", "Tag"), ("woche", "Woche"), ("monat", "Monat") }) { var mm = m; Btn("cal-mode-" + m, l, () => CalMode = mm, CalMode == m); }
        var calLink = CalLink; // T-0717: Rückgängig/Wiederholen der Hülle (100 Schritte)
        Btn("cal-undo", "Rückgängig", CalUndo);
        if (!calLink.Link.CanUndo) ((Button)nav.Children[^1]).IsEnabled = false;
        nav.Children.Add(ItemRedoButton(calLink, CalRedo, "kalender"));
        var title = CalMode switch { "tag" => CalDay.ToString("dddd, d. MMMM yyyy", De), "woche" => $"Woche {ISOWeek.GetWeekOfYear(CalDay)} · {Monday(CalDay):d. MMM} – {Monday(CalDay).AddDays(6):d. MMM yyyy}", _ => CalDay.ToString("MMMM yyyy", De) };
        sp.Children.Add(new DockPanel { Children = { nav, new TextBlock { Name = "cal-title", Text = title, Foreground = Text, FontSize = 18, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0) } } });
        if (_pimMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "cal-msg", Text = _pimMsg, Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        var view = CalMode switch { "tag" => CalDayView(CalDay), "woche" => CalWeekView(), _ => CalMonthView() };
        var side = new StackPanel { Spacing = 8, Children = { CalSelected is { } uid && Cal.Get(uid) is { } sel ? CalEditPanel(sel) : CalAddForm() } };
        if (SuggestionCard("kalender", CalendarSuggestions()) is { } sugg) side.Children.Insert(0, Card(sugg));
        var split = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,330") };
        split.Children.Add(view); Grid.SetColumn(side, 2); split.Children.Add(side);
        sp.Children.Add(split);
        ItemKeys(sp, CalUndo, CalRedo, () => Show("kalender")); // Strg+Z, Strg+Y
        return SysPage("Kalender", sp);
    }

    static DateTime Monday(DateTime d) => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    Control EventChip((CalEvent Event, DateTime Start, DateTime End) o, int i)
    {
        var e = o.Event;
        var b = new Button { Name = $"cal-ev-{i}", Padding = new Thickness(5, 2), Margin = new Thickness(0, 1), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brush.Parse(e.AllDay ? "#5510B981" : "#55C9A227"),
            Content = new TextBlock { Text = (e.AllDay ? "" : o.Start.ToString("HH:mm ", De)) + e.Title + (e.Repeat != Repeat.Keine ? " ↻" : ""), FontSize = 12, Foreground = Text, TextTrimming = TextTrimming.CharacterEllipsis } };
        ToolTip.SetTip(b, $"{e.Title}\n{(e.AllDay ? "ganztägig" : $"{o.Start:HH:mm}–{o.End:HH:mm}")}{(e.Location.Length > 0 ? "\n" + e.Location : "")}{(e.Notes.Length > 0 ? "\n" + e.Notes : "")}");
        var menu = new ContextMenu(); var del = new MenuItem { Header = "Termin löschen" }; del.Click += (_, _) => { Cal.Delete(e.Uid); _pimMsg = $"„{e.Title}“ gelöscht."; Show("kalender"); }; menu.Items.Add(del); b.ContextMenu = menu;
        b.Click += (_, _) => { if (_calDragged) { _calDragged = false; return; } CalSelected = e.Uid; Show("kalender"); };
        // im Monat auf einen anderen Tag ziehen
        Point? from = null;
        b.AddHandler(PointerPressedEvent, (_, ev) => { from = ev.GetPosition(this); _calDragged = false; }, Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, true);
        b.AddHandler(PointerMovedEvent, (_, ev) => { if (from is { } f && (Math.Abs(ev.GetPosition(this).X - f.X) + Math.Abs(ev.GetPosition(this).Y - f.Y)) > 8) _calDragged = true; }, Avalonia.Interactivity.RoutingStrategies.Bubble, true);
        b.AddHandler(PointerReleasedEvent, (_, ev) =>
        {
            if (from is null || !_calDragged) { from = null; return; }
            from = null;
            var hit = Avalonia.Input.InputExtensions.InputHitTest(this, ev.GetPosition(this)) as Visual;
            while (hit is not null && !(hit is Border { Tag: DateTime })) hit = Avalonia.VisualTree.VisualExtensions.GetVisualParent(hit);
            if (hit is Border { Tag: DateTime day } && day != o.Start.Date) CalChange(e.Uid, () => Cal.Shift(e.Uid, day - o.Start.Date), $"„{e.Title}“ auf {day:dddd, d. MMMM} verschoben.");
        }, Avalonia.Interactivity.RoutingStrategies.Bubble, true);
        return b;
    }

    Control CalMonthView()
    {
        var first = new DateTime(CalDay.Year, CalDay.Month, 1); var start = Monday(first);
        var occ = Cal.Occurrences(start, start.AddDays(42));
        var g = new Grid { Name = "cal-month", ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*,*,*"), RowDefinitions = new RowDefinitions("Auto,*,*,*,*,*,*") };
        var days = new[] { "Mo", "Di", "Mi", "Do", "Fr", "Sa", "So" };
        // V-21 (T-0709): Kalender mit Tageslinien – Wochentag steht über der Tageszahl (links bündig), jede Zelle hat sichtbare Linien wie in jedem Kalenderprogramm
        var line = Brush.Parse("#33FFFFFF");
        g.Children.Add(new Border { Name = "cal-month-frame", BorderBrush = line, BorderThickness = new Thickness(1, 1, 0, 0), [Grid.RowProperty] = 1, [Grid.RowSpanProperty] = 6, [Grid.ColumnSpanProperty] = 7, IsHitTestVisible = false });
        for (var c = 0; c < 7; c++) { var h = new TextBlock { Text = days[c], Foreground = Muted, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(5, 0, 0, 4) }; Grid.SetColumn(h, c); g.Children.Add(h); }
        var k = 0;
        for (var i = 0; i < 42; i++)
        {
            var d = start.AddDays(i);
            var cell = new StackPanel { Spacing = 1, Children = { new TextBlock { Text = d.Day.ToString(De), FontSize = 12, FontWeight = d == DateTime.Today ? FontWeight.Bold : FontWeight.Normal, Foreground = d.Month == CalDay.Month ? Text : Muted } } };
            foreach (var o in occ.Where(o => o.Start.Date <= d && o.End > d).Take(3)) cell.Children.Add(EventChip(o, k++));
            var more = occ.Count(o => o.Start.Date <= d && o.End > d) - 3; if (more > 0) cell.Children.Add(new TextBlock { Text = $"+{more} weitere", FontSize = 11, Foreground = Muted });
            var border = new Border { Name = $"cal-cell-{i}", Tag = d, Child = cell, MinHeight = 86, Padding = new Thickness(4), BorderBrush = line, BorderThickness = new Thickness(0, 0, 1, 1), Background = d == DateTime.Today ? Brush.Parse("#22C9A227") : Brushes.Transparent };
            var day = d; border.PointerPressed += (_, ev) => { if (ev.ClickCount == 2) { CalDay = day; CalMode = "tag"; Show("kalender"); } };
            Grid.SetRow(border, 1 + i / 7); Grid.SetColumn(border, i % 7); g.Children.Add(border);
        }
        return g;
    }

    // ---------- Zeitachse (Woche, Tag): ziehen, Dauer ändern, neu anlegen ----------
    public string? CalSelected { get; set; }
    private bool _calDragged;
    public CalTimeGrid? CalGridForTests { get; private set; }

    /// <summary>Kalender-Änderung mit Rückgängig: vorher merken, ändern, melden.</summary>
    void CalChange(string uid, Func<CalEvent> change, string msg)
    {
        var before = Cal.Get(uid);
        try { var n = change(); Items(CalLink).Record(msg.Length > 0 ? msg : "Termin geändert", n.Uid, Node(before), Node(Cal.Get(n.Uid))); CalSelected = n.Uid; _pimMsg = msg; } // T-0717: Rückgängig der Hülle
        catch (InvalidOperationException ex) { _pimMsg = ex.Message; }
        Show("kalender");
    }

    /// <summary>T-0717: Rückgängig/Wiederholen der Hülle (100 Schritte, vorher eigener Stapel mit 50 ohne Wiederholen).</summary>
    public void CalUndo() { ItemStep(CalLink, true, out var m); _pimMsg = m; }
    public void CalRedo() { ItemStep(CalLink, false, out var m); _pimMsg = m; }

    Control CalTimeView(DateTime first, int days, string name)
    {
        var occ = Cal.Occurrences(first, first.AddDays(days));
        var grid = new CalTimeGrid { Name = "cal-grid", FirstDay = first, Days = days, Items = occ, SelectedUid = CalSelected };
        CalGridForTests = grid;
        grid.EventClicked = uid => { CalSelected = uid; Show("kalender"); };
        grid.EventMoved = (uid, by) => { var ev = Cal.Get(uid); CalChange(uid, () => Cal.Shift(uid, by), $"„{ev?.Title}“ verschoben auf {(ev is null ? "" : (ev.Start + by).ToString("ddd d.M. HH:mm", De))}."); };
        grid.EventResized = (uid, end) => CalChange(uid, () => Cal.SetEnd(uid, end), $"Dauer geändert – endet {end:HH:mm} Uhr.");
        grid.RangeCreated = (a, b) =>
        {
            var e = new CalEvent(Guid.NewGuid().ToString("N"), a, b, "Neuer Termin");
            CalChange(e.Uid, () => { Cal.Add(e); return e; }, $"Neuer Termin {a:ddd d.M.} {a:HH:mm}–{b:HH:mm} – rechts benennen.");
        };
        // Kopf: Wochentage und ganztägige Termine
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("52," + string.Join(",", Enumerable.Repeat("*", days))) };
        var k = 0;
        for (var c = 0; c < days; c++)
        {
            var d = first.AddDays(c);
            var col = new StackPanel { Spacing = 1, Margin = new Thickness(2, 0, 2, 4), Children = { new TextBlock { Text = d.ToString(days == 1 ? "dddd, d. MMMM" : "ddd d.M.", De), Foreground = d == DateTime.Today ? Brush.Parse("#60A5FA") : Text, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center } } };
            foreach (var o in occ.Where(o => o.Event.AllDay && o.Start.Date <= d && o.End > d)) col.Children.Add(EventChip(o, k++));
            Grid.SetColumn(col, c + 1); head.Children.Add(col);
        }
        var scroll = new ScrollViewer { Name = "cal-scroll", Content = grid, Height = 560, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        var startHour = first <= DateTime.Today && DateTime.Today < first.AddDays(days) ? Math.Clamp(DateTime.Now.Hour - 2, 0, 14) : 7; // heute: kurz vor jetzt, sonst ab 7 Uhr
        scroll.AttachedToVisualTree += (_, _) => _ui.Post(() => scroll.Offset = new Vector(0, startHour * grid.HourHeight));
        var hint = new TextBlock { Text = "Termin ziehen verschiebt ihn · Unterkante ziehen ändert die Dauer · in freie Zeit ziehen legt einen neuen an · Klick wählt aus", Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
        var g = new Grid { Name = name, RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        g.Children.Add(head); Grid.SetRow(scroll, 1); g.Children.Add(scroll); Grid.SetRow(hint, 2); g.Children.Add(hint);
        return new Border { CornerRadius = new CornerRadius(12), BorderBrush = Edge, BorderThickness = new Thickness(1), Padding = new Thickness(6), Background = TileBg, Child = g };
    }

    Control CalWeekView() => CalTimeView(Monday(CalDay), 7, "cal-week");

    Control CalDayView(DateTime day)
    {
        var sp = new StackPanel { Name = "cal-day", Spacing = 6 };
        sp.Children.Add(CalTimeView(day.Date, 1, "cal-day-grid"));
        if (Cal.Occurrences(day, day.AddDays(1)).Count == 0) sp.Children.Add(new TextBlock { Name = "cal-day-empty", Text = "Keine Termine – in die Zeitachse ziehen oder rechts eintragen.", Foreground = Muted, FontSize = 12 });
        return sp;
    }

    /// <summary>Gewählten Termin bearbeiten – alle Felder, Übernehmen ist ein Schritt im Rückgängig.</summary>
    Control CalEditPanel(CalEvent e)
    {
        var t = new TextBox { Name = "cal-edit-title", Text = e.Title };
        var date = new TextBox { Name = "cal-edit-date", Text = e.Start.ToString("dd.MM.yyyy", De), Width = 105 };
        var from = new TextBox { Name = "cal-edit-start", Text = e.Start.ToString("HH:mm", De), Width = 64, IsEnabled = !e.AllDay };
        var to = new TextBox { Name = "cal-edit-end", Text = e.End.ToString("HH:mm", De), Width = 64, IsEnabled = !e.AllDay };
        var allDay = new CheckBox { Name = "cal-edit-allday", Content = "ganztägig", IsChecked = e.AllDay, Foreground = Text };
        allDay.IsCheckedChanged += (_, _) => { from.IsEnabled = to.IsEnabled = allDay.IsChecked != true; };
        var loc = new TextBox { Name = "cal-edit-loc", Text = e.Location, PlaceholderText = "Ort" };
        var notes = new TextBox { Name = "cal-edit-notes", Text = e.Notes, PlaceholderText = "Notizen", AcceptsReturn = true, MinHeight = 60, TextWrapping = TextWrapping.Wrap };
        int[] remindVals = [-1, 5, 15, 60, 1440];
        var remind = new ComboBox { Name = "cal-edit-remind", ItemsSource = new[] { "keine Erinnerung", "5 min vorher", "15 min vorher", "1 Stunde vorher", "1 Tag vorher" }, SelectedIndex = Math.Max(0, Array.IndexOf(remindVals, e.RemindMin)), HorizontalAlignment = HorizontalAlignment.Stretch };
        var rep = new ComboBox { Name = "cal-edit-repeat", ItemsSource = new[] { "einmalig", "täglich", "wöchentlich", "monatlich", "jährlich" }, SelectedIndex = (int)e.Repeat, HorizontalAlignment = HorizontalAlignment.Stretch };
        var save = new Button { Name = "cal-edit-save", Content = "Übernehmen", Background = Accent, Foreground = Brushes.White };
        save.Click += (_, _) =>
        {
            if ((t.Text ?? "").Trim().Length == 0) { _pimMsg = "Bitte einen Titel eingeben."; Show("kalender"); return; }
            if (!DateTime.TryParseExact((date.Text ?? "").Trim(), ["d.M.yyyy", "dd.MM.yyyy", "d.M.yy"], De, DateTimeStyles.None, out var d)) { _pimMsg = "Datum bitte als TT.MM.JJJJ."; Show("kalender"); return; }
            var all = allDay.IsChecked == true;
            DateTime s0, e0;
            if (all) { s0 = d; e0 = d.AddDays(Math.Max(1, (e.End.Date - e.Start.Date).Days)); }
            else
            {
                if (!TimeSpan.TryParse((from.Text ?? "").Trim(), De, out var a) || !TimeSpan.TryParse((to.Text ?? "").Trim(), De, out var b)) { _pimMsg = "Uhrzeit bitte als HH:MM."; Show("kalender"); return; }
                s0 = d + a; e0 = d + b; if (e0 <= s0) e0 = e0.AddDays(1); // über Mitternacht
            }
            var n = e with { Title = t.Text!.Trim(), Start = s0, End = e0, AllDay = all, Location = loc.Text?.Trim() ?? "", Notes = notes.Text?.Trim() ?? "", RemindMin = remindVals[Math.Max(0, remind.SelectedIndex)], Repeat = (Repeat)Math.Max(0, rep.SelectedIndex) };
            CalDay = s0.Date; CalChange(e.Uid, () => { Cal.Add(n); return n; }, $"„{n.Title}“ gespeichert.");
        };
        var del = new Button { Name = "cal-edit-delete", Content = "Löschen" };
        del.Click += (_, _) => { var before = Cal.Get(e.Uid); Cal.Delete(e.Uid); if (before is not null) Items(CalLink).Record($"„{e.Title}“ gelöscht", e.Uid, Node(before), null); CalSelected = null; _pimMsg = $"„{e.Title}“ gelöscht – Rückgängig holt ihn zurück."; Show("kalender"); };
        var close = new Button { Name = "cal-edit-close", Content = "Fertig" };
        close.Click += (_, _) => { CalSelected = null; Show("kalender"); };
        Control L(string label, Control c) => new StackPanel { Spacing = 2, Children = { new TextBlock { Text = label, Foreground = Muted, FontSize = 11.5 }, c } };
        return Card(new StackPanel { Name = "cal-edit", Spacing = 8, Children = {
            H("Termin", 14), L("Titel", t),
            new WrapPanel { Children = { L("Datum", date), new Border { Width = 6 }, L("Beginn", from), new Border { Width = 6 }, L("Ende", to) } }, allDay,
            L("Ort", loc), L("Erinnerung", remind), L("Wiederholung", rep), L("Notizen", notes),
            new WrapPanel { Children = { save, new Border { Width = 6 }, del, new Border { Width = 6 }, close } },
            new TextBlock { Text = e.Repeat != Repeat.Keine ? "Wiederholung: Verschieben und Ändern gelten für die ganze Reihe." : "Ziehen in der Zeitachse ändert Beginn und Dauer direkt.", Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap } } });
    }

    Control CalAddForm()
    {
        var t = new TextBox { Name = "cal-new-title", PlaceholderText = "Neuer Termin", Width = 220 };
        var date = new TextBox { Name = "cal-new-date", Text = CalDay.ToString("dd.MM.yyyy", De), Width = 105 };
        var time = new TextBox { Name = "cal-new-time", Text = "10:00", Width = 70 };
        var dur = new TextBox { Name = "cal-new-dur", Text = "60", Width = 60 };
        var allDay = new CheckBox { Name = "cal-new-allday", Content = "ganztägig", Foreground = Text };
        var loc = new TextBox { Name = "cal-new-loc", PlaceholderText = "Ort", Width = 150 };
        var remind = new ComboBox { Name = "cal-new-remind", ItemsSource = new[] { "keine Erinnerung", "5 min", "15 min", "1 Stunde", "1 Tag" }, SelectedIndex = 2, Width = 140 };
        var rep = new ComboBox { Name = "cal-new-repeat", ItemsSource = new[] { "einmalig", "täglich", "wöchentlich", "monatlich", "jährlich" }, SelectedIndex = 0, Width = 120 };
        var add = new Button { Name = "cal-add", Content = "Eintragen", Background = Accent };
        add.Click += (_, _) =>
        {
            if ((t.Text ?? "").Trim().Length == 0) { _pimMsg = "Bitte einen Titel eingeben."; Show("kalender"); return; }
            if (!DateTime.TryParseExact((date.Text ?? "").Trim(), ["d.M.yyyy", "dd.MM.yyyy", "d.M.yy"], De, DateTimeStyles.None, out var d)) { _pimMsg = "Datum bitte als TT.MM.JJJJ."; Show("kalender"); return; }
            var all = allDay.IsChecked == true;
            var start = all ? d : d + (TimeSpan.TryParse((time.Text ?? "").Trim(), De, out var ts) ? ts : TimeSpan.FromHours(10));
            var end = all ? d.AddDays(1) : start.AddMinutes(int.TryParse(dur.Text, out var m) && m > 0 ? m : 60);
            var rmin = remind.SelectedIndex switch { 0 => -1, 1 => 5, 3 => 60, 4 => 1440, _ => 15 };
            var ev = new CalEvent(Guid.NewGuid().ToString("N"), start, end, t.Text!.Trim(), loc.Text?.Trim() ?? "", "", all, rmin, (Repeat)Math.Max(0, rep.SelectedIndex));
            Cal.Add(ev); Items(CalLink).Record($"„{ev.Title}“ eingetragen", ev.Uid, null, Node(ev)); // T-0717: Eintragen ist jetzt auch rückgängig zu machen
            _pimMsg = $"„{t.Text!.Trim()}“ eingetragen."; CalDay = d; Show("kalender");
        };
        var ics = new Button { Name = "cal-export", Content = "Als ICS speichern" };
        ics.Click += (_, _) => { var f = Path.Combine(WorkFolder("kalender"), "kalender.ics"); Cal.ExportFile(f); _pimMsg = "Gespeichert: " + f; NotifyFileResult(_pimMsg); Show("kalender"); };
        var imp = new Button { Name = "cal-import", Content = "ICS einlesen …" };
        imp.Click += async (_, _) => { if (await PickMedia("cal-ics", ["*.ics"]) is { } f) CalendarImportFile(f); Show("kalender"); };
        return Card(new StackPanel { Spacing = 6, Children = { H("Termin eintragen", 14), new WrapPanel { Children = { t, new Border { Width = 4 }, date, new Border { Width = 4 }, time, new Border { Width = 4 }, dur, new TextBlock { Text = " min ", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }, allDay } },
            new WrapPanel { Children = { loc, new Border { Width = 4 }, remind, new Border { Width = 4 }, rep, new Border { Width = 4 }, add, new Border { Width = 12 }, imp, new Border { Width = 4 }, ics } } } });
    }

    public string PimMessage => _pimMsg;

    /// <summary>Kalenderdatei einlesen (öffentlich für Tests). Fremde Regeln, Ausnahmen und Zeitzonen löst ical.net auf (Alle Studios, Runde 6).</summary>
    public (int Events, int Todos)? CalendarImportFile(string path)
    {
        try
        {
            var text = File.ReadAllText(path);
            var full = IcsForeign.NeedsFullParser(text);
            var r = Cal.ImportText(text);
            _pimMsg = $"{r.Events} Termine und {r.Todos} Aufgaben eingelesen." + (full ? " Wiederholungen mit Ende, Ausnahmen und Zeitzonen wurden genau übernommen; Reihen mit Regeln stehen als einzelne Termine (ein Jahr zurück bis zwei Jahre voraus)." : "");
            return r;
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException) { _pimMsg = "Kalenderdatei ließ sich nicht einlesen: " + Trendo.Core.FriendlyError.Say(ex); return null; }
    }

    // ---------- Aufgaben: Tafel (Offen · In Arbeit · Erledigt) ----------
    public string? TodoSelected { get; set; }
    public string TodoListFilter { get; set; } = "";
    static readonly string[] Stages = ["Offen", "In Arbeit", "Erledigt"];

    void TodoChange(string uid, Func<TodoItem?> change, string msg)
    {
        var before = Cal.GetTodo(uid);
        try { change(); Items(TodoLink).Record(msg.Length > 0 ? msg : "Aufgabe geändert", uid, Node(before), Node(Cal.GetTodo(uid))); _pimMsg = msg; } // T-0717: Rückgängig der Hülle
        catch (InvalidOperationException ex) { _pimMsg = ex.Message; }
        Show("aufgaben");
    }

    /// <summary>T-0717: Rückgängig/Wiederholen der Hülle (100 Schritte, vorher eigener Stapel ohne Grenze und ohne Wiederholen).</summary>
    public void TodoUndo() { ItemStep(TodoLink, true, out var m); _pimMsg = m; }
    public void TodoRedo() { ItemStep(TodoLink, false, out var m); _pimMsg = m; }

    /// <summary>Aufgabe in eine Spalte setzen (auch für Tests und die KI).</summary>
    public void TodoMove(string uid, int stage) => TodoChange(uid, () => Cal.SetStage(uid, stage), $"„{Cal.GetTodo(uid)?.Title}“ → {Stages[Math.Clamp(stage, 0, 2)]}.");

    Control TodoView()
    {
        var sp = new StackPanel { Spacing = 8 };
        var title = new TextBox { Name = "todo-title", PlaceholderText = "Neue Aufgabe", Width = 280 };
        var due = new TextBox { Name = "todo-due", PlaceholderText = "fällig TT.MM.JJJJ", Width = 130 };
        var prio = new ComboBox { Name = "todo-prio", ItemsSource = new[] { "hoch", "normal", "niedrig" }, SelectedIndex = 1, Width = 100 };
        var list = new TextBox { Name = "todo-list", Text = TodoListFilter.Length > 0 ? TodoListFilter : "Allgemein", Width = 120 };
        var add = new Button { Name = "todo-add", Content = "Hinzufügen", Background = Accent };
        add.Click += (_, _) =>
        {
            if ((title.Text ?? "").Trim().Length == 0) { _pimMsg = "Bitte eine Aufgabe eintragen."; Show("aufgaben"); return; }
            DateTime? d = DateTime.TryParseExact((due.Text ?? "").Trim(), ["d.M.yyyy", "dd.MM.yyyy"], De, DateTimeStyles.None, out var dd) ? dd : null;
            var t = new TodoItem(Guid.NewGuid().ToString("N"), title.Text!.Trim(), d, false, prio.SelectedIndex + 1, (list.Text ?? "").Trim() is { Length: > 0 } l ? l : "Allgemein");
            TodoChange(t.Uid, () => { Cal.AddTodo(t); return t; }, "");
        };
        title.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) add.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); };
        var todoLink = TodoLink;
        var undo = new Button { Name = "todo-undo", Content = "Rückgängig", IsEnabled = todoLink.Link.CanUndo };
        undo.Click += (_, _) => { TodoUndo(); Show("aufgaben"); };
        sp.Children.Add(Card(new WrapPanel { Children = { title, new Border { Width = 4 }, due, new Border { Width = 4 }, prio, new Border { Width = 4 }, list, new Border { Width = 4 }, add, new Border { Width = 12 }, undo, ItemRedoButton(todoLink, TodoRedo, "aufgaben") } }));

        if (SuggestionCard("aufgaben", TodoSuggestions()) is { } sugg) sp.Children.Add(Card(sugg));
        var todos = Cal.Todos();
        // Listen als Filter
        var lists = todos.Select(t => t.List).Distinct().OrderBy(x => x, StringComparer.CurrentCulture).ToList();
        if (lists.Count > 1)
        {
            var chips = new WrapPanel { Name = "todo-lists" };
            foreach (var l in lists.Prepend(""))
            {
                var ll = l; var on = TodoListFilter == l;
                var b = new Button { Name = "todo-filter-" + (l.Length == 0 ? "alle" : lists.IndexOf(l).ToString()), Content = l.Length == 0 ? "Alle Listen" : $"{l} ({todos.Count(t => t.List == l && !t.Done)})", Margin = new Thickness(0, 0, 4, 4) };
                if (on) b.Background = Accent;
                b.Click += (_, _) => { TodoListFilter = ll; Show("aufgaben"); };
                chips.Children.Add(b);
            }
            sp.Children.Add(chips);
        }
        if (TodoListFilter.Length > 0) todos = todos.Where(t => t.List == TodoListFilter).ToList();
        if (_pimMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "todo-msg", Text = _pimMsg, Foreground = Muted, FontSize = 13 });

        var board = new Grid { Name = "todo-board", ColumnDefinitions = new ColumnDefinitions("*,10,*,10,*") };
        var idx = 0;
        for (var stage = 0; stage < 3; stage++)
        {
            var items = todos.Where(t => t.Stage == stage).ToList();
            var col = new StackPanel { Spacing = 6 };
            col.Children.Add(new DockPanel { Children = {
                new TextBlock { Text = $"{items.Count}", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, [DockPanel.DockProperty] = Avalonia.Controls.Dock.Right },
                new TextBlock { Text = Stages[stage], Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold } } });
            foreach (var t in items) col.Children.Add(TodoCard(t, idx++));
            if (items.Count == 0) col.Children.Add(new TextBlock { Text = stage switch { 0 => "Nichts offen – oben eintragen.", 1 => "Karte hierher ziehen, wenn du daran arbeitest.", _ => "Erledigtes landet hier." }, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            var lane = new Border { Name = $"todo-col-{stage}", Tag = stage, MinHeight = 360, Padding = new Thickness(10), CornerRadius = new CornerRadius(12), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Child = col };
            Grid.SetColumn(lane, stage * 2); board.Children.Add(lane);
        }
        // V-16 (T-0684): ein Leerzustand je Seite – ohne Aufgaben steht nur der Hinweis da, nicht zusätzlich drei leere Spalten, die dasselbe sagen
        var noTodos = Cal.Todos().Count == 0;
        if (noTodos) sp.Children.Add(EmptyState("todo-empty", "✅", "Keine Aufgaben", "Oben eintragen – mit Fälligkeit und Priorität. Danach erscheinen hier die Spalten Offen, In Arbeit und Erledigt."));
        var side = TodoSelected is { } su && Cal.GetTodo(su) is { } sel ? TodoEditPanel(sel) : null;
        var split = new Grid { ColumnDefinitions = new ColumnDefinitions(side is null ? "*" : "*,12,320") };
        split.Children.Add(board); if (side is not null) { Grid.SetColumn(side, 2); split.Children.Add(side); }
        if (!noTodos)
        {
            sp.Children.Add(split);
            sp.Children.Add(new TextBlock { Text = "Karte ziehen: in eine andere Spalte setzen · Klick: bearbeiten · Häkchen: erledigt", Foreground = Muted, FontSize = 11.5 });
        }
        ItemKeys(sp, TodoUndo, TodoRedo, () => Show("aufgaben")); // Strg+Z, Strg+Y
        return SysPage("Aufgaben", sp);
    }

    Control TodoCard(TodoItem t, int idx)
    {
        var uid = t.Uid;
        var overdue = !t.Done && t.Due is { } dd && dd.Date < DateTime.Today;
        var cb = new CheckBox { Name = $"todo-done-{idx}", IsChecked = t.Done, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 4, 0) };
        cb.IsCheckedChanged += (_, _) => { if ((cb.IsChecked == true) != t.Done) TodoChange(uid, () => Cal.SetStage(uid, cb.IsChecked == true ? 2 : 0), cb.IsChecked == true ? $"„{t.Title}“ erledigt." : $"„{t.Title}“ wieder offen."); };
        var info = new WrapPanel();
        if (t.Priority == 1) info.Children.Add(Chip("hoch", "#EF4444"));
        if (t.Due is { } d) info.Children.Add(Chip((overdue ? "überfällig " : "fällig ") + d.ToString("dd.MM.", De), overdue ? "#EF4444" : "#64748B"));
        info.Children.Add(Chip(t.List, "#2F86F6"));
        var body = new StackPanel { Spacing = 4, Children = {
            new TextBlock { Text = t.Title, Foreground = t.Done ? Muted : Text, FontSize = 13.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, TextDecorations = t.Done ? TextDecorations.Strikethrough : null }, info } };
        if (t.Notes.Length > 0) body.Children.Add(new TextBlock { Text = t.Notes, Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, MaxHeight = 32, TextTrimming = TextTrimming.CharacterEllipsis });
        var card = new Border { Name = $"todo-card-{idx}", Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(10), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            Background = Brush.Parse(TodoSelected == uid ? "#33C9A227" : "#14FFFFFF"), BorderBrush = Brush.Parse(TodoSelected == uid ? "#FACC15" : overdue ? "#88EF4444" : "#22FFFFFF"), BorderThickness = new Thickness(TodoSelected == uid ? 1.5 : 1),
            Child = new DockPanel { Children = { cb, body } } };
        DockPanel.SetDock(cb, Avalonia.Controls.Dock.Left);
        // ziehen in eine andere Spalte, Klick wählt aus
        Point? from = null; var dragged = false;
        card.PointerPressed += (_, e) => { if (e.Source is CheckBox || (e.Source is Visual sv && Avalonia.VisualTree.VisualExtensions.FindAncestorOfType<CheckBox>(sv) is not null)) return; from = e.GetPosition(this); dragged = false; e.Pointer.Capture(card); e.Handled = true; };
        card.PointerMoved += (_, e) => { if (from is { } f && Math.Abs(e.GetPosition(this).X - f.X) + Math.Abs(e.GetPosition(this).Y - f.Y) > 8) { dragged = true; card.Opacity = 0.6; } };
        card.PointerReleased += (_, e) =>
        {
            if (from is null) return; from = null; e.Pointer.Capture(null); card.Opacity = 1;
            if (!dragged) { TodoSelected = TodoSelected == uid ? null : uid; Show("aufgaben"); return; }
            var hit = Avalonia.Input.InputExtensions.InputHitTest(this, e.GetPosition(this)) as Visual;
            while (hit is not null && !(hit is Border { Tag: int })) hit = Avalonia.VisualTree.VisualExtensions.GetVisualParent(hit);
            if (hit is Border { Tag: int stage } && stage != t.Stage) TodoMove(uid, stage);
        };
        return card;

        Control Chip(string text, string color) => new Border { Margin = new Thickness(0, 0, 4, 0), Padding = new Thickness(6, 1), CornerRadius = new CornerRadius(6), Background = Brush.Parse("#33" + color.TrimStart('#')),
            Child = new TextBlock { Text = text, FontSize = 11, Foreground = Brush.Parse(color == "#64748B" ? "#CBD5E1" : color) } };
    }

    Control TodoEditPanel(TodoItem t)
    {
        var title = new TextBox { Name = "todo-edit-title", Text = t.Title };
        var due = new TextBox { Name = "todo-edit-due", Text = t.Due?.ToString("dd.MM.yyyy", De) ?? "", PlaceholderText = "TT.MM.JJJJ", Width = 120 };
        var prio = new ComboBox { Name = "todo-edit-prio", ItemsSource = new[] { "hoch", "normal", "niedrig" }, SelectedIndex = Math.Clamp(t.Priority - 1, 0, 2), Width = 110 };
        var stage = new ComboBox { Name = "todo-edit-stage", ItemsSource = Stages, SelectedIndex = t.Stage, Width = 120 };
        var list = new TextBox { Name = "todo-edit-list", Text = t.List };
        var notes = new TextBox { Name = "todo-edit-notes", Text = t.Notes, AcceptsReturn = true, MinHeight = 80, TextWrapping = TextWrapping.Wrap, PlaceholderText = "Beschreibung, Schritte, Links …" };
        var save = new Button { Name = "todo-edit-save", Content = "Übernehmen", Background = Accent, Foreground = Brushes.White };
        save.Click += (_, _) =>
        {
            if ((title.Text ?? "").Trim().Length == 0) { _pimMsg = "Bitte einen Titel eingeben."; Show("aufgaben"); return; }
            DateTime? d = null;
            if ((due.Text ?? "").Trim().Length > 0) { if (!DateTime.TryParseExact(due.Text!.Trim(), ["d.M.yyyy", "dd.MM.yyyy", "d.M.yy"], De, DateTimeStyles.None, out var dd)) { _pimMsg = "Datum bitte als TT.MM.JJJJ."; Show("aufgaben"); return; } d = dd; }
            var n = t with { Title = title.Text!.Trim(), Due = d, Priority = prio.SelectedIndex + 1, List = (list.Text ?? "").Trim() is { Length: > 0 } l ? l : "Allgemein", Notes = notes.Text?.Trim() ?? "", Done = stage.SelectedIndex == 2, Working = stage.SelectedIndex == 1 };
            TodoChange(t.Uid, () => { Cal.AddTodo(n); return n; }, $"„{n.Title}“ gespeichert.");
        };
        var del = new Button { Name = "todo-edit-delete", Content = "Löschen" };
        del.Click += (_, _) => { TodoSelected = null; TodoChange(t.Uid, () => { Cal.DeleteTodo(t.Uid); return null; }, $"„{t.Title}“ gelöscht – Rückgängig holt sie zurück."); };
        var close = new Button { Name = "todo-edit-close", Content = "Fertig" };
        close.Click += (_, _) => { TodoSelected = null; Show("aufgaben"); };
        Control L(string label, Control c) => new StackPanel { Spacing = 2, Children = { new TextBlock { Text = label, Foreground = Muted, FontSize = 11.5 }, c } };
        return Card(new StackPanel { Name = "todo-edit", Spacing = 8, Children = {
            H("Aufgabe", 14), L("Titel", title), new WrapPanel { Children = { L("Fällig", due), new Border { Width = 6 }, L("Priorität", prio) } },
            L("Spalte", stage), L("Liste", list), L("Beschreibung", notes),
            new WrapPanel { Children = { save, new Border { Width = 6 }, del, new Border { Width = 6 }, close } } } });
    }
}
