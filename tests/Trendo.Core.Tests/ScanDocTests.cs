using System.Text.Json.Nodes;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0264: Punktwolken-Dokument – Schritte nicht-destruktiv, Beispielraum mit bekannten Maßen, Speichern/Laden.</summary>
public class T0264_ScanDoc
{
    [Fact(DisplayName = "T-0264: Beispielraum 4 × 3 m; Störpunkte entfernen trifft genau die Staubpunkte; Boden entfernen; Zuschneiden auf die Kiste")]
    public void Demo_ops()
    {
        var s = new ScanDoc { Source = ScanDoc.DemoSource };
        var raw = s.Build(); var i = raw.Info();
        Assert.InRange(i.SizeX, 3.99, 4.02); Assert.InRange(i.SizeY, 2.99, 3.02); Assert.True(i.HasColor);
        var red = raw.Colors!.Count(c => c == (255, 60, 60)); Assert.Equal(40, red);
        s.Ops.Add(new("outliers", [0.05, 3])); // Standardwerte des Programms
        var clean = s.Build(); Assert.InRange(raw.Points.Count - clean.Points.Count, 40, 48); // die 40 Störpunkte und höchstens ein paar Eckpunkte (nur 2 Nachbarn)
        Assert.DoesNotContain(clean.Colors!, c => c == (255, 60, 60));
        s.Ops.Add(new("nofloor", [0.02]));
        var noFloor = s.Build(); Assert.True(noFloor.Points.Count < clean.Points.Count - 7000, $"{noFloor.Points.Count}"); Assert.True(noFloor.Info().Min.Z > 0.02);
        s.Ops.Add(new("crop", [0.95, 0.95, 0, 1.65, 1.45, 0.6]));
        var box = s.Build().Info(); Assert.InRange(box.SizeX, 0.58, 0.62); Assert.InRange(box.SizeZ, 0.45, 0.55); // nur die Kiste 0,6 × 0,4 × 0,5 m
        var ex = Assert.Throws<InvalidOperationException>(() => new ScanDoc { Source = ScanDoc.DemoSource }.Pipe(new ScanOp("crop", [10, 10, 10, 11, 11, 11])));
        Assert.Contains("kein Punkt übrig", ex.Message);
    }

    [Fact(DisplayName = "T-0264: Zentrieren, Drehen, Einheit, Zusammenführen; JSON hin und zurück; Fingerabdruck ändert sich mit den Schritten")]
    public void Transform_and_roundtrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-t0264-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var f = Path.Combine(dir, "teil.xyz"); File.WriteAllText(f, "0 0 0\n1000 0 0\n1000 500 0\n0 500 200\n");
            var s = new ScanDoc { Source = f };
            var fp0 = s.Fingerprint();
            s.Ops.Add(new("scale", [ScanPlus.UnitFactor("mm", "m")])); s.Ops.Add(new("center", [])); s.Ops.Add(new("rotate", [90]));
            Assert.NotEqual(fp0, s.Fingerprint());
            var i = s.Build().Info();
            Assert.Equal(0.5, i.SizeX, 6); Assert.Equal(1.0, i.SizeY, 6); Assert.Equal(0.2, i.SizeZ, 6); Assert.Equal(0, i.Min.Z, 9);
            s.Ops.Add(new("merge", [], f)); Assert.Equal(8, s.Build().Points.Count);
            var o = new JsonObject(); s.Yaw = 12; s.Box = [1, 2, 3, 4, 5, 6]; s.WriteTo(o);
            var back = ScanDoc.From(o);
            Assert.Equal(4, back.Ops.Count); Assert.Equal("merge", back.Ops[3].Kind); Assert.Equal(f, back.Ops[3].Path); Assert.Equal(12, back.Yaw); Assert.Equal(6, back.Box[5]);
            Assert.Equal(s.Fingerprint(), back.Fingerprint());
            Assert.Contains("Gedreht um 90°", back.Ops[2].Describe());
            Assert.Throws<ArgumentException>(() => new ScanDoc().Build());
        }
        finally { Directory.Delete(dir, true); }
    }
}

static class ScanDocTestExt
{
    public static int Pipe(this ScanDoc s, ScanOp op) { s.Ops.Add(op); return s.Build().Points.Count; }
}
