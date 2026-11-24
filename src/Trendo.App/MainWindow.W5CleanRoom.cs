using Avalonia.Platform.Storage;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>X-10: Sauberer Nachbau.</summary>
public partial class MainWindow
{
    public string CleanFolder => WorkFolder("Sauberer Nachbau");
    private string? _cleanOpen;
    private IReadOnlyList<Similar>? _cleanSims;

    private void InitW5CleanRoom() => RegisterTab(BusinessTabs, "nachbau", "Sauberer Nachbau", "X-10", CleanView, "clean room", "nachbau", "nachbauen", "urheberrecht", "aehnlichkeit", "plagiat");

    public void CleanCheck(string original)
    {
        if (_cleanOpen is null || !Directory.Exists(original)) { Say("Wähle einen Original-Ordner, der existiert.", "nachbau"); return; }
        _cleanSims = CleanRoom.Compare(Path.Combine(_cleanOpen, "2-Umsetzung"), original);
        _cleanTheirs = Path.GetFullPath(original); _cleanSimSel = 0;
        var rep = CleanRoom.Report(_cleanOpen, _cleanSims, original);
        CleanRoom.Log(_cleanOpen, "Prüfen", $"Ähnlichkeitsprüfung: {_cleanSims.Count} auffällige Dateien");
        Say(_cleanSims.Count == 0 ? "Keine auffällige Ähnlichkeit gefunden. Bericht gespeichert." : $"{_cleanSims.Count} Dateien sind dem Original sehr ähnlich. Bitte prüfen.", "nachbau");
    }

