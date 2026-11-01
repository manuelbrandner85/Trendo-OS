namespace Trendo.Core;

/// <summary>Balken eines Schritts im Zeitplan: Nummer (1-basiert), Beginn und Ende (jeweils ganzer Tag), Puffer in Tagen und ob er auf dem kritischen Pfad liegt.</summary>
public sealed record PlanBar(int No, Step Step, DateTime Start, DateTime End, int Slack, bool Critical)
{
    public int Days => (End - Start).Days + 1;
}

/// <summary>Vorhaben als Zeitplan (Masterprompt 131): Beginn/Ende je Schritt, verschieben und Dauer ändern, Abhängigkeiten (ohne Kreise),
/// Nachfolger rücken automatisch nach, kritischer Pfad. Alles bleibt lesbar in SCHRITTE.md.</summary>
public static partial class CreationSuite
{
    /// <summary>Balken der Schritte mit Ende. Ohne eigenen Beginn startet ein Schritt am Tag nach dem vorigen Ende (bzw. am Ende selbst); Meilensteine sind eintägig.</summary>
    public static List<PlanBar> Bars(IReadOnlyList<Step> steps)
    {
        var raw = new List<(int No, Step S, DateTime Start, DateTime End)>(); DateTime? prev = null;
        for (var i = 0; i < steps.Count; i++)
        {
            var s = steps[i]; if (s.Due is not { } due) continue;
            var start = s.Milestone ? due.Date : s.Start?.Date ?? (prev is { } p && p.AddDays(1) <= due.Date ? p.AddDays(1) : due.Date);
            if (start > due.Date) start = due.Date;
            raw.Add((i + 1, s, start, due.Date)); prev = due.Date > (prev ?? DateTime.MinValue) ? due.Date : prev;
        }
        // kritischer Pfad: Rückwärtsrechnung über die Abhängigkeiten; Schritte ohne Nachfolger enden spätestens am Projektende
        var end = raw.Count == 0 ? DateTime.Today : raw.Max(r => r.End);
        var lateFinish = raw.ToDictionary(r => r.No, _ => end);
        foreach (var r in raw.OrderByDescending(r => r.End))
            foreach (var d in r.S.After ?? [])
                if (lateFinish.ContainsKey(d))
                {
                    var lf = r.Start.AddDays(-1) + (lateFinish[r.No] - r.End); // spätester Beginn des Nachfolgers − 1 Tag
                    if (lf < lateFinish[d]) lateFinish[d] = lf;
                }
        return raw.Select(r => { var slack = Math.Max(0, (lateFinish[r.No] - r.End).Days); return new PlanBar(r.No, r.S, r.Start, r.End, slack, slack == 0 && !r.S.Done); }).ToList();
    }

    static Step ByNo(string dir, int no) => Steps(dir) is var all && no >= 1 && no <= all.Count ? all[no - 1] : throw new ArgumentException($"Schritt {no} gibt es nicht.");

    /// <summary>Beginn und Ende setzen (Ende nicht vor Beginn).</summary>
    public static void SetSpan(string dir, int line, DateTime start, DateTime end)
    {
        if (end.Date < start.Date) (start, end) = (end, start);
        Update(dir, line, s => s with { Start = start.Date, Due = end.Date });
    }

    /// <summary>Schritt um Tage verschieben (Beginn und Ende); danach rücken abhängige Schritte nach. Gibt die Zahl nachgerückter Schritte zurück.</summary>
    public static int MoveBy(string dir, int no, int days)
    {
        var bar = Bars(Steps(dir)).FirstOrDefault(b => b.No == no) ?? throw new ArgumentException("Dieser Schritt hat noch keine Termine.");
        SetSpan(dir, bar.Step.Line, bar.Start.AddDays(days), bar.End.AddDays(days));
        return Reschedule(dir);
    }

    /// <summary>Ende (oder mit <paramref name="atStart"/> den Beginn) um Tage ändern – mindestens ein Tag Dauer bleibt.</summary>
    public static int ResizeBy(string dir, int no, int days, bool atStart = false)
    {
        var bar = Bars(Steps(dir)).FirstOrDefault(b => b.No == no) ?? throw new ArgumentException("Dieser Schritt hat noch keine Termine.");
        var start = atStart ? bar.Start.AddDays(days) : bar.Start; var end = atStart ? bar.End : bar.End.AddDays(days);
        if (end < start) { if (atStart) start = end; else end = start; }
        SetSpan(dir, bar.Step.Line, start, end);
        return Reschedule(dir);
    }

    /// <summary>„Schritt <paramref name="no"/> kommt nach <paramref name="after"/>“ – Kreise werden abgewiesen.</summary>
    public static void SetAfter(string dir, int no, IEnumerable<int> after)
    {
        var steps = Steps(dir); var list = after.Distinct().Where(a => a != no).Order().ToArray();
        if (list.Any(a => a < 1 || a > steps.Count)) throw new ArgumentException("Diesen Schritt gibt es nicht.");
        var deps = steps.Select((s, i) => (No: i + 1, After: i + 1 == no ? list : s.After ?? [])).ToDictionary(x => x.No, x => x.After);
        bool Reaches(int from, int target, HashSet<int> seen) => from == target || seen.Add(from) && deps[from].Any(d => Reaches(d, target, seen));
        if (list.Any(a => Reaches(a, no, []))) throw new InvalidOperationException("Das ergäbe einen Kreis – ein Schritt kann nicht nach sich selbst kommen.");
        Update(dir, ByNo(dir, no).Line, s => s with { After = list.Length > 0 ? list : null });
    }
    public static void AddAfter(string dir, int no, int before) => SetAfter(dir, no, [.. ByNo(dir, no).After ?? [], before]);

    /// <summary>Abhängige Schritte beginnen frühestens am Tag nach dem Ende ihrer Vorgänger (Dauer bleibt). Gibt die Zahl verschobener Schritte zurück.</summary>
    public static int Reschedule(string dir)
    {
        var moved = new HashSet<int>();
        for (var pass = 0; pass < 50; pass++)
        {
            var changed = false; var bars = Bars(Steps(dir)).ToDictionary(b => b.No);
            foreach (var b in bars.Values)
            {
                var pre = (b.Step.After ?? []).Where(bars.ContainsKey).Select(d => bars[d].End).DefaultIfEmpty(DateTime.MinValue).Max();
                if (pre == DateTime.MinValue || b.Start > pre) continue;
                var shift = (pre.AddDays(1) - b.Start).Days;
                SetSpan(dir, b.Step.Line, b.Start.AddDays(shift), b.End.AddDays(shift)); moved.Add(b.No); changed = true; break;
            }
            if (!changed) break;
        }
        return moved.Count;
    }
}
