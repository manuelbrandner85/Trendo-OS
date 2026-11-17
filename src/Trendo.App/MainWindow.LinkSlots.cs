using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>T-0711 (B6 „Programm-Hülle für alle“, Etappe 1b): weitere Programme außerhalb der Hülle an Rückgängig und Notsicherung der Hülle anschließen –
/// derselbe Weg wie bei Briefen (<see cref="ProgramLink"/>, kein zweites System), nur einmal allgemein geschrieben: Jedes Programm meldet, wie sein Stand
/// als JSON aussieht (<see cref="LinkSlot.Get"/>) und wie es einen Stand übernimmt (<see cref="LinkSlot.Apply"/>). Speichern bleibt im eigenen Format des Programms.
/// Rückgängig: jeder Neuaufbau der Seite nach einer Aktion und jede Tipp-Pause ist ein Schritt (bis 100). Notsicherung: im Takt der Hülle (20 s), atomar,
/// für dieses Fenster gesperrt (<see cref="ProgramLocks"/>). Beim nächsten Öffnen zeigt das Programm eine Wiederherstellen-Karte.</summary>
public partial class MainWindow
{
    /// <summary>Ein angeschlossenes Programm (bzw. ein Dokument darin).</summary>
    public sealed class LinkSlot
    {
        public required string Key { get; init; }
        /// <summary>Seite, die nach Rückgängig/Zurückholen neu gezeichnet wird.</summary>
        public required string Page { get; init; }
        /// <summary>Vorsilbe der Knopf-Namen: „&lt;prefix&gt;-undo“, „-redo“, „-recovery“, „-restore-&lt;n&gt;“, „-discard-&lt;n&gt;“.</summary>
        public required string Prefix { get; init; }
        /// <summary>Was gesichert wurde, für die Karte („Eine E-Mail …“): Einzahl und Mehrzahl.</summary>
        public required string One { get; init; }
        public required string Many { get; init; }
        public required ProgramLink Link { get; init; }
        public required Func<JsonNode?> Get { get; init; }
        public required Action<JsonNode?> Apply { get; init; }
        public required Func<bool> Worth { get; init; }
        /// <summary>Titel für die Karte (aus dem Stand).</summary>
        public Func<string>? Title { get; init; }
        /// <summary>T-0716: Übernahme eines zurückgeholten Stands, wenn sie anders ist als Rückgängig (sofort speichernde Programme: nur die getippten Eingaben,
        /// die gespeicherten Daten bleiben). Fehlt: <see cref="Apply"/>.</summary>
        public Action<JsonNode?>? Recovered { get; init; }
        /// <summary>T-0716: Hinweis nach dem Zurückholen (Standard „Wiederhergestellt – bitte speichern.“).</summary>
        public string? RecoveredMsg { get; init; }
        /// <summary>Für Formulare: der laufende Stand, solange er nicht gespeichert ist (Felder werden daraus gefüllt).</summary>
        public JsonObject? Live { get; set; }
        public DispatcherTimer? Typing;
        public string Msg = "";
        public Action? Refresh;
    }

    readonly Dictionary<string, LinkSlot> _slots = [];

