using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0764, 14. Prüfrunde an der Oberfläche: Die Rolle kommt nicht mehr aus settings.json. Kind + „Einstellungen einlesen“ (Child:false),
/// Kind + „Zurücksetzen“, Kind + „Sicherung wiederherstellen“, Kind + Einrichtungsassistent, Kind + manipulierte und gelöschte settings.json –
/// jeweils bleibt es ein Kinderprofil. Erwachsene: Einlesen, Zurücksetzen und Assistent gehen weiter, ohne Rollenwechsel über Dateien.</summary>
public class RechteKernFix14Ui
{
    static void Settle() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    static string NewRoot(bool child)
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        new UserSettings { Child = child, Name = child ? "Kind" : "Erika Muster", SetupDone = true }.Save(root);
        return root;
    }

    static MainWindow Open(string root)
    {
        var w = new MainWindow(new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher(), offline: false), new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
        w.ProgramSetLevel(UserLevel.Erweitert); w.FreigabeConfirm = _ => Task.FromResult(true); w.Show(); Settle();
        return w;
    }

    static bool IsChild(MainWindow w) => !Rbac.Has(w.CurrentPrincipal, Permission.ManageSoftware);

    [AvaloniaFact(DisplayName = "T-0764 H1: Kind – „Einstellungen einlesen“ mit Child:false, fremdem Namen und Projektordner bleibt Kind; nur Bedienfelder werden übernommen. Erwachsene lesen weiter ein")]
    public void Einlesen()
    {
        foreach (var child in new[] { true, false })
        {
            var root = NewRoot(child); var w = Open(root);
            try
            {
                var f = Path.Combine(w.Files.Home, "x.json");
                A11y.ExportSettings(new UserSettings { Child = false, Theme = "hell", Name = "Boss", ProjectsFolder = Path.Combine(root, "AppData-Test"), LockOnStart = false }, f);
                w.Show("bedien-hilfen"); Settle();
                w.Find<TextBox>("settings-file").Text = f; w.Find<Button>("settings-import").Press(); Settle();
                var now = w.ReopenedAs ?? w;
                Assert.Equal(child, IsChild(now));
                var saved = UserSettings.Load(root);
                Assert.Equal("hell", saved.Theme); // Bedienfeld übernommen
                Assert.Equal(child ? "Kind" : "Erika Muster", saved.Name); Assert.NotEqual(Path.Combine(root, "AppData-Test"), saved.ProjectsFolder); Assert.Equal(child, saved.Child);
                now.Close();
                var again = Open(root); try { Assert.Equal(child, IsChild(again)); } finally { again.Close(); }
            }
            finally { w.Close(); }
        }
    }

    [AvaloniaFact(DisplayName = "T-0764 H2: Kind – „Trendo-Einstellungen zurücksetzen“ ist gesperrt und macht kein Erwachsenen-Profil; Erwachsene setzen zurück und bleiben Erwachsene")]
    public void Zuruecksetzen()
    {
        foreach (var child in new[] { true, false })
        {
            var root = NewRoot(child); var w = Open(root);
            try
            {
                w.Show("prog-pflege"); Settle();
                w.Find<Button>("reset-ask").Press(); Settle();
                w.Find<Button>("reset-yes").Press(); Settle();
                Assert.Equal(child, IsChild(w));
                var bak = Directory.Exists(Path.Combine(root, "sicherungen")) ? Directory.GetFiles(Path.Combine(root, "sicherungen"), "settings-*.json").Length : 0;
                Assert.Equal(child ? 0 : 1, bak); // Kind: nichts zurückgesetzt; Erwachsene: zurückgesetzt (mit Sicherung)
                if (child) Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "rbac.denied" && a.Detail.Contains("einstellungen.zuruecksetzen"));
                w.Close();
                if (!child) { var again = Open(root); try { Assert.False(IsChild(again)); } finally { again.Close(); } }
                // auch wenn settings.json wirklich weg ist (z. B. von außen gelöscht): Rolle aus der geschützten Quelle
                if (child) { File.Delete(UserSettings.PathIn(root)); var again = Open(root); try { Assert.True(IsChild(again)); } finally { again.Close(); } }
            }
            finally { w.Close(); }
        }
    }

    [AvaloniaFact(DisplayName = "T-0764 H1/H2: Kind – manipulierte settings.json (Child:false) auf der Platte und gelöschte settings.json: weiter Kind; Person aus personen.json ebenso")]
    public void Platte()
    {
        var root = NewRoot(true); var w = Open(root); Assert.True(IsChild(w)); w.Close();
        File.WriteAllText(UserSettings.PathIn(root), "{\"Child\":false,\"Name\":\"Boss\",\"SetupDone\":true}");
        var w2 = Open(root); try { Assert.True(IsChild(w2)); } finally { w2.Close(); }
        File.Delete(UserSettings.PathIn(root));
        var w3 = Open(root); try { Assert.True(IsChild(w3)); } finally { w3.Close(); }
        // weitere Person (Kind) im Hauptordner: settings.json der Person auf Child:false gesetzt → bleibt Kind
        var person = new PersonStore(root).Add("Mia", "emoji:🐼", true);
        var proot = new PersonStore(root).RootOf(person.Id);
        File.WriteAllText(UserSettings.PathIn(proot), "{\"Child\":false,\"SetupDone\":true}");
        var w4 = Open(proot); try { Assert.True(IsChild(w4)); } finally { w4.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0764 M: Kind – kein Einrichtungsassistent (auch nicht über die Seitenadresse), Projektordner außerhalb der Freigabe gilt nicht; Erwachsene: Assistent wie bisher")]
    public void Assistent_und_Projektordner()
    {
        foreach (var child in new[] { true, false })
        {
            var root = NewRoot(child); var w = Open(root);
            try
            {
                w.Show("einrichtung-2"); Settle();
                Assert.Equal(!child, w.TryFind<TextBox>("wiz-folder") is not null);
                w.Show("einrichtung"); Settle();
                Assert.Equal(!child, w.TryFind<Button>("wiz-next") is not null);
                Assert.Equal(child, IsChild(w));
            }
            finally { w.Close(); }
        }
        // Projektordner außerhalb der Freigabe (steht für AppData): Kind legt dort nichts an, sondern in „projekte“ im Datenordner
        var kid = NewRoot(true); var outside = Path.Combine(kid, "AppData-Test", "projekte-fremd");
        var s = UserSettings.Load(kid); s.ProjectsFolder = outside; s.Save(kid);
        var k = Open(kid);
        try
        {
            k.Show("bedien-hilfen"); Settle(); k.Find<Button>("settings-export").Press(); Settle(); // schreibt in WorkFolder("Einstellungen")
            Assert.False(Directory.Exists(outside));
            Assert.NotEmpty(Directory.GetFiles(Path.Combine(kid, "projekte", "Einstellungen"), "*.json"));
            Assert.StartsWith(Path.Combine(kid, "projekte"), k.ProjectsFolderInUse, StringComparison.OrdinalIgnoreCase);
        }
        finally { k.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0764 (14.): Kind – „Sicherung wiederherstellen“ ist gesperrt (Rolle und Dateien bleiben); Erwachsene stellen wieder her")]
    public void Wiederherstellen()
    {
        foreach (var child in new[] { true, false })
        {
            var root = NewRoot(child); var w = Open(root);
            try
            {
                w.Show("sicherung"); Settle();
                w.Find<Button>("btn-backup-now").Press(); Settle();
                for (var i = 0; i < 200 && w.TryFind<Button>("backup-restore-0") is null; i++) { Thread.Sleep(25); w.Show("sicherung"); Settle(); }
                File.WriteAllText(UserSettings.PathIn(root), "{\"Child\":false,\"Theme\":\"hell\",\"SetupDone\":true}");
                w.Find<Button>("backup-restore-0").Press(); Settle();
                w.Find<Button>("backup-restore-yes-0").Press(); Settle();
                Assert.Equal(child, IsChild(w));
                if (child) Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "rbac.denied" && a.Detail.Contains("sicherung.wiederherstellen"));
                else Assert.Equal("dunkel", UserSettings.Load(root).Theme); // Erwachsene: Stand der Sicherung zurück
            }
            finally { w.Close(); }
        }
    }
}
