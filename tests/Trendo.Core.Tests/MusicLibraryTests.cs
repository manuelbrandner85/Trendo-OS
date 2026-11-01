using System.Text;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0306: Musik & Videos als Player – Tags (ID3v2.3/2.4, ID3v1, FLAC), Cover, Pfad-Rückfall, Alben, Wiedergabelisten, Warteschlange.
public class MusicLibraryTests
{
    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    static byte[] Synch(int n) => [(byte)(n >> 21 & 0x7F), (byte)(n >> 14 & 0x7F), (byte)(n >> 7 & 0x7F), (byte)(n & 0x7F)];
    static byte[] Frame(int ver, string id, byte[] data) => [.. Encoding.ASCII.GetBytes(id), .. ver == 4 ? Synch(data.Length) : [(byte)(data.Length >> 24), (byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length], 0, 0, .. data];

    /// <summary>MP3 mit ID3v2-Kopf: Titel (Latin1), Künstler (UTF-16 mit BOM), Album (UTF-8), Nummer, Jahr, Cover (PNG).</summary>
    public static byte[] Mp3(string title, string artist, string album, string track, string year, int ver = 3, bool cover = true)
    {
        var frames = new List<byte>();
        frames.AddRange(Frame(ver, "TIT2", [0, .. Encoding.Latin1.GetBytes(title)]));
        frames.AddRange(Frame(ver, "TPE1", [1, 0xFF, 0xFE, .. Encoding.Unicode.GetBytes(artist), 0, 0]));
        frames.AddRange(Frame(ver, "TALB", [3, .. Encoding.UTF8.GetBytes(album)]));
        frames.AddRange(Frame(ver, "TRCK", [0, .. Encoding.Latin1.GetBytes(track)]));
        frames.AddRange(Frame(ver, ver == 4 ? "TDRC" : "TYER", [0, .. Encoding.Latin1.GetBytes(year)]));
        if (cover) frames.AddRange(Frame(ver, "APIC", [0, .. Encoding.ASCII.GetBytes("image/png"), 0, 3, 0, .. Png]));
        frames.AddRange(new byte[20]); // Auffüllung
        return [.. "ID3"u8.ToArray(), (byte)ver, 0, 0, .. Synch(frames.Count), .. frames, 0xFF, 0xFB, 0x90, 0x00, .. new byte[400]];
    }

    static byte[] Le(int n) => BitConverter.GetBytes(n);
    static byte[] Be(int n) => [(byte)(n >> 24), (byte)(n >> 16), (byte)(n >> 8), (byte)n];

    [Fact(DisplayName = "T-0306: ID3v2.3 und 2.4 (Latin1, UTF-16, UTF-8), Cover; ID3v1; FLAC Vorbis + Bild; ohne Tags aus Ordner und Dateiname")]
    public void Tags()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-musik-" + Guid.NewGuid().ToString("N"));
        try
        {
            var alb = Directory.CreateDirectory(Path.Combine(dir, "Die Band", "Erstes Album")).FullName;
            File.WriteAllBytes(Path.Combine(alb, "a.mp3"), Mp3("Morgenlied", "Erika Müster", "Frühling", "3/10", "2024"));
            File.WriteAllBytes(Path.Combine(alb, "b.mp3"), Mp3("Abendlied", "Erika Müster", "Frühling", "1", "2024-05-01", ver: 4, cover: false));
            var v1 = new byte[300]; var tag = new byte[128]; "TAG"u8.CopyTo(tag); Encoding.Latin1.GetBytes("Alt").CopyTo(tag, 3); Encoding.Latin1.GetBytes("Oma").CopyTo(tag, 33); Encoding.Latin1.GetBytes("Kiste").CopyTo(tag, 63); Encoding.Latin1.GetBytes("1999").CopyTo(tag, 93); tag[126] = 7;
            File.WriteAllBytes(Path.Combine(alb, "c.mp3"), [.. v1, .. tag]);
            var vc = new List<byte>(); var vendor = "x"u8.ToArray(); string[] cs = ["TITLE=Wellen", "ARTIST=Meer", "ALBUM=Küste", "TRACKNUMBER=2", "DATE=2021"];
            vc.AddRange(Le(vendor.Length)); vc.AddRange(vendor); vc.AddRange(Le(cs.Length)); foreach (var kv in cs) { var kb = Encoding.UTF8.GetBytes(kv); vc.AddRange(Le(kb.Length)); vc.AddRange(kb); }
            var pic = new List<byte>(); pic.AddRange(Be(3)); pic.AddRange(Be(9)); pic.AddRange("image/png"u8.ToArray()); pic.AddRange(Be(0)); pic.AddRange(new byte[16]); pic.AddRange(Be(Png.Length)); pic.AddRange(Png);
            byte[] Block(int type, bool last, List<byte> d) => [(byte)((last ? 0x80 : 0) | type), (byte)(d.Count >> 16), (byte)(d.Count >> 8), (byte)d.Count, .. d];
            File.WriteAllBytes(Path.Combine(alb, "d.flac"), [.. "fLaC"u8.ToArray(), .. Block(4, false, vc), .. Block(6, true, pic), 0xFF, 0xF8, 0, 0]);
            File.WriteAllBytes(Path.Combine(alb, "04 - Ohne Tags.mp3"), new byte[200]);
            Directory.CreateDirectory(Path.Combine(dir, "Videos")); File.WriteAllBytes(Path.Combine(dir, "Videos", "Urlaub.mp4"), new byte[10]);

            var all = MusicLibrary.Scan([dir]);
            Track T(string f) => all.Single(t => Path.GetFileName(t.Path) == f);
            var a = T("a.mp3"); Assert.Equal(("Morgenlied", "Erika Müster", "Frühling", 3, "2024"), (a.Title, a.Artist, a.Album, a.No, a.Year));
            var b = T("b.mp3"); Assert.Equal(("Abendlied", 1, "2024"), (b.Title, b.No, b.Year));
            var c = T("c.mp3"); Assert.Equal(("Alt", "Oma", "Kiste", 7, "1999"), (c.Title, c.Artist, c.Album, c.No, c.Year));
            var d = T("d.flac"); Assert.Equal(("Wellen", "Meer", "Küste", 2, "2021"), (d.Title, d.Artist, d.Album, d.No, d.Year));
            var n = T("04 - Ohne Tags.mp3"); Assert.Equal(("Ohne Tags", "Die Band", "Erstes Album", 4), (n.Title, n.Artist, n.Album, n.No));
            Assert.True(T("Urlaub.mp4").Video);
            Assert.Equal(Png, MusicLibrary.Cover(a.Path)); Assert.Equal(Png, MusicLibrary.Cover(d.Path)); Assert.Null(MusicLibrary.Cover(b.Path));
            File.WriteAllBytes(Path.Combine(alb, "cover.jpg"), [1, 2, 3]); Assert.Equal([1, 2, 3], MusicLibrary.Cover(b.Path));
            var albums = MusicLibrary.Albums(all);
            var fr = albums.Single(x => x.Album == "Frühling"); Assert.Equal(["Abendlied", "Morgenlied"], fr.Tracks.Select(t => t.Title));
            Assert.DoesNotContain(albums, x => x.Album == "Videos");
            Assert.Single(MusicLibrary.Search(all, "wellen")); Assert.Equal(2, MusicLibrary.Search(all, "müster").Count);
            Assert.Equal("3:05", MusicLibrary.Time(185)); Assert.Equal("1:01:01", MusicLibrary.Time(3661));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0306: Warteschlange – weiter/zurück, Wiederholen aus/alle/eins, als Nächstes, verschieben, entfernen, Zufall behält aktuellen Titel und lässt sich zurücknehmen")]
    public void Queue()
    {
        var q = new PlayQueue(); q.Play(["a", "b", "c", "d"], 1);
        Assert.Equal("b", q.Current); Assert.Equal("c", q.Next()); Assert.Equal("d", q.Next()); Assert.Null(q.Next()); Assert.Equal("d", q.Current);
        q.Repeat = "alle"; Assert.Equal("a", q.Next()); q.Repeat = "eins"; Assert.Equal("a", q.Next(auto: true)); Assert.Equal("b", q.Next());
        Assert.Equal("a", q.Prev()); Assert.Equal("a", q.Prev());
        q.PlayNext("x"); Assert.Equal(["a", "x", "b", "c", "d"], q.Items);
        q.Move(4, 0); Assert.Equal(["d", "a", "x", "b", "c"], q.Items); Assert.Equal("a", q.Current);
        q.RemoveAt(0); Assert.Equal("a", q.Current); Assert.Equal(0, q.Index);
        q.SetShuffle(true, seed: 5); Assert.Equal("a", q.Items[0]); Assert.Equal(["a", "b", "c", "x"], q.Items.Order());
        q.Next(); var cur = q.Current; q.SetShuffle(false); Assert.Equal(["a", "x", "b", "c"], q.Items); Assert.Equal(cur, q.Current);
        var e = new PlayQueue(); Assert.Null(e.Next()); e.Enqueue("z"); Assert.Equal("z", e.Current);
    }

    [Fact(DisplayName = "T-0306: Wiedergabelisten anlegen, füllen (Reihenfolge, Doppelte erlaubt), Eintrag entfernen, löschen")]
    public void Lists()
    {
        using var f = new Fixture(); var p = new Playlists(f.Core.DataRoot);
        p.Create("Sommer"); Assert.Throws<ArgumentException>(() => p.Create("sommer"));
        Assert.Equal(3, p.Add("Sommer", ["a", "b", "a"])); Assert.Equal(["a", "b", "a"], p.Get("Sommer"));
        p.RemoveAt("Sommer", 0); Assert.Equal(["b", "a"], p.Get("Sommer")); Assert.Equal([("Sommer", 2)], p.All());
        p.Delete("Sommer"); Assert.Empty(p.All());
    }
}
