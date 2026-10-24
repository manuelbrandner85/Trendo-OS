using Avalonia.Headless.XUnit;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0202: Auf jeder Seite der App stürzt kein Klick ab. Knöpfe ohne erkennbare Wirkung werden als Bericht ausgegeben
/// (TRENDO_BUTTON_REPORT=Datei); die strenge Wirkungsprüfung für die Alltagsseiten steht in <see cref="EveryButton_Alltag"/>.</summary>
public class EveryButton_AllPages
{
    /// <summary>Seiten, die der Test nicht klickt – nur mit Grund.</summary>
    static readonly Dictionary<string, string> Ausgenommen = new(StringComparer.Ordinal)
    {
        ["start"] = "Startbildschirm ist kein Fenster; seine Kacheln prüfen DeadButtonTests und StudioClickTests.",
        ["studio-deps"] = "Zwischenseite „Programm fehlt“ nur aus einem Studio heraus; geprüft in W4Studio3DUiTests (Blender) und den Studio-Tests.",
    };

    /// <summary>Langer Klick-Audit (ca. 35 Minuten): läuft nur mit TRENDO_ALL_BUTTONS=1, sonst sichtbar übersprungen.</summary>
    public static bool AllButtons => Environment.GetEnvironmentVariable("TRENDO_ALL_BUTTONS") == "1";

    [AvaloniaFact(DisplayName = "T-0202: Kein Klick auf irgendeiner Seite stürzt ab (Klick-Audit, Bericht der stummen Knöpfe)", Timeout = 3600000,
        Skip = "Langer Klick-Audit über alle Seiten: TRENDO_ALL_BUTTONS=1 setzen", SkipUnless = nameof(AllButtons))]
    public void Kein_Klick_stuerzt_ab()
    {
        var (w, l, root) = Ui.Open();
        var datei = Path.Combine(root, "auswahl.txt");
        File.WriteAllText(datei, "x");
        w.PickOverride = _ => Task.FromResult<string[]?>([datei]);
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (!Directory.Exists(downloads)) Directory.CreateDirectory(downloads);
        var pages = w.PageKeys.Where(p => !Ausgenommen.ContainsKey(p)).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        var befund = ButtonProbe.Require(w, l, root, pages);
        w.Close();
        if (Environment.GetEnvironmentVariable("TRENDO_BUTTON_REPORT") is { Length: > 0 } report) File.WriteAllLines(report, befund);
        var abstuerze = befund.Where(f => f.Contains("\tAbsturz ")).ToList();
        Assert.True(abstuerze.Count == 0, $"{abstuerze.Count} Klicks mit Absturz:\n" + string.Join("\n", abstuerze));
    }
}
