using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>T-0315: Programm, das der Assistent bedienen kann. <see cref="Begin"/> legt eine Arbeitskopie an – Vorschau und Plan laufen dort, erst „Übernehmen“ ändert das Original.</summary>
public interface IAssistTarget
{
    string Key { get; }
    string Title { get; }
    /// <summary>Fachrolle der KI für dieses Programm.</summary>
    string Role { get; }
    IReadOnlyList<AssistAction> Actions { get; }
    IAssistSession Begin();
    /// <summary>Hinweise aus dem Inhalt für Vorschläge („Spalte B hat Lücken – auffüllen?“).</summary>
    IEnumerable<AssistSuggestion> Hints();
    /// <summary>Letzten übernommenen Assistenten-Schritt zurücknehmen.</summary>
    bool Undo();
}

public interface IAssistSession
{
    void Run(AssistStep step);
    /// <summary>Wie die Arbeitskopie jetzt aussieht (null = keine Bildvorschau möglich).</summary>
    Control? Preview();
    bool Changed { get; }
    /// <summary>In das Original übernehmen – als ein rückgängig machbarer Schritt; Schritte mit Folgen laufen erst hier.</summary>
    void Apply(string label, IReadOnlyList<AssistStep> sideEffects);
}

/// <summary>Programme der Programm-Hülle: Aktionen aus dem Menüband (ohne Ansicht, Abspielen, Bauen, Export), Arbeitskopie des Dokuments, Vorschau = echte Arbeitsfläche der Kopie.</summary>
public sealed partial class ProgramAssistTarget(ProgramSpec spec, ProgramDocument doc) : IAssistTarget
{
    static readonly string[] SkipGroups = ["Ansicht", "Datei", "Export", "Ausgabe", "Hilfe", "Werkzeug"];
    [GeneratedRegex(@"(play|run|export|build|zoom|fit|start$|end$|speed|reset|-v-|photo|2d3d|grid|dims|tiles|-100|open|save|print|import|close|share|send)", RegexOptions.IgnoreCase)] private static partial Regex Risky();
    [GeneratedRegex(@"[^\p{L}\p{N} /-]")] private static partial Regex Symbols();

    public string Key => spec.Key;
    public string Title => spec.Title;
    public string Role => $"Du bist erfahrene Fachkraft für „{spec.Title}“ in Trendo OS. {spec.Sentence}".Trim();
    public IReadOnlyList<AssistAction> Actions { get; } = spec.Ribbon.Where(g => !SkipGroups.Contains(g.Group)).SelectMany(g => g.Cmds.Where(c => !Risky().IsMatch(c.Name))
        .Select(c => new AssistAction(c.Name, Symbols().Replace(c.Label, "").Trim() is { Length: > 0 } l ? l : c.Name, [.. Symbols().Replace(c.Label + " " + g.Group + " " + (c.Tip ?? ""), " ").Split(' ', StringSplitOptions.RemoveEmptyEntries)]))).ToList();

    public IAssistSession Begin() => new Session(spec, doc);
    public IEnumerable<AssistSuggestion> Hints() => [];
    public bool Undo() => doc.CanUndo && (doc.UndoLabel?.StartsWith("KI: ", StringComparison.Ordinal) ?? false) && doc.Undo();

