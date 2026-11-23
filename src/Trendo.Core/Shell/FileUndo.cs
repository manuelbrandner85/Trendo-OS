namespace Trendo.Core.Shell;

/// <summary>T-0754 (CORE-01): Rückgängig und Wiederholen für Dateiaktionen im Dateimanager – Verschieben, Umbenennen, Kopieren, Neuer Ordner
/// und Löschen (Papierkorb). Mehrere Schritte, Liste für die laufende Sitzung (nicht gespeichert, wie im Explorer).
/// Grundsatz: Es wird nie etwas überschrieben oder endgültig gelöscht. Vor jedem Schritt wird geprüft, ob die Dateien noch so sind wie nach der Aktion
/// (Größe, Änderungszeit, Anzahl). Wurde etwas von außen geändert, verschoben, belegt oder der Papierkorb geleert, sagt das Ergebnis das ehrlich
/// und es passiert nichts. Rückgängig von „Kopieren“ legt die Kopie in den Papierkorb, von „Neuer Ordner“ entfernt nur einen leeren Ordner.</summary>
public sealed class FileUndo(FileBrowser fb, Action<string>? windowsRecycle = null)
{
    public enum Kind { Verschieben, Umbenennen, Kopieren, NeuerOrdner, Papierkorb }

    /// <summary>Fingerabdruck eines Elements: Bytes, späteste Änderungszeit (UTC-Ticks) und Anzahl Einträge (Ordner: alle Dateien und Unterordner).</summary>
    public sealed record Stamp(bool IsDir, long Bytes, long Ticks, int Entries);

    /// <summary>Eine einzelne Aktion. <see cref="From"/> ist der Platz vorher, <see cref="To"/> danach (bei Papierkorb: die Lage im Papierkorb, null = nicht gefunden).</summary>
    public sealed class Op(string from, string? to)
    {
        public string From { get; internal set; } = from;
        public string? To { get; internal set; } = to;
        public Stamp? After { get; internal set; }
    }

    public sealed record Step(Kind Kind, IReadOnlyList<Op> Ops, string Label);

    /// <summary>Ergebnis von Rückgängig/Wiederholen: hat es geklappt, ehrlicher Satz für die Statuszeile, betroffene Pfade (zum Auswählen).</summary>
    public sealed record Result(bool Done, string Message, IReadOnlyList<string> Paths);

    readonly List<Step> _undo = [], _redo = [];
    public int Max { get; init; } = 100;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    /// <summary>Schritte zum Rückgängigmachen, neuester zuerst.</summary>
    public IReadOnlyList<string> UndoList => [.. Enumerable.Reverse(_undo).Select(s => s.Label)];
    /// <summary>Schritte zum Wiederholen, nächster zuerst.</summary>
    public IReadOnlyList<string> RedoList => [.. Enumerable.Reverse(_redo).Select(s => s.Label)];

    /// <summary>Eine erledigte Aktion merken (Paare Vorher → Nachher). Paare ohne Wirkung (gleicher Pfad) zählen nicht. Neue Aktion leert „Wiederholen“.</summary>
    public void Record(Kind kind, IEnumerable<(string From, string? To)> pairs)
    {
        var ops = new List<Op>();
        foreach (var (from, to) in pairs)
        {
            if (to is not null && kind != Kind.Papierkorb && string.Equals(Path.GetFullPath(from), Path.GetFullPath(to), StringComparison.Ordinal)) continue;
            // Gegenprüfung T-0754: bei „Papierkorb“ zählt nur ein absoluter Pfad in einem echten Papierkorb – nie ein Wort oder ein relativer Pfad
            var target = kind == Kind.Papierkorb && !(FileBrowser.IsTrashLocation(to) && Exists(to)) ? null : to;
            ops.Add(new Op(Path.GetFullPath(from), target is null ? null : Path.GetFullPath(target)) { After = target is null ? null : Take(target) });
        }
        if (ops.Count == 0) return;
        _undo.Add(new Step(kind, ops, Label(kind, ops)));
        if (_undo.Count > Max) _undo.RemoveAt(0);
        _redo.Clear();
    }