    /// <summary>Sauberer Nachbau als Projekt-Arbeitsfläche: links Projekte und Regeln, Mitte die drei Schritte (Beschreibung → Umsetzung → Prüfung)
    /// mit Beschreibung direkt bearbeitbar, rechts das Tagebuch mit Rollen und Zeit. Keine Rechtsberatung.</summary>
    private Control CleanView()
    {
        var left = new StackPanel { Spacing = 8 };
        var name = new TextBox { Name = "clean-name", PlaceholderText = "Name deines Projekts" }; var orig = new TextBox { Name = "clean-original", PlaceholderText = "Vorbild, z. B. ein bekannter Notizblock" };
        left.Children.Add(H("Neues Projekt", 13));
        left.Children.Add(name); left.Children.Add(orig);
        left.Children.Add(Act("clean-create", "＋ Anlegen", () =>
        {
            try { _cleanOpen = CleanRoom.Create(CleanFolder, string.IsNullOrWhiteSpace(name.Text) ? "Nachbau" : name.Text, orig.Text ?? ""); _cleanSims = null; Say("Angelegt. Schritt 1: Beschreibung schreiben.", "nachbau"); }
            catch (Exception ex) { Say("Hat nicht geklappt: " + Trendo.Core.FriendlyError.Say(ex), "nachbau"); }
        }, true));
        var dirs = Directory.GetDirectories(CleanFolder).OrderByDescending(Directory.GetLastWriteTime).ToList();
        _cleanOpen ??= dirs.FirstOrDefault();
        left.Children.Add(H($"Projekte ({dirs.Count})", 13));
        var n = 0;
        foreach (var d in dirs.Take(20))
        {
            var dir = d; var on = dir == _cleanOpen;
            var b = new Button { Name = $"clean-open-{n++}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Avalonia.Thickness(8, 5), Background = on ? Brush.Parse("#26D4A84B") : Brushes.Transparent, BorderThickness = new Avalonia.Thickness(0),
                Content = new StackPanel { Children = { new TextBlock { Text = Path.GetFileName(dir), Foreground = Text, FontSize = 13, FontWeight = on ? FontWeight.SemiBold : FontWeight.Normal }, new TextBlock { Text = Directory.GetLastWriteTime(dir).ToString("dd.MM.yyyy HH:mm"), Foreground = Muted, FontSize = 11 } } } };
            b.Click += (_, _) => { _cleanOpen = dir; _cleanSims = null; Show("nachbau"); }; Hover(b);
            left.Children.Add(b);
        }
        if (dirs.Count == 0) left.Children.Add(P("Noch keine – oben anlegen.", Muted, 11.5));
        var rules = new StackPanel { Spacing = 2 };
        foreach (var r in CleanRoom.Rules) rules.Children.Add(P("• " + r, null, 11));
        left.Children.Add(new Expander { Name = "clean-rules", Header = new TextBlock { Text = "Die Regeln", Foreground = Text, FontSize = 12.5 }, Content = rules, HorizontalAlignment = HorizontalAlignment.Stretch });

        Control center; Control right;
        if (_cleanOpen is { } open && Directory.Exists(open))
        {
            var specFile = Path.Combine(open, "1-Beschreibung", "BESCHREIBUNG.md");
            var spec = new TextBox { Name = "clean-spec-text", Text = File.Exists(specFile) ? File.ReadAllText(specFile) : "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 220, FontSize = 13 };
            var cl = CleanLink; LinkForm(cl, open, ("spec", spec)); // T-0711: Entwurf, Rückgängig, Notsicherung
            var specSave = Act("clean-spec-save", "Beschreibung speichern", () => { Directory.CreateDirectory(Path.GetDirectoryName(specFile)!); AtomicFile.WriteAllText(specFile, spec.Text ?? ""); LinkClose(cl); CleanRoom.Log(open, CleanRoom.Roles[0], "Beschreibung geändert"); Say("Beschreibung gespeichert.", "nachbau"); }, true);
            var impl = Path.Combine(open, "2-Umsetzung");
            var implFiles = Directory.Exists(impl) ? Directory.GetFiles(impl, "*", SearchOption.AllDirectories).Length : 0;
            Border Step(string nr, string title, string who, bool done, Control body) => new() { Background = TileBg, BorderBrush = done ? Brush.Parse("#664ADE80") : EdgeLit, BorderThickness = new Avalonia.Thickness(1), CornerRadius = new Avalonia.CornerRadius(14), Padding = new Avalonia.Thickness(14, 10),
                Child = new StackPanel { Spacing = 6, Children = { new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = {
                    new Border { Width = 28, Height = 28, CornerRadius = new Avalonia.CornerRadius(14), Background = done ? Brush.Parse("#4ADE80") : Accent, Child = new TextBlock { Text = done ? "✓" : nr, Foreground = Brushes.White, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } },
                    new TextBlock { Text = title, Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = "· " + who, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } }, body } } };
            var theirs = new TextBox { Name = "clean-theirs", PlaceholderText = "Ordner mit dem Original-Code (nur zum Vergleich)" };
            var check = new StackPanel { Spacing = 6, Children = { P("Vergleicht deinen Code in „2-Umsetzung“ mit dem Original. Umbenennen von Namen hilft dabei nicht.", null, 11.5),
                new DockPanel { Children = { Dk(Act("clean-check", "Prüfen", () => CleanCheck(theirs.Text?.Trim('"', ' ') ?? ""), true), Avalonia.Controls.Dock.Right), theirs } } } };
            if (_cleanSims is { } sims)
            {
                if (sims.Count == 0) check.Children.Add(P("✓ Keine auffällige Ähnlichkeit gefunden.", Brush.Parse("#4ADE80"), 12));
                var si = 0;
                foreach (var sm in sims.Take(10))
                {
                    var idx = si;
                    var b = new Button { Name = $"clean-sim-{si++}", Content = $"⚠ {sm.Ours} ↔ {sm.Theirs}: {sm.Score:P0} gleich", Foreground = Brush.Parse("#F97316"), FontSize = 11.5, Background = idx == _cleanSimSel ? Brush.Parse("#26D4A84B") : Brushes.Transparent, BorderThickness = new Avalonia.Thickness(0), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
                    b.Click += (_, _) => { _cleanSimSel = idx; Show("nachbau"); };
                    check.Children.Add(b);
                }
                if (sims.Count > 0) check.Children.Add(CleanSideBySideView()); // T-0762: Treffer nebeneinander
            }
            center = new ScrollViewer { Content = new StackPanel { Spacing = 10, Children = {
                new DockPanel { Children = { Dk(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Act("clean-spec", "Beschreibung als Datei", () => Launch(specFile)), Act("clean-folder", "Ordner", () => Launch(open)) } }, Avalonia.Controls.Dock.Right),
                    new TextBlock { Text = Path.GetFileName(open), Foreground = Text, FontSize = 22, FontWeight = FontWeight.SemiBold } } },
                Step("1", "Beschreibung", "wer das Original kennt – schreibt nur WAS es tut, kein Code", spec.Text?.Trim().Length > 40, new StackPanel { Spacing = 6, Children = { LinkUndoRow(cl), spec, specSave } }),
                Step("2", "Anforderungen", "jede Anforderung mit Herkunft, Datum und verknüpftem Test", CleanReq.Items.Count > 0 && CleanReq.Items.All(r => r.Status == CleanRoom.StatusDone), CleanRequirementsPanel(open)),
                Step("3", "Umsetzung", "wer das Original NICHT gesehen hat – baut nur nach der Beschreibung", implFiles > 0, new StackPanel { Spacing = 4, Children = {
                    P(implFiles == 0 ? "Noch keine Dateien in „2-Umsetzung“. Dort entsteht der eigene Code." : $"{implFiles} Dateien in „2-Umsetzung“.", Text, 12.5), Act("clean-impl-folder", "Umsetzungs-Ordner öffnen", () => { Directory.CreateDirectory(impl); Launch(impl); }),
                    Act("clean-impl-code", "Im Code-Editor öffnen", () => { Directory.CreateDirectory(impl); CodeOpenFolder(impl); Show(CEK); }) } }),
                Step("4", "Prüfung", "Ähnlichkeit mit dem Original messen, vor jeder Veröffentlichung", _cleanSims is { Count: 0 }, check) } } };
            var role = new ComboBox { Name = "clean-role", ItemsSource = CleanRoom.Roles, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
            var note = new TextBox { Name = "clean-note", PlaceholderText = "Was hast du gemacht?" };
            var journal = new StackPanel { Name = "clean-journal", Spacing = 6 };
            foreach (var e in CleanRoom.Journal(open).TakeLast(30).Reverse())
                journal.Children.Add(new Border { Background = Brush.Parse("#10FFFFFF"), CornerRadius = new Avalonia.CornerRadius(8), Padding = new Avalonia.Thickness(8, 5), Child = new StackPanel { Children = {
                    new TextBlock { Text = $"{e.At:dd.MM. HH:mm} · {e.Role}", Foreground = GoldText(), FontSize = 11 }, new TextBlock { Text = e.Text, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap } } } });
            right = new ScrollViewer { Content = new StackPanel { Spacing = 8, Children = { H("Tagebuch", 14), role, note, Act("clean-log", "Ins Tagebuch", () => { CleanRoom.Log(open, CleanRoom.Roles[Math.Max(0, role.SelectedIndex)], note.Text ?? ""); Show("nachbau"); }), journal } } };
        }
        else
        {
            center = new StackPanel { Spacing = 10, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Children = {
                new TextBlock { Text = "🧪", FontSize = 52, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = "Sauber nachbauen", Foreground = Text, FontSize = 20, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
                P("Baue eine Idee neu, ohne fremden Code zu kopieren: Beschreibung, Umsetzung und Prüfung in getrennten Rollen, mit Tagebuch. Links ein Projekt anlegen.", Muted, 12.5) } };
            right = P("Das Tagebuch erscheint, sobald ein Projekt offen ist.", Muted, 12);
        }
        if (LinkRecoveryCard(CleanLink) is { } ccard) left.Children.Insert(0, ccard); // T-0711: nicht gespeicherte Beschreibung
        var grid = new Grid { Name = "clean-workspace", ColumnDefinitions = new ColumnDefinitions("280,14,*,14,300"), Children = { Card(new ScrollViewer { Content = left }), Pos(center, 2), Pos(Card(right), 4) } };
        return BusinessPage("Sauberer Nachbau", grid);
    }

    // ───── T-0762 (X-10): Anforderungsliste, Abgleich mit Tests, Vergleich nebeneinander ─────

    private CleanRoom.RequirementsDoc? _cleanReqDoc;
    private string? _cleanTheirs;
    private int _cleanSimSel;
    private string _cleanReqMsg = "";
    /// <summary>Für Tests: echte Testläufe auch im isolierten Testfenster erlauben (sonst startet Trendo dort keine Programme).</summary>
    public bool CleanRealToolsInTests { get; set; }

    /// <summary>Anforderungsliste des offenen Projekts (lädt neu, wenn ein anderes Projekt offen ist).</summary>
    public CleanRoom.RequirementsDoc CleanReq
    {
        get
        {
            var open = _cleanOpen ?? throw new InvalidOperationException("Erst ein Projekt anlegen oder öffnen.");
            if (_cleanReqDoc is null || _cleanReqDoc.Dir != open) _cleanReqDoc = new CleanRoom.RequirementsDoc(open);
            return _cleanReqDoc;
        }
    }
    public string CleanReqMessage => _cleanReqMsg;

    public void CleanOpen(string dir) { _cleanOpen = Path.GetFullPath(dir); _cleanSims = null; _cleanReqDoc = null; }

    public Requirement CleanReqAdd(string function = "", string source = "eigene Beobachtung")
    {
        var r = CleanReq.Add(function, source);
        CleanRoom.Log(_cleanOpen!, CleanRoom.Roles[0], $"Anforderung {r.Id} angelegt");
        return r;
    }

    public void CleanReqUndo() { _cleanReqMsg = CleanReq.Undo() ? "Letzte Änderung zurückgenommen." : "Nichts zum Zurücknehmen."; }

    public string CleanReqExport()
    {
        var p = CleanRoom.ExportRequirements(_cleanOpen ?? throw new InvalidOperationException("Erst ein Projekt öffnen."));
        _cleanReqMsg = $"Als Markdown gespeichert: {Path.GetRelativePath(_cleanOpen, p)}";
        _core.Audit.Append(Principal.LocalOwner.Name, "nachbau.anforderungen.export", Path.GetFileName(_cleanOpen));
        return p;
    }

    CancellationTokenSource? _cleanRunCts;
    /// <summary>Läuft gerade ein Abgleich (Testlauf)?</summary>
    public bool CleanReconcileRunning => _cleanRunCts is not null;
    /// <summary>B3: laufenden Testlauf abbrechen – beendet den Prozessbaum, Status bleibt unverändert.</summary>
    public void CleanReqCancel() => _cleanRunCts?.Cancel();

    /// <summary>Abgleich: erst echter Testlauf in „2-Umsetzung“ (wenn Tests und Werkzeug da sind; führt Code aus „2-Umsetzung“ mit deinen Rechten aus),
    /// dann je Anforderung erfüllt/teilweise/offen. Die Liste wird vor dem Warten festgehalten – ein Projektwechsel schreibt nie in die falsche Liste (B3).</summary>
    public async Task CleanReqReconcile()
    {
        if (_cleanRunCts is not null) return;
        var open = _cleanOpen ?? throw new InvalidOperationException("Erst ein Projekt öffnen.");
        var doc = CleanReq; // fest: dieses Projekt, diese Liste
        var cts = _cleanRunCts = new CancellationTokenSource();
        try
        {
            _cleanReqMsg = "Abgleich läuft – führt Code aus „2-Umsetzung“ mit deinen Rechten aus. „Abbrechen“ beendet ihn."; if (CurrentView == "nachbau") Show("nachbau");
            DevBuild.TestRun? run = null; string log;
            if (_opt.Isolated && !CleanRealToolsInTests) log = "Testmodus: Trendo startet keine Programme – Abgleich ohne Testlauf.";
            else (run, log) = await CleanRoom.RunTestsAsync(open, cts.Token);
            if (cts.IsCancellationRequested) { _cleanReqMsg = $"Abgleich für „{Path.GetFileName(open)}“ abgebrochen – Status unverändert."; return; }
            var after = CleanRoom.ReconcileAll(open, doc.Items, run);
            doc.Change(_ => after);
            var done = after.Count(r => r.Status == CleanRoom.StatusDone);
            _cleanReqMsg = $"Abgleich „{Path.GetFileName(open)}“: {done} erfüllt · {after.Count(r => r.Status == CleanRoom.StatusPartly)} teilweise · {after.Count(r => r.Status == CleanRoom.StatusOpen)} offen. {log.Split('\n').Last()}";
            CleanRoom.Log(open, CleanRoom.Roles[2], $"Abgleich der Anforderungen: {done} von {after.Count} erfüllt");
            _core.Audit.Append(Principal.LocalOwner.Name, "nachbau.abgleich", $"{Path.GetFileName(open)}: {done}/{after.Count}");
        }
        finally
        {
            _cleanRunCts = null; cts.Dispose();
            if (CurrentView == "nachbau") Show("nachbau");
        }
    }

    /// <summary>Ausgewählter Treffer der Ähnlichkeitsprüfung nebeneinander: Dateien und markierte Zeilen (1-basiert).</summary>
    public (string Ours, string Theirs, IReadOnlyCollection<int> OursLines, IReadOnlyCollection<int> TheirsLines)? CleanSideBySide
    {
        get
        {
            if (_cleanOpen is null || _cleanTheirs is null || _cleanSims is not { Count: > 0 } sims) return null;
            var s = sims[Math.Clamp(_cleanSimSel, 0, sims.Count - 1)];
            var ours = Path.Combine(_cleanOpen, "2-Umsetzung", s.Ours); var theirs = Path.Combine(_cleanTheirs, s.Theirs);
            if (!File.Exists(ours) || !File.Exists(theirs)) return null;
            var (a, b) = CleanRoom.MatchLines(File.ReadAllText(ours), File.ReadAllText(theirs));
            return (ours, theirs, a, b);
        }
    }

    public void CleanSelectSim(int i) => _cleanSimSel = i;

    Control CleanSideBySideView()
    {
        if (CleanSideBySide is not { } v) return new Panel();
        Control Side(string name, string title, string file, IReadOnlyCollection<int> marked)
        {
            var lines = File.ReadAllLines(file);
            var sp = new StackPanel { Name = name };
            for (var i = 0; i < Math.Min(lines.Length, 400); i++)
            {
                var hit = marked.Contains(i + 1);
                sp.Children.Add(new Border { Background = hit ? Brush.Parse("#40F97316") : Brushes.Transparent, Child = new TextBlock { Text = $"{i + 1,4}  {lines[i]}", FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"), FontSize = 11.5, Foreground = hit ? Brush.Parse("#FED7AA") : Text } });
            }
            if (lines.Length > 400) sp.Children.Add(P($"… {lines.Length - 400} weitere Zeilen", Muted, 11));
            return new Border { Background = Brush.Parse("#14000000"), CornerRadius = new Avalonia.CornerRadius(8), Padding = new Avalonia.Thickness(8, 6), Child = new StackPanel { Spacing = 4, Children = {
                new TextBlock { Text = $"{title} · {marked.Count} markierte Zeilen", Foreground = GoldText(), FontSize = 12, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = file.Length > 70 ? "…" + file[^68..] : file, Foreground = Muted, FontSize = 10.5 },
                new ScrollViewer { MaxHeight = 360, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = sp } } } };
        }
        return new StackPanel { Name = "clean-side", Spacing = 4, Children = {
            P("Nebeneinander: links dein Code, rechts der Vergleichstext. Orange = gleiche Abschnitte (Namen umbenannt zählt mit). Diese Stellen neu und eigenständig schreiben.", Muted, 11.5),
            new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*"), Children = { Side("clean-side-ours", "Dein Code", v.Ours, v.OursLines), Pos(Side("clean-side-theirs", "Vergleichstext", v.Theirs, v.TheirsLines), 2) } } } };
    }

    /// <summary>Anforderungsliste als bearbeitbare Tabelle: ID, Funktion, erwartetes Verhalten, Prüfung, Quelle, Datum, Stufe, Status, Test.
    /// Jede Änderung wird sofort sicher gespeichert; Rückgängig (auch Strg+Z), Abgleich mit echtem Testlauf, Export als .md.</summary>
    Control CleanRequirementsPanel(string open)
    {
        var doc = CleanReq;
        var items = doc.Items;
        var undo = Act("clean-req-undo", "↶ Rückgängig", () => { CleanReqUndo(); Show("nachbau"); }); undo.IsEnabled = doc.CanUndo; ToolTip.SetTip(undo, "Letzte Änderung zurücknehmen (Strg+Z)");
        var reconcile = Act("clean-req-reconcile", CleanReconcileRunning ? "Abgleich läuft …" : "Abgleichen (Tests laufen lassen)", () => _ = CleanReqReconcile());
        reconcile.IsEnabled = !CleanReconcileRunning; ToolTip.SetTip(reconcile, "Lässt die Tests in „2-Umsetzung“ echt laufen – das führt Code aus „2-Umsetzung“ mit deinen Rechten aus.");
        var cancelRun = Act("clean-req-cancel", "■ Abbrechen", () => { CleanReqCancel(); Show("nachbau"); }); cancelRun.IsVisible = CleanReconcileRunning; cancelRun.Margin = new Avalonia.Thickness(6, 0, 0, 0);
        var bar = new WrapPanel { Children = {
            Act("clean-req-add", "＋ Anforderung", () => { CleanReqAdd(); Show("nachbau"); }), new Border { Width = 6 }, undo, new Border { Width = 6 },
            reconcile, cancelRun, new Border { Width = 6 },
            Act("clean-req-export", "Als .md exportieren", () => { CleanReqExport(); Show("nachbau"); }) } };
        var sp = new StackPanel { Name = "clean-req", Spacing = 6, Focusable = true };
        sp.Children.Add(P("Was soll dein Programm tun? Je Zeile eine Anforderung mit Herkunft (Quelle) und Datum. Verknüpfe einen Test als „Datei#Testname“ (Datei auch hierher ziehen) – „Abgleichen“ lässt die Tests echt laufen und setzt den Status. Achtung: Das führt Code aus „2-Umsetzung“ mit deinen Rechten aus.", Muted, 11.5));
        if (doc.Problem is { } problem) sp.Children.Add(new TextBlock { Name = "clean-req-problem", Text = "⚠ " + problem, Foreground = Brush.Parse("#FACC15"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(bar);
        sp.Children.Add(new TextBlock { Name = "clean-req-summary", Text = items.Count == 0 ? "Noch keine Anforderung – „＋ Anforderung“." : $"{items.Count(r => r.Status == CleanRoom.StatusDone)} erfüllt · {items.Count(r => r.Status == CleanRoom.StatusPartly)} teilweise · {items.Count(r => r.Status == CleanRoom.StatusOpen)} offen" + (_cleanReqMsg.Length > 0 ? "  —  " + _cleanReqMsg : ""), Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        // Je Anforderung zwei Zeilen, damit alle Spalten ohne Querscrollen passen: oben WAS (Funktion, Verhalten, Prüfung, Test), unten WOHER (Quelle, Datum, Stufe, Status, Abgleich)
        var table = new Grid { Name = "clean-req-table", ColumnDefinitions = new ColumnDefinitions("48,1*,1.3*,1*,1.2*,64"), MinWidth = 600 };
        string[] heads = ["ID", "Funktion", "Erwartetes Verhalten", "Prüfung", "Test / Datei", ""];
        string[] heads2 = ["", "Quelle (Herkunft)", "Datum · Stufe · Status", "", "Abgleich", ""];
        table.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var c = 0; c < heads.Length; c++) { table.Children.Add(Cell(new TextBlock { Text = heads[c], Foreground = Muted, FontSize = 11, FontWeight = FontWeight.SemiBold }, 0, c)); table.Children.Add(Cell(new TextBlock { Text = heads2[c], Foreground = Muted, FontSize = 10.5 }, 1, c)); }
        var row = 2; var rr = -1;
        foreach (var r in items)
        {
            var id = r.Id; rr++;
            table.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            TextBox Edit(string field, string value, Func<Requirement, string, Requirement> set, string hint = "")
            {
                var tb = new TextBox { Name = $"clean-req-{field}-{rr}", Text = value, PlaceholderText = hint, FontSize = 12, TextWrapping = TextWrapping.Wrap, AcceptsReturn = false, MinHeight = 30 };
                tb.LostFocus += (_, _) => { if ((tb.Text ?? "") != value) { CleanReq.Update(id, x => set(x, tb.Text ?? "")); } };
                tb.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { e.Handled = true; if ((tb.Text ?? "") != value) { CleanReq.Update(id, x => set(x, tb.Text ?? "")); Show("nachbau"); } } };
                return tb;
            }
            ComboBox Pick(string field, string[] options, string value, Func<Requirement, string, Requirement> set)
            {
                var cb = new ComboBox { Name = $"clean-req-{field}-{rr}", ItemsSource = options, SelectedItem = options.Contains(value) ? value : options[0], FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
                cb.SelectionChanged += (_, _) => { if (cb.SelectedItem is string s && s != value) { CleanReq.Update(id, x => set(x, s)); Show("nachbau"); } };
                return cb;
            }
            var status = Pick("status", CleanRoom.Statuses, r.Status, (x, v) => x with { Status = v, Note = "von Hand gesetzt" });
            status.Foreground = Brush.Parse(r.Status == CleanRoom.StatusDone ? "#4ADE80" : r.Status == CleanRoom.StatusPartly ? "#FACC15" : "#F97316");
            if (r.Note.Length > 0) ToolTip.SetTip(status, r.Note);
            var link = Edit("test", r.Link, (x, v) => x with { Link = v.Trim().Replace('\\', '/') }, "2-Umsetzung/test_x.py#test_name");
            Avalonia.Input.DragDrop.SetAllowDrop(link, true);
            link.AddHandler(Avalonia.Input.DragDrop.DropEvent, (_, e) =>
            {
                e.Handled = true;
                var f = e.DataTransfer.TryGetFiles()?.Select(x => x.TryGetLocalPath()).OfType<string>().FirstOrDefault();
                if (f is null) return;
                var rel = Path.GetRelativePath(open, f);
                if (rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel)) { _cleanReqMsg = "Nur Dateien aus diesem Projekt lassen sich verknüpfen."; Show("nachbau"); return; }
                CleanReq.Update(id, x => x with { Link = rel.Replace('\\', '/') }); Show("nachbau");
            });
            var (file, _) = CleanRoom.ResolveLink(open, r.Link);
            var code = new Button { Name = $"clean-req-code-{rr}", Content = "↗", FontSize = 12, IsEnabled = file is not null && File.Exists(file), Padding = new Avalonia.Thickness(6, 2) };
            ToolTip.SetTip(code, "Verknüpfte Datei im Code-Editor öffnen");
            code.Click += (_, _) => { if (file is not null) { CodeOpenFolder(Path.Combine(open, "2-Umsetzung") is var impl && file.StartsWith(impl, StringComparison.OrdinalIgnoreCase) ? impl : open); CodeOpenFile(file); Show(CEK); } };
            var del = new Button { Name = $"clean-req-del-{rr}", Content = "✕", FontSize = 12, Padding = new Avalonia.Thickness(6, 2) };
            ToolTip.SetTip(del, "Anforderung löschen (Rückgängig möglich)");
            del.Click += (_, _) => { CleanReq.Remove(id); Show("nachbau"); };
            var menu = new ContextMenu();
            foreach (var st in CleanRoom.Statuses) { var s0 = st; var mi = new MenuItem { Header = "Status: " + st }; mi.Click += (_, _) => { CleanReq.Update(id, x => x with { Status = s0, Note = "von Hand gesetzt" }); Show("nachbau"); }; menu.Items.Add(mi); }
            var mc = new MenuItem { Header = "Im Code-Editor öffnen", IsEnabled = code.IsEnabled }; mc.Click += (_, _) => code.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); menu.Items.Add(mc);
            var md = new MenuItem { Header = "Löschen" }; md.Click += (_, _) => { CleanReq.Remove(id); Show("nachbau"); }; menu.Items.Add(md);
            Control[] cells = [
                new TextBlock { Text = r.Id, Foreground = GoldText(), FontSize = 12, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, ContextMenu = menu, Background = Brushes.Transparent },
                Edit("fn", r.Function, (x, v) => x with { Function = v }, "z. B. Notiz speichern"),
                Edit("exp", r.Expected, (x, v) => x with { Expected = v }, "Was passiert genau?"),
                Edit("chk", r.Check, (x, v) => x with { Check = v }, "Wie prüfen?"),
                link,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { code, del } } ];
            for (var c = 0; c < cells.Length; c++) table.Children.Add(Cell(cells[c], row, c));
            var meta = new Grid { ColumnDefinitions = new ColumnDefinitions("1.1*,4,0.8*,4,1*") };
            meta.Children.Add(Edit("date", r.Date, (x, v) => x with { Date = v }, "JJJJ-MM-TT")); meta.Children.Add(Pos(Pick("stage", CleanRoom.Stages, r.Stage, (x, v) => x with { Stage = v }), 2)); meta.Children.Add(Pos(status, 4));
            var note = new TextBlock { Name = $"clean-req-note-{rr}", Text = r.Note, Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumnSpan(note, 2);
            table.Children.Add(Cell(Edit("src", r.Source, (x, v) => x with { Source = v }, "Herkunft"), row + 1, 1));
            Grid.SetColumnSpan(meta, 2);
            table.Children.Add(Cell(meta, row + 1, 2));
            table.Children.Add(Cell(note, row + 1, 4));
            var sep = new Border { Height = 1, Background = Brush.Parse("#22FFFFFF"), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Avalonia.Thickness(0, 0, 0, -2) };
            Grid.SetRow(sep, row + 1); Grid.SetColumnSpan(sep, 6); table.Children.Add(sep);
            row += 2;
        }
        sp.Children.Add(table); // volle Breite der Karte: Textfelder brechen um statt hinauszuragen
        // Strg+Z außerhalb eines Textfelds nimmt die letzte Änderung der Liste zurück (im Textfeld gilt das Rückgängig des Feldes)
        sp.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Z && e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control) && e.Source is not TextBox) { e.Handled = true; CleanReqUndo(); Show("nachbau"); }
        };
        return sp;

        static Control Cell(Control c, int r, int col) { c.Margin = new Avalonia.Thickness(0, 0, 4, 4); Grid.SetRow(c, r); Grid.SetColumn(c, col); return c; }
    }
}
