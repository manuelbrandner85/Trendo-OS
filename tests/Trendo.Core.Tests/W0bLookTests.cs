using Trendo.Core;
using Xunit;
namespace Trendo.Core.Tests;

public class W0b_Hintergrund_Farbe
{
    [Fact(DisplayName = "W0B-02/03: Abendrot ist Standard, Farbe passend zum Hintergrund, eigene Farbe bleibt erhalten")]
    public void Accent()
    {
        var s = new UserSettings();
        Assert.Equal("abendrot", s.CurrentWallpaper(DateTime.Now));
        Assert.Equal("#2F86F6", s.EffectiveAccent(DateTime.Now));
        s.Accent = "#22C55E"; s.Wallpaper = "morgen";
        Assert.Equal("#22C55E", s.EffectiveAccent(DateTime.Now));
        s.AccentFromWallpaper = true;
        Assert.Equal("#F59E0B", s.EffectiveAccent(DateTime.Now));
        foreach (var w in UserSettings.Wallpapers) Assert.Matches("^#[0-9A-F]{6}$", UserSettings.AccentFor(w));
        s.WallpaperByTime = true;
        Assert.Equal("#22C3A6", s.EffectiveAccent(new DateTime(2026, 10, 21, 23, 0, 0))); // Nacht → Polarlicht
        var d = Path.Combine(Path.GetTempPath(), "trendo-look-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d);
        s.Save(d); var r = UserSettings.Load(d);
        Assert.True(r.AccentFromWallpaper); Assert.Equal("#22C55E", r.Accent);
        Directory.Delete(d, true);
    }
}

public class T0107_Leistungsprofil
{
    [Fact(DisplayName = "T-0107: Leistungsprofil – automatisch auf ARM sparsam, „hoch“ und „sparsam“ gelten überall")]
    public void Profile()
    {
        var s = new UserSettings();
        Assert.Equal("auto", s.PowerProfile);
        Assert.False(s.IsLowPower(arm: false));
        Assert.True(s.IsLowPower(arm: true));
        s.PowerProfile = "hoch"; Assert.False(s.IsLowPower(arm: true));
        s.PowerProfile = "sparsam"; Assert.True(s.IsLowPower(arm: false));
        Assert.Equal(["auto", "hoch", "sparsam"], UserSettings.PowerProfiles);
    }
}

public class Deinstallation_Daten
{
    [Fact(DisplayName = "Deinstallieren: Daten löschen nur im Trendo-Ordner, nie Benutzer-/Laufwerksordner, nie wenn Projekte darin liegen")]
    public void RemoveUserData()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-del-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "audit")); File.WriteAllText(Path.Combine(root, "audit", "audit.jsonl"), "x");
        var ro = Path.Combine(root, "nurlesen.txt"); File.WriteAllText(ro, "x"); File.SetAttributes(ro, FileAttributes.ReadOnly);
        // Projekte im Datenordner: nichts löschen
        Assert.False(DataLocation.RemoveUserData(root, Path.Combine(root, "Projekte")));
        Assert.True(Directory.Exists(root));
        // geschützte Ordner
        Assert.False(DataLocation.RemoveUserData(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
        Assert.False(DataLocation.RemoveUserData(Path.GetPathRoot(root)!));
        Assert.False(DataLocation.RemoveUserData(Path.Combine(root, "gibt-es-nicht")));
        Assert.False(DataLocation.RemoveUserData("C:"));          // laufwerksrelativ
        Assert.False(DataLocation.RemoveUserData("TrendoOS"));    // relativ
        var fremd = Path.Combine(Path.GetTempPath(), "fremd-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(fremd);
        Assert.False(DataLocation.RemoveUserData(fremd));         // heißt nicht „Trendo…“
        Assert.True(Directory.Exists(fremd)); Directory.Delete(fremd);
        // normaler Fall, auch mit schreibgeschützter Datei
        Assert.True(DataLocation.RemoveUserData(root, Path.Combine(Path.GetTempPath(), "anderswo")));
        Assert.False(Directory.Exists(root));
    }
}