    public void Record(Kind kind, string from, string? to) => Record(kind, [(from, to)]);

    /// <summary>Letzten Schritt zurücknehmen.</summary>
    public Result Undo()
    {
        if (_undo.Count == 0) return new(false, "Es gibt nichts rückgängig zu machen.", []);
        var s = _undo[^1]; _undo.RemoveAt(_undo.Count - 1);
        // Erst alles prüfen, dann handeln – so bleibt bei einem Hindernis alles, wie es ist.
        foreach (var op in s.Ops) if (UndoProblem(s.Kind, op) is { } why) return new(false, $"„{s.Label}“ lässt sich nicht rückgängig machen: {why} Es wurde nichts geändert; der Schritt ist aus der Liste genommen.", []);
        var done = new List<string>(); var doneOps = new List<Op>();
        try
        {
            foreach (var op in Enumerable.Reverse(s.Ops)) { DoUndo(s.Kind, op); done.Add(op.From); doneOps.Add(op); }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Gegenprüfung T-0754: Teilerfolg – Erledigtes kann wiederholt werden, der Rest bleibt als eigener Rückgängig-Schritt in der Liste
            var rest = s.Ops.Where(o => !doneOps.Contains(o)).ToList();
            if (rest.Count > 0) _undo.Add(new Step(s.Kind, rest, Label(s.Kind, rest)));
            if (doneOps.Count > 0) { doneOps.Reverse(); _redo.Add(new Step(s.Kind, doneOps, Label(s.Kind, doneOps))); }
            return new(false, $"„{s.Label}“ nur teilweise rückgängig ({done.Count} von {s.Ops.Count}): {e.Message} Der Rest bleibt als eigener Schritt in der Liste.", done);
        }
        _redo.Add(s);
        return new(true, $"Rückgängig: {s.Label}.", s.Kind is Kind.Kopieren or Kind.NeuerOrdner ? [] : done);
    }

    /// <summary>Die letzten <paramref name="count"/> Schritte nacheinander zurücknehmen (Liste im Menü). Hält beim ersten Hindernis an.</summary>
    public Result UndoMany(int count)
    {
        Result last = new(false, "Es gibt nichts rückgängig zu machen.", []);
        for (var i = 0; i < count && _undo.Count > 0; i++) { last = Undo(); if (!last.Done) return i == 0 ? last : last with { Message = $"{i} Schritt(e) rückgängig gemacht, dann: {last.Message}" }; }
        return count > 1 && last.Done ? last with { Message = $"{count} Schritte rückgängig gemacht." } : last;
    }

    /// <summary>Zuletzt zurückgenommenen Schritt wieder ausführen.</summary>
    public Result Redo()
    {
        if (_redo.Count == 0) return new(false, "Es gibt nichts zu wiederholen.", []);
        var s = _redo[^1]; _redo.RemoveAt(_redo.Count - 1);
        foreach (var op in s.Ops) if (RedoProblem(s.Kind, op) is { } why) return new(false, $"„{s.Label}“ lässt sich nicht wiederholen: {why} Es wurde nichts geändert; der Schritt ist aus der Liste genommen.", []);
        var done = new List<string>(); var doneOps = new List<Op>();
        try
        {
            foreach (var op in s.Ops) { DoRedo(s.Kind, op); doneOps.Add(op); if (op.To is { } t) done.Add(t); }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            var rest = s.Ops.Where(o => !doneOps.Contains(o)).ToList();
            if (rest.Count > 0) _redo.Add(new Step(s.Kind, rest, Label(s.Kind, rest)));
            if (doneOps.Count > 0) _undo.Add(new Step(s.Kind, doneOps, Label(s.Kind, doneOps)));
            return new(false, $"„{s.Label}“ nur teilweise wiederholt ({done.Count} von {s.Ops.Count}): {e.Message}", done);
        }
        _undo.Add(s);
        return new(true, $"Wiederholt: {s.Label}.", s.Kind == Kind.Papierkorb ? [] : done);
    }

