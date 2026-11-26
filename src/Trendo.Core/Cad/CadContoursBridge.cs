namespace Trendo.Core.Cad;

/// <summary>T-0772: Spalten zwischen offenen Ketten überbrücken (Schritte 2 und 3 von <see cref="CadContours.Chain(List{CadContour}, double, List{string}?, bool, CancellationToken)"/>).
/// Bis 0.9.110 wurde in jeder Runde alles neu berechnet (alle Enden, alle Paare, sortieren, Mehrdeutigkeit) – bei vielen Spalten quadratisch
/// (30 000 dicht liegende Linien: fünf Minuten und mehr, „k5_dicht“ hing). Jetzt werden Paare und ihr Zustand (eindeutig / mehrdeutig) einmal
/// berechnet und nach jeder Verbindung nur in der Nähe der geänderten Enden neu bewertet. Das Ergebnis ist dasselbe: gleiche Verbindungen in gleicher
/// Reihenfolge, gleiche Meldungen in gleicher Reihenfolge. Nur wenn zwei Paare genau gleich weit sind, hing die Reihenfolge bisher vom Sortierverfahren
/// ab – dann wird diese eine Runde genau wie bisher gerechnet. Eigenes Arbeitsbudget: Ist es erschöpft, werden keine weiteren Spalten geschlossen
/// (Meldung) – nie ein Hängen.</summary>
public static partial class CadContours
{
    /// <summary>T-0772: Arbeitsbudget (Schritte) fürs Überbrücken je Aufruf. Nur für Tests verstellbar, je Thread; <c>null</c> = 2 Mrd.</summary>
    [ThreadStatic] public static long? BridgeBudget;
    /// <summary>T-0772: Kennzahlen des letzten Aufrufs im selben Thread (Runden, davon genau wie bisher gerechnet, Schritte) – für Messung und Tests.</summary>
    [ThreadStatic] public static (int Rounds, int Exact, long Work, bool BudgetOut) LastBridgeStats;
    const long DefaultBridgeBudget = 2_000_000_000;
    /// <summary>T-0772: höchstens so viele Paare gleichzeitig (Speicher); mehr gilt wie ein erschöpftes Budget.</summary>
    const int MaxBridgePairs = 4_000_000;

    /// <summary>T-0772: Meldung, wenn das Budget beim Überbrücken erschöpft ist.</summary>
    public const string BridgeBudgetNote = "Sehr viele Linienenden liegen dicht beieinander – weitere kleine Spalten werden nicht mehr geschlossen, offene Konturen darin werden ausgelassen. Bitte Umrisse als Polylinie zeichnen.";

    /// <summary>Punktfolge einer Kette ohne Kopieren beim Verbinden: Blatt (Liste aus Schritt 1) oder Verbindung zweier Ketten, jede ggf. umgedreht.</summary>
    sealed class Seq
    {
        public List<(double X, double Y)>? Leaf;
        public Seq? L, R;
        public bool RevL, RevR;
    }

    static List<(double X, double Y)> Flatten(Seq s, int count)
    {
        if (s.Leaf is not null) return s.Leaf;
        var res = new List<(double X, double Y)>(count);
        var st = new Stack<(Seq S, bool Rev)>(); st.Push((s, false));
        while (st.Count > 0)
        {
            var (n, rev) = st.Pop();
            if (n.Leaf is not null)
            {
                if (!rev) res.AddRange(n.Leaf); else for (var i = n.Leaf.Count - 1; i >= 0; i--) res.Add(n.Leaf[i]);
                continue;
            }
            var l = (n.L!, rev ^ n.RevL); var r = (n.R!, rev ^ n.RevR);
            if (!rev) { st.Push(r); st.Push(l); } else { st.Push(l); st.Push(r); }
        }
        return res;
    }

    sealed class PairInfo { public double D; public bool Amb; public (long, long) Key; public int KeyEnd; public (long, int, int, long, int, int, int) Ord; public bool Dead; }