    /// <summary>Vorschau-Lauf am echten Dokument mit Merken/Zurückrollen – so greifen auch Befehle, die das aktive Dokument direkt bearbeiten.
    /// Nach jedem Lauf steht das Original wieder exakt wie vorher (Daten und Rückgängig-Liste); erst „Übernehmen“ setzt das Ergebnis als einen Schritt.</summary>
    sealed class Session(ProgramSpec spec, ProgramDocument doc) : IAssistSession
    {
        readonly ProgramDocument.Mark _start = doc.Remember();
        string _after = doc.Data.ToJsonString();
        Control? _preview;
        public void Run(AssistStep step)
        {
            var cmd = spec.Ribbon.SelectMany(g => g.Cmds).First(c => c.Name == step.Action.Id);
            var before = doc.Remember();
            MainWindow.AssistQuiet++;
            try
            {
                doc.Rollback(before with { Data = _after }); // auf den bisherigen Vorschau-Stand setzen
                for (var i = 0; i < step.Times; i++) cmd.Run(doc);
                _after = doc.Data.ToJsonString();
                _preview = null;
            }
            finally { doc.Rollback(_start); MainWindow.AssistQuiet--; }
        }
        public Control? Preview()
        {
            if (_preview is not null) return _preview;
            MainWindow.AssistQuiet++;
            try { doc.Rollback(_start with { Data = _after }); return _preview = spec.Workspace(doc); } // Bild im Ergebnis-Stand aufbauen …
            finally { doc.Rollback(_start); MainWindow.AssistQuiet--; } // … und sofort zurück
        }
        public bool Changed => _after != _start.Data;
        public void Apply(string label, IReadOnlyList<AssistStep> sideEffects)
        {
            var after = JsonNode.Parse(_after)!.AsObject();
            doc.Edit("KI: " + label, d => { d.Clear(); foreach (var (k, v) in after.ToList()) { after.Remove(k); d[k] = v; } });
        }
    }
}

public partial class MainWindow
{
    /// <summary>&gt; 0 während der Assistent Befehle für Vorschau/Übernehmen ausführt: Programme zeichnen dann nicht neu.</summary>
    public static int AssistQuiet;
    /// <summary>Eigene Anbindungen von Programmen außerhalb der Programm-Hülle (Seite → Ziel).</summary>
    readonly Dictionary<string, Func<IAssistTarget?>> _assistAdapters = new(StringComparer.OrdinalIgnoreCase);

    public IAssistTarget? AssistTargetFor(string? key)
    {
        if (key is null) return null;
        if (_assistAdapters.TryGetValue(key, out var f)) return f();
        if (Programs.TryGetValue(key, out var spec) && ProgramActive(key) is { } doc && new ProgramAssistTarget(spec, doc) is { Actions.Count: > 0 } t) return t;
        return null;
    }

    /// <summary>Zustand eines laufenden Assistenten-Auftrags (je Fenster/Leiste einer).</summary>
    sealed class AssistRun(IAssistTarget target, AssistPlan plan, AssistPrefs prefs)
    {
        public IAssistTarget Target = target; public AssistPlan Plan = plan; public AssistPrefs Prefs = prefs;
        public IAssistSession? Session; public Control? Preview;
        public string Phase = "plan"; // plan → fragen → läuft → vorschau → fertig | fehler | verworfen
        public readonly List<string> StepState = []; public readonly List<string> Log = [];
        public bool Stop; public string Message = ""; public List<AssistSuggestion> Suggestions = [];
        public readonly HashSet<string> Remember = [];
        public ContentControl Host = new();
    }

    /// <summary>Neue Leiste einer Seite: laufenden Auftrag wieder anzeigen.</summary>
    void AssistAttach(string? key, ContentControl host)
    {
        if (key is null || !_assistByKey.TryGetValue(key, out var r) || r.Phase is "verworfen" && r.Message.Length == 0) return;
        _assistRuns.Remove(r.Host); r.Host = host; _assistRuns[host] = r;
        AssistRender(r, host);
    }

    readonly Dictionary<ContentControl, AssistRun> _assistRuns = [];
    /// <summary>Laufender Auftrag je Seite – überlebt das Neuzeichnen der Seite (neue Leiste übernimmt ihn).</summary>
    readonly Dictionary<string, AssistRun> _assistByKey = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Eingetippter Auftrag und Live-Zeile je Seite (überleben das Neuzeichnen).</summary>
    readonly Dictionary<string, (string Prompt, string Live)> _assistTyped = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Für Tests: Verzögerung zwischen den Schritten (sichtbares Abhaken).</summary>
    public static int AssistStepDelayMs { get; set; } = 140;
    /// <summary>Pause nach dem letzten Tastendruck bis zur Live-Vorschau.</summary>
    public static int AssistLiveDelayMs { get; set; } = 450;
    /// <summary>Für Tests: letzter Assistent-Lauf (Phase, Schritte, Meldung).</summary>
    public IReadOnlyList<string> AssistLog => _assistRuns.Values.LastOrDefault()?.Log ?? [];
    public (string Phase, IReadOnlyList<string> Steps, string Message)? AssistLast => _assistRuns.Values.LastOrDefault() is { } r ? (r.Phase, r.Plan.Steps.Select(s => s.Text).ToList(), r.Message) : null;

