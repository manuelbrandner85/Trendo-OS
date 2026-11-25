using System.IO.Compression;
using System.Security.Cryptography;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0764: Rechte im Kern – im Kinderprofil schlägt jede Installation und jedes Entfernen von Programmen fehl, egal über welche Seite.
/// Die Knöpfe sind nicht nur gesperrt: der Weg selbst wird aufgerufen, und der Kern lehnt ab (Rolle aus dem Profil, nicht „lokaler Besitzer“).</summary>
public class RechteKernUi
{
    static void Settle() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    static (MainWindow W, FakeLauncher L, string Root) OpenChild()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        new UserSettings { Child = true, Name = "Kind" }.Save(root);
        var l = new FakeLauncher();
        var core = new TrendoCore(root, new InMemorySecretStore(), l, offline: false);
        var w = new MainWindow(core, new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
        w.ProgramSetLevel(Trendo.Core.Programs.UserLevel.Erweitert);
        w.FreigabeConfirm = _ => Task.FromResult(true); // selbst ein „Ja“ darf im Kinderprofil nichts bewirken
        w.Show(); Settle();
        return (w, l, root);
    }

    /// <summary>Kleine, echte Portable-ZIP mit einem Programm darin und ihre SHA-256.</summary>
    static (string Zip, string Sha) PortableZip(string dir)
    {
        Directory.CreateDirectory(dir);
        var zip = Path.Combine(dir, "werkzeug.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
        using (var s = new StreamWriter(z.CreateEntry("werkzeug.exe").Open())) s.Write("MZ-Testprogramm");
        return (zip, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(zip))));
    }

    [AvaloniaFact(DisplayName = "T-0764 UI: Kinderprofil – „Portable Programme: Prüfen und entpacken“ entpackt nichts (Kern lehnt ab), Erwachsene schon")]
    public void Kinderprofil_Portable_entpackt_nichts()
    {
        var (w, _, root) = OpenChild();
        try
        {
            var (zip, sha) = PortableZip(Path.Combine(root, "quelle"));
            w.Show("prog-pflege"); Settle();
            w.Find<TextBox>("portable-zip").Text = zip; w.Find<TextBox>("portable-sha").Text = sha; w.Find<TextBox>("portable-name").Text = "werkzeug";
            w.Find<Button>("portable-run").Press(); Settle();
            Assert.False(Directory.Exists(Path.Combine(root, "werkzeuge", "werkzeug")));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "rbac.denied" && a.Actor == "kinderprofil");
            Assert.DoesNotContain(w.Core.Audit.ReadAll(), a => a.Action == "portable.install");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0764 UI: Erwachsene – Portable entpacken klappt weiter (Gegenprobe zum Kinderprofil)")]
    public void Erwachsene_Portable_entpackt()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var (zip, sha) = PortableZip(Path.Combine(root, "quelle"));
            w.Show("prog-pflege"); Settle();
            w.Find<TextBox>("portable-zip").Text = zip; w.Find<TextBox>("portable-sha").Text = sha; w.Find<TextBox>("portable-name").Text = "werkzeug";
            w.Find<Button>("portable-run").Press(); Settle();
            Assert.True(File.Exists(Path.Combine(root, "werkzeuge", "werkzeug", "werkzeug.exe")));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0764 UI: Kinderprofil – geprüfter Offline-Installer startet auch nach „Ja, starten“ nicht")]
    public void Kinderprofil_Offline_Installer_startet_nicht()
    {
        var (w, l, root) = OpenChild();
        try
        {
            var dir = Path.Combine(root, "offline"); Directory.CreateDirectory(dir);
            var exe = Path.Combine(dir, "setup.exe"); File.WriteAllText(exe, "MZ-Installer");
            File.WriteAllText(exe + ".sha256", Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(exe))));
            w.Show("prog-pflege"); Settle();
            w.Find<TextBox>("offline-folder").Text = dir;
            w.Find<Button>("offline-scan").Press(); Settle();
            w.Find<Button>("offline-run-0").Press(); Settle();
            w.Find<Button>("offline-yes").Press(); Settle();
            Assert.Empty(l.Opened);
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "rbac.denied" && a.Actor == "kinderprofil");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0764 UI: Kinderprofil – App (WhatsApp im Fenster) lässt sich nicht installieren, auch nicht am Knopf vorbei")]
    public void Kinderprofil_WebApp_wird_nicht_installiert()
    {
        var (w, _, _) = OpenChild();
        try
        {
            var id = WebApps.Catalog[0].Id;
            w.InstallWebApp(id); Settle();
            Assert.False(w.WebAppInstalled(id));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "rbac.denied" && a.Actor == "kinderprofil");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0764 UI: Kinderprofil – „Meine Installationen → Entfernen“ entfernt kein mitgeliefertes Programm")]
    public void Kinderprofil_entfernt_kein_Programm()
    {
        var (w, _, _) = OpenChild();
        try
        {
            var (comp, pkg) = Vollpaket.Included.First(kv => kv.Key == "7zip") is var kv2 ? (kv2.Key, kv2.Value) : default;
            var exe = Path.Combine(w.ToolsRootForTests, pkg.Exe.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(exe)!); File.WriteAllText(exe, "MZ");
            var item = w.AddItems().First(i => i.Id == comp);
            Assert.True(item.Installed);
            Assert.ThrowsAny<UnauthorizedAccessException>(() => item.Remove!().GetAwaiter().GetResult());
            Assert.True(File.Exists(exe));
        }
        finally { w.Close(); }
    }
}

