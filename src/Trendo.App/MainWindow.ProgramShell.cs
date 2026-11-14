using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>T-0231 V2/V3/V6/V9: Vorlage eines Studio-Programms – Startseite (Neu · Vorlagen · Zuletzt · Wiederherstellen), Reiter für mehrere Dokumente,
/// Menüband (Datei, Bearbeiten, Studio-Gruppen, Export), Arbeitsfläche, Eigenschaften rechts, Statusleiste, Tastenkürzel, automatisches Sichern.</summary>
public sealed record ProgramTemplate(string Id, string Name, string Hint, Func<JsonObject> Create);
/// <summary><paramref name="Level"/>: ab welcher Bedienstufe das Werkzeug erscheint (0 Einfach, 1 Erweitert, 2 Profi – siehe <see cref="UserLevel"/>).</summary>
public sealed record ProgramCmd(string Name, string Label, Action<ProgramDocument> Run, bool Primary = false, string? Tip = null, int Level = 0);
public sealed record ProgramExport(string Name, string Label, string Extension, Func<ProgramDocument, string, string> Write);
/// <summary>Paket F (A1 Profi-Bauplan): ein Werkzeug der Werkzeugleiste links – Zeichen, Name, Hinweis; <paramref name="Active"/> markiert das gewählte.</summary>
public sealed record ProgramTool(string Name, string Icon, string Label, string Tip, Action<ProgramDocument> Select, Func<bool> Active);
/// <summary>Paket F (A1): Bereich unten (Zeitleiste, Ebenen, Messwerte …).</summary>
public sealed record ProgramPanel(string Title, Func<ProgramDocument, Control> Build, double Height = 180);

public sealed class ProgramSpec(string key, string studio, string title)
{
    public string Key { get; } = key;
    public string Studio { get; } = studio;
    public string Title { get; } = title;
    public string Sentence { get; init; } = "";
    public string Folder { get; init; } = title;
    public List<ProgramTemplate> Templates { get; init; } = [];
    public List<(string Group, List<ProgramCmd> Cmds)> Ribbon { get; init; } = [];
    public List<ProgramExport> Exports { get; init; } = [];
    public required Func<ProgramDocument, Control> Workspace { get; init; }
    public Func<ProgramDocument, Control>? Properties { get; init; }
    /// <summary>Optional ganz oben auf der Startseite (ohne offenes Dokument), z. B. der Spiele-Prompt „Spiel aus einem Satz“.</summary>
    public Func<Control>? StartTop { get; init; }
    /// <summary>Wie viele Exporte die Stufe Einfach zeigt (die ersten).</summary>
    public int SimpleExports { get; init; } = 1;
    /// <summary>Paket F (A1): Werkzeugleiste links (wie in Profi-Programmen) – einklappbar.</summary>
    public List<ProgramTool> Tools { get; init; } = [];
    /// <summary>Paket F (A1): Bereich unten – einklappbar.</summary>
    public ProgramPanel? Bottom { get; init; }
    public Func<ProgramDocument, string>? Status { get; init; }
    public string Help { get; init; } = "";
    /// <summary>T-0256: Arbeitsfläche füllt die Fensterhöhe (Zeichenprogramme) statt mitzuscrollen.</summary>
    public bool FillHeight { get; init; }
    /// <summary>Masterprompt 131: Bedienstufe je Menüband-Gruppe (fehlt = Einfach); Befehle können zusätzlich eine eigene Stufe haben.</summary>
    public Dictionary<string, int> GroupLevels { get; init; } = [];
    public Dictionary<string, int> CmdLevels { get; init; } = [];
    /// <summary>Arbeitsfläche aus Karten und Listen (statt einer Zeichenfläche): bei voller Fensterhöhe senkrecht scrollbar.</summary>
    public bool ScrollWorkspace { get; init; }
    public int LevelOf(ProgramCmd c) => Math.Max(c.Level, CmdLevels.GetValueOrDefault(c.Name));
}

public partial class MainWindow
{
    public static readonly Dictionary<string, ProgramSpec> Programs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<ProgramDocument>> _progDocs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _progActive = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _progMsg = new(StringComparer.OrdinalIgnoreCase);
    private DispatcherTimer? _progAutosave;
    /// <summary>E7: letzter Fehler je Programm mit „Was tun?“ (Nochmal = derselbe Befehl).</summary>
    private readonly Dictionary<string, (FriendlyError Error, Action? Retry)> _progErr = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>E2: Programme, in denen die Person in „Einfach“ die weiteren Werkzeuge aufgeklappt hat (gilt bis zum Neustart).</summary>
    private readonly HashSet<string> _simpleToolsOpen = new(StringComparer.OrdinalIgnoreCase);
    private bool? _autoSaveDocs;
    /// <summary>E3 (T-0616): Dokumente nach jeder Änderung selbst speichern. Abgeschottete Tests schalten es aus (wie das Abo), eigene Tests schalten es ein.</summary>
    public bool AutoSaveDocs { get => _autoSaveDocs ?? (!_opt.Isolated && _settings.AutoSave); set => _autoSaveDocs = value; }
    /// <summary>E3: höchstens so oft behält das automatische Speichern einen früheren Stand als Version.</summary>
    public static readonly TimeSpan AutoVersionGap = TimeSpan.FromMinutes(10);
    private DispatcherTimer? _progSaveSoon;
    public ProgramRecent ProgRecent => new(_core.DataRoot);
    string ProgBackup => Path.Combine(_core.DataRoot, "programme", "sicherung");

    /// <summary>Studio-Programm anmelden: Seite, Reiter und Suchbegriffe wie bei jeder Studio-Seite.</summary>
    private void RegisterProgram(List<(string, string, string)> tabs, ProgramSpec spec, params string[] words)
    {
        Programs[spec.Key] = spec;
        RegisterTab(tabs, spec.Key, spec.Title, spec.Studio, () => ProgramView(spec), words);
        if (_progAutosave is null)
        {
            _progAutosave = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _progAutosave.Tick += (_, _) => ProgramAutosaveAll();
            _progAutosave.Start();
            Closing += (_, _) => ProgramAutosaveAll(); // E3: beim Beenden nichts verlieren
        }
    }

    // ---------- Öffentliche Wege (Tests, KI, Abnahme V12) ----------
    public List<ProgramDocument> ProgramDocs(string key) => _progDocs.TryGetValue(key, out var l) ? l : _progDocs[key] = [];
    public ProgramDocument? ProgramActive(string key) { var l = ProgramDocs(key); return l.Count == 0 ? null : l[Math.Clamp(_progActive.GetValueOrDefault(key), 0, l.Count - 1)]; }
    public string ProgramMessage(string key) => _progMsg.GetValueOrDefault(key, "");
    /// <summary>E7: letzter Fehler des Programms (für Tests und Claude), null = keiner.</summary>
    public FriendlyError? ProgramError(string key) => _progErr.TryGetValue(key, out var e) && _progMsg.GetValueOrDefault(key) == e.Error.Sentence ? e.Error : null;

    public ProgramDocument ProgramNew(string key, string? templateId = null)
    {
        var spec = Programs[key];
        var t = spec.Templates.FirstOrDefault(x => x.Id == templateId) ?? spec.Templates.FirstOrDefault();
        var d = new ProgramDocument(spec.Studio, t is null || t.Id == "leer" ? "Unbenannt" : t.Name, t?.Create() ?? []);
        var l = ProgramDocs(key); l.Add(d); _progActive[key] = l.Count - 1;
        _progMsg[key] = t is null || t.Id == "leer" ? "Neues Dokument." : $"Vorlage „{t.Name}“ geöffnet.";
        return d;
    }

