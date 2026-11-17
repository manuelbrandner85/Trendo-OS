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
public sealed record ProgramTool(string Name, string Icon, string Label, string Tip, Action<ProgramDocument> Select, Func<bool> Active)
{
    /// <summary>T-0728: Taste, die das Werkzeug wählt (z. B. „B“ Pinsel wie in Photoshop) – steht in der Menüleiste unter „Werkzeuge“ und gilt im Programm, nicht in Textfeldern.</summary>
    public string Shortcut { get; init; } = "";
}
/// <summary>Paket F (A1): Bereich unten (Zeitleiste, Ebenen, Messwerte …).</summary>
public sealed record ProgramPanel(string Title, Func<ProgramDocument, Control> Build, double Height = 180);
/// <summary>T-0686 (V-02): der erste Schritt in einem leeren Dokument – die Hülle zeigt dafür einen großen Knopf mitten auf der Arbeitsfläche.
/// <paramref name="Run"/> löst die echte Funktion aus (auch wenn die Eigenschaften-Spalte in „Einfach“ eingeklappt ist); <paramref name="Empty"/> sagt, ob das Dokument noch leer ist.</summary>
public sealed record ProgramFirstStep(string Label, string Hint, Action<ProgramDocument> Run, Func<ProgramDocument, bool> Empty);

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
    /// <summary>Welche Menüband-Befehle „Einfach“ zuerst direkt zeigt (Namen ohne „prog-cmd-“), vor der übrigen Reihenfolge; die Zahl bleibt
    /// <see cref="MainWindow.SimpleCommands"/> – der Rest liegt unter „Mehr“.</summary>
    public IReadOnlyList<string>? SimplePrefer { get; init; }
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
    /// <summary>T-0686 (V-02): erster Schritt im leeren Dokument (Text + Aktion); fehlt = kein Knopf.</summary>
    public ProgramFirstStep? FirstStep { get; init; }
    /// <summary>T-0700 (offener Punkt aus T-0686): Programme ohne eigenen Hauptbefehl – die Ausgabe, um die es geht (Bericht, Druckdatei, fertiges Bild), ist die eine goldene Aktion.
    /// Name eines Eintrags aus <see cref="Exports"/>; steht auch in „Einfach“ direkt im Menüband.</summary>
    public string? PrimaryExport { get; init; }
    /// <summary>V-09 (T-0700): Was die Hauptaktion gerade verhindert (fehlendes Modell, Werkzeug, Konto) – null, wenn nichts fehlt. Der Hinweis steht direkt neben der Hauptaktion.</summary>
    public Func<ProgramDocument, ProgramBlocker?>? Blocker { get; init; }
    /// <summary>T-0710: Menü der Menüleiste je Menüband-Gruppe, wenn es anders heißen soll als die Gruppe (z. B. „Werkzeuge“ → „Bild“). Fehlt = Name der Gruppe;
    /// „Datei“, „Bearbeiten“, „Ansicht“ und „Hilfe“ legen die Gruppe in das Standardmenü.</summary>
    public Dictionary<string, string> MenuNames { get; init; } = [];
    /// <summary>T-0710: fachliche Einträge, die nicht im Menüband stehen (eigene Menüs oder Ergänzungen der Standardmenüs) – mit denselben Handlern wie die Programmfunktionen.
    /// Kürzel, die hier stehen, löst die Hülle im Programm aus.</summary>
    public Func<ProgramDocument, IEnumerable<MenuTop>>? Menus { get; init; }
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

    // ---------- T-0680: absturzsicher – Sperre, Wiederherstellung, Rückfragen ----------
    /// <summary>So oft sichert die Hülle alle geänderten Dokumente, auch wenn ein Programm ohne Neuzeichnen ändert (dann greift die kurze Pause nach der
    /// letzten Änderung nicht). 20 s + Schreibzeit bleibt sicher unter dem Ziel „höchstens 30 s Verlust nach einem Absturz“.</summary>
    public static readonly TimeSpan ProgramAutosaveEvery = TimeSpan.FromSeconds(20);
    /// <summary>Kurze Pause nach der letzten Änderung, dann wird von selbst gespeichert.</summary>
    public static readonly TimeSpan ProgramSaveDelay = TimeSpan.FromSeconds(1.5);
    private ProgramLocks? _progLocks;
    /// <summary>Sperren dieses Fensters (eine Datei – ein Fenster). Beim Schließen des Fensters werden sie freigegeben.</summary>
    public ProgramLocks ProgLocks
    {
        get
        {
            if (_progLocks is not null) return _progLocks;
            var l = _progLocks = new ProgramLocks(Path.Combine(_core.DataRoot, "programme", "sperren"));
            Closed += (_, _) => l.Dispose();
            return l;
        }
    }
    /// <summary>Dokumente, die in einem anderen Fenster offen sind (hier nur lesen) – bis die Person gewählt hat.</summary>
    private readonly Dictionary<string, ProgramLocks.Holder> _progLocked = [];
    /// <summary>Gefundene Notsicherung je Dokument: Datei und Zeitpunkt – bis „Wiederherstellen“ oder „Verwerfen“.</summary>
    private readonly Dictionary<string, (string File, DateTime When)> _progRecover = [];
    /// <summary>Dokumente, deren Datei von außen geändert wurde – bis entschieden ist, welche Fassung bleibt.</summary>
    private readonly HashSet<string> _progConflict = [];
    /// <summary>Beschädigte Datei je Programm mit den Ständen, aus denen sie sich zurückholen lässt.</summary>
    private readonly Dictionary<string, (string Path, List<ProgramDocument.Recovery> From)> _progRescue = new(StringComparer.OrdinalIgnoreCase);
    private bool _progConflictNew;

    public bool ProgramIsLocked(ProgramDocument d) => _progLocked.ContainsKey(d.Id);
    public (string File, DateTime When)? ProgramRecovery(ProgramDocument d) => _progRecover.TryGetValue(d.Id, out var r) ? r : null;
    public bool ProgramHasConflict(ProgramDocument d) => _progConflict.Contains(d.Id);
    public IReadOnlyList<ProgramDocument.Recovery> ProgramRescueOffers(string key) => _progRescue.TryGetValue(key, out var r) ? r.From : [];

    static string Clock(DateTime t) => t.Date == DateTime.Today ? $"heute um {t:HH:mm} Uhr" : $"am {t:dd.MM.yyyy} um {t:HH:mm} Uhr";

    /// <summary>Alle Speicherwege der Hülle: erst die Sperre für das Ziel, dann speichern (atomar), dann die alte Sperre freigeben.</summary>
    void DocSave(ProgramDocument d, string? target = null, bool keepVersion = true, bool overwriteChanged = false)
    {
        var to = ProgramDocument.TargetOf(target ?? d.Path ?? throw new InvalidOperationException("Bitte einen Speicherort wählen."));
        var old = d.Path; var same = old is not null && ProgramDocument.SameFile(old, to);
        if (!(same && d.ReadOnly) && ProgLocks.Acquire(to) is { } other)
        {
            if (same) { d.ReadOnly = true; _progLocked[d.Id] = other; } // die Sperre ging verloren (z. B. Datenordner aufgeräumt) und ein anderes Fenster hat die Datei jetzt
            throw new IOException($"„{Path.GetFileName(to)}“ ist gerade in einem anderen Trendo-Fenster geöffnet (seit {Clock(other.Since)}). Bitte wähle einen anderen Namen – so wird dort nichts überschrieben.");
        }
        try { d.Save(to, keepVersion, overwriteChanged); }
        catch (FileChangedOutsideException) { if (_progConflict.Add(d.Id)) _progConflictNew = true; throw; }
        catch { if (!same) ProgLocks.Release(to); throw; }
        _progConflict.Remove(d.Id);
        if (!d.ReadOnly) _progLocked.Remove(d.Id); // unter neuem Namen gespeichert: kein „nur lesen“ mehr
        if (!same && old is not null) ProgLocks.Release(old);
    }

    /// <summary>Notsicherung schreiben; die eines noch unbenannten Dokuments wird für dieses Fenster gesperrt, damit ein zweites Fenster sie nicht als „abgestürzt“ anbietet.</summary>
    void DocBackup(ProgramDocument d)
    {
        if (d.Autosave(ProgBackup) is { } f && ProgramDocument.SameFile(f, d.BackupFile(ProgBackup))) ProgLocks.Acquire(f);
    }

    /// <summary>Nach dem Laden: Sperre holen (sonst nur lesen) und nach einer Notsicherung sehen.</summary>
    string DocAttach(ProgramSpec spec, ProgramDocument d, string path)
    {
        if (ProgLocks.Acquire(path) is { } holder)
        {
            d.ReadOnly = true; _progLocked[d.Id] = holder;
            return $"„{Path.GetFileName(path)}“ ist schon in einem anderen Trendo-Fenster geöffnet – hier nur zum Lesen.";
        }
        // Notsicherung neben der Datei: neuer als die Datei = nicht gespeicherte Arbeit aus einer abgebrochenen Sitzung
        var side = ProgramDocument.AutosaveFile(path); var keep = ProgramDocument.RecoveryFile(path); var note = "";
        try
        {
            if (File.Exists(side))
            {
                if (File.GetLastWriteTimeUtc(side) <= File.GetLastWriteTimeUtc(path)) { }  // älter als die Datei: überholt, nichts anbieten
                else if (ProgramDocument.Readable(side, spec.Studio)) SafeFile.Move(side, keep); // ab jetzt unter eigenem Namen – weiteres Speichern löscht das Angebot nicht
                else { SafeFile.Move(side, side + ".beschaedigt"); note = " Eine automatische Sicherung dazu war beschädigt und wurde übersprungen."; }
            }
            if (File.Exists(keep))
            {
                if (ProgramDocument.Readable(keep, spec.Studio)) _progRecover[d.Id] = (keep, File.GetLastWriteTime(keep));
                else { SafeFile.Move(keep, side + ".beschaedigt"); note = " Eine automatische Sicherung dazu war beschädigt und wurde übersprungen."; }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return "Geöffnet: " + Path.GetFileName(path) + note;
    }

    /// <summary>Gefundene Notsicherung übernehmen (ein Schritt, rückgängig machbar) oder verwerfen. Die Sicherung verschwindet erst danach.</summary>
    public void ProgramRecoverApply(string key, bool restore)
    {
        if (ProgramActive(key) is not { } d || !_progRecover.TryGetValue(d.Id, out var r)) return;
        if (restore)
        {
            d.TakeOver(ProgramDocument.Load(r.File, d.Studio), "Sicherung wiederhergestellt");
            _progMsg[key] = $"Der gesicherte Stand von {Clock(r.When)} ist zurück – Strg+Z macht es rückgängig.";
        }
        else _progMsg[key] = "Sicherung verworfen.";
        _progRecover.Remove(d.Id);
        // Die gefundene Sicherung darf erst weg, wenn ihr Inhalt woanders sicher liegt: in der Datei (automatisches Speichern) oder als normale Notsicherung daneben
        if (restore && d.Dirty && !ProgramAutoSave(key, d)) DocBackup(d);
        try { File.Delete(r.File); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Datei in einem anderen Fenster offen: „lesen“ (so lassen), „kopie“ (als eigene Datei weiterarbeiten) oder „pruefen“ (ist das andere Fenster inzwischen zu?).</summary>
    public void ProgramLockChoice(string key, string choice)
    {
        if (ProgramActive(key) is not { } d) return;
        switch (choice)
        {
            case "kopie":
                d.Detach(d.Title + " (Kopie)"); _progLocked.Remove(d.Id);
                _progMsg[key] = "Du arbeitest jetzt mit einer Kopie – das andere Dokument bleibt unverändert.";
                break;
            case "pruefen" when d.Path is { } p && d.ReadOnly:
                if (ProgLocks.Acquire(p) is { } still) { _progLocked[d.Id] = still; _progMsg[key] = "Das Dokument ist noch im anderen Fenster offen."; break; }
                _progLocked.Remove(d.Id);
                if (!d.Dirty) d.ReloadFromDisk(); // den neuesten Stand zeigen; eigene Änderungen bleiben (dann fragt das Speichern nach)
                d.ReadOnly = false; _progMsg[key] = "Das andere Fenster ist zu – du kannst jetzt hier weiterarbeiten.";
                break;
            default:
                _progLocked.Remove(d.Id); _progMsg[key] = "Nur zum Lesen geöffnet. Zum Ändern: „Speichern unter …“ oder das andere Fenster schließen.";
                break;
        }
    }

    /// <summary>Datei von außen geändert: „meine“ (überschreiben – die andere Fassung bleibt als Version), „kopie“ (unter neuem Namen speichern) oder „laden“ (die andere Fassung übernehmen).</summary>
    public void ProgramResolveConflict(string key, string choice)
    {
        if (ProgramActive(key) is not { } d || d.Path is not { } p) return;
        var spec = Programs[key];
        switch (choice)
        {
            case "meine":
                DocSave(d, overwriteChanged: true);
                _progMsg[key] = "Deine Fassung ist gespeichert. Die andere findest du unter „Versionen“.";
                break;
            case "kopie":
                var dir = Path.GetDirectoryName(Path.GetFullPath(p))!; var stem = Path.GetFileNameWithoutExtension(p); var copy = Path.Combine(dir, $"{stem} (Kopie){ProgramDocument.Extension}");
                for (var n = 2; File.Exists(copy); n++) copy = Path.Combine(dir, $"{stem} (Kopie {n}){ProgramDocument.Extension}");
                DocSave(d, copy); ProgRecent.Add(spec.Studio, d.Path!); RememberLastDoc(key, d.Path!);
                _progMsg[key] = "Als Kopie gespeichert: " + d.Path;
                break;
            case "laden":
                d.ReloadFromDisk();
                _progMsg[key] = "Die Fassung von der Platte ist geladen – Strg+Z holt deine zurück.";
                break;
            default: return;
        }
        _progConflict.Remove(d.Id);
    }

    /// <summary>Beschädigte Datei: aus einem früheren Stand zurückholen. Die beschädigte Datei bleibt daneben als „….beschaedigt“ liegen.</summary>
    public ProgramDocument ProgramRescue(string key, int index)
    {
        var spec = Programs[key];
        if (!_progRescue.TryGetValue(key, out var r) || index < 0 || index >= r.From.Count) throw new InvalidOperationException("Es gibt keinen Stand zum Zurückholen.");
        var from = r.From[index]; var src = from.File;
        // eine schon einmal angebotene Sicherung wieder als normale Notsicherung führen – das nächste Speichern räumt sie dann weg
        if (ProgramDocument.SameFile(src, ProgramDocument.RecoveryFile(r.Path))) { src = ProgramDocument.AutosaveFile(r.Path); SafeFile.Move(from.File, src); }
        var d = ProgramDocument.Restore(src, r.Path);
        if (ProgLocks.Acquire(r.Path) is { } holder) { d.ReadOnly = true; _progLocked[d.Id] = holder; }
        else try { File.Copy(r.Path, r.Path + ".beschaedigt", true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        var l = ProgramDocs(key); l.Add(d); _progActive[key] = l.Count - 1; _progRescue.Remove(key);
        _progMsg[key] = $"Zurückgeholt: Stand von {Clock(from.When)}." + (AutoSaveDocs ? "" : " Bitte speichern.");
        _core.Audit.Append(Principal.LocalOwner.Name, "program.rescue", $"{spec.Studio} {Path.GetFileName(r.Path)}");
        return d;
    }

    /// <summary>T-0680: Karten über der Arbeitsfläche – beschädigte Datei, gefundene Sicherung, anderes Fenster, von außen geändert. Jede mit klaren Knöpfen.</summary>
    List<Control> ProgramSafetyCards(string key, ProgramDocument? d)
    {
        var cards = new List<Control>();
        Control Card(string name, string text, params Control[] buttons)
        {
            var row = new WrapPanel(); row.Children.AddRange(buttons);
            return new Border { Name = name, Background = TileBg, BorderBrush = Accent, BorderThickness = new Thickness(3, 0, 0, 0), CornerRadius = new CornerRadius(4), Padding = new Thickness(12, 8),
                Child = new StackPanel { Spacing = 6, Children = { new TextBlock { Name = name + "-text", Text = text, Foreground = Text, FontSize = 13, TextWrapping = TextWrapping.Wrap }, row } } };
        }
        Button B(string name, string label, Action run, bool primary = false)
        {
            var b = new Button { Name = name, Content = label, Padding = new Thickness(12, 4), Margin = new Thickness(0, 0, 6, 4) };
            if (primary) { b.Background = Accent; b.Foreground = Brushes.White; }
            b.Click += (_, _) => ProgramRun(key, run);
            return b;
        }
        if (_progRescue.TryGetValue(key, out var rescue))
        {
            var name = Path.GetFileName(rescue.Path);
            var bs = rescue.From.Take(3).Select((f, i) => (Control)B($"prog-rescue-{i}", $"Stand von {Clock(f.When)} öffnen" + (f.Kind == "sicherung" ? " (automatisch gesichert)" : " (frühere Version)"), () => ProgramRescue(key, i), i == 0)).ToList();
            bs.Add(B("prog-rescue-close", rescue.From.Count == 0 ? "Verstanden" : "Nicht jetzt", () => _progRescue.Remove(key)));
            cards.Add(Card("prog-rescue", rescue.From.Count == 0
                ? $"„{name}“ ist beschädigt und lässt sich nicht öffnen. Es gibt davon leider keinen früheren Stand. Die Datei bleibt unverändert liegen."
                : $"„{name}“ ist beschädigt und lässt sich nicht öffnen. Du kannst den letzten gesicherten Stand zurückholen – die beschädigte Datei bleibt zur Sicherheit daneben liegen.", [.. bs]));
        }
        if (d is null) return cards;
        if (_progRecover.TryGetValue(d.Id, out var rec))
            cards.Add(Card("prog-recover", $"Von „{d.Title}“ gibt es einen Stand von {Clock(rec.When)}, der nicht mehr gespeichert wurde – Trendo hat ihn automatisch gesichert. Möchtest du ihn zurückholen?",
                B("prog-recover-yes", "Wiederherstellen", () => ProgramRecoverApply(key, true), true),
                B("prog-recover-no", "Verwerfen", () => ProgramRecoverApply(key, false))));
        if (_progLocked.TryGetValue(d.Id, out var holder))
            cards.Add(Card("prog-lock", $"„{d.Title}“ ist schon in einem anderen Trendo-Fenster geöffnet (seit {Clock(holder.Since)}). Damit dort nichts überschrieben wird, siehst du es hier nur zum Lesen.",
                B("prog-lock-readonly", "Nur lesen", () => ProgramLockChoice(key, "lesen"), true),
                B("prog-lock-copy", "Als Kopie öffnen", () => ProgramLockChoice(key, "kopie")),
                B("prog-lock-retry", "Nochmal prüfen", () => ProgramLockChoice(key, "pruefen"))));
        if (_progConflict.Contains(d.Id))
            cards.Add(Card("prog-conflict", $"„{Path.GetFileName(d.Path)}“ wurde außerhalb dieses Fensters geändert" + (d.DiskTime is { } t ? $" ({Clock(t)})" : "") + ". Welche Fassung soll bleiben? Bis du entschieden hast, wird hier nichts überschrieben.",
                B("prog-conflict-keep", "Meine Fassung speichern", () => ProgramResolveConflict(key, "meine"), true),
                B("prog-conflict-copy", "Meine als Kopie speichern", () => ProgramResolveConflict(key, "kopie")),
                B("prog-conflict-load", "Die andere Fassung laden", () => ProgramResolveConflict(key, "laden"))));
        return cards;
    }

    /// <summary>Studio-Programm anmelden: Seite, Reiter und Suchbegriffe wie bei jeder Studio-Seite.</summary>
    private void RegisterProgram(List<(string, string, string)> tabs, ProgramSpec spec, params string[] words)
    {
        Programs[spec.Key] = spec;
        RegisterTab(tabs, spec.Key, spec.Title, spec.Studio, () => ProgramView(spec), words);
        if (_progAutosave is null)
        {
            _progAutosave = new DispatcherTimer { Interval = ProgramAutosaveEvery };
            _progAutosave.Tick += (_, _) => ProgramAutosaveAll();
            _progAutosave.Start();
            Closing += (_, e) =>
            {
                // Gegenprüfung T-0732: laufende Aufnahme sauber beenden und ins Dokument übernehmen, laufenden Stapel abbrechen –
                // vor der Sicherung, damit die Aufnahme mitgesichert wird. Nicht, wenn das Schließen gleich abgelehnt wird (Einrichtung läuft).
                if (!e.Cancel && MayClose(e.IsProgrammatic)) { AuRecordStopOnClose(); MediaBatchStopOnClose(); }
                ProgramAutosaveAll(); // E3: beim Beenden nichts verlieren
            };
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
        ProgramDocument d;
        try { d = ProgramDocument.Load(path, spec.Studio); }
        catch (InvalidDataException e) when (ProgramDocument.ProblemOf(e) == ProgramDocument.LoadProblem.Damaged)
        {
            // T-0680: beschädigte Datei – kein stilles leeres Dokument, sondern eine klare Meldung mit den Ständen, aus denen sie sich zurückholen lässt
            var from = ProgramDocument.Recoveries(path, spec.Studio);
            _progRescue[key] = (path, from);
            throw new InvalidDataException($"„{Path.GetFileName(path)}“ ist beschädigt und lässt sich nicht öffnen." + (from.Count > 0 ? " Du kannst den letzten gesicherten Stand zurückholen." : " Es gibt davon leider keinen früheren Stand."));
        }
        _progRescue.Remove(key);
        var opened = DocAttach(spec, d, path);
        l.Add(d); _progActive[key] = l.Count - 1; ProgRecent.Add(spec.Studio, path); RememberLastDoc(key, path);
        _progMsg[key] = opened;
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
        DocSave(d, target); ProgRecent.Add(spec.Studio, d.Path!); RememberLastDoc(key, d.Path!); DropBackup(d);
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
        try { var f = d.BackupFile(ProgBackup); ProgLocks.Release(f); if (File.Exists(f)) File.Delete(f); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>E3 (T-0616): geändertes Dokument selbst speichern – ein neues landet beim ersten Speichern im Studio-Ordner. Leere, unveränderte Dokumente
    /// legen keine Datei an. Höchstens alle <see cref="AutoVersionGap"/> bleibt ein früherer Stand als Version. false = nicht gespeichert (aus oder Fehler).</summary>
    public bool ProgramAutoSave(string key, ProgramDocument d)
    {
        if (!AutoSaveDocs || !d.Dirty || !Programs.TryGetValue(key, out var spec)) return false;
        // T-0680: nur zum Lesen offen (anderes Fenster) oder von außen geändert und noch nicht entschieden: nichts überschreiben – die Notsicherung läuft weiter
        if (d.ReadOnly && d.Path is not null || _progConflict.Contains(d.Id)) return false;
        // Claude hat geändert und du hast noch nicht entschieden (behalten/verwerfen) oder eine Live-Animation läuft: erst danach speichern – „Verwerfen“ lässt die Datei so unverändert
        if (_claudeChanges.TryGetValue(key, out var pending) && pending.DocId == d.Id || _liveAnim.ContainsKey(key)) return false;
        try
        {
            if (d.Path is null)
            {
                DocSave(d, ProgramNewTarget(spec, d));
                ProgRecent.Add(spec.Studio, d.Path!); RememberLastDoc(key, d.Path!); _progAutoNamed.Add(d.Id);
                _core.Audit.Append(Principal.LocalOwner.Name, "program.autosave", $"{spec.Studio} {Path.GetFileName(d.Path)}");
            }
            else if (ProgramAutoRename(spec, d) is { } renamed)
            {
                // T-0620: von selbst angelegte Datei („Unbenannt.tdoc“) folgt dem neuen Titel – samt Versionen; die alte Datei verschwindet
                var oldPath = d.Path;
                DocSave(d, renamed, keepVersion: false);
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
            else DocSave(d, keepVersion: ProgramDocument.VersionDue(d.Path, AutoVersionGap));
            DropBackup(d);
            if (_progErr.TryGetValue(key, out var old) && old.Error.Kind == "speichern") { _progErr.Remove(key); if (_progMsg.GetValueOrDefault(key) == old.Error.Sentence) _progMsg.Remove(key); }
            return true;
        }
        catch (FileChangedOutsideException e)
        {
            // T-0680: von außen geändert – nicht überschreiben, sondern nachfragen (Karte mit drei Knöpfen); bis dahin sichert die Notsicherung
            _progMsg[key] = e.Message;
            return false;
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
        foreach (var (key, l) in _progDocs) foreach (var d in l.ToList())
            if (ProgramAutoSave(key, d)) n++;
            else if (d.Dirty) try { DocBackup(d); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } // T-0680: nicht gespeichert (aus, Rückfrage offen, Fehler) – dann wenigstens sichern
        if (_progConflictNew) { _progConflictNew = false; if (Programs.ContainsKey(CurrentView)) Show(CurrentView); return n; } // die Rückfrage „von außen geändert“ zeigen
        if (n > 0 && Programs.ContainsKey(CurrentView) && ProgramActive(CurrentView) is { Dirty: false, Path: not null } shown && ActivePageRoot is { } root) // nur die Anzeige des sichtbaren Dokuments, nicht neu zeichnen – beim Tippen geht nichts verloren
            PaintSaveState(root, CurrentView, shown);
        return n;
    }

    /// <summary>E3: entprellt – nach einer kurzen Pause ohne weitere Änderung speichern.</summary>
    void ProgramSaveSoon()
    {
        if (!AutoSaveDocs) return;
        if (_progSaveSoon is null) { _progSaveSoon = new DispatcherTimer { Interval = ProgramSaveDelay }; _progSaveSoon.Tick += (_, _) => ProgramAutosaveNow(); }
        _progSaveSoon.Stop(); _progSaveSoon.Start();
    }

    /// <summary>Vor dem Schließen: speichern (automatisch) oder wenigstens sichern.</summary>
    void ProgramKeep(string key, ProgramDocument d)
    {
        if (!d.Dirty || ProgramAutoSave(key, d)) return;
        DocBackup(d);
    }

    public void ProgramClose(string key, int index)
    {
        var l = ProgramDocs(key); if (index < 0 || index >= l.Count) return;
        var gone = l[index];
        if (gone.Path is { } gp && !gone.ReadOnly) ProgLocks.Release(gp); // T-0680: Datei für andere Fenster freigeben
        ProgLocks.Release(gone.BackupFile(ProgBackup)); _progLocked.Remove(gone.Id); _progRecover.Remove(gone.Id); _progConflict.Remove(gone.Id);
        l.RemoveAt(index); _progActive[key] = Math.Max(0, Math.Min(_progActive.GetValueOrDefault(key), l.Count - 1));
    }

    public void ProgramAutosaveAll()
    {
        foreach (var (key, l) in _progDocs) foreach (var d in l.ToList()) try { if (!ProgramAutoSave(key, d)) DocBackup(d); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        LinksAutosave(); // T-0708: angeschlossene Programme außerhalb der Hülle (Briefe …); T-0714: auch Text, Tabelle, Präsentation, CAD, 3D – eine Notsicherung
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
        var docs = ProgramDocs(key); var d = ProgramActive(key);
        var msg = _progMsg.GetValueOrDefault(key) is { Length: > 0 } m0 ? m0 : null;
        var isErr = msg is not null && _progErr.TryGetValue(key, out var pe) && pe.Error.Sentence == msg;
        var errCard = isErr && _progErr[key].Error.Fix != ErrorFix.None ? ProgramErrorCard(key, _progErr[key].Error, _progErr[key].Retry) : null; // E7: Fehler mit „Was tun?“
        // Karten über der Arbeit – nur wenn es etwas zu entscheiden gibt (sonst beginnt die Arbeitsfläche direkt unter den Werkzeugen)
        var cards = new List<Control>();
        if (ClaudeProgramExtra(key) is { } cx) cards.Add(cx); // z. B. das spielbare Spiel, das Claude gebaut hat
        if (ClaudeChangeBanner(key, d) is { } cb) cards.Add(cb); // Claude hat geändert: vergleichen, behalten, verwerfen
        cards.AddRange(ProgramSafetyCards(key, d)); // T-0680: Sicherung gefunden, anderes Fenster, von außen geändert, beschädigt
        if (ProgramIntro(key) is { } pi) cards.Add(pi); // E8: kurze Einführung beim ersten Öffnen
        Panel root;
        List<MenuTop> menus;
        if (d is null)
        {
            // Startseite: Name und Erklärsatz stehen im Seitenkopf (und hinter „?“)
            if (errCard is not null) cards.Add(errCard); else if (msg is not null) cards.Add(ResultCard(msg));
            menus = ProgramStartMenus(spec);
            root = new StackPanel { Name = "prog-root", Spacing = 10, Children = { ProgramStartBand(spec, OsStyle ? null : MenuBarOf(menus)) } };
            root.Children.AddRange(cards); root.Children.Add(ProgramStart(spec));
        }
        else
        {
            // T-0686 (V-01): in jedem Programm derselbe schmale Kopf – eine Zeile mit Programmname, Dokumenttitel und Speicherstand, darunter sofort die Werkzeuge.
            // Vorher hatten nur die Programme in voller Fensterhöhe diesen Kopf; alle anderen bekamen zusätzlich Seitenkopf-Karte, Hinweis-Karte mit „Kopieren“ und eine eigene Reiterzeile.
            var parts = ProgramWorkParts(spec, d, docs);
            menus = parts.Menus;
            // lange Ergebnisse und Fehler bleiben eine gut lesbare Karte; kurze Hinweise („Vorlage … geöffnet.“) stehen in der Kopfzeile
            var asCard = msg is not null && errCard is null && !spec.FillHeight && (isErr || msg.Length > 140 || msg.Contains('\n'));
            // T-0710: die Menüleiste steht in der Kopfzeile (keine zusätzliche Zeile – die Arbeitsfläche bleibt im oberen Drittel); im Windows-Stil steht sie im Kopf des Stils
            var head = ProgramHead(spec, d, docs, parts.Tabs, errCard is null && !asCard ? msg : null, isErr, OsStyle ? null : MenuBarOf(menus));
            if (errCard is not null) cards.Add(errCard); else if (asCard) cards.Add(ResultCard(msg!));
            if (spec.FillHeight)
            {
                // Kopf (Zeile, Karten, Menüband) fest, darunter Arbeitsfläche + Eigenschaften in voller Höhe, Statusleiste unten
                var top = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 0, 8), Children = { head } };
                top.Children.AddRange(cards); top.Children.Add(parts.Top);
                parts.Status.Margin = new Thickness(0, 8, 0, 0);
                // DockPanel: Kopf und Statusleiste bekommen die echte Breite (Menüband bricht richtig um), die Arbeitsfläche den Rest
                DockPanel.SetDock(top, Avalonia.Controls.Dock.Top); DockPanel.SetDock(parts.Status, Avalonia.Controls.Dock.Bottom);
                root = new DockPanel { Name = "prog-root", LastChildFill = true, Children = { top, parts.Status, parts.Grid } };
            }
            else
            {
                root = new StackPanel { Name = "prog-root", Spacing = 8, Margin = new Thickness(0, 0, 10, 0), Children = { head } }; // rechts Platz für den Rollbalken der Seite – nichts liegt mehr darunter
                root.Children.AddRange(cards);
                root.Children.Add(parts.Top); root.Children.Add(parts.Grid); root.Children.Add(parts.Status);
            }
        }
        root.KeyDown += (_, e) =>
        {
            if (e.KeyModifiers == KeyModifiers.Control)
                switch (e.Key)
                {
                    case Key.S when d is not null: ProgramRun(key, () => ProgramSave(key)); e.Handled = true; break;
                    case Key.Z when d is not null: ProgramRun(key, () => { ModelsUndoGuard(key); d.Undo(); }); e.Handled = true; break;
                    case Key.Y when d is not null: ProgramRun(key, () => { ModelsUndoGuard(key); d.Redo(); }); e.Handled = true; break;
                    case Key.N: ProgramRun(key, () => ProgramNew(key)); e.Handled = true; break;
                    case Key.W when d is not null: ProgramRun(key, () => { ProgramKeep(key, d); ProgramClose(key, _progActive.GetValueOrDefault(key)); }); e.Handled = true; break;
                }
            // T-0710: weitere Kürzel der Menüleiste (Strg+O, Strg+Umschalt+S …) – derselbe Eintrag wie im Menü
            if (!e.Handled && MenuShortcut(menus, e)) e.Handled = true;
        };
        ProgramOneGold(root); // V-03: je Ansicht genau ein goldener Knopf
        if (d is { Dirty: true }) ProgramSaveSoon(); // E3: kurz nach der letzten Änderung von selbst speichern
        return d is null ? Page(spec.Title, root) : spec.FillHeight ? FitPage(spec.Title, new Panel(), root, pageHead: false) : Page(spec.Title, root, pageHead: false);
    }

    /// <summary>Startseite (kein Dokument offen): nur die Stufenwahl; Programme in voller Fensterhöhe nennen hier ihren eigenen Namen (der Seitenkopf nennt das Studio).</summary>
    Control ProgramStartBand(ProgramSpec spec, Control? menuBar = null)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
        if (menuBar is not null) grid.Children.Add(menuBar); // T-0710: Menüleiste auch auf der Startseite (Neu, Öffnen, Zuletzt, Bedienstufe, Hilfe)
        if (spec.FillHeight)
            grid.Children.Add(Pos(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = {
                new TextBlock { Text = spec.Title, Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = spec.Sentence, Foreground = Muted, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis } } }, 1));
        grid.Children.Add(Pos(LevelArea(spec.Key), 2));
        return new Border { Name = "prog-band", BorderBrush = Edge, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(2, 4, 2, 8), Child = grid };
    }

    /// <summary>T-0686 (V-01, V-08): die eine Kopfzeile eines offenen Dokuments – Programmname · Dokumenttitel (änderbar) · Reiter · kurzer Hinweis · Speicherstand · „Mehr Werkzeuge“ bzw. Stufenwahl · „?“.</summary>
    Control ProgramHead(ProgramSpec spec, ProgramDocument d, List<ProgramDocument> docs, Panel? tabs, string? msg, bool msgIsError, Control? menuBar = null)
    {
        var key = spec.Key;
        var name = new TextBlock { Name = "prog-name", Text = spec.Title, Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 4, 0) };
        var title = new TextBox { Name = "prog-title", Text = d.Title, FontSize = 13, FontWeight = FontWeight.SemiBold, MinWidth = 170, MaxWidth = 340, MinHeight = 0, Padding = new Thickness(8, 4), VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent, Foreground = Text, BorderBrush = Edge, BorderThickness = new Thickness(1) };
        ToolTip.SetTip(title, "Name des Dokuments – zum Ändern hineinklicken");
        title.LostFocus += (_, _) => { if (!string.IsNullOrWhiteSpace(title.Text) && title.Text.Trim() != d.Title) { d.Title = title.Text.Trim(); d.EditMeta(_ => { }); Show(key); } };

        // V-08: der Speicherstand steht nur hier
        var state = new TextBlock { Name = "prog-dirty", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        var saveNow = new Button { Name = "prog-save-now", Content = "Speichern", FontSize = 12, Padding = new Thickness(9, 2), VerticalAlignment = VerticalAlignment.Center, Background = Brushes.Transparent, Foreground = Text, BorderBrush = Accent, BorderThickness = new Thickness(1) };
        ToolTip.SetTip(saveNow, "Jetzt speichern (Strg+S)");
        saveNow.Click += (_, _) => ProgramRun(key, () => ProgramSave(key));
        var save = new StackPanel { Name = "prog-save-state", Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children = { state, saveNow } };
        PaintSaveState(save, key, d);

        var help = new Button { Name = "btn-help", Content = "?", Width = 26, Height = 26, Padding = new Thickness(0), CornerRadius = new CornerRadius(13), HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.Bold, Background = Brush.Parse("#22FFFFFF"), Foreground = Text, VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(help, "Was kann ich hier tun?");
        help.Click += (_, _) => { if (_hintHost.Children.Count > 0) { _hintHost.Children.Clear(); return; } ShowHelp(key, force: true); };

        // T-0710: Spalte 0 = Menüleiste (Datei · Bearbeiten · …), dann Name, Titel, Reiter, Hinweis, Speicherstand, Stufe/Hilfe – eine Zeile
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto,*,Auto,Auto"), ColumnSpacing = 10, RowDefinitions = new RowDefinitions("Auto") };
        if (menuBar is not null) grid.Children.Add(menuBar);
        grid.Children.Add(Pos(name, 1)); grid.Children.Add(Pos(title, 2));
        if (tabs is not null)
        {
            // der Titel steht schon im Feld daneben: bei einem einzigen Dokument bleibt nur „×“ und „＋“ sichtbar, ab zwei Dokumenten die Reiter zum Wechseln
            if (docs.Count == 1 && tabs.Children.Count > 0) tabs.Children[0].IsVisible = false;
            // „Einfach“ mit einem Dokument: auch „＋“ bleibt weg („Neu“ steht unter „Mehr“, Strg+N geht weiter) – ein Knopf weniger in der Kopfzeile
            if (docs.Count == 1 && IsSimple && tabs.Children.Count > 0) tabs.Children[^1].IsVisible = false;
            tabs.VerticalAlignment = VerticalAlignment.Center;
            if (docs.Count > 3) { grid.RowDefinitions = new RowDefinitions("Auto,Auto"); Grid.SetRow(tabs, 1); Grid.SetColumnSpan(tabs, 7); tabs.Margin = new Thickness(0, 6, 0, 0); grid.Children.Add(tabs); }
            else grid.Children.Add(Pos(tabs, 3));
        }
        // „Neues Dokument.“ / „Vorlage … geöffnet.“ sagt nur, was gerade geschah: nach ein paar Sekunden verschwindet es wieder (die Meldung selbst bleibt abrufbar)
        var passing = msg is not null && !msgIsError && (msg == "Neues Dokument." || msg.StartsWith("Vorlage „", StringComparison.Ordinal));
        if (passing && _progNoteGone.GetValueOrDefault(key) == d.Id + msg) msg = null;
        if (msg is { Length: > 0 })
        {
            var note = new SelectableTextBlock { Name = "prog-msg", Text = msg, Foreground = msgIsError ? Text : Muted, FontSize = 12.5, FontWeight = msgIsError ? FontWeight.SemiBold : FontWeight.Normal, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(note, msg);
            grid.Children.Add(Pos(note, 4));
            if (passing && _progNoteTimed.Add(key + d.Id + msg)) // ein Zeitgeber je Hinweis, auch wenn die Seite inzwischen neu gezeichnet wird
            {
                var gone = d.Id + msg; var text = msg;
                Later(() =>
                {
                    _progNoteGone[key] = gone; _progNoteTimed.Remove(key + gone);
                    foreach (var t in ActivePageRoot?.GetLogicalDescendants().OfType<SelectableTextBlock>().Where(t => t.Name == "prog-msg" && t.Text == text).ToList() ?? []) t.IsVisible = false;
                }, PassingNoteTime);
            }
        }
        grid.Children.Add(Pos(save, 5));
        grid.Children.Add(Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Children = { LevelArea(key), help } }, 6));
        if (menuBar is Menu mb) MenuBarFitIn(grid, mb);
        return new Border { Name = "prog-band", BorderBrush = Edge, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(2, 0, 2, 6), Child = grid };
    }

    /// <summary>T-0710: Menüleiste und Kopfzeile bleiben eine Zeile (die Arbeitsfläche bleibt im oberen Drittel). Reicht die Breite nicht (schmales Fenster, viele Fachmenüs),
    /// stehen die hinteren Menüs unter „…“ – erreichbar mit Maus, Pfeiltasten und Alt+Buchstabe; nichts wird abgeschnitten.</summary>
    void MenuBarFitIn(Grid grid, Menu menuBar)
    {
        void Fit()
        {
            if (grid.Bounds.Width <= 0) return;
            var others = grid.Children.Where(c => !ReferenceEquals(c, menuBar) && Grid.GetRow(c) == 0 && c.IsVisible && Grid.GetColumn(c) != 4).ToList();
            MenuBarFit(menuBar, grid.Bounds.Width - others.Sum(c => c.DesiredSize.Width) - grid.ColumnSpacing * others.Count - 4);
        }
        grid.SizeChanged += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(Fit, Avalonia.Threading.DispatcherPriority.Background);
    }

    /// <summary>V-01: so lange steht ein flüchtiger Hinweis („Vorlage … geöffnet.“) in der Kopfzeile.</summary>
    public static readonly TimeSpan PassingNoteTime = TimeSpan.FromSeconds(4);
    /// <summary>Flüchtiger Hinweis je Programm, der schon wieder ausgeblendet ist (Dokument + Text) – er kommt beim Neuzeichnen nicht zurück.</summary>
    private readonly Dictionary<string, string> _progNoteGone = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _progNoteTimed = [];

    /// <summary>V-08: muss die Person selbst speichern? Ja, wenn das automatische Speichern aus ist (oder gerade nicht ging) und es etwas zu speichern gibt.</summary>
    bool SaveByHand(ProgramDocument d, string? key)
    {
        if (d.ReadOnly && d.Path is not null || _progConflict.Contains(d.Id)) return false; // dort entscheidet die Karte darüber
        if (!AutoSaveDocs) return d.Dirty || d.Path is null;
        return d.Dirty && key is not null && _progErr.TryGetValue(key, out var e) && e.Error.Kind == "speichern";
    }

    /// <summary>V-08 (T-0686): Speicherstand in einem Satz – „Wird automatisch gespeichert“, „Gespeichert um HH:MM“ oder „Nicht gespeichert –“ (dann steht der Knopf „Speichern“ daneben).
    /// Nie „nicht gespeichert“, solange das automatische Speichern läuft.</summary>
    public string SaveStateText(ProgramDocument d, string? key = null) =>
        d.ReadOnly && d.Path is not null ? "Nur lesen"
        : _progConflict.Contains(d.Id) ? "Nicht gespeichert – bitte oben entscheiden"
        : SaveByHand(d, key) ? "Nicht gespeichert –"
        : d.Dirty || d.Path is null ? "Wird automatisch gespeichert"
        : $"Gespeichert um {d.DiskTime ?? d.Changed:HH:mm}";

    /// <summary>Speicherstand in der Kopfzeile und Dateiname in der Statusleiste nachziehen, ohne die Seite neu zu zeichnen (beim Tippen geht nichts verloren).</summary>
    void PaintSaveState(Control root, string key, ProgramDocument d)
    {
        foreach (var c in new[] { root }.Concat(root.GetLogicalDescendants().OfType<Control>()))
            switch (c)
            {
                case TextBlock { Name: "prog-dirty" } tb: tb.Text = SaveStateText(d, key); break;
                case Button { Name: "prog-save-now" } b: b.IsVisible = SaveByHand(d, key); break;
                case TextBlock { Name: "prog-saved" } f: f.Text = d.Path is null ? "" : Path.GetFileName(d.Path); ToolTip.SetTip(f, d.Path); break;
            }
    }

    /// <summary>V-04: Breite der Werkzeugleiste in „Einfach“ (Symbol und Wort).</summary>
    public const double ToolRailWidth = 164;

    // ---------- T-0686 (V-03): genau ein goldener Knopf je Ansicht ----------
    /// <summary>Golden gefüllt = der nächste sinnvolle Schritt. Gilt für Knöpfe mit der Akzentfläche oder der Klasse „accent“.</summary>
    public static bool IsGold(Button b) => b.Classes.Contains("accent")
        || b.Background is ISolidColorBrush { Opacity: > 0.98 } s && Accent is ISolidColorBrush a && s.Color == a.Color;

    /// <summary>Ruhige Hervorhebung für „gewählt“ (Reiter, Werkzeug, Stufe) und für Hauptaktionen, die nicht die eine goldene sind: goldener Rand, zarter Goldton, normale Schrift.</summary>
    internal static void QuietGold(Button b)
    {
        b.Classes.Remove("accent");
        var c = Accent is ISolidColorBrush a ? a.Color : Color.Parse("#C9A227");
        b.Background = new SolidColorBrush(c, 0.16); b.BorderBrush = Accent; b.BorderThickness = new Thickness(1); b.Foreground = Text;
    }

    /// <summary>Regel der Hülle: Von allen golden gefüllten Knöpfen der Ansicht bleibt genau einer – der erste Schritt im leeren Dokument, sonst eine Rückfrage-Karte
    /// (Sicherung, Einführung, Fehler), sonst die Hauptaktion im Menüband (Befehl mit <c>Primary</c> oder <see cref="ProgramSpec.PrimaryExport"/>), sonst eine Hauptaktion der Arbeitsfläche mit der Klasse „haupt“. Alle anderen (auch die in der Arbeitsfläche) werden ruhig hervorgehoben; keine Funktion ändert sich.</summary>
    void ProgramOneGold(Control root)
    {
        var gold = root.GetLogicalDescendants().OfType<Button>().Where(IsGold).ToList();
        static bool Inside(Button b) => b.GetLogicalAncestors().OfType<Control>().Any(c => c.Name is "prog-workspace" or "prog-properties" or "prog-bottom" or "prog-tools-panel" or "prog-level");
        // T-0700: ohne Hauptbefehl im Menüband bleibt die Hauptaktion der Arbeitsfläche golden, wenn das Programm sie mit der Klasse „haupt“ kennzeichnet (z. B. „Spielen“)
        var keep = gold.FirstOrDefault(b => b.Name == "prog-first-step") ?? gold.FirstOrDefault(b => !Inside(b)) ?? gold.FirstOrDefault(b => b.Classes.Contains("haupt") && b.IsVisible);
        foreach (var b in gold) if (!ReferenceEquals(b, keep)) QuietGold(b);
        // die Akzentklasse (Fluent-Akzent) wird zur goldenen Fläche – keine Klasse „accent“ mehr in der Hülle
        if (keep is not null && keep.Classes.Contains("accent")) { keep.Classes.Remove("accent"); keep.Background = Accent; keep.Foreground = Brushes.White; }
    }

    /// <summary>V-02: Eigenschaften-Spalte zeigen, auch wenn sie in „Einfach“ eingeklappt oder von Hand ausgeblendet ist (für erste Schritte wie „Namen eingeben“).</summary>
    public void ProgramShowProperties(string key)
    {
        _simpleToolsOpen.Add(key);
        if (_settings.HiddenPanels.Remove(key + ":eigenschaften")) SaveSettings();
    }

    /// <summary>T-0702: Dokumente, deren erster Schritt „in ein Feld schreiben“ schon gewählt wurde – die Karte verschwindet dann, auch bevor etwas getippt ist (gilt bis zum Neustart).</summary>
    private readonly HashSet<string> _progFirstDone = [];
    public bool ProgramFirstDone(ProgramDocument d) => _progFirstDone.Contains(d.Id);

    /// <summary>T-0702: erster Schritt „Feld ausfüllen“ – Karte ausblenden und nach dem Neuzeichnen das Eingabefeld <paramref name="field"/> in den Fokus holen (Text markiert).</summary>
    public void ProgramFirstFocus(ProgramDocument d, string field)
    {
        _progFirstDone.Add(d.Id);
        Dispatcher.UIThread.Post(() =>
        {
            if (ActivePageRoot?.GetLogicalDescendants().OfType<TextBox>().FirstOrDefault(t => t.Name == field) is { } box) { box.Focus(); box.SelectAll(); }
        }, DispatcherPriority.Background);
    }

    /// <summary>V-02: Dateiauswahl für einen ersten Schritt („Video hinzufügen“, „Foto öffnen“) – danach läuft <paramref name="use"/> als normaler Befehl (Fehler werden zur Meldung).
    /// Tests ersetzen die Auswahl über <see cref="PickOverride"/>.</summary>
    public async Task ProgramPickThen(string key, string field, string title, IReadOnlyList<string> extensions, bool multiple, Action<string[]> use)
    {
        string[]? got;
        try
        {
            if (PickOverride is { } fake) got = await fake(new PickRequest(field, false, multiple, extensions));
            else
            {
                var filters = new List<FilePickerFileType>();
                if (extensions.Count > 0) filters.Add(new FilePickerFileType(title) { Patterns = extensions.Select(e => "*." + e.TrimStart('.')).ToList() });
                filters.Add(new FilePickerFileType("Alle Dateien") { Patterns = ["*"] });
                got = (await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = multiple, FileTypeFilter = filters })).Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException) { ProgramRun(key, () => throw new IOException(FriendlyError.Say(e), e)); return; }
        if (got is { Length: > 0 }) ProgramRun(key, () => use(got));
    }

    /// <summary>V-02: so weit sitzt die Karte des ersten Schritts über der genauen Mitte (halber Wert) – unten liegen oft Zeitleiste und Abspielknöpfe.</summary>
    public const double FirstStepLift = 90;

    /// <summary>V-02: großer Knopf für den ersten Schritt, mittig über der leeren Arbeitsfläche. Nur die Karte nimmt Klicks an – daneben bleibt die Fläche bedienbar.</summary>
    Control ProgramFirstStepCard(ProgramSpec spec, ProgramDocument d, ProgramFirstStep step)
    {
        var key = spec.Key;
        var go = new Button { Name = "prog-first-step", Content = step.Label, FontSize = 16, FontWeight = FontWeight.SemiBold, Padding = new Thickness(26, 12), CornerRadius = new CornerRadius(10), HorizontalAlignment = HorizontalAlignment.Center,
            Background = Accent, Foreground = Brushes.White, BorderThickness = new Thickness(0) };
        go.Click += (_, _) => ProgramRun(key, () => step.Run(d));
        return new Border { Name = "prog-first", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, FirstStepLift), MaxWidth = 460, Padding = new Thickness(26, 20), CornerRadius = new CornerRadius(14),
            Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow,
            Child = new StackPanel { Spacing = 12, Children = {
                new TextBlock { Name = "prog-first-hint", Text = step.Hint, Foreground = Text, FontSize = 14, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center },
                go } } };
    }

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
                     : "Speichern mit Strg+S – ungespeicherte Änderungen sichert Trendo zusätzlich alle 20 Sekunden.",
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
            b.Click += (_, _) => ProgramNewAct(key, tt.Id)();
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
            b.Click += (_, _) => ProgramOpenAct(key, path)();
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
        // T-0680: geprüfte Sicherungen – unlesbare werden gemeldet statt geladen; offene Dokumente dieses und anderer Fenster sind keine „abgestürzten“
        var (good, broken) = ProgramDocument.OrphanScan(ProgBackup, spec.Studio);
        var openHere = _progDocs.Values.SelectMany(x => x).Select(x => Path.GetFullPath(x.BackupFile(ProgBackup))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        good = good.Where(o => !openHere.Contains(Path.GetFullPath(o.File)) && ProgLocks.HeldByOther(o.File) is null).ToList();
        if (good.Count > 0 || broken.Count > 0)
        {
            var rs = new StackPanel { Name = "prog-restore", Spacing = 4, Children = { H("Wiederherstellen", 15) } };
            if (good.Count > 0) rs.Children.Add(P("Diese Dokumente wurden nicht gespeichert, aber automatisch gesichert. Du kannst sie zurückholen oder verwerfen.", null, 12));
            var k = 0;
            foreach (var o in good.Take(5))
            {
                var file = o.File;
                rs.Children.Add(Row(P($"{o.Title} · gesichert {Clock(o.When)}", Text, 12.5), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                    // die Sicherung bleibt liegen, bis das Dokument wirklich gespeichert ist (dann räumt DropBackup sie weg)
                    Act($"prog-restore-{k}", "Wiederherstellen", () => ProgramRun(key, () => { var d = ProgramDocument.Restore(file); ProgLocks.Acquire(file); var l = ProgramDocs(key); l.Add(d); _progActive[key] = l.Count - 1; _progMsg[key] = AutoSaveDocs ? "Wiederhergestellt." : "Wiederhergestellt – bitte speichern."; }), true),
                    Act($"prog-discard-{k}", "Verwerfen", () => ProgramRun(key, () => File.Delete(file))) } }));
                k++;
            }
            if (broken.Count > 0)
            {
                var note = P(broken.Count == 1 ? "Eine Sicherung ist beschädigt und lässt sich nicht zurückholen." : $"{broken.Count} Sicherungen sind beschädigt und lassen sich nicht zurückholen.", Text, 12.5);
                note.Name = "prog-restore-broken";
                rs.Children.Add(Row(note, Act("prog-restore-broken-drop", "Entfernen", () => ProgramRun(key, () => { foreach (var f in broken) File.Delete(f); }))));
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

    /// <summary>V2/V9: Reiter, Menüband, Arbeitsfläche, Eigenschaften, Statusleiste. <c>Tabs</c> ist null, wenn der Bedienstil (Windows, Mac, GNOME) die Reiter selbst in seinen Kopf setzt.</summary>
    private (Panel? Tabs, Control Top, Control Grid, Control Status, List<MenuTop> Menus) ProgramWorkParts(ProgramSpec spec, ProgramDocument d, List<ProgramDocument> docs)
    {
        var key = spec.Key;
        // T-0710: die Handler der Datei- und Bearbeiten-Befehle – Knöpfe, Menüleiste und Tastenkürzel rufen genau diese (keine zweite Befehlslogik)
        Action doNew = () => ProgramRun(key, () => ProgramNew(key));
        Action doOpen = () => _ = ProgramOpenPick(key);
        Action doSave = () => ProgramRun(key, () => ProgramSave(key));
        Action doSaveAs = () => _ = ProgramSaveAsPick(key);
        Action doClose = () => ProgramRun(key, () => { ProgramKeep(key, d); ProgramClose(key, _progActive.GetValueOrDefault(key)); });
        Action doUndo = () => ProgramRun(key, () => { ModelsUndoGuard(key); if (!d.Undo()) throw new InvalidOperationException("Es gibt nichts rückgängig zu machen."); });
        Action doRedo = () => ProgramRun(key, () => { ModelsUndoGuard(key); if (!d.Redo()) throw new InvalidOperationException("Es gibt nichts zu wiederholen."); });
        Action CmdRun(ProgramCmd c) => () => ProgramRun(key, () => c.Run(d));
        Action ExportRun(ProgramExport x) => () => ProgramRun(key, () =>
        {
            var file = Path.Combine(WorkFolder(spec.Folder), DevStudio.SafeName(d.Title) + x.Extension);
            var res = x.Write(d, file);
            _core.Audit.Append(Principal.LocalOwner.Name, "program.export", $"{spec.Studio} {Path.GetFileName(res)}");
            _progMsg[key] = "Exportiert: " + res; NotifyFileResult(_progMsg[key]);
        });
        Action VersionRun((string File, DateTime When) v) => () => ProgramRun(key, () => { ModelsUndoGuard(key); d.RestoreVersion(v.File); _progMsg[key] = $"Stand vom {v.When:dd.MM.yyyy HH:mm} zurückgeholt – Strg+Z macht es rückgängig."; });
        var goldGiven = false; // V-03: nur der erste sichtbare Hauptbefehl des Programms ist golden
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
            var pick = RBtn("prog-saveas-pick", "Ort wählen …", () => { open.Flyout?.Hide(); doSaveAs(); }, true);
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
                var run = VersionRun(v); b.Click += (_, _) => { open.Flyout?.Hide(); run(); };
                list.Children.Add(b);
            }
            open.Flyout = new Flyout { Content = new ScrollViewer { MaxHeight = 420, Content = list } };
            return [open];
        }
        var groups = new List<(string, List<Control>)>
        {
            ("Datei", [
                RBtn("prog-new", "Neu", doNew),
                RBtn("prog-save", "Speichern", doSave, tip: AutoSaveDocs ? "Jetzt speichern (Strg+S) – Änderungen werden sonst von selbst gespeichert" : "Speichern (Strg+S)"), // V-03: nicht mehr golden – gespeichert wird von selbst
                .. SaveAsControls(),
                .. VersionControls(),
                RBtn("prog-open-pick", "Öffnen …", doOpen),
                RBtn("prog-close", "Schließen", doClose),
            ]),
            ("Bearbeiten", [
                RBtn("prog-undo", "Rückgängig", doUndo, tip: d.UndoLabel is { } u ? "Rückgängig: " + u : "Nichts rückgängig zu machen"),
                RBtn("prog-redo", "Wiederholen", doRedo),
            ]),
        };
        var level = ProgramLevel;
        var blocker = spec.Blocker?.Invoke(d); // V-09: fehlt etwas, ist „Jetzt einrichten“ der nächste Schritt (golden), die Hauptaktion bleibt bedienbar, aber ruhig
        if (blocker is not null) goldGiven = true;
        foreach (var (g, cmds) in spec.Ribbon)
        {
            if (!UserLevel.Shows(level, spec.GroupLevels.GetValueOrDefault(g))) continue;
            var shown = new List<Control>();
            foreach (var c in cmds.Where(c => UserLevel.Shows(level, spec.LevelOf(c))))
            {
                var cb = RBtn("prog-cmd-" + c.Name, c.Label, CmdRun(c), c.Primary && !goldGiven && (goldGiven = true), c.Tip);
                if (c.Name == blocker?.Replaces) cb.Tag = SimpleIntoMore; // tut dasselbe wie „Jetzt einrichten“ – in „Einfach“ unter „Mehr“ statt doppelt im Menüband
                shown.Add(cb);
                if (c.Primary && blocker is not null && !shown.Any(x => x.Name == "prog-blocker")) shown.Add(BlockerChip("prog-blocker", blocker));
            }
            if (shown.Count > 0) groups.Add((g, shown));
        }
        if (spec.Exports.Count > 0)
            groups.Add(("Exportieren", spec.Exports.Where((x, i) => level != UserLevel.Einfach || i < spec.SimpleExports || x.Name == spec.PrimaryExport).Select(x => (Control)RBtn("prog-export-" + x.Name, x.Label, ExportRun(x), x.Name == spec.PrimaryExport && !goldGiven && (goldGiven = true))).ToList()));
        var pro = level == UserLevel.Profi ? ProTools(spec, d) : [];
        if (pro.Count > 0) groups.Add(("Profi", pro));
        var help = new List<Control>();
        if (spec.Help.Length > 0) help.Add(RBtn("prog-help", "? Hilfe", () => Toast(spec.Help)));
        help.Add(RBtn("prog-intro-show", "Einführung", () => ProgramIntroShow(key), tip: "Die drei kurzen Schritte zum Anfangen noch einmal zeigen"));
        // T-0686 (V-01): ohne die Kopfkarte der Seite bleiben „Team“ und die freien KIs des Studios hier erreichbar (in „Einfach“ unter „Mehr“)
        if (!spec.FillHeight && StudioTeams.For(spec.Studio) is { } team)
            help.Add(RBtn("page-team", "Team", () => AgentsForStudio(spec.Studio), tip: $"{team.Name}: {string.Join(", ", team.Roles.Select(r => r.Name))} – unter „KI einsetzen“ starten"));
        if (!spec.FillHeight && FreeKiRow(spec.Studio) is Panel free)
            foreach (var fb in free.Children.OfType<Button>().ToList()) { free.Children.Remove(fb); fb.Padding = new Thickness(12, 6); fb.Background = Brushes.Transparent; fb.BorderBrush = Edge; fb.BorderThickness = new Thickness(1); ToolTip.SetTip(fb, "Freie KI: " + ToolTip.GetTip(fb)); help.Add(fb); }
        groups.Add(("Hilfe", help));
        // T-0710: Menüleiste aus denselben Handlern (vor „Einfach“: dort wandern Knöpfe unter „Mehr“, die Leiste behält alles)
        var menus = ShellMenus(spec, d, new ShellActs(doNew, doOpen, doSave, doSaveAs, doClose, doUndo, doRedo, CmdRun, ExportRun, VersionRun), help, pro);
        if (level == UserLevel.Einfach && !OsStyle)
        {
            // E2: die Bedienstufe bleibt auch im „Mehr“-Menü wählbar
            groups.Add(("Bedienstufe", [RBtn("prog-stufe-erweitert", "Mehr Werkzeuge (Erweitert)", () => ProgramSetLevel(UserLevel.Erweitert, key)), RBtn("prog-stufe-profi", "Alle Werkzeuge (Professionell)", () => ProgramSetLevel(UserLevel.Profi, key))]));
            groups = SimpleRibbon(groups, spec); // Wunsch 08.11.2026: Wichtigstes sichtbar, alles andere hinter „Mehr“
        }
        Control? osChrome = OsStyle ? OsProgramChrome(spec, groups, tabs, docs.Count, MenuBarOf(menus)) : null; // Windows-Menüzeile, Mac-Menüleiste, GNOME-Kopfleiste
        var ribbon = new WrapPanel { Name = osChrome is null ? "prog-ribbon" : "prog-ribbon-trendo" };
        string? lastGroup = null;
        foreach (var (g, items) in osChrome is null ? groups : [])
        {
            if (DawLook)
            {
                // DAW-Stil (Wunsch 06.11.2026): Gruppe als Segment wie in Bitwig („FILE · PLAY · EDIT“) – Beschriftung links, Knöpfe dicht nebeneinander
                var seg = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; // bricht bei vielen Befehlen innerhalb der Gruppe um
                if (g != lastGroup) seg.Children.Add(new TextBlock { Name = "prog-group-label", Text = g.ToUpperInvariant(), Foreground = Muted, FontSize = 9.5, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 6, 0) });
                foreach (var it in items) { it.Margin = new Thickness(0, 2, 6, 2); it.VerticalAlignment = VerticalAlignment.Center; seg.Children.Add(it); } // V-04: sichtbarer Abstand zwischen den Knöpfen
                var box = new Border { Margin = new Thickness(0, 4, 6, 4), Padding = new Thickness(6, 3), CornerRadius = new CornerRadius(8), Background = GroupBg, BorderBrush = PaneEdge, BorderThickness = new Thickness(1), Child = seg };
                ToolTip.SetTip(box, g); lastGroup = g;
                ribbon.Children.Add(box);
                continue;
            }
            var row = new WrapPanel(); foreach (var it in items) { it.Margin = new Thickness(0, 0, 6, 4); it.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(it); }
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
        // V-02 (T-0686): leeres Dokument – großer Knopf für den ersten Schritt mitten auf der Arbeitsfläche
        if (spec.FirstStep is { } step && !d.ReadOnly && step.Empty(d)) { var body = work.Child!; work.Child = null; work.Child = new Panel { Children = { body, ProgramFirstStepCard(spec, d, step) } }; }
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
        var wordy = IsSimple; // V-04: Werkzeuge mit Symbol und Wort
        var dawOrder = DawLook && !wordy; // DAW-Stil: Inspektor links (wie Bitwig, Studio One), Werkzeuge als Leiste rechts – in „Einfach“ stehen die Werkzeuge immer links
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(dawOrder ? (showProps ? "300,12," : "") + "*" + (showTools ? ",10,Auto" : "") : (showTools ? "Auto,10," : "") + (showProps ? "*,12,300" : "*")) };
        var col = dawOrder && showProps ? 2 : 0;
        if (showTools)
        {
            // viele Werkzeuge: zwei Spalten wie in Photoshop/GIMP, damit nichts unten aus dem Bild läuft
            var two = spec.Tools.Count > 9 && !wordy;
            // V-04 (T-0686): in „Einfach“ eine breitere Leiste links mit Symbol und Wort – nichts wird mehr abgeschnitten; in den höheren Stufen nur das Symbol, das Wort steht im Hinweis
            Panel rail = wordy ? new StackPanel { Name = "prog-tools", Spacing = 4, Width = ToolRailWidth } : two ? new WrapPanel { Name = "prog-tools", Width = 88 } : new StackPanel { Name = "prog-tools", Spacing = 4 };
            foreach (var t in spec.Tools)
            {
                var on = t.Active(); var tt = t;
                var b = new Button { Name = "prog-cmd-" + t.Name, BorderBrush = Edge, BorderThickness = new Thickness(1), Background = Brushes.Transparent, Foreground = Text };
                if (wordy)
                {
                    b.HorizontalAlignment = HorizontalAlignment.Stretch; b.HorizontalContentAlignment = HorizontalAlignment.Left; b.Padding = new Thickness(8, 6); b.CornerRadius = new CornerRadius(8);
                    // dasselbe ruhige Linien-Symbol wie in den höheren Stufen (GlyphIcons), sonst das Zeichen des Werkzeugs
                    Control icon = GlyphIcons.Map.TryGetValue(t.Icon.Trim(), out var gl)
                        ? new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(gl.Path), Stretch = Stretch.Uniform, Width = 15, Height = 15, Margin = new Thickness(2.5, 0), VerticalAlignment = VerticalAlignment.Center, [!Avalonia.Controls.Shapes.Shape.FillProperty] = b[!Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty] }
                        : new TextBlock { Text = t.Icon, FontSize = 15, Width = 20, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    b.Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = {
                        icon,
                        new TextBlock { Text = t.Label, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis } } };
                    b.SetValue(GlyphIcons.GlyphProperty, t.Icon); // das Zeichen bleibt für Claude, Befehlsleiste und Tests abrufbar
                }
                else
                {
                    b.Content = t.Icon; b.Width = b.Height = DawLook ? 36 : 40; b.Padding = new Thickness(0); b.FontSize = DawLook ? 15 : 17; b.CornerRadius = new CornerRadius(DawLook ? 6 : 10);
                    b.HorizontalContentAlignment = HorizontalAlignment.Center; b.VerticalContentAlignment = VerticalAlignment.Center; if (two) b.Margin = new Thickness(2);
                }
                if (on) QuietGold(b); // V-03: „gewählt“ ist ein goldener Rand – golden gefüllt ist nur die Hauptaktion
                ToolTip.SetTip(b, $"{t.Label}{(t.Shortcut.Length > 0 ? $" ({t.Shortcut})" : "")} – {t.Tip}"); Avalonia.Automation.AutomationProperties.SetName(b, t.Label);
                b.Click += (_, _) => ProgramRun(key, () => tt.Select(d));
                rail.Children.Add(b);
            }
            var toolsBorder = new Border { Name = "prog-tools-panel", CornerRadius = new CornerRadius(14), Background = PaneBg, BorderBrush = PaneEdge, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Padding = new Thickness(6),
                Child = wordy ? new ScrollViewer { Content = rail, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto } : rail };
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
            b.Click += (_, _) => ProgramTogglePanel(key, part);
            toggles.Children.Add(b);
        }
        if (spec.Tools.Count > 0) Toggle("werkzeuge", "◧", "Werkzeuge", showTools);
        if (spec.Bottom is not null) Toggle("unten", "⬓", spec.Bottom.Title, showBottom);
        if (spec.Properties is not null) Toggle("eigenschaften", "◨", "Eigenschaften", showProps);
        toggles.IsVisible = !SimpleCollapsed(key); // E2: in „Einfach“ erst nach „Mehr Werkzeuge“

        // Statusleiste
        var status = new Border { Name = "prog-status", CornerRadius = new CornerRadius(10), Background = DawLook ? DawLcd : Brush.Parse(C("#331A1A1A", "#14000000")), BorderBrush = DawLook ? DawEdge : null, BorderThickness = new Thickness(DawLook ? 1 : 0), Padding = new Thickness(12, 5),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Children = {
                // T-0686 (Befund S-11): Nebensachen in normaler, ruhiger Schrift – nicht mehr blau in Schreibmaschinenschrift
                new TextBlock { Name = "prog-status-text", Text = spec.Status?.Invoke(d) ?? "", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis },
                // V-08 (T-0686): der Speicherstand steht nur noch in der Kopfzeile – hier nur, unter welchem Namen die Datei liegt (Hinweis: voller Pfad)
                Pos(new TextBlock { Name = "prog-saved", Text = d.Path is null ? "" : Path.GetFileName(d.Path), [ToolTip.TipProperty] = d.Path, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, 1),
                Pos(toggles, 2) } } };
        if (osChrome is not null) return (null, osChrome, grid, status, menus);
        Control ribbonHost = DawLook ? new Border { Name = "prog-ribbon-strip", CornerRadius = new CornerRadius(12), Background = DawPanel, BorderBrush = DawEdge, BorderThickness = new Thickness(1), Padding = new Thickness(8, 2, 8, 0), Child = ribbon } : ribbon; // DAW-Werkzeugleiste
        return (tabs, ribbonHost, grid, status, menus);

        Button RBtn(string name, string label, Action go, bool primary = false, string? tip = null)
        {
            var b = new Button { Name = name, Content = PlainWords.For(ProgramLevel, label), FontSize = DawLook ? 12 : 12.5, Padding = DawLook ? new Thickness(9, 4) : new Thickness(12, 6), CornerRadius = new CornerRadius(DawLook ? 5 : 8), Foreground = primary ? Brushes.White : Text,
                Background = primary ? Accent : DawLook ? DawButton : Brushes.Transparent, BorderBrush = DawLook ? DawEdge : Edge, BorderThickness = new Thickness(primary || DawLook ? 0 : 1) };
            if (tip is not null) ToolTip.SetTip(b, tip);
            b.Click += (_, _) => go(); return b;
        }
    }

    /// <summary>Rand ein- oder ausklappen (gemerkt je Programm) – Schalter in der Statusleiste und Menü „Ansicht“. In „Einfach“ holt „Eigenschaften“ die eingeklappte Spalte.</summary>
    public void ProgramTogglePanel(string key, string part)
    {
        var id = key + ":" + part;
        if (part == "eigenschaften" && SimpleCollapsed(key)) { ProgramShowProperties(key); Show(key); return; }
        if (!_settings.HiddenPanels.Remove(id)) _settings.HiddenPanels.Add(id);
        SaveSettings(); Show(key);
    }

    // ---------- Masterprompt 131: Bedienstufen ----------
    public int ProgramLevel => UserLevel.Of(_settings.Level);

    /// <summary>Bedienstufe wechseln – nur die Werkzeugauswahl ändert sich, Dokumente bleiben unberührt.</summary>
    public void ProgramSetLevel(int level, string? reopen = null)
    {
        _settings.Level = UserLevel.Id(level); SaveSettings();
        SimpleWords.AnySimple = ProgramLevel == UserLevel.Einfach;
        _core.Audit.Append(Principal.LocalOwner.Name, "program.level", _settings.Level);
        RefreshDesktopTiles(); // E12: Desktop folgt der Stufe
        _txAllTabs = false; // V5: „Mehr ▾“ gilt nur bis zum nächsten Stufenwechsel
        if (reopen is not null) Show(reopen);
        SimpleWords.Refresh(this); // V3/V7: Wörter an Knöpfen der Stufe anpassen (auch bei Teilen, die nicht neu gebaut werden)
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
                Background = Brushes.Transparent, Foreground = Text, BorderBrush = Edge, BorderThickness = new Thickness(1) };
            if (on) QuietGold(b); // V-03 (T-0686): gewählte Stufe mit goldenem Rand – golden gefüllt ist nur die Hauptaktion
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
            b.Click += (_, _) => ProgramRun(key, () => { ModelsUndoGuard(key); for (var n = 0; n < back; n++) d.Undo(); _progMsg[key] = $"{back} Schritt(e) zurück – „Wiederholen“ holt sie zurück."; });
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