    /// <summary>Auftrag an das offene Programm: Plan ohne KI, sonst KI (Claude, wenn verbunden – sonst lokal) mit Rolle des Programms. False = nicht verstanden (dann übernimmt der Studio-Ablauf).</summary>
    async Task<bool> AssistStart(IAssistTarget t, string prompt, ContentControl host)
    {
        var prefs = new AssistPrefs(_core.DataRoot, t.Key);
        var plan = AssistPlanner.Plan(prompt, t.Actions, prefs.Values);
        if (!plan.Understood && !_core.Offline && prompt.Trim().Length > 3)
        {
            var role = t.Role + StudioTeams.Brief(ClaudeBelongs(t.Key) ? ClaudeStudioOf(t.Key) : null, prompt); // T-0630: das Team des Studios arbeitet ohne Auswahl mit
            var ki = await Intel.AskAsync(new KiRequest(prompt, AssistPlanner.KiPrompt(t.Title, t.Actions, role, SkillsFor(t.Key)), UseKnowledge: false));
            if (ki.Ok) { var kp = AssistPlanner.FromKi(prompt, ki.Text, t.Actions); kp.Source = $"KI ({ki.Backend})"; if (kp.Understood) plan = kp; }
        }
        if (!plan.Understood) return false;
        var run = new AssistRun(t, plan, prefs) { Host = host };
        _assistRuns[host] = run; _assistByKey[t.Key] = run;
        _core.Audit.Append(Principal.LocalOwner.Name, "assist.plan", $"{t.Key} schritte={plan.Steps.Count} fragen={plan.Questions.Count} quelle={plan.Source}");
        await AssistAdvance(run, host);
        return true;
    }

    /// <summary>Hinweis auf Fachregeln (Skills, T-0317) – bis dahin leer.</summary>
    string? SkillsFor(string key) => SkillTextFor?.Invoke(key);
    public Func<string, string?>? SkillTextFor { get; set; }

    async Task AssistAdvance(AssistRun r, ContentControl host)
    {
        if (r.Plan.Questions.Count > 0) { r.Phase = "fragen"; AssistRender(r, host); return; }
        if (r.Plan.Steps.Count == 0) { r.Phase = "verworfen"; r.Message = "Nichts mehr zu tun."; AssistRender(r, host); return; }
        r.Phase = "läuft"; r.StepState.Clear(); r.StepState.AddRange(r.Plan.Steps.Select(_ => "offen")); r.Log.Clear();
        r.Session = r.Target.Begin();
        AssistRender(r, host);
        for (var i = 0; i < r.Plan.Steps.Count; i++)
        {
            if (r.Stop) { r.Phase = "verworfen"; r.Message = "Gestoppt – nichts übernommen."; AssistRender(r, host); return; }
            var s = r.Plan.Steps[i];
            if (s.Action.SideEffect) { r.StepState[i] = "folgt"; r.Log.Add($"„{s.Text}“ wird erst beim Übernehmen ausgeführt."); continue; }
            r.StepState[i] = "läuft"; r.Log.Add($"Ich {AssistVerb(s)} …"); AssistRender(r, host);
            if (AssistStepDelayMs > 0) await Task.Delay(AssistStepDelayMs);
            try { r.Session.Run(s); r.StepState[i] = "fertig"; }
            catch (Exception ex) when (ex is not OutOfMemoryException) { r.StepState[i] = "fehler"; r.Phase = "fehler"; r.Message = $"„{s.Text}“ hat nicht geklappt: {ex.Message}"; AssistRender(r, host); return; }
        }
        try { r.Preview = r.Session.Preview(); } catch (Exception ex) when (ex is not OutOfMemoryException) { r.Preview = null; r.Log.Add("Keine Bildvorschau möglich: " + ex.Message); }
        r.Phase = "vorschau";
        r.Message = r.Session.Changed || r.Plan.HasSideEffects ? "So sieht es aus. Behalten?" : "Das ändert nichts am Dokument.";
        AssistRender(r, host);
    }

