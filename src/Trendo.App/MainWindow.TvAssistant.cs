using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Tv;

namespace Trendo.App;

/// <summary>TV-Assistent (Wunsch 27.10.2026): „Blende alle Sender aus und behalte nur deutsche“, „Spiel ZDF“, „Nimm den Tatort auf“ …
/// Erst feste Regeln, sonst die KI – ausgeführt werden nur die erlaubten TV-Aktionen.</summary>
public partial class MainWindow
{
    string _tvAsk = "";
    static readonly string[] TvExamples = ["Behalte nur deutsche Sender", "Nur deutsche Filme und Serien", "Zeig Netflix-Serien", "Disney+ Filme", "Zeig Action-Filme", "Sperre Erwachsenen-Gruppen", "Zeig wieder alles", "Was läuft gerade?"];

    /// <summary>Gruppen eines Bereichs: live (Sender), film, serie oder leer (alles).</summary>
    List<string> TvScopeGroups(string scope) => (scope switch
    {
        "live" => TvChannels().Select(c => c.Group),
        "film" => TvVodList().Select(v => v.Category),
        "serie" => TvSeriesList().Select(s => s.Category),
        _ => TvChannels().Select(c => c.Group).Concat(TvVodList().Select(v => v.Category)).Concat(TvSeriesList().Select(s => s.Category)),
    }).Distinct().ToList();

    static string ScopeWord(string scope) => scope switch { "live" => "Sender", "film" => "Filme", "serie" => "Serien", _ => "Sender, Filme und Serien" };