    // ---------- Prüfen ----------
    static bool Exists(string? p) => p is not null && (File.Exists(p) || Directory.Exists(p));
    static string N(string p) => Path.GetFileName(p.TrimEnd('\\', '/'));
    static bool ParentOk(string p) => Path.GetDirectoryName(p) is { } d && Directory.Exists(d);

    string? Same(string path, Stamp? want)
    {
        if (!Exists(path)) return $"„{N(path)}“ gibt es dort nicht mehr (verschoben, umbenannt oder gelöscht).";
        if (want is null || Take(path) != want) return $"„{N(path)}“ wurde inzwischen verändert.";
        return null;
    }

    string? UndoProblem(Kind k, Op op)
    {
        try { fb.Guard(op.From); if (op.To is { } t) fb.Guard(t); } catch (UnauthorizedAccessException e) { return e.Message; }
        switch (k)
        {
            case Kind.Verschieben or Kind.Umbenennen:
                if (Same(op.To!, op.After) is { } a) return a;
                if (Exists(op.From)) return $"Am alten Platz liegt inzwischen wieder „{N(op.From)}“ – Trendo überschreibt nichts.";
                if (!ParentOk(op.From)) return $"Den Ordner „{Path.GetDirectoryName(op.From)}“ gibt es nicht mehr.";
                return null;
            case Kind.Kopieren:
                return Same(op.To!, op.After);
            case Kind.NeuerOrdner:
                if (!Directory.Exists(op.To)) return $"Den Ordner „{N(op.To!)}“ gibt es nicht mehr.";
                if (Directory.EnumerateFileSystemEntries(op.To!).Any()) return $"Der Ordner „{N(op.To!)}“ ist nicht mehr leer – Trendo entfernt ihn nicht. Leg ihn selbst in den Papierkorb, wenn du ihn nicht brauchst.";
                return null;
            case Kind.Papierkorb:
                if (op.To is null) return $"„{N(op.From)}“ wurde endgültig gelöscht (nicht im Papierkorb gelandet) – zurückholen geht nicht.";
                if (!Exists(op.To)) return $"„{N(op.From)}“ ist nicht mehr im Papierkorb (Papierkorb geleert oder schon zurückgeholt).";
                if (Take(op.To) != op.After) return $"„{N(op.From)}“ im Papierkorb ist nicht mehr dasselbe Element.";
                if (Exists(op.From)) return $"Am alten Platz liegt inzwischen wieder „{N(op.From)}“ – Trendo überschreibt nichts.";
                if (!ParentOk(op.From)) return $"Den Ordner „{Path.GetDirectoryName(op.From)}“ gibt es nicht mehr.";
                return null;
        }
        return "Unbekannte Aktion.";
    }

    string? RedoProblem(Kind k, Op op)
    {
        try { fb.Guard(op.From); if (op.To is { } t) fb.Guard(t); } catch (UnauthorizedAccessException e) { return e.Message; }
        switch (k)
        {
            case Kind.Verschieben or Kind.Umbenennen:
                if (Same(op.From, op.After) is { } a) return a;
                if (Exists(op.To)) return $"Am Ziel liegt inzwischen „{N(op.To!)}“ – Trendo überschreibt nichts.";
                if (!ParentOk(op.To!)) return $"Den Zielordner „{Path.GetDirectoryName(op.To)}“ gibt es nicht mehr.";
                return null;
            case Kind.Kopieren:
                if (!Exists(op.From)) return $"Das Original „{N(op.From)}“ gibt es nicht mehr.";
                if (Exists(op.To)) return $"Am Ziel liegt inzwischen „{N(op.To!)}“ – Trendo überschreibt nichts.";
                if (!ParentOk(op.To!)) return $"Den Zielordner „{Path.GetDirectoryName(op.To)}“ gibt es nicht mehr.";
                return null;
            case Kind.NeuerOrdner:
                if (Exists(op.To)) return $"„{N(op.To!)}“ gibt es dort schon.";
                if (!ParentOk(op.To!)) return $"Den Ordner „{Path.GetDirectoryName(op.To)}“ gibt es nicht mehr.";
                return null;
            case Kind.Papierkorb:
                if (!fb.HasTrash(op.From)) return $"„{N(op.From)}“ liegt auf einem Laufwerk ohne Papierkorb – Wiederholen würde endgültig löschen.";
                return Same(op.From, op.After);
        }
        return "Unbekannte Aktion.";
    }

