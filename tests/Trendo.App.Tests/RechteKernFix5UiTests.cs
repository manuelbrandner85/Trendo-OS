using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Sys;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0764, 5. Prüfrunde an der Oberfläche: Code mit KI (H3), Konsolen-Programme (H1), Prüfung im Hintergrund (M2), Papierkorb (N3).
/// Ohne statische Ersatz-Hooks; startet nichts echt (Testmodus, FakeLauncher).</summary>
public class RechteKernFix5Ui
{
    static void Settle() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    static (MainWindow W, FakeLauncher L, string Root) Open(bool child)
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        new UserSettings { Child = child, Name = child ? "Kind" : "Erika Muster" }.Save(root);
        var l = new FakeLauncher();
        var w = new MainWindow(new TrendoCore(root, new InMemorySecretStore(), l, offline: false), new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
        w.FreigabeConfirm = _ => Task.FromResult(true); w.Show(); Settle();
        return (w, l, root);
    }

    static ProgramDocument CodeProjekt(MainWindow w, string root)
    {
        var proj = Path.Combine(root, "projekt"); Directory.CreateDirectory(proj);
        File.WriteAllText(Path.Combine(proj, "rechner.py"), "x = 1\n"); File.WriteAllText(Path.Combine(proj, "test_rechner.py"), "import unittest\n");
        var d = w.ProgramNew("code-ki-programm"); w.CodeAiOpenFolder(d, proj);
        return d;
    }

    [AvaloniaFact(DisplayName = "T-0764 H3: Code mit KI – Kind: Tests starten nie (Rolle zuerst); Erwachsene: erst Rückfrage, „Nein“ startet nichts")]
    public async Task Code_mit_KI_Tests()
    {
        var (c, _, croot) = Open(true);
        try
        {
            var d = CodeProjekt(c, croot);
            Assert.Null(await c.CodeAiRunTests(d));
            Assert.Contains(c.Core.Audit.ReadAll(), a => a.Action == "rbac.denied" && a.Detail.Contains("codeai.test"));
            Assert.DoesNotContain(c.Core.Audit.ReadAll(), a => a.Action == "approval.requested");
        }
        finally { c.Close(); }
        var (w, _, root) = Open(false);
        try
        {
            var d = CodeProjekt(w, root);
            w.FreigabeConfirm = _ => Task.FromResult(false);
            Assert.Null(await w.CodeAiRunTests(d));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "approval.denied");
            Assert.Equal("code-ki.test", w.LastWirkung!.Action);
            Assert.DoesNotContain(w.Core.Audit.ReadAll(), a => a.Action == "execution.start");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0764 H1/N3: Kind – Konsolen-Programm mit klarem Hinweis abgelehnt, Kreatur sichtbar in Blender nicht; Papierkorb-Knopf ausgeblendet; Erwachsene: Knopf da")]
    public void Konsole_und_Papierkorb()
    {
        var (c, cl, croot) = Open(true);
        try
        {
            var code = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft VS Code", "Code.exe");
            Assert.False(c.LaunchAs(code, croot));
            Assert.Equal(Rbac.NurErwachseneKonsole, c.LastLaunchRefused);
            Assert.Empty(cl.Opened);
            Assert.False(c.Find<Button>("dock-trash").IsVisible);
        }
        finally { c.Close(); }
        var (w, _, _) = Open(false);
        try { Assert.True(w.Find<Button>("dock-trash").IsVisible); }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0764 M2: Kind startet ein eingerichtetes Trendo-Programm – Prüfung des ganzen Ordners im Hintergrund, danach gestartet; veränderter Ordner abgelehnt")]
    public async Task Pruefung_im_Hintergrund()
    {
        var (c, cl, croot) = Open(true);
        try
        {
            var dir = Path.Combine(croot, "werkzeuge", "sumatra"); Directory.CreateDirectory(Path.Combine(dir, "daten")); // 6. Prüfrunde (A): Komponente der Erlaubnisliste
            var exe = Path.Combine(dir, "SumatraPDF.exe"); File.WriteAllText(exe, "MZ"); File.WriteAllText(Path.Combine(dir, "daten", "a.txt"), "x");
            ProgramTrust.WriteMarker(dir, "notiz", "test");
            Assert.True(c.LaunchAs(exe)); // die Prüfung läuft im Hintergrund
            await c.ChildLaunchTask; Settle();
            Assert.True(ProgramTrust.Verified(exe, [Path.Combine(croot, "werkzeuge")])); // Ordner geprüft …
            Assert.Equal(Rbac.NurErwachseneProgramm, c.LastLaunchRefused); // … aber 7. Prüfrunde: Erlaubnisliste leer, Kinder starten kein Fremdprogramm
            File.WriteAllText(Path.Combine(dir, "daten", "neu.py"), "import os");
            Assert.False(ProgramTrust.Verified(exe, [Path.Combine(croot, "werkzeuge")]));
            Assert.True(c.LaunchAs(exe));
            await c.ChildLaunchTask; Settle();
            Assert.Empty(cl.Opened);
            Assert.Contains("Erwachsenen-Profil", c.LastLaunchRefused);
        }
        finally { c.Close(); }
    }
}