    // ───── T-0711: Formular-Programme (Entwurf je bearbeitetem Element) ─────
    /// <summary>Recherche: Notizfeld.</summary>
    public LinkSlot ResearchLink => FormSlot("recherche", "LINK-RECHERCHE", "recherche", "rs-note", "Eine Recherche-Notiz", "Recherche-Notizen", _ => { });
    /// <summary>Wissen für die KI: Seiten-Editor („neu“ = neue Seite).</summary>
    public LinkSlot WikiLink => FormSlot("ki-wissen", "LINK-WISSEN", "ki-wissen", "wissen", "Eine Wissens-Seite", "Wissens-Seiten",
        id => { _wikiId = id == "neu" ? null : id; _wikiEdit = true; _wikiTrashView = false; _wikiWeek = false; _wikiGraph = false; _wikiDelArmed = false; });
    /// <summary>KI-Vorlagen: Vorlagen-Editor („neu“ = neue Vorlage).</summary>
    public LinkSlot PromptLink => FormSlot("ki-vorlagen", "LINK-VORLAGEN", "ki-vorlagen", "pedit", "Eine KI-Vorlage", "KI-Vorlagen",
        id => { _promptSel = id == "neu" ? null : id; _promptEdit = true; });
    /// <summary>Skills: Skill-Editor („neu“ = neuer Skill).</summary>
    public LinkSlot SkillLink => FormSlot("ki-skills", "LINK-SKILLS", "ki-skills", "skill", "Ein Skill", "Skills",
        id => { _skillSel = id == "neu" ? null : id; _skillEdit = true; });
    /// <summary>Sauberer Nachbau: Beschreibung (BESCHREIBUNG.md) je Projektordner.</summary>
    public LinkSlot CleanLink => FormSlot("nachbau", "LINK-NACHBAU", "nachbau", "clean", "Eine Nachbau-Beschreibung", "Nachbau-Beschreibungen",
        id => { if (Directory.Exists(id)) { _cleanOpen = id; _cleanSims = null; } });

    /// <summary>Zeile Rückgängig · Wiederholen (mit Hinweis) für ein Formular, ohne Karte (die steht oben auf der Seite).</summary>
    Control LinkUndoRow(LinkSlot s)
    {
        var (u, r) = LinkButtons(s);
        var sp = new StackPanel { Spacing = 0 };
        if (LinkMsg(s) is { } m) sp.Children.Add(m);
        sp.Children.Add(new WrapPanel { Name = s.Prefix + "-link", Children = { u, r } });
        return sp;
    }
    /// <summary>Für Tests: alle angeschlossenen Programme dieses Fensters.</summary>
    public IReadOnlyDictionary<string, LinkSlot> LinkSlots => _slots;

    LinkSlot Slot(string key, string studio, string format, string page, string prefix, string one, string many, Func<JsonNode?> get, Action<JsonNode?> apply, Func<bool> worth, Func<string>? title = null)
    {
        if (_slots.TryGetValue(key, out var have)) return have;
        var s = new LinkSlot { Key = key, Page = page, Prefix = prefix, One = one, Many = many, Get = get, Apply = apply, Worth = worth, Title = title,
            Link = new ProgramLink(studio, format, get(), one) { Locks = ProgLocks } };
        return _slots[key] = s;
    }

    /// <summary>Formular-Programm: Stand ist ein JSON-Objekt mit „id“ (welches Element bearbeitet wird) und den Feldern.</summary>
    LinkSlot FormSlot(string key, string studio, string page, string prefix, string one, string many, Action<string> open)
    {
        LinkSlot? me = null;
        me = Slot(key, studio, key, page, prefix, one, many,
            () => me?.Live?.DeepClone(),
            st => { me!.Live = st as JsonObject; if (me.Live?["id"]?.GetValue<string>() is { } id) open(id); },
            () => me?.Live is { } o && o.Where(kv => kv.Key != "id").Any(kv => kv.Value is JsonValue v && v.TryGetValue<string>(out var t) && t.Trim().Length > 0),
            () => me?.Live is { } o ? o.Where(kv => kv.Key != "id").Select(kv => kv.Value?.ToString().Trim() ?? "").FirstOrDefault(t => t.Length > 0) ?? one : one);
        return me;
    }

    static string LinkShort(string t) { t = t.ReplaceLineEndings(" ").Trim(); return t.Length > 60 ? t[..60] + "…" : t; }

