using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

public class RecentPathsTests
{
    [Fact(DisplayName = "Zuletzt benutzte Dateien: je Studio, neueste zuerst, höchstens acht, kaputte Datei bleibt leer")]
    public void Per_studio_newest_first()
    {
        var file = Path.Combine(Path.GetTempPath(), "zuletzt-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var r = new RecentPaths(file);
            Assert.Empty(r.List("3d"));
            r.Remember("3d", @"C:\a\eins.blend");
            r.Remember("3d", @"C:\a\zwei.blend");
            r.Remember("office", @"C:\b\brief.docx");
            Assert.Equal([@"C:\a\zwei.blend", @"C:\a\eins.blend"], r.List("3d"));
            Assert.Equal([@"C:\b\brief.docx"], r.List("office"));
            r.Remember("3d", @"c:\a\eins.blend");
            Assert.Equal(@"c:\a\eins.blend", r.List("3d")[0]);
            Assert.Equal(2, r.List("3d").Count);
            for (var i = 0; i < 10; i++) r.Remember("3d", "datei-" + i);
            Assert.Equal(8, r.List("3d").Count);
            Assert.Equal("datei-9", r.List("3d")[0]);
            File.WriteAllText(file, "{");
            Assert.Empty(new RecentPaths(file).List("3d"));
        }
        finally { try { File.Delete(file); } catch (IOException) { } }
    }
}

public class OpenWithFileTests
{
    [Fact(DisplayName = "Programm öffnen übergibt Programmpfad und Datei getrennt und schreibt es ins Protokoll")]
    public void Passes_exe_and_file()
    {
        using var f = new Fixture();
        var exe = Path.Combine(f.Root, "blender");
        var file = Path.Combine(f.Root, "kiste.blend");
        File.WriteAllText(exe, "");
        File.WriteAllText(file, "");
        f.Core.Integrations.OpenWithFile(Principal.LocalOwner, exe, file, f.Core.Audit);
        Assert.Equal((exe, file), Assert.Single(f.Launcher.With));
        Assert.Contains(f.Core.Audit.ReadAll(), e => e.Action == "user.direct_launch" && e.Detail.Contains("kiste.blend"));
    }

    [Fact(DisplayName = "Programm öffnen ohne Programm oder ohne Datei startet nichts")]
    public void Rejects_empty()
    {
        using var f = new Fixture();
        Assert.Throws<ArgumentException>(() => f.Core.Integrations.OpenWithFile(Principal.LocalOwner, "", "x", f.Core.Audit));
        Assert.Throws<ArgumentException>(() => f.Core.Integrations.OpenWithFile(Principal.LocalOwner, "blender", "  ", f.Core.Audit));
        Assert.Empty(f.Launcher.Opened);
        Assert.Empty(f.Launcher.With);
    }
}
