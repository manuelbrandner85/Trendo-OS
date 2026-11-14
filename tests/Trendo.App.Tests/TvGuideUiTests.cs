using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core.Tv;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// T-0300: Programmführer als Raster – Tage, Zeit, Sparten, Suche, Aufnahme mit Überschneidung, Erinnerung mit Hinweis.
public class TvGuide_Ui
{
    const string M3U = "#EXTM3U\n#EXTINF:-1 tvg-id=\"eins.de\" group-title=\"Deutschland\",Eins\nhttps://tv.example/eins.m3u8\n#EXTINF:-1 tvg-id=\"zwei.de\" group-title=\"Deutschland\",Zwei\nhttps://tv.example/zwei.m3u8\n#EXTINF:-1 tvg-id=\"kika.de\" group-title=\"Kinder\",Kinderkanal\nhttps://tv.example/kika.m3u8\n";

    static string Cell(TvChannel c, TvProgramme p) => "tv-prog-" + c.Id.Replace(':', '-') + "-" + p.Start.ToString("MMddHHmm");

    [AvaloniaFact(DisplayName = "T-0300: Raster mit Jetzt-Linie, Tage und 20:15, Sparten-Filter, Suche springt zur Sendung, Aufnahme meldet Überschneidung, Erinnerung zeigt Hinweis")]
    public void Ui_guide()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        void Click(string name) { var b = V<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Button>(w, name)); Settle(); }
        var now = DateTime.UtcNow; DateTime M(int m) => now.AddMinutes(m);
        var tomorrow = DateTime.SpecifyKind(now.ToLocalTime().Date.AddDays(1).AddHours(20.25), DateTimeKind.Local).ToUniversalTime();
        var src = w.Tv.AddSource("Test", "datei", "liste.m3u"); w.Tv.StorePlaylist(src.Id, M3U);
        List<TvProgramme> progs = [
            new("eins.de", M(-10), M(20), "Tagesschau", "Nachrichten aus aller Welt", "Nachrichten"), new("eins.de", M(20), M(110), "Tatort", "Ein Fall für zwei", "Krimi"),
            new("zwei.de", M(-30), M(60), "Fußball live", "", "Sport"), new("zwei.de", M(60), M(150), "Krimi-Abend", "", "Serie"),
            new("kika.de", M(2), M(10), "Sandmann", "", "Kinder"), new("eins.de", tomorrow, tomorrow.AddMinutes(90), "Tatort", "Neuer Fall", "Krimi")];
        w.Tv.StoreEpg(src.Id, progs);
        var ch = w.Tv.Channels(); var eins = ch.First(c => c.EpgId == "eins.de"); var zwei = ch.First(c => c.EpgId == "zwei.de"); var kika = ch.First(c => c.EpgId == "kika.de");
        w.Show("tv"); Settle(); Click("tv-tab-guide");
        // Raster: alle Sender, Jetzt-Linie, laufende Sendung als Zelle
        Assert.NotNull(w.Find<Canvas>("tv-guide-nowline"));
        Assert.Equal(4, w.Find<StackPanel>("tv-guide-channels").Children.Count); // Kopf + 3 Sender
        Assert.NotNull(w.Find<Button>(Cell(eins, progs[0])));
        Assert.Null(w.TryFind<Button>(Cell(eins, progs[5]))); // morgen: noch nicht im Fenster
        // echte Maus auf eine Zelle (Jetzt-Linie darf nicht im Weg sein)
        Click(Cell(zwei, progs[2]));
        Assert.Contains(w.GetLogicalTexts(), t => t == "Fußball live");
        Assert.NotNull(w.Find<StackPanel>("tv-prog-detail"));
        // Sparten-Filter dimmt andere Sparten
        Click("tv-guide-kind-sport");
        Assert.Equal(1, w.Find<Button>(Cell(zwei, progs[2])).Opacity);
        Assert.True(w.Find<Button>(Cell(eins, progs[1])).Opacity < 0.5);
        Click("tv-guide-kind-alle");
        // Morgen, 20:15
        Click("tv-guide-day-1");
        Assert.Null(w.TryFind<Canvas>("tv-guide-nowline"));
        Click("tv-guide-prime");
        Assert.NotNull(w.Find<Button>(Cell(eins, progs[5])));
        Click("tv-guide-now");
        Assert.NotNull(w.Find<Canvas>("tv-guide-nowline"));
        // Suche über alle Tage, Treffer springt hin
        var find = w.Find<TextBox>("tv-guide-find"); find.Text = "tatort";
        find.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter }); Settle();
        Assert.Equal(3, w.Find<StackPanel>("tv-guide-hits").Children.Count); // Kopf + 2 Termine
        Click("tv-guide-hit-1");
        Assert.NotNull(w.Find<Button>(Cell(eins, progs[5])));
        Assert.Contains(w.GetLogicalTexts(), t => t == "Neuer Fall");
        Click("tv-guide-find-clear");
        Assert.Null(w.TryFind<StackPanel>("tv-guide-hits"));
        Click("tv-guide-now");
        // Aufnahme planen, zweite überschneidet sich → Hinweis, Merker ⏺, entfernen
        w.Find<Button>(Cell(eins, progs[1])).Press(); Settle();
        Click("tv-prog-rec");
        w.Find<Button>(Cell(zwei, progs[3])).Press(); Settle();
        Click("tv-prog-rec");
        Assert.Equal(2, w.Tv.Recordings.Count);
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("überschneidet sich mit „Tatort“"));
        Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("●"));
        Click("tv-prog-unrec");
        Assert.Single(w.Tv.Recordings);
        // Erinnerung: an, fällig → Hinweis mit „Einschalten“, nur einmal
        w.Find<Button>(Cell(kika, progs[4])).Press(); Settle();
        Click("tv-prog-remind");
        Assert.True(w.Tv.HasReminder(kika.Id, progs[4].Start));
        Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("Trendo erinnert dich"));
        w.TvReminderTick(); Settle();
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("„Sandmann“ auf Kinderkanal"));
        var toasts = w.Find<StackPanel>("toast-host").Children.Count;
        w.TvReminderTick(); Settle();
        Assert.Equal(toasts, w.Find<StackPanel>("toast-host").Children.Count);
    }
}
