using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Trendo.Core;
using Trendo.Core.Sys;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0689: Ansicht des Mailprogramms – drei Spalten (Ordner · Liste · Nachricht), Schreibfenster, Konto-Assistent.
/// Genau eine goldene Hauptaktion je Zustand: „Konto einrichten“ (leer), „Neue E-Mail“ (lesen), „Senden …“ (schreiben), „Ja, jetzt senden“ (Rückfrage).</summary>
public partial class MainWindow
{
    static readonly IBrush MailPick = Brush.Parse("#33C9A227");

    void MailRefresh() { if (CurrentView == "posteingang") Show("posteingang"); }

    Button MailBtn(string name, string label, Func<Task> run, bool gold = false, string? tip = null)
    {
        var b = new Button { Name = name, Content = label, FontSize = 12.5, Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(8), Foreground = Text, Background = gold ? Accent : Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(gold ? 0 : 1), Margin = new Thickness(0, 0, 6, 6) };
        if (tip is not null) ToolTip.SetTip(b, tip);
        b.Click += async (_, _) => { b.IsEnabled = false; try { await run(); } finally { b.IsEnabled = true; } MailRefresh(); };
        return b;
    }
    Button MailBtn(string name, string label, Action run, bool gold = false, string? tip = null) => MailBtn(name, label, () => { run(); return Task.CompletedTask; }, gold, tip);

