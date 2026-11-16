using System.Globalization;

namespace Trendo.Core;

/// <summary>T-0691: Bearbeitbares, indiziertes Dreiecksnetz eines Teils (Punkte, Kanten, Flächen) – wie der Bearbeitungsmodus in Blender.
/// Koordinaten sind lokal zum Teil (Zentimeter, ohne Drehung und Lage); <see cref="ToWorld"/> rechnet in die Szene um.
/// Flächen sind Dreiecke. Alle Werkzeuge ändern nur dieses Netz; Rückgängig macht das 3D-Studio über seinen Szenen-Stapel.</summary>
public sealed class EditMesh
{
    public List<Vec3> V { get; } = [];
    /// <summary>Dreiecke als Punktnummern, je drei hintereinander (gegen den Uhrzeigersinn von außen gesehen).</summary>
    public List<int> T { get; } = [];
    public int Faces => T.Count / 3;

    /// <summary>Netz eines Teils zum Bearbeiten: Grundformen (samt Abrunden, Loch, Spiegeln …) werden in Dreiecke umgerechnet und gleiche Punkte verschmolzen.</summary>
    public static EditMesh FromPart(Part3D part)
    {
        var local = Mesh3D.Welded(Mesh3D.LocalMesh(part));
        var e = new EditMesh();
        double sx = 1, sy = 1, sz = 1;
        if (part.Shape == "mesh")
        {
            sx = Math.Abs(part.BaseX) < 1e-8 ? 1 : part.Sx / part.BaseX;
            sy = Math.Abs(part.BaseY) < 1e-8 ? 1 : part.Sy / part.BaseY;
            sz = Math.Abs(part.BaseZ) < 1e-8 ? 1 : part.Sz / part.BaseZ;
        }
        foreach (var v in local.V) e.V.Add(new Vec3(v.X * sx, v.Y * sy, v.Z * sz));
        e.T.AddRange(local.I);
        return e;
    }

    /// <summary>Netz zurück ins Teil schreiben: das Teil ist danach ein Netz („Datei“-Form), Maße = Umfang des Netzes, Werkzeuge sind eingerechnet.</summary>
    public void WriteTo(Part3D part)
    {
        Compact();
        var pos = new float[V.Count * 3];
        for (var i = 0; i < V.Count; i++) { pos[i * 3] = (float)V[i].X; pos[i * 3 + 1] = (float)V[i].Y; pos[i * 3 + 2] = (float)V[i].Z; }
        part.Positions = pos; part.Indices = [.. T];
        var (mn, mx) = Bounds();
        double Ext(double a, double b) => Math.Max(1e-4, Math.Round(b - a, 6));
        part.Sx = part.BaseX = V.Count == 0 ? 1 : Ext(mn.X, mx.X);
        part.Sy = part.BaseY = V.Count == 0 ? 1 : Ext(mn.Y, mx.Y);
        part.Sz = part.BaseZ = V.Count == 0 ? 1 : Ext(mn.Z, mx.Z);
        // Positions wird als float gespeichert: Base aus genau diesen Werten, damit kein Maßstab ≠ 1 entsteht
        if (V.Count > 0)
        {
            float Mn(int o) { var m = float.MaxValue; for (var i = o; i < pos.Length; i += 3) m = Math.Min(m, pos[i]); return m; }
            float Mx(int o) { var m = float.MinValue; for (var i = o; i < pos.Length; i += 3) m = Math.Max(m, pos[i]); return m; }
            part.Sx = part.BaseX = Math.Max(1e-4, Mx(0) - (double)Mn(0)); part.Sy = part.BaseY = Math.Max(1e-4, Mx(1) - (double)Mn(1)); part.Sz = part.BaseZ = Math.Max(1e-4, Mx(2) - (double)Mn(2));
        }
        part.Shape = "mesh"; part.Ops.Clear(); part.Text = "";
    }

