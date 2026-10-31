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
            InboxOpen = await imap.MessageAsync(uid); InboxOpenUid = uid;
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
        var accountBox = new StackPanel { Spacing = 6, Children = { H("Konto", 14), new WrapPanel { Children = { email, new Border { Width = 4 }, name, new Border { Width = 4 }, pw, new Border { Width = 4 }, save, new Border { Width = 4 }, fetch } },
            P("Trendo liest nur – auf dem Server wird nichts gelöscht oder als gelesen markiert. Bei Gmail, iCloud und Yahoo brauchst du ein App-Passwort. Das Passwort wird geschützt gespeichert.", null, 12) } };
        if (a is null) sp.Children.Add(Card(accountBox));
        else sp.Children.Add(new Expander { Name = "inbox-account", Header = $"Konto: {a.Email}", IsExpanded = _inboxAccountOpen, Content = accountBox, HorizontalAlignment = HorizontalAlignment.Stretch });
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
        folders.Children.Add(Folder("inbox-folder-merker", $"⭐ Gemerkt ({Inbox.Count(h => _settings.MailFlagged.Contains(h.Uid))})", _inboxFolder == "\u0002", () => _inboxFolder = "\u0002"));
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
        var search = new TextBox { Name = "inbox-search", Text = _inboxSearch, PlaceholderText = "Suchen: Absender oder Betreff", Margin = new Thickness(0, 0, 0, 4) };
        search.KeyUp += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter || (search.Text ?? "").Length == 0) { _inboxSearch = search.Text ?? ""; Show("posteingang"); } };
        list.Children.Add(search);
        var shown = 0;
        for (var i = 0; i < Inbox.Count; i++)
        {
            var h = Inbox[i]; var uid = h.Uid;
            var cat = active.FirstOrDefault(r => r.Matches(h))?.Category;
            if (_inboxFolder == "\u0001" ? h.Seen : _inboxFolder == "\u0002" ? !_settings.MailFlagged.Contains(h.Uid) : _inboxFolder is not null && cat != _inboxFolder) continue;
            if (_inboxSearch.Trim() is { Length: > 0 } q && !(h.Subject.Contains(q, StringComparison.CurrentCultureIgnoreCase) || h.From.Contains(q, StringComparison.CurrentCultureIgnoreCase))) continue;
            shown++;
            var b = new Button { Name = $"inbox-item-{i}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 5), Background = InboxOpenUid == uid ? Accent : Brushes.Transparent,
                Content = new StackPanel { Children = {
                    new TextBlock { Text = (_settings.MailFlagged.Contains(uid) ? "⭐ " : "") + (h.Subject.Length > 0 ? h.Subject : "(ohne Betreff)"), Foreground = Text, FontWeight = h.Seen ? FontWeight.Normal : FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = $"{h.From} · {(h.Date is { } d ? d.LocalDateTime.ToString("dd.MM. HH:mm", De) : "")}{(cat is null ? "" : $"  [{cat}]")}", Foreground = Muted, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis } } } };
            b.Click += async (_, _) => { await OpenInboxMail(uid); InboxOpenUid = uid; _composeOpen = false; Show("posteingang"); };
            list.Children.Add(b);
        }
        if (Inbox.Count == 0) list.Children.Add(EmptyState("inbox-empty", "📬", "Noch keine Nachrichten geladen", "Konto eintragen und „Abrufen“ drücken."));
        else if (shown == 0) list.Children.Add(P(_inboxSearch.Length > 0 ? $"Nichts gefunden für „{_inboxSearch}“." : "In diesem Ordner ist nichts.", Text));

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
            var uidOpen = InboxOpenUid ?? 0; var flagged = _settings.MailFlagged.Contains(uidOpen);
            var flag = new Button { Name = "inbox-flag", Content = flagged ? "⭐ Gemerkt" : "☆ Merken" };
            flag.Click += (_, _) => { if (flagged) _settings.MailFlagged.Remove(uidOpen); else _settings.MailFlagged.Add(uidOpen); SaveSettings(); Show("posteingang"); };
            var fwd = new Button { Name = "inbox-forward", Content = "Weiterleiten" };
            fwd.Click += (_, _) => { _sendDraft = new MailDraft("", m.Subject.StartsWith("WG:", StringComparison.OrdinalIgnoreCase) ? m.Subject : "WG: " + m.Subject, $"\n\n---------- Weitergeleitete Nachricht ----------\nVon: {m.From}\nDatum: {m.Date}\nBetreff: {m.Subject}\n\n{m.Body}"); _composeOpen = true; Show("posteingang"); };
            var task = new Button { Name = "inbox-task", Content = "☑ Als Aufgabe" };
            task.Click += (_, _) =>
            {
                var due = MailDates.Find(m.Subject + "\n" + m.Body, reference).Select(x => (DateTime?)x.Start.Date).FirstOrDefault();
                var title = System.Text.RegularExpressions.Regex.Replace(m.Subject, @"^(re|aw|fw|wg):\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
                Cal.AddTodo(new TodoItem(Guid.NewGuid().ToString("N"), title.Length > 0 ? title : "E-Mail beantworten", due, false, 2, "E-Mail", Notes: $"Aus der E-Mail von {m.From}"));
                _pimMsg = $"„{title}“ steht in den Aufgaben{(due is { } dd ? $", fällig {dd:dd.MM.}" : "")}."; Show("posteingang");
            };
            read.Children.Add(new WrapPanel { Children = { reply, new Border { Width = 6 }, fwd, new Border { Width = 6 }, task, new Border { Width = 6 }, flag } });
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
        void Btn(string name, string label, Action a, bool on = false) { var b = new Button { Name = name, Content = label, Margin = new Thickness(0, 0, 4, 0) }; if (on) b.Background = Accent; /* ohne eigenen Hintergrund (null) wäre der Knopf nur auf der Schrift anklickbar */ b.Click += (_, _) => { a(); Show("kalender"); }; nav.Children.Add(b); }
        Btn("cal-prev", "◀", () => CalDay = CalMode switch { "tag" => CalDay.AddDays(-1), "woche" => CalDay.AddDays(-7), _ => CalDay.AddMonths(-1) });
        Btn("cal-today", "Heute", () => CalDay = DateTime.Today);
        Btn("cal-next", "▶", () => CalDay = CalMode switch { "tag" => CalDay.AddDays(1), "woche" => CalDay.AddDays(7), _ => CalDay.AddMonths(1) });
        foreach (var (m, l) in new[] { ("tag", "Tag"), ("woche", "Woche"), ("monat", "Monat") }) { var mm = m; Btn("cal-mode-" + m, l, () => CalMode = mm, CalMode == m); }
        Btn("cal-undo", "↶ Rückgängig", CalUndo);
        if (_calUndo.Count == 0) ((Button)nav.Children[^1]).IsEnabled = false;
        var title = CalMode switch { "tag" => CalDay.ToString("dddd, d. MMMM yyyy", De), "woche" => $"Woche {ISOWeek.GetWeekOfYear(CalDay)} · {Monday(CalDay):d. MMM} – {Monday(CalDay).AddDays(6):d. MMM yyyy}", _ => CalDay.ToString("MMMM yyyy", De) };
        sp.Children.Add(new DockPanel { Children = { nav, new TextBlock { Name = "cal-title", Text = title, Foreground = Text, FontSize = 18, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0) } } });
        if (_pimMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "cal-msg", Text = _pimMsg, Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        var view = CalMode switch { "tag" => CalDayView(CalDay), "woche" => CalWeekView(), _ => CalMonthView() };
        var side = new StackPanel { Spacing = 8, Children = { CalSelected is { } uid && Cal.Get(uid) is { } sel ? CalEditPanel(sel) : CalAddForm() } };
        if (SuggestionCard("kalender", CalendarSuggestions()) is { } sugg) side.Children.Insert(0, Card(sugg));
        var split = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,330") };
        split.Children.Add(view); Grid.SetColumn(side, 2); split.Children.Add(side);
        sp.Children.Add(split);
        sp.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Z && e.KeyModifiers == Avalonia.Input.KeyModifiers.Control) { CalUndo(); Show("kalender"); e.Handled = true; } }; // Strg+Z
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
        for (var c = 0; c < 7; c++) { var h = new TextBlock { Text = days[c], Foreground = Muted, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center }; Grid.SetColumn(h, c); g.Children.Add(h); }
        var k = 0;
        for (var i = 0; i < 42; i++)
        {
            var d = start.AddDays(i);
            var cell = new StackPanel { Spacing = 1, Children = { new TextBlock { Text = d.Day.ToString(De), FontSize = 12, FontWeight = d == DateTime.Today ? FontWeight.Bold : FontWeight.Normal, Foreground = d.Month == CalDay.Month ? Text : Muted } } };
            foreach (var o in occ.Where(o => o.Start.Date <= d && o.End > d).Take(3)) cell.Children.Add(EventChip(o, k++));
            var more = occ.Count(o => o.Start.Date <= d && o.End > d) - 3; if (more > 0) cell.Children.Add(new TextBlock { Text = $"+{more} weitere", FontSize = 11, Foreground = Muted });
            var border = new Border { Name = $"cal-cell-{i}", Tag = d, Child = cell, MinHeight = 86, Padding = new Thickness(4), BorderBrush = Edge, BorderThickness = new Thickness(0.5), Background = d == DateTime.Today ? Brush.Parse("#222F86F6") : Brushes.Transparent };
            var day = d; border.PointerPressed += (_, ev) => { if (ev.ClickCount == 2) { CalDay = day; CalMode = "tag"; Show("kalender"); } };
            Grid.SetRow(border, 1 + i / 7); Grid.SetColumn(border, i % 7); g.Children.Add(border);
        }
        return g;
    }

    // ---------- Zeitachse (Woche, Tag): ziehen, Dauer ändern, neu anlegen ----------
    public string? CalSelected { get; set; }
    private bool _calDragged;
    private readonly List<(string Uid, CalEvent? Before)> _calUndo = [];
    public CalTimeGrid? CalGridForTests { get; private set; }

    /// <summary>Kalender-Änderung mit Rückgängig: vorher merken, ändern, melden.</summary>
    void CalChange(string uid, Func<CalEvent> change, string msg)
    {
        var before = Cal.Get(uid);
        try { var n = change(); _calUndo.Add((n.Uid, before)); if (_calUndo.Count > 50) _calUndo.RemoveAt(0); CalSelected = n.Uid; _pimMsg = msg; }
        catch (InvalidOperationException ex) { _pimMsg = ex.Message; }
        Show("kalender");
    }

    public void CalUndo()
    {
        if (_calUndo.Count == 0) { _pimMsg = "Es gibt nichts rückgängig zu machen."; return; }
        var (uid, before) = _calUndo[^1]; _calUndo.RemoveAt(_calUndo.Count - 1);
        if (before is null) { Cal.Delete(uid); CalSelected = null; } else { Cal.Add(before); CalSelected = uid; }
        _pimMsg = "Rückgängig gemacht.";
    }

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
        scroll.AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => scroll.Offset = new Vector(0, startHour * grid.HourHeight));
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
        del.Click += (_, _) => { var before = Cal.Get(e.Uid); Cal.Delete(e.Uid); if (before is not null) _calUndo.Add((e.Uid, before)); CalSelected = null; _pimMsg = $"„{e.Title}“ gelöscht – Rückgängig holt ihn zurück."; Show("kalender"); };
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

    // ---------- Aufgaben: Tafel (Offen · In Arbeit · Erledigt) ----------
    public string? TodoSelected { get; set; }
    public string TodoListFilter { get; set; } = "";
    private readonly List<TodoItem?> _todoUndoBefore = [];
    private readonly List<string> _todoUndoUid = [];
    static readonly string[] Stages = ["Offen", "In Arbeit", "Erledigt"];

    void TodoChange(string uid, Func<TodoItem?> change, string msg)
    {
        var before = Cal.GetTodo(uid);
        try { change(); _todoUndoUid.Add(uid); _todoUndoBefore.Add(before); _pimMsg = msg; }
        catch (InvalidOperationException ex) { _pimMsg = ex.Message; }
        Show("aufgaben");
    }

    public void TodoUndo()
    {
        if (_todoUndoUid.Count == 0) { _pimMsg = "Es gibt nichts rückgängig zu machen."; return; }
        var uid = _todoUndoUid[^1]; var before = _todoUndoBefore[^1]; _todoUndoUid.RemoveAt(_todoUndoUid.Count - 1); _todoUndoBefore.RemoveAt(_todoUndoBefore.Count - 1);
        if (before is null) Cal.DeleteTodo(uid); else Cal.AddTodo(before);
        _pimMsg = "Rückgängig gemacht.";
    }

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
        var undo = new Button { Name = "todo-undo", Content = "↶ Rückgängig", IsEnabled = _todoUndoUid.Count > 0 };
        undo.Click += (_, _) => { TodoUndo(); Show("aufgaben"); };
        sp.Children.Add(Card(new WrapPanel { Children = { title, new Border { Width = 4 }, due, new Border { Width = 4 }, prio, new Border { Width = 4 }, list, new Border { Width = 4 }, add, new Border { Width = 12 }, undo } }));

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
        if (Cal.Todos().Count == 0) sp.Children.Add(EmptyState("todo-empty", "✅", "Keine Aufgaben", "Oben eintragen – mit Fälligkeit und Priorität. Karten zieht man zwischen Offen, In Arbeit und Erledigt."));
        var side = TodoSelected is { } su && Cal.GetTodo(su) is { } sel ? TodoEditPanel(sel) : null;
        var split = new Grid { ColumnDefinitions = new ColumnDefinitions(side is null ? "*" : "*,12,320") };
        split.Children.Add(board); if (side is not null) { Grid.SetColumn(side, 2); split.Children.Add(side); }
        sp.Children.Add(split);
        sp.Children.Add(new TextBlock { Text = "Karte ziehen: in eine andere Spalte setzen · Klick: bearbeiten · Häkchen: erledigt", Foreground = Muted, FontSize = 11.5 });
        sp.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Z && e.KeyModifiers == Avalonia.Input.KeyModifiers.Control) { TodoUndo(); Show("aufgaben"); e.Handled = true; } };
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
            Background = Brush.Parse(TodoSelected == uid ? "#332F86F6" : "#14FFFFFF"), BorderBrush = Brush.Parse(TodoSelected == uid ? "#FACC15" : overdue ? "#88EF4444" : "#22FFFFFF"), BorderThickness = new Thickness(TodoSelected == uid ? 1.5 : 1),
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