    /// <summary>Schritte 2 und 3: <paramref name="open"/> = offene Ketten in ihrer Reihenfolge; geschlossene kommen in Schließreihenfolge an
    /// <paramref name="done"/>, danach die offen gebliebenen in ihrer Reihenfolge.</summary>
    static void Bridge(List<(List<(double X, double Y)> Pts, string Layer)> open, List<CadContour> done, double tol, double lim, double cell,
        List<string>? notes, CancellationToken ct)
    {
        var budget = BridgeBudget ?? DefaultBridgeBudget;
        long work = 0; var budgetOut = false; var exactRounds = 0; var rounds = 0;
        (long, long) C((double X, double Y) p) => ((long)Math.Floor(p.X / cell), (long)Math.Floor(p.Y / cell));
        // Ketten: Nummer c, Enden 2c (Anfang) und 2c+1 (Ende); Rang = Platz in der Liste (neue Ketten hinten)
        var seq = new List<Seq>(); var layer = new List<string>(); var rank = new List<long>(); var alive = new List<bool>(); var count = new List<int>();
        var first = new List<(double X, double Y)>(); var last = new List<(double X, double Y)>();
        var order = new List<int>(); long nextRank = 0;
        int NewChain(Seq s, string lay, int n, (double X, double Y) f, (double X, double Y) l)
        {
            var c = seq.Count; seq.Add(s); layer.Add(lay); rank.Add(nextRank++); alive.Add(true); count.Add(n); first.Add(f); last.Add(l); order.Add(c);
            return c;
        }
        foreach (var (pts, lay) in open) NewChain(new Seq { Leaf = pts }, lay, pts.Count, pts[0], pts[^1]);
        (double X, double Y) P(int e) => (e & 1) == 0 ? first[e >> 1] : last[e >> 1];
        var emap = new Dictionary<(long, long), List<int>>();
        var pairs = new Dictionary<(int, int), PairInfo>();
        var adj = new Dictionary<int, List<int>>();
        var nonAmb = new SortedSet<(double D, int A, int B)>();
        var ambU = new SortedSet<(double D, int A, int B)>();
        var reported = new HashSet<(long, long)>();
        // Reihenfolge, in der die Paare bis 0.9.110 in die Liste kamen: Ende a (kleinerer Rang, Anfang vor Ende), Nachbarzelle von b, Ende b
        // sortiert gehalten; Neues sammelt sich in listNew und wird erst bei Bedarf eingemischt, Entferntes ist als Dead markiert
        var listOrder = new List<((long, int, int, long, int, int, int) Ord, PairInfo Pi)>(); var listNew = new List<((long, int, int, long, int, int, int) Ord, PairInfo Pi)>();
        var closedOrder = new List<int>();
        void Tick() { if ((++work & 4095) == 0) ct.ThrowIfCancellationRequested(); }
        void PutEnd(int e) { var k = C(P(e)); if (!emap.TryGetValue(k, out var l)) emap[k] = l = []; l.Add(e); }
        void DropEnd(int e)
        {
            var k = C(P(e)); var l = emap[k]; l.Remove(e); if (l.Count == 0) emap.Remove(k);
            if (adj.Remove(e, out var ps))
                foreach (var o in ps)
                {
                    var key = e < o ? (e, o) : (o, e);
                    if (!pairs.Remove(key, out var pi)) continue;
                    var t = (pi.D, key.Item1, key.Item2); nonAmb.Remove(t); ambU.Remove(t); pi.Dead = true;
                }
        }
        bool Amb(int a, int b, double d)
        {
            var (pa, pb) = (P(a), P(b)); var (ca, cb) = (a >> 1, b >> 1); var lay = layer[ca];
            foreach (var q in new[] { pa, pb })
            {
                var (cx, cy) = C(q);
                for (var dx = -1L; dx <= 1; dx++)
                    for (var dy = -1L; dy <= 1; dy++)
                        if (emap.TryGetValue((cx + dx, cy + dy), out var l))
                            foreach (var e in l)
                            {
                                Tick();
                                if (e == a || e == b || e >> 1 == ca || e >> 1 == cb || layer[e >> 1] != lay) continue;
                                var pe = P(e);
                                if (Math.Min(Dist(pe, pa), Dist(pe, pb)) <= d + tol / 2) return true;
                            }
            }
            return false;
        }
        void Place(int lo, int hi, PairInfo pi)
        {
            var t = (pi.D, lo, hi); nonAmb.Remove(t); ambU.Remove(t);
            if (!pi.Amb) nonAmb.Add(t); else if (!reported.Contains(pi.Key)) ambU.Add(t);
        }
        void Link(int e, int o) { if (!adj.TryGetValue(e, out var l)) adj[e] = l = []; l.Add(o); }
        // neue Paare eines Endes (noch ohne Bewertung)
        void PairsOf(int x, List<(int, int)> fresh)
        {
            var px = P(x); var (cx, cy) = C(px); var lay = layer[x >> 1];
            for (var dx = -1L; dx <= 1; dx++)
                for (var dy = -1L; dy <= 1; dy++)
                    if (emap.TryGetValue((cx + dx, cy + dy), out var l))
                        foreach (var e in l)
                        {
                            Tick();
                            if (e >> 1 == x >> 1 || layer[e >> 1] != lay) continue;
                            var key = e < x ? (e, x) : (x, e);
                            var dist = Dist(P(key.Item1), P(key.Item2));
                            if (dist > lim || pairs.ContainsKey(key)) continue;
                            var ke = rank[key.Item1 >> 1] < rank[key.Item2 >> 1] ? key.Item1 : key.Item2;
                            var kp = P(ke);
                            var ob = ke == key.Item1 ? key.Item2 : key.Item1; var (ca0, cb0) = (C(kp), C(P(ob)));
                            var ord = (rank[ke >> 1], ke & 1, (int)((cb0.Item1 - ca0.Item1 + 1) * 3 + (cb0.Item2 - ca0.Item2 + 1)), rank[ob >> 1], ob & 1, ke, ob);
                            var npi = new PairInfo { D = dist, KeyEnd = ke, Key = ((long)Math.Round(kp.X * 1000), (long)Math.Round(kp.Y * 1000)), Ord = ord };
                            pairs[key] = npi; listNew.Add((ord, npi));
                            Link(key.Item1, key.Item2); Link(key.Item2, key.Item1); fresh.Add(key);
                        }
        }
        // Paare in der Nähe geänderter Stellen neu bewerten
        void Refresh(IEnumerable<(double X, double Y)> at, List<(int, int)> fresh)
        {
            var todo = new HashSet<(int, int)>(fresh);
            foreach (var q in at)
            {
                var (cx, cy) = C(q);
                for (var dx = -1L; dx <= 1; dx++)
                    for (var dy = -1L; dy <= 1; dy++)
                        if (emap.TryGetValue((cx + dx, cy + dy), out var l))
                            foreach (var e in l)
                                if (adj.TryGetValue(e, out var ps))
                                    foreach (var o in ps) { Tick(); var key = e < o ? (e, o) : (o, e); if (pairs.ContainsKey(key)) todo.Add(key); }
            }
            foreach (var key in todo)
            {
                var pi = pairs[key]; pi.Amb = Amb(key.Item1, key.Item2, pi.D); Place(key.Item1, key.Item2, pi);
            }
        }
        void Kill(int c) { alive[c] = false; DropEnd(2 * c); DropEnd(2 * c + 1); }
        bool TryClose(int c, bool placed)
        {
            if (count[c] < 3 || Dist(first[c], last[c]) > lim) return false;
            closedOrder.Add(c);
            if (!placed) { alive[c] = false; return true; } // Runde 0: Enden noch nicht eingetragen
            var at = new[] { first[c], last[c] };
            Kill(c); Refresh(at, []);
            return true;
        }
        bool Over() => work > budget || pairs.Count > MaxBridgePairs;

        // Runde genau wie bis 0.9.110 (bei gleich weiten Paaren): Rückgabe = zu verbindende Enden (a mit kleinerem Rang) oder (-1, -1)
        (int A, int B) ExactRound()
        {
            exactRounds++;
            // Liste in der Reihenfolge von 0.9.110: Neues einsortieren und mit dem Bestand mischen (linear), Entferntes weglassen
            listNew.Sort((x, y) => x.Ord.CompareTo(y.Ord));
            var merged = new List<((long, int, int, long, int, int, int) Ord, PairInfo Pi)>(listOrder.Count + listNew.Count);
            int u = 0, v = 0;
            while (u < listOrder.Count || v < listNew.Count)
            {
                Tick();
                var takeOld = v >= listNew.Count || u < listOrder.Count && listOrder[u].Ord.CompareTo(listNew[v].Ord) < 0;
                var x = takeOld ? listOrder[u++] : listNew[v++];
                if (!x.Pi.Dead) merged.Add(x);
            }
            listOrder = merged; listNew.Clear();
            var arr = new (double D, int A, int B)[listOrder.Count];
            for (var k = 0; k < arr.Length; k++) { var o = listOrder[k].Ord; arr[k] = (listOrder[k].Pi.D, o.Item6, o.Item7); }
            IEnumerable<(double D, int A, int B)> sorted;
            if (IntroPrefix.MatchesRuntime) sorted = new IntroPrefix(arr, Tick).Items(); // nur so weit sortieren, wie gebraucht
            else { work += (long)(arr.Length * Math.Log2(arr.Length + 2)); Array.Sort(arr, (x, y) => x.D.CompareTo(y.D)); sorted = arr; }
            foreach (var (_, a, b) in sorted)
            {
                Tick();
                // Mehrdeutigkeit wie bisher bewertet, laufend aktuell gehalten (Refresh)
                if (pairs[a < b ? (a, b) : (b, a)].Amb) { Report(P(a)); continue; }
                return (a, b);
            }
            return (-1, -1);
        }
        void Report((double X, double Y) p)
        {
            var key = ((long)Math.Round(p.X * 1000), (long)Math.Round(p.Y * 1000));
            if (reported.Add(key)) notes?.Add($"Mehrdeutiger Spalt bei X {p.X.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("de-DE"))} Y {p.Y.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("de-DE"))}: mehrere Enden liegen etwa gleich nah – nicht verbunden.");
        }
        // Runde schnell: Ergebnis wie ExactRound, solange keine zwei maßgeblichen Paare gleich weit sind; sonst null
        (int A, int B)? FastRound()
        {
            (double D, int A, int B)? m = null; var second = false;
            foreach (var t in nonAmb) { if (m is null) m = t; else { second = t.D == m.Value.D; break; } }
            if (second) return null;
            var plan = new List<(double D, int A, int B)>(); var planned = new HashSet<(long, long)>(); var lastD = double.NaN;
            var stale = new List<(double D, int A, int B)>();
            foreach (var t in ambU)
            {
                Tick();
                var pi = pairs[(t.A, t.B)];
                if (reported.Contains(pi.Key)) { stale.Add(t); continue; }
                if (m is not null && t.D > m.Value.D) break;
                if (m is not null && t.D == m.Value.D) return null;
                if (planned.Contains(pi.Key)) continue;
                if (t.D == lastD) return null;
                plan.Add(t); planned.Add(pi.Key); lastD = t.D;
            }
            foreach (var t in stale) ambU.Remove(t);
            foreach (var t in plan) { var pi = pairs[(t.A, t.B)]; Report(P(pi.KeyEnd)); ambU.Remove(t); }
            if (m is null) return (-1, -1);
            var pm = pairs[(m.Value.A, m.Value.B)];
            return (pm.KeyEnd, pm.KeyEnd == m.Value.A ? m.Value.B : m.Value.A);
        }
        int Merge(int a, int b)
        {
            var (ca, cb) = (a >> 1, b >> 1); var (aLast, bLast) = ((a & 1) == 1, (b & 1) == 1);
            var f = aLast ? first[ca] : last[ca]; var l = bLast ? first[cb] : last[cb];
            var at = new[] { first[ca], last[ca], first[cb], last[cb] };
            Kill(ca); Kill(cb);
            var n = NewChain(new Seq { L = seq[ca], RevL = !aLast, R = seq[cb], RevR = bLast }, layer[ca], count[ca] + count[cb], f, l);
            PutEnd(2 * n); PutEnd(2 * n + 1);
            var fresh = new List<(int, int)>(); PairsOf(2 * n, fresh); PairsOf(2 * n + 1, fresh);
            Refresh(at, fresh);
            return n;
        }

        var lastJoined = -1;
        for (var round = 0; round < 100_000; round++)
        {
            ct.ThrowIfCancellationRequested();
            rounds = round + 1;
            if (round == 0)
            {
                for (var c = seq.Count - 1; c >= 0; c--) TryClose(c, false);
                // Enden und Paare einmal aufbauen
                for (var c = 0; c < seq.Count; c++) if (alive[c]) { PutEnd(2 * c); PutEnd(2 * c + 1); }
                var fresh = new List<(int, int)>();
                for (var c = 0; c < seq.Count && !Over(); c++) if (alive[c]) { PairsOf(2 * c, fresh); PairsOf(2 * c + 1, fresh); }
                if (Over()) { budgetOut = true; break; }
                foreach (var key in fresh)
                {
                    var pi = pairs[key]; pi.Amb = Amb(key.Item1, key.Item2, pi.D); Place(key.Item1, key.Item2, pi);
                    if (work > budget) { budgetOut = true; break; }
                }
                if (budgetOut) break;
            }
            else if (lastJoined >= 0) TryClose(lastJoined, true);
            if (pairs.Count == 0) break;
            if (Over()) { budgetOut = true; break; }
            var pick = FastRound() ?? ExactRound();
            if (pick.A < 0) break;
            lastJoined = Merge(pick.A, pick.B);
        }
        if (budgetOut) notes?.Add(BridgeBudgetNote);
        LastBridgeStats = (rounds, exactRounds, work, budgetOut);
        foreach (var c in closedOrder) done.Add(new CadContour(Flatten(seq[c], count[c]), true, layer[c]));
        foreach (var c in order) if (alive[c]) done.Add(new CadContour(Flatten(seq[c], count[c]), false, layer[c]));
    }

