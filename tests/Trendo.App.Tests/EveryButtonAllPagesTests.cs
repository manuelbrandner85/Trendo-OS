using Avalonia.Headless.XUnit;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0202: Auf jeder Seite der App wirkt jeder sichtbare, aktive Knopf (Seitenwechsel, Programm, Datei oder Text) und kein Klick stürzt ab.</summary>
public class EveryButton_AllPages
{
    /// <summary>Seiten, die der Test nicht klickt – nur mit Grund.</summary>
    static readonly Dictionary<string, string> Ausgenommen = new(StringComparer.Ordinal)
    {
        ["start"] = "Startbildschirm ist kein Fenster; seine Kacheln prüfen DeadButtonTests und StudioClickTests.",
        ["studio-deps"] = "Zwischenseite „Programm fehlt“ nur aus einem Studio heraus; geprüft in W4Studio3DUiTests (Blender) und den Studio-Tests.",
    };

    [AvaloniaFact(DisplayName = "T-0202: Jeder sichtbare Knopf auf jeder Seite wirkt, kein Klick stürzt ab", Timeout = 3600000)]
    public void Jeder_Knopf_auf_jeder_Seite()
    {
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (!Directory.Exists(downloads)) Directory.CreateDirectory(downloads);
        (MainWindow, FakeLauncher, string) Open()
        {
            var (w, l, root) = Ui.Open();
            var datei = Path.Combine(root, "auswahl.txt");
            File.WriteAllText(datei, "x");
            w.PickOverride = _ => Task.FromResult<string[]?>([datei]);
            return (w, l, root);
        }
        var (w0, _, _) = Ui.Open();
        var pages = w0.PageKeys.Where(p => !Ausgenommen.ContainsKey(p)).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        w0.Close();
        var fehl = ButtonProbe.RequireFresh(Open, pages);
        if (Environment.GetEnvironmentVariable("TRENDO_BUTTON_REPORT") is { Length: > 0 } report) File.WriteAllLines(report, fehl);
        Assert.True(fehl.Count == 0, $"{fehl.Count} Knöpfe ohne Wirkung oder mit Absturz:\n" + string.Join("\n", fehl));
    }
}