    public ProgramDocument ProgramOpen(string key, string path)
    {
        var spec = Programs[key]; var l = ProgramDocs(key);
        var have = l.FindIndex(x => x.Path is not null && string.Equals(Path.GetFullPath(x.Path), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
        if (have >= 0) { _progActive[key] = have; return l[have]; }
        var d = ProgramDocument.Load(path, spec.Studio);
        l.Add(d); _progActive[key] = l.Count - 1; ProgRecent.Add(spec.Studio, path); RememberLastDoc(key, path);
        _progMsg[key] = "Geöffnet: " + Path.GetFileName(path);
        _core.Audit.Append(Principal.LocalOwner.Name, "program.open", $"{spec.Studio} {Path.GetFileName(path)}");
        return d;
    }

    /// <summary>Speichern; ohne Ort in den Studio-Ordner der Projekte (vorhandene Dateien werden nicht überschrieben).</summary>
    public string ProgramSave(string key, string? path = null)
    {
        var spec = Programs[key]; var d = ProgramActive(key) ?? throw new InvalidOperationException("Es ist kein Dokument offen.");
        var target = path ?? d.Path;
        if (target is null) target = ProgramNewTarget(spec, d);
        else if (!Path.IsPathRooted(target)) // nur ein Name: neben das Dokument, sonst in den Ordner des Programms, nie ins Arbeitsverzeichnis des Prozesses
            target = Path.Combine(d.Path is { } current && Path.IsPathRooted(current) ? Path.GetDirectoryName(current)! : WorkFolder(spec.Folder), target);
        d.Save(target); ProgRecent.Add(spec.Studio, d.Path!); RememberLastDoc(key, d.Path!); DropBackup(d);
        if (path is not null) _progAutoNamed.Remove(d.Id); // selbst benannt: nicht mehr umbenennen
        _core.Audit.Append(Principal.LocalOwner.Name, "program.save", $"{spec.Studio} {Path.GetFileName(d.Path)}");
        _progMsg[key] = "Gespeichert: " + d.Path; return d.Path!;
    }

    /// <summary>Freier Dateiname im Studio-Ordner der Projekte (vorhandene Dateien werden nie überschrieben).</summary>
    string ProgramNewTarget(ProgramSpec spec, ProgramDocument d)
    {
        var dir = WorkFolder(spec.Folder); var stem = DevStudio.SafeName(d.Title); var target = Path.Combine(dir, stem + ProgramDocument.Extension);
        for (var n = 2; File.Exists(target); n++) target = Path.Combine(dir, $"{stem} ({n}){ProgramDocument.Extension}");
        return target;
    }

    /// <summary>Dokumente, deren Datei das automatische Speichern selbst angelegt und benannt hat (nur diese Sitzung).</summary>
    readonly HashSet<string> _progAutoNamed = [];

    /// <summary>T-0620: Neuer Dateiname, wenn eine von selbst angelegte Datei noch nach dem alten Titel heißt; sonst null.
    /// Nur im Studio-Ordner und nur für Dateien, die das automatische Speichern in dieser Sitzung angelegt hat.</summary>
    string? ProgramAutoRename(ProgramSpec spec, ProgramDocument d)
    {
        if (d.Path is not { } cur || !_progAutoNamed.Contains(d.Id)) return null;
        var stem = DevStudio.SafeName(d.Title);
        var name = Path.GetFileNameWithoutExtension(cur);
        if (name == stem || name.StartsWith(stem + " (", StringComparison.Ordinal)) return null;
        var dir = Path.GetFullPath(WorkFolder(spec.Folder));
        if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(cur)), dir, StringComparison.OrdinalIgnoreCase)) return null;
        return ProgramNewTarget(spec, d);
    }

    /// <summary>Gespeichert: die Absturz-Sicherung des noch unbenannten Dokuments wird nicht mehr gebraucht.</summary>
    void DropBackup(ProgramDocument d)
    {
        try { var f = Path.Combine(ProgBackup, $"{d.Studio}-{d.Id}{ProgramDocument.Extension}"); if (File.Exists(f)) File.Delete(f); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>E3 (T-0616): geändertes Dokument selbst speichern – ein neues landet beim ersten Speichern im Studio-Ordner. Leere, unveränderte Dokumente
    /// legen keine Datei an. Höchstens alle <see cref="AutoVersionGap"/> bleibt ein früherer Stand als Version. false = nicht gespeichert (aus oder Fehler).</summary>
    public bool ProgramAutoSave(string key, ProgramDocument d)
    {
        if (!AutoSaveDocs || !d.Dirty || !Programs.TryGetValue(key, out var spec)) return false;
        // Claude hat geändert und du hast noch nicht entschieden (behalten/verwerfen) oder eine Live-Animation läuft: erst danach speichern – „Verwerfen“ lässt die Datei so unverändert
        if (_claudeChanges.TryGetValue(key, out var pending) && pending.DocId == d.Id || _liveAnim.ContainsKey(key)) return false;
        try
        {
            if (d.Path is null)
            {
                d.Save(ProgramNewTarget(spec, d));
                ProgRecent.Add(spec.Studio, d.Path!); RememberLastDoc(key, d.Path!); _progAutoNamed.Add(d.Id);
                _core.Audit.Append(Principal.LocalOwner.Name, "program.autosave", $"{spec.Studio} {Path.GetFileName(d.Path)}");
            }
            else if (ProgramAutoRename(spec, d) is { } renamed)
            {
                // T-0620: von selbst angelegte Datei („Unbenannt.tdoc“) folgt dem neuen Titel – samt Versionen; die alte Datei verschwindet
                var oldPath = d.Path;
                d.Save(renamed, keepVersion: false);
                try
                {
                    var ov = ProgramDocument.VersionDir(oldPath); var nv = ProgramDocument.VersionDir(renamed);
                    if (Directory.Exists(ov) && !Directory.Exists(nv)) Directory.Move(ov, nv);
                    if (File.Exists(oldPath)) File.Delete(oldPath);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
                ProgRecent.Add(spec.Studio, d.Path!); RememberLastDoc(key, d.Path!);
                _core.Audit.Append(Principal.LocalOwner.Name, "program.autosave.rename", $"{Path.GetFileName(oldPath)} -> {Path.GetFileName(d.Path)}");
            }
            else d.Save(keepVersion: ProgramDocument.VersionDue(d.Path, AutoVersionGap));
            DropBackup(d);
            if (_progErr.TryGetValue(key, out var old) && old.Error.Kind == "speichern") { _progErr.Remove(key); if (_progMsg.GetValueOrDefault(key) == old.Error.Sentence) _progMsg.Remove(key); }
            return true;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // 9: nicht stumm – Meldung mit „Anderen Ort wählen“; die Sicherung im Hintergrund bleibt
            var f0 = FriendlyError.From(e);
            var fe = f0 with { Kind = "speichern", Sentence = "Automatisch speichern ging nicht: " + f0.Sentence, Fix = ErrorFix.PickFolder };
            _progErr[key] = (fe, null); _progMsg[key] = fe.Sentence;
            return false;
        }
    }

    /// <summary>E3: alle geänderten Dokumente jetzt speichern (sonst nach kurzer Pause von selbst). Gibt die Zahl der gespeicherten zurück.</summary>
    public int ProgramAutosaveNow()
    {
        _progSaveSoon?.Stop();
        var n = 0;
        foreach (var (key, l) in _progDocs) foreach (var d in l.ToList()) if (ProgramAutoSave(key, d)) n++;
        if (n > 0 && Programs.ContainsKey(CurrentView) && ProgramActive(CurrentView) is { Dirty: false, Path: not null } shown && ActivePageRoot is { } root) // nur die Anzeige des sichtbaren Dokuments, nicht neu zeichnen – beim Tippen geht nichts verloren
        {
            foreach (var tb in root.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Name == "prog-dirty")) tb.Text = SaveStateText(shown);
            foreach (var tb in root.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Name == "prog-saved")) tb.Text = Path.GetFileName(shown.Path) + " · ✓ Gespeichert" + $" · {shown.Changed:HH:mm}";
        }
        return n;
    }

    /// <summary>E3: entprellt – nach einer kurzen Pause ohne weitere Änderung speichern.</summary>
    void ProgramSaveSoon()
    {
        if (!AutoSaveDocs) return;
        if (_progSaveSoon is null) { _progSaveSoon = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) }; _progSaveSoon.Tick += (_, _) => ProgramAutosaveNow(); }
        _progSaveSoon.Stop(); _progSaveSoon.Start();
    }

    /// <summary>Vor dem Schließen: speichern (automatisch) oder wenigstens sichern.</summary>
    void ProgramKeep(string key, ProgramDocument d)
    {
        if (!d.Dirty || ProgramAutoSave(key, d)) return;
        d.Autosave(ProgBackup);
    }

    public void ProgramClose(string key, int index)
    {
        var l = ProgramDocs(key); if (index < 0 || index >= l.Count) return;
        l.RemoveAt(index); _progActive[key] = Math.Max(0, Math.Min(_progActive.GetValueOrDefault(key), l.Count - 1));
    }

    public void ProgramAutosaveAll()
    {
        foreach (var (key, l) in _progDocs) foreach (var d in l.ToList()) try { if (!ProgramAutoSave(key, d)) d.Autosave(ProgBackup); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        OfficeAutosave(); // T-0254: Text, Tabelle, Präsentation
    }

    /// <summary>Eigenschaftsfelder übernehmen mit Enter (bei einzeiligen Feldern) oder beim Verlassen.</summary>
    /// <summary>T-0273: Ein Eingabefeld, das nach dem Neuzeichnen schon aus der Seite gelöst ist, übernimmt beim Fokusverlust nichts mehr –
    /// sonst schreibt es nach Enter + „Rückgängig“ den alten Wert erneut zurück. Klassen-Handler laufen vor den Feld-Handlern.</summary>
    static readonly bool StaleFocusGuard = RegisterStaleFocusGuard();
    static bool RegisterStaleFocusGuard()
    {
        InputElement.LostFocusEvent.AddClassHandler<TextBox>((t, e) => { if (TopLevel.GetTopLevel(t) is null) e.Handled = true; });
        return true;
    }

    private static void OnEnter(TextBox t, Action commit) => t.KeyDown += (_, e) => { if (e.Key == Key.Enter && !t.AcceptsReturn) { commit(); e.Handled = true; } };

    /// <summary>Befehl ausführen: Fehler werden zur Meldung, danach Seite neu zeichnen.</summary>
    private void ProgramRun(string key, Action a)
    {
        if (AssistQuiet > 0) { a(); return; } // T-0315: Vorschau-Lauf des Assistenten – kein Neuzeichnen, Fehler gehen an den Assistenten
        try { a(); _progErr.Remove(key); }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException or InvalidDataException or FormatException or KeyNotFoundException)
        {
            // E7: Alltagssatz statt Systemtext, dazu ein Knopf (Nochmal, Einrichten, anderen Ort wählen, Claude fragen); der technische Text bleibt aufklappbar
            var fe = FriendlyError.From(ex); _progMsg[key] = fe.Sentence; _progErr[key] = (fe, () => ProgramRun(key, a));
        }
        Show(key);
    }

    // ---------- Oberfläche ----------
    private Control ProgramView(ProgramSpec spec)
    {
        var key = spec.Key;
        var icon = StudioIcons.For(spec.Studio);
        var (c1, c2) = icon is null ? ("#7FD3FF", "#2F6BF6") : (icon.C1, icon.C2);
        var docs = ProgramDocs(key); var d = ProgramActive(key);

        // V6: Kopfband in der Farbwelt des Studios
        var title = d is null ? null : new TextBox { Name = "prog-title", Text = d.Title, FontSize = 15, FontWeight = FontWeight.SemiBold, MinWidth = 260, Background = Brush.Parse("#26FFFFFF"), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
        if (title is not null) title.LostFocus += (_, _) => { if (d is not null && !string.IsNullOrWhiteSpace(title.Text) && title.Text.Trim() != d.Title) { d.Title = title.Text.Trim(); d.EditMeta(_ => { }); Show(key); } };
        var band = new Border
        {
            Name = "prog-band", CornerRadius = new CornerRadius(16), Padding = new Thickness(16, 12), BoxShadow = CardShadow,
            Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.Parse(c2), 0), new GradientStop(Color.Parse(c1), 1) } },
            BorderBrush = Brush.Parse("#55FFFFFF"), BorderThickness = new Thickness(1, 1, 1, 0),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = {
                new Border { Width = 46, Height = 46, CornerRadius = new CornerRadius(12), Background = Brush.Parse("#33FFFFFF"), Child = icon is null ? null : Ico(icon.Path, 26, Brushes.White) },
                Pos(new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = {
                    new TextBlock { Text = spec.Title, Foreground = Brushes.White, FontSize = 18, FontWeight = FontWeight.Bold },
                    spec.FillHeight && d is not null && _progMsg.GetValueOrDefault(key) is { Length: > 0 } bm
                        ? new SelectableTextBlock { Name = "prog-msg", Text = "" + bm, Foreground = Brushes.White, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, MaxHeight = 40 }
                        : new TextBlock { Text = spec.Sentence, Foreground = Brush.Parse("#E6FFFFFF"), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis } } }, 1),
                Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Children = {
                    // E1 (T-0616): Claude und „Markierung“ stehen im Knopf „KI ▾“ der KI-Leiste darüber – nicht noch einmal hier
                    title is null ? LevelArea(key) : new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Children = {
                    LevelArea(key), title, new TextBlock { Name = "prog-dirty", Text = SaveStateText(d!), Foreground = Brushes.White, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } } } }, 2) } },
        };

        // D2 (13.11.2026): Programmkopf immer schlicht – keine Studio-Farbfläche, kein Symbolblock, kein doppelter Titel.
        // Den Namen zeigt sonst schon der Seitenkopf darüber; nur Programme in voller Fensterhöhe (ohne Seitenkopf) nennen ihn hier.
        band.Background = Brushes.Transparent; band.BorderBrush = Edge; band.BorderThickness = new Thickness(0, 0, 0, 1); band.CornerRadius = new CornerRadius(0); band.BoxShadow = default;
        band.Padding = new Thickness(2, 4, 2, 8);
        if (band.Child is Grid bg && bg.Children.Count > 1)
        {
            bg.Children[0].IsVisible = false; // Symbolblock
            if (bg.Children[1] is StackPanel ts) { ts.Margin = new Thickness(0); if (!spec.FillHeight) ts.IsVisible = false; } // Titel und Satz stehen schon im Seitenkopf
        }
        foreach (var tb in band.GetLogicalDescendants().OfType<TextBlock>()) if (ReferenceEquals(tb.Foreground, Brushes.White) || tb.Foreground is ISolidColorBrush { Color.A: > 200, Color.R: > 220, Color.G: > 220, Color.B: > 220 }) tb.Foreground = tb.FontSize >= 15 ? Text : Muted;
        foreach (var tb in band.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.FontSize >= 18)) { tb.FontSize = 15; tb.FontWeight = FontWeight.SemiBold; }
        if (title is not null) { title.Foreground = Text; title.Background = Brushes.Transparent; title.FontSize = 13; }
        Panel root;
        if (spec.FillHeight && d is not null)
        {
            // Kopf (Band, Meldung, Reiter, Menüband) fest, darunter Arbeitsfläche + Eigenschaften in voller Höhe, Statusleiste unten
            var (top, grid, status) = ProgramWorkParts(spec, d, docs);
            band.Padding = new Thickness(14, 8);
            var head = new StackPanel { Spacing = 6, Children = { band, top } };
            if (_progErr.TryGetValue(key, out var fhe) && fhe.Error.Fix != ErrorFix.None && _progMsg.GetValueOrDefault(key) == fhe.Error.Sentence) head.Children.Insert(1, ProgramErrorCard(key, fhe.Error, fhe.Retry)); // E7
            if (ProgramIntro(key) is { } fi) head.Children.Insert(1, fi); // E8
            if (ClaudeChangeBanner(key, d) is { } cb) head.Children.Insert(1, cb); // Claude hat geändert: vergleichen, behalten, verwerfen
            if (ClaudeProgramExtra(key) is { } cx) head.Children.Insert(1, cx); // z. B. das spielbare Spiel, das Claude gebaut hat
            // DockPanel: Kopf und Statusleiste bekommen die echte Breite (Menüband bricht richtig um), die Arbeitsfläche den Rest
            head.Margin = new Thickness(0, 0, 0, 8); status.Margin = new Thickness(0, 8, 0, 0);
            DockPanel.SetDock(head, Avalonia.Controls.Dock.Top); DockPanel.SetDock(status, Avalonia.Controls.Dock.Bottom);
            root = new DockPanel { Name = "prog-root", LastChildFill = true, Children = { head, status, grid } };
        }
        else
        {
            Control body = d is null ? ProgramStart(spec) : ProgramWork(spec, d, docs);
            root = new StackPanel { Name = "prog-root", Spacing = 10, Children = { band, body } };
            if (_progMsg.GetValueOrDefault(key) is { Length: > 0 } msg)
                root.Children.Insert(1, _progErr.TryGetValue(key, out var pe) && pe.Error.Fix != ErrorFix.None && pe.Error.Sentence == msg ? ProgramErrorCard(key, pe.Error, pe.Retry) : ResultCard(msg)); // E7: Fehler mit „Was tun?“
            if (ProgramIntro(key) is { } pi) root.Children.Insert(1, pi); // E8: kurze Einführung beim ersten Öffnen
            if (ClaudeChangeBanner(key, d) is { } cb) root.Children.Insert(1, cb); // Claude hat geändert: vergleichen, behalten, verwerfen
            if (ClaudeProgramExtra(key) is { } cx) root.Children.Insert(1, cx); // z. B. das spielbare Spiel, das Claude gebaut hat
        }
        root.KeyDown += (_, e) =>
        {
            if (e.KeyModifiers != KeyModifiers.Control) return;
            switch (e.Key)
            {
                case Key.S when d is not null: ProgramRun(key, () => ProgramSave(key)); e.Handled = true; break;
                case Key.Z when d is not null: ProgramRun(key, () => d.Undo()); e.Handled = true; break;
                case Key.Y when d is not null: ProgramRun(key, () => d.Redo()); e.Handled = true; break;
                case Key.N: ProgramRun(key, () => ProgramNew(key)); e.Handled = true; break;
                case Key.W when d is not null: ProgramRun(key, () => { ProgramKeep(key, d); ProgramClose(key, _progActive.GetValueOrDefault(key)); }); e.Handled = true; break;
            }
        };
        if (d is { Dirty: true }) ProgramSaveSoon(); // E3: kurz nach der letzten Änderung von selbst speichern
        return spec.FillHeight && d is not null ? FitPage(spec.Title, new Panel(), root, pageHead: false) : Page(spec.Title, root);
    }

    /// <summary>E3: Speicherstand in einem Wort – mit automatischem Speichern nur „Gespeichert“.</summary>
    string SaveStateText(ProgramDocument d) => AutoSaveDocs
        ? (d.Dirty ? "Wird gespeichert …" : d.Path is null ? "" : "✓ Gespeichert")
        : (d.Dirty ? "geändert" : d.Path is null ? "nicht gespeichert" : "✓ gespeichert");

    /// <summary>E2 (T-0616): In „Einfach“ ist die Stufenwahl eingeklappt – „Mehr Werkzeuge“ holt Stufenwahl, Eigenschaften und Rand-Schalter.</summary>
    Control LevelArea(string key)
    {
        if (!IsSimple) return LevelSwitch(key);
        var open = _simpleToolsOpen.Contains(key);
        var b = new Button { Name = "prog-tools-more", Content = open ? "Weniger Werkzeuge" : "Mehr Werkzeuge", FontSize = 12, Padding = new Thickness(10, 3), VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(b, open ? "Stufenwahl, Eigenschaften und Rand-Schalter wieder einklappen" : "Stufenwahl, Eigenschaften und Rand-Schalter zeigen – die Bedienstufe wählst du auch unter „Mehr“ und in den Einstellungen");
        b.Click += (_, _) => { if (!_simpleToolsOpen.Remove(key)) _simpleToolsOpen.Add(key); Show(key); };
        return open ? new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Children = { LevelSwitch(key), b } } : b;
    }

    /// <summary>E2: In „Einfach“ sind Eigenschaften und Rand-Schalter eingeklappt, bis „Mehr Werkzeuge“ sie holt.</summary>
    bool SimpleCollapsed(string key) => IsSimple && !_simpleToolsOpen.Contains(key);

    /// <summary>E4: Dateiauswahl des Systems (Tests ersetzen sie über <see cref="PickOverride"/>). save: „Speichern unter“ mit Vorschlag.</summary>
    async Task<string?> PickProgramFile(bool save, string suggested)
    {
        var req = new PickRequest(save ? "prog-saveas" : "prog-open", false, false, [ProgramDocument.Extension.TrimStart('.')]);
        if (PickOverride is { } fake) return (await fake(req)) is [var f, ..] ? f : null;
        var type = new Avalonia.Platform.Storage.FilePickerFileType("Trendo-Dokument") { Patterns = ["*" + ProgramDocument.Extension] };
        if (save)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions { Title = "Speichern unter", SuggestedFileName = suggested, DefaultExtension = ProgramDocument.Extension.TrimStart('.'), FileTypeChoices = [type] });
            return file?.TryGetLocalPath();
        }
        var got = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Dokument öffnen", AllowMultiple = false, FileTypeFilter = [type] });
        return got.Select(f => f.TryGetLocalPath()).OfType<string>().FirstOrDefault();
    }

    /// <summary>E4: „Speichern unter …“ mit der Dateiauswahl; nie überschreiben ohne Rückfrage (die Auswahl des Systems fragt selbst nach).</summary>
    async Task ProgramSaveAsPick(string key)
    {
        if (ProgramActive(key) is not { } d) return;
        string? f;
        try { f = await PickProgramFile(true, DevStudio.SafeName(d.Title) + ProgramDocument.Extension); }
        catch (Exception e) when (e is not OutOfMemoryException) { ProgramRun(key, () => throw new IOException(FriendlyError.Say(e), e)); return; }
        if (f is { Length: > 0 }) ProgramRun(key, () => ProgramSave(key, f));
    }

    /// <summary>E4: „Öffnen …“ mit der Dateiauswahl.</summary>
    async Task ProgramOpenPick(string key)
    {
        string? f;
        try { f = await PickProgramFile(false, ""); }
        catch (Exception e) when (e is not OutOfMemoryException) { ProgramRun(key, () => throw new IOException(FriendlyError.Say(e), e)); return; }
        if (f is { Length: > 0 }) ProgramRun(key, () => { if (!File.Exists(f)) throw new FileNotFoundException("Diese Datei gibt es nicht."); ProgramOpen(key, f); });
    }

    /// <summary>E5: zuletzt bearbeitetes Dokument genau dieses Programms – „Zuletzt“ ist je Studio, ein Studio kann mehrere Programme haben
    /// (z. B. Bild und Ton). Ohne eigenen Eintrag nur dann aus „Zuletzt“, wenn das Studio genau ein Programm hat.</summary>
    public string? LastDocOf(string key)
    {
        if (!Programs.TryGetValue(key, out var spec)) return null;
        if (_settings.LastProgramDoc.TryGetValue(key, out var f) && File.Exists(f)) return f;
        return Programs.Values.Count(p => p.Studio == spec.Studio) == 1 ? ProgRecent.Get(spec.Studio).FirstOrDefault() : null;
    }

    void RememberLastDoc(string key, string path)
    {
        if (_settings.LastProgramDoc.GetValueOrDefault(key) == path) return;
        _settings.LastProgramDoc[key] = path; SaveSettings();
    }

    /// <summary>E5 (T-0616): das zuletzt bearbeitete Dokument des Programms öffnen (Desktop „Weitermachen“, oberste Karte der Startansicht). false = keines da.</summary>
    public bool ProgramContinue(string key)
    {
        if (LastDocOf(key) is not { } last) return false;
        ProgramRun(key, () => ProgramOpen(key, last));
        return true;
    }

    // ---------- E8: kurze Einführung beim ersten Öffnen ----------
    private readonly Dictionary<string, int> _introStep = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _introAsked = new(StringComparer.OrdinalIgnoreCase);
    private bool? _introOn;
    /// <summary>E8 (T-0616): drei kurze Schritte beim ersten Öffnen eines Programms. Abgeschottete Tests zeigen sie nicht (nichts darf dort warten); über „Einführung“ in der Hilfe kommt sie immer.</summary>
    public bool ProgramIntroOn { get => _introOn ?? !_opt.Isolated; set => _introOn = value; }

    /// <summary>Die drei Schritte der Einführung.</summary>
    public string[] ProgramIntroSteps() =>
    [
        "Hier anfangen: Wähle unter „Neu“ eine Vorlage oder öffne unter „Zuletzt“, woran du gearbeitet hast.",
        "Hier hilft die KI: Schreib oben in die Zeile, was entstehen soll, und drück „Los“. Unter „KI ▾“ findest du Claude, kie.ai und mehr.",
        AutoSaveDocs ? "Speichern passiert von selbst: Jede Änderung wird gesichert, frühere Stände findest du unter „Versionen“."
                     : "Speichern mit Strg+S – ungespeicherte Änderungen sichert Trendo zusätzlich alle 30 Sekunden.",
    ];

    Control? ProgramIntro(string key)
    {
        var id = "intro:" + key;
        if (!(ProgramIntroOn || _introAsked.Contains(key)) || _settings.SeenHints.Contains(id)) return null;
        var steps = ProgramIntroSteps(); var i = Math.Clamp(_introStep.GetValueOrDefault(key), 0, steps.Length - 1);
        void Done() { _introStep.Remove(key); _introAsked.Remove(key); if (!_settings.SeenHints.Contains(id)) { _settings.SeenHints.Add(id); SaveSettings(); } Show(key); }
        var next = new Button { Name = "prog-intro-next", Content = i < steps.Length - 1 ? "Weiter" : "Fertig", Background = Accent, Foreground = Brushes.White, Padding = new Thickness(12, 4) };
        next.Click += (_, _) => { if (i < steps.Length - 1) { _introStep[key] = i + 1; Show(key); } else Done(); };
        var skip = new Button { Name = "prog-intro-skip", Content = "Überspringen", Padding = new Thickness(12, 4), Background = Brushes.Transparent };
        ToolTip.SetTip(skip, "Die Einführung kommt über „Hilfe“ → „Einführung“ wieder");
        skip.Click += (_, _) => Done();
        return new Border { Name = "prog-intro", Background = TileBg, BorderBrush = Accent, BorderThickness = new Thickness(3, 0, 0, 0), CornerRadius = new CornerRadius(4), Padding = new Thickness(12, 8),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = {
                    new TextBlock { Name = "prog-intro-step", Text = $"Kurze Einführung · Schritt {i + 1} von {steps.Length}", Foreground = Muted, FontSize = 11.5 },
                    new TextBlock { Name = "prog-intro-text", Text = steps[i], Foreground = Text, FontSize = 13, TextWrapping = TextWrapping.Wrap } } },
                Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), Children = { skip, next } }, 1) } } };
    }

    /// <summary>E8: Einführung über die Hilfe wieder abrufen.</summary>
    public void ProgramIntroShow(string key)
    {
        _settings.SeenHints.Remove("intro:" + key); _introStep[key] = 0; _introAsked.Add(key); SaveSettings();
        Show(key);
    }

    /// <summary>E7: Fehlerkarte des Programms – Alltagssatz, ein Knopf „Was tun?“, technischer Text aufklappbar.</summary>
    Control ProgramErrorCard(string key, FriendlyError fe, Action? retry) =>
        ErrorCard(fe, retry, Programs.TryGetValue(key, out var sp) ? sp.Studio : null, () => _ = fe.Kind == "speichern" && ProgramActive(key) is not null ? ProgramSaveAsPick(key) : ProgramOpenPick(key));

    /// <summary>G1/V2: Startseite des Programms.</summary>
    private Control ProgramStart(ProgramSpec spec)
    {
        var key = spec.Key;
        var tpl = new WrapPanel { Name = "prog-templates" };
        foreach (var t in spec.Templates)
        {
            var tt = t;
            var b = new Button { Name = "prog-tpl-" + t.Id, Width = 210, MinHeight = 96, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(14, 12), CornerRadius = new CornerRadius(14),
                Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Top,
                Content = new StackPanel { Spacing = 4, Children = {
                    new TextBlock { Text = t.Id == "leer" ? "＋ " + t.Name : t.Name, Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = t.Hint, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap } } } };
            b.Click += (_, _) => ProgramRun(key, () => ProgramNew(key, tt.Id));
            tpl.Children.Add(b);
        }
        var recent = new StackPanel { Name = "prog-recent", Spacing = 3 };
        var i = 0;
        foreach (var f in ProgRecent.Get(spec.Studio))
        {
            var path = f;
            var b = new Button { Name = $"prog-recent-{i++}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent, Padding = new Thickness(8, 5),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = Path.GetFileNameWithoutExtension(f), Foreground = Text, FontSize = 13 }, Pos(P(File.GetLastWriteTime(f).ToString("dd.MM.yyyy HH:mm", De), null, 11), 1) } } };
            ToolTip.SetTip(b, f);
            b.Click += (_, _) => ProgramRun(key, () => ProgramOpen(key, path));
            recent.Children.Add(b);
        }
        if (i == 0) recent.Children.Add(P("Noch nichts gespeichert.", null, 12));
        // E4 (T-0616): Öffnen über die Dateiauswahl statt eines getippten Pfads
        var open = Act("prog-open", "Anderes Dokument öffnen …", () => _ = ProgramOpenPick(key));
        ToolTip.SetTip(open, "Ein gespeichertes Dokument (" + ProgramDocument.Extension + ") von einem beliebigen Ort öffnen");
        recent.Children.Add(Line(open));
        var sp = new StackPanel { Spacing = 10, Children = {
            Card(new StackPanel { Spacing = 8, Children = { H("Neu", 15), tpl } }),
            Card(new StackPanel { Spacing = 6, Children = { H("Zuletzt", 15), recent } }) } };
        if (StudioTaskCard(spec.Studio, spec.Key) is { } tasks) sp.Children.Insert(1, tasks); // unter „Neu“: Die Vorlagen bleiben dort, wo man sie kennt
        var orphans = ProgramDocument.Orphans(ProgBackup, spec.Studio);
        if (orphans.Count > 0)
        {
            var rs = new StackPanel { Spacing = 4, Children = { H("Wiederherstellen", 15), P("Diese Dokumente wurden nicht gespeichert, aber automatisch gesichert.", null, 12) } };
            var k = 0;
            foreach (var o in orphans.Take(5))
            {
                var file = o;
                rs.Children.Add(Row(P($"{ProgramDocument.Load(file).Title} · gesichert {File.GetLastWriteTime(file):dd.MM. HH:mm}", Text, 12.5), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                    Act($"prog-restore-{k}", "Wiederherstellen", () => ProgramRun(key, () => { var d = ProgramDocument.Restore(file); File.Delete(file); var l = ProgramDocs(key); l.Add(d); _progActive[key] = l.Count - 1; _progMsg[key] = "Wiederhergestellt – bitte speichern."; }), true),
                    Act($"prog-discard-{k}", "Verwerfen", () => ProgramRun(key, () => File.Delete(file))) } }));
                k++;
            }
            sp.Children.Insert(0, Card(rs));
        }
        // E5 (T-0616): oberste Karte „Weitermachen“ – das zuletzt bearbeitete Dokument mit einem Klick
        if (LastDocOf(key) is { } last)
        {
            var go = new Button { Name = "prog-continue", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 10), CornerRadius = new CornerRadius(10),
                Background = TileBg, BorderBrush = Accent, BorderThickness = new Thickness(1),
                Content = new StackPanel { Spacing = 2, Children = {
                    new TextBlock { Text = "Weitermachen: " + Path.GetFileNameWithoutExtension(last), Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = "Zuletzt bearbeitet am " + File.GetLastWriteTime(last).ToString("dd.MM.yyyy 'um' HH:mm", De), Foreground = Muted, FontSize = 12 } } } };
            ToolTip.SetTip(go, last);
            go.Click += (_, _) => ProgramContinue(key);
            sp.Children.Insert(0, go);
        }
        if (spec.StartTop is { } top) sp.Children.Insert(0, top());
        return sp;
    }

    /// <summary>V2/V9: Reiter, Menüband, Arbeitsfläche, Eigenschaften, Statusleiste.</summary>
    private Control ProgramWork(ProgramSpec spec, ProgramDocument d, List<ProgramDocument> docs)
    {
        var (top, grid, status) = ProgramWorkParts(spec, d, docs);
        var inner = (StackPanel)((StackPanel)top).Children[1]; inner.Children.Add(grid); inner.Children.Add(status);
        return top;
    }

    private (Control Top, Control Grid, Control Status) ProgramWorkParts(ProgramSpec spec, ProgramDocument d, List<ProgramDocument> docs)
    {
        var key = spec.Key;
        // V9: Reiter der offenen Dokumente
        var tabs = new WrapPanel { Name = "prog-tabs" };
        for (var k = 0; k < docs.Count; k++)
        {
            var idx = k; var doc = docs[k]; var on = ReferenceEquals(doc, d);
            var t = new Button { Name = $"prog-tab-{k}", Content = doc.Title + (doc.Dirty ? " •" : ""), FontSize = 12.5, Padding = new Thickness(12, 5), Margin = new Thickness(0, 0, 2, 0), CornerRadius = new CornerRadius(10, 10, 0, 0),
                Background = on ? TileBg : Brushes.Transparent, Foreground = on ? Text : Muted, BorderBrush = on ? EdgeLit : Edge, BorderThickness = new Thickness(1, 1, 1, 0) };
            t.Click += (_, _) => { _progActive[key] = idx; Show(key); };
            var x = new Button { Name = $"prog-tab-close-{k}", Content = "×", Padding = new Thickness(6, 1), Margin = new Thickness(0, 0, 8, 0), Background = Brushes.Transparent, FontSize = 12 };
            ToolTip.SetTip(x, doc.Dirty ? (AutoSaveDocs ? "Schließen (Änderungen werden gespeichert)" : "Schließen (Änderungen sind automatisch gesichert)") : "Schließen");
            x.Click += (_, _) => ProgramRun(key, () => { ProgramKeep(key, doc); ProgramClose(key, idx); });
            tabs.Children.Add(t); tabs.Children.Add(x);
        }
        var plus = new Button { Name = "prog-tab-new", Content = "＋", Padding = new Thickness(10, 3), Background = Brushes.Transparent };
        ToolTip.SetTip(plus, "Neues Dokument (Strg+N)"); plus.Click += (_, _) => ProgramRun(key, () => ProgramNew(key));
        tabs.Children.Add(plus);

        // Menüband
        var saveAs = Field("prog-saveas-path", "Speichern unter: Ordner\\Name", 240);
        // E4 (T-0616): „Speichern unter …“ öffnet die Dateiauswahl; eintippen geht weiter im Klappfeld (auch für Claude)
        List<Control> SaveAsControls()
        {
            var open = RBtn("prog-saveas-open", "Speichern unter …", () => { });
            var pick = RBtn("prog-saveas-pick", "Ort wählen …", () => { open.Flyout?.Hide(); _ = ProgramSaveAsPick(key); }, true);
            var go = RBtn("prog-saveas", "Speichern unter", () => { open.Flyout?.Hide(); ProgramRun(key, () => ProgramSave(key, Need(saveAs, "Ordner und Namen"))); });
            go.Margin = new Thickness(0, 6, 0, 0);
            open.Flyout = new Flyout { Content = new StackPanel { Width = 380, Spacing = 4, Children = { pick, P("oder Ordner und Dateinamen eintippen", null, 12), saveAs, go } } };
            return [open];
        }
        // X17: frühere Stände – jedes Speichern behält den alten Stand (höchstens 20)
        List<Control> VersionControls()
        {
            if (d.Path is null || ProgramDocument.Versions(d.Path) is not { Count: > 0 } vs) return [];
            var list = new StackPanel { Width = 300, Spacing = 2, Children = { P($"Frühere Stände von „{d.Title}“ – Wiederherstellen lässt sich rückgängig machen.", null, 12) } };
            var open = RBtn("prog-versions", $"Versionen ({vs.Count})", () => { });
            for (var i = 0; i < vs.Count; i++)
            {
                var v = vs[i];
                var b = new Button { Name = $"prog-version-{i}", Content = v.When.ToString("dd.MM.yyyy  HH:mm:ss"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
                b.Click += (_, _) => { open.Flyout?.Hide(); ProgramRun(key, () => { d.RestoreVersion(v.File); _progMsg[key] = $"Stand vom {v.When:dd.MM.yyyy HH:mm} zurückgeholt – Strg+Z macht es rückgängig."; }); };
                list.Children.Add(b);
            }
            open.Flyout = new Flyout { Content = new ScrollViewer { MaxHeight = 420, Content = list } };
            return [open];
        }
        var groups = new List<(string, List<Control>)>
        {
            ("Datei", [
                RBtn("prog-new", "Neu", () => ProgramRun(key, () => ProgramNew(key))),
                RBtn("prog-save", "Speichern", () => ProgramRun(key, () => ProgramSave(key)), true),
                .. SaveAsControls(),
                .. VersionControls(),
                RBtn("prog-open-pick", "Öffnen …", () => _ = ProgramOpenPick(key)),
                RBtn("prog-close", "Schließen", () => ProgramRun(key, () => { ProgramKeep(key, d); ProgramClose(key, _progActive.GetValueOrDefault(key)); })),
            ]),
            ("Bearbeiten", [
                RBtn("prog-undo", "Rückgängig", () => ProgramRun(key, () => { if (!d.Undo()) throw new InvalidOperationException("Es gibt nichts rückgängig zu machen."); }), tip: d.UndoLabel is { } u ? "Rückgängig: " + u : "Nichts rückgängig zu machen"),
                RBtn("prog-redo", "Wiederholen", () => ProgramRun(key, () => { if (!d.Redo()) throw new InvalidOperationException("Es gibt nichts zu wiederholen."); })),
            ]),
        };
        var level = ProgramLevel;
        foreach (var (g, cmds) in spec.Ribbon)
        {
            if (!UserLevel.Shows(level, spec.GroupLevels.GetValueOrDefault(g))) continue;
            var shown = cmds.Where(c => UserLevel.Shows(level, spec.LevelOf(c))).Select(c => (Control)RBtn("prog-cmd-" + c.Name, c.Label, () => ProgramRun(key, () => c.Run(d)), c.Primary, c.Tip)).ToList();
            if (shown.Count > 0) groups.Add((g, shown));
        }
        if (spec.Exports.Count > 0)
            groups.Add(("Exportieren", spec.Exports.Take(level == UserLevel.Einfach ? spec.SimpleExports : int.MaxValue).Select(x => (Control)RBtn("prog-export-" + x.Name, x.Label, () => ProgramRun(key, () =>
            {
                var file = Path.Combine(WorkFolder(spec.Folder), DevStudio.SafeName(d.Title) + x.Extension);
                var res = x.Write(d, file);
                _core.Audit.Append(Principal.LocalOwner.Name, "program.export", $"{spec.Studio} {Path.GetFileName(res)}");
                _progMsg[key] = "Exportiert: " + res; NotifyFileResult(_progMsg[key]);
            }))).ToList()));
        if (level == UserLevel.Profi) groups.Add(("Profi", ProTools(spec, d)));
        var help = new List<Control>();
        if (spec.Help.Length > 0) help.Add(RBtn("prog-help", "? Hilfe", () => Toast(spec.Help)));
        help.Add(RBtn("prog-intro-show", "Einführung", () => ProgramIntroShow(key), tip: "Die drei kurzen Schritte zum Anfangen noch einmal zeigen"));
        groups.Add(("Hilfe", help));
        if (level == UserLevel.Einfach && !OsStyle)
        {
            // E2: die Bedienstufe bleibt auch im „Mehr“-Menü wählbar
            groups.Add(("Bedienstufe", [RBtn("prog-stufe-erweitert", "Mehr Werkzeuge (Erweitert)", () => ProgramSetLevel(UserLevel.Erweitert, key)), RBtn("prog-stufe-profi", "Alle Werkzeuge (Professionell)", () => ProgramSetLevel(UserLevel.Profi, key))]));
            groups = SimpleRibbon(groups, spec); // Wunsch 08.11.2026: Wichtigstes sichtbar, alles andere hinter „Mehr“
        }
        Control? osChrome = OsStyle ? OsProgramChrome(spec, groups, tabs, docs.Count) : null; // Windows-Menüzeile, Mac-Menüleiste, GNOME-Kopfleiste
        var ribbon = new WrapPanel { Name = osChrome is null ? "prog-ribbon" : "prog-ribbon-trendo" };
        string? lastGroup = null;
        foreach (var (g, items) in osChrome is null ? groups : [])
        {
            if (DawLook)
            {
                // DAW-Stil (Wunsch 06.11.2026): Gruppe als Segment wie in Bitwig („FILE · PLAY · EDIT“) – Beschriftung links, Knöpfe dicht nebeneinander
                var seg = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; // bricht bei vielen Befehlen innerhalb der Gruppe um
                if (g != lastGroup) seg.Children.Add(new TextBlock { Name = "prog-group-label", Text = g.ToUpperInvariant(), Foreground = Muted, FontSize = 9.5, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 6, 0) });
                foreach (var it in items) { it.Margin = new Thickness(0, 1, 2, 1); it.VerticalAlignment = VerticalAlignment.Center; seg.Children.Add(it); }
                var box = new Border { Margin = new Thickness(0, 4, 6, 4), Padding = new Thickness(6, 3), CornerRadius = new CornerRadius(8), Background = GroupBg, BorderBrush = PaneEdge, BorderThickness = new Thickness(1), Child = seg };
                ToolTip.SetTip(box, g); lastGroup = g;
                ribbon.Children.Add(box);
                continue;
            }
            var row = new WrapPanel(); foreach (var it in items) { it.Margin = new Thickness(0, 0, 4, 4); it.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(it); }
            if (spec.FillHeight)
            {
                // Gruppenname klein über den Befehlen (wie die Gruppen in Word), damit man sieht, was zusammengehört; gleicher Name direkt hintereinander nur einmal (Prüfung „Einfachheit“, 04.11.2026)
                // als Legende auf dem oberen Rand: kostet kaum Höhe, die Arbeitsfläche bleibt groß
                var grp = new Border { Margin = new Thickness(0, 7, 6, 3), Padding = new Thickness(6, 6, 6, 0), CornerRadius = new CornerRadius(10), Background = GroupBg, BorderBrush = PaneEdge, BorderThickness = new Thickness(1), Child = row };
                ToolTip.SetTip(grp, g);
                Control cell = grp;
                if (g != lastGroup)
                    cell = new Panel { Children = { grp, new Border { Name = "prog-group-label", Background = GroupBg, CornerRadius = new CornerRadius(4), Padding = new Thickness(4, 0), Margin = new Thickness(10, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false,
                        Child = new TextBlock { Text = g, Foreground = Muted, FontSize = 9.5, FontWeight = FontWeight.SemiBold } } } };
                lastGroup = g;
                ribbon.Children.Add(cell);
            }
            else
            ribbon.Children.Add(new Border { Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(10, 6, 10, 4), CornerRadius = new CornerRadius(12), Background = GroupBg, BorderBrush = PaneEdge, BorderThickness = new Thickness(1),
                Child = new StackPanel { Spacing = 2, Children = { row, new TextBlock { Text = g, Foreground = Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center } } } });
        }

        // Arbeitsfläche und Eigenschaften
        var work = new Border { Name = "prog-workspace", ClipToBounds = true, CornerRadius = new CornerRadius(14), Background = PaneBg, BorderBrush = PaneEdge, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Padding = new Thickness(spec.FillHeight ? 8 : 12), MinHeight = spec.FillHeight ? 220 : 420, Child = spec.FillHeight && spec.ScrollWorkspace ? new ScrollViewer { Name = "prog-workspace-scroll", Content = spec.Workspace(d), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled } : spec.Workspace(d) };
        // Paket F (A1 Profi-Bauplan): Werkzeuge links · große Mitte · Eigenschaften rechts · Bereich unten – jeder Rand einklappbar
        bool Hidden(string part) => _settings.HiddenPanels.Contains(key + ":" + part);
        var showTools = spec.Tools.Count > 0 && !Hidden("werkzeuge"); var showProps = spec.Properties is not null && !Hidden("eigenschaften") && !SimpleCollapsed(key); var showBottom = spec.Bottom is not null && !Hidden("unten");
        Control center = work;
        if (spec.Bottom is { } bottom && showBottom)
        {
            var bp = new Border { Name = "prog-bottom", Height = bottom.Height, CornerRadius = new CornerRadius(14), Background = PaneBg, BorderBrush = PaneEdge, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Padding = new Thickness(10, 6),
                Child = new DockPanel { Children = { Dk(new TextBlock { Text = bottom.Title, Foreground = Muted, FontSize = 12, FontWeight = FontWeight.SemiBold, Margin = new Thickness(2, 0, 0, 4) }, Avalonia.Controls.Dock.Top), bottom.Build(d) } } };
            var g2 = new Grid { RowDefinitions = new RowDefinitions("*,10,Auto"), Children = { work } }; Grid.SetRow(bp, 2); g2.Children.Add(bp); center = g2;
        }
        var dawOrder = DawLook; // DAW-Stil: Inspektor links (wie Bitwig, Studio One), Werkzeuge als Leiste rechts
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(dawOrder ? (showProps ? "300,12," : "") + "*" + (showTools ? ",10,Auto" : "") : (showTools ? "Auto,10," : "") + (showProps ? "*,12,300" : "*")) };
        var col = dawOrder && showProps ? 2 : 0;
        if (showTools)
        {
            // viele Werkzeuge: zwei Spalten wie in Photoshop/GIMP, damit nichts unten aus dem Bild läuft
            var two = spec.Tools.Count > 9;
            Panel rail = two ? new WrapPanel { Name = "prog-tools", Width = 88 } : new StackPanel { Name = "prog-tools", Spacing = 4 };
            foreach (var t in spec.Tools)
            {
                var on = t.Active(); var tt = t;
                var b = new Button { Name = "prog-cmd-" + t.Name, Content = t.Icon, Width = DawLook ? 36 : 40, Height = DawLook ? 36 : 40, Padding = new Thickness(0), FontSize = DawLook ? 15 : 17, CornerRadius = new CornerRadius(DawLook ? 6 : 10),
                    HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                    Background = on ? Accent : Brushes.Transparent, Foreground = on ? Brushes.White : Text, BorderBrush = on ? Brushes.Transparent : Edge, BorderThickness = new Thickness(1), Margin = two ? new Thickness(2) : default };
                ToolTip.SetTip(b, $"{t.Label} – {t.Tip}"); Avalonia.Automation.AutomationProperties.SetName(b, t.Label);
                b.Click += (_, _) => ProgramRun(key, () => tt.Select(d));
                rail.Children.Add(b);
            }
            var toolsBorder = new Border { Name = "prog-tools-panel", CornerRadius = new CornerRadius(14), Background = PaneBg, BorderBrush = PaneEdge, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Padding = new Thickness(6), Child = rail };
            if (dawOrder) grid.Children.Add(Pos(toolsBorder, showProps ? 4 : 2)); else { grid.Children.Add(toolsBorder); col = 2; }
        }
        grid.Children.Add(Pos(center, col));
        if (spec.Properties is { } props && showProps)
            grid.Children.Add(Pos(new Border { Name = "prog-properties", CornerRadius = new CornerRadius(14), Background = PaneBg, BorderBrush = PaneEdge, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Padding = new Thickness(12),
                Child = spec.FillHeight ? new ScrollViewer { Content = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 8, 0), Children = { (DawLook ? new TextBlock { Text = PlainWords.For(ProgramLevel, "INSPEKTOR"), Foreground = Muted, FontSize = 10.5, FontWeight = FontWeight.Bold } : H("Eigenschaften", 13)), props(d) } } } : new StackPanel { Spacing = 6, Children = { (DawLook ? new TextBlock { Text = PlainWords.For(ProgramLevel, "INSPEKTOR"), Foreground = Muted, FontSize = 10.5, FontWeight = FontWeight.Bold } : H("Eigenschaften", 13)), props(d) } } }, dawOrder ? 0 : col + 2));

        // Ränder ein- und ausklappen (gemerkt je Programm)
        var toggles = new StackPanel { Name = "prog-panel-toggles", Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(10, 0, 0, 0) };
        void Toggle(string part, string icon, string label, bool on)
        {
            var b = new Button { Name = "prog-toggle-" + part, Content = icon, FontSize = 12, Padding = new Thickness(7, 1), Background = on ? Brush.Parse("#22FFFFFF") : Brushes.Transparent, Foreground = on ? Text : Muted, BorderThickness = new Thickness(0) };
            ToolTip.SetTip(b, (on ? "Einklappen: " : "Ausklappen: ") + label);
            b.Click += (_, _) => { var id = key + ":" + part; if (!_settings.HiddenPanels.Remove(id)) _settings.HiddenPanels.Add(id); SaveSettings(); Show(key); };
            toggles.Children.Add(b);
        }
        if (spec.Tools.Count > 0) Toggle("werkzeuge", "◧", "Werkzeuge", showTools);
        if (spec.Bottom is not null) Toggle("unten", "⬓", spec.Bottom.Title, showBottom);
        if (spec.Properties is not null) Toggle("eigenschaften", "◨", "Eigenschaften", showProps);
        toggles.IsVisible = !SimpleCollapsed(key); // E2: in „Einfach“ erst nach „Mehr Werkzeuge“

        // Statusleiste
        var status = new Border { Name = "prog-status", CornerRadius = new CornerRadius(10), Background = DawLook ? DawLcd : Brush.Parse(C("#331A1A1A", "#14000000")), BorderBrush = DawLook ? DawEdge : null, BorderThickness = new Thickness(DawLook ? 1 : 0), Padding = new Thickness(12, 5),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Children = {
                new TextBlock { Name = "prog-status-text", Text = spec.Status?.Invoke(d) ?? "", Foreground = DawLook ? DawDigits : Muted, FontSize = 12, FontFamily = DawLook ? new FontFamily("Consolas, monospace") : FontFamily.Default, VerticalAlignment = VerticalAlignment.Center },
                Pos(new TextBlock { Name = "prog-saved", Text = AutoSaveDocs ? (d.Path is null && !d.Dirty ? "Noch leer" : (d.Path is null ? "" : Path.GetFileName(d.Path) + " · ") + (d.Dirty ? "wird gespeichert …" : "✓ Gespeichert") + $" · {d.Changed:HH:mm}")
                    : (d.Path is null ? "Noch nicht gespeichert" : Path.GetFileName(d.Path)) + (d.Dirty ? " · geändert, wird automatisch gesichert" : "") + $" · {d.Changed:HH:mm}", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, 1),
                Pos(toggles, 2) } } };
        if (osChrome is not null) return (new StackPanel { Spacing = 0, Children = { new Border(), new StackPanel { Spacing = 8, Children = { osChrome } } } }, grid, status);
        Control ribbonHost = DawLook ? new Border { Name = "prog-ribbon-strip", CornerRadius = new CornerRadius(12), Background = DawPanel, BorderBrush = DawEdge, BorderThickness = new Thickness(1), Padding = new Thickness(8, 2, 8, 0), Child = ribbon } : ribbon; // DAW-Werkzeugleiste
        return (new StackPanel { Spacing = 0, Children = { tabs, new StackPanel { Spacing = 8, Children = { ribbonHost } } } }, grid, status);

        Button RBtn(string name, string label, Action go, bool primary = false, string? tip = null)
        {
            var b = new Button { Name = name, Content = PlainWords.For(ProgramLevel, label), FontSize = DawLook ? 12 : 12.5, Padding = DawLook ? new Thickness(9, 4) : new Thickness(12, 6), CornerRadius = new CornerRadius(DawLook ? 5 : 8), Foreground = primary ? Brushes.White : Text,
                Background = primary ? Accent : DawLook ? DawButton : Brushes.Transparent, BorderBrush = DawLook ? DawEdge : Edge, BorderThickness = new Thickness(primary || DawLook ? 0 : 1) };
            if (tip is not null) ToolTip.SetTip(b, tip);
            b.Click += (_, _) => go(); return b;
        }
    }

    // ---------- Masterprompt 131: Bedienstufen ----------
    public int ProgramLevel => UserLevel.Of(_settings.Level);

    /// <summary>Bedienstufe wechseln – nur die Werkzeugauswahl ändert sich, Dokumente bleiben unberührt.</summary>
    public void ProgramSetLevel(int level, string? reopen = null)
    {
        _settings.Level = UserLevel.Id(level); SaveSettings();
        _core.Audit.Append(Principal.LocalOwner.Name, "program.level", _settings.Level);
        RefreshDesktopTiles(); // E12: Desktop folgt der Stufe
        if (reopen is not null) Show(reopen);
    }

    private Control LevelSwitch(string key)
    {
        var bar = new StackPanel { Name = "prog-level", Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        for (var i = 0; i < 3; i++)
        {
            var lv = i; var on = ProgramLevel == lv;
            var b = new Button { Name = "prog-level-" + UserLevel.Ids[i], Content = UserLevel.Names[i], FontSize = 11.5, Padding = new Thickness(9, 3), Margin = new Thickness(0),
                CornerRadius = i == 0 ? new CornerRadius(4, 0, 0, 4) : i == 2 ? new CornerRadius(0, 4, 4, 0) : new CornerRadius(0),
                // neutraler Segmentschalter (Vorgabe 13.11.2026): aktive Stufe im Akzent, sonst ruhig mit feinem Rand
                Background = on ? Accent : Brushes.Transparent, Foreground = on ? Brushes.White : Text, BorderBrush = Edge, BorderThickness = new Thickness(1) };
            ToolTip.SetTip(b, lv switch
            {
                UserLevel.Einfach => "Einfach: nur das Wichtigste, große Schritte",
                UserLevel.Erweitert => "Erweitert: alle Fachwerkzeuge",
                _ => "Professionell: zusätzlich Verlauf und gespeicherte Daten; alle Befehle findest du mit Strg+K",
            });
            b.Click += (_, _) => ProgramSetLevel(lv, key);
            bar.Children.Add(b);
        }
        return bar;
    }

    /// <summary>Profi-Werkzeuge: Verlauf mit Sprung zurück, Projektdaten direkt bearbeiten. Alle Befehle (auch die der Stufe verborgenen) findet die Befehlsleiste Strg+K (E11).</summary>
    private List<Control> ProTools(ProgramSpec spec, ProgramDocument d)
    {
        var key = spec.Key;
        Button Tool(string name, string label, string tip, Control content)
        {
            var b = new Button { Name = name, Content = label, FontSize = 12.5, Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1),
                Flyout = new Flyout { Content = content } };
            ToolTip.SetTip(b, tip); return b;
        }
        // Verlauf
        var hist = new StackPanel { Name = "prog-history-list", Spacing = 2 };
        var steps = d.History;
        if (steps.Count == 0) hist.Children.Add(P("Noch keine Änderung.", null, 12));
        for (var i = 0; i < Math.Min(steps.Count, 25); i++)
        {
            var back = i + 1;
            var b = new Button { Name = $"prog-history-{i}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, Padding = new Thickness(8, 3),
                Content = new TextBlock { Text = steps[i], Foreground = Text, FontSize = 12.5 } };
            ToolTip.SetTip(b, back == 1 ? "Diesen Schritt rückgängig machen" : $"Bis vor diesen Schritt zurück ({back} Schritte)");
            b.Click += (_, _) => ProgramRun(key, () => { for (var n = 0; n < back; n++) d.Undo(); _progMsg[key] = $"{back} Schritt(e) zurück – „Wiederholen“ holt sie zurück."; });
            hist.Children.Add(b);
        }

        // Projektdaten
        var json = new TextBox { Name = "prog-data-text", Text = d.Data.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), AcceptsReturn = true, FontFamily = new FontFamily("Consolas, Cascadia Mono, monospace"), FontSize = 12, Width = 560, Height = 380, TextWrapping = TextWrapping.NoWrap };
        var apply = new Button { Name = "prog-data-apply", Content = "Übernehmen", Background = Accent, Foreground = Brushes.White, Padding = new Thickness(12, 6) };
        apply.Click += (_, _) => ProgramRun(key, () => { d.ReplaceData(json.Text ?? ""); _progMsg[key] = "Projektdaten übernommen – „Rückgängig“ stellt den alten Stand her."; });

        return [
            Tool("prog-history", $"Verlauf ({steps.Count})", "Änderungen ansehen und zu einem früheren Stand zurückspringen", new ScrollViewer { MaxHeight = 360, Width = 320, Content = hist }),
            Tool("prog-data", PlainWords.Plain("Projektdaten"), "Die gespeicherten Daten des Dokuments direkt bearbeiten (Fachwort: Projektdaten, JSON)", new StackPanel { Spacing = 6, Children = { P("Genau das, was gespeichert wird. Übernehmen ist ein Schritt im Verlauf.", null, 12), json, apply } }),
        ];
    }
}
