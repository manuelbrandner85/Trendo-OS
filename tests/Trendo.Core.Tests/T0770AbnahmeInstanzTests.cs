using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0770: Abnahme-Instanz neben einer echten Installation – Daten, Werkzeuge und Altordner liegen dann unter dem eigenen Ordner.</summary>
public class T0770AbnahmeInstanzTests
{
    [Fact(DisplayName = "T-0770: Prüf-Instanz legt Daten, Werkzeuge und Altordner unter den eigenen Ordner")]
    public void Instance_paths_stay_inside()
    {
        var inst = Path.Combine(Path.GetTempPath(), "abnahme-instanz");
        Assert.Equal(Path.GetFullPath(inst), DataLocation.Instance(inst));
        Assert.Equal(Path.Combine(inst, "TrendoOS"), DataLocation.DefaultIn(inst));
        Assert.Equal(Path.Combine(inst, DataLocation.ToolsFolderName), DataLocation.ToolsRootIn(inst));
        Assert.Equal(Path.Combine(inst, "TrendoOS-alt"), DataLocation.LegacyIn(inst));
        // Name des Werkzeugordners bleibt gleich: die Schutzregeln beim Deinstallieren greifen auch in der Prüf-Instanz
        Assert.Equal(DataLocation.ToolsFolderName, Path.GetFileName(DataLocation.ToolsRootIn(inst)));
    }

    [Theory(DisplayName = "T-0770: ohne gültige Prüf-Instanz bleiben die normalen Ordner")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relativ/ordner")]
    public void Invalid_instance_is_ignored(string value)
    {
        Assert.Null(DataLocation.Instance(value));
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Assert.Equal(Path.Combine(appData, "TrendoOS"), DataLocation.DefaultIn(DataLocation.Instance(value)));
        Assert.Equal(Path.Combine(local, DataLocation.ToolsFolderName), DataLocation.ToolsRootIn(DataLocation.Instance(value)));
        Assert.Equal(Path.Combine(local, "TrendoOS"), DataLocation.LegacyIn(DataLocation.Instance(value)));
    }

    [Fact(DisplayName = "T-0770: ohne Umgebungsvariable gelten die normalen Ordner (Endnutzer unverändert)")]
    public void Without_variable_defaults_unchanged()
    {
        if (Environment.GetEnvironmentVariable(DataLocation.InstanceVar) is { Length: > 0 }) return; // Prüflauf in einer Instanz
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TrendoOS"), DataLocation.Default());
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DataLocation.ToolsFolderName), DataLocation.ToolsRoot());
    }
}
