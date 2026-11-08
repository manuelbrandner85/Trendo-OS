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
    public IntelligenceCore Intel => _intel ??= NewIntel();
    IntelligenceCore NewIntel()
    {
        var i = new IntelligenceCore(_core.DataRoot, _core.Audit, () => KiBackendsOverride?.Invoke() ?? DefaultKiBackends(), KnowledgeLookup);
        i.RemoteEverywhere = () => KiSourceSettings.Load(_core.DataRoot).RemoteEverywhere;
        i.RemoteUsed = name => _ui.Post(() => RemoteKiUsed(name));
        i.Memory = KiMemory;
        return i;
    }

    private Func<string, int, IReadOnlyList<(string Title, string Text)>>? KnowledgeLookup => LookupKnowledge;
    /// <summary>Erst alle eingeschalteten Quellen auf diesem PC, danach eingeschaltete KI-Dienste mit gespeichertem Schlüssel (Rückfall, Text verlässt den PC).</summary>
    private IReadOnlyList<IKiBackend> DefaultKiBackends()
    {
        var set = KiSourceSettings.Load(_core.DataRoot);
        var list = set.Active().Select(MakeBackend).ToList();
        if (_core.Offline) return list;
        var cloud = new List<IKiBackend>();
        foreach (var id in set.Cloud)
            if (KiCatalog.Cloud.FirstOrDefault(c => c.Id == id) is { } svc && _core.Secrets.Get(svc.SecretName) is { Length: > 0 } key)
                cloud.Add(CloudBackendFactory?.Invoke(svc, key) ?? CloudKiBackend.For(svc, key));
        if (set.OpenRouterModels.TryGetValue(OpenRouterFree.Text, out var orText)) CloudKiBackend.Preferred["openrouter"] = orText; else CloudKiBackend.Preferred.TryRemove("openrouter", out _);
        var order = set.RemoteMode == "zuerst" ? [.. cloud, .. list] : new List<IKiBackend>([.. list, .. cloud]); // „zuerst“: Internet-KI bevorzugt, sonst die KI auf dem PC
        // T-0316: Claude über das Abo des Nutzers ist die Haupt-KI, solange verbunden (nie in Tests mit eigenem Datenordner); sonst übernimmt die KI auf dem PC
        if (ClaudeEmbedApproved && set.ClaudeMain && (!_opt.Isolated || ClaudeStatusOverride is not null)) order.Insert(0, new ClaudeKiBackend(this, () => KiSourceSettings.Load(_core.DataRoot).ClaudeModel));
        return order;
    }

    /// <summary>Für Tests: KI-Dienst ohne Netz nachbilden.</summary>
    public Func<KiCloudService, string, IKiBackend>? CloudBackendFactory { get; set; }

    private void InitWave23()
    {
        _showMap["ki-fragen"] = AskView;
        StudioPages["CORE-02"] = "ki-fragen";
        ExtraPages.Add(("KI fragen", "ki-fragen", ["frage", "chat", "assistent", "trendo"]));
        InitX05(); InitCore10(); InitCore12(); InitSkills(); InitConnections(); InitCore03(); InitSys05(); InitOpenRouter();
        InitWave3();
        InitWave6();
    }
    partial void InitWave3();

    private Control KiNav(bool vertical = false)
    {
        Panel row = vertical ? new StackPanel { Spacing = 4 } : new WrapPanel();
        foreach (var (k, l) in KiTabs.GroupBy(t => t.Key).Select(g => g.First()).Where(t => _showMap.ContainsKey(t.Key) && OwnTab(t.Key)))
        {
            var on = CurrentView == k;
            var b = new Button { Name = "ki-tab-" + k, Content = l, Padding = new Thickness(10, vertical ? 8 : 6), CornerRadius = new CornerRadius(10), Background = on ? Accent : Brushes.Transparent, Foreground = Text, BorderBrush = Edge, BorderThickness = new Thickness(on ? 0 : 1) };
            if (vertical) { b.HorizontalAlignment = HorizontalAlignment.Stretch; b.HorizontalContentAlignment = HorizontalAlignment.Left; }
            else b.Margin = new Thickness(0, 0, 6, 6);
            b.Click += (_, _) => Show(k); row.Children.Add(b);
        }
        if (InAppShell(CurrentView)) { row.IsVisible = false; return row; } // E2: die KI-Zentrale hat links ihre eigene Navigation
        return TabsOrNothing(row);
    }

    private Control KiPage(string title, Control body) => Page(title, new StackPanel { Spacing = 8, Children = { KiNav(), body } });

    private static Border WCard(Control child) => new() { Background = PaneBg, BorderBrush = PaneEdge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 8), Child = child, BoxShadow = CardShadow };

    // ---------- CORE-02: Fragen an die KI auf diesem PC ----------
    private readonly List<KiMessage> _chat = [];
    public string LastAnswer { get; private set; } = "";
    private string _askState = "";

    // AskView: siehe MainWindow.ChatWindow.cs (Chatfenster, Runde 7)
}

