using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Welle 2: KI & Automatisierung. Alle Seiten hängen unter „KI“ mit einer Reiterleiste (max. 2 Ebenen).</summary>
public partial class MainWindow
{
    public static readonly List<(string Key, string Label)> KiTabs = [("ki", "Modell"), ("ki-fragen", "Fragen")];

    public Func<IReadOnlyList<IKiBackend>>? KiBackendsOverride { get; set; }
    private IntelligenceCore? _intel;
    public IntelligenceCore Intel => _intel ??= new IntelligenceCore(_core.DataRoot, _core.Audit, () => KiBackendsOverride?.Invoke() ?? DefaultKiBackends(), KnowledgeLookup);

    private Func<string, int, IReadOnlyList<(string Title, string Text)>>? KnowledgeLookup => LookupKnowledge;
    private IReadOnlyList<IKiBackend> DefaultKiBackends() => KiSourceSettings.Load(_core.DataRoot).Active().Select(MakeBackend).ToList();

    private void InitWave23()
    {
        _showMap["ki-fragen"] = AskView;
        StudioPages["CORE-02"] = "ki-fragen";
        ExtraPages.Add(("KI fragen", "ki-fragen", ["frage", "chat", "assistent", "trendo"]));
        InitX05(); InitCore10(); InitCore12(); InitCore03(); InitSys05();
        InitWave3();
    }
    partial void InitWave3();

    private Control KiNav()
    {
        var row = new WrapPanel();
        foreach (var (k, l) in KiTabs.Where(t => _showMap.ContainsKey(t.Key)))
        {
            var b = new Button { Name = "ki-tab-" + k, Content = l, Margin = new Thickness(0, 0, 6, 6), Background = CurrentView == k ? Accent : null };
            b.Click += (_, _) => Show(k); row.Children.Add(b);
        }
        return row;
    }

    private Control KiPage(string title, Control body) => Page(title, new StackPanel { Spacing = 8, Children = { KiNav(), body } });

    private static Border WCard(Control child) => new() { Background = TileBg, CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8), Child = child };

    // ---------- CORE-02: Fragen an die KI auf diesem PC ----------
    private readonly List<KiMessage> _chat = [];
    public string LastAnswer { get; private set; } = "";
    private string _askState = "";

    private Control AskView()
    {
        var sp = new StackPanel { Spacing = 8, Children = { P("Stell eine Frage. Die Antwort kommt von der KI auf diesem PC – nichts geht ins Internet.") } };
        var log = new StackPanel { Name = "ask-log", Spacing = 6 };
        foreach (var m in _chat)
            log.Children.Add(WCard(new StackPanel { Children = { P(m.Role == "user" ? "Du" : "Trendo", m.Role == "user" ? Accent : null, 10.5), new SelectableTextBlock { Text = m.Content, Foreground = Text, FontSize = 13, TextWrapping = TextWrapping.Wrap } } }));
        var input = new TextBox { Name = "ask-input", PlaceholderText = "Was möchtest du wissen?", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 54 };
        var state = new TextBlock { Name = "ask-state", Text = _askState, Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap };
        var send = new Button { Name = "ask-send", Content = "Fragen", Background = Accent };
        var clear = new Button { Name = "ask-clear", Content = "Neues Gespräch" };
        clear.Click += (_, _) => { _chat.Clear(); Show("ki-fragen"); };
        send.Click += async (_, _) =>
        {
            var q = input.Text?.Trim() ?? "";
            if (q == "") { state.Text = "Bitte schreib zuerst eine Frage."; return; }
            send.IsEnabled = false; state.Text = "Trendo denkt nach …";
            var a = await Intel.AskAsync(new KiRequest(q, PreferredModel: KiModelName, History: _chat.ToList()));
            LastAnswer = a.Text;
            if (a.Ok) { _chat.Add(new("user", q)); _chat.Add(new("assistant", a.Text)); }
            _askState = a.Ok ? $"Antwort von {a.Model} ({a.Backend}) in {a.Took.TotalSeconds:0.0} s{(a.Sources.Count > 0 ? " · Wissen: " + string.Join(", ", a.Sources) : "")}" : a.Text;
            Show("ki-fragen");
        };
        sp.Children.Add(log);
        sp.Children.Add(input);
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { send, clear } });
        sp.Children.Add(state);
        if (_chat.Count >= 2)
        {
            var (lq, la) = (_chat[^2].Content, _chat[^1].Content);
            var good = new Button { Name = "ask-good", Content = "👍 Gut" };
            good.Click += (_, _) => { Wissen.Feedback(lq, la, true); _askState = "Danke für deine Rückmeldung."; Show("ki-fragen"); };
            var fix = new TextBox { Name = "ask-fix", PlaceholderText = "Was ist richtig? Trendo merkt es sich.", Width = 320 };
            var bad = new Button { Name = "ask-bad", Content = "Verbessern" };
            bad.Click += (_, _) => { Wissen.Feedback(lq, la, false, fix.Text); _askState = string.IsNullOrWhiteSpace(fix.Text) ? "Danke. Schreib beim nächsten Mal dazu, was richtig ist – dann lernt Trendo daraus." : "Gemerkt. Trendo nutzt die Korrektur bei der nächsten Frage."; Show("ki-fragen"); };
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { good, fix, bad } });
        }
        var hist = Intel.History(5);
        if (hist.Count > 0)
        {
            sp.Children.Add(H("Zuletzt gefragt", 14));
            foreach (var h in hist) sp.Children.Add(P($"{h.At:dd.MM. HH:mm} · {h.Question}", null, 11));
            var del = new Button { Name = "ask-history-clear", Content = "Verlauf löschen" };
            del.Click += (_, _) => { Intel.ClearHistory(); Show("ki-fragen"); };
            sp.Children.Add(del);
        }
        return KiPage("KI fragen", sp);
    }
}

