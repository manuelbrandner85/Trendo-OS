using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Trendo.Core;

namespace Trendo.App;

/// <summary>X-03: Baukasten ohne Programmieren – als App-Designer (Masterprompt 131, Runde 7): Bausteine links per Ziehen (oder Klick) in die
/// Live-Vorschau legen, in der Vorschau anklicken und ziehen, rechts Eigenschaften einstellen; Vorschau als Handy oder Bildschirm, Rückgängig,
/// im Browser ausprobieren und als ZIP exportieren.
/// T-0765: echter App-Baukasten – mehrere Seiten mit Navigation und Knopf „zu Seite X“, Datensammlungen mit Eingabe-Formular (Pflichtfelder),
/// Datenliste und Auswertung (Anzahl/Summe), gespeichert im Browser der fertigen App (ohne Server, ohne Netz); Text per Doppelklick direkt auf der
/// Seite bearbeiten, Vorlage „Einkaufsliste“, exportierte ZIP wieder öffnen.</summary>
public partial class MainWindow
{
    public string BuilderFolder => WorkFolder("Baukasten");
    public string BuilderFile => Path.Combine(BuilderFolder, "seite.json");
    private SitePage? _page;
    public SitePage BuilderPage => _page ??= LoadBuilder();
    public int BuilderSel { get; set; } = -1;
    /// <summary>T-0765: gerade bearbeitete Seite der App (Index in <see cref="SitePage.Pages"/>).</summary>
    public int BuilderScreen { get; set; }
    /// <summary>T-0765: Baustein, dessen Text gerade direkt auf der Seite bearbeitet wird (-1 = keiner).</summary>
    public int BuilderInline { get; private set; } = -1;
    public bool BuilderPhone { get; set; } = true;
    /// <summary>T-0765: Noch nichts gespeichert (keine seite.json) – die Seite zeigt den ersten Schritt.</summary>
    public bool BuilderFresh { get; private set; }
    /// <summary>T-0716: Rückgängig über die Hülle (bis 100 Schritte; vorher eigener Stapel ohne Grenze) – Zahl der Schritte, die zurückgehen.</summary>
    public int BuilderUndoCount => BuilderLink.Link.Doc.History.Count;
    /// <summary>T-0716: Meldung, wenn <c>seite.json</c> von einer neueren Trendo-Fassung stammt – dann wird nichts überschrieben.</summary>
    string? _builderProblem;

