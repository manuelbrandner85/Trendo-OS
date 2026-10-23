using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Installieren-Knopf nur gegen einen Fake. Ohne eingetragenen Installer bleibt er weg.</summary>
public class Paket44_InstallKnopf
{
    static Button B(Window w, string name) { var b = V<Button>(w, name); b.BringIntoView(); Settle(); return b; }

    sealed class Fake : IProgramInstaller
    {
        public ProgramOffer? Offer { get; set; }
        public InstallResult Result { get; set; } = new(true, "Fertig.");
        public bool Installed { get; set; }
        public List<string> Calls { get; } = [];
        public TaskCompletionSource? Halt { get; set; }
        public Task<ProgramOffer?> GetOfferAsync(string catalogId, CancellationToken ct) { Calls.Add("offer:" + catalogId); return Task.FromResult(Offer); }
        public async Task<InstallResult> InstallAsync(string catalogId, IProgress<InstallProgress> progress, CancellationToken ct)
        {
            Calls.Add("install:" + catalogId);
            progress.Report(new InstallProgress(40, "Paket wird geholt"));
            if (Halt is not null) await Halt.Task.WaitAsync(ct);
            ct.ThrowIfCancellationRequested();
            progress.Report(new InstallProgress(100, "Fertig"));
            return Result;
        }
        public Task<bool> IsInstalledAsync(string catalogId, CancellationToken ct) { Calls.Add("is:" + catalogId); return Task.FromResult(Installed); }
    }