    static string AssistVerb(AssistStep s) => "führe aus: " + s.Text;

    void AssistApply(AssistRun r, ContentControl host)
    {
        if (r.Session is null) return;
        var label = r.Plan.Prompt.Length > 48 ? r.Plan.Prompt[..48] + "…" : r.Plan.Prompt;
        try { r.Session.Apply(label, r.Plan.Steps.Where(s => s.Action.SideEffect).ToList()); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { r.Phase = "fehler"; r.Message = "Übernehmen hat nicht geklappt: " + ex.Message; AssistRender(r, host); return; }
        _core.Audit.Append(Principal.LocalOwner.Name, "assist.apply", $"{r.Target.Key} „{label}“");
        r.Phase = "fertig"; r.Message = "✓ Behalten. Rückgängig geht hier oder mit Strg+Z.";
        r.Suggestions = AssistPlanner.Suggest(r.Plan.Steps.Select(s => s.Action), r.Target.Actions, r.Target.Hints()).ToList();
        var page = CurrentView; Show(page); // neue Leiste übernimmt das Panel (AssistAttach)
    }

    /// <summary>Panel wie in Claude Code: Plan mit Live-Haken, Protokoll, Rückfragen als Knöpfe, Vorschau, Übernehmen/Verwerfen, Vorschläge, gemerkte Vorlieben.</summary>
    void AssistRender(AssistRun r, ContentControl host)
    {
        host = r.Host;
        var sp = new StackPanel { Name = "assist-panel", Spacing = 6 };
        var head = new WrapPanel { Children = {
            new TextBlock { Text = $"Assistent · {r.Target.Title}", Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 13, VerticalAlignment = VerticalAlignment.Center },
            new TextBlock { Name = "assist-source", Text = $"   verstanden über: {r.Plan.Source}" + (r.Plan.Source == "Regeln" ? " (ohne KI)" : ""), Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center } } };
        sp.Children.Add(head);
        for (var i = 0; i < r.Plan.Steps.Count; i++)
        {
            var st = i < r.StepState.Count ? r.StepState[i] : "offen";
            var (icon, col) = st switch { "fertig" => ("✓", "#4ADE80"), "läuft" => ("◐", "#60A5FA"), "fehler" => ("✗", "#F87171"), "folgt" => ("⏵", "#FACC15"), _ => ("○", "#94A3B8") };
            var s = r.Plan.Steps[i];
            sp.Children.Add(new StackPanel { Name = $"assist-step-{i}", Orientation = Orientation.Horizontal, Spacing = 8, Children = {
                new TextBlock { Text = icon, Foreground = Brush.Parse(col), FontSize = 14, Width = 16 },
                new TextBlock { Text = s.Text + (s.FromMemory.Count > 0 ? "  (gemerkt)" : "") + (s.Action.SideEffect ? "  – wirkt nach außen" : ""), Foreground = Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap } } });
        }
        if (r.Plan.Unclear.Count > 0) sp.Children.Add(P("Nicht verstanden und ausgelassen: " + string.Join(" · ", r.Plan.Unclear.Select(u => $"„{u}“")), Brush.Parse("#FACC15"), 11.5));
        if (r.Log.Count > 0) sp.Children.Add(new TextBlock { Name = "assist-log", Text = string.Join("\n", r.Log.TakeLast(3)), Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });

        // Rückfragen als Knöpfe
        var qi = 0;
        foreach (var q in r.Plan.Questions)
        {
            var qq = q; var idx = qi++;
            var row = new WrapPanel();
            var order = q.Options.Select((o, n) => (o, n)).OrderBy(x => x.n == q.Recommended ? 0 : 1).ThenBy(x => x.n).ToList();
            foreach (var (o, n) in order)
            {
                var opt = o;
                var b = Act($"assist-q-{idx}-{n}", n == q.Recommended && q.Options.Count > 1 ? opt + " (Empfohlen)" : opt, () => AssistAnswer(r, host, qq, opt), n == q.Recommended);
                b.Margin = new Thickness(0, 0, 6, 4); row.Children.Add(b);
            }
            if (q.AllowOther)
            {
                var other = new TextBox { Name = $"assist-q-{idx}-text", PlaceholderText = q.Options.Count == 0 ? "Deine Antwort …" : "Andere …", Width = 200, Margin = new Thickness(0, 0, 6, 4) };
                other.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) AssistAnswer(r, host, qq, other.Text ?? ""); };
                row.Children.Add(other);
                var ok = Act($"assist-q-{idx}-ok", "OK", () => AssistAnswer(r, host, qq, other.Text ?? "")); ok.Margin = new Thickness(0, 0, 6, 4); row.Children.Add(ok);
            }
            if (!q.Confirm && q.Param is not null)
            {
                var rem = new CheckBox { Name = $"assist-q-{idx}-remember", Content = "Immer so", Foreground = Muted, IsChecked = r.Remember.Contains(q.Id), Margin = new Thickness(4, 0, 0, 4) };
                rem.IsCheckedChanged += (_, _) => { if (rem.IsChecked == true) r.Remember.Add(qq.Id); else r.Remember.Remove(qq.Id); };
                row.Children.Add(rem);
            }
            sp.Children.Add(new Border { Name = $"assist-q-{idx}", Background = Brush.Parse(q.Confirm ? "#1AF5A524" : "#1AC9A227"), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 8),
                Child = new StackPanel { Spacing = 6, Children = { new TextBlock { Text = q.Text, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap }, row } } });
        }

        // Vorschau und Entscheidung
        if (r.Phase == "vorschau" && r.Preview is { } pv && (pv.Parent is not Decorator old || (old.Child = null) is null))
            sp.Children.Add(new Border { Name = "assist-preview", Height = 260, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), ClipToBounds = true, Background = TileBg,
                Child = new Viewbox { Stretch = Stretch.Uniform, Child = new Border { Width = 1100, Height = 650, Child = pv } } });
        var bar = new WrapPanel();
        void B(Control c) { c.Margin = new Thickness(0, 0, 6, 0); bar.Children.Add(c); }
        if (r.Phase == "läuft") B(Act("assist-stop", "Stopp", () => r.Stop = true));
        if (r.Phase == "vorschau")
        {
            var apply = Act("assist-apply", "Ja, behalten", () => AssistApply(r, host), true); apply.IsEnabled = r.Session?.Changed == true || r.Plan.HasSideEffects; B(apply);
            B(Act("assist-discard", "Nein", () => { r.Phase = "verworfen"; r.Message = "Verworfen – nichts geändert."; _core.Audit.Append(Principal.LocalOwner.Name, "assist.discard", r.Target.Key); AssistRender(r, host); }));
        }
        if (r.Phase == "fertig") B(Act("assist-undo", "Rückgängig", () => { if (r.Target.Undo()) { r.Message = "Zurückgenommen."; r.Phase = "verworfen"; var p = CurrentView; Show(p); } else { r.Message = "Nichts zum Zurücknehmen."; AssistRender(r, host); } }));
        if (r.Phase is "fertig" or "verworfen" or "fehler" or "fragen") B(Act("assist-close", "Schließen", () => { _assistRuns.Remove(r.Host); _assistByKey.Remove(r.Target.Key); r.Host.Content = null; }));
        if (bar.Children.Count > 0) sp.Children.Add(bar);
        if (r.Message.Length > 0) sp.Children.Add(new TextBlock { Name = "assist-msg", Text = r.Message, Foreground = r.Phase == "fehler" ? Brush.Parse("#F87171") : Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });

        // T-0317: Prüfung nach Skill – Checkliste der passenden Fachregeln vor dem Übernehmen
        if (r.Phase is "vorschau" or "fertig" && SkillChecklist(r.Target.Key) is { Count: > 0 } checks)
        {
            var list = new StackPanel { Spacing = 2 };
            foreach (var (skill, item) in checks) list.Children.Add(new CheckBox { Content = $"{item}  ({skill})", Foreground = Text, FontSize = 11.5 });
            sp.Children.Add(new Expander { Name = "assist-checklist", Header = $"Prüfen nach Skill ({checks.Count} Punkte)", Content = list, IsExpanded = r.Phase == "vorschau" });
        }

        // Vorschläge
        if (r.Suggestions.Count > 0)
        {
            var row = new WrapPanel(); var k = 0;
            row.Children.Add(new TextBlock { Text = "Als Nächstes:", Foreground = Muted, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            foreach (var s in r.Suggestions) { var ss = s; var c = Act($"assist-sug-{k++}", ss.Text, () => _ = AssistStart(r.Target, ss.Prompt, host)); c.Margin = new Thickness(0, 0, 6, 0); row.Children.Add(c); }
            sp.Children.Add(row);
        }

        // Gemerkte Vorlieben
        if (r.Prefs.All.Count > 0)
        {
            var list = new StackPanel { Spacing = 3 }; var n = 0;
            foreach (var it in r.Prefs.All) { var key = it.Key; list.Children.Add(new WrapPanel { Children = { P(it.Text, Text, 11.5), Act($"assist-pref-del-{n++}", "✕", () => { r.Prefs.Forget(key); AssistRender(r, host); }) } }); }
            sp.Children.Add(new Expander { Name = "assist-prefs", Header = $"Gemerkt ({r.Prefs.All.Count})", Content = list });
        }
        host.Content = new Border { Background = Brush.Parse("#0F3F3F3F"), BorderBrush = GoldEdge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10), Margin = new Thickness(0, 4, 0, 4), Child = sp };
    }

    void AssistAnswer(AssistRun r, ContentControl host, AssistQuestion q, string value)
    {
        try { AssistPlanner.Answer(r.Plan, q.Id, value, r.Prefs, r.Remember.Contains(q.Id)); r.Message = ""; }
        catch (ArgumentException e) { r.Message = e.Message; AssistRender(r, host); return; }
        _ = AssistAdvance(r, host);
    }

    /// <summary>Echtzeit-Vorschau beim Tippen: was der Assistent tun würde (ohne KI, ohne etwas zu ändern), dazu ein kleines Bild der Arbeitskopie.</summary>
    (string Text, Control? Thumb) AssistLive(IAssistTarget t, string prompt)
    {
        if (prompt.Trim().Length < 3) return ("", null);
        var plan = AssistPlanner.Plan(prompt, t.Actions, new AssistPrefs(_core.DataRoot, t.Key).Values);
        if (!plan.Understood) return ("Noch nicht verstanden – „Plan erstellen“ fragt die KI.", null);
        var text = "Ich würde: " + string.Join(" → ", plan.Steps.Select(s => s.Text)) + (plan.Questions.Count > 0 ? $"  ·  {plan.Questions.Count} Rückfrage(n)" : "");
        if (!plan.Ready || plan.HasSideEffects) return (text, null);
        try
        {
            var s = t.Begin(); foreach (var st in plan.Steps) s.Run(st);
            return (text, s.Changed && s.Preview() is { } c ? new Border { Name = "assist-live-thumb", Width = 240, Height = 140, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), ClipToBounds = true,
                Child = new Viewbox { Stretch = Stretch.Uniform, Child = new Border { Width = 1100, Height = 650, Child = c } } } : null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return (text, null); }
    }
}

public partial class MainWindow
{
    /// <summary>Eigene Anbindung eines Programms außerhalb der Programm-Hülle anmelden (Seite → Ziel).</summary>
    public void RegisterAssistAdapter(string key, Func<IAssistTarget?> target) => _assistAdapters[key] = target;
}

public partial class MainWindow
{
    ContentControl AssistAttached(string? key, ContentControl host) { AssistAttach(key, host); return host; }
}
