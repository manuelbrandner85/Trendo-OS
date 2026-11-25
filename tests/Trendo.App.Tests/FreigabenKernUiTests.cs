using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>B4 (T-0706): Freigaben in den Kern – ein Klick allein führt nichts aus, die Freigabe ist ein eigener Schritt der angemeldeten Person,
/// ausgeführt wird nur über den Kern mit genau dieser Freigabe; Kinderprofil darf nicht freigeben; keine Selbst-Freigaben im selben Handler.</summary>
public class FreigabenKernUi
{
    static void Settle() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }
    static Button DlgButton(Window dlg, string name) => dlg.GetLogicalDescendants().OfType<Button>().First(b => b.Name == name);
    static string RepoRoot() { for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent) if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return d.FullName; throw new DirectoryNotFoundException(); }

    [AvaloniaFact(DisplayName = "B4 UI: Klick auf „assimp einrichten“ allein installiert nichts – erst eigene Rückfrage; Abbrechen lehnt ab, erst „Einrichten“ gibt frei und löst genau einmal im Kern aus")]
    public void Klick_allein_fuehrt_nichts_aus()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.FreigabeConfirm = null; // echter Dialog
            MainWindow.AssimpDllOverride = ""; w.Show("3d"); Settle();
            w.Find<Button>("3d-assimp-setup").Press(); Settle();
            var dlg = Assert.Single(w.OwnedWindows);
            var log = w.Core.Audit.ReadAll();
            Assert.Contains(log, a => a.Action == "approval.requested" && a.Detail.EndsWith("deps.install assimp"));
            Assert.DoesNotContain(log, a => a.Action is "approval.approved" or "execution.start");
            var text = dlg.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Name == "freigabe-text").Text!;
            Assert.Contains("assimp", text); Assert.Contains("Lizenz", text); Assert.Contains("Größe", text);
            var no = DlgButton(dlg, "freigabe-nein"); Assert.True(no.IsDefault); Assert.True(no.IsCancel);
            no.Press(); Settle();
            Assert.Empty(w.OwnedWindows);
            log = w.Core.Audit.ReadAll();
            Assert.Contains(log, a => a.Action == "approval.denied");
            Assert.DoesNotContain(log, a => a.Action is "approval.approved" or "execution.start");
            Assert.Contains("abgebrochen", w.AssimpMessage);
            Assert.Empty(w.Core.Approvals.Pending());

            w.Show("3d"); Settle();
            w.Find<Button>("3d-assimp-setup").Press(); Settle();
            DlgButton(Assert.Single(w.OwnedWindows), "freigabe-ja").Press(); Settle();
            var id = w.LastFreigabe!.Id;
            log = w.Core.Audit.ReadAll();
            Assert.Contains(log, a => a.Action == "approval.approved" && a.Detail == id && a.Actor == Principal.LocalOwner.Name);
            Assert.Single(log, a => a.Action == "execution.start" && a.Detail == $"{id} deps.install assimp");
            Assert.Contains("Testmodus", w.AssimpMessage);
            // dieselbe Freigabe ist verbraucht
            Assert.Throws<ApprovalException>(() => w.Core.Approvals.Execute(id, DependencyManager.InstallAction, "assimp", () => true));
        }
        finally { MainWindow.AssimpDllOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "B4 UI: Kinderprofil – Rolle Benutzer aus dem Profil, keine Rückfrage, Kern lehnt ab, Freigaben-Seite gesperrt, nichts installiert")]
    public void Kinderprofil_gibt_nichts_frei()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        new UserSettings { Child = true, Name = "Kind" }.Save(root);
        var core = new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher(), offline: false);
        var w = new MainWindow(core, new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
        w.Show(); Settle();
        try
        {
            Assert.Equal(Role.User, w.CurrentPrincipal.Role);
            Assert.False(w.CanApprove);
            var asked = 0; w.FreigabeConfirm = _ => { asked++; return Task.FromResult(true); };
            MainWindow.AssimpDllOverride = ""; w.Show("3d"); Settle();
            w.Find<Button>("3d-assimp-setup").Press(); Settle();
            Assert.Equal(0, asked); Assert.Empty(w.OwnedWindows);
            var log = core.Audit.ReadAll();
            Assert.Contains(log, a => a.Action == "freigabe.nicht.erlaubt" && a.Actor == "kinderprofil");
            Assert.Contains(log, a => a.Action == "approval.withdrawn");
            Assert.DoesNotContain(log, a => a.Action is "approval.approved" or "execution.start");
            // auch eine offene Anfrage auf der Freigaben-Seite kann das Kinderprofil nicht freigeben
            var r = core.Integrations.PlanOpenUrl(Principal.LocalOwner, "https://example.org/");
            w.Show("approvals"); Settle();
            Assert.False(w.Find<Button>("btn-approve-" + r.Id).IsEnabled);
        }
        finally { MainWindow.AssimpDllOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "B4 UI: Freigaben-Seite öffnet fremde Wirkungen (z. B. Installieren) nie als Startziel – nur Hinweis und Ablehnen")]
    public void Freigabenseite_fuehrt_fremde_Wirkungen_nicht_aus()
    {
        var (w, l, _) = Ui.Open();
        try
        {
            var r = w.Core.Approvals.Request(Principal.LocalOwner, DependencyManager.InstallAction, "ffmpeg installieren", "ffmpeg");
            w.Show("approvals"); Settle();
            Assert.Null(w.TryFind<Button>("btn-approve-" + r.Id));
            Assert.NotNull(w.TryFind<TextBlock>("approval-elsewhere-" + r.Id));
            w.Find<Button>("btn-deny-" + r.Id).Press(); Settle();
            Assert.Empty(w.Core.Approvals.Pending());
            Assert.Empty(l.Opened);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "B4 UI: Ablauf „Freigeben …“ – der Klick zeigt Probelauf und Rückfrage; ohne Ja bleibt der Ablauf gesperrt")]
    public async Task Ablauf_freigeben_braucht_Ja()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var inbox = Path.Combine(root, "eingang"); Directory.CreateDirectory(inbox);
            var wf = w.Workflows.Add(new Workflow { Name = "Sortieren", Steps = [new() { Kind = StepKind.Verschieben, From = inbox, To = Path.Combine(inbox, "ziel") }] });
            var answers = new Queue<bool>([false, true]);
            w.FreigabeConfirm = _ => Task.FromResult(answers.Dequeue());
            w.WfSelected = wf.Id; w.Show("ablaeufe"); Settle();
            var btn = w.GetLogicalDescendants().OfType<Button>().First(b => b.Name?.StartsWith("wf-approve-") == true);
            btn.Press(); await Task.Delay(50); Settle();
            Assert.False(wf.IsApproved);
            Assert.Equal("workflow.approve", w.LastWirkung!.Action);
            Assert.False(string.IsNullOrEmpty(w.LastWirkung.Preview));
            w.Show("ablaeufe"); Settle();
            w.GetLogicalDescendants().OfType<Button>().First(b => b.Name?.StartsWith("wf-approve-") == true).Press(); await Task.Delay(50); Settle();
            Assert.True(wf.IsApproved);
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "execution.done" && a.Detail == w.LastFreigabe!.Id);
        }
        finally { w.Close(); }
    }

    [Fact(DisplayName = "B4 Architektur: keine Selbst-Freigabe im selben Handler – Approve nur im Freigabe-Baustein, Antworten auf Rückfrage-Karten nur an bekannten Stellen")]
    public void Keine_Selbstfreigabe_im_Code()
    {
        var app = Path.Combine(RepoRoot(), "src", "Trendo.App");
        var files = Directory.GetFiles(app, "*.cs", SearchOption.TopDirectoryOnly);
        // erlaubt: der Freigabe-Baustein, die Kommandozeile (--approve, ausdrücklich), die Claude-Brücke (eigene Rückfrage-Karte im Chat; Bereich B5/B6)
        var approveOk = new[] { "MainWindow.Freigabe.cs", "Program.cs" }; // T-0707: Claude-Brücke gibt nicht mehr selbst frei (FreigabeAusRueckfrage nach „Ja“ auf der Zustimmungskarte)
        string[] Hits(string pattern, params string[] ok) => files.Where(f => Regex.IsMatch(File.ReadAllText(f), pattern)).Select(f => Path.GetFileName(f)!).Where(n => !ok.Contains(n)).ToArray();
        Assert.Empty(Hits(@"\bApprovals\.Approve\(", approveOk));
        Assert.Empty(Hits(@"=\s*_?core\.Approvals\s*;")); // kein Umweg über eine zweite Variable
        // FreigabeAusRueckfrage / ExtApprove nur aus dem Ja-Knopf einer sichtbaren Rückfrage-Karte
        Assert.Empty(Hits(@"FreigabeAusRueckfrage\(", "MainWindow.Freigabe.cs", "MainWindow.AddHub.cs", "MainWindow.axaml.cs", "MainWindow.Live.cs", "MainWindow.Phone.cs", "MainWindow.Programs.cs", "MainWindow.W5Ext.cs", "MainWindow.WebApps.cs", "MainWindow.ClaudeBridge.cs", "MainWindow.KiSourceSetup.cs")); // T-0764: KiSourceInstall(karte: true) nur aus „Ja, laden“ (src-install-yes) bzw. hub-yes // ClaudeBridge: Ja-Knopf der Zustimmungskarte im Claude-Bereich
        Assert.Empty(Hits(@"\bExtApprove\(", "MainWindow.W5Ext.cs", "MainWindow.W5ExtCat.cs", "MainWindow.W5ExtMod.cs")); // T-0768 (T-0766 × T-0764): W5ExtMod – ExtGrantApproved läuft nur aus „Ja, diese Rechte erlauben“ der Rückfrage-Karte von ExtAskGrant (_extAsk)
        // Installieren nur über den Kern mit Freigabe – T-0713: ohne Ausnahme (Ersteinrichtung, „alle fehlenden“ und „Reparieren“ über die Sammel-Rückfrage)
        Assert.Empty(Hits(@"_deps\.InstallAsync\("));
        Assert.Empty(Hits(@"InstallOne\((?![^)]*approvalId)[^)]*\)"));
        Assert.Empty(Hits(@"GiveConsent\(Principal\.LocalOwner", "Program.cs"));
        // keine Stelle, die anfragt und wenige Zeilen später selbst freigibt
        var same = new List<string>();
        foreach (var f in files.Where(f => !approveOk.Contains(Path.GetFileName(f))))
        {
            var lines = File.ReadAllLines(f);
            for (var i = 0; i < lines.Length; i++)
                if (lines[i].Contains("Approvals.Request(") && lines.Skip(i).Take(6).Any(l => l.Contains("FreigabeErteilen(") || l.Contains("FreigabeAusEinstellung(") || Regex.IsMatch(l, @"\.Approve\(")))
                    same.Add($"{Path.GetFileName(f)}:{i + 1}");
        }
        // erlaubt: Dauerfreigabe durch die Einstellung „Freie Modelle automatisch laden“ (X05)
        Assert.Equal(["MainWindow.X05.cs"], same.Select(s => s.Split(':')[0]));
    }
}