    public (Vec3 Min, Vec3 Max) Bounds()
    {
        if (V.Count == 0) return (new(0, 0, 0), new(0, 0, 0));
        return (new(V.Min(v => v.X), V.Min(v => v.Y), V.Min(v => v.Z)), new(V.Max(v => v.X), V.Max(v => v.Y), V.Max(v => v.Z)));
    }

    public static Vec3 ToWorld(Vec3 local, Part3D part) => Mesh3D.Rot(local, part.Rx, part.Ry, part.Rz) + new Vec3(part.X, part.Y, part.Z);

    /// <summary>Richtung oder Verschiebung aus der Szene in die lokalen Koordinaten des Teils (Drehung rückwärts).</summary>
    public static Vec3 DirToLocal(Vec3 world, Part3D part)
    {
        static Vec3 Rx(Vec3 p, double a) { var c = Math.Cos(a); var s = Math.Sin(a); return new(p.X, p.Y * c - p.Z * s, p.Y * s + p.Z * c); }
        static Vec3 Ry(Vec3 p, double a) { var c = Math.Cos(a); var s = Math.Sin(a); return new(p.X * c + p.Z * s, p.Y, -p.X * s + p.Z * c); }
        static Vec3 Rz(Vec3 p, double a) { var c = Math.Cos(a); var s = Math.Sin(a); return new(p.X * c - p.Y * s, p.X * s + p.Y * c, p.Z); }
        const double D = Math.PI / 180;
        return Rx(Ry(Rz(world, -part.Rz * D), -part.Ry * D), -part.Rx * D);
    }

    /// <summary>Alle Kanten (einmal, kleinere Nummer zuerst).</summary>
    public List<(int A, int B)> Edges()
    {
        var set = new HashSet<(int, int)>();
        for (var t = 0; t < T.Count; t += 3)
            for (var k = 0; k < 3; k++) set.Add(Key(T[t + k], T[t + (k + 1) % 3]));
        return [.. set];
    }

    public static (int A, int B) Key(int a, int b) => a < b ? (a, b) : (b, a);

    public Vec3 FaceNormal(int f)
    {
        var a = V[T[f * 3]]; var b = V[T[f * 3 + 1]]; var c = V[T[f * 3 + 2]];
        return Vec3.Cross(b - a, c - a).Norm();
    }

    public Vec3 Centroid(IEnumerable<int> verts)
    {
        double x = 0, y = 0, z = 0; var n = 0;
        foreach (var i in verts) { x += V[i].X; y += V[i].Y; z += V[i].Z; n++; }
        return n == 0 ? new(0, 0, 0) : new(x / n, y / n, z / n);
    }

    /// <summary>Kanten, die nicht genau zwei Flächen haben (Löcher, offene Ränder, Verzweigungen).</summary>
    public int OpenEdges()
    {
        var count = new Dictionary<(int, int), int>();
        for (var t = 0; t < T.Count; t += 3)
            for (var k = 0; k < 3; k++) { var e = Key(T[t + k], T[t + (k + 1) % 3]); count[e] = count.GetValueOrDefault(e) + 1; }
        return count.Values.Count(c => c != 2);
    }

    /// <summary>Eingeschlossenes Volumen (cm³; bei geschlossenen, nach außen zeigenden Netzen positiv).</summary>
    public double Volume()
    {
        double s = 0;
        for (var t = 0; t < T.Count; t += 3) s += Vec3.Dot(V[T[t]], Vec3.Cross(V[T[t + 1]], V[T[t + 2]]));
        return s / 6;
    }

    // ---------- Umformen ----------
    public void Translate(IReadOnlyCollection<int> verts, Vec3 by) { foreach (var i in verts) V[i] = V[i] + by; }

