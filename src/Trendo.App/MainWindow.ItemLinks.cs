using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Programs;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>T-0717 (B6 „Programm-Hülle für alle“, Etappe E5 KI &amp; Wissen, dazu der offene Punkt aus E2):
/// <list type="bullet">
/// <item>Programme, die jede Änderung sofort selbst speichern – Aufgaben, Kalender, Vorhaben (Zeitplan), Abläufe (Bausteine), Produktion (Tafel) –,
/// hatten je einen kleinen eigenen Rückgängig-Stapel (ohne Grenze bzw. 50, ohne Wiederholen) oder gar keinen (Tafel). Jetzt: Rückgängig/Wiederholen der Hülle
/// (<see cref="ItemUndo"/> über <see cref="ProgramLink"/>, 100 Schritte), als <see cref="LinkSlot"/> angeschlossen – damit auch „Bearbeiten“ in der Menüleiste (T-0710).
/// Eine Notsicherung braucht es dort nicht: Es gibt keinen ungespeicherten Stand.</item>
/// <item>Entwürfe, die bisher nur im Speicher lagen – neuer Ablauf, Angaben eines Produktions-Ablaufs, neue Lernkarte, Auftrag und neue Rolle der KI-Rollen,
/// Nachricht an die KI –, hängen als Formular an der Notsicherung der Hülle (20-s-Takt, atomar, Sperre gegen ein zweites Fenster, Wiederherstellen-Karte).</item>
/// </list></summary>
public partial class MainWindow
{
    readonly Dictionary<string, ItemUndo> _items = [];

    /// <summary>Programm mit sofortigem Speichern: Rückgängig der Hülle über die geänderten Elemente. <paramref name="apply"/>(Kennung, Stand oder null = löschen).</summary>
    LinkSlot ItemSlot(string key, string page, string prefix, string one, string many, Action<string, JsonNode?> apply)
    {
        if (_slots.TryGetValue(key, out var have)) return have;
        var u = _items[key] = new ItemUndo("LINK-" + key.ToUpperInvariant(), key);
        return _slots[key] = new LinkSlot
        {
            Key = key, Page = page, Prefix = prefix, One = one, Many = many, Link = u.Link,
            Get = () => u.Link.State, // nichts Getipptes: der Stand ist immer der zuletzt festgehaltene
            Apply = st => { foreach (var (k, v) in u.Changes(st)) apply(k, v); },
            Worth = () => false, // alles ist schon gespeichert – keine Notsicherung
        };
    }

    ItemUndo Items(LinkSlot s) => _items[s.Key];

    static JsonNode? Node<T>(T? v) where T : class => v is null ? null : JsonSerializer.SerializeToNode(v);

    /// <summary>Einen Schritt zurück bzw. vor. Meldung für die Seite; false = es gab nichts.</summary>
    bool ItemStep(LinkSlot s, bool back, out string msg)
    {
        try
        {
            var ok = back ? LinkUndo(s) : LinkRedo(s);
            msg = ok ? back ? "Rückgängig gemacht." : "Wiederholt." : back ? "Es gibt nichts rückgängig zu machen." : "Es gibt nichts zu wiederholen.";
            return ok;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException or ArgumentException)
        {
            msg = "Das ging nicht: " + FriendlyError.Say(e); return false;
        }
    }

    /// <summary>Knopf „Wiederholen“ (Name „&lt;prefix&gt;-redo“) im Stil des vorhandenen Rückgängig-Knopfs der Seite.</summary>
    Button ItemRedoButton(LinkSlot s, Action redo, string page)
    {
        var b = new Button { Name = s.Prefix + "-redo", Content = "Wiederholen", IsEnabled = s.Link.CanRedo, Margin = new Thickness(4, 0, 0, 0) };
        ToolTip.SetTip(b, "Wiederholen (Strg+Y)");
        b.Click += (_, _) => { redo(); Show(page); };
        return b;
    }

