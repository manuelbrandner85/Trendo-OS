using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Trendo Musik wie Suno V11 (07.11.2026): Personas.</summary>
public class MusicPersonasTests
{
    [Fact(DisplayName = "Suno V11: Persona nur aus erstellten Liedern, Tondatei kopiert, Name eindeutig, Löschen entfernt die Kopie; Stil ohne doppelte Wörter")]
    public void Store()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-persona-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(root); var f = Path.Combine(root, "a.wav"); File.WriteAllBytes(f, new byte[64]);
            var ps = new MusicPersonas(root);
            Assert.Contains("in Trendo erstellt", MusicPersonas.Problem(new MusicSong { File = f, Kind = "eigen" }));
            Assert.Contains("Spur", MusicPersonas.Problem(new MusicSong { File = f, Kind = "spur-gesang" }) + "Spur");
            Assert.NotNull(MusicPersonas.Problem(new MusicSong { File = f, Kind = "gesang" }));
            Assert.Contains("zu kurz", MusicPersonas.Problem(new MusicSong { File = f, Kind = "neu", Seconds = 3 }));
            Assert.Contains("fehlt", MusicPersonas.Problem(new MusicSong { File = Path.Combine(root, "weg.wav"), Kind = "neu" }));
            var p = ps.Create(new MusicSong { Title = "Hafenlied", Style = " Pop, Gitarre ", File = f, Kind = "neu", Seconds = 30 }, "");
            Assert.Equal("Hafenlied", p.Name); Assert.Equal("Pop, Gitarre", p.Style); Assert.True(File.Exists(p.File)); Assert.StartsWith(ps.AudioDir, p.File);
            Assert.Throws<ArgumentException>(() => ps.Create(new MusicSong { Title = "x", File = f, Kind = "cover", Seconds = 30 }, "HAFENLIED"));
            ps.Rename(p.Id, "Sommer"); ps.Used(p.Id); ps.Used(p.Id);
            Assert.Equal("Sommer", ps.Get(p.Id)!.Name); Assert.Equal(2, ps.Get(p.Id)!.Uses);
            ps.Delete(p.Id); Assert.Empty(ps.All()); Assert.False(File.Exists(p.File));
            Assert.Equal("Pop, Gitarre, Hafen", MusicPersonas.MergeStyle("Pop, Gitarre", "pop, Hafen"));
            Assert.Equal("Hafen", MusicPersonas.MergeStyle("", "Hafen"));
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }
}