    /// <summary>T-0772: Sortieren wie <see cref="List{T}.Sort(Comparison{T})"/> (.NET-Introsort: Dreiermedian, Teilen, bis 16 Einfügen, bei zu tiefer
    /// Rekursion Heapsort) – aber nur so weit, wie Elemente abgeholt werden: Teilbereiche rechts werden erst sortiert, wenn man dort ankommt.
    /// Gleich weite Elemente landen dadurch genau dort, wo List.Sort sie hinlegt. Ob das auf der laufenden .NET-Version stimmt, prüft
    /// <see cref="MatchesRuntime"/> einmal gegen List.Sort; sonst wird ganz mit List.Sort sortiert.</summary>
    internal sealed class IntroPrefix((double D, int A, int B)[] keys, Action tick)
    {
        const int Threshold = 16;
        static int Cmp((double D, int A, int B) x, (double D, int A, int B) y) => x.D.CompareTo(y.D);

        public IEnumerable<(double D, int A, int B)> Items()
        {
            if (keys.Length <= 1) { foreach (var k in keys) yield return k; yield break; }
            var st = new Stack<(int Lo, int Len, int Depth, bool Done)>();
            st.Push((0, keys.Length, 2 * (System.Numerics.BitOperations.Log2((uint)keys.Length) + 1), false));
            while (st.Count > 0)
            {
                var (lo, len, depth, done) = st.Pop();
                if (done || len <= 1) { for (var i = lo; i < lo + len; i++) yield return keys[i]; continue; }
                if (len <= Threshold)
                {
                    if (len == 2) SwapIfGreater(lo, lo + 1);
                    else if (len == 3) { SwapIfGreater(lo, lo + 1); SwapIfGreater(lo, lo + 2); SwapIfGreater(lo + 1, lo + 2); }
                    else InsertionSort(lo, len);
                    st.Push((lo, len, 0, true)); continue;
                }
                if (depth == 0) { HeapSort(lo, len); st.Push((lo, len, 0, true)); continue; }
                var p = Partition(lo, len);
                st.Push((lo + p + 1, len - p - 1, depth - 1, false)); // rechts (später)
                st.Push((lo + p, 1, 0, true)); // Teiler steht schon richtig
                st.Push((lo, p, depth - 1, false)); // links zuerst
            }
        }

