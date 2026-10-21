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
        return FachPage("Lernen", sp);
    }
}