    Control TvAssistantBar()
    {
        var box = new TextBox { Name = "tv-ki", Text = _tvAsk, PlaceholderText = "✨  Sag, was Trendo tun soll – z. B. „Behalte nur deutsche Sender“", CornerRadius = new CornerRadius(14), FontSize = 14, Padding = new Thickness(12, 9) };
        var go = new Button { Name = "tv-ki-go", Content = "Ausführen", CornerRadius = new CornerRadius(14), Padding = new Thickness(18, 9), Foreground = Brush.Parse("#1B1406"), FontWeight = FontWeight.SemiBold,
            Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse("#FFF1C870"), 0), new GradientStop(Color.Parse("#FFD4A84B"), 1) } } };
        go.Click += async (_, _) => { _tvAsk = box.Text ?? ""; await TvDo(_tvAsk); };
        box.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; _tvAsk = box.Text ?? ""; await TvDo(_tvAsk); } };
        var chips = new WrapPanel { Name = "tv-ki-examples" };
        for (var i = 0; i < TvExamples.Length; i++)
        {
            var ex = TvExamples[i];
            var b = new Button { Name = "tv-ki-ex-" + i, Content = ex, FontSize = 11.5, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 0), CornerRadius = new CornerRadius(12), Background = Brush.Parse("#14FFFFFF"), BorderBrush = Brush.Parse("#22D4A84B"), BorderThickness = new Thickness(1) };
            b.Click += async (_, _) => { _tvAsk = ex; await TvDo(ex); };
            chips.Children.Add(b);
        }
        return Glass2(new StackPanel { Spacing = 6, Children = { new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { box, Pos(Sp(go), 1) } }, chips } }, 10);
    }

    /// <summary>Wunsch ausführen. Liefert die Meldung (steht danach auch im Studio).</summary>
    public async Task<string> TvDo(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var actions = TvCommands.Rules(text);
        if (actions.Count == 0)
        {
            using var label = KiActivityHub.Label("TV-Assistent");
            var a = await Intel.AskAsync(new KiRequest(text, SystemPrompt: TvCommands.KiSystem, UseKnowledge: false));
            if (a.Ok) actions = TvCommands.ParseKi(a.Text);
        }
        var msgs = new List<string>();
        foreach (var act in actions) msgs.Add(await TvRun(act));
        _tvMsg = msgs.Count == 0 ? "✗ Das habe ich nicht verstanden. Beispiele: „Behalte nur deutsche Sender“, „Spiel ZDF“, „Nimm den Tatort auf“, „Zeig Action-Filme“." : string.Join(" ", msgs);
        _core.Audit.Append(Principal.LocalOwner.Name, "tv.assistant", string.Join(",", actions.Select(x => x.Kind)));
        Show("tv");
        return _tvMsg;
    }

    static readonly Regex Adult = new(@"\b(erwachsen\w*|adult|xxx|18\+|porn\w*|erotik|for adults)\b", RegexOptions.IgnoreCase);

    List<string> TvGroupsMatching(string arg)
    {
        var groups = TvChannels().Select(c => c.Group).Distinct();
        return arg is "adult" or "erwachsene" ? groups.Where(g => Adult.IsMatch(g)).ToList()
            : groups.Where(g => g.Contains(arg.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
    }

    async Task<string> TvRun(TvCommands.Action a)
    {
        var all = TvChannels();
        switch (a.Kind)
        {
            case "only-country":
            {
                var groups = TvScopeGroups(a.Extra);
                var keep = groups.Where(g => TvCommands.CountryOf(g) == a.Arg || TvCommands.IsCountry(g, a.Arg)).ToHashSet();
                if (a.Extra is "" or "live") keep.UnionWith(TvCommands.CountryGroups(all, a.Arg)); // Sendergruppen auch nach den Sendernamen
                if (keep.Count == 0) return $"✗ Ich habe keine Gruppen für dieses Land gefunden ({a.Arg.ToUpperInvariant()}). Unter „Quellen“ kannst du Gruppen einzeln ausblenden.";
                var hide = groups.Where(g => !keep.Contains(g)).ToList();
                Tv.SetHiddenGroups(Tv.HiddenGroups.Except(groups).Concat(hide));
                _tvGroup = "Alle"; _tvVodCat = "Alle"; _tvSerCat = "Alle";
                _tvTab = a.Extra switch { "film" => "film", "serie" => "serie", _ => "live" };
                return $"✓ Nur noch {CountryName(a.Arg)} {ScopeWord(a.Extra)}: {keep.Count} Gruppen sichtbar, {hide.Count} ausgeblendet. „Zeig wieder alles“ macht es rückgängig.";
            }
            case "show-all":
                Tv.SetHiddenGroups(a.Extra is "" ? [] : Tv.HiddenGroups.Except(TvScopeGroups(a.Extra)));
                _tvGroup = "Alle"; _tvVodProv = _tvVodLang = _tvSerProv = _tvSerLang = _tvVodCat = _tvSerCat = "Alle";
                return $"✓ Alle {ScopeWord(a.Extra)} sind wieder sichtbar.";
            case "provider":
                if (a.Extra == "film") { _tvTab = "film"; _tvVodProv = a.Arg; _tvVodCat = "Alle"; _tvVodSel = null; }
                else { _tvTab = "serie"; _tvSerProv = a.Arg; _tvSerCat = "Alle"; _tvSerSel = null; }
                var provCats = (a.Extra == "film" ? TvVodList().Select(v => v.Category) : TvSeriesList().Select(s => s.Category)).Distinct().Count(c => TvCommands.HasProvider(c, a.Arg));
                return provCats > 0 ? $"✓ {(a.Extra == "film" ? "Filme" : "Serien")} von {a.Arg} ({provCats} Kategorien)." : $"✗ In deiner Liste gibt es keine {(a.Extra == "film" ? "Filme" : "Serien")} von {a.Arg}.";
            case "lang":
                if (a.Extra == "film") { _tvTab = "film"; _tvVodLang = a.Arg; _tvVodCat = "Alle"; } else { _tvTab = "serie"; _tvSerLang = a.Arg; _tvSerCat = "Alle"; }
                return $"✓ Sprache: {TvCommands.LanguageLabel(a.Arg)}.";
            case "hide-groups":
            {
                var g = TvGroupsMatching(a.Arg); if (g.Count == 0) return $"✗ Keine Gruppe mit „{a.Arg}“ gefunden.";
                Tv.SetHiddenGroups(Tv.HiddenGroups.Concat(g)); return $"✓ {g.Count} Gruppe(n) ausgeblendet.";
            }
            case "show-groups":
            {
                var g = TvGroupsMatching(a.Arg); Tv.SetHiddenGroups(Tv.HiddenGroups.Except(g)); return $"✓ {g.Count} Gruppe(n) wieder sichtbar.";
            }
            case "lock-groups":
            {
                var g = TvGroupsMatching(a.Arg); if (g.Count == 0) return "✗ Keine passenden Gruppen gefunden.";
                Tv.SetLockedGroups(Tv.LockedGroups.Concat(g)); Tv.SetHiddenGroups(Tv.HiddenGroups.Concat(g)); _tvUnlocked = false;
                return $"✓ {g.Count} Gruppe(n) gesperrt und ausgeblendet{(Lock.Enabled ? " – nur mit deiner PIN zu öffnen" : " – tipp: unter „Personen“ eine PIN festlegen")}.";
            }
            case "unlock-groups":
                if (Lock.Enabled && !_tvUnlocked) return "✗ Zum Entsperren unter „Quellen“ mit deiner PIN „Gesperrte Gruppen öffnen“.";
                Tv.SetLockedGroups([]); return "✓ Alle Gruppen entsperrt.";
            case "play":
            case "search":
            {
                if (TvCommands.FindChannel(all, a.Arg) is not { } c) { _tvQuery = a.Arg; _tvTab = "live"; return $"✗ Keinen Sender „{a.Arg}“ gefunden – ich habe danach gesucht."; }
                _tvTab = "live";
                if (a.Kind == "search") { _tvQuery = a.Arg; return $"✓ Suche nach „{a.Arg}“."; }
                return TvPlay(c.Id) ? $"✓ {c.Name} läuft." : _tvMsg;
            }
            case "favorite":
            {
                if (TvCommands.FindChannel(all, a.Arg) is not { } c) return $"✗ Keinen Sender „{a.Arg}“ gefunden.";
                if (!Tv.Favorites.Contains(c.Id)) Tv.ToggleFavorite(c.Id);
                return $"✓ {c.Name} ist jetzt ein Favorit.";
            }
            case "record":
            {
                var title = Regex.Replace(a.Arg, @"\b(heute|abend|morgen|bitte|den|die|das|sendung)\b", "", RegexOptions.IgnoreCase).Trim();
                var now = DateTime.UtcNow;
                var p = TvProgs().Where(x => x.Stop > now && x.Title.Contains(title, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Start).FirstOrDefault();
                if (p is null || all.FirstOrDefault(c => string.Equals(c.EpgId, p.Channel, StringComparison.OrdinalIgnoreCase)) is not { } ch)
                    return $"✗ „{title}“ habe ich im Programmführer nicht gefunden.";
                TvPlanRecording(ch, p.Title, p.Start, p.Stop);
                return $"✓ Aufnahme geplant: {p.Title} auf {ch.Name}, {p.Start.ToLocalTime():ddd HH:mm}–{p.Stop.ToLocalTime():HH:mm}.";
            }
            case "films":
            {
                _tvTab = "film"; _tvVodQuery = a.Arg;
                var cat = TvVodList().Select(v => v.Category).Distinct().FirstOrDefault(c => a.Extra.Length > 2 && c.Contains(a.Extra, StringComparison.OrdinalIgnoreCase));
                if (cat is not null) { _tvVodCat = cat; _tvVodQuery = ""; }
                return $"✓ Filme{(cat is not null ? $" in „{cat}“" : a.Arg.Length > 0 ? $" mit „{a.Arg}“" : "")}.";
            }
            case "series":
            {
                _tvTab = "serie"; _tvSerQuery = a.Arg; _tvSerSel = null;
                var cat = TvSeriesList().Select(s => s.Category).Distinct().FirstOrDefault(c => a.Extra.Length > 2 && c.Contains(a.Extra, StringComparison.OrdinalIgnoreCase));
                if (cat is not null) { _tvSerCat = cat; _tvSerQuery = ""; }
                return $"✓ Serien{(cat is not null ? $" in „{cat}“" : a.Arg.Length > 0 ? $" mit „{a.Arg}“" : "")}.";
            }
            case "live": _tvTab = "live"; return "✓ Live-TV.";
            case "guide": _tvTab = "guide"; return "✓ Programmführer – das läuft gerade.";
        }
        await Task.CompletedTask;
        return "";
    }

    static string CountryName(string code) => code switch { "de" => "deutsche", "at" => "österreichische", "ch" => "Schweizer", "uk" => "britische", "us" => "amerikanische", "tr" => "türkische", "fr" => "französische", "it" => "italienische", "es" => "spanische", "pl" => "polnische", "nl" => "niederländische", "ar" => "arabische", _ => code.ToUpperInvariant() };
}