    sealed class Runner : IProcessRunner
    {
        public List<string> Calls { get; } = [];
        public Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default)
        {
            Calls.Add(file + " " + args);
            return Task.FromResult((0, "Blender 4.2.0"));
        }
    }

    static ProgramOffer Frei(string? groesse = "400 MB", long? bytes = null) => new(
        "Blender", "BlenderFoundation.Blender", groesse, "GPL-3.0", "Kostenlos auf diesem PC.",
        true, false, "https://blender.org", null, bytes);

    static void Ein(MainWindow w, Fake fake)
    {
        w.InstallierenSichtbar = true;
        w.ProgramInstaller = fake;
    }

    static async Task Bis(Func<bool> da)
    {
        for (var i = 0; i < 50 && !da(); i++)
        {
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact(DisplayName = "Paket 44: ohne Installer und ohne Schalter gibt es keinen Installieren-Knopf")]
    public void Knopf_bleibt_verborgen()
    {
        var (w, _, _) = Ui.Open();
        var fake = new Fake { Offer = Frei() };
        w.Show("prog-extern"); Settle();
        Assert.Null(w.TryFind<Button>("fach-inst-blender"));
        w.ProgramInstaller = fake;
        w.Show("prog-extern"); Settle();
        Assert.Null(w.TryFind<Button>("fach-inst-blender"));
        Assert.Empty(fake.Calls);
        w.InstallierenSichtbar = true;
        w.ProgramInstaller = null;
        w.Show("prog-extern"); Settle();
        Assert.Null(w.TryFind<Button>("fach-inst-blender"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Paket 44: Rückfrage nennt Quelle und Größe, danach Prüfung, Abbruch lädt nicht weiter")]
    public async Task Frage_dann_install_oder_abbruch()
    {
        var (w, l, _) = Ui.Open();
        var fake = new Fake { Offer = Frei(null, 400_000_000) };
        var runner = new Runner();
        var da = false;
        w.Runner = runner;
        w.AppPathOverride = id => da && id == "blender" ? "/fake/blender" : null;
        Ein(w, fake);
        w.Show("prog-extern"); Settle();
        Assert.Empty(fake.Calls);
        RealClick(w, B(w, "fach-inst-blender"));
        await Bis(() => w.TryFind<Button>("fach-inst-yes-blender") is not null);
        var frage = w.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Name == "fach-inst-text-blender").Text!;
        Assert.Contains("BlenderFoundation.Blender", frage);
        Assert.Contains("400 MB", frage);
        Assert.Contains("GPL-3.0", frage);
        Assert.Equal(["offer:blender"], fake.Calls);
        Assert.Empty(l.Opened);

        fake.Halt = new TaskCompletionSource();
        RealClick(w, B(w, "fach-inst-yes-blender"));
        await Bis(() => (w.TryFind<TextBlock>("fach-inst-stand-blender")?.Text ?? "").Contains("40 Prozent"));
        Assert.Contains("install:blender", fake.Calls);
        Assert.DoesNotContain(fake.Calls, c => c.StartsWith("is:"));
        RealClick(w, B(w, "fach-inst-stop-blender"));
        await Bis(() => w.GetLogicalTexts().Any(t => t.Contains("Abgebrochen")));
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Abgebrochen") && t.Contains("nichts weiter geladen"));
        Assert.Empty(runner.Calls);
        Assert.Empty(l.Opened);

        da = true;
        fake.Installed = true;
        fake.Halt = null;
        fake.Calls.Clear();
        w.Show("prog-extern"); Settle();
        RealClick(w, B(w, "fach-inst-blender"));
        await Bis(() => w.TryFind<Button>("fach-inst-yes-blender") is not null);
        RealClick(w, B(w, "fach-inst-yes-blender"));
        await Bis(() => w.GetLogicalTexts().Any(t => t.Contains("Gefunden, nicht geprüft")));
        Assert.Contains(fake.Calls, c => c == "install:blender");
        Assert.Contains(fake.Calls, c => c == "is:blender");
        Assert.Empty(runner.Calls);
        Assert.DoesNotContain(runner.Calls, c => c.Contains("winget") || c.Contains("curl"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Paket 44: kostenpflichtig oder mit Extra-Schritt nur Hinweis und Link, kein Installieren")]
    public async Task Nur_Hinweis_mit_Link()
    {
        var (w, l, _) = Ui.Open();
        var fake = new Fake
        {
            Offer = Frei() with { Name = "Unreal Engine", IsFree = false, NeedsUserStep = true, Hinweis = "Dafür brauchst du ein Konto beim Hersteller.", InfoLink = "https://www.unrealengine.com/download" }
        };
        Ein(w, fake);
        w.Show("prog-extern"); Settle();
        RealClick(w, B(w, "fach-inst-unreal"));
        await Bis(() => w.TryFind<TextBlock>("fach-inst-text-unreal") is not null);
        Assert.Null(w.TryFind<Button>("fach-inst-yes-unreal"));
        var text = w.Find<TextBlock>("fach-inst-text-unreal").Text!;
        Assert.Contains("nicht automatisch", text);
        Assert.Contains("https://www.unrealengine.com/download", text);
        Assert.DoesNotContain(fake.Calls, c => c.StartsWith("install:"));
        Assert.Empty(l.Opened);
        RealClick(w, B(w, "fach-inst-link-unreal"));
        Assert.Contains(l.Opened, o => o.Contains("unrealengine.com"));
        Assert.DoesNotContain(fake.Calls, c => c.StartsWith("install:"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Paket 44: Fehlermeldung vom Installer bleibt deutsch und prüft nicht als Erfolg")]
    public async Task Fehler_bleibt_verstaendlich()
    {
        var (w, _, _) = Ui.Open();
        var fake = new Fake { Offer = Frei(), Result = new(false, "Die Quelle hat abgelehnt."), Installed = false };
        var runner = new Runner();
        w.Runner = runner;
        Ein(w, fake);
        w.Show("prog-extern"); Settle();
        RealClick(w, B(w, "fach-inst-blender"));
        await Bis(() => w.TryFind<Button>("fach-inst-yes-blender") is not null);
        RealClick(w, B(w, "fach-inst-yes-blender"));
        await Bis(() => w.GetLogicalTexts().Any(t => t.Contains("abgelehnt")));
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Die Quelle hat abgelehnt."));
        Assert.Empty(runner.Calls);
        Assert.DoesNotContain(fake.Calls, c => c.StartsWith("is:"));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "Paket 44: ohne Herstellerseite oder Paketkennung wird nicht installiert")]
    public async Task Quelle_muss_offiziell_sein()
    {
        var (w, l, _) = Ui.Open();
        var fake = new Fake { Offer = Frei() with { Quelle = "irgendwo aus dem Netz" } };
        Ein(w, fake);
        w.Show("prog-extern"); Settle();
        RealClick(w, B(w, "fach-inst-blender"));
        await Bis(() => w.GetLogicalTexts().Any(t => t.Contains("keine Herstellerseite")));
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("keine Herstellerseite") && t.Contains("nichts geladen"));
        Assert.Null(w.TryFind<Button>("fach-inst-yes-blender"));
        Assert.DoesNotContain(fake.Calls, c => c.StartsWith("install:"));
        Assert.Empty(l.Opened);
        w.Close();
    }
}
