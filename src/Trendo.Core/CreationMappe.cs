namespace Trendo.Core;

/// <summary>Eckdaten eines Vorhabens für die Projektmappe: Zählung je Spalte, überfällige Schritte, nächster Schritt, Zieltermin und verbleibende Tage.</summary>
public sealed record SuiteSummary(string Name, string Goal, string Kind, int Percent, int Open, int InWork, int Done, int Overdue, Step? Next, DateTime? End, int? DaysLeft, int Files);

/// <summary>Eintrag im Mappen-Baum: Pfad relativ zum Vorhaben, Tiefe (0 = oberste Ebene), Ordner oder Datei; bei Ordnern die Zahl der Dateien darin.</summary>
public sealed record MappeEntry(string Relative, string Name, int Depth, bool IsFolder, int Count);

/// <summary>Neues Projekt als Projektmappe (Masterprompt 131, Runde 7): Übersicht, Baum der Ordner und Dateien, Umbenennen – alles aus dem Vorhaben-Ordner.</summary>
public static partial class CreationSuite
{
    static string Header(string dir, string key)
    {
        var f = Path.Combine(dir, ListFile); if (!File.Exists(f)) return "";
        foreach (var l in File.ReadLines(f).Take(12)) if (l.StartsWith(key + ":", StringComparison.Ordinal)) return l[(key.Length + 1)..].Trim();
        return "";
    }

    public static SuiteSummary Summary(string dir, DateTime today)
    {
        var steps = Steps(dir); today = today.Date;
        var bars = Bars(steps);
        DateTime? end = steps.LastOrDefault(s => s.Milestone)?.Due ?? (bars.Count > 0 ? bars.Max(b => b.End) : null);
        return new(Path.GetFileName(dir), Header(dir, "Ziel"), Header(dir, "Vorhaben"), Percent(dir),
            steps.Count(s => s.State == 0), steps.Count(s => s.State == 1), steps.Count(s => s.State == 2),
            steps.Count(s => !s.Done && s.Due is { } d && d < today),
            steps.FirstOrDefault(s => s.State == 1) ?? steps.FirstOrDefault(s => !s.Done),
            end, end is { } e ? (e - today).Days : null, Files(dir).Count);
    }

    /// <summary>Ordner und Dateien des Vorhabens als Baum (Ordner zuerst, alphabetisch); versteckte Ordner, node_modules und die Schrittliste fehlen. Höchstens <paramref name="max"/> Einträge.</summary>
    public static List<MappeEntry> Tree(string dir, int max = 300)
    {
        var list = new List<MappeEntry>();
        if (!Directory.Exists(dir)) return list;
        static bool Hidden(string name) => name.StartsWith('.') || name == "node_modules";
        void Walk(DirectoryInfo d, int depth)
        {
            if (depth > 6) return;
            foreach (var sub in d.EnumerateDirectories().Where(x => !Hidden(x.Name)).OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                if (list.Count >= max) return;
                var count = sub.EnumerateFiles("*", SearchOption.AllDirectories).Count(f => !f.FullName.Contains(Path.DirectorySeparatorChar + "node_modules" + Path.DirectorySeparatorChar, StringComparison.Ordinal));
                list.Add(new(Path.GetRelativePath(dir, sub.FullName), sub.Name, depth, true, count));
                Walk(sub, depth + 1);
            }
            foreach (var f in d.EnumerateFiles().Where(x => !Hidden(x.Name) && !(depth == 0 && x.Name == ListFile)).OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                if (list.Count >= max) return;
                list.Add(new(Path.GetRelativePath(dir, f.FullName), f.Name, depth, false, 0));
            }
        }
        Walk(new DirectoryInfo(dir), 0);
        return list;
    }

    /// <summary>Vorhaben umbenennen: Ordner umbenennen und die Überschrift der Schrittliste anpassen. Gibt den neuen Ordner zurück.</summary>
    public static string Rename(string dir, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Bitte einen Namen eingeben.");
        var safe = DevStudio.SafeName(name);
        if (safe.Length == 0) throw new ArgumentException("Bitte einen Namen eingeben.");
        var target = Path.Combine(Path.GetDirectoryName(dir)!, safe);
        if (string.Equals(Path.GetFullPath(target), Path.GetFullPath(dir), StringComparison.Ordinal)) return dir;
        if (Directory.Exists(target) && !string.Equals(Path.GetFullPath(target), Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase))
            throw new IOException($"Das Vorhaben „{safe}“ gibt es schon.");
        Directory.Move(dir, target);
        var f = Path.Combine(target, ListFile);
        if (File.Exists(f)) { var lines = File.ReadAllLines(f); if (lines.Length > 0 && lines[0].StartsWith("# ", StringComparison.Ordinal)) { lines[0] = "# " + name!.Trim(); File.WriteAllLines(f, lines); } }
        return target;
    }
}