    /// <summary>Um eine Achse (lokal, wird normiert) durch <paramref name="center"/> drehen, Winkel in Grad.</summary>
    public void Rotate(IReadOnlyCollection<int> verts, Vec3 axis, double degrees, Vec3 center)
    {
        var k = axis.Norm(); var a = degrees * Math.PI / 180; var c = Math.Cos(a); var s = Math.Sin(a);
        foreach (var i in verts)
        {
            var p = V[i] - center; // Rodrigues
            var r = p * c + Vec3.Cross(k, p) * s + k * (Vec3.Dot(k, p) * (1 - c));
            V[i] = center + r;
        }
    }

    /// <summary>Gleichmäßig skalieren um <paramref name="center"/>.</summary>
    public void Scale(IReadOnlyCollection<int> verts, double factor, Vec3 center) { foreach (var i in verts) V[i] = center + (V[i] - center) * factor; }

    /// <summary>Entlang einer Richtung (lokal, wird normiert) skalieren.</summary>
    public void ScaleAlong(IReadOnlyCollection<int> verts, Vec3 dir, double factor, Vec3 center)
    {
        var d = dir.Norm();
        foreach (var i in verts) { var p = V[i] - center; V[i] = center + p + d * (Vec3.Dot(p, d) * (factor - 1)); }
    }

    // ---------- Topologie ----------
    /// <summary>Flächen herausziehen: die gewählten Flächen lösen sich als Block, wandern um <paramref name="distance"/> entlang ihrer
    /// mittleren Normale, an den Außenkanten entstehen Seitenwände. Rückgabe: die neuen (verschobenen) Flächen.</summary>
    public List<int> Extrude(IReadOnlyCollection<int> faces, double distance)
    {
        if (faces.Count == 0) throw new MeshEditError("Zum Extrudieren zuerst Flächen wählen.");
        var sel = faces.Where(f => f >= 0 && f < Faces).ToHashSet();
        var edgeUse = new Dictionary<(int, int), int>();
        foreach (var f in sel) for (var k = 0; k < 3; k++) { var e = Key(T[f * 3 + k], T[f * 3 + (k + 1) % 3]); edgeUse[e] = edgeUse.GetValueOrDefault(e) + 1; }
        var n = new Vec3(0, 0, 0);
        foreach (var f in sel) n += FaceNormal(f);
        n = n.Len < 1e-9 ? new Vec3(0, 0, 1) : n.Norm();
        var map = new Dictionary<int, int>();
        int Dup(int v) { if (map.TryGetValue(v, out var x)) return x; V.Add(V[v] + n * distance); return map[v] = V.Count - 1; }
        var sides = new List<(int A, int B)>();
        foreach (var f in sel)
            for (var k = 0; k < 3; k++)
            {
                var a = T[f * 3 + k]; var b = T[f * 3 + (k + 1) % 3];
                if (edgeUse[Key(a, b)] == 1) sides.Add((a, b)); // Außenkante der Auswahl, Richtung wie in der Fläche
            }
        foreach (var f in sel) for (var k = 0; k < 3; k++) T[f * 3 + k] = Dup(T[f * 3 + k]);
        foreach (var (a, b) in sides)
        {
            var na = map[a]; var nb = map[b];
            T.AddRange([a, b, nb, a, nb, na]);
        }
        return [.. sel];
    }

    /// <summary>Flächen entfernen; Punkte ohne Fläche verschwinden mit.</summary>
    public int DeleteFaces(IReadOnlyCollection<int> faces)
    {
        var sel = faces.ToHashSet(); var keep = new List<int>(T.Count);
        for (var f = 0; f < Faces; f++) if (!sel.Contains(f)) keep.AddRange([T[f * 3], T[f * 3 + 1], T[f * 3 + 2]]);
        var removed = Faces - keep.Count / 3;
        T.Clear(); T.AddRange(keep); Compact();
        return removed;
    }

    /// <summary>Punkte entfernen – mit allen Flächen, die sie berühren.</summary>
    public int DeleteVerts(IReadOnlyCollection<int> verts)
    {
        var sel = verts.ToHashSet();
        return DeleteFaces(Enumerable.Range(0, Faces).Where(f => sel.Contains(T[f * 3]) || sel.Contains(T[f * 3 + 1]) || sel.Contains(T[f * 3 + 2])).ToList());
    }

