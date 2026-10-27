using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0109: Logo ersetzt das V-Konzeptlogo überall. Seit 27.10.2026 Logo C „Gold-Orbit“ (TrendoLogo.cs), vorher Logo B „Saphir-Kristall“.</summary>
public class LogoTests
{
    static string Root([System.Runtime.CompilerServices.CallerFilePath] string f = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(f)!, "..", ".."));

    [Fact(DisplayName = "Logo: kein V-Pfad und kein Konzept-Hinweis mehr im Quelltext")]
    public void No_old_logo_in_source()
    {
        foreach (var f in Directory.EnumerateFiles(Path.Combine(Root(), "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") || f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
            var t = File.ReadAllText(f);
            Assert.False(t.Contains("L50,90"), $"V-Pfad in {f}");
            if (!f.EndsWith("MdiIcons.cs")) Assert.False(t.Contains("Mdi.MicrosoftWindows"), $"Windows-Zeichen statt Logo in {f}");
            Assert.False(t.Contains("Logo-Konzept") || t.Contains("Logo: Konzept") || t.Contains("Logo: Entwurf, noch nicht"), $"Konzept-Hinweis in {f}");
        }
    }

    [Fact(DisplayName = "Logo: trendo.ico enthält 16, 32, 48 und 256 px")]
    public void Ico_has_sizes()
    {
        var b = File.ReadAllBytes(Path.Combine(Root(), "src", "Trendo.App", "Assets", "logo", "trendo.ico"));
        Assert.Equal(0, BitConverter.ToUInt16(b, 0)); Assert.Equal(1, BitConverter.ToUInt16(b, 2));
        var n = BitConverter.ToUInt16(b, 4); var sizes = new HashSet<int>();
        for (var i = 0; i < n; i++) { var w = b[6 + i * 16]; sizes.Add(w == 0 ? 256 : w); }
        foreach (var s in new[] { 16, 32, 48, 256 }) Assert.Contains(s, sizes);
    }

    [AvaloniaFact(DisplayName = "Logo: Hauptfenster hat Icon, Startknopf zeigt das Logo")]
    public void Window_icon_and_start_button()
    {
        var (w, _, _) = Ui.Open();
        Assert.NotNull(w.Icon);
        var start = w.GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "start-button");
        Assert.Contains(start.GetLogicalDescendants(), c => c is Viewbox { Name: "trendo-logo" });
        Assert.Contains(w.GetLogicalDescendants(), c => c is Viewbox { Name: "trendo-logo" } && !start.GetLogicalDescendants().Contains(c));
        w.Close();
    }

    [Fact(DisplayName = "Logo C „Gold-Orbit“: fotorealistisches Emblem aus der Vorlage liegt bei, PNGs in allen Größen")]
    public void Emblem_and_pngs()
    {
        var dir = Path.Combine(Root(), "src", "Trendo.App", "Assets", "logo");
        Assert.True(File.Exists(Path.Combine(Root(), "docs", "design", "logo-vorschlaege", "gold-orbit-vorlage.png")), "Vorlage fehlt");
        foreach (var f in new[] { "trendo-emblem.png", "trendo-32.png", "trendo-256.png", "trendo-512.png" })
        {
            var b = File.ReadAllBytes(Path.Combine(dir, f));
            Assert.True(b.Length > 1000 && b[0] == 0x89 && b[1] == (byte)'P', f + " ist kein PNG");
        }
        Assert.False(File.Exists(Path.Combine(dir, "trendo-logo-b.svg")), "altes Logo B liegt noch bei");
    }
}
