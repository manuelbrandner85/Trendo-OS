using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>SPEC-06: Lernen & Training – Karteikarten.</summary>
public partial class MainWindow
{
    private LearnBox? _learn;
    public LearnBox Learn => _learn ??= new LearnBox(_core.DataRoot);
    private bool _reveal;

    private void InitSpec06() => RegisterTab(FachTabs, "lernen", "Lernen", "SPEC-06", LearnView, "lernen", "karteikarten", "vokabeln", "schule", "prüfung", "training", "quiz", "anki");

    private Control LearnView()
    {
        var sp = new StackPanel { Spacing = 8 };
        var (total, due, learned, hard) = Learn.Progress();
        sp.Children.Add(new TextBlock { Name = "learn-progress", Text = $"{total} Karten · {due} heute fällig · {learned} sitzen sicher · {hard} machen Mühe", Foreground = Text, FontSize = 14 });
        var card = Learn.DueCards().FirstOrDefault();
        if (card is null) sp.Children.Add(P(total == 0 ? "Noch keine Karten. Lege unten welche an oder füge eine Liste ein." : "✓ Für heute alles gelernt. Morgen geht es weiter.", Text));
        else
        {
            var body = new StackPanel { Spacing = 6, Children = { P(card.Deck, null, 11), new TextBlock { Name = "learn-front", Text = card.Front, FontSize = 18, Foreground = Text, TextWrapping = TextWrapping.Wrap } } };
            if (!_reveal) { var show = new Button { Name = "learn-show", Content = "Antwort zeigen", Background = Accent }; show.Click += (_, _) => { _reveal = true; Show("lernen"); }; body.Children.Add(show); }
            else
            {
                body.Children.Add(new TextBlock { Name = "learn-back", Text = card.Back, FontSize = 16, Foreground = Accent, TextWrapping = TextWrapping.Wrap });
                var row = new WrapPanel();
                foreach (var (g, l) in new[] { (Grade.Falsch, "Wusste ich nicht"), (Grade.Schwer, "Schwer"), (Grade.Gut, "Gut"), (Grade.Leicht, "Leicht") })
                { var b = new Button { Name = "learn-" + g.ToString().ToLowerInvariant(), Content = l }; b.Click += (_, _) => { var c = Learn.Answer(card.Id, g); _reveal = false; _w45Msg = $"Nächstes Mal in {c.IntervalDays} Tag(en)."; Show("lernen"); }; row.Children.Add(MSp(b)); }
                body.Children.Add(row);
            }
            sp.Children.Add(WCard(body));
        }
        sp.Children.Add(FachTool("learn-add", "Karte anlegen", "Ein Stapel ist ein Thema, z. B. „Englisch“ oder „Führerschein“.",
            [("deck", "Stapel", "Englisch"), ("front", "Frage", ""), ("back", "Antwort", "")],
            v => { Learn.Add(v("deck"), v("front"), v("back")); Show("lernen"); return "Karte angelegt."; }, "Anlegen"));
        sp.Children.Add(FachTool("learn-import", "Liste einfügen", "Eine Karte pro Zeile: „Frage;Antwort“ (oder Tab-getrennt aus Excel/Anki). Mehrere Zeilen mit | trennen.",
            [("deck", "Stapel", "Englisch"), ("text", "Zeilen", "")],
            v => { var n = Learn.Import(v("deck").Trim() == "" ? "Allgemein" : v("deck").Trim(), v("text").Replace('|', '\n')); Show("lernen"); return $"{n} Karte(n) übernommen."; }, "Übernehmen"));
        if (Learn.Decks.Count > 0) sp.Children.Add(P("Stapel: " + string.Join(", ", Learn.Decks.Select(d => $"{d} ({Learn.Progress(d).Total})")), null, 11));
        var (streak, today) = Learn.Streak();
        sp.Children.Add(FachGroup("learn-play", "Anders lernen", false,
            FachTool("learn-type", "Antwort eintippen", "Die nächste fällige Karte – Antwort tippen; kleine Tippfehler sind erlaubt.",
                [("ans", card is null ? "Gerade nichts fällig" : card.Front, "")],
                v => { if (card is null) return "Gerade ist nichts fällig."; var (ok, typo) = LearnBox.CheckTyped(card.Back, v("ans")); Learn.Answer(card.Id, ok ? (typo ? Grade.Schwer : Grade.Gut) : Grade.Falsch); _reveal = false; Show("lernen"); return ok ? (typo ? $"✓ Fast – richtig ist „{card.Back}“." : "✓ Richtig!") : $"✗ Richtig wäre „{card.Back}“."; }, "Prüfen"),
            FachTool("learn-quiz", "Auswahl-Quiz", "Trendo zeigt eine Frage mit 4 Antworten. Antwort-Nummer (1–4) eintippen.",
                [("deck", "Stapel (leer = alle)", ""), ("pick", "Deine Wahl (1–4)", "")],
                v =>
                {
                    var d = v("deck").Trim() == "" ? null : v("deck").Trim();
                    if (_fachIn.TryGetValue("quiz-card", out var qid) && v("pick").Trim() != "" && Learn.Store.Cards.FirstOrDefault(c => c.Id == qid) is { } qc)
                    { var right = (int)Fach.Num(v("pick")) - 1 == int.Parse(_fachIn["quiz-ok"]); Learn.Answer(qc.Id, right ? Grade.Gut : Grade.Falsch); _fachIn.Remove("quiz-card"); _fachIn["learn-quiz-pick"] = ""; return right ? "✓ Richtig! Nächste Frage: nochmal auf „Quiz“." : $"✗ Richtig war: {qc.Back}"; }
                    var q = Learn.Quiz(d); _fachIn["quiz-card"] = q.Card.Id; _fachIn["quiz-ok"] = q.Correct.ToString();
                    return $"{q.Card.Front}\n" + string.Join("\n", q.Options.Select((o, i) => $"{i + 1}) {o}"));
                }, "Quiz")));
        sp.Children.Add(FachGroup("learn-stats", $"Fortschritt · Serie {streak} Tag(e){(today ? " ✓" : "")}", false,
            new TextBlock { Name = "learn-forecast", Text = "Nächste 7 Tage: " + string.Join(" · ", Learn.Forecast().Select(f => $"{f.Day:ddd} {f.Due}")), Foreground = Text, TextWrapping = TextWrapping.Wrap },
            P(Learn.Leeches().Count == 0 ? "Keine Problemkarten." : "Problemkarten (oft vergessen): " + string.Join(", ", Learn.Leeches().Take(10).Select(c => $"{c.Front} ({c.Lapses}×)")) + ". Tipp: Karte umformulieren oder aufteilen.", null, 11),
            FachTool("learn-exam", "Prüfungsplan", "Wie viele neue Karten pro Tag bis zur Prüfung? 3 Tage vorher nur noch wiederholen.",
                [("date", "Prüfung am (TT.MM.JJJJ)", DateTime.Today.AddDays(30).ToString("dd.MM.yyyy")), ("deck", "Stapel (leer = alle)", "")],
                v => { if (!DateTime.TryParse(v("date"), Fach.De, System.Globalization.DateTimeStyles.None, out var d)) throw new FormatException("Datum bitte als TT.MM.JJJJ."); var p = Learn.ExamPlan(d, v("deck").Trim() == "" ? null : v("deck").Trim()); return $"{p.NewCards} neue Karten in {p.DaysLeft} Tagen → {p.NewPerDay} pro Tag. {p.Advice}"; }, "Planen")));
        sp.Children.Add(FachGroup("learn-manage", "Karten & Stapel verwalten", false,
            FachTool("learn-edit", "Karte ändern oder löschen", "Frage genau wie gespeichert eingeben. Antwort leer lassen und „löschen“ eintippen, um zu löschen.",
                [("front", "Frage", ""), ("newfront", "Neue Frage", ""), ("back", "Neue Antwort (oder „löschen“)", "")],
                v => { var c = Learn.Store.Cards.FirstOrDefault(x => x.Front.Equals(v("front").Trim(), StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException("Karte nicht gefunden."); if (v("back").Trim().Equals("löschen", StringComparison.OrdinalIgnoreCase)) { Learn.Delete(c.Id); Show("lernen"); return "Gelöscht."; } Learn.Edit(c.Id, v("newfront").Trim() == "" ? c.Front : v("newfront"), v("back").Trim() == "" ? c.Back : v("back")); Show("lernen"); return "Geändert."; }, "Übernehmen"),
            FachTool("learn-rename", "Stapel umbenennen", "Gibt es den neuen Namen schon, werden die Stapel zusammengeführt.",
                [("from", "Stapel", Learn.Decks.FirstOrDefault() ?? ""), ("to", "Neuer Name", "")],
                v => { var n = Learn.RenameDeck(v("from"), v("to")); Show("lernen"); return $"{n} Karte(n) verschoben."; }, "Umbenennen"),
            FachTool("learn-rev", "Umgekehrt lernen", "Erzeugt zu jedem Stapel-Eintrag die Gegenrichtung (z. B. Englisch → Deutsch).",
                [("deck", "Stapel", Learn.Decks.FirstOrDefault() ?? "")],
                v => { var n = Learn.AddReversed(v("deck").Trim()); Show("lernen"); return $"{n} umgekehrte Karte(n) angelegt."; }, "Erzeugen"),
            FachTool("learn-export", "Stapel speichern", "Als CSV zum Teilen oder Sichern (lässt sich wieder einfügen).",
                [("deck", "Stapel (leer = alle)", "")],
                v => { var d = v("deck").Trim() == "" ? null : v("deck").Trim(); var p = Path.Combine(Fach.StoreDir(_core.DataRoot, "lernen"), $"karten-{(d is null ? "alle" : string.Concat(d.Where(char.IsLetterOrDigit)))}.csv"); File.WriteAllText(p, Learn.Export(d)); return "Gespeichert: " + p; }, "Speichern")));
        return FachPage("Lernen", sp);
    }
}