    /// <summary>Kanten entfernen – mit den angrenzenden Flächen (die Punkte bleiben, solange sie noch eine Fläche haben).</summary>
    public int DeleteEdges(IReadOnlyCollection<(int A, int B)> edges)
    {
        var sel = edges.Select(e => Key(e.A, e.B)).ToHashSet();
        return DeleteFaces(Enumerable.Range(0, Faces).Where(f => Enumerable.Range(0, 3).Any(k => sel.Contains(Key(T[f * 3 + k], T[f * 3 + (k + 1) % 3])))).ToList());
    }

    /// <summary>Neue Fläche aus 3 oder 4 gewählten Punkten (wie „F“ in Blender). Die Fläche zeigt vom Netz weg nach außen.</summary>
    public List<int> FillFace(IReadOnlyList<int> verts)
    {
        var ids = verts.Distinct().ToList();
        if (ids.Count is < 3 or > 4) throw new MeshEditError("Für eine neue Fläche genau 3 oder 4 Punkte wählen.");
        var c = Centroid(ids);
        var normal = new Vec3(0, 0, 0);
        for (var i = 0; i < ids.Count; i++) normal += Vec3.Cross(V[ids[i]] - c, V[ids[(i + 1) % ids.Count]] - c);
        // Punkte rund um die Mitte ordnen (in der Ebene der Fläche)
        var nn = normal.Len < 1e-12 ? Vec3.Cross(V[ids[1]] - V[ids[0]], V[ids[2]] - V[ids[0]]).Norm() : normal.Norm();
        var u = (V[ids[0]] - c).Norm(); var w = Vec3.Cross(nn, u);
        ids = [.. ids.OrderBy(i => Math.Atan2(Vec3.Dot(V[i] - c, w), Vec3.Dot(V[i] - c, u)))];
        var tris = ids.Count == 3 ? new List<int[]> { new[] { ids[0], ids[1], ids[2] } } : [new[] { ids[0], ids[1], ids[2] }, new[] { ids[0], ids[2], ids[3] }];
        if (tris.All(t => Vec3.Cross(V[t[1]] - V[t[0]], V[t[2]] - V[t[0]]).Len < 1e-12)) throw new MeshEditError("Die Punkte liegen auf einer Linie – daraus wird keine Fläche.");
        // nach außen: weg von der Mitte des übrigen Netzes
        var meshCenter = Centroid(Enumerable.Range(0, V.Count));
        var fn = Vec3.Cross(V[tris[0][1]] - V[tris[0][0]], V[tris[0][2]] - V[tris[0][0]]);
        var flip = Vec3.Dot(fn, c - meshCenter) < 0;
        // gibt es schon eine Nachbarfläche, richtet sich die neue nach ihr (gemeinsame Kante läuft dort andersherum)
        var directed = new HashSet<(int, int)>();
        for (var t = 0; t < T.Count; t += 3) for (var k = 0; k < 3; k++) directed.Add((T[t + k], T[t + (k + 1) % 3]));
        foreach (var t in tris)
            for (var k = 0; k < 3; k++)
            {
                if (directed.Contains((t[k], t[(k + 1) % 3]))) { flip = true; goto decided; }
                if (directed.Contains((t[(k + 1) % 3], t[k]))) { flip = false; goto decided; }
            }
        decided:
        var existing = new HashSet<string>();
        for (var f = 0; f < Faces; f++) existing.Add(FaceKey(T[f * 3], T[f * 3 + 1], T[f * 3 + 2]));
        var added = new List<int>();
        foreach (var t in tris)
        {
            if (existing.Contains(FaceKey(t[0], t[1], t[2]))) throw new MeshEditError("Diese Fläche gibt es schon.");
            if (flip) T.AddRange([t[0], t[2], t[1]]); else T.AddRange(t);
            added.Add(Faces - 1);
        }
        return added;
    }

