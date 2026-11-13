using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Briefe wie im Kanzlei-Alltag (Profi 5 + Claude 5, 06.11.2026): Briefart erkennen mit Frist, Pflichtangaben und Anlagen, Frist erst nach „Ja“ in den Kalender,
/// Wiedervorlage je Akte, Fristen-Übersicht über alle Akten, Postausgangsbuch (was wann wohin ging) mit CSV.</summary>
public partial class MainWindow
{
    static readonly CultureInfo DeC = CultureInfo.GetCultureInfo("de-DE");
    LetterOffice.PostBook PostOut => new(ProjectsRoot);
    DateTime? _kindAsk; string _kindMsg = "";
    public IReadOnlyList<LetterOffice.PostEntry> PostOutEntries => PostOut.List();
    /// <summary>Für Tests: „heute“.</summary>
    public DateTime? LetterToday { get; set; }
    DateTime Today => LetterToday ?? DateTime.Today;

    /// <summary>Brief ins Postausgangsbuch eintragen (öffentlich für Tests und Claude).</summary>
    public LetterOffice.PostEntry LetterPostOut(LetterDraft d, string channel, DateTime date, string proof = "", string caseId = "")
    {
        var e = PostOut.Add(new(date, d.Recipient, d.Subject, channel, caseId, "", proof.Trim()));
        _core.Audit.Append(Principal.LocalOwner.Name, "letter.postausgang", $"{e.Date:yyyy-MM-dd} {e.Channel} {e.Recipient}");
        return e;
    }