        void Swap(int i, int j) => (keys[i], keys[j]) = (keys[j], keys[i]);
        void SwapIfGreater(int i, int j) { tick(); if (Cmp(keys[i], keys[j]) > 0) Swap(i, j); }

        int Partition(int lo, int len)
        {
            var hi = len - 1; var middle = hi >> 1;
            SwapIfGreater(lo, lo + middle); SwapIfGreater(lo, lo + hi); SwapIfGreater(lo + middle, lo + hi);
            var pivot = keys[lo + middle];
            Swap(lo + middle, lo + hi - 1);
            int left = 0, right = hi - 1;
            while (left < right)
            {
                while (Cmp(keys[lo + ++left], pivot) < 0) tick();
                while (Cmp(pivot, keys[lo + --right]) < 0) tick();
                if (left >= right) break;
                Swap(lo + left, lo + right);
            }
            if (left != hi - 1) Swap(lo + left, lo + hi - 1);
            return left;
        }

        void HeapSort(int lo, int n)
        {
            for (var i = n >> 1; i >= 1; i--) DownHeap(lo, i, n);
            for (var i = n; i > 1; i--) { Swap(lo, lo + i - 1); DownHeap(lo, 1, i - 1); }
        }

        void DownHeap(int lo, int i, int n)
        {
            var d = keys[lo + i - 1];
            while (i <= n >> 1)
            {
                tick();
                var child = 2 * i;
                if (child < n && Cmp(keys[lo + child - 1], keys[lo + child]) < 0) child++;
                if (!(Cmp(d, keys[lo + child - 1]) < 0)) break;
                keys[lo + i - 1] = keys[lo + child - 1]; i = child;
            }
            keys[lo + i - 1] = d;
        }