    /// <summary>Strg+Z / Strg+Y (auch Strg+Umschalt+Z) auf einer Seite.</summary>
    static void ItemKeys(Control c, Action undo, Action redo, Action show)
    {
        c.KeyDown += (_, e) =>
        {
            if (e.KeyModifiers == Avalonia.Input.KeyModifiers.Control && e.Key == Avalonia.Input.Key.Z) { undo(); show(); e.Handled = true; }
            else if (e.KeyModifiers == Avalonia.Input.KeyModifiers.Control && e.Key == Avalonia.Input.Key.Y
                  || e.KeyModifiers == (Avalonia.Input.KeyModifiers.Control | Avalonia.Input.KeyModifiers.Shift) && e.Key == Avalonia.Input.Key.Z) { redo(); show(); e.Handled = true; }
        };
    }

    // ───── Aufgaben, Kalender (aus E2 offen: eigene Stapel _todoUndoUid/_todoUndoBefore ohne Grenze, _calUndo 50) ─────
    public LinkSlot TodoLink => ItemSlot("aufgaben", "aufgaben", "todo", "Eine Aufgabe", "Aufgaben",
        (uid, v) => { if (v is null) Cal.DeleteTodo(uid); else Cal.AddTodo(v.Deserialize<TodoItem>()!); });
    public LinkSlot CalLink => ItemSlot("kalender", "kalender", "cal", "Ein Termin", "Termine",
        (uid, v) => { if (v is null) { Cal.Delete(uid); if (CalSelected == uid) CalSelected = null; } else { Cal.Add(v.Deserialize<CalEvent>()!); CalSelected = uid; } });

    // ───── Vorhaben: Zeitplan (aus E2 offen: _planUndo, Datei-Text ohne Grenze) ─────
    public LinkSlot PlanLink => ItemSlot("vorhaben", "vorhaben", "suite-plan", "Ein Zeitplan", "Zeitpläne",
        (file, v) => { if (v is not null) AtomicFile.WriteAllText(file, v.GetValue<string>()); });

    // ───── Abläufe: Bausteine gespeicherter Abläufe (vorher _wfUndo, 50) ─────
    public LinkSlot WfLink => ItemSlot("ablaeufe", "ablaeufe", "wf", "Ein Ablauf", "Abläufe",
        (id, v) =>
        {
            // T-0742: ein von der KI angelegter Ablauf hat „vorher“ = nichts – Rückgängig entfernt ihn (samt Windows-Aufgabe), Wiederholen legt ihn wieder an (aus)
            if (v is null)
            {
                if (Workflows.Get(id) is not { } gone) return;
                if (gone.WinTask is not null) WfRemoveTask(gone);
                Workflows.Remove(id); if (WfSelected == id) WfSelected = null; WfNode = -1; return;
            }
            var w = Workflows.Get(id) ?? Workflows.Add(new Workflow { Id = id, Enabled = false });
            WfRestore(w, v.GetValue<string>()); Workflows.Update(w); WfSelected = id; WfNode = Math.Min(WfNode, w.Steps.Count - 1);
        });

    // ───── Produktion: Tafel eines Großprojekts (vorher ohne Rückgängig) ─────
    public LinkSlot BigLink => ItemSlot("produktion", "produktion", "big", "Eine Tafel", "Tafeln",
        (pid, v) =>
        {
            if (v is null || !_core.Projects.List().Any(p => p.Id == pid)) return;
            var to = JsonSerializer.Deserialize<BigBoard>(v.GetValue<string>()) ?? new();
            BigBoardHandoff.Reconcile(_core.Projects, pid, BigBoards.Load(_core.Projects, pid), to); // T-0743: Übergaben und Dateien gehen mit zurück bzw. wieder vor
            BigBoards.Save(_core.Projects, pid, to);
        });

