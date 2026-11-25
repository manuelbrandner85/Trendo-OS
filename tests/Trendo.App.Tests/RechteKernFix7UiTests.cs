using System.IO.Compression;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0764, 7. Prüfrunde an der Oberfläche: Kinder öffnen Dateien nur in Trendos eigenen Betrachtern, nie über Windows; Archive nur ohne
/// ausführbaren Inhalt. Ohne statische Ersatz-Hooks; startet nichts echt (Testmodus, FakeLauncher).</summary>
public class RechteKernFix7Ui
{
    const string Hinweis = "Diese Datei kann nur ein Erwachsener öffnen.";
    static void Settle() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    static (MainWindow W, FakeLauncher L, string Root) Open(bool child)
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        new UserSettings { Child = child, Name = child ? "Kind" : "Erika Muster" }.Save(root);
        var l = new FakeLauncher();
        var w = new MainWindow(new TrendoCore(root, new InMemorySecretStore(), l, offline: false), new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
        w.ProgramSetLevel(UserLevel.Erweitert);
        w.FreigabeConfirm = _ => Task.FromResult(true); w.Show(); Settle();
        return (w, l, root);
    }

    static string Datei(string root, string name, string text = "x")
    {
        var f = Path.Combine(root, "dokumente", name); Directory.CreateDirectory(Path.GetDirectoryName(f)!); File.WriteAllText(f, text); return f;
    }

    [AvaloniaFact(DisplayName = "T-0764 H: Kind – Text, Tabelle, Bild, 3D in Trendos eigenen Betrachtern; .zip, .iso, .py, .kra, .blend, .vhdx mit Hinweis; nichts geht an Windows")]
    public void Kind_eigene_Betrachter()
    {
        var (c, l, root) = Open(true);
        try
        {
            Assert.True(c.LaunchAs(Datei(root, "brief.txt", "Hallo"))); Settle(); Assert.Equal("text", c.CurrentView);
            Assert.True(c.LaunchAs(Datei(root, "liste.csv", "a;b\n1;2"))); Settle(); Assert.Equal("tabelle", c.CurrentView);
            var png = Path.Combine(root, "dokumente", "bild.png"); File.WriteAllBytes(png, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="));
            Assert.True(c.LaunchAs(png)); Settle(); Assert.Equal("medien", c.CurrentView);
            Assert.True(c.LaunchAs(Datei(root, "modell.stl", "solid x\nendsolid x"))); Settle(); Assert.Equal("xr", c.CurrentView);
            foreach (var n in new[] { "spiel.zip", "abbild.iso", "skript.py", "bild.kra", "szene.blend", "platte.vhdx" })
            {
                Assert.False(c.LaunchAs(Datei(root, n)));
                Assert.Equal(Hinweis, c.LastLaunchRefused);
            }
            c.FilesOpen(Datei(root, "noch.zip")); Settle();
            Assert.Equal(Hinweis, c.FilesMessage);
            Assert.Empty(l.Opened);
        }
        finally { c.Close(); }
        var (w, wl, wroot) = Open(false);
        try { Assert.True(w.LaunchAs(Datei(wroot, "spiel.zip"))); Assert.Single(wl.Opened); } // Erwachsene: wie bisher
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0764 H: Kind – ZIP mit Programm wird nicht entpackt (Hinweis), ZIP nur mit Bildern schon")]
    public void Kind_Archiv()
    {
        var (c, _, root) = Open(true);
        try
        {
            string Zip(string name, params string[] entries)
            {
                var z = Path.Combine(c.Files.Home, name); /* 10. Prüfrunde: eigener Ordner (der Trendo-Datenordner ist für Kinder geschützt) */ Directory.CreateDirectory(Path.GetDirectoryName(z)!);
                using var a = ZipFile.Open(z, ZipArchiveMode.Create); foreach (var e in entries) using (var s = new StreamWriter(a.CreateEntry(e).Open())) s.Write("x");
                return z;
            }
            void Unpack(string z) { c.Show("alltag-dateien"); Settle(); c.Find<TextBox>("zip-file").Text = z; c.Find<Button>("zip-unpack").Press(); Settle(); }
            var bad = Zip("spiel.zip", "bild.png", "spiel.exe");
            Unpack(bad);
            Assert.False(Directory.Exists(Path.Combine(c.Files.Home, "spiel")));
            Assert.Contains(c.Core.Audit.ReadAll(), a => a.Action == "rbac.denied" && a.Detail.Contains("spiel.zip"));
            var ok = Zip("fotos.zip", "bild.png");
            Unpack(ok);
            Assert.True(File.Exists(Path.Combine(c.Files.Home, "fotos", "bild.png")));
        }
        finally { c.Close(); }
    }
}
