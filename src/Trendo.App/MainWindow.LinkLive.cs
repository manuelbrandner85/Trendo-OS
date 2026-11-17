using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>T-0716 (B6 „Programm-Hülle für alle“, Etappe E4): Anschluss für Programme, die jede Änderung SOFORT speichern (Baukasten, Anlagen-Monitor; das Fahrzeugbuch ist mit T-0720 entfernt) –
/// derselbe Weg wie T-0711 (<see cref="ProgramLink"/>, <see cref="LinkSlot"/>, kein zweites System). Der Stand besteht aus
/// <c>daten</c> (genau das, was das Programm speichert) und <c>eingaben</c> (in Feldern Getipptes, das noch nicht übernommen ist – Enter bzw. Feld verlassen –, mit <c>ort</c>,
/// zu welcher Ansicht die Felder gehören).
/// <list type="bullet">
/// <item>Rückgängig/Wiederholen (bis 100 Schritte der Hülle): jede gespeicherte Änderung und jede Tipp-Pause ist ein Schritt; Rückgängig einer gespeicherten Änderung
/// schreibt den früheren Stand wieder in die Datei des Programms (wie vorher das eigene Rückgängig des Baukastens).</item>
/// <item>Notsicherung (20-s-Takt der Hülle, atomar, für das Fenster gesperrt): nur wenn etwas getippt und noch nicht übernommen ist – alles andere ist schon gespeichert.</item>
/// <item>Wiederherstellen-Karte: holt die getippten Eingaben zurück in ihre Felder; die gespeicherten Daten bleiben, wie sie auf der Platte sind (nichts Neueres wird überschrieben).</item>
/// <item><c>keep</c>: Verträgt eine Änderung kein Zurücknehmen (Anlage/Sensor angelegt, gelöscht oder umbenannt), beginnt der Verlauf dort neu.</item>
/// </list></summary>
public partial class MainWindow
{
    readonly Dictionary<string, (Func<JsonNode?> Data, Func<string> Where, Func<JsonNode?, JsonNode?, bool>? Keep)> _liveSlots = [];

