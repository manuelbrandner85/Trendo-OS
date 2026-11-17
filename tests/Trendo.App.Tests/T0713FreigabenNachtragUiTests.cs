using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0713 (Nachtrag zu B4) mit dem echten Sammel-Dialog: „alle fehlenden installieren“, „Reparieren“ und Ersteinrichtung fragen einmal mit
/// einer abwählbaren Liste und geben je gewählter Komponente genau eine Freigabe frei; Kinderprofil darf nicht; PIN-Hinweis in den Einstellungen.</summary>
public class T0713FreigabenNachtragUi
{
    static async Task Settle(int rounds = 10) { for (var i = 0; i < rounds; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); } Dispatcher.UIThread.RunJobs(); }
    static T Dlg<T>(Window dlg, string name) where T : Control => dlg.GetLogicalDescendants().OfType<T>().First(b => b.Name == name);

    static (MainWindow W, TrendoCore Core, string Root) Open(UserSettings? s = null, bool realDialog = true)
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        (s ?? new UserSettings { SetupDone = true }).Save(root);
        var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher(), offline: false);
        var w = new MainWindow(core, new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
        if (realDialog) { w.FreigabeConfirm = null; w.SammelConfirm = null; }
        w.Show(); Dispatcher.UIThread.RunJobs();
        return (w, core, root);
    }

    [AvaloniaFact(DisplayName = "T-0713 UI: „Fehlende Programme installieren“ – echter Sammel-Dialog mit Lizenz/Größe/Herkunft/Wirkung; abgewählt = keine Freigabe, gewählt = genau eine Freigabe und genau eine Ausführung im Tor (Testmodus)")]
    public async Task Alle_fehlenden_mit_Sammel_Dialog()
    {
        var (w, core, _) = Open();
        try
        {
            w.Show("components"); Dispatcher.UIThread.RunJobs();
            w.Find<Button>("btn-deps-consent").Press(); await Settle();
            var dlg = Assert.Single(w.OwnedWindows);
            Assert.DoesNotContain(core.Audit.ReadAll(), a => a.Action is "approval.requested" or "approval.approved" or "execution.start");
            var boxes = dlg.GetLogicalDescendants().OfType<CheckBox>().Where(c => c.Name?.StartsWith("sammel-") == true).ToList();
            Assert.True(boxes.Count >= 2, $"zu wenige fehlende Programme im Test: {boxes.Count}");
            Assert.All(boxes, b => Assert.True(b.IsChecked));
            var first = boxes[0].Name!["sammel-".Length..];
            var text = Dlg<TextBlock>(dlg, "sammel-text-" + first).Text!;
            Assert.Contains("Lizenz:", text); Assert.Contains("Größe:", text); Assert.Contains("Woher:", text); Assert.Contains("Wirkung:", text);
            var no = Dlg<Button>(dlg, "sammel-nein"); Assert.True(no.IsDefault); Assert.True(no.IsCancel);

            // alle bis auf den ersten abwählen
            foreach (var b in boxes.Skip(1)) b.IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            Assert.StartsWith("1 von", Dlg<TextBlock>(dlg, "sammel-summe").Text);
            Dlg<Button>(dlg, "sammel-ja").Press(); await Settle(20);
            Assert.Empty(w.OwnedWindows);

            var log = core.Audit.ReadAll();
            var requested = log.Where(a => a.Action == "approval.requested").Select(a => a.Detail.Split(' ')).ToList();
            var only = Assert.Single(requested);
            Assert.Equal([DependencyManager.InstallAction, first], only[1..]);
            Assert.Single(log, a => a.Action == "approval.approved" && a.Detail == only[0]);
            Assert.Single(log, a => a.Action == "execution.start" && a.Detail == $"{only[0]} {DependencyManager.InstallAction} {first}");
            Assert.Single(log, a => a.Action == "deps.install.testmodus" && a.Detail == first); // Testmodus: Freigabe eingelöst, nichts installiert
            foreach (var b in boxes.Skip(1)) Assert.DoesNotContain(log, a => a.Action == "deps.install.testmodus" && a.Detail == b.Name!["sammel-".Length..]);
            Assert.Contains(log, a => a.Action == "freigabe.sammel" && a.Detail.StartsWith("gewählt=" + first + " "));
            Assert.Equal(first, Assert.Single(w.LastSammel).C.Id);
            Assert.Throws<ApprovalException>(() => core.Approvals.Execute(only[0], DependencyManager.InstallAction, first, () => true)); // verbraucht

            // zweiter Durchgang: „Abbrechen“ – keine einzige Freigabe
            var before = core.Audit.ReadAll().Count(a => a.Action == "approval.requested");
            w.Show("components"); Dispatcher.UIThread.RunJobs();
            w.Find<Button>("btn-deps-consent").Press(); await Settle();
            Dlg<Button>(Assert.Single(w.OwnedWindows), "sammel-nein").Press(); await Settle();
            Assert.Empty(w.OwnedWindows);
            Assert.Equal(before, core.Audit.ReadAll().Count(a => a.Action == "approval.requested"));
            Assert.Contains(core.Audit.ReadAll(), a => a.Action == "freigabe.nein" && a.Detail.StartsWith("gewählt= "));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0713 UI: „Reparieren“ im Systemcheck fragt mit dem Sammel-Dialog; Abbrechen ändert nichts, Ja richtet genau diese Komponente über das Tor ein")]
    public async Task Reparieren_mit_Sammel_Dialog()
    {
        var (w, core, _) = Open();
        try
        {
            var installed = new List<string>();
            w.InstallOverride = c => { installed.Add(c.Id); return Task.FromResult(true); }; // Werkzeug ersetzt, Tor echt
            w.Show("systemcheck");
            Button? fix = null;
            for (var i = 0; i < 200 && fix is null; i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); fix = w.TryFind<Button>("fix-"); }
            Assert.NotNull(fix);
            var id = fix!.Name!["fix-".Length..];
            fix.Press(); await Settle();
            var dlg = Assert.Single(w.OwnedWindows);
            Assert.Equal("sammel-" + id, Assert.Single(dlg.GetLogicalDescendants().OfType<CheckBox>()).Name);
            Dlg<Button>(dlg, "sammel-nein").Press(); await Settle();
            Assert.Empty(installed);
            Assert.DoesNotContain(core.Audit.ReadAll(), a => a.Action == "approval.requested");

            for (var i = 0; i < 200 && (fix = w.TryFind<Button>("fix-" + id)) is null; i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); }
            fix!.Press(); await Settle();
            Dlg<Button>(Assert.Single(w.OwnedWindows), "sammel-ja").Press(); await Settle(20);
            Assert.Equal([id], installed);
            var log = core.Audit.ReadAll();
            Assert.Single(log, a => a.Action == "execution.start" && a.Detail.EndsWith($"{DependencyManager.InstallAction} {id}"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0713 UI: Kinderprofil – „Fehlende Programme installieren“ zeigt keine Rückfrage, legt keine Freigabe an, installiert nichts; Not-Stopp kann es nicht aufheben")]
    public async Task Kinderprofil_sammelt_nicht()
    {
        var (w, core, _) = Open(new UserSettings { SetupDone = true, Child = true, Name = "Kind" });
        try
        {
            var installed = new List<string>();
            w.InstallOverride = c => { installed.Add(c.Id); return Task.FromResult(true); };
            w.Show("components"); Dispatcher.UIThread.RunJobs();
            w.Find<Button>("btn-deps-consent").Press(); await Settle();
            Assert.Empty(w.OwnedWindows);
            Assert.Empty(installed);
            var log = core.Audit.ReadAll();
            Assert.Contains(log, a => a.Action == "freigabe.nicht.erlaubt" && a.Actor == "kinderprofil" && a.Detail.Contains("sammel:"));
            Assert.DoesNotContain(log, a => a.Action is "approval.requested" or "approval.approved" or "execution.start" or "deps.consent");

            await w.HaltAll("Test"); Assert.True(core.Approvals.Halted);
            Assert.Contains(core.Audit.ReadAll(), a => a.Action == "notstopp.aktiv" && a.Actor == "kinderprofil");
            w.ResumeAll();
            Assert.True(core.Approvals.Halted); // nur Erwachsene setzen fort
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0713 UI: Hinweis-Karte in den Einstellungen, wenn ein Erwachsenen-Profil ohne PIN neben einem Kinderprofil besteht; „PIN festlegen“ führt zur Anmeldung; mit PIN verschwindet sie")]
    public void Pin_Hinweis_in_Einstellungen()
    {
        var (w, _, root) = Open(realDialog: false);
        try
        {
            w.Show("settings"); Dispatcher.UIThread.RunJobs();
            Assert.Null(w.TryFind<Border>("pin-hinweis")); // kein Kind → kein Hinweis
            new PersonStore(root).Add("Kind", "emoji:🐻", child: true);
            w.Show("settings"); Dispatcher.UIThread.RunJobs();
            var card = w.Find<Border>("pin-hinweis");
            Assert.True(card.IsVisible);
            Assert.Contains("keine PIN", w.Find<TextBlock>("pin-hinweis-text").Text);
            var set = w.Find<Button>("pin-hinweis-festlegen");
            Assert.NotEqual(Avalonia.Media.Color.Parse(w.Settings.Accent), (set.Background as Avalonia.Media.ISolidColorBrush)?.Color); // nicht golden: keine zweite Hauptaktion
            set.Press();
            Assert.Equal("einrichtung", w.CurrentView);
            Assert.NotNull(w.TryFind<RadioButton>("wiz-lock-pin"));
            // die Personen-Seite zeigt denselben Hinweis
            w.Show("personen"); Dispatcher.UIThread.RunJobs();
            Assert.NotNull(w.TryFind<Border>("pin-hinweis"));
            // mit PIN: kein Hinweis mehr (nur Hinweis, nichts erzwungen)
            new ProfileLock(root).Set(ProfileLock.Kind.Pin, "4711");
            Assert.Empty(new PersonStore(root).AdultsWithoutLockBesideChild());
            w.Show("personen"); Dispatcher.UIThread.RunJobs(); // beide offenen Fenster neu aufbauen
            w.Show("settings"); Dispatcher.UIThread.RunJobs();
            Assert.Null(w.TryFind<Border>("pin-hinweis"));
        }
        finally { w.Close(); }
    }
}
