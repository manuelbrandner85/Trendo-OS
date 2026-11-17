using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>T-0743 (Audit X-01, Rang 18): Karten eines Großprojekts tun etwas. „An Studio übergeben“ öffnet das gewählte Programm mit einem vorbereiteten Dokument
/// (Titel, Aufgabe, Ergebnis der Karte davor als Eingabe), der Auftrag steht als Übergabe im Projekt. Exportiert die Person dort, geht die Datei von selbst an die Karte zurück:
/// verknüpft, mit Prüfsumme, Status „erledigt“ und Vorschau. Die Kette ist die Reihenfolge der Karten – frei zusammenstellbar; die 4 Abläufe bleiben Vorlagen.
/// Kein zweites Register: Tafel im <see cref="ProjectStore"/>, Auftrag und Ergebnis über <see cref="ProjectHandoff"/>, Dokumente über die Programm-Hülle.</summary>
public partial class MainWindow
{
    /// <summary>Dokumente dieser Sitzung, die zu einer Karte gehören (Dokument → Projekt, Karte). Nach einem Neustart findet <see cref="BigBoundByPath"/> sie über den Dateipfad.</summary>
    readonly Dictionary<string, (string Project, string Card)> _bigBound = [];

    /// <summary>Alle Programme der Programm-Hülle, an die eine Karte gehen kann, nach Namen.</summary>
    public static IReadOnlyList<ProgramSpec> BigPrograms => [.. Programs.Values.OrderBy(p => p.Title, StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>Schritt einer Ablauf-Vorlage (Studioname) → Programm mit genau diesem Namen; sonst null (die Person wählt selbst).</summary>
    public static string? BigProgramFor(string studioLabel) => Programs.Values.FirstOrDefault(p => p.Title.Equals(studioLabel, StringComparison.OrdinalIgnoreCase))?.Key;

    /// <summary>Studio (Programm) einer Karte wählen; null = keins.</summary>
    public void BigSetProgram(string cardId, string? key)
    {
        if (key is not null && !Programs.ContainsKey(key)) throw new ArgumentException("Dieses Studio gibt es nicht.");
        BigChange(b => { if (b.Cards.FirstOrDefault(x => x.Id == cardId) is { } c) { c.Program = key; c.Studio = key is null ? null : Programs[key].Studio; } });
    }

    /// <summary>Karte in der Kette früher (−1) oder später (+1).</summary>
    public void BigReorder(string cardId, int delta) => BigChange(b => BigBoardHandoff.Reorder(b, cardId, delta));

    /// <summary>Karte an ihr Studio übergeben: Auftrag als Übergabe ins Projekt, Programm öffnet sich mit vorbereitetem Dokument (im Projekt gespeichert).
    /// Ein Schritt im Rückgängig der Tafel (Strg+Z nimmt Übergabe, Auftrag und Dokument zusammen zurück).</summary>
    public ProgramDocument BigHandOver(string cardId) => BigSend(cardId, reopen: false);

    /// <summary>Erledigte Karte wieder öffnen: erneut an dasselbe Studio übergeben, das bisherige Dokument öffnet sich wieder; der nächste Export wird eine neue Fassung.</summary>
    public ProgramDocument BigReopen(string cardId) => BigSend(cardId, reopen: true);

    ProgramDocument BigSend(string cardId, bool reopen)
    {
        var pid = BigProject ?? throw new InvalidOperationException("Es ist kein Großprojekt offen.");
        ProgramDocument? d = null; ProgramSpec? spec = null; var title = "";
        BigChangeQuiet(pid, b =>
        {
            var c = b.Cards.FirstOrDefault(x => x.Id == cardId) ?? throw new ArgumentException("Diese Karte gibt es nicht.");
            if (c.Program is not { } key || !Programs.TryGetValue(key, out var sp)) throw new InvalidOperationException("Bitte zuerst ein Studio für die Karte wählen.");
            if (!reopen && c.Status == BigBoardHandoff.Handed) throw new InvalidOperationException("Diese Karte ist schon übergeben – „Im Studio öffnen“ zeigt ihr Dokument.");
            var input = BigBoardHandoff.InputFor(_core.Projects, pid, b, c);
            if (reopen) BigBoardHandoff.Reopen(_core.Projects, pid, b, c, sp.Studio, sp.Title);
            else BigBoardHandoff.HandOver(_core.Projects, pid, b, c, sp.Studio, sp.Title);
            var old = c.Doc is { } rel0 ? _core.Projects.ResolveInProject(pid, rel0) : null;
            if (reopen && old is not null && File.Exists(old)) d = ProgramOpen(key, old); // dasselbe Dokument – nichts geht verloren
            else
            {
                d = ProgramNew(key);
                d.Title = c.Title;
                BigPrepare(key, d, c, input);
                var rel = $"grossprojekt/auftraege/{c.Id}-{DevStudio.SafeName(c.Title)}{ProgramDocument.Extension}";
                var full = _core.Projects.ResolveInProject(pid, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                ProgramSave(key, full);
                c.Doc = rel;
            }
            spec = sp; title = c.Title;
        }, reopen ? "Karte wieder geöffnet" : "An Studio übergeben");
        _bigBound[d!.Id] = (pid, cardId);
        _core.Audit.Append(Principal.LocalOwner.Name, reopen ? "grossprojekt.wieder" : "grossprojekt.uebergabe", $"{pid} {cardId} → {spec!.Studio}");
        BigCard = cardId; _bigMsg = reopen ? $"„{title}“ ist wieder offen – {spec.Title} hat das Dokument." : $"„{title}“ ist an {spec.Title} übergeben.";
        _progMsg[spec.Key] = reopen ? $"Wieder offen aus dem Großprojekt: „{title}“. Der nächste Export kommt als neue Fassung an die Karte zurück."
            : $"Auftrag aus dem Großprojekt: „{title}“. Exportiere das Ergebnis – es geht von selbst an die Karte zurück.";
        Show(spec.Key);
        return d;
    }

    /// <summary>Vorbereitetes Dokument: Titel immer; Text-Programme bekommen die Aufgabe (und eine Text-Eingabe), Datei-Programme die Eingabedatei der Karte davor.</summary>
    void BigPrepare(string key, ProgramDocument d, BoardCard c, string? input)
    {
        var task = c.Detail.Trim().Length > 0 ? c.Detail.Trim() : c.Title;
        var ext = input is null ? "" : Path.GetExtension(input).ToLowerInvariant();
        if (key == BookKey)
        {
            var text = $"# {c.Title}\n\n{task}\n";
            if (ext is ".md" or ".txt") text += "\n" + File.ReadAllText(input!).Replace("\r\n", "\n");
            d.Data["title"] = c.Title; d.Data["text"] = text;
        }
        else if (key == PwKey && input is not null && (ext == ".pdf" || PwIsImage(input)))
        {
            if (PwIsImage(input)) PwSetTool(d, "bilder");
            PwSetFiles(d, [input], "Eingabe aus dem Großprojekt");
        }
    }

    /// <summary>Karte zum Dokument über den gespeicherten Pfad (nach einem Neustart). Gegenprüfung T-0743: kein Lauf über alle Projekte – nur ein Dokument, das bei den
    /// Aufträgen eines Projekts liegt (<c>&lt;Projekt&gt;/files/grossprojekt/auftraege/</c>, dort legt die Übergabe es ab), wird geprüft, und nur in diesem einen Projekt.
    /// Eine kaputte Karte wird übersprungen (Protokolleintrag), nie eine Ausnahme.</summary>
    (string Project, string Card)? BigBoundByPath(ProgramDocument d)
    {
        if (d.Path is not { } path) return null;
        var p = Path.GetFullPath(path);
        var orders = Path.GetDirectoryName(p); var big = orders is null ? null : Path.GetDirectoryName(orders); var files = big is null ? null : Path.GetDirectoryName(big);
        var projDir = files is null ? null : Path.GetDirectoryName(files);
        if (orders is null || big is null || files is null || projDir is null || Path.GetFileName(orders) != "auftraege" || Path.GetFileName(big) != "grossprojekt" || Path.GetFileName(files) != "files") return null;
        var pid = Path.GetFileName(projDir);
        try { if (!string.Equals(Path.GetFullPath(_core.Projects.ProjectFilesRoot(pid)).TrimEnd(Path.DirectorySeparatorChar), files.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) return null; }
        catch (ProjectIsolationException) { return null; }
        foreach (var c in BigBoards.Load(_core.Projects, pid).Cards.Where(x => x.Doc is not null))
            try { if (string.Equals(Path.GetFullPath(_core.Projects.ResolveInProject(pid, c.Doc!)), p, StringComparison.OrdinalIgnoreCase)) return (pid, c.Id); }
            catch (ProjectIsolationException) { BigLog("grossprojekt.rueckweg.uebersprungen", $"{pid} {c.Id}: Dokumentpfad außerhalb des Projekts"); }
        return null;
    }

    void BigLog(string what, string detail) { try { _core.Audit.Append(Principal.LocalOwner.Name, what, detail); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* Protokoll nicht schreibbar – der Export geht vor */ } }

    /// <summary>Von der Programm-Hülle (Export-Knopf, Menü, Claude „exportieren“) nach jedem Export: gehört das Dokument zu einer Karte, kommt die Datei an sie zurück –
    /// bei einer erledigten Karte als neue Fassung. Der Export selbst scheitert nie am Rückweg: jeder Fehler hier wird gemeldet und protokolliert, nicht weitergeworfen.</summary>
    void BigExported(string key, ProgramDocument d, string file)
    {
        try
        {
            if (!File.Exists(file)) return;
            var bound = _bigBound.TryGetValue(d.Id, out var x) ? x : BigBoundByPath(d);
            if (bound is not { } at) return;
            var r = BigReturnCore(at.Project, at.Card, Programs[key].Studio, file);
            var add = r switch
            {
                null => "",
                { Unchanged: true } => $" · Schon an der Karte „{r.Value.Title}“ – unverändert.",
                { Version: > 1 } => $" · Zurück an die Karte „{r.Value.Title}“ im Großprojekt – neue Fassung {r.Value.Version}, die alte bleibt als Beleg.",
                _ => $" · Zurück an die Karte „{r.Value.Title}“ im Großprojekt.",
            };
            _progMsg[key] = (_progMsg.GetValueOrDefault(key) ?? "") + add;
        }
        catch (Exception e) // bewusst breit: der Rückweg ist eine Zugabe zum Export und darf ihn nie abbrechen
        {
            BigLog("grossprojekt.rueckweg.fehler", $"{key} {Path.GetFileName(file)}: {e.GetType().Name} {e.Message}");
            _progMsg[key] = (_progMsg.GetValueOrDefault(key) ?? "") + " · An die Karte zurück ging nicht: " + FriendlyError.Say(e);
        }
    }

    /// <summary>Ergebnisdatei an die Karte (auch von Hand: gewählte oder abgelegte Datei). Gibt den Kartentitel zurück, null = Karte fehlt oder ist nicht übergeben.
    /// Bei einer erledigten Karte wird die Datei die neue Fassung (die alte bleibt als Beleg); dieselbe Datei noch einmal ändert nichts.</summary>
    public string? BigReturn(string projectId, string cardId, string studio, string file) => BigReturnCore(projectId, cardId, studio, file)?.Title;

    (string Title, int Version, bool Unchanged)? BigReturnCore(string projectId, string cardId, string studio, string file)
    {
        var b = BigBoards.Load(_core.Projects, projectId);
        if (b.Cards.FirstOrDefault(c => c.Id == cardId) is not { } card || card.Status == BigBoardHandoff.Open) return null;
        if (card.Status == BigBoardHandoff.Done && card.ResultSha is { } sha && File.Exists(file)
            && Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file))).Equals(sha, StringComparison.OrdinalIgnoreCase))
            return (card.Title, BigBoardHandoff.Versions(card), true);
        var version = 0;
        BigChangeQuiet(projectId, nb =>
        {
            var c = nb.Cards.First(x => x.Id == cardId);
            BigBoardHandoff.Return(_core.Projects, projectId, c, studio, file, c.Program is { } pk && Programs.TryGetValue(pk, out var ps) ? ps.Title : null);
            version = BigBoardHandoff.Versions(c);
        }, card.Status == BigBoardHandoff.Done ? "Neue Fassung zurück" : "Ergebnis zurück");
        _core.Audit.Append(Principal.LocalOwner.Name, "grossprojekt.ergebnis", $"{projectId} {cardId} {Path.GetFileName(file)} Fassung {version}");
        if (projectId == BigProject) _bigMsg = version > 1 ? $"„{card.Title}“: neue Fassung {version} des Ergebnisses ist an der Karte – die alte bleibt als Beleg." : $"„{card.Title}“ ist erledigt – das Ergebnis hängt an der Karte.";
        return (card.Title, version, false);
    }

    /// <summary>Vorbereitetes Dokument der Karte wieder im Studio öffnen.</summary>
    public void BigOpenInStudio(string cardId)
    {
        var pid = BigProject ?? throw new InvalidOperationException("Es ist kein Großprojekt offen.");
        var c = BigBoards.Load(_core.Projects, pid).Cards.FirstOrDefault(x => x.Id == cardId) ?? throw new ArgumentException("Diese Karte gibt es nicht.");
        if (c.Program is not { } key || c.Doc is null) throw new InvalidOperationException("Diese Karte wurde noch nicht übergeben.");
        var d = ProgramOpen(key, _core.Projects.ResolveInProject(pid, c.Doc));
        _bigBound[d.Id] = (pid, c.Id);
        Show(key);
    }

    /// <summary>Vorschau des Ergebnisses: erste PDF-Seite, Bild oder Textanfang; sonst Name und Größe.</summary>
    Control BigPreview(string file)
    {
        var ext = Path.GetExtension(file).ToLowerInvariant();
        var box = new Border { Name = "big-result-preview", CornerRadius = new CornerRadius(8), BorderBrush = Edge, BorderThickness = new Thickness(1), Padding = new Thickness(6), Background = Brush.Parse(C("#22000000", "#0F000000")) };
        if (ext == ".pdf") { var img = new Image { Name = "big-result-image", MaxHeight = 260, Stretch = Stretch.Uniform }; PdfThumb(img, file, 0, 0, 40); box.Child = img; }
        else if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp")
        {
            try { box.Child = new Image { Name = "big-result-image", MaxHeight = 260, Stretch = Stretch.Uniform, Source = new Bitmap(file) }; }
            catch (Exception) { box.Child = P(Path.GetFileName(file), Muted, 11); }
        }
        else if (ext is ".txt" or ".md" or ".csv" or ".html" or ".json")
        {
            var t = File.ReadAllText(file); box.Child = new TextBlock { Name = "big-result-text", Text = t.Length > 600 ? t[..600] + " …" : t, Foreground = Text, FontSize = 11, TextWrapping = TextWrapping.Wrap };
        }
        else box.Child = P($"{Path.GetFileName(file)} · {new FileInfo(file).Length / 1024.0:0.#} KB", Muted, 11);
        return box;
    }

    /// <summary>Rechtsklick auf eine Karte.</summary>
    ContextMenu BigCardMenu(BoardCard c, int index, int count)
    {
        var cid = c.Id;
        var menu = new ContextMenu { Name = "big-card-menu" };
        void Item(string name, string header, Action run, bool enabled = true)
        {
            var mi = new MenuItem { Name = name, Header = header, IsEnabled = enabled };
            mi.Click += (_, _) => Try("produktion", () => { BigCard = cid; run(); });
            menu.Items.Add(mi);
        }
        Item("big-m-handover", "An Studio übergeben", () => BigHandOver(cid), c.Program is not null && c.Status == BigBoardHandoff.Open);
        if (c.Status == BigBoardHandoff.Done) Item("big-m-reopen", "Karte wieder öffnen", () => BigReopen(cid), c.Program is not null);
        Item("big-m-up", "Früher in der Kette", () => BigReorder(cid, -1), index > 0);
        Item("big-m-down", "Später in der Kette", () => BigReorder(cid, 1), index < count - 1);
        menu.Items.Add(new Separator());
        Item("big-m-trash", "In den Papierkorb", () => { BigCard = null; BigChange(b => BigBoards.Remove(b, cid)); });
        return menu;
    }

    /// <summary>Dateien aus dem Explorer auf eine Karte ziehen: übergebene Karte → Ergebnis, sonst Beleg.</summary>
    void BigCardDrop(Control card, string cardId)
    {
        DragDrop.SetAllowDrop(card, true);
        card.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = BueroDropFiles(e).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; });
        card.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            e.Handled = true;
            var files = BueroDropFiles(e);
            if (files.Count == 0 || BigProject is not { } pid) return;
            Try("produktion", () =>
            {
                BigCard = cardId;
                var c = BigBoards.Load(_core.Projects, pid).Cards.FirstOrDefault(x => x.Id == cardId);
                if (c is { Status: BigBoardHandoff.Handed, Studio: { } st }) { BigReturn(pid, cardId, st, files[0]); Show("produktion"); }
                else BigChange(b => { if (b.Cards.FirstOrDefault(x => x.Id == cardId) is { } x) foreach (var f in files) BigBoards.AddEvidence(_core.Projects, pid, x, f, "abgelegt"); });
            });
        });
    }
}
