using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// T-0306: Medien › Musik & Videos als Player – Alben, Abspielen, Fortschritt, Pause, Titelende → nächster, Zurück, Wiederholen, Springen, Warteschlange, Wiedergabeliste.
public class MusicPlayer_Ui
{
    sealed class FakeAudio : IAudioOut
    {
        public readonly List<string> Loads = []; public readonly List<double> Seeks = []; public bool? Paused; public int Volume = -1;
        public event Action<double, double>? Progress; public event Action? Ended;
        public void Load(string file) => Loads.Add(file);
        public void SetPause(bool pause) => Paused = pause;
        public void Seek(double seconds) => Seeks.Add(seconds);
        public void SetVolume(int percent) => Volume = percent;
        public void Stop() { }
        public void Dispose() { }
        public void Tick(double pos, double dur) => Progress?.Invoke(pos, dur);
        public void End() => Ended?.Invoke();
    }

    static byte[] Synch(int n) => [(byte)(n >> 21 & 0x7F), (byte)(n >> 14 & 0x7F), (byte)(n >> 7 & 0x7F), (byte)(n & 0x7F)];
    static byte[] Frame(string id, byte[] d) => [.. Encoding.ASCII.GetBytes(id), (byte)(d.Length >> 24), (byte)(d.Length >> 16), (byte)(d.Length >> 8), (byte)d.Length, 0, 0, .. d];
    static byte[] Mp3(string title, string artist, string album, int no)
    {
        byte[] frames = [.. Frame("TIT2", [3, .. Encoding.UTF8.GetBytes(title)]), .. Frame("TPE1", [3, .. Encoding.UTF8.GetBytes(artist)]), .. Frame("TALB", [3, .. Encoding.UTF8.GetBytes(album)]), .. Frame("TRCK", [0, .. Encoding.ASCII.GetBytes(no.ToString())])];
        return [.. "ID3"u8.ToArray(), 3, 0, 0, .. Synch(frames.Length), .. frames, .. new byte[300]];
    }

    [AvaloniaFact(DisplayName = "T-0306: Musik – Album öffnen und abspielen, Fortschritt/Pause, Titelende spielt den nächsten, Wiederholen, Springen per Leiste, Warteschlange, Liste anlegen und füllen")]
    public void Ui_music()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        void Click(string name) { var b = V<Control>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Control>(w, name)); Settle(); }
        var dir = Path.Combine(root, "Musik");
        var a = Directory.CreateDirectory(Path.Combine(dir, "Erika Muster", "Sommer")).FullName; var b2 = Directory.CreateDirectory(Path.Combine(dir, "Chor", "Winter")).FullName;
        File.WriteAllBytes(Path.Combine(a, "1.mp3"), Mp3("Sonne", "Erika Muster", "Sommer", 1)); File.WriteAllBytes(Path.Combine(a, "2.mp3"), Mp3("Strand", "Erika Muster", "Sommer", 2)); File.WriteAllBytes(Path.Combine(a, "3.mp3"), Mp3("Abend", "Erika Muster", "Sommer", 3));
        File.WriteAllBytes(Path.Combine(b2, "1.mp3"), Mp3("Schnee", "Chor", "Winter", 1));
        var fake = new FakeAudio(); w.AudioOverride = () => fake; w.MusicFoldersOverride = [dir];
        w.Show("medien"); Settle(); Click("media-tab-musik");
        for (var i = 0; i < 200 && w.MusicTracks.Count == 0; i++) { Thread.Sleep(15); Settle(); }
        Assert.Equal(4, w.MusicTracks.Count);
        // Alben sortiert nach Künstler: Chor/Winter, Erika Muster/Sommer
        Click("music-album-1"); Assert.Equal("Sommer", w.Find<TextBlock>("music-album-title").Text);
        Click("music-album-play");
        Assert.Equal(Path.Combine(a, "1.mp3"), fake.Loads[^1]); Assert.Equal("Sonne", w.Find<TextBlock>("music-now-title").Text);
        fake.Tick(30, 180); Settle();
        Assert.Equal("0:30", w.Find<TextBlock>("music-time").Text); Assert.Equal("3:00", w.Find<TextBlock>("music-duration").Text); Assert.Equal(30, w.Find<Slider>("music-seek").Value);
        Click("music-play"); Assert.True(fake.Paused); Assert.True(w.MusicPaused);
        Click("music-play"); Assert.False(fake.Paused);
        // Titelende → nächster
        fake.End(); Settle(); Assert.Equal(Path.Combine(a, "2.mp3"), fake.Loads[^1]);
        Click("music-next"); Assert.Equal(Path.Combine(a, "3.mp3"), fake.Loads[^1]);
        fake.End(); Settle(); Assert.True(w.MusicPaused); Assert.Equal(3, fake.Loads.Count); // Ende der Liste, Wiederholen aus
        Click("music-repeat"); Assert.Equal("alle", w.MusicQueue.Repeat);
        fake.End(); Settle(); Assert.Equal(Path.Combine(a, "1.mp3"), fake.Loads[^1]);
        Click("music-prev"); Assert.Equal(Path.Combine(a, "1.mp3"), fake.Loads[^1]); // erster Titel: bleibt
        // Springen über die Fortschrittsleiste (echter Klick)
        fake.Tick(10, 200); Settle(); Click("music-seek"); Assert.NotEmpty(fake.Seeks);
        // Lautstärke
        w.Find<Slider>("music-volume").Value = 40; Assert.Equal(40, fake.Volume);
        // Warteschlange: öffnen, Titel entfernen, Titel anspringen
        Click("music-queue"); Assert.NotNull(w.Find<StackPanel>("music-queue-panel"));
        Click("music-q-del-2"); Assert.Equal(2, w.MusicQueue.Items.Count);
        Click("music-q-1"); Assert.Equal(Path.Combine(a, "2.mp3"), fake.Loads[^1]);
        // Wiedergabeliste anlegen und über „Alle Titel“ füllen
        w.Find<TextBox>("music-list-name").Text = "Lieblinge"; Click("music-list-create");
        Click("music-nav-titel"); Click("music-addlist-0");
        Assert.Single(w.MusicLists.Get("Lieblinge"));
        Click("music-nav-list-0"); Click("music-list-play"); Assert.Equal(w.MusicLists.Get("Lieblinge")[0], fake.Loads[^1]);
    }
}