    static string FaceKey(int a, int b, int c) { var s = new[] { a, b, c }; Array.Sort(s); return string.Join(",", s); }

    /// <summary>Kante in der Mitte (oder bei <paramref name="t"/>) teilen: neuer Punkt, angrenzende Dreiecke werden je in zwei geteilt.</summary>
    public int SplitEdge(int a, int b, double t = 0.5)
    {
        var faces = new List<int>();
        for (var f = 0; f < Faces; f++)
            for (var k = 0; k < 3; k++) if (Key(T[f * 3 + k], T[f * 3 + (k + 1) % 3]) == Key(a, b)) { faces.Add(f); break; }
        if (faces.Count == 0) throw new MeshEditError("Diese Kante gibt es nicht.");
        V.Add(V[a] + (V[b] - V[a]) * t); var m = V.Count - 1;
        foreach (var f in faces)
        {
            int[] tri = [T[f * 3], T[f * 3 + 1], T[f * 3 + 2]];
            var k = Enumerable.Range(0, 3).First(i => Key(tri[i], tri[(i + 1) % 3]) == Key(a, b));
            var p = tri[k]; var q = tri[(k + 1) % 3]; var r = tri[(k + 2) % 3];
            T[f * 3] = p; T[f * 3 + 1] = m; T[f * 3 + 2] = r; // p → m → r
            T.AddRange([m, q, r]);                            // m → q → r
        }
        return m;
    }

    /// <summary>Gewählte Punkte in ihrer Mitte zu einem verschmelzen; dabei entartete Flächen fallen weg. Rückgabe: Nummer des verbliebenen Punkts.</summary>
    public int MergeVerts(IReadOnlyCollection<int> verts)
    {
        var ids = verts.Distinct().ToList();
        if (ids.Count < 2) throw new MeshEditError("Zum Verschmelzen mindestens 2 Punkte wählen.");
        var keep = ids.Min(); V[keep] = Centroid(ids);
        var set = ids.ToHashSet();
        for (var i = 0; i < T.Count; i++) if (set.Contains(T[i])) T[i] = keep;
        DropDegenerate();
        var before = V[keep]; Compact();
        return V.FindIndex(v => v.X == before.X && v.Y == before.Y && v.Z == before.Z);
    }

    /// <summary>Punkte, die näher als <paramref name="distance"/> beieinander liegen, verschmelzen (wie „Nach Abstand verschmelzen“). Rückgabe: Anzahl entfernter Punkte.</summary>
    public int MergeByDistance(double distance)
    {
        var before = V.Count; var cell = Math.Max(distance, 1e-9);
        var grid = new Dictionary<(long, long, long), List<int>>(); var remap = new int[V.Count];
        for (var i = 0; i < V.Count; i++)
        {
            var v = V[i]; var key = ((long)Math.Floor(v.X / cell), (long)Math.Floor(v.Y / cell), (long)Math.Floor(v.Z / cell));
            var found = -1;
            for (var dx = -1; dx <= 1 && found < 0; dx++) for (var dy = -1; dy <= 1 && found < 0; dy++) for (var dz = -1; dz <= 1 && found < 0; dz++)
                        if (grid.TryGetValue((key.Item1 + dx, key.Item2 + dy, key.Item3 + dz), out var l))
                            foreach (var j in l) if ((V[j] - v).Len <= distance) { found = j; break; }
            if (found >= 0) remap[i] = found;
            else { remap[i] = i; if (!grid.TryGetValue(key, out var list)) grid[key] = list = []; list.Add(i); }
        }
        for (var i = 0; i < T.Count; i++) T[i] = remap[T[i]];
        DropDegenerate(); Compact();
        return before - V.Count;
    }