    /// <summary>Formular an seinen Entwurf binden: Gehört der Entwurf zu einem anderen Element (oder gibt es keinen), beginnt ein neuer – ein ungespeicherter alter
    /// bleibt dabei als Sicherung zum Zurückholen liegen. Sonst werden die Felder aus dem Entwurf gefüllt (nichts geht beim Neuzeichnen verloren).
    /// Jede Eingabe wird nach einer Tipp-Pause ein Rückgängig-Schritt.</summary>
    void LinkForm(LinkSlot s, string id, params (string Key, TextBox Box)[] fields) =>
        LinkFormFields(s, id, fields.Select(f => new LinkField(f.Key, () => f.Box.Text ?? "", v => f.Box.Text = v, h => f.Box.TextChanged += (_, _) => h())).ToArray());

    /// <summary>T-0714: ein Feld eines Formulars, gleich welcher Art (Textfeld, Ankreuzfeld „ja“/„nein“ …): lesen, setzen, bei Änderung melden.</summary>
    sealed record LinkField(string Key, Func<string> Read, Action<string> Write, Action<Action> OnChange)
    {
        public static LinkField Of(string key, CheckBox cb) => new(key, () => cb.IsChecked == true ? "ja" : "nein", v => cb.IsChecked = v == "ja", h => cb.IsCheckedChanged += (_, _) => h());
        public static LinkField Of(string key, TextBox tb) => new(key, () => tb.Text ?? "", v => tb.Text = v, h => tb.TextChanged += (_, _) => h());
    }

