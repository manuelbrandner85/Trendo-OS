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
    public List<MailHeader> Inbox { get; private set; } = [];
    public ParsedMail? InboxOpen { get; private set; }
    public List<MailRule> MailRules { get; private set; } = [];
    public bool SendConfirmOpen { get; private set; }
    public DateTime CalDay { get; set; } = DateTime.Today;
    public string CalMode { get; set; } = "monat";
    private string _pimMsg = "", _pwDraft = "";
    private MailDraft _sendDraft = new("", "", "");
    private DateTime _calLastCheck = DateTime.Now;
    private string? _inboxFolder; private bool _composeOpen;
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
        MailRules = LoadRules();
    }

    string PostDir { get { var d = Path.Combine(_core.DataRoot, "post"); Directory.CreateDirectory(d); return d; } }
    MailAccount? LoadMailAccount() { try { var f = Path.Combine(_core.DataRoot, "post", "konto.json"); return File.Exists(f) ? JsonSerializer.Deserialize<MailAccount>(File.ReadAllText(f)) : null; } catch (JsonException) { return null; } }
    List<MailRule> LoadRules() { try { var f = Path.Combine(_core.DataRoot, "post", "regeln.json"); return File.Exists(f) ? JsonSerializer.Deserialize<List<MailRule>>(File.ReadAllText(f)) ?? [] : []; } catch (JsonException) { return []; } }
    void SaveRules() => File.WriteAllText(Path.Combine(PostDir, "regeln.json"), JsonSerializer.Serialize(MailRules));

    // ---------- Öffentliche Wege (auch für Tests und die KI) ----------
    /// <summary>Konto einrichten: Servereinstellungen werden aus der Adresse erraten; das Passwort kommt in den Geheimnisspeicher.</summary>
    public MailAccount SetMailAccount(string email, string name, string? password, string? imapHost = null, string? smtpHost = null, int? smtpPort = null)
    {
        if (!MailCenter.ValidEmail(email)) throw new ArgumentException("Bitte eine gültige E-Mail-Adresse eintragen.");
        var a = MailAccount.Guess(email, name);
        if (imapHost is { Length: > 0 }) a = a with { ImapHost = imapHost.Trim() };
        if (smtpHost is { Length: > 0 }) a = a with { SmtpHost = smtpHost.Trim() };
        if (smtpPort is { } sp) a = a with { SmtpPort = sp, SmtpImplicitTls = sp == 465 };
        MailAccountCfg = a;
        File.WriteAllText(Path.Combine(PostDir, "konto.json"), JsonSerializer.Serialize(a));
        if (password is { Length: > 0 }) _core.Secrets.Set(a.SecretKey, password);
        _core.Audit.Append(Principal.LocalOwner.Name, "post.konto", a.Email);
        return a;
    }

    /// <summary>O1: Posteingang abrufen – nur lesend (EXAMINE/PEEK), es wird nichts auf dem Server verändert.</summary>
    public async Task<bool> FetchInbox()
    {
        if (MailAccountCfg is not { } a) { _pimMsg = "Erst das Konto einrichten."; return false; }
        var pw = _core.Secrets.Get(a.SecretKey);
        if (pw is null && ImapStreamOverride is null) { _pimMsg = "Das Passwort fehlt."; return false; }
        _pimMsg = "Posteingang wird abgerufen …";
        try
        {
            ImapSession imap;
            if (ImapStreamOverride is { } open) { imap = new ImapSession(await open()); await imap.GreetingAsync(); await imap.LoginAsync(a.User, pw ?? ""); }
            else imap = await MailNet.OpenImap(a, pw!);
            var n = await imap.ExamineAsync();
            Inbox = await imap.HeadersAsync(n, 50);
            await imap.LogoutAsync();
            _pimMsg = $"{Inbox.Count} Nachrichten geladen (nur gelesen – auf dem Server ändert sich nichts).";
            _core.Audit.Append(Principal.LocalOwner.Name, "post.abruf", $"{Inbox.Count}");
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Net.Sockets.SocketException or System.Security.Authentication.AuthenticationException)
        { _pimMsg = "Abrufen ging nicht: " + ex.Message; return false; }
    }

    public async Task OpenInboxMail(long uid)
    {
        if (MailAccountCfg is not { } a) return;
        try
        {
            ImapSession imap;
            if (ImapStreamOverride is { } open) { imap = new ImapSession(await open()); await imap.GreetingAsync(); await imap.LoginAsync(a.User, _core.Secrets.Get(a.SecretKey) ?? ""); }
            else imap = await MailNet.OpenImap(a, _core.Secrets.Get(a.SecretKey) ?? "");
            await imap.ExamineAsync();
            InboxOpen = await imap.MessageAsync(uid);
            await imap.LogoutAsync();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Net.Sockets.SocketException or System.Security.Authentication.AuthenticationException) { _pimMsg = "Öffnen ging nicht: " + ex.Message; }
    }

    /// <summary>O2: Senden vorbereiten – die Nachricht geht erst nach „Ja, jetzt senden“ raus.</summary>
    public void AskSend(MailDraft d)
    {
        var problems = MailCenter.Check(d);
        if (problems.Any(x => x.Contains("fehlt") || x.Contains("gültige"))) { _pimMsg = string.Join(" ", problems); return; }
        if (MailAccountCfg is null) { _pimMsg = "Erst das Konto einrichten."; return; }
        _sendDraft = d; SendConfirmOpen = true;
    }

    public async Task ConfirmSend(bool yes)
    {
        SendConfirmOpen = false;
        if (!yes || MailAccountCfg is not { } a) { _pimMsg = "Nicht gesendet."; return; }
        try
        {
            if (SendOverride is { } fake) await fake(_sendDraft);
            else await MailNet.SendAsync(a, _core.Secrets.Get(a.SecretKey) ?? throw new InvalidOperationException("Das Passwort fehlt."), _sendDraft);
            _pimMsg = $"Gesendet an {_sendDraft.To}.";
            _core.Audit.Append(Principal.LocalOwner.Name, "post.gesendet", $"{_sendDraft.To} · {_sendDraft.Subject}");
            _sendDraft = new("", "", ""); _composeOpen = false;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Net.Sockets.SocketException or System.Security.Authentication.AuthenticationException) { _pimMsg = "Senden ging nicht: " + ex.Message; }
    }

    /// <summary>O3: Erinnerungen aus dem Kalender (alle 30 Sekunden geprüft) als Benachrichtigung.</summary>
    public List<(CalEvent Event, DateTime Start)> CheckCalendarReminders(DateTime now)
    {
        var due = Cal.DueReminders(_calLastCheck, now); _calLastCheck = now;
        foreach (var (e, start) in due) Toast($"⏰ {e.Title} – {(e.AllDay ? start.ToString("dddd, d. MMMM", De) : start.ToString("HH:mm", De) + " Uhr")}{(e.Location.Length > 0 ? " · " + e.Location : "")}");
        if (due.Count > 0) _core.Audit.Append("app", "kalender.erinnerung", string.Join(", ", due.Select(d => d.Event.Title)));
        return due;
    }

    // ---------- Posteingang ----------
    Control InboxView()
    {
        var sp = new StackPanel { Spacing = 8 };
        var a = MailAccountCfg;
        // Konto
        var email = new TextBox { Name = "inbox-email", Text = a?.Email ?? "", PlaceholderText = "E-Mail-Adresse", Width = 240 };
        var name = new TextBox { Name = "inbox-name", Text = a?.Name ?? "", PlaceholderText = "Dein Name (Absender)", Width = 180 };
        var pw = new TextBox { Name = "inbox-password", PasswordChar = '•', Text = _pwDraft, PlaceholderText = a is not null && _core.Secrets.Get(a.SecretKey) is not null ? "Passwort gespeichert" : "Passwort / App-Passwort", Width = 200 };
        pw.TextChanged += (_, _) => _pwDraft = pw.Text ?? "";
        var save = new Button { Name = "inbox-save", Content = "Konto speichern" };
        save.Click += (_, _) => { try { SetMailAccount(email.Text ?? "", name.Text ?? "", _pwDraft); _pwDraft = ""; _pimMsg = $"Konto gespeichert ({MailAccountCfg!.ImapHost} / {MailAccountCfg.SmtpHost})."; } catch (ArgumentException ex) { _pimMsg = ex.Message; } Show("posteingang"); };
        var fetch = new Button { Name = "inbox-fetch", Content = "Abrufen", Background = Accent, IsEnabled = a is not null };
        fetch.Click += async (_, _) => { await FetchInbox(); Show("posteingang"); };
        sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = { H("Konto", 14), new WrapPanel { Children = { email, new Border { Width = 4 }, name, new Border { Width = 4 }, pw, new Border { Width = 4 }, save, new Border { Width = 4 }, fetch } },
            P("Trendo liest nur – auf dem Server wird nichts gelöscht oder als gelesen markiert. Bei Gmail, iCloud und Yahoo brauchst du ein App-Passwort. Das Passwort wird geschützt gespeichert.", null, 12) } }));
        // U1: drei Spalten – Ordner · Liste · Lesebereich
        var active = MailRules.Where(r => r.Active).ToList();
        var folders = new StackPanel { Name = "inbox-folders", Spacing = 2 };
        Button Folder(string name, string label, bool on, Action go)
        {
            var b = new Button { Name = name, Content = label, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(6),
                Background = on ? Accent : Brushes.Transparent, Foreground = Text, FontSize = 12.5 };
            b.Click += (_, _) => { go(); Show("posteingang"); }; return b;
        }
        folders.Children.Add(Folder("inbox-folder-alle", $"Posteingang ({Inbox.Count})", _inboxFolder is null, () => _inboxFolder = null));
        folders.Children.Add(Folder("inbox-folder-ungelesen", $"Ungelesen ({Inbox.Count(h => !h.Seen)})", _inboxFolder == "\u0001", () => _inboxFolder = "\u0001"));
        foreach (var (cat, k) in active.Select(r => r.Category).Distinct().Select((c, k) => (c, k)))
            folders.Children.Add(Folder($"inbox-folder-{k}", $"{cat} ({Inbox.Count(h => active.FirstOrDefault(r => r.Matches(h))?.Category == cat)})", _inboxFolder == cat, () => _inboxFolder = cat));
        folders.Children.Add(new Border { Height = 10 });
        var compose = new Button { Name = "inbox-new", Content = "✉ Neue Nachricht", Background = Accent, IsEnabled = a is not null, HorizontalAlignment = HorizontalAlignment.Stretch };
        compose.Click += (_, _) => { _sendDraft = new("", "", ""); _composeOpen = true; Show("posteingang"); };
        folders.Children.Add(compose);
        folders.Children.Add(Folder("inbox-go-kalender", "📅 Kalender", false, () => { }));
        ((Button)folders.Children[^1]).Click += (_, _) => Show("kalender");
        folders.Children.Add(Folder("inbox-go-aufgaben", "☑ Aufgaben", false, () => { }));
        ((Button)folders.Children[^1]).Click += (_, _) => Show("aufgaben");

        var list = new StackPanel { Name = "inbox-list", Spacing = 2 };
        var shown = 0;
        for (var i = 0; i < Inbox.Count; i++)
        {
            var h = Inbox[i]; var uid = h.Uid;
            var cat = active.FirstOrDefault(r => r.Matches(h))?.Category;
            if (_inboxFolder == "\u0001" ? h.Seen : _inboxFolder is not null && cat != _inboxFolder) continue;
            shown++;
            var b = new Button { Name = $"inbox-item-{i}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 5), Background = InboxOpenUid == uid ? Accent : Brushes.Transparent,
                Content = new StackPanel { Children = {
                    new TextBlock { Text = h.Subject.Length > 0 ? h.Subject : "(ohne Betreff)", Foreground = Text, FontWeight = h.Seen ? FontWeight.Normal : FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = $"{h.From} · {(h.Date is { } d ? d.LocalDateTime.ToString("dd.MM. HH:mm", De) : "")}{(cat is null ? "" : $"  [{cat}]")}", Foreground = Muted, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis } } } };
            b.Click += async (_, _) => { await OpenInboxMail(uid); InboxOpenUid = uid; _composeOpen = false; Show("posteingang"); };
            list.Children.Add(b);
        }
        if (Inbox.Count == 0) list.Children.Add(EmptyState("inbox-empty", "📬", "Noch keine Nachrichten geladen", "Konto eintragen und „Abrufen“ drücken."));
        else if (shown == 0) list.Children.Add(P("In diesem Ordner ist nichts.", Text));

        var read = new StackPanel { Spacing = 6 };
        if (InboxOpen is { } m && !_composeOpen && !SendConfirmOpen)
        {
            read.Children.Add(new TextBlock { Name = "inbox-subject", Text = m.Subject, Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            read.Children.Add(P($"Von {m.From} · {m.Date}", null, 12));
            // F2: Termin aus dem Text erkennen
            var reference = DateTimeOffset.TryParse(m.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var md) ? md.LocalDateTime : DateTime.Now;
            var hints = MailDates.Find(m.Subject + "\n" + m.Body, reference).Take(3).ToList();
            if (hints.Count > 0)
            {
                var hw = new WrapPanel { Name = "inbox-dates" };
                for (var k = 0; k < hints.Count; k++)
                {
                    var hint = hints[k];
                    var hb = new Button { Name = $"inbox-cal-{k}", Content = $"📅 {(hint.HasTime ? hint.Start.ToString("ddd dd.MM.yyyy, HH:mm", De) : hint.Start.ToString("ddd dd.MM.yyyy", De))} als Termin", FontSize = 12, Margin = new Thickness(0, 0, 6, 4) };
                    ToolTip.SetTip(hb, $"Gefunden: „{hint.Text}“");
                    hb.Click += (_, _) =>
                    {
                        Cal.Add(new CalEvent(Guid.NewGuid().ToString("N"), hint.Start, hint.HasTime ? hint.Start.AddHours(1) : hint.Start.AddDays(1), m.Subject.Length > 0 ? m.Subject : "Termin", "", "Aus der E-Mail von " + m.From, AllDay: !hint.HasTime));
                        _pimMsg = $"Termin eingetragen: {hint.Start.ToString(hint.HasTime ? "dddd, dd.MM. HH:mm" : "dddd, dd.MM.", De)}."; Show("posteingang");
                    };
                    hw.Children.Add(hb);
                }
                read.Children.Add(new Border { Background = Brush.Parse("#222F86F6"), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 6), Child = new StackPanel { Spacing = 4, Children = { P("Termin in dieser Nachricht gefunden – bitte prüfen:", Text, 12), hw } } });
            }
            read.Children.Add(new SelectableTextBlock { Name = "inbox-body", Text = m.Body, Foreground = Text, TextWrapping = TextWrapping.Wrap, FontSize = 13 });
            var reply = new Button { Name = "inbox-reply", Content = "Antworten", Background = Accent };
            var from = System.Text.RegularExpressions.Regex.Match(m.From, @"<([^>]+)>") is { Success: true } mm ? mm.Groups[1].Value : m.From;
            reply.Click += (_, _) => { _sendDraft = new MailDraft(from, m.Subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase) ? m.Subject : "Re: " + m.Subject, "\n\n> " + m.Body.Replace("\n", "\n> ")); _composeOpen = true; Show("posteingang"); };
            read.Children.Add(reply);
        }
        else if (_composeOpen || SendConfirmOpen)
        {
            // Schreiben mit Bestätigung
            var to = new TextBox { Name = "send-to", Text = _sendDraft.To, PlaceholderText = "An" };
            var subj = new TextBox { Name = "send-subject", Text = _sendDraft.Subject, PlaceholderText = "Betreff" };
            var body = new TextBox { Name = "send-body", Text = _sendDraft.Body, PlaceholderText = "Nachricht", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 180 };
            var send = new Button { Name = "send-ask", Content = "Senden …", Background = Accent, IsEnabled = a is not null };
            send.Click += (_, _) => { AskSend(new MailDraft(to.Text ?? "", subj.Text ?? "", body.Text ?? "")); Show("posteingang"); };
            var cancel = new Button { Name = "send-discard", Content = "Verwerfen" };
            cancel.Click += (_, _) => { _composeOpen = false; _sendDraft = new("", "", ""); Show("posteingang"); };
            read.Children.Add(H("Neue Nachricht", 15)); read.Children.Add(to); read.Children.Add(subj); read.Children.Add(body);
            read.Children.Add(new WrapPanel { Children = { send, new Border { Width = 6 }, cancel } });
            if (SendConfirmOpen)
            {
                var yes = new Button { Name = "send-confirm-yes", Content = "Ja, jetzt senden", Background = Accent }; yes.Click += async (_, _) => { await ConfirmSend(true); Show("posteingang"); };
                var no = new Button { Name = "send-confirm-no", Content = "Abbrechen" }; no.Click += async (_, _) => { await ConfirmSend(false); Show("posteingang"); };
                read.Children.Add(new Border { Name = "send-confirm", Background = Brush.Parse("#33F59E0B"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Child = new StackPanel { Spacing = 6, Children = {
                    new TextBlock { Text = $"An {_sendDraft.To}: „{_sendDraft.Subject}“ jetzt wirklich senden?", Foreground = Text, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold },
                    new WrapPanel { Children = { yes, new Border { Width = 6 }, no } } } } });
            }
        }
        else read.Children.Add(EmptyState("inbox-read-empty", "✉", "Keine Nachricht ausgewählt", "Links eine Nachricht anklicken oder eine neue schreiben."));
        sp.Children.Add(new Grid { Name = "inbox-columns", ColumnDefinitions = new ColumnDefinitions("200,8,380,8,*"), Children = {
            Card(folders), Pos(Card(new ScrollViewer { Content = list, MaxHeight = 560 }), 2), Pos(Card(new ScrollViewer { Content = read, MaxHeight = 560 }), 4) } });
        // O6 Regelvorschläge
        var rules = new StackPanel { Name = "inbox-rules", Spacing = 4, Children = { H("Sortier-Regeln (Vorschläge)", 14), P("Regeln sortieren nur in Trendos Ansicht, nie auf dem Server – und erst, wenn du sie freigibst.", null, 12) } };
        var suggestBtn = new Button { Name = "inbox-suggest", Content = "Vorschläge aus dem Posteingang", IsEnabled = Inbox.Count > 0 };
        suggestBtn.Click += (_, _) => { foreach (var r in MailRule.Suggest(Inbox)) if (!MailRules.Any(x => x.FromContains == r.FromContains)) MailRules.Add(r); SaveRules(); Show("posteingang"); };
        rules.Children.Add(suggestBtn);
        for (var i = 0; i < MailRules.Count; i++)
        {
            var r = MailRules[i]; var idx = i;
            var tg = new Button { Name = $"inbox-rule-{i}", Content = r.Active ? "Aktiv – ausschalten" : "Freigeben", Background = r.Active ? Accent : null };
            tg.Click += (_, _) => { MailRules[idx] = r with { Active = !r.Active }; SaveRules(); _core.Audit.Append(Principal.LocalOwner.Name, "post.regel", $"{r.Name} aktiv={!r.Active}"); Show("posteingang"); };
            var del = new Button { Content = "Entfernen" }; del.Click += (_, _) => { MailRules.RemoveAt(idx); SaveRules(); Show("posteingang"); };
            rules.Children.Add(Row(P($"{r.Name}: würde {Inbox.Count(r.Matches)} Nachricht(en) als „{r.Category}“ markieren", null, 12.5), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { tg, del } }));
        }
        sp.Children.Add(Card(rules));
        if (_pimMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "pim-msg", Text = _pimMsg, Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        return SysPage("Posteingang", sp);
    }

    // ---------- Kalender ----------
    Control CalendarView()
    {
        var sp = new StackPanel { Spacing = 8 };
        var nav = new WrapPanel();
        void Btn(string name, string label, Action a, bool on = false) { var b = new Button { Name = name, Content = label, Background = on ? Accent : null, Margin = new Thickness(0, 0, 4, 0) }; b.Click += (_, _) => { a(); Show("kalender"); }; nav.Children.Add(b); }
        Btn("cal-prev", "◀", () => CalDay = CalMode switch { "tag" => CalDay.AddDays(-1), "woche" => CalDay.AddDays(-7), _ => CalDay.AddMonths(-1) });
        Btn("cal-today", "Heute", () => CalDay = DateTime.Today);
        Btn("cal-next", "▶", () => CalDay = CalMode switch { "tag" => CalDay.AddDays(1), "woche" => CalDay.AddDays(7), _ => CalDay.AddMonths(1) });
        foreach (var (m, l) in new[] { ("tag", "Tag"), ("woche", "Woche"), ("monat", "Monat") }) { var mm = m; Btn("cal-mode-" + m, l, () => CalMode = mm, CalMode == m); }
        var title = CalMode switch { "tag" => CalDay.ToString("dddd, d. MMMM yyyy", De), "woche" => $"Woche {ISOWeek.GetWeekOfYear(CalDay)} · {Monday(CalDay):d. MMM} – {Monday(CalDay).AddDays(6):d. MMM yyyy}", _ => CalDay.ToString("MMMM yyyy", De) };
        sp.Children.Add(new DockPanel { Children = { nav, new TextBlock { Name = "cal-title", Text = title, Foreground = Text, FontSize = 18, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0) } } });
        sp.Children.Add(CalMode switch { "tag" => CalDayView(CalDay), "woche" => CalWeekView(), _ => CalMonthView() });
        sp.Children.Add(CalAddForm());
        if (_pimMsg.Length > 0) sp.Children.Add(new TextBlock { Text = _pimMsg, Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        return SysPage("Kalender", sp);
    }

    static DateTime Monday(DateTime d) => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    Control EventChip((CalEvent Event, DateTime Start, DateTime End) o, int i)
    {
        var e = o.Event;
        var b = new Button { Name = $"cal-ev-{i}", Padding = new Thickness(5, 2), Margin = new Thickness(0, 1), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brush.Parse(e.AllDay ? "#5510B981" : "#552F86F6"),
            Content = new TextBlock { Text = (e.AllDay ? "" : o.Start.ToString("HH:mm ", De)) + e.Title + (e.Repeat != Repeat.Keine ? " ↻" : ""), FontSize = 12, Foreground = Text, TextTrimming = TextTrimming.CharacterEllipsis } };
        ToolTip.SetTip(b, $"{e.Title}\n{(e.AllDay ? "ganztägig" : $"{o.Start:HH:mm}–{o.End:HH:mm}")}{(e.Location.Length > 0 ? "\n" + e.Location : "")}{(e.Notes.Length > 0 ? "\n" + e.Notes : "")}");
        var menu = new ContextMenu(); var del = new MenuItem { Header = "Termin löschen" }; del.Click += (_, _) => { Cal.Delete(e.Uid); _pimMsg = $"„{e.Title}“ gelöscht."; Show("kalender"); }; menu.Items.Add(del); b.ContextMenu = menu;
        b.Click += (_, _) => { CalDay = o.Start.Date; CalMode = "tag"; Show("kalender"); };
        return b;
    }

    Control CalMonthView()
    {
        var first = new DateTime(CalDay.Year, CalDay.Month, 1); var start = Monday(first);
        var occ = Cal.Occurrences(start, start.AddDays(42));
        var g = new Grid { Name = "cal-month", ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*,*,*"), RowDefinitions = new RowDefinitions("Auto,*,*,*,*,*,*") };
        var days = new[] { "Mo", "Di", "Mi", "Do", "Fr", "Sa", "So" };
        for (var c = 0; c < 7; c++) { var h = new TextBlock { Text = days[c], Foreground = Muted, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center }; Grid.SetColumn(h, c); g.Children.Add(h); }
        var k = 0;
        for (var i = 0; i < 42; i++)
        {
            var d = start.AddDays(i);
            var cell = new StackPanel { Spacing = 1, Children = { new TextBlock { Text = d.Day.ToString(De), FontSize = 12, FontWeight = d == DateTime.Today ? FontWeight.Bold : FontWeight.Normal, Foreground = d.Month == CalDay.Month ? Text : Muted } } };
            foreach (var o in occ.Where(o => o.Start.Date <= d && o.End > d).Take(3)) cell.Children.Add(EventChip(o, k++));
            var more = occ.Count(o => o.Start.Date <= d && o.End > d) - 3; if (more > 0) cell.Children.Add(new TextBlock { Text = $"+{more} weitere", FontSize = 11, Foreground = Muted });
            var border = new Border { Child = cell, MinHeight = 86, Padding = new Thickness(4), BorderBrush = Edge, BorderThickness = new Thickness(0.5), Background = d == DateTime.Today ? Brush.Parse("#222F86F6") : Brushes.Transparent };
            var day = d; border.PointerPressed += (_, ev) => { if (ev.ClickCount == 2) { CalDay = day; CalMode = "tag"; Show("kalender"); } };
            Grid.SetRow(border, 1 + i / 7); Grid.SetColumn(border, i % 7); g.Children.Add(border);
        }
        return g;
    }

    Control CalWeekView()
    {
        var mon = Monday(CalDay); var occ = Cal.Occurrences(mon, mon.AddDays(7));
        var g = new Grid { Name = "cal-week", ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*,*,*") }; var k = 0;
        for (var c = 0; c < 7; c++)
        {
            var d = mon.AddDays(c);
            var col = new StackPanel { Spacing = 2, Margin = new Thickness(2), Children = { new TextBlock { Text = d.ToString("ddd d.M.", De), Foreground = d == DateTime.Today ? Brush.Parse("#60A5FA") : Text, FontWeight = FontWeight.SemiBold } } };
            foreach (var o in occ.Where(o => o.Start.Date <= d && o.End > d)) col.Children.Add(EventChip(o, k++));
            var b = new Border { Child = col, MinHeight = 260, BorderBrush = Edge, BorderThickness = new Thickness(0.5), Padding = new Thickness(4) };
            Grid.SetColumn(b, c); g.Children.Add(b);
        }
        return g;
    }

    Control CalDayView(DateTime day)
    {
        var occ = Cal.Occurrences(day, day.AddDays(1)); var sp = new StackPanel { Name = "cal-day", Spacing = 3 }; var k = 0;
        if (occ.Count == 0) sp.Children.Add(EmptyState("cal-day-empty", "📅", "Keine Termine", "Unten einen Termin eintragen."));
        foreach (var o in occ) sp.Children.Add(Row(EventChip(o, k++), new TextBlock { Text = (o.Event.AllDay ? "ganztägig" : $"{o.Start:HH:mm}–{o.End:HH:mm}") + (o.Event.Location.Length > 0 ? " · " + o.Event.Location : "") + (o.Event.RemindMin >= 0 ? $" · Erinnerung {o.Event.RemindMin} min vorher" : ""), Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }));
        return sp;
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
            Cal.Add(new CalEvent(Guid.NewGuid().ToString("N"), start, end, t.Text!.Trim(), loc.Text?.Trim() ?? "", "", all, rmin, (Repeat)Math.Max(0, rep.SelectedIndex)));
            _pimMsg = $"„{t.Text!.Trim()}“ eingetragen."; CalDay = d; Show("kalender");
        };
        var ics = new Button { Name = "cal-export", Content = "Als ICS speichern" };
        ics.Click += (_, _) => { var f = Path.Combine(WorkFolder("kalender"), "kalender.ics"); Cal.ExportFile(f); _pimMsg = "Gespeichert: " + f; NotifyFileResult(_pimMsg); Show("kalender"); };
        var imp = new Button { Name = "cal-import", Content = "ICS einlesen …" };
        imp.Click += async (_, _) => { if (await PickMedia("cal-ics", ["*.ics"]) is { } f) { var (ne, nt) = Cal.ImportText(File.ReadAllText(f)); _pimMsg = $"{ne} Termine und {nt} Aufgaben eingelesen."; } Show("kalender"); };
        return Card(new StackPanel { Spacing = 6, Children = { H("Termin eintragen", 14), new WrapPanel { Children = { t, new Border { Width = 4 }, date, new Border { Width = 4 }, time, new Border { Width = 4 }, dur, new TextBlock { Text = " min ", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }, allDay } },
            new WrapPanel { Children = { loc, new Border { Width = 4 }, remind, new Border { Width = 4 }, rep, new Border { Width = 4 }, add, new Border { Width = 12 }, imp, new Border { Width = 4 }, ics } } } });
    }

    // ---------- Aufgaben ----------
    Control TodoView()
    {
        var sp = new StackPanel { Spacing = 8 };
        var title = new TextBox { Name = "todo-title", PlaceholderText = "Neue Aufgabe", Width = 280 };
        var due = new TextBox { Name = "todo-due", PlaceholderText = "fällig TT.MM.JJJJ", Width = 130 };
        var prio = new ComboBox { Name = "todo-prio", ItemsSource = new[] { "hoch", "normal", "niedrig" }, SelectedIndex = 1, Width = 100 };
        var list = new TextBox { Name = "todo-list", Text = "Allgemein", Width = 120 };
        var add = new Button { Name = "todo-add", Content = "Hinzufügen", Background = Accent };
        add.Click += (_, _) =>
        {
            if ((title.Text ?? "").Trim().Length == 0) { _pimMsg = "Bitte eine Aufgabe eintragen."; Show("aufgaben"); return; }
            DateTime? d = DateTime.TryParseExact((due.Text ?? "").Trim(), ["d.M.yyyy", "dd.MM.yyyy"], De, DateTimeStyles.None, out var dd) ? dd : null;
            Cal.AddTodo(new TodoItem(Guid.NewGuid().ToString("N"), title.Text!.Trim(), d, false, prio.SelectedIndex + 1, (list.Text ?? "").Trim() is { Length: > 0 } l ? l : "Allgemein"));
            _pimMsg = ""; Show("aufgaben");
        };
        title.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) add.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); };
        sp.Children.Add(Card(new WrapPanel { Children = { title, new Border { Width = 4 }, due, new Border { Width = 4 }, prio, new Border { Width = 4 }, list, new Border { Width = 4 }, add } }));
        var todos = Cal.Todos(); var i = 0;
        if (todos.Count == 0) sp.Children.Add(EmptyState("todo-empty", "✅", "Keine Aufgaben", "Oben eintragen – mit Fälligkeit und Priorität. Erledigte bleiben durchgestrichen sichtbar."));
        foreach (var grp in todos.GroupBy(t => t.List))
        {
            sp.Children.Add(H(grp.Key, 14));
            foreach (var t in grp)
            {
                var uid = t.Uid; var idx = i++;
                var cb = new CheckBox { Name = $"todo-done-{idx}", IsChecked = t.Done, Content = new TextBlock { Text = t.Title, Foreground = t.Done ? Muted : Text, TextDecorations = t.Done ? TextDecorations.Strikethrough : null } };
                cb.IsCheckedChanged += (_, _) => { Cal.SetDone(uid, cb.IsChecked == true); Show("aufgaben"); };
                var overdue = !t.Done && t.Due is { } dd && dd.Date < DateTime.Today;
                var info = new TextBlock { Text = (t.Priority == 1 ? "‼ " : "") + (t.Due is { } d ? $"fällig {d:dd.MM.yyyy}" : ""), Foreground = overdue ? Brush.Parse("#F87171") : Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) };
                var del = new Button { Name = $"todo-del-{idx}", Content = "✕", Padding = new Thickness(6, 0) }; del.Click += (_, _) => { Cal.DeleteTodo(uid); Show("aufgaben"); };
                sp.Children.Add(Row(new StackPanel { Orientation = Orientation.Horizontal, Children = { cb, info } }, del));
            }
        }
        if (_pimMsg.Length > 0) sp.Children.Add(new TextBlock { Text = _pimMsg, Foreground = Muted, FontSize = 13 });
        return SysPage("Aufgaben", sp);
    }
}
