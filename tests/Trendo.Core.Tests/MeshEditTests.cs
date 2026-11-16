using Trendo.Core.Cad;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0691: Netzbearbeitung (Punkte, Kanten, Flächen) und Import/Export als bearbeitbares Netz.</summary>
public class MeshEditTests
{
    static string Fixture(string name)
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) d = d.Parent;
        return Path.Combine(d!.FullName, "tests", "fixtures", "t0691", name);
    }

    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-netz-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    /// <summary>Belege für reports/T-0691: ist TRENDO_BELEG_DIR gesetzt, werden die exportierten Dateien dorthin kopiert (für die Prüfung mit Blender, Validator, Inkscape).</summary>
    static void Beleg(string file)
    {
        if (Environment.GetEnvironmentVariable("TRENDO_BELEG_DIR") is not { Length: > 0 } d) return;
        Directory.CreateDirectory(d); File.Copy(file, Path.Combine(d, Path.GetFileName(file)), true);
        var mtl = Path.ChangeExtension(file, ".mtl"); if (File.Exists(mtl)) File.Copy(mtl, Path.Combine(d, Path.GetFileName(mtl)), true);
    }

    static EditMesh Cube() => EditMesh.FromPart(new Part3D { Shape = "cube", Sx = 10, Sy = 10, Sz = 10 });

    [Fact(DisplayName = "T-0691 Netz: Würfel wird ein verschweißtes Netz (8 Punkte, 12 Flächen, 18 Kanten, geschlossen, 1000 cm³)")]
    public void Cube_becomes_indexed_mesh()
    {
        var m = Cube();
        Assert.Equal(8, m.V.Count); Assert.Equal(12, m.Faces); Assert.Equal(18, m.Edges().Count);
        Assert.Equal(0, m.OpenEdges());
        Assert.Equal(1000, m.Volume(), 3);
    }

    [Fact(DisplayName = "T-0691 Netz: Fläche oben extrudieren – 5 cm höher, Volumen 1500 cm³, weiterhin geschlossen")]
    public void Extrude_top()
    {
        var m = Cube();
        var top = Enumerable.Range(0, m.Faces).Where(f => m.FaceNormal(f).Z > 0.9).ToList();
        Assert.Equal(2, top.Count);
        m.Extrude(top, 5);
        Assert.Equal(0, m.OpenEdges());
        Assert.Equal(1500, m.Volume(), 3);
        Assert.Equal(10, m.Bounds().Max.Z, 6); // Würfel liegt mittig: −5 … +5, oben jetzt +10
        Assert.Equal(12, m.V.Count); Assert.Equal(20, m.Faces);
    }

    [Fact(DisplayName = "T-0691 Netz: Punkte verschieben, drehen, skalieren wirken nur auf die Auswahl")]
    public void Transform_selection()
    {
        var m = Cube();
        var top = Enumerable.Range(0, m.V.Count).Where(i => m.V[i].Z > 0).ToList();
        m.Translate(top, new Vec3(0, 0, 2)); Assert.Equal(7, m.Bounds().Max.Z, 6); Assert.Equal(-5, m.Bounds().Min.Z, 6);
        m.Scale(top, 2, m.Centroid(top)); Assert.Equal(10, m.Bounds().Max.X, 6);
        Assert.Equal(2800, m.Volume(), 6); // Pyramidenstumpf: h = 12, unten 10×10, oben 20×20 → 12/3·(100 + 400 + 200)
        var corner = top.First(i => m.V[i].X > 9 && m.V[i].Y > 9);
        m.Rotate(top, new Vec3(0, 0, 1), 90, m.Centroid(top));
        Assert.Equal(-10, m.V[corner].X, 6); Assert.Equal(10, m.V[corner].Y, 6); Assert.Equal(7, m.V[corner].Z, 6);
        var bottom = Enumerable.Range(0, m.V.Count).Except(top).ToList();
        Assert.All(bottom, i => Assert.Equal(-5, m.V[i].Z, 6)); // unten unverändert
        m.ScaleAlong(bottom, new Vec3(1, 0, 0), 3, new Vec3(0, 0, -5));
        Assert.Equal(15, bottom.Max(i => m.V[i].X), 6); Assert.Equal(5, bottom.Max(i => m.V[i].Y), 6);
    }

    [Fact(DisplayName = "T-0691 Netz: Fläche löschen öffnet das Netz, Fläche aus 4 Punkten füllen schließt es wieder (nach außen gerichtet)")]
    public void Delete_and_fill()
    {
        var m = Cube();
        var top = Enumerable.Range(0, m.Faces).Where(f => m.FaceNormal(f).Z > 0.9).ToList();
        Assert.Equal(2, m.DeleteFaces(top));
        Assert.Equal(4, m.OpenEdges());
        var ring = Enumerable.Range(0, m.V.Count).Where(i => m.V[i].Z > 0).ToList();
        var added = m.FillFace(ring);
        Assert.Equal(2, added.Count); Assert.Equal(0, m.OpenEdges());
        Assert.True(added.All(f => m.FaceNormal(f).Z > 0.9), "neue Fläche zeigt nach außen");
        Assert.Equal(1000, m.Volume(), 3);
        Assert.Throws<MeshEditError>(() => m.FillFace(ring)); // gibt es schon
    }

    [Fact(DisplayName = "T-0691 Netz: Kante teilen, Punkte verschmelzen, nach Abstand verschmelzen")]
    public void Split_and_merge()
    {
        var m = Cube(); var (a, b) = m.Edges()[0];
        var mid = m.SplitEdge(a, b);
        Assert.Equal(9, m.V.Count); Assert.Equal(14, m.Faces); Assert.Equal(0, m.OpenEdges());
        Assert.Equal(1000, m.Volume(), 3); // Teilen ändert die Form nicht
        Assert.Equal((m.V[a] + m.V[b]) * 0.5 is var c ? c.X : 0, m.V[mid].X, 6);
        var keep = m.MergeVerts([mid, a]);
        Assert.Equal(8, m.V.Count); Assert.True(keep >= 0);
        // Abstand: ein doppelter Punkt wird zusammengeführt
        var n = Cube(); n.V.Add(n.V[0] + new Vec3(0.0001, 0, 0)); n.T[0] = n.V.Count - 1;
        Assert.Equal(1, n.MergeByDistance(0.001)); Assert.Equal(8, n.V.Count); Assert.Equal(0, n.OpenEdges());
    }

    [Fact(DisplayName = "T-0691 Netz: Normalen neu berechnen richtet umgedrehte Flächen wieder nach außen")]
    public void Recalc_normals()
    {
        var m = Cube();
        m.FlipFaces([0, 3, 7]);
        Assert.NotEqual(1000, m.Volume(), 3);
        Assert.Equal(3, m.RecalcNormals());
        Assert.Equal(1000, m.Volume(), 3);
        m.FlipFaces(Enumerable.Range(0, m.Faces).ToList()); // alles nach innen
        Assert.True(m.Volume() < 0); m.RecalcNormals(); Assert.Equal(1000, m.Volume(), 3);
    }

    [Fact(DisplayName = "T-0691 Netz: ins Teil zurückschreiben – Lage, Drehung und Form bleiben, Werkzeuge (Abrunden) sind eingerechnet")]
    public void Write_back_keeps_world_geometry()
    {
        var p = new Part3D { Shape = "cylinder", Sx = 8, Sy = 8, Sz = 12, X = 5, Y = -3, Z = 6, Rz = 30, Ops = { new PartOp { Kind = "mirror", Axis = "x" } } };
        var before = Mesh3D.BuildWorld(p); var vb = before.V.Select(v => (Math.Round(v.X, 3), Math.Round(v.Y, 3), Math.Round(v.Z, 3))).ToHashSet();
        var m = EditMesh.FromPart(p); m.WriteTo(p);
        Assert.Equal("mesh", p.Shape); Assert.Empty(p.Ops);
        var after = Mesh3D.BuildWorld(p);
        Assert.Equal(before.Triangles, after.Triangles);
        Assert.All(after.V, v => Assert.Contains((Math.Round(v.X, 3), Math.Round(v.Y, 3), Math.Round(v.Z, 3)), vb));
        // Welt ↔ lokal: Verschiebung in der Szene entlang X wird lokal gedreht
        var d = EditMesh.DirToLocal(new Vec3(1, 0, 0), p);
        var back = EditMesh.ToWorld(d, p) - EditMesh.ToWorld(new Vec3(0, 0, 0), p);
        Assert.Equal(1, back.X, 9); Assert.Equal(0, back.Y, 9);
    }

    /// <summary>Rundreise mit einer Datei, die Blender geschrieben hat (tests/fixtures/t0691/affe-blender.glb – Suzanne, 968 Dreiecke).</summary>
    [Theory(DisplayName = "T-0691 Rundreise: Blender-Datei importieren → Punkt verschieben → als glTF/GLB/OBJ/STL exportieren → wieder einlesen → Geometrie stimmt")]
    [InlineData("affe-blender.glb", ".gltf")]
    [InlineData("affe-blender.glb", ".glb")]
    [InlineData("affe-blender.obj", ".obj")]
    [InlineData("affe-blender.stl", ".stl")]
    public void Roundtrip_foreign_file(string fixture, string ext)
    {
        var dir = Tmp();
        try
        {
            var parts = Mesh3D.ImportParts(Fixture(fixture), "cm");
            var part = Assert.Single(parts);
            var m = EditMesh.FromPart(part);
            var faces = m.Faces; // Suzanne: 968 Dreiecke, eines fällt beim Verschweißen als entartet weg
            Assert.InRange(faces, 960, 968); Assert.True(m.V.Count is > 400 and < 600, "verschweißt: " + m.V.Count);
            var top = Enumerable.Range(0, m.V.Count).OrderByDescending(i => m.V[i].Z).First();
            var moved = m.V[top] + new Vec3(0, 0, 7.5);
            m.Translate([top], new Vec3(0, 0, 7.5));
            m.WriteTo(part);
            var scene = new Scene3D { Parts = { part } };
            var world = Mesh3D.FromScene(scene);
            var file = Path.Combine(dir, "affe" + ext);
            switch (ext) { case ".gltf": Mesh3D.WriteGltf(world, file); break; case ".glb": Mesh3D.WriteGlb(world, file); break; case ".stl": Mesh3D.WriteStl(world, file); break; default: Mesh3D.WriteObj(world, file); break; }
            Beleg(file);
            var again = Assert.Single(Mesh3D.ImportParts(file, ext == ".stl" ? "mm" : "cm"));
            var back = Mesh3D.FromScene(new Scene3D { Parts = { again } });
            Assert.Equal(world.Triangles, back.Triangles);
            // Umfang gleich (auf 0,01 cm; STL/glTF speichern float)
            var (a0, a1) = (Min(world), Max(world)); var (b0, b1) = (Min(back), Max(back));
            Assert.Equal(a1.Z - a0.Z, b1.Z - b0.Z, 2); Assert.Equal(a1.X - a0.X, b1.X - b0.X, 2); Assert.Equal(a1.Y - a0.Y, b1.Y - b0.Y, 2);
            // der verschobene Punkt ist wieder da (relativ zur Unterkante, weil der Import die Lage ins Teil legt)
            var expected = EditMesh.ToWorld(moved, part);
            var hit = back.V.Any(v => Math.Abs(v.Z - b0.Z - (expected.Z - a0.Z)) < 0.01);
            Assert.True(hit, "verschobener Punkt fehlt nach dem Wiedereinlesen");
            Assert.Equal(faces, EditMesh.FromPart(again).Faces);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        static Vec3 Min(Mesh m) => new(m.V.Min(v => v.X), m.V.Min(v => v.Y), m.V.Min(v => v.Z));
        static Vec3 Max(Mesh m) => new(m.V.Max(v => v.X), m.V.Max(v => v.Y), m.V.Max(v => v.Z));
    }

    [Fact(DisplayName = "T-0691 glTF-Export besteht den Khronos-glTF-Validator ohne Fehler (echtes Werkzeug, falls eingerichtet)")]
    public void Gltf_passes_validator()
    {
        var exe = EchtWerkzeuge.GltfValidator();
        if (exe is null) return; // nicht eingerichtet – Beleg unter reports/T-0691 nennt den Stand
        var dir = Tmp();
        try
        {
            var p = Assert.Single(Mesh3D.ImportParts(Fixture("affe-blender.glb"), "cm"));
            var m = EditMesh.FromPart(p); m.Extrude([0, 1], 2); m.WriteTo(p);
            var world = Mesh3D.FromScene(new Scene3D { Parts = { p } });
            foreach (var ext in new[] { ".gltf", ".glb" })
            {
                var f = Path.Combine(dir, "affe" + ext);
                if (ext == ".gltf") Mesh3D.WriteGltf(world, f); else Mesh3D.WriteGlb(world, f);
                var psi = new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                psi.ArgumentList.Add("-o"); psi.ArgumentList.Add(f);
                using var pr = System.Diagnostics.Process.Start(psi)!;
                var json = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(60_000);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                Assert.True(doc.RootElement.GetProperty("issues").GetProperty("numErrors").GetInt32() == 0, ext + ": " + json);
            }
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    // ---------- CAD: DXF und STEP ----------
    [Fact(DisplayName = "T-0691 DXF: handgeschriebene Fremd-DXF lesen – Ebenen, Polylinie mit Bogen, Kreise, Bogen, Linie, Text, MTEXT; Bemaßung wird gemeldet")]
    public void Dxf_reads_foreign_file()
    {
        var (d, skipped) = CadDxf.Read(Fixture("fremd-halteplatte.dxf"));
        Assert.Equal(["0", "Kontur", "Bohrungen", "Beschriftung", "Hilfslinien"], d.Layers.Select(l => l.Name));
        Assert.False(d.Layers.Single(l => l.Name == "Hilfslinien").Visible); // Farbe −8 = Ebene aus
        Assert.Equal(1, d.Layers.Single(l => l.Name == "Kontur").Color);
        var poly = d.Entities.Single(e => e.Kind == "polylinie");
        Assert.Equal("Kontur", poly.Layer); Assert.True(poly.Closed); Assert.Equal(5, poly.Points.Count);
        Assert.Equal(0.414213562373095, poly.Points[2][2], 12);
        // Viertelkreis R10 um (70|40): ein Zwischenpunkt liegt auf dem Kreis
        var outline = poly.Outline(64);
        Assert.Contains(outline, q => Math.Abs(Math.Sqrt((q.X - 70) * (q.X - 70) + (q.Y - 40) * (q.Y - 40)) - 10) < 1e-6 && q.X > 77 && q.Y > 42);
        Assert.Equal(2, d.Entities.Count(e => e.Kind == "kreis" && e.Layer == "Bohrungen" && e.R == 4.5));
        var arc = d.Entities.Single(e => e.Kind == "bogen"); Assert.Equal((40, 25, 12, 30, 150), (arc.Cx, arc.Cy, arc.R, arc.A0, arc.A1));
        Assert.Single(d.Entities, e => e.Kind == "linie");
        Assert.Contains(d.Entities, e => e.Kind == "text" && e.Text == "Halteplatte A" && e.Height == 3.5);
        Assert.Contains(d.Entities, e => e.Kind == "text" && e.Text == "Werkstoff PLA\nDicke 6 mm");
        Assert.Equal(1, skipped["DIMENSION"]);
    }

    [Fact(DisplayName = "T-0691 DXF-Rundreise: Fremd-DXF lesen → Ebene ändern → schreiben → wieder lesen → alles gleich")]
    public void Dxf_roundtrip()
    {
        var (d, _) = CadDxf.Read(Fixture("fremd-halteplatte.dxf"));
        d.Entities.Single(e => e.Kind == "linie").Points[1][0] = 70; // bearbeiten
        var text = CadDxf.Write(d, [("Umriss", 0, 0, 10, 0)]);
        if (Environment.GetEnvironmentVariable("TRENDO_BELEG_DIR") is { Length: > 0 } bd) { Directory.CreateDirectory(bd); File.WriteAllText(Path.Combine(bd, "halteplatte-trendo.dxf"), text); }
        var (e2, skipped) = CadDxf.Parse(text);
        Assert.Empty(skipped);
        // DXF R12 kennt kein MTEXT: mehrzeiliger Text wird je Zeile ein TEXT – Inhalt und Ebene bleiben
        Assert.Equal(d.Entities.Count + 1 + 1, e2.Entities.Count);
        Assert.Equal(d.Entities.Where(e => e.Kind == "text").SelectMany(e => e.Text.Split('\n')), e2.Entities.Where(e => e.Kind == "text").Select(e => e.Text));
        foreach (var (a, b) in d.Entities.Where(e => e.Kind != "text").Zip(e2.Entities.Where(e => e.Kind != "text")))
        {
            Assert.Equal(a.Kind, b.Kind); Assert.Equal(a.Layer, b.Layer); Assert.Equal(a.Closed, b.Closed);
            Assert.Equal(a.Points.Count, b.Points.Count);
            for (var i = 0; i < a.Points.Count; i++) for (var k = 0; k < 2; k++) Assert.Equal(a.Points[i][k], b.Points[i][k], 6);
            Assert.Equal((a.Cx, a.Cy, a.R, a.A0, a.A1), (b.Cx, b.Cy, b.R, b.A0, b.A1));
        }
        Assert.Equal(70, e2.Entities.Single(e => e.Kind == "linie" && e.Layer == "Hilfslinien").Points[1][0]);
        Assert.Equal(d.Entities.Single(e => e.Kind == "polylinie").Points[2][2], e2.Entities.Single(e => e.Kind == "polylinie").Points[2][2], 9);
        Assert.False(e2.Layers.Single(l => l.Name == "Hilfslinien").Visible);
        Assert.Contains(e2.Entities, e => e.Kind == "text" && e.Text == "Halteplatte A");
    }

    [Fact(DisplayName = "T-0691 STEP: Trendos facettiertes STEP wird als Referenzkörper wieder gelesen (Volumen gleich); echtes B-Rep-STEP meldet ehrlich, dass es nicht geht")]
    public void Step_facetted_roundtrip()
    {
        var dir = Tmp();
        try
        {
            var solid = CadShapes.Box(new Vec3(0, 0, 0), 30, 20, 10);
            var f = Path.Combine(dir, "quader.step"); CadExport.WriteStep([("Quader", solid)], f);
            var mesh = CadStepRead.ReadFacetted(f);
            var r = CadReference.FromMesh("Quader", f, mesh);
            Assert.Equal(6000, r.ToSolid().Volume(), 3);
            var brep = Path.Combine(dir, "echt.step");
            File.WriteAllText(brep, "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=CARTESIAN_POINT('',(0.,0.,0.));\n#2=CYLINDRICAL_SURFACE('',#3,5.);\n#4=ADVANCED_FACE('',(#5),#2,.T.);\nENDSEC;\nEND-ISO-10303-21;\n");
            var ex = Assert.Throws<CadError>(() => CadStepRead.ReadFacetted(brep));
            Assert.Contains("echte Flächen", ex.Message);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0691 CAD-Dokument: Zeichnung und Referenzkörper werden gespeichert und geladen; alte .tcad ohne diese Angaben bleiben lesbar")]
    public void CadDoc_keeps_drawing()
    {
        var dir = Tmp();
        try
        {
            var doc = new CadDoc(); doc.Drawing = CadDxf.Read(Fixture("fremd-halteplatte.dxf")).Drawing;
            doc.References.Add(CadReference.FromMesh("Affe", "affe.stl", Mesh3D.ReadMeshFile(Fixture("affe-blender.stl"))));
            var f = Path.Combine(dir, "teil.tcad"); doc.Save(f);
            var back = CadDoc.Load(f);
            Assert.Equal(doc.Drawing.Entities.Count, back.Drawing!.Entities.Count);
            Assert.Equal(doc.References[0].Indices.Length, back.References[0].Indices.Length);
            var clone = back.Clone(); clone.Drawing!.Entities.Clear(); Assert.NotEmpty(back.Drawing.Entities); // Rückgängig braucht echte Kopien
            var old = CadDoc.FromJson("{\"Params\":{},\"Bodies\":[{\"Name\":\"Teil 1\",\"Features\":[]}],\"Segments\":48}");
            Assert.Null(old.Drawing); Assert.Empty(old.References);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