    // ---------- Konto-Assistent ----------
    Control MailWizard(MailAccount? a)
    {
        var email = new TextBox { Name = "inbox-email", Text = a?.Email ?? "", PlaceholderText = "E-Mail-Adresse, z. B. erika.muster@example.org", Width = 320 };
        var name = new TextBox { Name = "inbox-name", Text = a?.Name ?? "", PlaceholderText = "Dein Name (so steht er beim Empfänger)", Width = 320 };
        var pw = new TextBox { Name = "inbox-password", PasswordChar = '•', Text = _pwDraft, PlaceholderText = a is not null && _core.Secrets.Get(a.SecretKey) is not null ? "Passwort gespeichert – nur zum Ändern neu eintragen" : "Passwort oder App-Passwort", Width = 320 };
        pw.TextChanged += (_, _) => _pwDraft = pw.Text ?? "";
        var imap = new TextBox { Name = "inbox-imap", Text = _wizImap, PlaceholderText = "Server zum Abrufen (IMAP)", Width = 260 };
        var smtp = new TextBox { Name = "inbox-smtp", Text = _wizSmtp, PlaceholderText = "Server zum Senden (SMTP)", Width = 260 };
        var port = new TextBox { Name = "inbox-smtp-port", Text = _wizPort, PlaceholderText = "Port: 465 oder 587", Width = 150 };
        imap.TextChanged += (_, _) => _wizImap = imap.Text ?? ""; smtp.TextChanged += (_, _) => _wizSmtp = smtp.Text ?? ""; port.TextChanged += (_, _) => _wizPort = port.Text ?? "";
        var info = new TextBlock { Name = "inbox-provider", Foreground = Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, MaxWidth = 640 };
        var infoBox = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 8), Child = info, HorizontalAlignment = HorizontalAlignment.Left };
        void Info()
        {
            var mail = (email.Text ?? "").Trim();
            if (!MailCenter.ValidEmail(mail)) { info.Text = "Sobald die Adresse vollständig ist, zeigt Trendo hier die passenden Einstellungen."; infoBox.Background = Brush.Parse("#14FFFFFF"); return; }
            var g = MailAccount.Guess(mail);
            if (MailProviders.For(mail) is { } p)
            {
                info.Text = $"{p.Name}: {(p.Auth == MailAuth.OAuthOnly ? "BLOCKIERT – " : "")}{p.State}.\nAbrufen über {p.ImapHost} (Port {p.ImapPort}, verschlüsselt), Senden über {p.SmtpHost} (Port {p.SmtpPort}, verschlüsselt).\n{p.Hint}";
                infoBox.Background = Brush.Parse(p.Auth == MailAuth.OAuthOnly ? "#33F59E0B" : "#22C9A227");
            }
            else { info.Text = $"Diesen Anbieter kennt Trendo nicht. Versucht wird {g.ImapHost} (Abrufen) und {g.SmtpHost} (Senden, Port {g.SmtpPort}). Stimmt das nicht, trage die Server unter „Server von Hand eintragen“ ein – sie stehen in der Hilfe deines Anbieters."; infoBox.Background = Brush.Parse("#14FFFFFF"); }
        }
        email.TextChanged += (_, _) => Info(); Info();
        foreach (var tb in new[] { email, name, pw }) tb.HorizontalAlignment = HorizontalAlignment.Left;
        var save = MailBtn("inbox-save", a is null ? "Konto einrichten" : "Speichern und verbinden", async () =>
        {
            try
            {
                _pwDraft = pw.Text is { Length: > 0 } typed ? typed : _pwDraft; _wizImap = imap.Text ?? ""; _wizSmtp = smtp.Text ?? ""; _wizPort = port.Text ?? ""; // Felder direkt lesen (Textänderungen kommen verzögert)
                int? prt = int.TryParse(_wizPort.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pn) ? pn : null;
                if (prt is not null and not (465 or 587)) { _pimMsg = "Zum Senden geht nur Port 465 oder 587 – beide sind verschlüsselt. Port 25 nutzt Trendo nicht."; return; }
                SetMailAccount(email.Text ?? "", name.Text ?? "", _pwDraft, _wizImap, _wizSmtp, prt); _pwDraft = "";
            }
            catch (ArgumentException ex) { _pimMsg = ex.Message; return; }
            if (await MailTestConnection()) { _mailWizard = false; _wizImap = _wizSmtp = _wizPort = ""; await FetchInbox(); }
            else if (MailProviders.For(MailAccountCfg!.Email) is { } p) _pimMsg += " Hinweis zu " + p.Name + ": " + p.Hint;
        }, gold: true);
        var table = new Grid { Name = "inbox-provider-table", ColumnDefinitions = new ColumnDefinitions("170,190,230,*") };
        void Cell(int r, int c, string t, bool head = false) { var tb = new TextBlock { Text = t, Foreground = head ? Muted : Text, FontSize = 11.5, Margin = new Thickness(0, 2, 10, 2), TextWrapping = TextWrapping.Wrap }; Grid.SetRow(tb, r); Grid.SetColumn(tb, c); table.Children.Add(tb); }
        table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Cell(0, 0, "Anbieter", true); Cell(0, 1, "Abrufen (IMAP, Port 993)", true); Cell(0, 2, "Senden (SMTP)", true); Cell(0, 3, "Anmeldung", true);
        for (var i = 0; i < MailProviders.All.Count; i++)
        {
            var p = MailProviders.All[i]; table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Cell(i + 1, 0, p.Name); Cell(i + 1, 1, p.ImapHost); Cell(i + 1, 2, $"{p.SmtpHost} · Port {p.SmtpPort}"); Cell(i + 1, 3, p.State);
        }
        var box = new StackPanel { Spacing = 8, Children = {
            H(a is null ? "E-Mail-Konto einrichten" : "Konto", 16),
            P("Adresse und Passwort eintragen – die Einstellungen der bekannten Anbieter kennt Trendo schon. Die Verbindung ist immer verschlüsselt; das Passwort liegt nur im geschützten Speicher dieses PCs, nie in einer Datei.", null, 12.5),
            email, name, pw, infoBox,
            new Expander { Name = "inbox-servers", Header = "Server von Hand eintragen", IsExpanded = (_wizImap + _wizSmtp + _wizPort).Length > 0, Content = new WrapPanel { Children = { MSp(imap), MSp(smtp), MSp(port) } } },
            new Expander { Name = "inbox-providers", Header = "Bekannte Anbieter und ihre Einstellungen", Content = table },
        } };
        var row = new WrapPanel { Children = { save } };
        if (a is not null) row.Children.Add(MailBtn("inbox-wizard-close", "Schließen", () => { _mailWizard = false; }));
        box.Children.Add(row);
        return Card(box);
    }

    // ---------- Posteingang ----------
    Control InboxView()
    {
        var sp = new StackPanel { Spacing = 8 };
        var a = MailAccountCfg;
        _composeSync = null;
        var composing = _composeOpen || SendConfirmOpen;
        if (a is null && Inbox.Count == 0 && !composing)
        {
            // Leerzustand: der erste Schritt ist das Konto
            sp.Children.Add(MailWizard(null));
            if (_pimMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "pim-msg", Text = _pimMsg, Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap });
            return SysPage("Posteingang", sp);
        }
        if (_mailWizard) sp.Children.Add(MailWizard(a));

        // Leiste: eine goldene Hauptaktion
        var compose = new Button { Name = "inbox-new", Content = "Neue E-Mail", FontSize = 12.5, Padding = new Thickness(14, 6), CornerRadius = new CornerRadius(8), Foreground = Text, Background = composing ? Brushes.Transparent : Accent, BorderBrush = Edge, BorderThickness = new Thickness(composing ? 1 : 0), Margin = new Thickness(0, 0, 6, 6) };
        ToolTip.SetTip(compose, "Neue E-Mail schreiben (Strg+N)");
        compose.Click += (_, _) => MailCompose(new("", "", ""));
        var fetch = MailBtn("inbox-fetch", MailBusy ? "Wird abgerufen …" : "Abrufen", async () => { await FetchInbox(); }, tip: "Neue Nachrichten holen");
        fetch.IsEnabled = a is not null && !MailBusy;
        var bar = new WrapPanel { Name = "inbox-bar", Children = { compose, fetch,
            MailBtn("inbox-account-open", a is null ? "Konto einrichten" : "Konto", () => { _mailWizard = !_mailWizard; }, tip: a?.Email),
            MailBtn("inbox-more", _mailMoreOpen ? "Regeln ausblenden" : "Regeln", () => { _mailMoreOpen = !_mailMoreOpen; }, tip: "Sortier-Regeln für die Ansicht") } };
        sp.Children.Add(bar);

        // Spalte 1: Ordner
        var active = MailRules.Where(r => r.Active).ToList();
        var folders = new StackPanel { Name = "inbox-folders", Spacing = 2 };
        Button Folder(string name, string label, bool on, Action go)
        {
            var b = new Button { Name = name, Content = label, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(6),
                Background = on ? MailPick : Brushes.Transparent, Foreground = Text, FontSize = 12.5, FontWeight = on ? FontWeight.SemiBold : FontWeight.Normal };
            b.Click += (_, _) => { MailDraftFlush(); _composeOpen = false; SendConfirmOpen = false; go(); Show("posteingang"); }; return b;
        }
        var local = _inboxFolder is "\u0003" or "\u0004";
        var unread = Inbox.Count(h => !h.Seen);
        if (MailServerFolders.Count == 0)
            folders.Children.Add(Folder("inbox-folder-alle", $"Posteingang ({Inbox.Count})", _inboxFolder is null, () => _inboxFolder = null));
        else
            for (var i = 0; i < MailServerFolders.Count; i++)
            {
                var f = MailServerFolders[i]; if (!f.Selectable) continue;
                var here = f.Path == MailFolder;
                var b = Folder(f.Role == "inbox" ? "inbox-folder-alle" : $"inbox-srv-{i}", here ? $"{f.Label} ({(MailExists > 0 ? MailExists : Inbox.Count)})" : f.Label, here && _inboxFolder is null, () => { });
                if (!here) b.Click += async (_, _) => await MailOpenFolder(f.Path); else b.Click += (_, _) => { _inboxFolder = null; _sentOpen = null; Show("posteingang"); };
                // Nachricht auf einen Ordner ziehen = verschieben
                DragDrop.SetAllowDrop(b, true);
                b.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = e.DataTransfer.TryGetText()?.StartsWith("trendo-mail:", StringComparison.Ordinal) == true ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; });
                b.AddHandler(DragDrop.DropEvent, async (_, e) => { if (e.DataTransfer.TryGetText() is { } t && t.StartsWith("trendo-mail:", StringComparison.Ordinal) && long.TryParse(t[12..], out var du)) { e.Handled = true; await MailMove([du], f.Path); MailRefresh(); } });
                folders.Children.Add(b);
            }
        folders.Children.Add(new Border { Height = 1, Background = Edge, Margin = new Thickness(4, 6) });
        folders.Children.Add(Folder("inbox-folder-ungelesen", $"Ungelesen ({unread})", _inboxFolder == "\u0001", () => _inboxFolder = "\u0001"));
        folders.Children.Add(Folder("inbox-folder-merker", $"Gemerkt ({Inbox.Count(MailIsFlagged)})", _inboxFolder == "\u0002", () => _inboxFolder = "\u0002"));
        foreach (var (cat, k) in active.Select(r => r.Category).Distinct().Select((c, k) => (c, k)))
            folders.Children.Add(Folder($"inbox-folder-{k}", $"{cat} ({Inbox.Count(h => active.FirstOrDefault(r => r.Matches(h))?.Category == cat)})", _inboxFolder == cat, () => _inboxFolder = cat));
        folders.Children.Add(new Border { Height = 1, Background = Edge, Margin = new Thickness(4, 6) });
        var onDevice = MailServerFolders.Count > 0 ? " · dieses Gerät" : "";
        folders.Children.Add(Folder("inbox-folder-entwuerfe", $"Entwürfe{onDevice} ({MailBoxes.Drafts.Count})", _inboxFolder == "\u0003", () => { _inboxFolder = "\u0003"; _sentOpen = null; }));
        folders.Children.Add(Folder("inbox-folder-gesendet", $"Gesendet{onDevice} ({MailBoxes.Sent.Count})", _inboxFolder == "\u0004", () => { _inboxFolder = "\u0004"; _sentOpen = null; }));
        var threads = new CheckBox { Name = "inbox-threads", Content = "Als Unterhaltungen", IsChecked = _inboxThreads, Foreground = Text, FontSize = 12, Margin = new Thickness(4, 6, 0, 0) };
        threads.IsCheckedChanged += (_, _) => { _inboxThreads = threads.IsChecked == true; Show("posteingang"); };
        folders.Children.Add(threads);
        folders.Children.Add(new Border { Height = 6 });
        folders.Children.Add(Folder("inbox-go-kalender", "Kalender →", false, () => { }));
        ((Button)folders.Children[^1]).Click += (_, _) => Show("kalender");
        folders.Children.Add(Folder("inbox-go-aufgaben", "Aufgaben →", false, () => { }));
        ((Button)folders.Children[^1]).Click += (_, _) => Show("aufgaben");

        // Spalte 2: Liste
        var list = new StackPanel { Name = "inbox-list", Spacing = 2 };
        var search = new TextBox { Name = "inbox-search", Text = _inboxSearch, PlaceholderText = "Suchen: Absender, Betreff, Text", Margin = new Thickness(0, 0, 0, 4) };
        search.KeyUp += (_, e) => { if (e.Key == Key.Enter || (search.Text ?? "").Length == 0) { _inboxSearch = search.Text ?? ""; _mailServerHits = []; Show("posteingang"); } };
        list.Children.Add(search);
        var shown = 0;
        var localList = _inboxFolder == "\u0003" ? MailBoxes.Drafts : _inboxFolder == "\u0004" ? MailBoxes.Sent : null;
        if (localList is not null)
        {
            for (var i = 0; i < localList.Count; i++)
            {
                var st = localList[i]; var isDraft = _inboxFolder == "\u0003";
                if (_inboxSearch.Trim() is { Length: > 0 } lq && !(st.Subject.Contains(lq, StringComparison.CurrentCultureIgnoreCase) || st.To.Contains(lq, StringComparison.CurrentCultureIgnoreCase) || st.Body.Contains(lq, StringComparison.CurrentCultureIgnoreCase))) continue;
                shown++;
                var lb = new Button { Name = $"inbox-local-{i}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 5), Background = _sentOpen?.Id == st.Id ? MailPick : Brushes.Transparent,
                    Content = new StackPanel { Children = {
                        new TextBlock { Text = (st.Attachments.Count > 0 ? "📎 " : "") + (st.Subject.Length > 0 ? st.Subject : "(ohne Betreff)"), Foreground = Text, TextTrimming = TextTrimming.CharacterEllipsis },
                        new TextBlock { Text = $"An {(st.To.Length > 0 ? st.To : "–")} · {st.At.LocalDateTime.ToString("dd.MM. HH:mm", De)}", Foreground = Muted, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis } } } };
                lb.Click += (_, _) => { if (isDraft) { MailDraftFlush(); _draftId = st.Id; MailCompose(st.ToDraft(), keepDraft: true); } else { _sentOpen = st; _composeOpen = false; InboxOpen = null; Show("posteingang"); } };
                if (isDraft) WithContext(lb, ContextMenuOf("inbox-localctx", [("oeffnen", "Weiterschreiben", () => { MailDraftFlush(); _draftId = st.Id; MailCompose(st.ToDraft(), keepDraft: true); }), ("loeschen", "Entwurf löschen", () => { MailBoxes.DeleteDraft(st.Id); _pimMsg = "Entwurf gelöscht."; MailRefresh(); })]));
                list.Children.Add(lb);
            }
        }
        var threadOf = _inboxThreads && localList is null ? MailFolders.Threads(Inbox).ToDictionary(t => t.Mails[0].Uid, t => t.Mails.Count) : null;
        var query = _inboxSearch.Trim();
        var bodyHits = query.Length > 0 && localList is null && MailCache is { } cache ? cache.Search(MailFolder, query).Select(h => h.Uid).ToHashSet() : null;
        var movable = MailServerFolders.Where(f => f.Selectable && f.Path != MailFolder).Take(12).ToList();
        for (var i = 0; i < Inbox.Count && localList is null; i++)
        {
            var h = Inbox[i]; var uid = h.Uid;
            if (threadOf is not null && !threadOf.ContainsKey(uid)) continue; // Unterhaltung: nur die neueste zeigen
            var cat = active.FirstOrDefault(r => r.Matches(h))?.Category;
            if (_inboxFolder == "\u0001" ? h.Seen : _inboxFolder == "\u0002" ? !MailIsFlagged(h) : _inboxFolder is not null && cat != _inboxFolder) continue;
            if (query.Length > 0 && !MailMatches(h, query, bodyHits)) continue;
            shown++;
            var who = MailRole("sent")?.Path == MailFolder || MailRole("drafts")?.Path == MailFolder ? "An " + (h.To.Length > 0 ? h.To : "–") : h.From;
            var b = new Button { Name = $"inbox-item-{i}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 5), Background = InboxOpenUid == uid ? MailPick : Brushes.Transparent,
                Content = new StackPanel { Children = {
                    new TextBlock { Text = (MailIsFlagged(h) ? "★ " : "") + (h.Subject.Length > 0 ? h.Subject : "(ohne Betreff)") + (threadOf?.GetValueOrDefault(uid) is > 1 and var tc ? $"  ({tc})" : ""), Foreground = Text, FontWeight = h.Seen ? FontWeight.Normal : FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = $"{(h.HasAttachment ? "📎 " : "")}{who} · {(h.Date is { } d ? d.LocalDateTime.ToString(d.LocalDateTime.Date == DateTime.Today ? "HH:mm" : "dd.MM. HH:mm", De) : "")}{(cat is null ? "" : $"  [{cat}]")}", Foreground = Muted, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis } } } };
            b.Click += async (_, _) =>
            {
                await OpenInboxMail(uid); InboxOpenUid = uid; _composeOpen = false; _sentOpen = null; Show("posteingang");
                if (!h.Seen && InboxOpen is not null) { await MailSetSeen([uid], true); MailRefresh(); } // wie in Mailprogrammen: geöffnet = gelesen
            };
            // Rechtsklick: dieselben Aktionen wie im Lesebereich
            IEnumerable<(string, string, Action)> moves = movable.Select((f, k) => ($"nach-{k}", $"Nach „{f.Label}“ verschieben", (Action)(async () => { await MailMove([uid], f.Path); MailRefresh(); })));
            WithContext(b, ContextMenuOf("inbox-ctx",
                [("oeffnen", "Öffnen", async () => { await OpenInboxMail(uid); _composeOpen = false; MailRefresh(); }),
                 ("antworten", "Antworten", async () => { await OpenInboxMail(uid); if (InboxOpen is { } om) MailCompose(MailReplyDraft(om, false)); }),
                 ("weiterleiten", "Weiterleiten", async () => { await OpenInboxMail(uid); if (InboxOpen is { } om) MailCompose(MailForwardDraft(om)); })],
                [("gelesen", h.Seen ? "Als ungelesen markieren" : "Als gelesen markieren", async () => { await MailSetSeen([uid], !h.Seen); MailRefresh(); }),
                 ("merken", MailIsFlagged(h) ? "Nicht mehr merken" : "Merken", async () => { await MailSetFlag(uid, !MailIsFlagged(h)); MailRefresh(); })],
                moves,
                [("loeschen", MailRole("trash")?.Path == MailFolder ? "Endgültig löschen …" : "Löschen (in den Papierkorb)", async () => { await MailDelete([uid]); MailRefresh(); })]));
            // Ziehen auf einen Ordner
            PointerPressedEventArgs? pressed = null; Point start = default;
            b.AddHandler(PointerPressedEvent, (_, e) => { if (e.GetCurrentPoint(b).Properties.IsLeftButtonPressed) { pressed = e; start = e.GetPosition(b); } }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            b.AddHandler(PointerReleasedEvent, (_, _) => pressed = null, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            b.AddHandler(PointerMovedEvent, async (_, e) =>
            {
                if (pressed is not { } pe || movable.Count == 0 || (Math.Abs(e.GetPosition(b).X - start.X) < 6 && Math.Abs(e.GetPosition(b).Y - start.Y) < 6)) return;
                pressed = null; var data = new DataTransfer(); data.Add(DataTransferItem.CreateText("trendo-mail:" + uid.ToString(CultureInfo.InvariantCulture)));
                try { await DragDrop.DoDragDropAsync(pe, data, DragDropEffects.Move); } catch (InvalidOperationException) { }
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            list.Children.Add(b);
        }
        if (localList is null && Inbox.Count == 0)
            list.Children.Add(EmptyState("inbox-empty", "📬", a is null ? "Noch kein Konto" : "Hier ist noch nichts", a is null ? "Oben „Konto einrichten“ wählen." : "„Abrufen“ holt die Nachrichten dieses Ordners."));
        else if (shown == 0) list.Children.Add(P(_inboxSearch.Length > 0 ? $"Nichts gefunden für „{_inboxSearch}“." : "In diesem Ordner ist nichts.", Text));
        var listBox = new StackPanel { Spacing = 6, Children = { list } };
        if (localList is null && a is not null)
        {
            var under = new WrapPanel { Name = "inbox-list-more" };
            if (MailExists > Inbox.Count && query.Length == 0)
                under.Children.Add(MailBtn("inbox-older", $"Ältere laden ({Inbox.Count} von {MailExists})", async () => { await MailLoadMore(); }));
            if (query.Length > 0)
                under.Children.Add(MailBtn("inbox-search-server", "Auch auf dem Server suchen", async () => { await MailSearchServer(query); }, tip: "Durchsucht alle Nachrichten dieses Ordners im Postfach, auch die noch nicht geladenen"));
            if (under.Children.Count > 0) listBox.Children.Add(under);
        }

        // Spalte 3: Nachricht oder Schreibfenster
        var read = new StackPanel { Spacing = 6 };
        if (composing) MailComposePane(read, a);
        else if (_sentOpen is { } so)
        {
            read.Children.Add(new TextBlock { Name = "inbox-subject", Text = so.Subject, Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            read.Children.Add(P($"Gesendet an {so.To}{(so.Cc.Length > 0 ? $", Kopie an {so.Cc}" : "")}{(so.Bcc.Length > 0 ? $", Blindkopie an {so.Bcc}" : "")} · {so.At.LocalDateTime.ToString("dd.MM.yyyy HH:mm", De)}", null, 12));
            read.Children.Add(new SelectableTextBlock { Name = "inbox-body", Text = so.Body, Foreground = Text, TextWrapping = TextWrapping.Wrap, FontSize = 13 });
            if (so.Attachments.Count > 0) read.Children.Add(P("Anhänge: " + string.Join(", ", so.Attachments.Select(Path.GetFileName)), null, 12));
            read.Children.Add(Act("inbox-resend", "Als neue Nachricht verwenden", () => MailCompose(so.ToDraft())));
        }
        else if (InboxOpen is { } m) MailReadPane(read, m, a, movable);
        else read.Children.Add(EmptyState("inbox-read-empty", "✉", "Keine Nachricht ausgewählt", "Links eine Nachricht anklicken oder oben „Neue E-Mail“ wählen."));

        var readCard = Card(new ScrollViewer { Content = read, MaxHeight = 640 });
        // Datei auf das Schreibfenster ziehen = Anhang (auf den Lesebereich: neue E-Mail mit Anhang)
        DragDrop.SetAllowDrop(readCard, true);
        readCard.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; });
        readCard.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            var paths = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().Where(File.Exists).ToList() ?? [];
            if (paths.Count > 0) { MailAttach(paths); e.Handled = true; }
        });
        var grid = new Grid { Name = "inbox-columns", ColumnDefinitions = new ColumnDefinitions("215,8,380,8,*"), Children = { Card(folders) } };
        if (composing) { Grid.SetColumn(readCard, 2); Grid.SetColumnSpan(readCard, 3); grid.Children.Add(readCard); }
        else { grid.Children.Add(Pos(Card(new ScrollViewer { Content = listBox, MaxHeight = 640 }), 2)); grid.Children.Add(Pos(readCard, 4)); }
        sp.Children.Add(grid);

        // O6 Regelvorschläge (hinter „Regeln“, außer es gibt schon welche)
        if (_mailMoreOpen || MailRules.Count > 0)
        {
            var rules = new StackPanel { Name = "inbox-rules", Spacing = 4, Children = { H("Sortier-Regeln", 14), P("Regeln sortieren nur in Trendos Ansicht, nie auf dem Server – und erst, wenn du sie freigibst.", null, 12) } };
            var suggestBtn = new Button { Name = "inbox-suggest", Content = "Vorschläge aus diesem Ordner", IsEnabled = Inbox.Count > 0 };
            suggestBtn.Click += (_, _) => { foreach (var r in MailRule.Suggest(Inbox)) if (!MailRules.Any(x => x.FromContains == r.FromContains)) MailRules.Add(r); SaveRules(); Show("posteingang"); };
            rules.Children.Add(suggestBtn);
            for (var i = 0; i < MailRules.Count; i++)
            {
                var r = MailRules[i]; var idx = i;
                var tg = new Button { Name = $"inbox-rule-{i}", Content = r.Active ? "Aktiv – ausschalten" : "Freigeben" };
                tg.Click += (_, _) => { MailRules[idx] = r with { Active = !r.Active }; SaveRules(); _core.Audit.Append(Principal.LocalOwner.Name, "post.regel", $"{r.Name} aktiv={!r.Active}"); Show("posteingang"); };
                var del = new Button { Content = "Entfernen" }; del.Click += (_, _) => { MailRules.RemoveAt(idx); SaveRules(); Show("posteingang"); };
                rules.Children.Add(Row(P($"{r.Name}: würde {Inbox.Count(r.Matches)} Nachricht(en) als „{r.Category}“ markieren", null, 12.5), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { tg, del } }));
            }
            sp.Children.Add(Card(rules));
        }
        if (_pimMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "pim-msg", Text = _pimMsg, Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        sp.KeyDown += (_, e) =>
        {
            var inText = e.Source is TextBox;
            var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            if (inText && !(ctrl && e.Key is Key.N or Key.R or Key.Enter or Key.S)) return; // in Textfeldern nur die Strg-Kürzel
            if (inText && !ctrl) return;
            if (MailKey(e.Key, e.KeyModifiers)) e.Handled = true;
        };
        return SysPage("Posteingang", sp);
    }

    // ---------- Lesebereich ----------
    void MailReadPane(StackPanel read, ParsedMail m, MailAccount? a, List<MailFolderInfo> movable)
    {
        var uid = InboxOpenUid ?? 0;
        var head = Inbox.FirstOrDefault(h => h.Uid == uid);
        read.Children.Add(new TextBlock { Name = "inbox-subject", Text = m.Subject.Length > 0 ? m.Subject : "(ohne Betreff)", Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        read.Children.Add(P($"Von {m.From} · {(m.When is { } w ? w.LocalDateTime.ToString("dddd, dd.MM.yyyy HH:mm", De) : m.Date)}", null, 12));
        if ((m.To + m.Cc).Length > 0) read.Children.Add(P($"An {m.To}{(m.Cc.Length > 0 ? $" · Kopie an {m.Cc}" : "")}", null, 11.5));

        // Aktionen (die goldene Hauptaktion bleibt „Neue E-Mail“ in der Leiste)
        var flagged = head is not null ? MailIsFlagged(head) : _settings.MailFlagged.Contains(uid);
        var seen = head?.Seen ?? true;
        var move = MailBtn("inbox-move", "Verschieben ▾", () => { });
        move.IsEnabled = movable.Count > 0;
        move.Flyout = ContextMenuOf("inbox-move", movable.Select((f, k) => ($"{k}", f.Label, (Action)(async () => { await MailMove([uid], f.Path); MailRefresh(); }))));
        var flag = new Button { Name = "inbox-flag", Content = flagged ? "Gemerkt ★" : "Merken", FontSize = 12.5, Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(8), Foreground = Text, Background = Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 6, 6) };
        flag.Click += (_, _) => { var t = MailSetFlag(uid, !flagged); Show("posteingang"); _ = t.ContinueWith(_ => _ui.Post(MailRefresh)); };
        var reference = m.When?.LocalDateTime ?? DateTime.Now;
        var task = MailBtn("inbox-task", "Als Aufgabe", () =>
        {
            var due = MailDates.Find(m.Subject + "\n" + m.Body, reference).Select(x => (DateTime?)x.Start.Date).FirstOrDefault();
            var title = System.Text.RegularExpressions.Regex.Replace(m.Subject, @"^(re|aw|fw|wg):\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
            Cal.AddTodo(new TodoItem(Guid.NewGuid().ToString("N"), title.Length > 0 ? title : "E-Mail beantworten", due, false, 2, "E-Mail", Notes: $"Aus der E-Mail von {m.From}"));
            _pimMsg = $"„{title}“ steht in den Aufgaben{(due is { } dd ? $", fällig {dd:dd.MM.}" : "")}.";
        });
        read.Children.Add(new WrapPanel { Name = "inbox-actions", Children = {
            MailBtn("inbox-reply", "Antworten", () => MailCompose(MailReplyDraft(m, false)), tip: "Strg+R"),
            MailBtn("inbox-reply-all", "Allen antworten", () => MailCompose(MailReplyDraft(m, true)), tip: "Strg+Umschalt+R"),
            MailBtn("inbox-forward", "Weiterleiten", () => MailCompose(MailForwardDraft(m))),
            MailBtn("inbox-delete", MailRole("trash")?.Path == MailFolder ? "Endgültig löschen …" : "Löschen", async () => { await MailDelete([uid]); }, tip: "Entf"),
            move,
            MailBtn("inbox-seen", seen ? "Als ungelesen" : "Als gelesen", async () => { await MailSetSeen([uid], !seen); }),
            flag, task } });
        var html = m.Html.Length > 0;
        var second = new WrapPanel { Name = "inbox-actions-more", Children = {
            MailBtn("inbox-ki-summary", "Zusammenfassen (KI)", async () => { _pimMsg = "Die KI liest …"; await MailSummarize(); }),
            MailBtn("inbox-ki-reply", "Antwort vorschlagen (KI)", async () => { await MailSuggestReply(); }, tip: "Schreibt einen Entwurf ins Schreibfenster – gesendet wird nichts"),
            MailBtn("inbox-export", "Als Datei sichern", () => { MailExportOpen(); }, tip: "Speichert die Nachricht als .eml – öffnet sich in jedem Mailprogramm") } };
        if (html)
        {
            second.Children.Add(MailBtn("inbox-textonly", _mailTextOnly ? "Formatiert zeigen" : "Nur Text zeigen", () => { _mailTextOnly = !_mailTextOnly; }));
            second.Children.Add(MailBtn("inbox-browser", "Im Browser ansehen", () => { if (MailHtmlFile() is { } f) { Launch(f); _pimMsg = "Geprüfte Fassung im Browser geöffnet (ohne Skripte)."; } }, tip: "Zeigt die geprüfte Fassung mit dem ursprünglichen Aufbau"));
        }
        read.Children.Add(second);

        if (MailDeleteAskOpen)
            read.Children.Add(new Border { Name = "inbox-delete-confirm", Background = Brush.Parse("#33F59E0B"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Child = new StackPanel { Spacing = 6, Children = {
                new TextBlock { Text = MailRole("trash") is null ? "Dieses Postfach hat keinen Papierkorb. Die Nachricht wird endgültig gelöscht – das lässt sich nicht zurückholen. Wirklich löschen?" : "Die Nachricht liegt schon im Papierkorb. Endgültig löschen? Das lässt sich nicht zurückholen.", Foreground = Text, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold },
                new WrapPanel { Children = { MailBtn("inbox-delete-yes", "Ja, endgültig löschen", async () => { await MailDeleteConfirm(true); }), MailBtn("inbox-delete-no", "Abbrechen", async () => { await MailDeleteConfirm(false); }) } } } } });
        if (MailLinkAsk is { } url)
            read.Children.Add(new Border { Name = "inbox-link-confirm", Background = Brush.Parse("#33F59E0B"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Child = new StackPanel { Spacing = 6, Children = {
                new TextBlock { Text = "Dieser Link führt zu:", Foreground = Text, FontWeight = FontWeight.SemiBold },
                new SelectableTextBlock { Name = "inbox-link-target", Text = url, Foreground = Text, TextWrapping = TextWrapping.Wrap, FontSize = 12.5 },
                P("Nur öffnen, wenn du die Adresse kennst und dem Absender vertraust.", null, 12),
                new WrapPanel { Children = { MailBtn("inbox-link-yes", "Im Browser öffnen", () => MailLinkConfirm(true)), MailBtn("inbox-link-no", "Nicht öffnen", () => MailLinkConfirm(false)) } } } } });
        if (MailSummary.Length > 0)
            read.Children.Add(new Border { Name = "inbox-summary", Background = Brush.Parse("#22C9A227"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 8), Child = new StackPanel { Spacing = 4, Children = {
                P("Zusammenfassung der KI – bitte gegenlesen:", Text, 12), new SelectableTextBlock { Name = "inbox-summary-text", Text = MailSummary, Foreground = Text, TextWrapping = TextWrapping.Wrap, FontSize = 13 },
                MailBtn("inbox-summary-close", "Ausblenden", () => { MailSummary = ""; }) } } });

        // F2: Termin aus dem Text erkennen
        var hints = MailDates.Find(m.Subject + "\n" + m.Body, reference).Take(3).ToList();
        if (hints.Count > 0)
        {
            var hw = new WrapPanel { Name = "inbox-dates" };
            for (var k = 0; k < hints.Count; k++)
            {
                var hint = hints[k];
                var hb = new Button { Name = $"inbox-cal-{k}", Content = $"{(hint.HasTime ? hint.Start.ToString("ddd dd.MM.yyyy, HH:mm", De) : hint.Start.ToString("ddd dd.MM.yyyy", De))} als Termin", FontSize = 12, Margin = new Thickness(0, 0, 6, 4) };
                ToolTip.SetTip(hb, $"Gefunden: „{hint.Text}“");
                hb.Click += (_, _) =>
                {
                    Cal.Add(new CalEvent(Guid.NewGuid().ToString("N"), hint.Start, hint.HasTime ? hint.Start.AddHours(1) : hint.Start.AddDays(1), m.Subject.Length > 0 ? m.Subject : "Termin", "", "Aus der E-Mail von " + m.From, AllDay: !hint.HasTime));
                    _pimMsg = $"Termin eingetragen: {hint.Start.ToString(hint.HasTime ? "dddd, dd.MM. HH:mm" : "dddd, dd.MM.", De)}."; Show("posteingang");
                };
                hw.Children.Add(hb);
            }
            read.Children.Add(new Border { Background = Brush.Parse("#22C9A227"), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 6), Child = new StackPanel { Spacing = 4, Children = { P("Termin in dieser Nachricht gefunden – bitte prüfen:", Text, 12), hw } } });
        }

        // Inhalt: formatiert (geprüft, ohne Skripte; Bilder aus dem Internet erst nach Klick) oder als Text
        if (html && !_mailTextOnly)
        {
            var safe = MailHtml.Sanitize(m.Html, _mailRemoteImages);
            if (safe.BlockedImages > 0)
                read.Children.Add(new Border { Name = "inbox-images-blocked", Background = Brush.Parse("#14FFFFFF"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6), Child = new WrapPanel { Children = {
                    new TextBlock { Text = $"{safe.BlockedImages} Bild(er) aus dem Internet nicht geladen – so erfährt der Absender nicht, dass du die Nachricht geöffnet hast.", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxWidth = 460, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) },
                    MailBtn("inbox-images-load", "Bilder laden", async () => { _mailRemoteImages = true; await MailLoadImages(m); }) } } });
            var view = new MailHtmlView(Text, Muted, Accent, img => MailImageBytes(m, img), href => { MailLinkAsk = href; MailRefresh(); });
            read.Children.Add(new Border { Name = "inbox-html", Child = view.Build(safe) });
        }
        else read.Children.Add(new SelectableTextBlock { Name = "inbox-body", Text = m.Body, Foreground = Text, TextWrapping = TextWrapping.Wrap, FontSize = 13 });

        // Anhänge: ansehen und speichern – nie automatisch öffnen
        var files = m.Attachments.Where(x => !(x.Inline && html && m.Html.Contains("cid:" + x.ContentId, StringComparison.OrdinalIgnoreCase))).ToList();
        if (files.Count > 0)
        {
            var att = new StackPanel { Name = "inbox-attachments", Spacing = 3, Children = { P(files.Count == 1 ? "1 Anhang" : $"{files.Count} Anhänge", Text, 12) } };
            for (var k = 0; k < files.Count; k++)
            {
                var at = files[k];
                var risky = Browsers.Risky(at.Name);
                var saveAtt = Act($"inbox-att-{k}", "Speichern", () => MailSaveAttachment(at), !risky);
                saveAtt.Background = Brushes.Transparent; saveAtt.BorderThickness = new Thickness(1);
                att.Children.Add(new WrapPanel { Children = { P($"{at.Name} · {MailSize(at.Size)}{(risky ? " · ⚠ Programmdatei – nur speichern, wenn du dem Absender vertraust" : "")}", risky ? Brush.Parse("#FACC15") : null, 11.5), new Border { Width = 6 }, saveAtt } });
            }
            if (files.Count > 1) att.Children.Add(Act("inbox-att-all", "Alle speichern", () => { foreach (var f in files.Where(f => !Browsers.Risky(f.Name))) MailSaveAttachment(f); _pimMsg = $"Gespeichert in: {MailAttachmentFolder}"; Show("posteingang"); }));
            read.Children.Add(new Border { Background = Brush.Parse("#14FFFFFF"), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 6), Child = att });
        }
        read.Children.Add(P("Tasten: Strg+N neu · Strg+R antworten · Strg+Umschalt+R allen antworten · F weiterleiten · Entf löschen · J/K nächste/vorige", Muted, 10.5));
    }

    byte[]? MailImageBytes(ParsedMail m, MailHtmlElement img)
    {
        if (!img.Attr.TryGetValue("src", out var src)) return null;
        if (src.StartsWith("cid:", StringComparison.Ordinal)) return m.Attachments.FirstOrDefault(x => x.ContentId == src[4..] && x.ContentType.StartsWith("image/", StringComparison.Ordinal) && x.ContentType != "image/svg+xml")?.Data;
        if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) { try { return Convert.FromBase64String(src[(src.IndexOf(',') + 1)..]); } catch (FormatException) { return null; } }
        return _mailImages.GetValueOrDefault(src);
    }

    /// <summary>Test-Naht: lädt ein Bild aus dem Internet (sonst über HTTP, ohne Cookies und ohne Absenderangabe).</summary>
    public Func<string, Task<byte[]?>>? MailImageOverride { get; set; }
    static readonly Lazy<HttpClient> MailHttp = new(() => new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = true, MaxAutomaticRedirections = 3 }) { Timeout = TimeSpan.FromSeconds(12), MaxResponseContentBufferSize = 6 << 20 });

    /// <summary>Bilder aus dem Internet laden – nur nach dem ausdrücklichen Klick „Bilder laden“, nie von selbst.</summary>
    async Task MailLoadImages(ParsedMail m)
    {
        var safe = MailHtml.Sanitize(m.Html, allowRemoteImages: true);
        var urls = MailHtml.All(safe.Root).OfType<MailHtmlElement>().Where(e => e.Tag == "img").Select(e => e.Attr.GetValueOrDefault("src") ?? "").Where(s => s.StartsWith("http", StringComparison.OrdinalIgnoreCase)).Distinct().Take(40).ToList();
        var loaded = 0;
        foreach (var u in urls)
        {
            if (_mailImages.ContainsKey(u)) { loaded++; continue; }
            try
            {
                byte[]? data = MailImageOverride is { } fake ? await fake(u) : _core.Offline || _opt.Isolated ? null : await MailHttp.Value.GetByteArrayAsync(u);
                if (data is { Length: > 0 }) { _mailImages[u] = data; loaded++; }
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException or IOException) { }
        }
        _pimMsg = loaded == urls.Count ? $"{loaded} Bild(er) geladen." : $"{loaded} von {urls.Count} Bildern geladen{(_core.Offline ? " (ohne Internet)" : "")}.";
    }

    // ---------- Schreibfenster ----------
    void MailComposePane(StackPanel read, MailAccount? a)
    {
        var d = _sendDraft;
        var to = new TextBox { Name = "send-to", Text = d.To, PlaceholderText = "An (mehrere mit Komma)" };
        var cc = new TextBox { Name = "send-cc", Text = d.Cc, PlaceholderText = "Kopie an (optional)" };
        var bcc = new TextBox { Name = "send-bcc", Text = d.Bcc, PlaceholderText = "Blindkopie an (optional) – die anderen sehen diese Adressen nicht" };
        var subj = new TextBox { Name = "send-subject", Text = d.Subject, PlaceholderText = "Betreff" };
        var body = new TextBox { Name = "send-body", Text = d.Body, PlaceholderText = "Nachricht", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 240 };
        bool Sync()
        {
            var n = _sendDraft with { To = to.Text ?? "", Cc = cc.Text ?? "", Bcc = bcc.Text ?? "", Subject = subj.Text ?? "", Body = body.Text ?? "" };
            if (n.To == _sendDraft.To && n.Cc == _sendDraft.Cc && n.Bcc == _sendDraft.Bcc && n.Subject == _sendDraft.Subject && n.Body == _sendDraft.Body) return false;
            _sendDraft = n; return true;
        }
        _composeSync = Sync;
        void Changed() { if (Sync()) MailDraftChanged(); }
        foreach (var tb in new[] { to, cc, bcc, subj, body }) tb.TextChanged += (_, _) => Changed();
        var people = Mail.Contacts().Where(c => c.Email != "").ToList();
        var pick = new ComboBox { Name = "send-pick", PlaceholderText = "Aus dem Adressbuch", ItemsSource = people.Select(c => $"{c.Name} <{c.Email}>").ToList(), MinWidth = 200, IsVisible = people.Count > 0 };
        pick.SelectionChanged += (_, _) => { if (pick.SelectedIndex < 0) return; var c = people[pick.SelectedIndex]; to.Text = string.IsNullOrWhiteSpace(to.Text) ? c.Email : to.Text.TrimEnd(',', ' ') + ", " + c.Email; };

        var close = new Button { Name = "send-close", Content = "✕", Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Muted, Padding = new Thickness(8, 2) };
        ToolTip.SetTip(close, "Schließen – der Stand bleibt als Entwurf gespeichert");
        close.Click += (_, _) => { Sync(); _composeDirty = true; MailDraftFlush(); _composeSync = null; _draftId = null; _composeOpen = false; SendConfirmOpen = false; _sendDraft = new("", "", ""); _pimMsg = "Schreibfenster geschlossen – der Stand liegt bei den Entwürfen."; Show("posteingang"); };
        read.Children.Add(new Grid { Name = "send-window", ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Children = { H(_draftId is null ? "Neue E-Mail" : "Entwurf bearbeiten", 15), Pos(pick, 1), Pos(close, 2) } });
        read.Children.Add(P(a is null ? "Noch kein Konto – schreiben geht, senden erst nach „Konto einrichten“." : $"Von {(a.Name.Length > 0 ? a.Name + " · " : "")}{a.Email}", null, 11.5));
        read.Children.Add(to); read.Children.Add(cc); read.Children.Add(bcc); read.Children.Add(subj); read.Children.Add(body);

        // Anhänge
        var files = (d.Attachments ?? []).ToList();
        var attRow = new WrapPanel { Name = "send-attachments" };
        for (var i = 0; i < files.Count; i++)
        {
            var f = files[i]; var gone = !File.Exists(f);
            var rm = new Button { Name = $"send-att-remove-{i}", Content = "✕", Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Muted, Padding = new Thickness(6, 0), FontSize = 11 };
            ToolTip.SetTip(rm, "Anhang entfernen");
            rm.Click += (_, _) => { Sync(); _sendDraft = _sendDraft with { Attachments = (_sendDraft.Attachments ?? []).Where(x => x != f).ToList() }; MailDraftChanged(); Show("posteingang"); };
            attRow.Children.Add(new Border { Name = $"send-att-{i}", Background = Brush.Parse("#14FFFFFF"), CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 3, 2, 3), Margin = new Thickness(0, 0, 6, 6), Child = new StackPanel { Orientation = Orientation.Horizontal, Children = {
                new TextBlock { Text = gone ? $"⚠ {Path.GetFileName(f)} (nicht mehr da)" : $"📎 {Path.GetFileName(f)} · {MailSize(new FileInfo(f).Length)}", Foreground = gone ? Brush.Parse("#FACC15") : Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, rm } } });
        }
        var attach = MailBtn("send-attach", "Anhang hinzufügen", async () =>
        {
            string[]? got = PickOverride is { } fake ? await fake(new PickRequest("send-attach", false, true, []))
                : (await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Anhang wählen", AllowMultiple = true })).Select(x => x.TryGetLocalPath()).OfType<string>().ToArray();
            if (got is { Length: > 0 }) MailAttach(got);
        }, tip: "Oder Dateien einfach hierher ziehen");
        attRow.Children.Add(attach);
        read.Children.Add(attRow);
        var format = new CheckBox { Name = "send-format", Content = "Formatiert senden (**fett**, _kursiv_, Listen mit „- “, Links)", IsChecked = _sendFormatted, Foreground = Text, FontSize = 12 };
        format.IsCheckedChanged += (_, _) => { _sendFormatted = format.IsChecked == true; MailDraftChanged(); };
        read.Children.Add(format);

        var send = new Button { Name = "send-ask", Content = "Senden …", FontSize = 12.5, Padding = new Thickness(14, 6), CornerRadius = new CornerRadius(8), Foreground = Text, Background = SendConfirmOpen ? Brushes.Transparent : Accent, BorderBrush = Edge, BorderThickness = new Thickness(SendConfirmOpen ? 1 : 0), Margin = new Thickness(0, 0, 6, 6), IsEnabled = a is not null };
        ToolTip.SetTip(send, a is null ? "Erst das Konto einrichten" : "Fragt noch einmal nach, bevor die Nachricht rausgeht (Strg+Eingabe)");
        send.Click += (_, _) => { Sync(); AskSend(_sendDraft with { Html = "" }); Show("posteingang"); };
        var row = new WrapPanel { Children = { send,
            MailBtn("send-draft", "Als Entwurf speichern", async () => { await MailSaveDraft(); }, tip: "Strg+S"),
            MailBtn("send-discard", "Verwerfen", () => { _draftTimer?.Stop(); _composeDirty = false; _composeSync = null; if (_draftId is { } did) MailBoxes.DeleteDraft(did); _draftId = null; _composeOpen = false; SendConfirmOpen = false; _sendDraft = new("", "", ""); _mailKiUndo = null; _pimMsg = "Verworfen."; }) } };
        if (_mailKiUndo is not null) row.Children.Add(MailBtn("send-ki-undo", "KI-Text zurücknehmen", MailKiUndo));
        read.Children.Add(row);
        if (SendConfirmOpen)
        {
            var yes = new Button { Name = "send-confirm-yes", Content = "Ja, jetzt senden", Background = Accent }; yes.Click += async (_, _) => { yes.IsEnabled = false; await ConfirmSend(true); Show("posteingang"); };
            var no = new Button { Name = "send-confirm-no", Content = "Abbrechen" }; no.Click += async (_, _) => { await ConfirmSend(false); Show("posteingang"); };
            var n = SmtpSession.Recipients(_sendDraft).Count; var an = (_sendDraft.Attachments ?? []).Count;
            read.Children.Add(new Border { Name = "send-confirm", Background = Brush.Parse("#33F59E0B"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Child = new StackPanel { Spacing = 6, Children = {
                new TextBlock { Text = $"An {_sendDraft.To}: „{_sendDraft.Subject}“ jetzt wirklich senden?", Foreground = Text, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold },
                P($"{n} Empfänger{(_sendDraft.Cc.Length > 0 ? $" · Kopie an {_sendDraft.Cc}" : "")}{(_sendDraft.Bcc.Length > 0 ? $" · Blindkopie an {_sendDraft.Bcc}" : "")}{(an > 0 ? $" · {an} Anhang/Anhänge" : "")}{(_sendFormatted ? " · formatiert" : "")}. Gesendetes lässt sich nicht zurückholen.", Text, 12),
                new WrapPanel { Children = { yes, new Border { Width = 6 }, no } } } } });
        }
        read.Children.Add(P("Der Stand wird beim Schreiben von selbst als Entwurf gesichert. Dateien hierher ziehen hängt sie an.", Muted, 10.5));
    }
}
