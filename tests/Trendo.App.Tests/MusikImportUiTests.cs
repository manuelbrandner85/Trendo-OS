using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Fehler 07.11.2026: „Eigene Aufnahme“ zeigte im Dateidialog keine Lieder – jetzt alle Musik- und Videoformate, Start im Musik-Ordner, Umwandeln per FFmpeg.</summary>
public class MusikImport_Ui
{
    [AvaloniaFact(DisplayName = "Trendo Musik „＋ Eigene Aufnahme“: Dialog bietet MP3, M4A, AAC, OPUS, WMA, MP4 … an; M4A und MP4 werden echt in WAV umgewandelt und landen in der Bibliothek; Datei ohne Ton wird gemeldet")]
    public async Task Import_any_music()
    {
        var ff = Cli.Find("ffmpeg"); if (ff is null) return;
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            string Make(string name, params string[] args) { var f = Path.Combine(root, name); var r = Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", .. args, f], root, 60000); Assert.True(File.Exists(f), r.Err); return f; }
            var m4a = Make("Sommer.m4a", "-f", "lavfi", "-i", "sine=f=440:d=2", "-c:a", "aac");
            var mp4 = Make("Konzert.mp4", "-f", "lavfi", "-i", "testsrc2=s=160x90:r=10:d=2", "-f", "lavfi", "-i", "sine=f=330:d=2", "-shortest", "-pix_fmt", "yuv420p");
            var silentVideo = Make("Stumm.mp4", "-f", "lavfi", "-i", "testsrc2=s=160x90:r=10:d=1", "-pix_fmt", "yuv420p");
            var asked = new List<Trendo.App.MainWindow.PickRequest>(); var next = m4a;
            w.PickOverride = req => { asked.Add(req); return Task.FromResult<string[]?>([next]); };
            w.FfmpegRunOverride = a => Cli.Run(ff, a, root, 60000);
            w.Show("musik-studio"); Settle();
            w.Find<Button>("ms-import").Press();
            for (var i = 0; i < 200 && w.Songs.All().Count == 0; i++) { await Task.Delay(20); Settle(); }
            Assert.Contains("m4a", asked[0].Extensions); Assert.Contains("mp4", asked[0].Extensions); Assert.Contains("opus", asked[0].Extensions); Assert.Contains("wma", asked[0].Extensions);
            var s1 = w.Songs.All().Single();
            Assert.Equal(("Sommer", "eigen"), (s1.Title, s1.Kind)); Assert.EndsWith(".wav", s1.File);
            Assert.InRange(AudioFile.Read(s1.File).Seconds, 1.9, 2.2);
            var s2 = await w.MsImport(mp4);
            Assert.EndsWith("Konzert.wav", s2!.File); Assert.InRange(AudioFile.Read(s2.File).Seconds, 1.9, 2.2);
            Assert.Null(await w.MsImport(silentVideo));
            Assert.Equal(2, w.Songs.All().Count);
            Assert.Contains(w.GetLogicalDescendantsOfType<TextBlock>(), t => (t.Text ?? "").Contains("keine lesbare Tonspur"));
        }
        finally { w.Close(); }
    }
}
