using System.IO.Compression;
using System.Text;
using Trendo.Core;
using Trendo.Core.Sys;
using Xunit;
namespace Trendo.Core.Tests;

public class Sys02_Alltag
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-s02-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "SYS-02-P05: Zwischenablage max. 50, keine Passwörter/Schlüssel, löschbar")]
    public void Clip()
    {
        var c = new ClipHistory(Tmp());
        for (var i = 0; i < 60; i++) Assert.True(c.Add("Text " + i));
        Assert.Equal(50, c.All().Count); Assert.Equal("Text 59", c.All()[0]);
        Assert.False(c.Add("Xy7#kLm9!pQr2")); Assert.False(c.Add("ghp_abcdefghijklmnop")); Assert.False(c.Add("passwort: geheim"));
        Assert.False(c.Add("normaler Text", fromPasswordField: true));
        Assert.True(c.Add("Hallo Welt, wie geht es dir?"));
        c.Delete(0); Assert.DoesNotContain("Hallo Welt, wie geht es dir?", c.All()); c.Clear(); Assert.Empty(c.All());
    }

    [Fact(DisplayName = "SYS-02-P07/P23: Aufnahme- und Audio-Befehle für ffmpeg")]
    public void Ffmpeg()
    {
        var w = FfmpegJobs.RecordArgs(true, "a.mp4", 30, "Mikrofon");
        Assert.Contains("gdigrab", w); Assert.Contains("audio=Mikrofon", w); Assert.Equal("a.mp4", w[^1]);
        Assert.DoesNotContain("-c:a", FfmpegJobs.RecordArgs(false, "b.mp4", 5));
        Assert.Throws<ArgumentException>(() => FfmpegJobs.RecordArgs(true, "c.mp4", 0));
        Assert.Throws<ArgumentException>(() => FfmpegJobs.AudioArgs("a.wav", "a.exe"));
    }

    [Fact(DisplayName = "SYS-02-P08/P09: Mediathek indiziert 2000 Dateien in < 5 s; Spielerwahl mpv → VLC → Standard")]
    public void Library()
    {
        var d = Tmp();
        for (var i = 0; i < 2100; i++) File.WriteAllText(Path.Combine(d, $"s{i}.{(i % 3 == 0 ? "mp3" : i % 3 == 1 ? "mp4" : "jpg")}"), "x");
        File.WriteAllText(Path.Combine(d, "liesmich.txt"), "x");
        var sw = System.Diagnostics.Stopwatch.StartNew(); var l = MediaLibrary.Index([d]); sw.Stop();
        Assert.Equal(2000, l.Count); Assert.True(sw.Elapsed.TotalSeconds < 5); Assert.DoesNotContain(l, x => x.Path.EndsWith(".txt"));
        Assert.Equal("mpv", MediaLibrary.Player("a.mp3", "mpv", "vlc")!.Value.Exe);
        Assert.Equal("vlc", MediaLibrary.Player("a.mp3", null, "vlc")!.Value.Exe);
        Assert.Null(MediaLibrary.Player("a.mp3", null, null));
    }

    [NeedsFact("ffmpeg", DisplayName = "SYS-02-P23 (T-0244 getrennt): Audio mit echtem ffmpeg umwandeln, abspielbar, Prüfsumme im Protokoll")]
    public void Ffmpeg_real()
    {
        var ff = Cli.Find("ffmpeg")!;
        var d = Tmp(); var wav = Path.Combine(d, "ton.wav");
        Assert.True(Cli.Run(ff, ["-y", "-f", "lavfi", "-i", "sine=frequency=440:duration=1", wav]).Ok);
        var log = new AuditLog(Path.Combine(d, "audit.jsonl"));
        foreach (var f in FfmpegJobs.AudioFormats)
        {
            var (ok, msg, sha) = FfmpegJobs.ConvertAudio(ff, wav, f, log);
            Assert.True(ok, msg); Assert.Equal(64, sha!.Length);
            Assert.True(Cli.Run(ff, ["-v", "error", "-i", Path.Combine(d, msg), "-f", "null", "-"]).Ok); // abspielbar/decodierbar
        }
        Assert.True(File.Exists(wav)); Assert.Contains(log.ReadAll(), e => e.Action == "audio.convert" && e.Detail.Contains("sha256="));
    }

    [Fact(DisplayName = "SYS-02-P11: Umbenennen – Vorschau = Ergebnis, Rückgängig-Datei, Konflikte erkannt")]
    public void Rename()
    {
        var d = Tmp(); foreach (var n in new[] { "b.txt", "a.txt", "c.txt" }) File.WriteAllText(Path.Combine(d, n), n);
        var plan = Renamer.Plan(Directory.GetFiles(d), "Datei_{n:000}");
        var u = Renamer.Apply(plan, Path.Combine(d, "undo"));
        Assert.Equal(plan.Select(m => Path.GetFileName(m.To)).OrderBy(x => x), Directory.GetFiles(d).Select(Path.GetFileName).OrderBy(x => x));
        Assert.Equal("a.txt", File.ReadAllText(Path.Combine(d, "Datei_001.txt")));
        Renamer.Undo(u); Assert.True(File.Exists(Path.Combine(d, "b.txt"))); Assert.False(File.Exists(u));
        Assert.Throws<ArgumentException>(() => Renamer.Plan(Directory.GetFiles(d), "gleich"));
        Assert.Throws<ArgumentException>(() => Renamer.Plan(Directory.GetFiles(d), "a/b{n}"));
        var swap = new List<Move> { new(Path.Combine(d, "a.txt"), Path.Combine(d, "b.txt")), new(Path.Combine(d, "b.txt"), Path.Combine(d, "a.txt")) };
        Renamer.Apply(swap, Path.Combine(d, "undo")); Assert.Equal("b.txt", File.ReadAllText(Path.Combine(d, "a.txt")));
    }

    [Fact(DisplayName = "SYS-02-P12/P13: Doppelte per SHA-256 (nichts gelöscht), Speicherplatz-Summen stimmen")]
    public void Analysis()
    {
        var d = Tmp(); Directory.CreateDirectory(Path.Combine(d, "x"));
        File.WriteAllText(Path.Combine(d, "1.txt"), "gleich"); File.WriteAllText(Path.Combine(d, "x", "2.txt"), "gleich"); File.WriteAllText(Path.Combine(d, "3.txt"), "andere");
        var g = Assert.Single(FileAnalysis.Duplicates(d)); Assert.Equal(2, g.Count); Assert.Equal(3, Directory.GetFiles(d, "*", SearchOption.AllDirectories).Length);
        File.WriteAllBytes(Path.Combine(d, "x", "gross.bin"), new byte[10000]);
        var (total, top) = FileAnalysis.Analyze(d);
        Assert.Equal(Directory.GetFiles(d, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length), total);
        Assert.True(top[0].IsFolder); Assert.Equal(10006, top[0].Bytes);
    }

    [Fact(DisplayName = "SYS-02-P14: ZIP packen/entpacken, Zip-Slip wird blockiert (nichts entpackt)")]
    public void Zip()
    {
        var d = Tmp(); var src = Path.Combine(d, "ordner"); Directory.CreateDirectory(Path.Combine(src, "sub")); File.WriteAllText(Path.Combine(src, "sub", "a.txt"), "A");
        Archives.Pack([src], Path.Combine(d, "o.zip")); Assert.Equal(1, Archives.Unpack(Path.Combine(d, "o.zip"), Path.Combine(d, "aus")));
        Assert.Equal("A", File.ReadAllText(Path.Combine(d, "aus", "ordner", "sub", "a.txt")));
        var evil = Path.Combine(d, "boese.zip");
        using (var z = ZipFile.Open(evil, ZipArchiveMode.Create)) { z.CreateEntry("ok.txt"); z.CreateEntry("../../ausbruch.txt"); }
        Assert.Throws<IOException>(() => Archives.Unpack(evil, Path.Combine(d, "ziel")));
        Assert.False(File.Exists(Path.Combine(d, "ziel", "ok.txt"))); Assert.False(File.Exists(Path.Combine(d, "ausbruch.txt")));
    }

    [Fact(DisplayName = "T-0243: Entpacken/Packen überschreibt nie still – Rückfrage nennt die Dateien, dann Überspringen, Beide behalten oder Ersetzen")]
    public void Zip_conflicts()
    {
        var d = Tmp(); var src = Path.Combine(d, "ordner"); Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "a.txt"), "neu"); File.WriteAllText(Path.Combine(src, "b.txt"), "B");
        var zip = Archives.Pack([src], Path.Combine(d, "o.zip"));
        var dest = Path.Combine(d, "aus"); Directory.CreateDirectory(Path.Combine(dest, "ordner"));
        File.WriteAllText(Path.Combine(dest, "ordner", "a.txt"), "meins");
        // Standard: Rückfrage, nichts verändert (auch b.txt nicht angelegt)
        var ex = Assert.Throws<ArchiveConflictException>(() => Archives.Unpack(zip, dest));
        Assert.Equal(["ordner/a.txt"], ex.Names);
        Assert.Equal("meins", File.ReadAllText(Path.Combine(dest, "ordner", "a.txt")));
        Assert.False(File.Exists(Path.Combine(dest, "ordner", "b.txt")));
        // Vorhandene behalten
        Assert.Equal(1, Archives.Unpack(zip, dest, ArchiveConflict.Skip));
        Assert.Equal("meins", File.ReadAllText(Path.Combine(dest, "ordner", "a.txt")));
        // Beide behalten
        Assert.Equal(2, Archives.Unpack(zip, dest, ArchiveConflict.KeepBoth));
        Assert.Equal("neu", File.ReadAllText(Path.Combine(dest, "ordner", "a (2).txt")));
        Assert.Equal("B", File.ReadAllText(Path.Combine(dest, "ordner", "b (2).txt")));
        Assert.Equal("meins", File.ReadAllText(Path.Combine(dest, "ordner", "a.txt")));
        // Ersetzen nur ausdrücklich
        Archives.Unpack(zip, dest, ArchiveConflict.Overwrite);
        Assert.Equal("neu", File.ReadAllText(Path.Combine(dest, "ordner", "a.txt")));
        // Packen: vorhandene ZIP-Datei bleibt, außer bei ausdrücklicher Wahl
        var before = File.ReadAllBytes(zip);
        Assert.Throws<ArchiveConflictException>(() => Archives.Pack([src], zip));
        Assert.Equal(before, File.ReadAllBytes(zip));
        Assert.Equal(Path.Combine(d, "o (2).zip"), Archives.Pack([src], zip, ArchiveConflict.KeepBoth));
        Assert.Equal(zip, Archives.Pack([src], zip, ArchiveConflict.Overwrite));
    }

    [Fact(DisplayName = "SYS-02-P15/P19: Erinnerung fällig ±2 s, nur einmal; Fokus endet automatisch")]
    public void Time()
    {
        var root = Tmp(); var r = new ErinnerungStore(root); var t0 = new DateTime(2026, 10, 21, 12, 0, 0);
        r.Add(t0.AddSeconds(10), "Tee");
        Assert.Empty(r.TakeDue(t0.AddSeconds(7)));
        Assert.Single(r.TakeDue(t0.AddSeconds(8)));
        Assert.Empty(r.TakeDue(t0.AddSeconds(20)));
        var f = new FocusMode(root); f.Start(TimeSpan.FromMinutes(25), t0);
        Assert.Equal(t0.AddMinutes(25), f.Until(t0.AddMinutes(10)));
        Assert.Null(f.Until(t0.AddMinutes(26))); Assert.Null(f.Until(t0)); // nach Ablauf zurückgesetzt
    }

    [Fact(DisplayName = "SYS-02-P16: Kalender – ICS-Rundweg verlustfrei (Sonderzeichen, Zeilenumbruch)")]
    public void Calendar()
    {
        var e = new CalEvent("u1", new DateTime(2026, 11, 3, 9, 30, 0), new DateTime(2026, 11, 3, 10, 15, 0), "Zahnarzt; Kontrolle, kurz", "Praxis Dr. Ä\\Ö", "Zeile 1\nZeile 2");
        var back = Assert.Single(Ics.Import(Ics.Export([e]))); Assert.Equal(e, back);
        var folded = "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:x\r\nDTSTART;TZID=Europe/Berlin:20261224T180000\r\nSUMMARY:Lange Besch\r\n reibung\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";
        var f = Assert.Single(Ics.Import(folded)); Assert.Equal("Lange Beschreibung", f.Title); Assert.Equal(new DateTime(2026, 12, 24, 19, 0, 0), f.End);
        var root = Tmp(); var c = new Trendo.Core.Sys.Calendar(root); c.Add(e); var file = Path.Combine(root, "t.ics"); c.ExportFile(file);
        var c2 = new Trendo.Core.Sys.Calendar(Tmp()); Assert.Equal(1, c2.ImportFile(file)); Assert.Equal(e, c2.All()[0]);
    }

    [Fact(DisplayName = "SYS-02-P17/P18: Einheiten, Währung nur mit eigenem Kurs, Farben HEX/RGB/HSL")]
    public void UnitsColors()
    {
        Assert.Equal(1.609344, Units.Convert(1, "meile", "km"), 6); Assert.Equal(212, Units.Convert(100, "°C", "°F"), 6); Assert.Equal(1000, Units.Convert(1, "kg", "g"), 6);
        Assert.Throws<ArgumentException>(() => Units.Convert(1, "kg", "m")); Assert.Equal(108, Units.Currency(100, 1.08), 6); Assert.Throws<ArgumentException>(() => Units.Currency(1, 0));
        Assert.Equal((47, 134, 246), Colors.ParseHex("#2F86F6")); Assert.Equal("#FFFFFF", Colors.Hex(255, 255, 255)); Assert.Equal((255, 0, 0), Colors.ParseHex("f00"));
        Assert.Equal((0, 100, 50), Colors.Hsl(255, 0, 0)); Assert.Throws<ArgumentException>(() => Colors.ParseHex("xyz123"));
    }

    [Fact(DisplayName = "SYS-02-P21: Papierkorb lesen (Windows $I v2 + Linux), Leeren nur mit Bestätigung")]
    public void TrashTest()
    {
        var d = Tmp(); var bin = Path.Combine(d, "bin"); Directory.CreateDirectory(bin);
        var name = @"C:\Users\x\Desktop\brief.docx" + "\0"; var nb = Encoding.Unicode.GetBytes(name);
        using (var ms = new MemoryStream()) using (var w = new BinaryWriter(ms))
        { w.Write(2L); w.Write(1234L); w.Write(new DateTime(2026, 10, 1, 12, 0, 0).ToFileTime()); w.Write(name.Length); w.Write(nb); File.WriteAllBytes(Path.Combine(bin, "$IABC123.docx"), ms.ToArray()); }
        File.WriteAllText(Path.Combine(bin, "$RABC123.docx"), "inhalt");
        var lt = Path.Combine(d, "Trash"); Directory.CreateDirectory(Path.Combine(lt, "info")); Directory.CreateDirectory(Path.Combine(lt, "files"));
        File.WriteAllText(Path.Combine(lt, "files", "foto.jpg"), "12345");
        File.WriteAllText(Path.Combine(lt, "info", "foto.jpg.trashinfo"), "[Trash Info]\nPath=/home/x/Bilder/foto%20neu.jpg\nDeletionDate=2026-10-20T10:00:00\n");
        var l = Trash.List(bin, lt);
        Assert.Equal(2, l.Count); Assert.Equal(@"C:\Users\x\Desktop\brief.docx", l.Single(x => x.Bytes == 1234).OriginalPath); Assert.Equal("/home/x/Bilder/foto neu.jpg", l[0].OriginalPath);
        Assert.Throws<InvalidOperationException>(() => Trash.Empty(l, false)); Assert.Equal(2, Trash.List(bin, lt).Count);
        Assert.Equal(2, Trash.Empty(l, true)); Assert.Empty(Trash.List(bin, lt)); Assert.Empty(Directory.GetFiles(bin));
    }

    [Fact(DisplayName = "SYS-02-P22: Fotos ordnen – EXIF-Datum, Simulation ändert nichts, rückgängig")]
    public void Photos()
    {
        var d = Tmp();
        var exif = Encoding.ASCII.GetBytes("Exif\0\0....2024:07:15 18:30:00\0");
        var jpg = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE1, (byte)((exif.Length + 2) >> 8), (byte)((exif.Length + 2) & 0xFF) }; jpg.AddRange(exif); jpg.AddRange([0xFF, 0xD9]);
        File.WriteAllBytes(Path.Combine(d, "urlaub.jpg"), jpg.ToArray());
        File.WriteAllText(Path.Combine(d, "ohne.png"), "x"); File.SetLastWriteTime(Path.Combine(d, "ohne.png"), new DateTime(2025, 3, 2));
        Assert.Equal(new DateTime(2024, 7, 15, 18, 30, 0), PhotoSorter.ExifDate(Path.Combine(d, "urlaub.jpg")));
        var p = PhotoSorter.Plan(d); Assert.Equal(2, Directory.GetFiles(d).Length);
        Assert.Contains(p, m => m.To.EndsWith(Path.Combine("2024", "2024-07", "urlaub.jpg"))); Assert.Contains(p, m => m.To.EndsWith(Path.Combine("2025", "2025-03", "ohne.png")));
        var u = Renamer.Apply(p, Path.Combine(d, "..", Path.GetFileName(d) + "-undo")); Assert.Empty(Directory.GetFiles(d));
        Renamer.Undo(u); Assert.Equal(2, Directory.GetFiles(d).Length);
    }

    [Fact(DisplayName = "SYS-02-P24/P25: Wetter offline ohne Fehler (nur Auswertung), Warnung bei RAM > 90 % / < 5 GB frei")]
    public void WidgetsWarn()
    {
        Assert.Equal("12 °C, Regen", Weather.Parse("""{"current":{"temperature_2m":12.3,"weather_code":63}}"""));
        Assert.Null(Weather.Parse("kein json")); Assert.Null(Weather.Parse("{}"));
        Assert.StartsWith("https://api.open-meteo.com/", Weather.Url(52.52, 13.4));
        Assert.Empty(ResourceWarn.Check(50, 100L << 30)); Assert.Equal(2, ResourceWarn.Check(95, 1L << 30).Count); Assert.Empty(ResourceWarn.Check(null, null));
    }
}