    /// <summary>Normalen neu berechnen: Flächen eines zusammenhängenden Stücks gleich ausrichten (gemeinsame Kanten gegenläufig),
    /// geschlossene Stücke zeigen danach nach außen (positives Volumen). Rückgabe: Anzahl umgedrehter Flächen.</summary>
    public int RecalcNormals()
    {
        var nf = Faces; var turned = new bool[nf];
        var byEdge = new Dictionary<(int, int), List<int>>();
        for (var f = 0; f < nf; f++) for (var k = 0; k < 3; k++) { var e = Key(T[f * 3 + k], T[f * 3 + (k + 1) % 3]); if (!byEdge.TryGetValue(e, out var l)) byEdge[e] = l = []; l.Add(f); }
        var seen = new bool[nf];
        bool Has(int f, int a, int b) { for (var k = 0; k < 3; k++) if (T[f * 3 + k] == a && T[f * 3 + (k + 1) % 3] == b) return true; return false; }
        void Flip(int f) { (T[f * 3 + 1], T[f * 3 + 2]) = (T[f * 3 + 2], T[f * 3 + 1]); turned[f] = !turned[f]; }
        for (var start = 0; start < nf; start++)
        {
            if (seen[start]) continue;
            var comp = new List<int>(); var q = new Queue<int>(); q.Enqueue(start); seen[start] = true;
            while (q.Count > 0)
            {
                var f = q.Dequeue(); comp.Add(f);
                for (var k = 0; k < 3; k++)
                {
                    var a = T[f * 3 + k]; var b = T[f * 3 + (k + 1) % 3];
                    foreach (var g in byEdge[Key(a, b)])
                    {
                        if (seen[g]) continue;
                        if (Has(g, a, b)) Flip(g); // Nachbar läuft gleich herum → umdrehen
                        seen[g] = true; q.Enqueue(g);
                    }
                }
            }
            double vol = 0;
            foreach (var f in comp) vol += Vec3.Dot(V[T[f * 3]], Vec3.Cross(V[T[f * 3 + 1]], V[T[f * 3 + 2]]));
            if (vol < 0) foreach (var f in comp) Flip(f);
        }
        return turned.Count(x => x);
    }

    /// <summary>Gewählte Flächen umdrehen.</summary>
    public void FlipFaces(IReadOnlyCollection<int> faces) { foreach (var f in faces) (T[f * 3 + 1], T[f * 3 + 2]) = (T[f * 3 + 2], T[f * 3 + 1]); }

    void DropDegenerate()
    {
        var keep = new List<int>(T.Count);
        for (var t = 0; t < T.Count; t += 3)
        {
            var a = T[t]; var b = T[t + 1]; var c = T[t + 2];
            if (a == b || b == c || a == c) continue;
            keep.AddRange([a, b, c]);
        }
        T.Clear(); T.AddRange(keep);
    }

    /// <summary>Punkte ohne Fläche entfernen und neu nummerieren.</summary>
    public void Compact()
    {
        var used = new int[V.Count]; Array.Fill(used, -1);
        var nv = new List<Vec3>();
        for (var i = 0; i < T.Count; i++)
        {
            var v = T[i];
            if (used[v] < 0) { used[v] = nv.Count; nv.Add(V[v]); }
            T[i] = used[v];
        }
        V.Clear(); V.AddRange(nv);
    }

    /// <summary>Kurzbeschreibung für die Anzeige.</summary>
    public string Stats()
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        return string.Format(de, "{0:N0} Punkte · {1:N0} Kanten · {2:N0} Flächen", V.Count, Edges().Count, Faces);
    }
}

public sealed class MeshEditError(string message) : Exception(message);

public static partial class Mesh3D
{
    /// <summary>Netz eines Teils in seinen eigenen Koordinaten, mit allen Werkzeugen (für den Bearbeitungsmodus).</summary>
    public static Mesh LocalMesh(Part3D part) => BuildLocal(part);
    /// <summary>Gleiche Punkte (auf 1/1000 cm) zusammenfassen – aus einer Dreiecksliste wird ein zusammenhängendes Netz.</summary>
    public static Mesh Welded(Mesh m) => Weld(m);
}