        void InsertionSort(int lo, int len)
        {
            for (var i = 0; i < len - 1; i++)
            {
                var t = keys[lo + i + 1]; var j = i;
                while (j >= 0 && Cmp(t, keys[lo + j]) < 0) { tick(); keys[lo + j + 1] = keys[lo + j]; j--; }
                keys[lo + j + 1] = t;
            }
        }

        static readonly Lazy<bool> Check = new(() =>
        {
            var r = new Random(4711);
            foreach (var n in new[] { 2, 3, 5, 16, 17, 40, 300, 2048, 9000 })
                foreach (var distinct in new[] { 1, 3, 50, 1_000_000 })
                {
                    var a = Enumerable.Range(0, n).Select(i => ((double)r.Next(distinct), i, 0)).ToArray();
                    var list = new List<(double D, int A, int B)>(a); list.Sort((x, y) => x.D.CompareTo(y.D));
                    var mine = new IntroPrefix(((double D, int A, int B)[])a.Clone(), () => { }).Items().ToList();
                    if (!list.SequenceEqual(mine)) return false;
                }
            return true;
        });

        /// <summary>Gleiche Reihenfolge wie List.Sort auf dieser .NET-Version (einmal geprüft, auch mit vielen gleichen Werten).</summary>
        public static bool MatchesRuntime => Check.Value;
    }
}