    /// <summary>T-0716 (B6 Etappe E4): Baukasten an Rückgängig (100 Schritte), Notsicherung (getippte, noch nicht übernommene Eingaben) und Wiederherstellen-Karte
    /// der Hülle (<see cref="LiveSlot"/>). Gespeichert wird weiter sofort in <c>seite.json</c> – jetzt atomar und mit Versionsfeld.
    /// T-0765: Ort der Eingaben = „Seite:Baustein“ (vorher nur der Baustein).</summary>
    public LinkSlot BuilderLink => LiveSlot("baukasten", "LINK-BAUKASTEN", "baukasten", "builder", "Eine Baukasten-Seite", "Baukasten-Seiten",
        () => System.Text.Json.Nodes.JsonNode.Parse(BuilderPage.ToJson()), // T-0765: genau die gespeicherte Form (Fassung 1 oder 2) – sonst wirkt ein Neuladen wie eine Änderung
        n => { _page = n is null ? new SitePage() : SitePage.FromJson(n.ToJsonString()); BuilderClamp(); PersistBuilder(); },
        () => BuilderScreen.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + BuilderSel.ToString(System.Globalization.CultureInfo.InvariantCulture),
        o =>
        {
            var parts = o.Split(':'); static int? N(string s) => int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var i) ? i : null;
            if (parts.Length == 2) { if (N(parts[0]) is { } s) BuilderScreen = s; if (N(parts[1]) is { } b) BuilderSel = b; }
            else if (N(o) is { } i) BuilderSel = i; // Sicherungen vor T-0765
            BuilderClamp();
        },
        title: () => BuilderPage.Title.Length > 0 ? BuilderPage.Title : "Seite");

    private void InitW4Builder() => RegisterTab(KreativTabs, "baukasten", "Baukasten", "X-03", BuilderView, "no-code", "webseite", "homepage", "baukasten", "ohne programmieren", "app-designer", "app bauen", "einkaufsliste", "formular", "datenliste");

    private void BuilderSave() { PersistBuilder(); Show("baukasten"); }

    private void ReloadBuilder() => _page = LoadBuilder();

    static SitePage BuilderRead(string file, ref string? problem)
    {
        try { return SitePage.Load(file); }
        catch (InvalidDataException e) { problem = e.Message; return new SitePage(); } // neuere Fassung: nichts überschreiben (siehe BuilderChange)
    }

    private SitePage LoadBuilder()
    {
        _builderProblem = null; BuilderFresh = false;
        var project = NewestProject();
        if (project is not null)
        {
            try
            {
                var full = _core.Projects.ResolveInProject(project, "baukasten/seite.json");
                if (File.Exists(full)) return BuilderRead(full, ref _builderProblem);
            }
            catch (Exception ex) when (ex is ProjectIsolationException or IOException) { }
        }
        if (File.Exists(BuilderFile)) return BuilderRead(BuilderFile, ref _builderProblem);
        BuilderFresh = true;
        return new SitePage { Blocks = [new("h1", "Willkommen"), new("p", "Schreib hier etwas über dich.")] };
    }

    private void PersistBuilder()
    {
        if (_builderProblem is not null) throw new InvalidOperationException(_builderProblem);
        var page = BuilderPage;
        var project = NewestProject();
        if (project is null) { page.Save(BuilderFile); return; }
        // T-0716: atomar (vorher File.WriteAllText über ProjectStore.WriteFile) – im Projekt und im Baukasten-Ordner
        AtomicFile.WriteAllText(_core.Projects.ResolveInProject(project, "baukasten/seite.json"), page.ToJson());
        _core.Audit.Append("user", "file.write", $"{project}:baukasten/seite.json");
        page.Save(BuilderFile);
    }

    public string BuilderPublish()
    {
        var p = BuilderPage.Publish(Path.Combine(BuilderFolder, "fertig"), BuilderFolder);
        _core.Audit.Append(Principal.LocalOwner.Name, "builder.publish", $"{BuilderPage.Pages.Sum(s => s.Blocks.Count)} Bausteine, {BuilderPage.Pages.Count} Seite(n)");
        return p;
    }

    void BuilderClamp()
    {
        var pg = _page ?? BuilderPage;
        BuilderScreen = Math.Clamp(BuilderScreen, 0, pg.Pages.Count - 1);
        BuilderSel = Math.Min(BuilderSel, pg.Pages[BuilderScreen].Blocks.Count - 1);
    }

    /// <summary>T-0765: Bausteine der gerade bearbeiteten Seite.</summary>
    List<Block> BuilderBlocks(SitePage pg) => pg.Pages[Math.Clamp(BuilderScreen, 0, pg.Pages.Count - 1)].Blocks;
    /// <summary>T-0765 (Tests, Anzeige): Bausteine der gerade bearbeiteten Seite.</summary>
    public IReadOnlyList<Block> BuilderScreenBlocks => BuilderBlocks(BuilderPage);

    /// <summary>Änderung als ein Schritt im Rückgängig, danach speichern und neu zeichnen.</summary>
    /// <summary>T-0716: Der Schritt entsteht beim Neuaufbau der Seite (<see cref="LiveSync"/>: gespeicherte Daten anders als im Verlauf).</summary>
    void BuilderChange(Action<SitePage> change)
    {
        if (_builderProblem is not null) { Say(_builderProblem, "baukasten"); return; }
        change(BuilderPage); BuilderFresh = false; BuilderClamp(); BuilderSave();
    }
    /// <summary>Rückgängig über die Hülle (bis 100 Schritte) – schreibt den früheren Stand wieder in seite.json.</summary>
    public void BuilderUndo() { LinkUndo(BuilderLink); Show("baukasten"); }
    public void BuilderRedo() { LinkRedo(BuilderLink); Show("baukasten"); }

    static string BuilderDefault(string kind) => kind switch
    {
        "h1" => "Überschrift", "p" => "Neuer Text", "button" => "Mehr erfahren", "list" => "Punkt eins\nPunkt zwei", "contact" => "Schreib mir", "img" => "Bild",
        "form" => "Speichern", "datalist" => "Noch keine Einträge.", "stat" => "", _ => "",
    };

    /// <summary>Baustein an einer Stelle einfügen (Ziehen aus der Leiste; öffentlich für Tests). T-0765: Daten-Bausteine bekommen die erste Datensammlung
    /// (gibt es noch keine, entsteht „Einträge“ mit einem Pflichtfeld).</summary>
    public void BuilderInsert(string kind, int index)
    {
        var at = Math.Clamp(index, 0, BuilderBlocks(BuilderPage).Count);
        BuilderSel = at; BuilderInline = -1;
        BuilderChange(pg =>
        {
            string? data = null; var url = "";
            if (SitePage.IsDataKind(kind)) { data = (pg.Collections.FirstOrDefault() ?? pg.AddCollection("Einträge", [])).Id; if (kind == "stat") url = "anzahl"; }
            BuilderBlocks(pg).Insert(at, new(kind, BuilderDefault(kind), url, data));
        });
    }
    /// <summary>Baustein in der Vorschau an eine andere Stelle ziehen (öffentlich für Tests).</summary>
    public void BuilderMove(int from, int to)
    {
        var n = BuilderBlocks(BuilderPage).Count; if (from < 0 || from >= n || to < 0 || to >= n || from == to) return;
        BuilderSel = to; BuilderChange(pg => { var bl = BuilderBlocks(pg); var b = bl[from]; bl.RemoveAt(from); bl.Insert(to, b); });
    }

    /// <summary>T-0765: Baustein ändern (Text direkt auf der Seite, Link, Datensammlung …) – ein Rückgängig-Schritt.</summary>
    public void BuilderSetBlock(int idx, Func<Block, Block> change)
    {
        if (idx < 0 || idx >= BuilderBlocks(BuilderPage).Count) return;
        var now = BuilderBlocks(BuilderPage)[idx]; var next = change(now);
        if (next == now) return;
        BuilderChange(pg => BuilderBlocks(pg)[idx] = next);
    }

    // ── T-0765: Text direkt auf der Seite (Doppelklick oder F2) ──
    static bool BuilderInlineKind(string kind) => kind is "h1" or "p" or "button" or "list" or "contact" or "form" or "datalist" or "stat";
    public void BuilderEditInline(int idx)
    {
        if (idx < 0 || idx >= BuilderBlocks(BuilderPage).Count || !BuilderInlineKind(BuilderBlocks(BuilderPage)[idx].Kind)) return;
        BuilderSel = idx; BuilderInline = idx; Show("baukasten");
    }
    void BuilderInlineDone(int idx, string? text)
    {
        if (BuilderInline != idx) return;
        BuilderInline = -1;
        if (text is not null && idx < BuilderBlocks(BuilderPage).Count && text != BuilderBlocks(BuilderPage)[idx].Text) BuilderSetBlock(idx, b => b with { Text = text });
        else Show("baukasten");
    }

    // ── T-0765: Seiten ──
    public void BuilderGoTo(int screen) { BuilderScreen = Math.Clamp(screen, 0, BuilderPage.Pages.Count - 1); BuilderSel = -1; BuilderInline = -1; Show("baukasten"); }
    public void BuilderAddPage(string name)
    {
        var at = BuilderScreen + 1; BuilderSel = -1; BuilderInline = -1;
        BuilderChange(pg => { var s = pg.AddPage(name, at); BuilderScreen = pg.Pages.IndexOf(s); s.Blocks.Add(new("button", "Zurück", "seite:" + pg.Pages[0].Id)); });
    }
    public void BuilderRenamePage(int i, string name) { if (i >= 0 && i < BuilderPage.Pages.Count && name.Trim().Length > 0 && name.Trim() != BuilderPage.Pages[i].Name) BuilderChange(pg => pg.RenamePage(i, name)); }
    public void BuilderMovePage(int i, int delta)
    {
        var j = i + delta; if (i < 0 || j < 0 || i >= BuilderPage.Pages.Count || j >= BuilderPage.Pages.Count) return;
        BuilderChange(pg => { pg.MovePage(i, delta); if (BuilderScreen == i) BuilderScreen = j; else if (BuilderScreen == j) BuilderScreen = i; });
    }
    public void BuilderDeletePage(int i)
    {
        if (BuilderPage.Pages.Count <= 1 || i < 0 || i >= BuilderPage.Pages.Count) return;
        BuilderSel = -1; BuilderInline = -1;
        BuilderChange(pg => { pg.DeletePage(i); if (BuilderScreen >= i) BuilderScreen = Math.Max(0, BuilderScreen - 1); });
    }

    // ── T-0765: Daten ──
    public void BuilderAddCollection(string name, string fields = "") => BuilderChange(pg => pg.AddCollection(name, SitePage.ParseFields(fields)));
    void BuilderCollection(string id, Action<DataCollection> change) { if (BuilderPage.Collection(id) is not null) BuilderChange(pg => change(pg.Collection(id)!)); }
    public void BuilderAddField(string coll, string label, string type = "text", bool required = false) => BuilderCollection(coll, c => SitePage.AddField(c, label, type, required));
    public void BuilderSetField(string coll, int k, Func<DataField, DataField> change)
    {
        if (BuilderPage.Collection(coll) is not { } c || k < 0 || k >= c.Fields.Count) return;
        var next = change(c.Fields[k]); if (next == c.Fields[k]) return;
        BuilderCollection(coll, x => x.Fields[k] = next);
    }
    public void BuilderRemoveField(string coll, int k) { if (BuilderPage.Collection(coll) is { Fields.Count: > 1 } c && k >= 0 && k < c.Fields.Count) BuilderCollection(coll, x => x.Fields.RemoveAt(k)); }

    static void BuilderAdopt(SitePage dst, SitePage src)
    {
        dst.Title = src.Title; dst.Accent = src.Accent; dst.AppId = src.AppId; dst.Pages = src.Pages; dst.Collections = src.Collections;
    }

    /// <summary>T-0765: Vorlage übernehmen (ersetzt den jetzigen Stand – Rückgängig holt ihn zurück).</summary>
    public void BuilderUseTemplate(string id)
    {
        if (SitePage.Template(id) is not { } t) return;
        BuilderScreen = 0; BuilderSel = -1; BuilderInline = -1;
        BuilderChange(pg => BuilderAdopt(pg, t));
        Say($"Vorlage „{t.Title}“ geladen: {t.Pages.Count} Seiten, Datensammlung „{t.Collections[0].Name}“. Probier sie mit „Ausprobieren im Browser“ aus.", "baukasten");
    }

    /// <summary>T-0765 (Rundreise): exportierte ZIP, fertigen Ordner oder index.html wieder öffnen – als ein Rückgängig-Schritt.</summary>
    public void BuilderOpenExport(string path)
    {
        // Gegenprüfung M5: erst prüfen, dann schreiben – bei einer neueren seite.json (T-0716) wird gar nichts angefasst; Bilder kommen in einen
        // eigenen Unterordner und überschreiben nie Vorhandenes (FromExport liest und prüft alles, bevor es schreibt).
        if (_builderProblem is not null) { Say(_builderProblem, "baukasten"); return; }
        var p = SitePage.FromExport(path, BuilderFolder, "importiert/" + DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));
        BuilderScreen = 0; BuilderSel = -1; BuilderInline = -1;
        BuilderChange(pg => BuilderAdopt(pg, p));
        _core.Audit.Append(Principal.LocalOwner.Name, "builder.import", Path.GetFileName(path));
        Say($"Geöffnet: „{p.Title}“ mit {p.Pages.Count} Seite(n).", "baukasten");
    }

    async Task BuilderPickOpen()
    {
        if (await PickMedia("baukasten-oeffnen", ["*.zip", "*.html"]) is not { } f) return;
        Try("baukasten", () => BuilderOpenExport(f));
    }

    /// <summary>Fertige Seite als ZIP zum Hochladen (index.html und Bilder).</summary>
    public string BuilderExport()
    {
        var dir = Path.GetDirectoryName(BuilderPublish())!;
        var stem = Path.Combine(BuilderFolder, DevStudio.SafeName(BuilderPage.Title.Length > 0 ? BuilderPage.Title : "Seite")); var zip = stem + ".zip";
        for (var k = 2; File.Exists(zip); k++) zip = $"{stem} ({k}).zip";
        System.IO.Compression.ZipFile.CreateFromDirectory(dir, zip);
        _core.Audit.Append(Principal.LocalOwner.Name, "builder.export", Path.GetFileName(zip));
        return zip;
    }

    /// <summary>T-0718: Ergebnis der letzten Veröffentlichung aus dem Baukasten (Tests, Anzeige).</summary>
    public WebPublishing.Ergebnis? BuilderPublishResult { get; private set; }

    /// <summary>T-0718: Baukasten-Seite in einen Ordner veröffentlichen – nur über das Freigabe-Tor (Rückfrage mit Ziel, Dateien, Größe),
    /// ausgeführt vom Kern mit genau dieser Freigabe, danach Link- und Dateiprüfung. Kinderprofil: nichts passiert.</summary>
    public async Task BuilderPublishToFolder()
    {
        const string V = "baukasten";
        string dir;
        try { dir = Path.GetDirectoryName(BuilderPublish())!; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Say("Nicht veröffentlicht: Die fertige Seite ließ sich nicht schreiben (" + e.Message + ").", V); return; }
        var plan = WebDesignPublish.PrepareFolder(dir);
        if (!plan.Ok) { Say("Nicht veröffentlicht, die Prüfung hat Fehler: " + plan.Blockers[0] + "\nWas tun: den Fehler in der Seite beheben und noch einmal veröffentlichen.", V); return; }
        if (await PickFolderPath("Wohin soll die fertige Seite?") is not { } dest) return;
        if (WebPublishing.FolderError(dir, dest) is { } fe) { BuilderPublishResult = fe; Say(fe.Text, V); return; }
        var r = await WebPublishViaGate(WebPublishing.Describe(WebPublishing.Weg.Ordner, dest, plan),
            id => Task.FromResult(WebPublishing.ToFolder(_core.Approvals, id, dir, plan, dest)));
        if (r is null) { Say(CanApprove ? "Abgebrochen – es wurde nichts veröffentlicht." : "Nicht veröffentlicht: " + NurErwachsene, V); return; }
        BuilderPublishResult = r;
        _core.Audit.Append(CurrentPrincipal.Name, r.Dateien > 0 ? "web.veroeffentlicht" : "web.veroeffentlichen.fehler", $"baukasten {r.Dateien} Dateien → {r.Wo}; Befunde={r.Befunde.Count}");
        Say(r.Text, V);
    }

    /// <summary>Welcher Vorschau-Baustein liegt unter dem Zeiger? -1 = keiner; bei Leerraum unten = Ende.</summary>
    int BuilderHit(PointerEventArgs e)
    {
        var hit = this.InputHitTest(e.GetPosition(this)) as Visual;
        while (hit is not null && !(hit is Control { Tag: int } c && c.Name?.StartsWith("builder-block-") == true)) { if (hit is Control { Name: "builder-preview-frame" }) return BuilderBlocks(BuilderPage).Count; hit = hit.GetVisualParent(); }
        return hit is Control { Tag: int i } ? i : -1;
    }

    static readonly IBrush BuilderInk = Brush.Parse("#1B2333"), BuilderGrey = Brush.Parse("#8A94A6"), BuilderLine = Brush.Parse("#D5DAE3"), BuilderSoft = Brush.Parse("#F5F6F9");

    /// <summary>T-0765: Vorschau der Daten-Bausteine (in der fertigen App füllt das eingebettete Skript sie mit den im Browser gespeicherten Einträgen).</summary>
    Control BuilderDataPreview(SitePage pg, Block b, IBrush acc)
    {
        var c = pg.Collection(b.Data);
        if (c is null) return new TextBlock { Text = "Keine Datensammlung gewählt – rechts eine auswählen.", Foreground = Brush.Parse("#B45309"), TextWrapping = TextWrapping.Wrap };
        switch (b.Kind)
        {
            case "form":
                var f = new StackPanel { Spacing = 6 };
                foreach (var x in c.Fields)
                {
                    f.Children.Add(new TextBlock { Text = x.Label + (x.Required ? " *" : "") + (x.Type == "zahl" ? "  (Zahl)" : ""), Foreground = BuilderInk, FontWeight = FontWeight.SemiBold, FontSize = 13 });
                    f.Children.Add(new Border { Height = 30, BorderBrush = Brush.Parse("#C5CCD8"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Background = Brushes.White });
                }
                f.Children.Add(new Border { Background = acc, CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 8), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0), Child = new TextBlock { Text = b.Text.Length > 0 ? b.Text : "Speichern", Foreground = Brushes.White, FontWeight = FontWeight.SemiBold } });
                f.Children.Add(new TextBlock { Text = $"Speichert in „{c.Name}“", Foreground = BuilderGrey, FontSize = 11 });
                return f;
            case "datalist":
                return new Border { BorderBrush = BuilderLine, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 9),
                    Child = new StackPanel { Spacing = 3, Children = {
                        new TextBlock { Text = b.Text.Length > 0 ? b.Text : "Noch keine Einträge.", Foreground = BuilderGrey, TextWrapping = TextWrapping.Wrap },
                        new TextBlock { Text = $"Liste „{c.Name}“ – Einträge erscheinen hier, gespeichert im Browser", Foreground = BuilderGrey, FontSize = 11 } } } };
            default:
                return new Border { Background = BuilderSoft, CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8),
                    Child = new DockPanel { Children = {
                        new TextBlock { Text = "0", FontSize = 18, FontWeight = FontWeight.Bold, Foreground = BuilderInk, [DockPanel.DockProperty] = Avalonia.Controls.Dock.Right },
                        new TextBlock { Text = b.Text.Length > 0 ? b.Text : pg.MeasureLabel(b), Foreground = BuilderInk, VerticalAlignment = VerticalAlignment.Center } } } };
        }
    }

    private Control BuilderView()
    {
        const string V = "baukasten";
        ReloadBuilder();
        var link = BuilderLink; LiveSync(link); // T-0716: gespeicherte Änderung → Rückgängig-Schritt
        var pg = BuilderPage;
        BuilderClamp();
        var screen = pg.Pages[BuilderScreen];
        var blocksOf = screen.Blocks;
        // Wiederhergestellt oder Rückgängig: noch nicht übernommener Text aus der Direktbearbeitung steht wieder im Feld auf der Seite
        if (BuilderInline < 0)
            foreach (var kv in LiveInputs(link))
                if (kv.Key.StartsWith("builder-inline-", StringComparison.Ordinal) && int.TryParse(kv.Key["builder-inline-".Length..], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var ii)) { BuilderInline = ii; BuilderSel = ii; break; }
        if (BuilderInline >= blocksOf.Count) BuilderInline = -1;
        var acc = Brush.Parse(System.Text.RegularExpressions.Regex.IsMatch(pg.Accent, "^#[0-9A-Fa-f]{6}$") ? pg.Accent : "#2F86F6");

        // ── links: Seiten und Bausteine (ziehen oder klicken) ──
        var palette = new StackPanel { Name = "builder-palette", Spacing = 4, Children = { H("Seiten", 13) } };
        for (var i = 0; i < pg.Pages.Count; i++)
        {
            var si = i; var on = i == BuilderScreen;
            var pb = new Button { Name = $"builder-page-{i}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 5),
                Background = on ? Brush.Parse("#33C8A04A") : Brushes.Transparent, BorderBrush = on ? Accent : Brushes.Transparent, BorderThickness = new Thickness(on ? 1 : 0),
                Content = new TextBlock { Text = (i == 0 ? "⌂ " : "▫ ") + pg.Pages[i].Name, Foreground = Text, TextTrimming = TextTrimming.CharacterEllipsis } };
            ToolTip.SetTip(pb, i == 0 ? "Startseite der App – öffnet sich zuerst" : "Seite bearbeiten");
            pb.Click += (_, _) => BuilderGoTo(si);
            palette.Children.Add(pb);
        }
        palette.Children.Add(Act("builder-page-add", "+ Seite", () => BuilderAddPage("Seite " + (BuilderPage.Pages.Count + 1))));
        palette.Children.Add(Act("builder-vorlage", "Vorlage: Einkaufsliste", () => BuilderUseTemplate("einkaufsliste")));
        palette.Children.Add(Act("builder-open", "Öffnen (ZIP) …", () => { _ = BuilderPickOpen(); })); // Anweisung statt Ausdruck: sonst greift die rahmenlose Task-Überladung von Act
        palette.Children.Add(new Border { Height = 8 });
        palette.Children.Add(H("Bausteine", 13));
        palette.Children.Add(P("In die Vorschau ziehen oder antippen", Muted, 11));
        foreach (var (k, l) in SitePage.Kinds)
        {
            var kind = k;
            if (kind == "form") { palette.Children.Add(new Border { Height = 4 }); palette.Children.Add(P("Daten (App)", Muted, 11)); }
            var b = new Button { Name = "builder-add-" + k, Content = "+ " + l, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 6), Cursor = new Cursor(StandardCursorType.DragMove) };
            Point? from = null; var dragged = false;
            b.AddHandler(PointerPressedEvent, (_, e) => { from = e.GetPosition(this); dragged = false; }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            b.AddHandler(PointerMovedEvent, (_, e) => { if (from is { } f && (Math.Abs(e.GetPosition(this).Y - f.Y) > 10 || Math.Abs(e.GetPosition(this).X - f.X) > 10)) { dragged = true; b.Opacity = 0.6; } }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            b.AddHandler(PointerReleasedEvent, (_, e) =>
            {
                if (from is null) return; from = null; b.Opacity = 1;
                if (!dragged) return; // Klick → Click-Ereignis
                e.Handled = true; var at = BuilderHit(e); if (at >= 0) BuilderInsert(kind, at);
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            b.Click += (_, _) => { if (!dragged) BuilderInsert(kind, BuilderSel >= 0 ? BuilderSel + 1 : blocksOf.Count); };
            palette.Children.Add(b);
        }

        // ── Mitte: Live-Vorschau der gewählten Seite ──
        var blocks = new StackPanel { Spacing = 10 };
        for (var i = 0; i < blocksOf.Count; i++)
        {
            var idx = i; var b = blocksOf[i];
            Control inner;
            if (idx == BuilderInline)
            {
                var multi = b.Kind is "p" or "list";
                var tb = new TextBox { Name = $"builder-inline-{idx}", Text = b.Text, AcceptsReturn = multi, TextWrapping = TextWrapping.Wrap, MinHeight = multi ? 70 : 0,
                    FontSize = b.Kind == "h1" ? 22 : 14, Background = Brushes.White, Foreground = BuilderInk, BorderBrush = acc, BorderThickness = new Thickness(2), CaretBrush = BuilderInk };
                // auf der weißen Seite bleibt das Feld hell – auch mit Fokus und unter dem Zeiger (das dunkle Thema setzt sonst seine Feldfarben)
                foreach (var (key, brush) in new (string, IBrush)[] { ("TextControlBackground", Brushes.White), ("TextControlBackgroundFocused", Brushes.White), ("TextControlBackgroundPointerOver", Brushes.White),
                    ("TextControlForeground", BuilderInk), ("TextControlForegroundFocused", BuilderInk), ("TextControlForegroundPointerOver", BuilderInk), ("TextControlBorderBrushFocused", acc), ("TextControlBorderBrushPointerOver", acc) })
                    tb.Resources[key] = brush;
                var done = false;
                void Finish(string? t) { if (done) return; done = true; BuilderInlineDone(idx, t); }
                tb.KeyDown += (_, e) =>
                {
                    if (e.Key == Key.Escape) { e.Handled = true; Finish(null); }
                    else if (e.Key == Key.Enter && (!multi || e.KeyModifiers == KeyModifiers.Control)) { e.Handled = true; Finish(tb.Text ?? ""); }
                };
                tb.LostFocus += (_, _) => Finish(tb.Text ?? "");
                tb.AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => { tb.Focus(); tb.SelectAll(); });
                ToolTip.SetTip(tb, multi ? "Strg+Enter übernimmt · Esc bricht ab" : "Enter übernimmt · Esc bricht ab");
                inner = tb;
            }
            else
            {
                inner = b.Kind switch
                {
                    "h1" => new TextBlock { Text = b.Text, Foreground = acc, FontSize = 26, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap },
                    "p" => new TextBlock { Text = b.Text, Foreground = BuilderInk, FontSize = 14, TextWrapping = TextWrapping.Wrap, LineHeight = 21 },
                    "button" => new StackPanel { Spacing = 2, Children = {
                        new Border { Background = acc, CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 9), HorizontalAlignment = HorizontalAlignment.Left, Child = new TextBlock { Text = b.Text, Foreground = Brushes.White, FontWeight = FontWeight.SemiBold } },
                        new TextBlock { Text = b.Url.StartsWith("seite:", StringComparison.Ordinal) ? "→ Seite „" + (pg.Pages.FirstOrDefault(s => s.Id == b.Url[6..])?.Name ?? "?") + "“" : "", Foreground = BuilderGrey, FontSize = 11, IsVisible = b.Url.StartsWith("seite:", StringComparison.Ordinal) } } },
                    "contact" => new Border { BorderBrush = acc, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 8), HorizontalAlignment = HorizontalAlignment.Left, Child = new TextBlock { Text = "" + b.Text, Foreground = acc, FontWeight = FontWeight.SemiBold } },
                    "list" => new StackPanel { Spacing = 3, Children = { } },
                    "hr" => new Border { Height = 1, Background = BuilderLine, Margin = new Thickness(0, 6) },
                    "img" => BuilderImage(b),
                    "form" or "datalist" or "stat" => BuilderDataPreview(pg, b, acc),
                    _ => new TextBlock { Text = b.Text, Foreground = BuilderInk },
                };
                if (b.Kind == "list" && inner is StackPanel lp) foreach (var line in b.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries)) lp.Children.Add(new TextBlock { Text = "•  " + line.Trim(), Foreground = BuilderInk, FontSize = 14, TextWrapping = TextWrapping.Wrap });
            }
            var on = idx == BuilderSel;
            var cell = new Border { Name = $"builder-block-{i}", Tag = idx, Padding = new Thickness(8, 6), CornerRadius = new CornerRadius(6), Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.Hand),
                BorderBrush = on ? acc : Brushes.Transparent, BorderThickness = new Thickness(on ? 2 : 1), Child = inner };
            if (idx != BuilderInline)
            {
                Point? from = null; var dragged = false;
                cell.PointerPressed += (_, e) =>
                {
                    if (!e.GetCurrentPoint(cell).Properties.IsLeftButtonPressed) return; // Rechtsklick: Kontextmenü
                    if (e.ClickCount >= 2 && BuilderInlineKind(b.Kind)) { e.Handled = true; from = null; BuilderEditInline(idx); return; } // T-0765: Doppelklick = Text direkt bearbeiten
                    from = e.GetPosition(this); dragged = false; e.Pointer.Capture(cell); e.Handled = true;
                };
                cell.PointerMoved += (_, e) => { if (from is { } f && Math.Abs(e.GetPosition(this).Y - f.Y) > 8) { dragged = true; cell.Opacity = 0.55; } };
                cell.PointerReleased += (_, e) =>
                {
                    if (from is null) return; from = null; e.Pointer.Capture(null); cell.Opacity = 1;
                    if (!dragged) { BuilderSel = idx; Show(V); return; }
                    var to = BuilderHit(e); if (to >= 0) BuilderMove(idx, Math.Min(to, blocksOf.Count - 1));
                };
                MenuItem Mi(string name, string header, Action a) { var m = new MenuItem { Name = name, Header = header }; m.Click += (_, _) => a(); return m; }
                var menu = new List<MenuItem>();
                if (BuilderInlineKind(b.Kind)) menu.Add(Mi($"builder-ctx-edit-{idx}", "Text bearbeiten (F2)", () => BuilderEditInline(idx)));
                menu.Add(Mi($"builder-ctx-up-{idx}", "Nach oben", () => BuilderMove(idx, idx - 1)));
                menu.Add(Mi($"builder-ctx-down-{idx}", "Nach unten", () => BuilderMove(idx, idx + 1)));
                menu.Add(Mi($"builder-ctx-dup-{idx}", "Verdoppeln", () => { BuilderSel = idx + 1; BuilderChange(p => BuilderBlocks(p).Insert(idx + 1, BuilderBlocks(p)[idx])); }));
                menu.Add(Mi($"builder-ctx-del-{idx}", "Entfernen (Entf)", () => { BuilderSel = Math.Min(idx, blocksOf.Count - 2); BuilderChange(p => BuilderBlocks(p).RemoveAt(idx)); }));
                cell.ContextMenu = new ContextMenu { ItemsSource = menu };
                ToolTip.SetTip(cell, "Antippen: einstellen · Doppelklick: Text bearbeiten · ziehen: umsortieren · Rechtsklick: mehr");
            }
            blocks.Children.Add(cell);
        }
        if (blocksOf.Count == 0) blocks.Children.Add(new TextBlock { Name = "builder-empty-page", Text = "Diese Seite ist leer.\nErster Schritt: Zieh links einen Baustein hierher – zum Beispiel „Überschrift“.", Foreground = BuilderGrey, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 40) });

        var inFrame = new StackPanel { Spacing = 10 };
        if (pg.Pages.Count > 1)
        {
            var nav = new WrapPanel { Name = "builder-nav", HorizontalAlignment = HorizontalAlignment.Center };
            for (var i = 0; i < pg.Pages.Count; i++)
            {
                var si = i; var on = i == BuilderScreen;
                var n = new Button { Name = $"builder-nav-{i}", Padding = new Thickness(12, 3), Margin = new Thickness(2), CornerRadius = new CornerRadius(12),
                    Background = on ? acc : Brushes.White, BorderBrush = on ? acc : BuilderLine, BorderThickness = new Thickness(1),
                    Content = new TextBlock { Text = pg.Pages[i].Name, Foreground = on ? Brushes.White : BuilderInk, FontSize = 12 } };
                ToolTip.SetTip(n, "So wechselt man in der fertigen App die Seite – antippen bearbeitet sie hier");
                n.Click += (_, _) => BuilderGoTo(si);
                nav.Children.Add(n);
            }
            inFrame.Children.Add(nav);
        }
        else inFrame.Children.Add(new TextBlock { Text = pg.Title, Foreground = BuilderGrey, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center });
        inFrame.Children.Add(blocks);
        var frame = new Border { Name = "builder-preview-frame", Width = BuilderPhone ? 390 : 760, MinHeight = 520, Background = Brushes.White, CornerRadius = new CornerRadius(BuilderPhone ? 28 : 10),
            BorderBrush = Brush.Parse("#3E3E3E"), BorderThickness = new Thickness(BuilderPhone ? 10 : 2), Padding = new Thickness(18, 22), HorizontalAlignment = HorizontalAlignment.Center, BoxShadow = CardShadow,
            Child = inFrame };
        var (undo, redo) = LinkButtons(link, "builder-undo", "builder-redo"); // T-0716: Rückgängig/Wiederholen der Hülle, bis 100 Schritte
        ToolTip.SetTip(undo, "Rückgängig (Strg+Z)"); ToolTip.SetTip(redo, "Wiederholen (Strg+Y oder Strg+Umschalt+Z)");
        var toolbar = new WrapPanel { Children = {
            Act("builder-view-phone", (BuilderPhone ? "✓ " : "") + "Handy", () => { BuilderPhone = true; Show(V); }), new Border { Width = 4 },
            Act("builder-view-desk", (BuilderPhone ? "" : "✓ ") + "Bildschirm", () => { BuilderPhone = false; Show(V); }), new Border { Width = 12 }, undo, redo, new Border { Width = 6 },
            Act("builder-preview", "Ausprobieren im Browser", () => { PersistBuilder(); Launch(BuilderPublish()); }, !BuilderFresh), new Border { Width = 4 },
            Act("builder-export", "Exportieren (ZIP)", () => Try(V, () => { var z = BuilderExport(); Say("Exportiert: " + z, V); })), new Border { Width = 4 },
            Act("builder-folder", "Ordner mit fertiger Seite", () => { BuilderPublish(); Launch(Path.Combine(BuilderFolder, "fertig")); }), new Border { Width = 4 },
            Act("builder-veroeffentlichen", "Veröffentlichen …", () => { _ = BuilderPublishToFolder(); }) } };
        var center = new StackPanel { Spacing = 10, Children = { toolbar, new ScrollViewer { Content = frame, MaxHeight = 760, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto } } };

        // ── rechts: Eigenschaften ──
        var props = new StackPanel { Name = "builder-props", Spacing = 6, Children = { H("App", 13) } };
        var title = new TextBox { Name = "builder-title", Text = pg.Title };
        void CommitTitle() { if ((title.Text ?? "") != pg.Title) BuilderChange(p => p.Title = title.Text ?? ""); }
        title.LostFocus += (_, _) => CommitTitle(); OnEnter(title, CommitTitle);
        props.Children.Add(P("Titel", Muted, 11)); props.Children.Add(title);
        var swatches = new WrapPanel { Name = "builder-accent" };
        foreach (var c in new[] { "#2F86F6", "#C8A04A", "#22B35E", "#E5484D", "#7C4DFF", "#1B2333" })
        {
            var col = c;
            var sw = new Button { Name = "builder-accent-" + c.TrimStart('#'), Width = 26, Height = 26, Margin = new Thickness(0, 0, 4, 4), CornerRadius = new CornerRadius(13), Background = Brush.Parse(c),
                BorderBrush = Brushes.White, BorderThickness = new Thickness(string.Equals(pg.Accent, c, StringComparison.OrdinalIgnoreCase) ? 3 : 0) };
            ToolTip.SetTip(sw, "Akzentfarbe"); sw.Click += (_, _) => BuilderChange(p => p.Accent = col);
            swatches.Children.Add(sw);
        }
        props.Children.Add(P("Akzentfarbe", Muted, 11)); props.Children.Add(swatches);

        // T-0765: diese Seite – umbenennen, sortieren, löschen
        props.Children.Add(H("Diese Seite", 13));
        var pname = new TextBox { Name = "builder-page-name", Text = screen.Name };
        var si0 = BuilderScreen;
        void CommitName() { if ((pname.Text ?? "").Trim().Length > 0 && (pname.Text ?? "").Trim() != screen.Name) BuilderRenamePage(si0, pname.Text ?? ""); }
        pname.LostFocus += (_, _) => CommitName(); OnEnter(pname, CommitName);
        props.Children.Add(P("Name in der Navigation", Muted, 11)); props.Children.Add(pname);
        var del = Act("builder-page-del", "Seite löschen", () => BuilderDeletePage(si0)); del.IsEnabled = pg.Pages.Count > 1;
        var up = Act("builder-page-up", "▲", () => BuilderMovePage(si0, -1)); up.IsEnabled = si0 > 0; ToolTip.SetTip(up, "Seite nach vorn (die erste öffnet sich zuerst)");
        var down = Act("builder-page-down", "▼", () => BuilderMovePage(si0, 1)); down.IsEnabled = si0 < pg.Pages.Count - 1; ToolTip.SetTip(down, "Seite nach hinten");
        props.Children.Add(new WrapPanel { Children = { up, new Border { Width = 4 }, down, new Border { Width = 4 }, del } });

        if (BuilderSel >= 0 && BuilderSel < blocksOf.Count)
        {
            var idx = BuilderSel; var b = blocksOf[idx];
            var label = SitePage.Kinds.FirstOrDefault(x => x.Kind == b.Kind).Label ?? b.Kind;
            props.Children.Add(H($"Baustein: {label}", 13));
            if (b.Kind != "hr")
            {
                var t = new TextBox { Name = $"builder-text-{idx}", Text = b.Text, AcceptsReturn = b.Kind is "p" or "list", TextWrapping = TextWrapping.Wrap, MinHeight = b.Kind is "p" or "list" ? 80 : 0,
                    PlaceholderText = b.Kind switch { "img" => "Beschreibung des Bildes", "stat" => pg.MeasureLabel(b), "datalist" => "Text, solange die Liste leer ist", _ => "Text" } };
                void Commit() { if ((t.Text ?? "") != BuilderBlocks(BuilderPage)[idx].Text) BuilderSetBlock(idx, x => x with { Text = t.Text ?? "" }); }
                t.LostFocus += (_, _) => Commit(); OnEnter(t, Commit);
                props.Children.Add(P(b.Kind switch { "list" => "Punkte (je Zeile einer)", "form" => "Beschriftung des Knopfs", "stat" => "Beschriftung", "datalist" => "Text bei leerer Liste", _ => "Text" }, Muted, 11)); props.Children.Add(t);
            }
            if (b.Kind == "button")
            {
                // T-0765: Ziel „zu Seite X“ oder Adresse
                var targets = new List<string> { "Adresse (Link)" }; targets.AddRange(pg.Pages.Select(s => "Seite: " + s.Name));
                var cur = b.Url.StartsWith("seite:", StringComparison.Ordinal) ? pg.PageIndex(b.Url[6..]) + 1 : 0;
                var cb = new ComboBox { Name = $"builder-target-{idx}", ItemsSource = targets, SelectedIndex = Math.Max(0, cur), HorizontalAlignment = HorizontalAlignment.Stretch };
                cb.SelectionChanged += (_, _) =>
                {
                    var k = cb.SelectedIndex; if (k < 0) return;
                    var url = k == 0 ? (b.Url.StartsWith("seite:", StringComparison.Ordinal) ? "" : b.Url) : "seite:" + BuilderPage.Pages[k - 1].Id;
                    if (url != b.Url) BuilderSetBlock(idx, x => x with { Url = url });
                };
                props.Children.Add(P("Ziel", Muted, 11)); props.Children.Add(cb);
            }
            if (b.Kind is "img" or "contact" || b.Kind == "button" && !b.Url.StartsWith("seite:", StringComparison.Ordinal))
            {
                var u = new TextBox { Name = $"builder-url-{idx}", Text = b.Url, PlaceholderText = b.Kind == "contact" ? "deine E-Mail-Adresse" : b.Kind == "img" ? "Bilddatei im Baukasten-Ordner oder https-Adresse" : "https://…" };
                void CommitUrl() { if ((u.Text ?? "") != BuilderBlocks(BuilderPage)[idx].Url) BuilderSetBlock(idx, x => x with { Url = u.Text ?? "" }); }
                u.LostFocus += (_, _) => CommitUrl(); OnEnter(u, CommitUrl);
                props.Children.Add(P(b.Kind == "contact" ? "E-Mail" : b.Kind == "img" ? "Bild" : "Link", Muted, 11)); props.Children.Add(u);
            }
            if (SitePage.IsDataKind(b.Kind) && pg.Collections.Count > 0)
            {
                var names = pg.Collections.Select(c => c.Name).ToList();
                var cb = new ComboBox { Name = $"builder-coll-{idx}", ItemsSource = names, SelectedIndex = Math.Max(0, pg.Collections.FindIndex(c => c.Id == b.Data)), HorizontalAlignment = HorizontalAlignment.Stretch };
                cb.SelectionChanged += (_, _) => { if (cb.SelectedIndex >= 0 && BuilderPage.Collections[cb.SelectedIndex].Id != b.Data) { var id = BuilderPage.Collections[cb.SelectedIndex].Id; BuilderSetBlock(idx, x => x with { Data = id, Url = x.Kind == "stat" ? "anzahl" : x.Url }); } };
                props.Children.Add(P("Datensammlung", Muted, 11)); props.Children.Add(cb);
                if (b.Kind == "stat" && pg.Collection(b.Data) is { } sc)
                {
                    var opts = new List<(string Label, string Url)> { ("Anzahl der Einträge", "anzahl") };
                    opts.AddRange(sc.Fields.Where(f => f.Type == "zahl").Select(f => ("Summe von „" + f.Label + "“", "summe:" + f.Id)));
                    var mb = new ComboBox { Name = $"builder-measure-{idx}", ItemsSource = opts.Select(o => o.Label).ToList(), SelectedIndex = Math.Max(0, opts.FindIndex(o => o.Url == b.Url)), HorizontalAlignment = HorizontalAlignment.Stretch };
                    mb.SelectionChanged += (_, _) => { if (mb.SelectedIndex >= 0 && opts[mb.SelectedIndex].Url != b.Url) { var url = opts[mb.SelectedIndex].Url; BuilderSetBlock(idx, x => x with { Url = url }); } };
                    props.Children.Add(P("Auswertung", Muted, 11)); props.Children.Add(mb);
                    if (opts.Count == 1) props.Children.Add(P("Für eine Summe braucht die Sammlung ein Zahlenfeld (unten bei „Daten“).", Muted, 11));
                }
            }
            props.Children.Add(new WrapPanel { Children = {
                Act($"builder-up-{idx}", "▲", () => { if (idx > 0) BuilderMove(idx, idx - 1); }), new Border { Width = 4 },
                Act($"builder-down-{idx}", "▼", () => { if (idx < BuilderBlocks(BuilderPage).Count - 1) BuilderMove(idx, idx + 1); }), new Border { Width = 4 },
                Act($"builder-dup-{idx}", "Verdoppeln", () => { BuilderSel = idx + 1; BuilderChange(p => BuilderBlocks(p).Insert(idx + 1, BuilderBlocks(p)[idx])); }), new Border { Width = 4 },
                Act($"builder-del-{idx}", "Entfernen", () => { BuilderSel = Math.Min(idx, BuilderBlocks(BuilderPage).Count - 2); BuilderChange(p => BuilderBlocks(p).RemoveAt(idx)); }) } });
        }
        else props.Children.Add(P("Tippe einen Baustein in der Vorschau an, um ihn einzustellen – Doppelklick bearbeitet den Text direkt.", Muted, 11.5));

        // T-0765: Daten – Sammlungen und ihre Felder
        props.Children.Add(H("Daten", 13));
        if (pg.Collections.Count > 0) props.Children.Add(new TextBlock { Name = "builder-speicher-hinweis", Text = SitePage.StorageNote, Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap }); // Gegenprüfung N3
        if (pg.Collections.Count == 0) props.Children.Add(P("Noch keine Datensammlung. Sie entsteht mit dem ersten Formular, der Datenliste oder der Auswertung – oder hier:", Muted, 11));
        for (var ci = 0; ci < pg.Collections.Count; ci++)
        {
            var c = pg.Collections[ci]; var cid = c.Id;
            var cname = new TextBox { Name = $"builder-coll-name-{ci}", Text = c.Name, FontWeight = FontWeight.SemiBold };
            void CommitC() { var n = (cname.Text ?? "").Trim(); if (n.Length > 0 && n != c.Name) BuilderCollection(cid, x => x.Name = n); }
            cname.LostFocus += (_, _) => CommitC(); OnEnter(cname, CommitC);
            props.Children.Add(cname);
            for (var k = 0; k < c.Fields.Count; k++)
            {
                var fk = k; var f = c.Fields[k];
                var fl = new TextBox { Name = $"builder-field-{ci}-{k}", Text = f.Label, MinWidth = 0 };
                void CommitF() { var n = (fl.Text ?? "").Trim(); if (n.Length > 0) BuilderSetField(cid, fk, x => x with { Label = n }); }
                fl.LostFocus += (_, _) => CommitF(); OnEnter(fl, CommitF);
                var ty = new Button { Name = $"builder-fieldtype-{ci}-{k}", Content = f.Type == "zahl" ? "Zahl" : "Text", Padding = new Thickness(6, 3), Margin = new Thickness(4, 0, 0, 0) };
                ToolTip.SetTip(ty, "Feldart wechseln (Text oder Zahl)"); ty.Click += (_, _) => BuilderSetField(cid, fk, x => x with { Type = x.Type == "zahl" ? "text" : "zahl" });
                var rq = new CheckBox { Name = $"builder-fieldreq-{ci}-{k}", Content = "Pflicht", IsChecked = f.Required, Margin = new Thickness(4, 0, 0, 0), Foreground = Text };
                rq.IsCheckedChanged += (_, _) => BuilderSetField(cid, fk, x => x with { Required = rq.IsChecked == true });
                var rm = new Button { Name = $"builder-fielddel-{ci}-{k}", Content = "✕", Padding = new Thickness(6, 3), Margin = new Thickness(4, 0, 0, 0), IsEnabled = c.Fields.Count > 1 };
                ToolTip.SetTip(rm, "Feld entfernen"); rm.Click += (_, _) => BuilderRemoveField(cid, fk);
                // Name des Felds über die ganze Breite, darunter Art, Pflicht, Entfernen (nebeneinander wurde das Namensfeld in der schmalen Spalte zu eng)
                props.Children.Add(new StackPanel { Spacing = 3, Margin = new Thickness(0, 2, 0, 4), Children = { fl, new StackPanel { Orientation = Orientation.Horizontal, Children = { ty, rq, rm } } } });
            }
            props.Children.Add(Act($"builder-field-add-{ci}", "+ Feld", () => BuilderAddField(cid, "Feld " + (c.Fields.Count + 1))));
        }
        props.Children.Add(Act("builder-coll-add", "+ Datensammlung", () => BuilderAddCollection("Sammlung " + (BuilderPage.Collections.Count + 1), "Eintrag*")));

        Border Col(Control c) => new() { CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Padding = new Thickness(10), VerticalAlignment = VerticalAlignment.Top, Child = c };
        var grid = new Grid { Name = "builder-designer", ColumnDefinitions = new ColumnDefinitions("200,12,*,12,280"), Children = { Col(palette), Pos(center, 2), Pos(Col(props), 4) } };
        grid.KeyDown += (_, e) =>
        {
            if (e.Source is TextBox) return; // im Feld bleibt Strg+Z das zeichenweise Rückgängig des Felds
            if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control) { BuilderUndo(); e.Handled = true; }
            else if (e.Key == Key.Y && e.KeyModifiers == KeyModifiers.Control || e.Key == Key.Z && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift)) { BuilderRedo(); e.Handled = true; }
            else if (e.Key == Key.F2 && BuilderSel >= 0) { BuilderEditInline(BuilderSel); e.Handled = true; } // T-0765
            else if (e.Key == Key.Delete && BuilderSel >= 0 && BuilderSel < BuilderBlocks(BuilderPage).Count) { var i = BuilderSel; BuilderSel = Math.Min(i, BuilderBlocks(BuilderPage).Count - 2); BuilderChange(p => BuilderBlocks(p).RemoveAt(i)); e.Handled = true; }
        };
        // T-0765: exportierte ZIP oder index.html auf die Arbeitsfläche ziehen = wieder öffnen
        DragDrop.SetAllowDrop(grid, true);
        static bool Openable(string f) => f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".html", StringComparison.OrdinalIgnoreCase);
        grid.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = BueroDropFiles(e).Any(Openable) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; });
        grid.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            e.Handled = true;
            if (BueroDropFiles(e).FirstOrDefault(Openable) is { } f) Try(V, () => BuilderOpenExport(f));
            else Say("Hier lässt sich eine exportierte Baukasten-App (ZIP oder index.html) ablegen.", V);
        });

        var head = new StackPanel { Spacing = 0 };
        if (LinkRecoveryCard(link) is { } card) head.Children.Add(card);
        if (LinkMsg(link) is { } msg) head.Children.Add(msg);
        if (_builderProblem is { } problem) head.Children.Add(P("⚠ " + problem, Brush.Parse("#F5A524"), 12));
        if (BuilderFresh)
        {
            // T-0765: Leerzustand – der erste Schritt ist sichtbar (Vorlage aus der Beispielliste oder leer anfangen)
            var start = SitePage.Templates[0];
            head.Children.Add(new Border { Name = "builder-first", Background = Brush.Parse("#1AC8A04A"), BorderBrush = Brush.Parse("#66C8A04A"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10), Margin = new Thickness(0, 0, 0, 8),
                Child = new DockPanel { Children = {
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, [DockPanel.DockProperty] = Avalonia.Controls.Dock.Right, Children = {
                        Act("builder-tpl-" + start.Id, "Vorlage „" + start.Name + "“ nehmen", () => BuilderUseTemplate(start.Id), true),
                        Act("builder-start-empty", "Leer anfangen", () => { BuilderFresh = false; PersistBuilder(); Show(V); }) } },
                    new StackPanel { Spacing = 2, Children = {
                        new TextBlock { Text = "Erster Schritt: Nimm eine Vorlage – oder fang mit der leeren Seite unten an.", Foreground = Text, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                        new TextBlock { Text = start.Name + ": " + start.Hint, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap } } } } } });
        }
        var intro = new StackPanel { Spacing = 2, Children = {
            P("Bau deine kleine App aus Bausteinen – ohne Programmieren: mehrere Seiten, Formulare, Listen und Auswertungen. Die Vorschau zeigt sofort, wie es aussieht."),
            new WrapPanel { Children = {
                new TextBlock { Name = "builder-abgrenzung", Text = "Baukasten = einfache Apps ohne Code. Für Profi-Webseiten mit eigenem Design gibt es das Web-Design-Studio.", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap },
                new Border { Width = 8 }, Act("builder-to-webdesign", "Zum Web-Design-Studio", () => Show("dev")) } } } };
        return LiveBind(link, KreativPage("Baukasten", new StackPanel { Spacing = 8, Children = { head, intro, grid } }));
    }

    Control BuilderImage(Block b)
    {
        var local = b.Url.Length > 0 && !b.Url.Contains(':') && FreeAssets.SafeRel(b.Url) ? Path.Combine(BuilderFolder, b.Url) : null;
        if (local is not null && File.Exists(local))
            try { using var s = File.OpenRead(local); return new Image { Source = Avalonia.Media.Imaging.Bitmap.DecodeToWidth(s, 720), MaxHeight = 260, Stretch = Stretch.Uniform }; }
            catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or NotSupportedException or NullReferenceException) { }
        return new Border { Height = 140, Background = Brush.Parse("#EEF1F6"), CornerRadius = new CornerRadius(8),
            Child = new TextBlock { Text = "" + (b.Text.Length > 0 ? b.Text : "Bild") + (b.Url.StartsWith("https://", StringComparison.Ordinal) ? "\n(Bild aus dem Internet – im Browser sichtbar)" : ""), Foreground = Brush.Parse("#8A94A6"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center } };
    }
}
