using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>CORE-12: Vorlagen für gute KI-Aufträge – ausfüllen, prüfen, senden, Versionen, Masterprompt-Baukasten.</summary>
public partial class MainWindow
{
    private PromptLibrary? _prompts;
    public PromptLibrary Prompts => _prompts ??= new PromptLibrary(_core.DataRoot, _core.Audit);
    private string? _promptSel; private bool _promptEdit; private string _promptMsg = "";
    public string LastPromptOutput { get; private set; } = "";
    private readonly Dictionary<string, string> _promptVals = new();

    private void InitCore12()
    {
        _showMap["ki-vorlagen"] = PromptsView; KiTabs.Add(("ki-vorlagen", "Vorlagen"));
        StudioPages["CORE-12"] = "ki-vorlagen";
        ExtraPages.Add(("KI-Vorlagen", "ki-vorlagen", ["prompt", "vorlage", "masterprompt", "auftrag"]));
    }

    private Control PromptsView()
    {
        var sp = new StackPanel { Spacing = 6, Children = { P("Fertige Vorlagen für gute KI-Aufträge. Lücken ausfüllen, prüfen, an die KI schicken.") } };
        var row = new WrapPanel();
        foreach (var t in Prompts.All())
        {
            var b = new Button { Name = "prompt-" + t.Id, Content = t.Name, Margin = new Thickness(0, 0, 6, 6), Background = _promptSel == t.Id ? Accent : null };
            ToolTip.SetTip(b, t.Category);
            b.Click += (_, _) => { _promptSel = t.Id; _promptEdit = false; _promptVals.Clear(); _promptMsg = ""; Show("ki-vorlagen"); };
            row.Children.Add(b);
        }
        var nb = new Button { Name = "prompt-new", Content = "+ Neue Vorlage", Margin = new Thickness(0, 0, 6, 6) };
        nb.Click += (_, _) => { _promptSel = null; _promptEdit = true; Show("ki-vorlagen"); };
        var mb = new Button { Name = "prompt-master", Content = "Masterprompt bauen", Margin = new Thickness(0, 0, 6, 6) };
        mb.Click += (_, _) => { _promptSel = "#master"; Show("ki-vorlagen"); };
        row.Children.Add(nb); row.Children.Add(mb);
        sp.Children.Add(row);
        var t0 = _promptSel is null ? null : Prompts.Get(_promptSel);
        if (_promptSel == "#master") sp.Children.Add(MasterBuilder());
        else if (_promptEdit) sp.Children.Add(PromptEditor(t0));
        else if (t0 is not null) sp.Children.Add(PromptFill(t0));
        sp.Children.Add(new TextBlock { Name = "prompt-msg", Text = _promptMsg, Foreground = Text, TextWrapping = TextWrapping.Wrap });
        if (LastPromptOutput != "" && t0 is not null && !_promptEdit)
            sp.Children.Add(WCard(new SelectableTextBlock { Name = "prompt-answer", Text = LastPromptOutput, Foreground = Text, TextWrapping = TextWrapping.Wrap }));
        return KiPage("Vorlagen", sp);
    }

    private Control Hints(string text)
    {
        var sp = new StackPanel { Name = "prompt-hints", Spacing = 2 };
        foreach (var h in PromptLibrary.Check(text))
            sp.Children.Add(P((h.Level == "ok" ? "✓ " : h.Level == "warn" ? "⚠ " : "💡 ") + h.Text, h.Level == "warn" ? Brush.Parse("#FACC15") : null, 11));
        return sp;
    }

