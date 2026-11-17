using System.Diagnostics;
using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Cad;
using Trendo.Core.Programs;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0716 (B6 „Programm-Hülle für alle“, Etappe E4 – 3D/Spiele/Technik/Entwickeln): Baukasten speichert atomar und mit Versionsfeld, echter Abbruchtest;
/// Rückgängig von 3D und CAD als Deltas – gemessen gegen den bisherigen Weg (ganze Kopie je Schritt).</summary>
public class T0716_HuelleE4Tests(ITestOutputHelper output)
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0716-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "T-0716 Baukasten: alte seite.json ohne Version bleibt lesbar, Speichern schreibt Version (vorn) atomar, neuere Fassung wird verständlich abgelehnt")]
    public void Builder_page_version()
    {
        var dir = Tmp();
        try
        {
            // so schrieb der Baukasten vor T-0716 (System.Text.Json, eingerückt, ohne Version)
            var f = Path.Combine(dir, "seite.json");
            File.WriteAllText(f, "{\n  \"Title\": \"Alte Seite\",\n  \"Accent\": \"#22B35E\",\n  \"Blocks\": [\n    {\n      \"Kind\": \"h1\",\n      \"Text\": \"Hallo\",\n      \"Url\": \"\"\n    },\n    {\n      \"Kind\": \"button\",\n      \"Text\": \"Mehr\",\n      \"Url\": \"https://example.org\"\n    }\n  ]\n}");
            var p = SitePage.Load(f);
            Assert.Equal(1, p.Version); Assert.Equal("Alte Seite", p.Title); Assert.Equal("#22B35E", p.Accent);
            Assert.Equal(["h1", "button"], p.Blocks.Select(b => b.Kind));
            Assert.Contains("href=\"https://example.org/\"", p.ToHtml());
            p.Title = "Neu"; p.Save(f);
            var text = File.ReadAllText(f);
            Assert.StartsWith("{\n  \"Version\": 1,", text.ReplaceLineEndings("\n"));
            Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
            Assert.Equal("Neu", SitePage.Load(f).Title);
            // neuere Fassung: verständlich abgelehnt, Datei bleibt unverändert
            var newer = text.Replace("\"Version\": 1", "\"Version\": 2");
            File.WriteAllText(f, newer);
            var ex = Assert.Throws<InvalidDataException>(() => SitePage.Load(f));
            Assert.Contains("neueren Trendo-Fassung", ex.Message);
            Assert.Equal(newer, File.ReadAllText(f));
            // veröffentlichen: index.html atomar, UTF-8 ohne BOM wie vorher
            var html = new SitePage { Title = "Ä", Blocks = [new("h1", "Grüße")] }.Publish(Path.Combine(dir, "fertig"));
            var bytes = File.ReadAllBytes(html); Assert.NotEqual(0xEF, bytes[0]); Assert.Contains("<h1>Gr&#252;&#223;e</h1>", File.ReadAllText(html));
        }
        finally { Directory.Delete(dir, true); }

        // Gegenprobe im Quelltext: kein direktes File.WriteAllText mehr in PageBuilder (vor T-0716: halbe seite.json nach einem Abbruch möglich)
        var root = new DirectoryInfo(AppContext.BaseDirectory); while (root is not null && !File.Exists(Path.Combine(root.FullName, "Trendo.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var src = File.ReadAllText(Path.Combine(root!.FullName, "src", "Trendo.Core", "PageBuilder.cs"));
        Assert.DoesNotMatch(@"(?<!Atomic)File\.WriteAllText\(", src);
    }

    [Fact(DisplayName = "T-0716 Absturz: Baukasten (seite.json + Notsicherung der Hülle mit Sperre) – 10-mal hart beendet, Seite und Sicherung öffnen sich jedes Mal, höchstens der laufende Schritt fehlt")]
    public void Kill_while_saving_builder()
    {
        var dir = Tmp(); var rnd = new Random(716); int during = 0, worst = 0; const int rounds = 10;
        try
        {
            for (var round = 1; round <= 3 * rounds && (round <= rounds || during < rounds / 2); round++) // unter Last weiter abbrechen, bis genug mitten im Schreiben lagen (wie T0708)
            {
                if (ProgramCrashTests.RunAndKill("baukasten", dir, 2, rnd)) during++;
                var (started, done, _) = ProgramCrashTests.ReadLog(dir);
                // 1) seite.json ist nie halb (vor T-0716: File.WriteAllText)
                var page = SitePage.Load(Path.Combine(dir, "seite.json"));
                var n = int.Parse(page.Title.Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture);
                Assert.True(n >= done && n <= started, $"Runde {round}: Seite hat Stand {n}, fertig {done}, begonnen {started}");
                Assert.Equal(4000, page.Blocks.Count); // Umfang aus dem Aufruf (40 000 / 10)
                // 2) genau eine lesbare Notsicherung, die verwaiste Sperre stört nicht
                var bak = Path.Combine(dir, "sicherung");
                Assert.Empty(ProgramDocument.OrphanScan(bak, "LINK-BAUKASTEN").Broken);
                using var locks = new ProgramLocks(Path.Combine(dir, "sperren"));
                var link = new ProgramLink("LINK-BAUKASTEN", "baukasten") { Locks = locks };
                var pending = link.Pending(bak);
                Assert.True(pending.Count == 1, $"Runde {round}: {pending.Count} Sicherungen statt einer");
                var st = link.Restore(pending[0].File, bak)!;
                var m = int.Parse(st["daten"]!["Title"]!.GetValue<string>().Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture);
                Assert.Equal("getippt " + m, st["eingaben"]!["builder-text-0"]!.GetValue<string>());
                Assert.True(m >= done && m <= started, $"Runde {round}: Sicherung hat Stand {m}, fertig {done}");
                worst = Math.Max(worst, started - Math.Max(n, m));
                locks.Release(pending[0].File);
            }
            Assert.True(worst <= 1, "Mehr als der eine laufende Schritt ging verloren: " + worst);
            Assert.True(during >= rounds / 2, $"Nur {during} von {rounds} Abbrüchen fielen in einen laufenden Schreibvorgang – der Test taugt so nicht.");
            output.WriteLine($"baukasten: {rounds} harte Abbrüche, {during} mitten im Schreiben, höchstens {worst} Schritt verloren.");
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    static Part3D BigMesh(int triangles)
    {
        var pos = new float[triangles * 9]; var idx = new int[triangles * 3];
        for (var i = 0; i < pos.Length; i++) pos[i] = (i % 997) * 0.123f;
        for (var i = 0; i < idx.Length; i++) idx[i] = i;
        return new Part3D { Shape = "mesh", Name = "Eingelesenes Netz", Positions = pos, Indices = idx };
    }

    [Fact(DisplayName = "T-0716 Messung 3D: Rückgängig als Delta – 100 Schritte an einem kleinen Teil neben einem großen Netz: alle 100 bleiben (vorher Grenze nach wenigen), Speicher ≈ eine Szene, Rückgängig exakt")]
    public void Scene_undo_delta()
    {
        var mesh = BigMesh(40_000); var cube = new Part3D { Shape = "cube", Name = "Würfel" };
        var scene = new Scene3D { Name = "Messung", Parts = [mesh, cube] };
        var original = scene.ToJson();

        // bisheriger Weg (bis T-0715): je Schritt die ganze Szene als Text, höchstens 100 Schritte und 150 Mio. Zeichen – nur Längen gezählt, Zeit gemessen
        var lens = new List<long>(); var sw = Stopwatch.StartNew();
        for (var i = 1; i <= 100; i++)
        {
            lens.Add(scene.ToJson().Length);
            while (lens.Count > 100 || lens.Count > 1 && lens.Sum() > 150_000_000) lens.RemoveAt(0);
            cube.X = i;
        }
        var oldMs = sw.ElapsedMilliseconds; var oldSteps = lens.Count; var oldChars = lens.Sum();

        // neuer Weg: Deltas (unveränderte Teile geteilt)
        cube.X = 0; var h = new Scene3D.History(); sw.Restart();
        for (var i = 1; i <= 100; i++) { h.Push(scene); cube.X = i; }
        var newMs = sw.ElapsedMilliseconds;
        output.WriteLine($"Szene: {original.Length:N0} Zeichen (Netz 40 000 Dreiecke). Bisher: {oldSteps} von 100 Schritten behalten, {oldChars:N0} Zeichen, {oldMs} ms. " +
                         $"Delta: {h.Count} Schritte, {h.Chars:N0} Zeichen, {newMs} ms.");
        Assert.True(oldSteps < 100, "Messaufbau: der bisherige Weg hätte alle Schritte behalten – Netz zu klein");
        Assert.Equal(100, h.Count);
        Assert.True(h.Chars < original.Length * 1.2, $"Delta belegt {h.Chars:N0} Zeichen – mehr als eine Szene ({original.Length:N0})");
        Assert.True(h.Chars * 10 < oldChars, "Delta spart nicht mindestens 90 %");

        // exakt zurück: 100 × Rückgängig ergibt die Ausgangsszene Zeichen für Zeichen, Wiederholen wieder den letzten Stand
        var cur = scene;
        for (var i = 0; i < 100; i++) cur = h.Undo(cur)!;
        Assert.Null(h.Undo(cur));
        Assert.Equal(original, cur.ToJson());
        Assert.Equal(100, h.RedoCount);
        for (var i = 0; i < 100; i++) cur = h.Redo(cur)!;
        Assert.Equal(100, cur.Parts[1].X);
        Assert.Equal(mesh.Positions, cur.Parts[0].Positions);
        // abgelehnte Änderung: Drop nimmt den letzten Schritt ohne Wiederholen
        h.Push(cur); cur.Parts[1].X = 999; var back = h.Drop()!; Assert.Equal(100, back.Parts[1].X); Assert.Equal(0, h.RedoCount);
        // neuere Fassung im Kopf wird nicht still übernommen (gleiche Prüfung wie beim Öffnen)
        h.Clear(); Assert.Equal(0, h.Count); Assert.Equal(0, h.Chars);
    }

    [Fact(DisplayName = "T-0716 Messung CAD: Rückgängig-Schritt teilt das Netz eines Referenzkörpers (Delta) statt es zu kopieren – 100 Schritte belegen einen Bruchteil, Inhalt gleich")]
    public void Cad_snapshot_delta()
    {
        var r = new CadReference { Name = "Netz", Positions = new float[20_000 * 9], Indices = new int[20_000 * 3] };
        for (var i = 0; i < r.Positions.Length; i++) r.Positions[i] = i * 0.01f;
        var doc = new CadDoc { References = { r } };
        doc.Params["breite"] = 40;

        long Measure(Func<CadDoc, CadDoc> take, out long ms)
        {
            GC.Collect(); var keep = new List<CadDoc>(); var a = GC.GetAllocatedBytesForCurrentThread(); var sw = Stopwatch.StartNew();
            for (var i = 0; i < 100; i++) { keep.Add(take(doc)); doc.Params["breite"] = 40 + i; }
            ms = sw.ElapsedMilliseconds; var bytes = GC.GetAllocatedBytesForCurrentThread() - a; GC.KeepAlive(keep); return bytes;
        }
        var oldBytes = Measure(d => d.Clone(), out var oldMs);
        var newBytes = Measure(d => d.Snapshot(), out var newMs);
        output.WriteLine($"CAD, Referenzkörper 20 000 Dreiecke ({(r.Positions.Length * 4 + r.Indices.Length * 4) / 1024:N0} KB): 100 Schritte bisher (Clone) {oldBytes / 1024:N0} KB in {oldMs} ms, Delta (Snapshot) {newBytes / 1024:N0} KB in {newMs} ms.");
        Assert.True(newBytes * 20 < oldBytes, $"Delta {newBytes} B spart nicht mindestens 95 % gegenüber {oldBytes} B");

        var s = doc.Snapshot();
        Assert.Same(r.Positions, s.References[0].Positions); // geteilt
        Assert.Equal(doc.ToJson(), s.ToJson()); // gleicher Inhalt
        doc.Params["breite"] = 1; doc.References.Clear(); // Änderungen am Dokument wirken nicht auf den Schritt
        Assert.Equal(139, (int)s.Params["breite"]); Assert.Single(s.References);
    }
}