    /// <summary>Wie <see cref="LinkForm"/>, für beliebige Felder (T-0714, z. B. PDF-Formulare mit Ankreuzfeldern).</summary>
    void LinkFormFields(LinkSlot s, string id, LinkField[] fields)
    {
        if (s.Live?["id"]?.GetValue<string>() != id)
        {
            if (s.Live is not null && s.Link.Dirty) { LinkCommit(s); try { s.Link.Autosave(ProgBackup, s.Worth()); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
            var o = new JsonObject { ["id"] = id }; foreach (var f in fields) o[f.Key] = f.Read();
            s.Live = o; s.Typing?.Stop(); s.Link.Reset(s.Get(), s.Title?.Invoke()); // neuer Verlauf; die alte Sicherung (falls ungespeichert) bleibt
        }
        else foreach (var f in fields) f.Write(s.Live[f.Key]?.GetValue<string>() ?? "");
        foreach (var f in fields)
        {
            var fld = f;
            fld.OnChange(() => { if (s.Live is null || s.Live[fld.Key]?.GetValue<string>() == fld.Read()) return; s.Live[fld.Key] = fld.Read(); LinkTyped(s); });
        }
        LinkCommit(s);
    }

    /// <summary>Jetzigen Stand als Schritt übernehmen (gleicher Stand: nichts).</summary>
    bool LinkCommit(LinkSlot s, string label = "Geändert")
    {
        s.Typing?.Stop();
        if (s.Title?.Invoke() is { Length: > 0 } t) s.Link.Doc.Title = LinkShort(t);
        return s.Link.Commit(label, s.Get());
    }

    /// <summary>Getippt: nach <see cref="LinkTypingDelay"/> Pause wird daraus ein Rückgängig-Schritt.</summary>
    void LinkTyped(LinkSlot s)
    {
        if (s.Typing?.IsEnabled != true && JsonNode.DeepEquals(s.Get(), s.Link.State)) return; // nur Neuzeichnen, nichts geändert
        if (s.Typing is null) { s.Typing = new DispatcherTimer { Interval = LinkTypingDelay }; s.Typing.Tick += (_, _) => { LinkCommit(s, "Text geschrieben"); s.Refresh?.Invoke(); }; }
        s.Typing.Stop(); s.Typing.Interval = LinkTypingDelay; s.Typing.Start();
        s.Refresh?.Invoke();
    }

    public bool LinkUndo(LinkSlot s) { LinkCommit(s, "Text geschrieben"); if (s.Link.Undo() is not { } st) return false; s.Apply(st); return true; }
    public bool LinkRedo(LinkSlot s) { LinkCommit(s, "Text geschrieben"); if (s.Link.Redo() is not { } st) return false; s.Apply(st); return true; }

    /// <summary>Neu angefangen oder frisch geöffnet: neuer Verlauf, gilt als gespeichert, die eigene Notsicherung entfällt.</summary>
    void LinkReset(LinkSlot s) { s.Typing?.Stop(); s.Link.Reset(s.Get(), s.Title?.Invoke(), ProgBackup); }
    /// <summary>Im eigenen Format gespeichert: Notsicherung weg.</summary>
    void LinkSaved(LinkSlot s) { LinkCommit(s); s.Link.Saved(ProgBackup); }
    /// <summary>Formular gespeichert oder bewusst abgebrochen: Entwurf und Sicherung weg.</summary>
    void LinkClose(LinkSlot s) { s.Typing?.Stop(); s.Live = null; s.Link.Reset(null, null, ProgBackup); }

    /// <summary>Takt der Hülle: jedes angeschlossene Programm übernimmt Getipptes als Schritt und sichert, was ungespeichert und etwas wert ist.</summary>
    void SlotsAutosave()
    {
        foreach (var s in _slots.Values)
            try { LinkCommit(s, "Text geschrieben"); s.Link.Autosave(ProgBackup, s.Worth()); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public List<(string File, string Title, DateTime When)> LinkRecoveries(LinkSlot s) => s.Link.Pending(ProgBackup);

    /// <summary>Gesicherten Stand zurückholen (der offene geht dabei nicht verloren – er bleibt als Sicherung) oder die Sicherung verwerfen.</summary>
    public void LinkRecover(LinkSlot s, int index, bool restore)
    {
        var all = LinkRecoveries(s); if (index < 0 || index >= all.Count) return;
        var f = all[index].File;
        if (!restore) { ProgramLink.Discard(f, ProgLocks); s.Msg = "Sicherung verworfen."; return; }
        LinkCommit(s); s.Link.Autosave(ProgBackup, s.Worth());
        (s.Recovered ?? s.Apply)(s.Link.Restore(f, ProgBackup));
        s.Msg = s.RecoveredMsg ?? "Wiederhergestellt – bitte speichern.";
        _core.Audit.Append(Principal.LocalOwner.Name, "link.recover", s.Key + " " + all[index].Title);
    }

    /// <summary>Rückgängig und Wiederholen als Knöpfe (Namen „&lt;prefix&gt;-undo“/„-redo“), richtig an- und ausgeschaltet.</summary>
    (Button Undo, Button Redo) LinkButtons(LinkSlot s, string? undoName = null, string? redoName = null)
    {
        Button B(string n, string label, string icon, string tip)
        {
            var b = new Button { Name = n, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 6, 6),
                Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Ico(icon, 14, Text), new TextBlock { Text = label, Foreground = Text, VerticalAlignment = VerticalAlignment.Center } } } };
            ToolTip.SetTip(b, tip); Avalonia.Automation.AutomationProperties.SetName(b, label); return b;
        }
        var undo = B(undoName ?? s.Prefix + "-undo", "Rückgängig", Mdi.Undo, "Rückgängig (bis 100 Schritte)"); var redo = B(redoName ?? s.Prefix + "-redo", "Wiederholen", Mdi.Redo, "Wiederholen");
        undo.Click += (_, _) => { if (LinkUndo(s)) s.Msg = "Rückgängig gemacht."; Show(s.Page); };
        redo.Click += (_, _) => { if (LinkRedo(s)) s.Msg = "Wiederholt."; Show(s.Page); };
        s.Refresh = () => { undo.IsEnabled = s.Link.CanUndo || s.Typing?.IsEnabled == true; redo.IsEnabled = s.Link.CanRedo; };
        s.Refresh();
        return (undo, redo);
    }

    /// <summary>Hinweis nach Rückgängig/Zurückholen (einmal anzeigen).</summary>
    TextBlock? LinkMsg(LinkSlot s)
    {
        if (s.Msg.Length == 0) return null;
        var t = new TextBlock { Name = s.Prefix + "-link-msg", Text = s.Msg, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
        s.Msg = ""; return t;
    }

    /// <summary>Wiederherstellen-Karte: nicht gespeicherte Stände aus einer früheren Sitzung oder einem Absturz (höchstens drei).</summary>
    Control? LinkRecoveryCard(LinkSlot s)
    {
        List<(string File, string Title, DateTime When)> all;
        try { all = LinkRecoveries(s); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
        return RecoveryCardView(s.Prefix + "-recovery", (k, yes) => $"{s.Prefix}-{(yes ? "restore" : "discard")}-{k}", s.One, s.Many, all, (k, yes) =>
        {
            if (yes) { try { LinkRecover(s, k, true); } catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { s.Msg = "Zurückholen ging nicht: " + FriendlyError.Say(ex); } }
            else LinkRecover(s, k, false);
            Show(s.Page);
        });
    }

    /// <summary>T-0714: eine Karte für alle angeschlossenen Programme (vorher je Weg eigener Aufbau): Überschrift, je Stand Titel und Zeit, „Wiederherstellen“ / „Verwerfen“.
    /// <paramref name="buttonName"/>(n, true) = Name von „Wiederherstellen“, (n, false) = „Verwerfen“; <paramref name="decide"/>(n, wiederherstellen?).</summary>
    Control? RecoveryCardView(string cardName, Func<int, bool, string> buttonName, string one, string many, List<(string File, string Title, DateTime When)> all, Action<int, bool> decide)
    {
        if (all.Count == 0) return null;
        var rows = new StackPanel { Spacing = 6 };
        rows.Children.Add(new TextBlock { Text = all.Count == 1 ? $"{one} wurde nicht gespeichert, aber automatisch gesichert." : $"{all.Count} {many} wurden nicht gespeichert, aber automatisch gesichert.", Foreground = Text, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        for (var i = 0; i < Math.Min(3, all.Count); i++)
        {
            var k = i; var o = all[i];
            var yes = new Button { Name = buttonName(k, true), Content = "Wiederherstellen", Padding = new Thickness(12, 4), Margin = new Thickness(0, 0, 6, 0) };
            yes.Click += (_, _) => decide(k, true);
            var no = new Button { Name = buttonName(k, false), Content = "Verwerfen", Padding = new Thickness(12, 4) };
            no.Click += (_, _) => decide(k, false);
            rows.Children.Add(new DockPanel { Children = {
                new StackPanel { Orientation = Orientation.Horizontal, [DockPanel.DockProperty] = Avalonia.Controls.Dock.Right, Children = { yes, no } },
                new TextBlock { Text = $"„{o.Title}“ · gesichert {o.When:dd.MM.yyyy, HH:mm} Uhr", Foreground = Text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center } } });
        }
        return new Border { Name = cardName, Background = Brush.Parse("#1AF5A524"), BorderBrush = Brush.Parse("#66F5A524"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8), Margin = new Thickness(0, 0, 0, 8), Child = rows };
    }

    /// <summary>Alles zusammen für eine Seite: Karte (falls nötig), Hinweis und die Zeile Rückgängig · Wiederholen. Übernimmt vorher den jetzigen Stand als Schritt.</summary>
    Control LinkBar(LinkSlot s, params Control[] more)
    {
        LinkCommit(s);
        var sp = new StackPanel { Name = s.Prefix + "-link", Spacing = 0 };
        if (LinkRecoveryCard(s) is { } card) sp.Children.Add(card);
        if (LinkMsg(s) is { } msg) sp.Children.Add(msg);
        var (u, r) = LinkButtons(s);
        var row = new WrapPanel { Orientation = Orientation.Horizontal, Children = { u, r } };
        foreach (var m in more) row.Children.Add(m);
        sp.Children.Add(row);
        return sp;
    }
}
