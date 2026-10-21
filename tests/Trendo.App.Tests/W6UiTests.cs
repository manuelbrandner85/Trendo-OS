using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

public static class W6Ui
{
    public static string Run(MainWindow w, string id, params (string Key, string Val)[] vals)
    {
        foreach (var (k, v) in vals) w.Find<TextBox>($"{id}-{k}").Text = v;
        w.Find<Button>($"{id}-run").Press(); W2Ui.Settle();
        return w.Find<SelectableTextBlock>($"{id}-out").Text ?? "";
    }
}

public class SPEC01_Ui
{
    [AvaloniaFact(DisplayName = "SPEC-01 UI: Studio-Kachel öffnet Räume, Raum anlegen, Bedarf, Grundriss")]
    public void Rooms()
    {
        var (w, l, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-01"]);
        W6Ui.Run(w, "arch-add", ("name", "Küche"), ("l", "3,5"), ("w", "3"));
        Assert.Single(w.Rooms.Project.Rooms);
        Assert.Contains("Pakete", W6Ui.Run(w, "arch-need"));
        Assert.Contains("⚠", W6Ui.Run(w, "arch-add", ("l", "abc")));
        w.Find<Button>("arch-svg").Press();
        Assert.Contains(l.Opened, x => x.EndsWith("grundriss.svg"));
    }
}

public class SPEC02_Ui
{
    [AvaloniaFact(DisplayName = "SPEC-02 UI: Elektronik-Rechner und Stückliste")]
    public void Electronics()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-02"]);
        Assert.Contains("4,70 kΩ", W6Ui.Run(w, "el-code"));
        Assert.Contains("150", W6Ui.Run(w, "el-led"));
        Assert.Contains("⚠", W6Ui.Run(w, "el-ohm", ("i", "1")));
        Assert.Contains("Gespeichert", W6Ui.Run(w, "el-bom"));
        w.Find<Button>("fach-tab-architektur").Press();
        Assert.Equal("architektur", w.CurrentView);
    }
}

public class SPEC03_Ui
{
    [AvaloniaFact(DisplayName = "SPEC-03 UI: Balken prüfen, Wärmedehnung, Einheiten")]
    public void Engineering()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["SPEC-03"]);
        Assert.Contains("Sicherheit", W6Ui.Run(w, "eng-beam"));
        Assert.Contains("⚠", W6Ui.Run(w, "eng-beam", ("case", "9")));
        Assert.Contains("2,76 mm", W6Ui.Run(w, "eng-heat"));
        Assert.Contains("6,8948 bar", W6Ui.Run(w, "eng-unit"));
    }
}

public class SPEC04_Ui
{
    [AvaloniaFact(DisplayName = "SPEC-04 UI: GLB-Modell prüfen und Bildzeit")]
    public void Xr()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        w.Show(MainWindow.StudioPages["SPEC-04"]);
        var o = W6Ui.Run(w, "xr-check", ("file", Glb.Write(root, 500_000)));
        Assert.Contains("500.000 Dreiecke", o); Assert.Contains("⚠ Meta Quest 2", o); Assert.Contains("✓ PC-VR", o);
        Assert.Contains("⚠", W6Ui.Run(w, "xr-check", ("file", "/gibtsnicht.glb")));
        Assert.Contains("Ruckelt", W6Ui.Run(w, "xr-frame", ("ms", "20")));
    }
}

public static class Glb
{
    /// <summary>Echte, minimale GLB-Datei mit einem Dreiecksnetz (tris Dreiecke).</summary>
    public static string Write(string dir, long tris, int textures = 0)
    {
        var json = $"{{\"asset\":{{\"version\":\"2.0\",\"generator\":\"Test\"}},\"meshes\":[{{\"primitives\":[{{\"attributes\":{{\"POSITION\":0}},\"indices\":1,\"material\":0}}]}}],\"materials\":[{{}}],\"accessors\":[{{\"count\":{tris * 3}}},{{\"count\":{tris * 3}}}],\"textures\":[{string.Join(",", Enumerable.Repeat("{}", textures))}],\"nodes\":[{{\"mesh\":0}}]}}";
        while (json.Length % 4 != 0) json += " ";
        var j = System.Text.Encoding.UTF8.GetBytes(json);
        var p = Path.Combine(dir, $"m{tris}.glb");
        using var w = new BinaryWriter(File.Create(p));
        w.Write(0x46546C67u); w.Write(2u); w.Write((uint)(12 + 8 + j.Length)); w.Write((uint)j.Length); w.Write(0x4E4F534Au); w.Write(j);
        return p;
    }
}


public class SPEC05_Ui
{
    [AvaloniaFact(DisplayName = "SPEC-05 UI: CSV auswerten, Regression, Gruppenvergleich")]
    public void Science()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        var f = Path.Combine(root, "mess.csv"); File.WriteAllText(f, "x;y\n1;2,1\n2;3,9\n3;6,2\n4;7,8\n5;10,1\n");
        w.Show(MainWindow.StudioPages["SPEC-05"]);
        Assert.Contains("n = 5", W6Ui.Run(w, "sci-desc", ("file", f)));
        Assert.True(File.Exists(Path.Combine(root, "mess.auswertung.md")));
        Assert.Contains("starker", W6Ui.Run(w, "sci-fit", ("file", f)));
        Assert.Contains("signifikant", W6Ui.Run(w, "sci-t", ("file", f)));
        Assert.Contains("⚠", W6Ui.Run(w, "sci-fit", ("x", "zz")));
    }
}
