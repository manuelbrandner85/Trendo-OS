using System.Text.Json;

namespace Trendo.Core;

public sealed partial class Scene3D
{
    /// <summary>T-0716 (B6 Etappe E4, Audit 6.5): Rückgängig/Wiederholen von „3D-Modelle erstellen“ als Deltas statt ganzer Kopien.
    /// Vorher war jeder Schritt der ganze Szene-Text (<see cref="ToJson"/>) – bei einem eingelesenen Netz von einigen MB hieß das: die 150-MB-Grenze war nach
    /// wenigen Schritten erreicht, ältere Schritte fielen weg (gemessen im Bericht T-0716). Jetzt ist ein Schritt „Kopf + Text je Teil“, und ein Teil, das sich seit dem
    /// letzten Schritt nicht geändert hat, wird nicht noch einmal gespeichert, sondern geteilt (dieselbe Zeichenkette). Gezählt wird nur, was wirklich im Speicher liegt.
    /// Grenzen wie vorher: 100 Schritte, 150 Mio. Zeichen. Inhalt eines Schritts = genau der gespeicherte Text des Teils (verlustfrei, gleiche Serialisierung wie die Datei).</summary>
    public sealed class History
    {
        public const int MaxSteps = 100;
        public const long MaxChars = 150_000_000;

        sealed record Snap(string Head, string[] Parts);

        readonly List<Snap> _undo = [], _redo = [];
        /// <summary>Zuletzt gesehener Text je Teil (Kennung) – daraus wird geteilt.</summary>
        Dictionary<string, string> _known = [];

        public int Count => _undo.Count;
        public int RedoCount => _redo.Count;

        /// <summary>Zeichen, die alle Schritte zusammen wirklich belegen (jede geteilte Zeichenkette einmal).</summary>
        public long Chars
        {
            get
            {
                var seen = new HashSet<string>(ReferenceEqualityComparer.Instance); long n = 0;
                foreach (var s in _undo.Concat(_redo)) { if (seen.Add(s.Head)) n += s.Head.Length; foreach (var p in s.Parts) if (seen.Add(p)) n += p.Length; }
                return n;
            }
        }

        Snap Take(Scene3D scene)
        {
            var parts = scene.Parts; string head;
            scene.Parts = [];
            try { head = JsonSerializer.Serialize(scene, Opt); } finally { scene.Parts = parts; }
            var texts = new string[parts.Count]; var known = new Dictionary<string, string>(parts.Count);
            for (var i = 0; i < parts.Count; i++)
            {
                var t = JsonSerializer.Serialize(parts[i], Opt);
                if (_known.TryGetValue(parts[i].Id ?? "", out var old) && string.Equals(old, t, StringComparison.Ordinal)) t = old; // unverändert: teilen
                texts[i] = t; known[parts[i].Id ?? ""] = t;
            }
            _known = known;
            return new Snap(head, texts);
        }

        static Scene3D Make(Snap s)
        {
            var scene = JsonSerializer.Deserialize<Scene3D>(s.Head, Opt) ?? new Scene3D();
            scene.Parts = [.. s.Parts.Select(p => JsonSerializer.Deserialize<Part3D>(p, Opt) ?? new Part3D())];
            return Normalize(scene);
        }

        /// <summary>Stand vor einer Änderung merken (ein Schritt); Wiederholen entfällt.</summary>
        public void Push(Scene3D scene) { _undo.Add(Take(scene)); _redo.Clear(); Trim(); }

        void Trim()
        {
            while (_undo.Count > MaxSteps) _undo.RemoveAt(0);
            while (_undo.Count > 1 && Chars > MaxChars) _undo.RemoveAt(0);
        }

        /// <summary>Einen Schritt zurück: liefert den früheren Stand (null: nichts da); der jetzige kommt auf Wiederholen.</summary>
        public Scene3D? Undo(Scene3D current)
        {
            if (_undo.Count == 0) return null;
            _redo.Add(Take(current));
            var s = _undo[^1]; _undo.RemoveAt(_undo.Count - 1);
            return Make(s);
        }

        public Scene3D? Redo(Scene3D current)
        {
            if (_redo.Count == 0) return null;
            _undo.Add(Take(current));
            var s = _redo[^1]; _redo.RemoveAt(_redo.Count - 1);
            return Make(s);
        }

        /// <summary>Letzten Schritt verwerfen und dessen Stand liefern, ohne Wiederholen (eine abgelehnte Änderung zurücknehmen).</summary>
        public Scene3D? Drop()
        {
            if (_undo.Count == 0) return null;
            var s = _undo[^1]; _undo.RemoveAt(_undo.Count - 1);
            return Make(s);
        }

        public void Clear() { _undo.Clear(); _redo.Clear(); _known = []; }
    }
}
