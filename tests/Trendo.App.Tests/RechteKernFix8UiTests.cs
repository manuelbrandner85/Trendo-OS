using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0764, 8. Prüfrunde an der Oberfläche: mpv-Argumente der eingebetteten Player (H), Trendos eigene Datei-Auswahl im Kinderprofil statt
/// des Windows-Dialogs (M1). Ohne statische Ersatz-Hooks; startet nichts echt.</summary>
public class RechteKernFix8Ui
{
    static void Settle() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    static (MainWindow W, string Root) Open(bool child)
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        new UserSettings { Child = child, Name = child ? "Kind" : "Erika Muster" }.Save(root);
        var w = new MainWindow(new TrendoCore(root, new InMemorySecretStore(), new FakeLauncher(), offline: false), new AppOptions(root, null, 0, false, Isolated: true), new Probe("browser"));
        w.ProgramSetLevel(UserLevel.Erweitert);
        w.FreigabeConfirm = _ => Task.FromResult(true); w.Show(); Settle();
        return (w, root);
    }

    [AvaloniaFact(DisplayName = "T-0764 H: eingebettete Player (Ton, Fernsehen) starten mpv ohne Einstellungen, Skripte und Standardtasten")]
    public void Mpv_eingebettet()
    {
        foreach (var args in new IReadOnlyList<string>[] { MpvAudio.StartArgs("t"), MpvView.Args("1", "t", false, "https://example.org/a.m3u8") })
        {
            foreach (var a in new[] { "--no-config", "--load-scripts=no", "--input-default-bindings=no" }) Assert.Contains(a, args);
            Assert.Contains(args, a => a.StartsWith("--input-conf=", StringComparison.Ordinal));
        }
    }

    static T Btn<T>(Window w, string name) where T : Control => w.GetLogicalDescendants().OfType<T>().First(c => c.Name == name);

    [AvaloniaFact(DisplayName = "T-0764 M1: Kind – Öffnen zeigt Trendos eigene Auswahl (nur eigene Ordner, nicht weiter nach oben); Speichern lehnt Programm-Namen ab")]
    public async Task Kind_Auswahl()
    {
        var (c, root) = Open(true);
        try
        {
            var home = c.Files.Home; Directory.CreateDirectory(home);
            var bild = Path.Combine(home, "bild.png"); File.WriteAllText(bild, "x"); File.WriteAllText(Path.Combine(home, "liste.txt"), "x");
            var open = c.PickOpenFiles(new FilePickerOpenOptions { Title = "Bild wählen", FileTypeFilter = [new FilePickerFileType("Bilder") { Patterns = ["*.png"] }] });
            Settle();
            var dlg = c.KidPickWindow ?? throw new Xunit.Sdk.XunitException("Trendos Auswahl ist nicht offen");
            Assert.DoesNotContain(dlg.GetLogicalDescendants().OfType<Button>(), b => b.Name == "kp-file-liste.txt"); // Filter
            Btn<Button>(dlg, "kp-up").Click2(); Settle();
            Assert.Contains("nicht", Btn<TextBlock>(dlg, "kp-msg").Text);
            Btn<Button>(dlg, "kp-file-bild.png").Click2(); Settle();
            Btn<Button>(dlg, "kp-ok").Click2(); Settle();
            await open;
            Assert.Equal([bild], c.KidPickLast);
            Assert.Contains(c.Core.Audit.ReadAll(), a => a.Action == "datei.auswahl.trendo");

            var save = c.PickSaveFile(new FilePickerSaveOptions { Title = "Speichern", SuggestedFileName = "spiel.exe", DefaultExtension = "txt" });
            Settle();
            var s = c.KidPickWindow!;
            Btn<Button>(s, "kp-ok").Click2(); Settle();
            Assert.Contains("keine Programme", Btn<TextBlock>(s, "kp-msg").Text);
            Btn<TextBox>(s, "kp-name").Text = "brief";
            Btn<Button>(s, "kp-ok").Click2(); Settle();
            await save;
            Assert.Equal([Path.Combine(home, "brief.txt")], c.KidPickLast);
        }
        finally { c.Close(); }
    }
}

static class KidPickTestExtensions
{
    public static void Click2(this Button b) => b.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
}
