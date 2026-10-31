using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Assist;
using Trendo.Core.Media;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>Masterprompt 131 / KI-Unterstützung: Programme schlagen vor, du sagst Ja oder Nein. Ein „Nein“ wird gemerkt, ein „Ja“ führt genau
/// den beschriebenen Schritt aus (mit Rückgängig des jeweiligen Programms). Nichts passiert ohne Klick.</summary>
public partial class MainWindow
{
    public bool SuggestionDismissed(string key) => _settings.DismissedSuggestions.Contains(key);
    void SuggestionDone(string key) { if (!_settings.DismissedSuggestions.Contains(key)) { _settings.DismissedSuggestions.Add(key); if (_settings.DismissedSuggestions.Count > 500) _settings.DismissedSuggestions.RemoveAt(0); SaveSettings(); } }

    /// <summary>Karte „Trendo schlägt vor“ mit Ja/Nein je Vorschlag; null, wenn nichts offen ist.</summary>
    Control? SuggestionCard(string page, IEnumerable<(Suggestion S, Action Yes)> items)
    {
        var open = items.Where(x => !SuggestionDismissed(x.S.Key)).Take(4).ToList();
        if (open.Count == 0) return null;
        var sp = new StackPanel { Name = "sugg-card", Spacing = 6, Children = { new TextBlock { Text = "✨ Trendo schlägt vor", Foreground = Brush.Parse("#FCD34D"), FontWeight = FontWeight.SemiBold, FontSize = 13 } } };
        var i = 0;
        foreach (var (s, yes) in open)
        {
            var key = s.Key; var n = i++;
            var ja = new Button { Name = $"sugg-yes-{n}", Content = "Ja", Background = Accent, Foreground = Brushes.White, Padding = new Thickness(14, 4), Margin = new Thickness(0, 0, 6, 0) };
            var nein = new Button { Name = $"sugg-no-{n}", Content = "Nein", Padding = new Thickness(12, 4) };
            ja.Click += (_, _) => { SuggestionDone(key); _core.Audit.Append(Principal.LocalOwner.Name, "suggest.yes", key); yes(); Show(page); };
            nein.Click += (_, _) => { SuggestionDone(key); _core.Audit.Append(Principal.LocalOwner.Name, "suggest.no", key); Show(page); };
            sp.Children.Add(new Border { Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(10), Background = Brush.Parse("#1AFCD34D"), BorderBrush = Brush.Parse("#44FCD34D"), BorderThickness = new Thickness(1),
                Child = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = {
                    new TextBlock { Text = s.Icon, FontSize = 20, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 10, 0) },
                    Pos(new StackPanel { Spacing = 2, Children = { new TextBlock { Name = $"sugg-title-{n}", Text = s.Title, Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 13, TextWrapping = TextWrapping.Wrap }, new TextBlock { Text = s.Detail, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap },
                        new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0), Children = { ja, nein } } } }, 1) } } });
        }
        return sp;
    }

    // ---------- Vorschläge je Programm ----------
    /// <summary>Für Tests: feste „jetzt“-Zeit der Vorschläge.</summary>
    public Func<DateTime> SuggestNow { get; set; } = () => DateTime.Now;

    IEnumerable<(Suggestion, Action)> CalendarSuggestions()
    {
        var now = SuggestNow();
        foreach (var (s, todo, a, b) in Suggest.TaskBlocks(Cal.Todos(), Cal.Occurrences(now.Date, now.Date.AddDays(15)), now))
            yield return (s, () => { var e = new CalEvent(Guid.NewGuid().ToString("N"), a, b, "Arbeiten an: " + todo.Title, "", "aufgabe:" + todo.Uid, RemindMin: 15);
                CalChange(e.Uid, () => { Cal.Add(e); return e; }, $"Eingeplant: {a:ddd d.M. HH:mm}–{b:HH:mm} für „{todo.Title}“."); CalDay = a.Date; });
    }

    IEnumerable<(Suggestion, Action)> TodoSuggestions() =>
        Suggest.TasksFromMail(Inbox.Select(h => (h.Uid, h.From, h.Subject)), Cal.Todos(), SuggestNow())
            .Select(x => (x.S, (Action)(() => { var t = new TodoItem(Guid.NewGuid().ToString("N"), x.Title, x.Due, false, x.Due is not null ? 1 : 2, "E-Mail"); TodoChange(t.Uid, () => { Cal.AddTodo(t); return t; }, $"„{x.Title}“ steht jetzt in den Aufgaben."); })));

    IEnumerable<(Suggestion, Action)> WorkflowSuggestions()
    {
        var have = WorkflowEngine.Templates.Where(t => Workflows.All().Any(w => w.Name == t.Make(WorkflowHome).Name)).Select(t => t.Id);
        foreach (var (s, tpl) in Suggest.Workflows(WorkflowHome, have))
            yield return (s, () => { var t = WorkflowEngine.Templates.First(x => x.Id == tpl); var w = Workflows.Add(t.Make(WorkflowHome)); WfSelected = w.Id; WfNode = -1; _wfMsg = $"„{w.Name}“ angelegt – mach zuerst einen Probelauf{(w.NeedsApproval ? ", dann „Freigeben …“" : "")}."; });
    }

    /// <summary>Videoschnitt: Ergebnis der Stille-Suche je Stück (Quellzeiten), bis „Ja“/„Nein“.</summary>
    private readonly Dictionary<string, List<(double Start, double End)>> _silences = [];
    /// <summary>Für Tests: Ersatz für FFmpeg-silencedetect (Datei → Ausgabe).</summary>
    public Func<string, string>? SilenceProbe { get; set; }

    /// <summary>Stille in allen Stücken mit Ton suchen (FFmpeg silencedetect, mindestens 1 s unter −35 dB).</summary>
    public int EditFindSilences(Trendo.Core.Programs.ProgramDocument d)
    {
        _silences.Clear(); var found = 0;
        foreach (var c in VideoEdit.Clips(d.Data).Where(c => c.HasAudio && !c.IsImage))
        {
            string err;
            if (SilenceProbe is { } probe) err = probe(c.File);
            else
            {
                var ff = FfmpegPath ?? throw new InvalidOperationException("Für die Stille-Suche fehlt FFmpeg.");
                if (_opt.Isolated) throw new InvalidOperationException("Im abgeschotteten Testmodus startet Trendo kein FFmpeg.");
                err = Cli.Run(ff, Suggest.SilenceArgs(c.File), null, 300000).Err;
            }
            var r = Suggest.Silences(err).Where(x => x.End > c.In && x.Start < c.Out).ToList();
            if (r.Count > 0) { _silences[c.Id] = r; found += r.Count; }
        }
        _progMsg[VK] = found == 0 ? "Keine längeren stillen Stellen gefunden." : $"{found} stille Stelle(n) gefunden – Vorschläge stehen rechts.";
        return found;
    }

    IEnumerable<(Suggestion, Action)> EditSuggestions(Trendo.Core.Programs.ProgramDocument d)
    {
        var clips = VideoEdit.Clips(d.Data);
        foreach (var (id, ranges) in _silences.ToList())
        {
            var c = clips.FirstOrDefault(x => x.Id == id); if (c is null) continue;
            var secs = ranges.Sum(r => Math.Min(r.End, c.Out) - Math.Max(r.Start, c.In));
            var key = $"stille:{c.Id}:{c.In:0.0}-{c.Out:0.0}";
            yield return (new Suggestion(key, $"Stille in „{Path.GetFileName(c.File)}“ herausschneiden", $"{ranges.Count} stille Stelle(n), zusammen {secs:0.0} s. Das Video wird dadurch {secs:0.0} s kürzer.", "✂"),
                () => { var idx = VideoEdit.Clips(d.Data).FindIndex(x => x.Id == id); if (idx < 0) return; d.Edit("Stille entfernen", x => VideoEdit.CutOut(x, idx, ranges)); _silences.Remove(id); _progMsg[VK] = $"{ranges.Count} stille Stelle(n) herausgeschnitten – Strg+Z holt sie zurück."; });
        }
    }
}