/// <summary>T-0764 (SYS-11): Programm-Seite je Programm – vom Klick auf „Öffnen“ bis zum verschobenen Ordner und zurück. Keine Windows-Einstellungen.</summary>
public class ProgrammSeiteUi
{
    static void Settle() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    static string Notiz(string root)
    {
        var dir = Path.Combine(root, "werkzeuge", "notiz"); Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "notiz.exe"), "MZ-Programm");
        return dir;
    }

    [AvaloniaFact(DisplayName = "SYS-11 T-0764 UI: Pflegen → Öffnen → Starten/Entfernen auf einer Seite; Entfernen mit Rückfrage in die Sicherung, Wiederherstellen bringt es zurück")]
    public void Programmseite_Entfernen_und_Wiederherstellen()
    {
        var (w, l, root) = Ui.Open();
        try
        {
            var dir = Notiz(root);
            w.Show("prog-pflege"); Settle();
            w.Find<Button>("trendo-prog-open-Portable-notiz").Press(); Settle();
            Assert.Equal("prog-programm", w.CurrentView);
            Assert.Equal("notiz", w.Find<TextBlock>("prog-detail-name").Text);
            w.Find<Button>("prog-detail-start").Press(); Settle();
            Assert.Contains(l.Opened, o => o.EndsWith("notiz.exe"));
            // erster Klick fragt nur
            w.Find<Button>("prog-detail-remove").Press(); Settle();
            Assert.True(Directory.Exists(dir));
            w.Find<Button>("prog-detail-remove-yes").Press(); Settle();
            Assert.False(Directory.Exists(dir));
            Assert.DoesNotContain(l.Opened, o => o.Contains("ms-settings", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("prog-pflege", w.CurrentView);
            Assert.Null(w.TryFind<Button>("trendo-prog-open-Portable-notiz"));
            w.Find<Button>("trendo-prog-restore-0").Press(); Settle();
            Assert.Equal("MZ-Programm", File.ReadAllText(Path.Combine(dir, "notiz.exe")));
            Assert.NotNull(w.TryFind<Button>("trendo-prog-open-Portable-notiz"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "SYS-11 T-0764 UI: Kinderprofil – „Ja, entfernen“ auf der Programm-Seite verschiebt nichts (Kern lehnt ab)")]
    public void Programmseite_Kind()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        new UserSettings { Child = true, Name = "Kind" }.Save(root);
        var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher(), offline: false);
        var w = new MainWindow(core, new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
        w.Show(); Settle();
        try
        {
            var dir = Notiz(root);
            w.Show("prog-pflege"); Settle();
            w.Find<Button>("trendo-prog-open-Portable-notiz").Press(); Settle();
            w.Find<Button>("prog-detail-remove").Press(); Settle();
            w.Find<Button>("prog-detail-remove-yes").Press(); Settle();
            Assert.True(File.Exists(Path.Combine(dir, "notiz.exe")));
            Assert.Contains(core.Audit.ReadAll(), a => a.Action == "rbac.denied" && a.Actor == "kinderprofil");
            Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Erwachsenen-Profil"));
        }
        finally { w.Close(); }
    }
}