    private Control PromptFill(PromptTemplate t)
    {
        var sp = new StackPanel { Spacing = 6, Children = { H($"{t.Name} (Version {t.Version})", 14) } };
        var boxes = new List<(string, TextBox)>();
        void Grab() { foreach (var (n, b) in boxes) _promptVals[n] = b.Text ?? ""; }
        foreach (var (name, def) in PromptLibrary.Variables(t.Text))
        {
            var box = new TextBox { Name = "pvar-" + name, PlaceholderText = def is null ? name : $"{name} (Standard: {def})", Text = _promptVals.GetValueOrDefault(name, ""), AcceptsReturn = name is "Text" or "Nachricht", TextWrapping = TextWrapping.Wrap };
            boxes.Add((name, box));
            sp.Children.Add(box);
        }
        var send = new Button { Name = "prompt-send", Content = "An die KI senden", Background = Accent };
        send.Click += async (_, _) =>
        {
            Grab(); var (text, missing) = PromptLibrary.Fill(t.Text, _promptVals);
            if (missing.Count > 0) { _promptMsg = "Bitte noch ausfüllen: " + string.Join(", ", missing); Show("ki-vorlagen"); return; }
            send.IsEnabled = false;
            var a = await Intel.AskAsync(new KiRequest(text, PreferredModel: KiModelName, UseKnowledge: false));
            LastPromptOutput = a.Ok ? a.Text : ""; _promptMsg = a.Ok ? $"Antwort von {a.Model}" : a.Text; Show("ki-vorlagen");
        };
        var copy = new Button { Name = "prompt-copy", Content = "Text kopieren" };
        copy.Click += async (_, _) => { Grab(); var (text, _) = PromptLibrary.Fill(t.Text, _promptVals); if (Clipboard is { } cb) await cb.SetTextAsync(text); _promptMsg = "Kopiert."; Show("ki-vorlagen"); };
        var edit = new Button { Name = "prompt-edit", Content = "Bearbeiten" };
        edit.Click += (_, _) => { _promptEdit = true; Show("ki-vorlagen"); };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { send, copy, edit } });
        sp.Children.Add(P(t.Text, null, 11));
        return WCard(sp);
    }

    private Control PromptEditor(PromptTemplate? t)
    {
        var name = new TextBox { Name = "pedit-name", PlaceholderText = "Name", Text = t?.Name ?? "" };
        var cat = new TextBox { Name = "pedit-cat", PlaceholderText = "Bereich, z. B. Briefe", Text = t?.Category ?? "" };
        var text = new TextBox { Name = "pedit-text", PlaceholderText = "Auftrag an die KI. Lücken so schreiben: {{Thema}} oder {{Anzahl|3}}", Text = t?.Text ?? "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90 };
        var hints = new ContentControl { Content = Hints(text.Text ?? "") };
        text.TextChanged += (_, _) => hints.Content = Hints(text.Text ?? "");
        var save = new Button { Name = "pedit-save", Content = "Speichern", Background = Accent };
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(text.Text) || string.IsNullOrWhiteSpace(name.Text)) { _promptMsg = "Name und Text fehlen noch."; Show("ki-vorlagen"); return; }
            var s = Prompts.Save(t?.Id, name.Text, cat.Text ?? "", text.Text); _promptSel = s.Id; _promptEdit = false; _promptMsg = $"Gespeichert als Version {s.Version}."; Show("ki-vorlagen");
        };
        var sp = new StackPanel { Spacing = 6, Children = { name, cat, text, hints, save } };
        if (t is not null)
        {
            if (!t.BuiltIn) { var del = new Button { Name = "pedit-delete", Content = "Löschen" }; del.Click += (_, _) => { Prompts.Delete(t.Id); _promptSel = null; _promptEdit = false; Show("ki-vorlagen"); }; sp.Children.Add(del); }
            if (t.Versions.Count > 1)
            {
                sp.Children.Add(H("Frühere Versionen", 13));
                foreach (var v in t.Versions.AsEnumerable().Reverse().Skip(1).Take(5))
                {
                    var r = new Button { Name = $"pedit-restore-{v.Version}", Content = $"Version {v.Version} vom {v.At:dd.MM. HH:mm} zurückholen", Padding = new Thickness(8, 2) };
                    r.Click += (_, _) => { var s = Prompts.Restore(t.Id, v.Version); _promptMsg = $"Version {v.Version} ist jetzt wieder aktuell (als Version {s.Version})."; Show("ki-vorlagen"); };
                    sp.Children.Add(r);
                }
            }
        }
        return WCard(sp);
    }

    private Control MasterBuilder()
    {
        TextBox F(string n, string ph, bool multi = false) => new() { Name = "master-" + n, PlaceholderText = ph, AcceptsReturn = multi, TextWrapping = TextWrapping.Wrap, MinHeight = multi ? 54 : 0 };
        var role = F("role", "Rolle, z. B. „ein erfahrener Steuerberater“"); var goal = F("goal", "Ziel: Was soll am Ende herauskommen?", true);
        var ctx = F("context", "Hintergrund: Was muss die KI wissen?", true); var fmt = F("format", "Form: z. B. „Tabelle mit 3 Spalten“");
        var rules = F("rules", "Regeln, eine pro Zeile", true);
        var outp = new SelectableTextBlock { Name = "master-out", Foreground = Text, TextWrapping = TextWrapping.Wrap };
        var hints = new ContentControl();
        var build = new Button { Name = "master-build", Content = "Zusammensetzen", Background = Accent };
        build.Click += (_, _) => { outp.Text = PromptLibrary.Master(role.Text ?? "", goal.Text ?? "", ctx.Text ?? "", fmt.Text ?? "", rules.Text ?? ""); hints.Content = Hints(outp.Text); };
        var save = new Button { Name = "master-save", Content = "Als Vorlage speichern" };
        save.Click += (_, _) => { if (string.IsNullOrEmpty(outp.Text)) return; var s = Prompts.Save(null, "Masterprompt " + DateTime.Now.ToString("dd.MM. HH:mm"), "Masterprompts", outp.Text); _promptSel = s.Id; _promptMsg = "Als Vorlage gespeichert."; Show("ki-vorlagen"); };
        return WCard(new StackPanel { Spacing = 6, Children = { role, goal, ctx, fmt, rules, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { build, save } }, outp, hints } });
    }
}
