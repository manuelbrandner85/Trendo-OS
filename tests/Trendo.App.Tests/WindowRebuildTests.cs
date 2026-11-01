using System.Collections;
using System.Reflection;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Trendo.App.Tests;

/// Neuaufbau des Fensters (z. B. nach Design-Wechsel) und viele Testfenster: statische Listen wachsen nicht mit jedem Fenster (sonst doppelte Reiter, Speicher wächst).
public class WindowRebuildTests
{
    static Dictionary<string, int> Counts()
    {
        var d = new Dictionary<string, int>();
        foreach (var f in typeof(MainWindow).Assembly.GetTypes().SelectMany(t => t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)))
            if (!f.FieldType.IsPrimitive && f.FieldType != typeof(string) && !f.IsLiteral && f.DeclaringType != typeof(StartupTrace))
                try { if (f.GetValue(null) is ICollection c) d[$"{f.DeclaringType!.Name}.{f.Name}"] = c.Count; } catch (Exception) { }
        return d;
    }

    [AvaloniaFact(DisplayName = "Neue Fenster verdoppeln keine Reiter und Sucheinträge (statische Listen bleiben gleich groß)")]
    public void No_growth()
    {
        var (w0, _, _) = Ui.Open(); w0.Close();
        var a = Counts();
        for (var i = 0; i < 3; i++) { var (w, _, _) = Ui.Open(); foreach (var k in new[] { "ki-wissen", "assets", "vorhaben", "produktion" }) w.Show(k); w.Close(); }
        var grow = Counts().Where(kv => a.TryGetValue(kv.Key, out var x) && kv.Value > x).Select(kv => $"{kv.Key}: {a[kv.Key]} → {kv.Value}").ToList();
        Assert.True(grow.Count == 0, "Wachsen: " + string.Join(", ", grow));
    }
}