    // ---------- Ausführen ----------
    void DoUndo(Kind k, Op op)
    {
        switch (k)
        {
            case Kind.Verschieben or Kind.Umbenennen: FileBrowser.MoveAny(op.To!, op.From); break;
            case Kind.Kopieren: fb.Trash(op.To!, windowsRecycle); break; // die Kopie geht in den Papierkorb, nicht endgültig weg
            case Kind.NeuerOrdner: Directory.Delete(op.To!, false); break;  // nur leer (geprüft); recursive=false schützt zusätzlich
            case Kind.Papierkorb: fb.Restore(op.To!, op.From); break;
        }
    }

    void DoRedo(Kind k, Op op)
    {
        switch (k)
        {
            case Kind.Verschieben or Kind.Umbenennen: FileBrowser.MoveAny(op.From, op.To!); break;
            case Kind.Kopieren:
                if (Directory.Exists(op.From)) FileBrowser.CopyDir(op.From, op.To!); else File.Copy(op.From, op.To!, overwrite: false);
                op.After = Take(op.To!); break;
            case Kind.NeuerOrdner: Directory.CreateDirectory(op.To!); op.After = Take(op.To!); break;
            case Kind.Papierkorb:
                var r = fb.Trash(op.From, windowsRecycle);
                op.To = FileBrowser.IsTrashLocation(r) && Exists(r) ? Path.GetFullPath(r!) : null; op.After = op.To is null ? null : Take(op.To); break;
        }
    }

    /// <summary>Fingerabdruck lesen (null, wenn es das Element nicht gibt).</summary>
    public static Stamp? Take(string p)
    {
        try
        {
            if (File.Exists(p)) { var f = new FileInfo(p); return new(false, f.Length, f.LastWriteTimeUtc.Ticks, 1); }
            if (!Directory.Exists(p)) return null;
            long bytes = 0, ticks = 0; var n = 0;
            foreach (var e in new DirectoryInfo(p).EnumerateFileSystemInfos("*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 }))
            {
                n++;
                if (e is FileInfo fi) { bytes += fi.Length; ticks = Math.Max(ticks, fi.LastWriteTimeUtc.Ticks); }
            }
            return new(true, bytes, ticks, n);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    static string Label(Kind k, IReadOnlyList<Op> ops)
    {
        string What(Func<Op, string> name) => ops.Count == 1 ? $"„{name(ops[0])}“" : $"{ops.Count} Elementen";
        return k switch
        {
            Kind.Verschieben => "Verschieben von " + What(o => N(o.From)),
            Kind.Umbenennen => ops.Count == 1 ? $"Umbenennen von „{N(ops[0].From)}“ in „{N(ops[0].To!)}“" : $"Umbenennen von {ops.Count} Elementen",
            Kind.Kopieren => "Kopieren von " + What(o => N(o.From)),
            Kind.NeuerOrdner => ops.Count == 1 ? $"Neuer Ordner „{N(ops[0].To!)}“" : $"{ops.Count} neue Ordner",
            _ => "Löschen von " + What(o => N(o.From)),
        };
    }
}