    /// <summary>Claude 5 / Profi 5: Briefart erkennen, Frist ausrechnen, Pflichtangaben und Anlagen – die Frist kommt erst nach „Ja“ in den Kalender.</summary>
    private Control LetterKindCard(LetterDraft d)
    {
        const string V = "brief";
        var sp = new StackPanel { Name = "letter-kind", Spacing = 5 };
        sp.Children.Add(new TextBlock { Text = "Briefart und Pflichtangaben", Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold });
        var k = LetterOffice.Detect(d.Subject, d.Body);
        sp.Children.Add(new TextBlock { Name = "letter-kind-name", Text = k is null ? "Noch keine bestimmte Briefart erkannt – Betreff und Text schreiben." : $"Erkannt: {k.Label}", Foreground = k is null ? Muted : GoldText(), FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        if (k is not null) sp.Children.Add(P(k.Hint, null, 11));
        var missing = LetterOffice.Missing(d, k);
        foreach (var m in missing) sp.Children.Add(P("Fehlt: " + m, Brush.Parse("#F5A524"), 11.5));
        if (missing.Count == 0) sp.Children.Add(P("Pflichtangaben nach DIN 5008 vollständig.", Brush.Parse("#2FBF71"), 11.5));
        var row = new WrapPanel();
        if (k is { Attachments.Length: > 0 } && string.IsNullOrWhiteSpace(d.Attachments))
            row.Children.Add(Act("letter-kind-attach", "Anlagen eintragen", () => { d.Attachments = string.Join("\n", k.Attachments); _letterMsg = "Anlagen eingetragen – bitte prüfen."; Show(V); }));
        if (k is not null && LetterOffice.KindDeadline(k, k.From == "heute" ? Today : d.Date) is { } dl)
        {
            var text = $"Frist: {dl.Date.ToString("dddd, dd.MM.yyyy", DeC)} ({k.Amount} {k.Unit} ab {(k.From == "heute" ? "heute" : k.From + " – hier: Briefdatum, bitte anpassen")}){(dl.Shifted ? ", aufs Wochenende gefallen → Montag" : "")}";
            sp.Children.Add(new TextBlock { Name = "letter-kind-deadline", Text = text, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            if (_kindAsk is { } ask && ask == dl.Date)
            {
                sp.Children.Add(P($"Soll die Frist am {dl.Date:dd.MM.yyyy} in deinen Kalender (Erinnerung zwei Tage vorher)?", Text, 12));
                row.Children.Add(Act("letter-kind-cal-yes", "Ja, eintragen", () =>
                {
                    Cal.Add(new Trendo.Core.Sys.CalEvent(Guid.NewGuid().ToString("N"), dl.Date, dl.Date.AddDays(1), "Frist: " + (d.Subject.Trim().Length > 0 ? d.Subject.Trim() : k.Label), AllDay: true, RemindMin: 2 * 24 * 60));
                    _core.Audit.Append(Principal.LocalOwner.Name, "letter.frist.kalender", $"{k.Id} {dl.Date:yyyy-MM-dd}");
                    _kindAsk = null; _letterMsg = $"Frist am {dl.Date:dd.MM.yyyy} im Kalender."; Show(V);
                }, true));
                row.Children.Add(Act("letter-kind-cal-no", "Nein", () => { _kindAsk = null; _letterMsg = "Nicht eingetragen."; Show(V); }));
            }
            else row.Children.Add(Act("letter-kind-cal", "Frist in den Kalender …", () => { _kindAsk = dl.Date; Show(V); }));
        }
        // Postausgangsbuch für einzelne Briefe
        var channel = new ComboBox { Name = "letter-post-channel", ItemsSource = LetterOffice.Channels.Select(c => c.Label).ToList(), SelectedIndex = 0, Width = 160 };
        row.Children.Add(new Border { Width = 8 }); row.Children.Add(channel);
        row.Children.Add(Act("letter-post-add", "Als verschickt eintragen", () =>
        {
            try { var e = LetterPostOut(d, LetterOffice.Channels[Math.Max(0, channel.SelectedIndex)].Id, Today); _letterMsg = $"Im Postausgangsbuch: {e.Date:dd.MM.yyyy} an {e.Recipient}."; }
            catch (ArgumentException ex) { _letterMsg = ex.Message; }
            Show(V);
        }));
        sp.Children.Add(row);
        return new Border { CornerRadius = new CornerRadius(10), BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(10), Background = TileBg, Child = sp };
    }

    /// <summary>In der Akte: Wiedervorlage und Postausgang dieser Akte.</summary>
    private Control CaseOfficeCard(LetterCase c)
    {
        var sp = new StackPanel { Name = "case-office", Spacing = 5 };
        sp.Children.Add(H("Wiedervorlage", 14));
        var date = new TextBox { Name = "case-resubmit-date", Text = c.ResubmitOn?.ToString("dd.MM.yyyy") ?? "", PlaceholderText = "TT.MM.JJJJ", Width = 110 };
        var note = new TextBox { Name = "case-resubmit-note", Text = c.ResubmitNote, PlaceholderText = "Wozu? z. B. „Antwort der Behörde da?“", Width = 280 };
        sp.Children.Add(new WrapPanel { Children = {
            date, new Border { Width = 4 }, note, new Border { Width = 4 },
            Act("case-resubmit-set", "Setzen", () =>
            {
                if (!DateTime.TryParseExact((date.Text ?? "").Trim(), "d.M.yyyy", DeC, DateTimeStyles.None, out var on)) { _caseMsg = "Datum bitte als TT.MM.JJJJ."; Show("akte"); return; }
                c.ResubmitOn = on; c.ResubmitNote = (note.Text ?? "").Trim(); SaveCase("Wiedervorlage"); _caseMsg = $"Wiedervorlage am {on:dd.MM.yyyy}."; Show("akte");
            }, true),
            new Border { Width = 4 }, Act("case-resubmit-in2w", "+ 2 Wochen", () => { c.ResubmitOn = Today.AddDays(14); SaveCase("Wiedervorlage"); Show("akte"); }),
            new Border { Width = 4 }, Act("case-resubmit-done", "Erledigt", () => { c.ResubmitOn = null; c.ResubmitNote = ""; SaveCase("Wiedervorlage erledigt"); _caseMsg = "Wiedervorlage erledigt."; Show("akte"); }) } });
        sp.Children.Add(H("Postausgang dieser Akte", 14));
        var channel = new ComboBox { Name = "case-post-channel", ItemsSource = LetterOffice.Channels.Select(x => x.Label).ToList(), SelectedIndex = 1, Width = 160 };
        var proof = new TextBox { Name = "case-post-proof", PlaceholderText = "Nachweis (Sendungsnummer, Sendebericht …)", Width = 260 };
        sp.Children.Add(new WrapPanel { Children = { channel, new Border { Width = 4 }, proof, new Border { Width = 4 }, Act("case-post-add", "Als verschickt eintragen", () =>
        {
            try
            {
                var e = LetterPostOut(c.Draft, LetterOffice.Channels[Math.Max(0, channel.SelectedIndex)].Id, Today, proof.Text ?? "", c.Id);
                c.Log.Add($"{e.Date:dd.MM.yyyy}: verschickt per {LetterOffice.Channels.First(x => x.Id == e.Channel).Label}" + (e.Proof.Length > 0 ? $" (Nachweis {e.Proof})" : ""));
                if (e.Proof.Length > 0) { c.DeliveryProof = e.Proof; c.State = DeliveryState.Versendet; }
                SaveCase("Postausgang"); _caseMsg = "Im Postausgangsbuch eingetragen.";
            }
            catch (ArgumentException ex) { _caseMsg = ex.Message; }
            Show("akte");
        }) } });
        foreach (var e in PostOut.List().Where(x => x.CaseId == c.Id))
            sp.Children.Add(P($"{e.Date:dd.MM.yyyy} · {LetterOffice.Channels.FirstOrDefault(x => x.Id == e.Channel).Label} · an {e.Recipient}" + (e.Proof.Length > 0 ? $" · Nachweis {e.Proof}" : ""), Text, 11.5));
        return sp;
    }

    /// <summary>Über allen Akten: was ansteht (Fristen und Wiedervorlagen) und das Postausgangsbuch.</summary>
    private Control LetterAgendaCard()
    {
        var sp = new StackPanel { Name = "case-agenda", Spacing = 3 };
        var items = LetterOffice.Agenda(Cases.List(Principal.LocalOwner), Today).Where(a => a.Soon).ToList();
        sp.Children.Add(H(items.Count == 0 ? "Fristen und Wiedervorlagen – nichts in den nächsten 7 Tagen" : $"Steht an ({items.Count})", 14));
        foreach (var (a, i) in items.Select((a, i) => (a, i)))
        {
            var cid = a.CaseId;
            var b = new Button { Name = $"case-agenda-{i}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, Padding = new Thickness(6, 2),
                Content = new TextBlock { Text = $"{(a.Overdue ? "⚠ überfällig · " : "")}{a.Date:ddd dd.MM.} · {a.Kind} · {a.CaseTitle}: {a.Text}", Foreground = a.Overdue ? Brush.Parse("#F87171") : Text, FontSize = 12, TextWrapping = TextWrapping.Wrap } };
            b.Click += (_, _) => { if (Cases.List(Principal.LocalOwner).FirstOrDefault(x => x.Id == cid) is { } cc) { OpenCase(cc); Show("akte"); } };
            sp.Children.Add(b);
        }
        var book = PostOut.List();
        sp.Children.Add(new WrapPanel { Margin = new Thickness(0, 6, 0, 0), Children = {
            new TextBlock { Text = $"Postausgangsbuch: {book.Count} Einträge", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) },
            Act("post-book-csv", "Als Tabelle (CSV) speichern", () =>
            {
                var f = Path.Combine(ProjectsRoot, "Briefe", $"Postausgangsbuch-{Today:yyyy-MM-dd}.csv"); Directory.CreateDirectory(Path.GetDirectoryName(f)!);
                File.WriteAllText(f, PostOut.Csv(), new System.Text.UTF8Encoding(true)); _caseMsg = "Gespeichert: " + f; NotifyFileResult(_caseMsg); Show("akten");
            }) } });
        foreach (var e in book.Take(8)) sp.Children.Add(P($"{e.Date:dd.MM.yyyy} · {e.Recipient} · {e.Subject} · {LetterOffice.Channels.FirstOrDefault(x => x.Id == e.Channel).Label}", Muted, 11));
        return sp;
    }
}
