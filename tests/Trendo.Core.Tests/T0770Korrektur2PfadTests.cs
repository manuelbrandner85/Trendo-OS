using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0770 Korrekturrunde 2, N-c: feste Werkzeugpfade des Katalogs folgen einer Prüf-Instanz; ohne Instanz unverändert.</summary>
public class T0770Korrektur2PfadTests
{
    [Fact(DisplayName = "T-0770 N-c: Katalogpfade %LOCALAPPDATA%/TrendoWerkzeuge zeigen in der Prüf-Instanz auf deren Werkzeugordner, sonst unverändert")]
    public void Tools_paths_follow_instance()
    {
        var p = Path.Combine("%LOCALAPPDATA%", "TrendoWerkzeuge", "qpdf", "qpdf-*", "bin", "qpdf.exe");
        Assert.Equal(Environment.ExpandEnvironmentVariables(p), DependencyManager.ExpandPath(p, null)); // ohne Instanz: genau wie bisher
        var inst = Path.Combine(Path.GetTempPath(), "trendo-t0770-inst");
        Assert.Equal(Path.Combine(inst, "TrendoWerkzeuge", "qpdf", "qpdf-*", "bin", "qpdf.exe"), DependencyManager.ExpandPath(p, inst));
        var other = Path.Combine("%LOCALAPPDATA%", "Programs", "x.exe");
        Assert.Equal(Environment.ExpandEnvironmentVariables(other), DependencyManager.ExpandPath(other, inst)); // andere Pfade bleiben
        var similar = Path.Combine("%LOCALAPPDATA%", "TrendoWerkzeugeAlt", "y.exe");
        Assert.Equal(Environment.ExpandEnvironmentVariables(similar), DependencyManager.ExpandPath(similar, inst)); // nur der ganze Ordnername
    }
}