    // ───── Entwürfe (Formulare an der Notsicherung der Hülle) ─────
    /// <summary>Abläufe: neuer Ablauf (Name, Auslöser, Zeit/Ordner, Schritte).</summary>
    public LinkSlot WfDraftLink => FormSlot("ablaeufe-neu", "LINK-ABLAEUFE", "ablaeufe", "wfe", "Ein neuer Ablauf", "neue Abläufe", _ => { _wfNew = true; WfPro = false; });
    /// <summary>Produktion: Angaben eines Ablaufs über mehrere Studios (je Vorlage).</summary>
    public LinkSlot FusionLink => FormSlot("produktion-angaben", "LINK-PRODUKTION", "produktion", "fusion", "Angaben für einen Ablauf", "Angaben für Abläufe",
        id => { var k = Fusion.Recipes.ToList().FindIndex(r => r.Id == id); if (k >= 0 && k != _fusionRecipe) { _fusionRecipe = k; _fusionApproval = null; _fusionResult = []; } ProdTab = "ablaeufe"; });
    /// <summary>Lernen: neue Karteikarte (Frage, Antwort).</summary>
    public LinkSlot LearnLink => FormSlot("lernen", "LINK-LERNEN", "lernen", "learn-new", "Eine neue Lernkarte", "neue Lernkarten", _ => { LearnMode = "karten"; });
    /// <summary>KI-Rollen: Auftrag an die Rolle bzw. das Team.</summary>
    public LinkSlot AgentGoalLink => FormSlot("ki-rollen", "LINK-ROLLEN", "ki-rollen", "agent-goal", "Ein Auftrag an die KI-Rollen", "Aufträge an die KI-Rollen", _ => { });
    /// <summary>KI-Rollen: neue eigene Rolle.</summary>
    public LinkSlot AgentRoleLink => FormSlot("ki-rolle-neu", "LINK-ROLLE", "ki-rollen", "agent-role", "Eine neue KI-Rolle", "neue KI-Rollen", _ => { _agentNewRole = true; });
    /// <summary>KI fragen: Nachricht, die noch nicht gesendet ist.</summary>
    public LinkSlot AskLink => FormSlot("ki-fragen", "LINK-KIFRAGEN", "ki-fragen", "ask", "Eine Nachricht an die KI", "Nachrichten an die KI", _ => { });

    /// <summary>Für Tests: Schritte des neuen Ablaufs (Entwurf).</summary>
    public int WfDraftSteps => _wfDraft.Count;

    /// <summary>Feld einer Auswahlliste (Stand = Nummer des Eintrags).</summary>
    static LinkField ComboField(string key, ComboBox cb) =>
        new(key, () => cb.SelectedIndex.ToString(), v => { if (int.TryParse(v, out var i) && i >= 0 && i < cb.ItemCount) cb.SelectedIndex = i; }, h => cb.SelectionChanged += (_, _) => h());

    /// <summary>Getipptes in einem Feld sofort (nach der Tipp-Pause) speichern, ohne die Seite neu aufzubauen – der Cursor bleibt, wo er ist.
    /// T-0726: Die Pause misst ein Zeitgeber des Systems (<see cref="System.Threading.Timer"/>), gespeichert wird auf dem Oberflächen-Thread (Post) – ein neuer Tastendruck
    /// schiebt den Zeitpunkt hinaus. Vorher ein DispatcherTimer, der bei jedem Tastendruck neu gestartet wurde; im Test-Dispatcher feuerte er teils nie
    /// (betraf Tafel, Projekt-Notiz, Lernkarten, Sendeablauf).</summary>
    void SaveOnPause(TextBox tb, Action save)
    {
        System.Threading.Timer? t = null; var pending = false;
        void Due() => Dispatcher.UIThread.Post(() => { if (!pending) return; pending = false; save(); });
        tb.TextChanged += (_, _) =>
        {
            if (!tb.IsFocused) return; // nur echtes Tippen (nicht das Füllen beim Aufbau)
            pending = true;
            t ??= new System.Threading.Timer(_ => Due());
            t.Change(LinkTypingDelay, Timeout.InfiniteTimeSpan);
        };
        tb.DetachedFromVisualTree += (_, _) => { t?.Dispose(); t = null; if (pending) { pending = false; save(); } };
    }
}