    /// <summary>Ein sofort speicherndes Programm anschließen. <paramref name="setData"/> übernimmt einen Stand der Daten UND speichert ihn (atomar, wie das Programm selbst);
    /// <paramref name="where"/>/<paramref name="goTo"/>: Ansicht, zu der die Eingabefelder gehören (z. B. gewählte Anlage und Reiter).</summary>
    LinkSlot LiveSlot(string key, string studio, string page, string prefix, string one, string many, Func<JsonNode?> data, Action<JsonNode?> setData,
        Func<string> where, Action<string> goTo, Func<JsonNode?, JsonNode?, bool>? keep = null, Func<string>? title = null)
    {
        if (_slots.TryGetValue(key, out var have)) return have;
        LinkSlot? me = null;
        JsonObject Inputs() => LiveInputs(me!);
        JsonNode Get()
        {
            var inp = Inputs();
            return new JsonObject { ["daten"] = data()?.DeepClone(), ["ort"] = inp.Count > 0 ? me!.Live?["ort"]?.DeepClone() : null, ["eingaben"] = inp.DeepClone() };
        }
        void TakeInputs(JsonNode? st)
        {
            var ort = st?["ort"]?.GetValue<string>();
            if (ort is not null && ort != where()) goTo(ort);
            me!.Live = new JsonObject { ["ort"] = where(), ["eingaben"] = (st?["eingaben"] as JsonObject)?.DeepClone() ?? new JsonObject() };
        }
        me = new LinkSlot
        {
            Key = key, Page = page, Prefix = prefix, One = one, Many = many, Title = title,
            Link = new ProgramLink(studio, key, null, one) { Locks = ProgLocks },
            Get = Get,
            Apply = st =>
            {
                var target = st?["daten"];
                if (!JsonNode.DeepEquals(target, data()))
                    try { setData(target?.DeepClone()); }
                    catch (Exception e) when (e is InvalidOperationException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
                    { me!.Link.Track(Get()); me.Msg = "Das ließ sich nicht zurücknehmen: " + Trendo.Core.FriendlyError.Say(e); return; } // Stand bleibt der gespeicherte
                TakeInputs(st);
                if (Inputs().Count == 0) me!.Link.Saved(ProgBackup); // nichts Getipptes offen: alles steht in der Datei des Programms
            },
            // Zurückholen: nur die Eingaben; die Daten bleiben die gespeicherten (die Sicherung ist nur eine Kopie des Ungespeicherten)
            Recovered = st => { TakeInputs(st); me!.Link.Track(Get()); },
            RecoveredMsg = "Wiederhergestellt – deine Eingaben stehen wieder in den Feldern. Mit Enter übernehmen.",
            Worth = () => Inputs().Count > 0,
        };
        me.Link.Reset(Get(), title?.Invoke() ?? one);
        _liveSlots[key] = (data, where, keep);
        return _slots[key] = me;
    }

    static JsonObject LiveInputs(LinkSlot s)
    {
        s.Live ??= new JsonObject { ["ort"] = "", ["eingaben"] = new JsonObject() };
        if (s.Live["eingaben"] is not JsonObject o) s.Live["eingaben"] = o = new JsonObject();
        return o;
    }

    /// <summary>Zu Beginn jedes Neuaufbaus der Seite: Hat das Programm gespeichert (Daten anders als im Verlauf), wird das ein Rückgängig-Schritt
    /// (bzw. der Verlauf beginnt neu, wenn <c>keep</c> es verlangt); offene Eingaben gehören dann nicht mehr dazu. Andere Ansicht: Eingaben der alten entfallen
    /// (wie bisher beim Neuzeichnen) – Rückgängig bringt sie zurück.</summary>
    void LiveSync(LinkSlot s)
    {
        if (!_liveSlots.TryGetValue(s.Key, out var f)) return;
        var cur = f.Data(); var before = s.Link.State?["daten"];
        var ort = f.Where();
        if (s.Live?["ort"]?.GetValue<string>() != ort) s.Live = new JsonObject { ["ort"] = ort, ["eingaben"] = new JsonObject() };
        if (!JsonNode.DeepEquals(cur, before))
        {
            s.Live = new JsonObject { ["ort"] = ort, ["eingaben"] = new JsonObject() };
            if (f.Keep is null || f.Keep(before, cur)) LinkCommit(s, "Geändert");
            else { s.Typing?.Stop(); s.Link.Reset(s.Get(), s.Title?.Invoke()); }
        }
        if (LiveInputs(s).Count == 0) { LinkCommit(s, "Geändert"); s.Link.Saved(ProgBackup); }
    }

    /// <summary>Nach dem Aufbau der Seite: benannte Eingabefelder an die Eingaben binden – offene Eingaben (nach Rückgängig oder Zurückholen) stehen wieder im Feld,
    /// jede Eingabe wird nach einer Tipp-Pause ein Rückgängig-Schritt. Felder für Kennwörter oder Schlüssel werden nie gesichert.</summary>
    Control LiveBind(LinkSlot s, Control view, Func<string, bool>? field = null)
    {
        var inputs = LiveInputs(s);
        foreach (var tb in view.GetLogicalDescendants().OfType<TextBox>().ToList())
        {
            if (string.IsNullOrEmpty(tb.Name) || tb.PasswordChar != '\0' || tb.IsReadOnly || field?.Invoke(tb.Name) == false) continue;
            var box = tb; var name = tb.Name; var fresh = tb.Text ?? "";
            if (inputs[name]?.GetValue<string>() is { } v) { if (v == fresh) inputs.Remove(name); else box.Text = v; }
            box.TextChanged += (_, _) =>
            {
                var t = box.Text ?? ""; var inp = LiveInputs(s);
                if (t == fresh) inp.Remove(name); else inp[name] = t;
                LinkTyped(s);
            };
        }
        return view;
    }

    /// <summary>Daten als JSON (wie das Programm sie speichert; NaN-Grenzen als Text, damit sie ohne Fehler hin und zurück gehen).</summary>
    static JsonNode? LiveNode<T>(T value) => JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(value, LiveJson));
    static T? LiveObject<T>(JsonNode? n) => n is null ? default : System.Text.Json.JsonSerializer.Deserialize<T>(n.ToJsonString(), LiveJson);
    static readonly System.Text.Json.JsonSerializerOptions LiveJson = new() { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };

    /// <summary>Für Tests: offene (noch nicht übernommene) Eingaben eines sofort speichernden Programms.</summary>
    public IReadOnlyDictionary<string, string> LiveInputsOf(string key) =>
        _slots.TryGetValue(key, out var s) ? LiveInputs(s).ToDictionary(kv => kv.Key, kv => kv.Value?.GetValue<string>() ?? "") : new Dictionary<string, string>();
}
