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
    static string Tmp(string prefix) { var d = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "Deinstallieren: Daten löschen nur im Trendo-Ordner, nie Benutzer-/Laufwerksordner, nie wenn der Projektordner darin liegt")]
    public void RemoveUserData()
    {
        var root = Tmp("trendo-del-");
        Directory.CreateDirectory(Path.Combine(root, "audit")); File.WriteAllText(Path.Combine(root, "audit", "audit.jsonl"), "x");
        var ro = Path.Combine(root, "nurlesen.txt"); File.WriteAllText(ro, "x"); File.SetAttributes(ro, FileAttributes.ReadOnly);
        Assert.Equal(-1, DataLocation.RemoveUserData(root, Path.Combine(root, "Projekte")));   // Projektordner liegt darin
        Assert.True(File.Exists(ro));
        Assert.Equal(-1, DataLocation.RemoveUserData(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
        Assert.Equal(-1, DataLocation.RemoveUserData(Path.GetPathRoot(root)!));
        Assert.Equal(-1, DataLocation.RemoveUserData(Path.Combine(root, "gibt-es-nicht")));
        Assert.Equal(-1, DataLocation.RemoveUserData("C:"));           // laufwerksrelativ
        Assert.Equal(-1, DataLocation.RemoveUserData("TrendoOS"));     // relativ
        var fremd = Tmp("fremd-");
        Assert.Equal(-1, DataLocation.RemoveUserData(fremd));          // heißt nicht „Trendo…“
        Directory.Delete(fremd);
        Assert.Equal(2, DataLocation.RemoveUserData(root, Path.Combine(Path.GetTempPath(), "anderswo")));
        Assert.False(Directory.Exists(root));
    }

    [Fact(DisplayName = "Deinstallieren: Projekt-Unterordner bleiben, Verknüpfungen werden nie verfolgt (fremde Dateien unverändert)")]
    public void Projekte_und_Verknuepfungen()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Tmp("trendo-del-");
        Directory.CreateDirectory(Path.Combine(root, "projects", "p1")); File.WriteAllText(Path.Combine(root, "projects", "p1", "brief.docx"), "x");
        Directory.CreateDirectory(Path.Combine(root, "projekte")); File.WriteAllText(Path.Combine(root, "projekte", "scan.ply"), "x");
        File.WriteAllText(Path.Combine(root, "settings.json"), "{}");
        var fremd = Tmp("fremd-"); var fremdFile = Path.Combine(fremd, "wichtig.txt"); File.WriteAllText(fremdFile, "x"); File.SetAttributes(fremdFile, FileAttributes.ReadOnly);
        var link = Path.Combine(root, "export");
        var mk = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{fremd}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
        mk.WaitForExit();
        Assert.True(Directory.Exists(link), "Junction konnte nicht angelegt werden");
        try
        {
            var n = DataLocation.RemoveUserData(root);
            Assert.Equal(2, n); // settings.json und die Verknüpfung
            Assert.True(File.Exists(Path.Combine(root, "projects", "p1", "brief.docx")));
            Assert.True(File.Exists(Path.Combine(root, "projekte", "scan.ply")));
            Assert.False(Directory.Exists(link));
            Assert.True(File.Exists(fremdFile));
            Assert.True(File.GetAttributes(fremdFile).HasFlag(FileAttributes.ReadOnly)); // Ziel der Verknüpfung unberührt
        }
        finally
        {
            File.SetAttributes(fremdFile, FileAttributes.Normal); Directory.Delete(fremd, true);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}

public class Deinstallation_Werkzeuge
{
    [Fact(DisplayName = "Deinstallieren: Zusatzprogramme – nur ein Ordner „TrendoWerkzeuge“, Größe und Programme richtig, Verknüpfungen nie verfolgt")]
    public void RemoveTools()
    {
        var parent = Path.Combine(Path.GetTempPath(), "trendo-wz-" + Guid.NewGuid().ToString("N"));
        var tools = Path.Combine(parent, DataLocation.ToolsFolderName);
        Directory.CreateDirectory(Path.Combine(tools, "qpdf", "bin")); File.WriteAllBytes(Path.Combine(tools, "qpdf", "bin", "qpdf.exe"), new byte[2048]);
        Directory.CreateDirectory(Path.Combine(tools, "colmap")); File.WriteAllBytes(Path.Combine(tools, "colmap", "colmap.exe"), new byte[1024]);
        var fremd = Path.Combine(parent, "fremd"); Directory.CreateDirectory(fremd); var fremdFile = Path.Combine(fremd, "bleibt.txt"); File.WriteAllText(fremdFile, "x");
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var link = Path.Combine(tools, "verweis");
                var mk = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{fremd}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;
                mk.WaitForExit();
            }
            var info = DataLocation.ToolsSummary(tools);
            Assert.NotNull(info);
            Assert.Equal(["colmap", "qpdf"], info.Value.Programs); // Verknüpfung zählt nicht als Programm
            Assert.Equal(3072, info.Value.Bytes);
            Assert.Equal(-1, DataLocation.RemoveTools(fremd));                       // falscher Name
            Assert.Equal(-1, DataLocation.RemoveTools("TrendoWerkzeuge"));            // relativ
            Assert.True(DataLocation.RemoveTools(tools) >= 2);
            Assert.False(Directory.Exists(tools));
            Assert.True(File.Exists(fremdFile));                                      // Ziel der Verknüpfung unberührt
            Assert.Null(DataLocation.ToolsSummary(tools));
        }
        finally { Directory.Delete(parent, true); }
    }

    [Fact(DisplayName = "Zusatzprogramme: Standardordner ist %LOCALAPPDATA%/TrendoWerkzeuge, derselbe wie im DependencyManager")]
    public void Same_root()
    {
        Assert.EndsWith(DataLocation.ToolsFolderName, DataLocation.ToolsRoot());
        Assert.Equal(DataLocation.ToolsRoot(), new DependencyManager(new AuditLog(Path.Combine(Path.GetTempPath(), "trendo-a-" + Guid.NewGuid().ToString("N") + ".jsonl")), new RealProcessRunner()).ToolsRoot);
    }
}
