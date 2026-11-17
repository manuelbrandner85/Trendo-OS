using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0749 Echtlauf Einscannen (SPEC-09): ein Fotosatz wird über die Oberfläche („Einlesen und prüfen“, „Werkzeug prüfen“, „Modell rechnen“)
/// mit dem echten COLMAP dieses PCs zu einem Netz gerechnet, angezeigt und als OBJ/STL/GLB gespeichert. Ersetzt ist nur die Suche nach der Programmdatei:
/// Das Testfenster ist isoliert und sieht absichtlich keine installierten Programme – den Pfad liefert die Erkennung der App (<see cref="DependencyManager.ResolvePath"/>)
/// ohne Isolierung. Prozessstart, Ausgabe, Abbruch und Datei sind echt.
/// Ohne <c>TRENDO_T0749_FOTOS</c> (Ordner mit Fotos, z. B. von <c>tools/echtlauf/einscannen/t0749_bilder.py</c>) wird übersprungen; ohne COLMAP: BLOCKIERT.
/// Belege: Ordner aus <c>TRENDO_BELEG_DIR</c> (sonst Temp\trendo-t0749-beleg).</summary>
public class T0749EinscannenEcht_Ui
{
    static (string Fotos, string Colmap, string Beleg) Need()
    {
        var fotos = Environment.GetEnvironmentVariable("TRENDO_T0749_FOTOS");
        if (string.IsNullOrWhiteSpace(fotos) || !Directory.Exists(fotos)) Assert.Skip("Echtlauf aus: TRENDO_T0749_FOTOS (Ordner mit mindestens 3 Fotos) ist nicht gesetzt.");
        var deps = new DependencyManager(new AuditLog(Path.Combine(Path.GetTempPath(), "trendo-t0749-needs.jsonl")), new RealProcessRunner());
        var colmap = deps.ResolvePath(DependencyCatalog.Get("colmap"));
        if (colmap is null) Assert.Skip("BLOCKIERT – COLMAP fehlt auf diesem PC. Einrichten: Trendo → „Gegenstände einscannen“ → „Werkzeug prüfen“ → „Jetzt einrichten“ (COLMAP 4.2.1 aus dem Vollpaket bzw. der offiziellen GitHub-Quelle, Prüfsumme fest im Katalog).");
        var beleg = Path.Combine(Environment.GetEnvironmentVariable("TRENDO_BELEG_DIR") is { Length: > 0 } d ? d : Path.Combine(Path.GetTempPath(), "trendo-t0749-beleg"));
        Directory.CreateDirectory(beleg);
        return (fotos!, colmap, beleg);
    }

    static string Sha(string f) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant();

    static void Pump(Func<bool> done, TimeSpan max)
    {
        var sw = Stopwatch.StartNew();
        while (!done() && sw.Elapsed < max) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(50); }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Kopf einer PLY-Datei: Anzahl Punkte und Flächen.</summary>
    static (int Vertices, int Faces) PlyCounts(string file)
    {
        var b = File.ReadAllBytes(file);
        var head = Encoding.ASCII.GetString(b, 0, Math.Min(b.Length, 4096));
        int Count(string el) => head.Split('\n').Select(l => l.Trim().Split(' ')).Where(p => p.Length == 3 && p[0] == "element" && p[1] == el).Select(p => int.Parse(p[2])).FirstOrDefault();
        return (Count("vertex"), Count("face"));
    }

    [AvaloniaFact(DisplayName = "T-0749 Echtlauf: Fotos → COLMAP → Netz im Scan-Arbeitsplatz → OBJ/STL/GLB")]
    public void Fotos_zu_netz()
    {
        var (fotos, colmap, belegDir) = Need();
        var log = new StringBuilder();
        void L(string s) => log.Append(s).Append('\n');
        var (w, _, root) = Ui.Open();
        try
        {
            L($"# T-0749 Echtlauf Einscannen – {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            L($"COLMAP: {Path.GetFileName(colmap)} (Erkennung der App)");
            w.ScanToolOverride = colmap;
            w.Show("scan3d");
            w.Find<TextBox>("scan-photos-folder").Text = fotos;
            w.Find<Button>("scan-load").Press();
            Assert.True(w.PhotoReport!.Count >= 3);
            L($"Fotos: {w.PhotoReport.Count}, {w.Find<TextBlock>("scan-count").Text}");
            w.Find<Button>("scan-check").Press();
            Pump(() => w.ScanStatusText.Contains("ist da"), TimeSpan.FromSeconds(30));
            Assert.Contains("COLMAP ist da", w.ScanStatusText);

            var sw = Stopwatch.StartNew();
            w.Find<Button>("scan-run").Press();
            Pump(() => w.ScanSteps.Count > 0 && !w.ScanBusy, TimeSpan.FromMinutes(20));
            sw.Stop();
            L($"Schritte: {string.Join(" → ", w.ScanSteps)}");
            L($"Laufzeit über die Oberfläche: {sw.Elapsed.TotalSeconds:0.0} s");
            L($"Status: {w.ScanStatusText}");
            var work = Directory.GetDirectories(Path.Combine(root), "*", SearchOption.AllDirectories).Where(d => File.Exists(Path.Combine(d, "datenbank.db"))).OrderByDescending(Directory.GetCreationTimeUtc).FirstOrDefault();
            if (work is not null && File.Exists(Path.Combine(work, "rechnung-protokoll.txt")))
            {
                var raw = Path.Combine(belegDir, "T-0749-colmap-rohlog.txt");
                File.Copy(Path.Combine(work, "rechnung-protokoll.txt"), raw, true);
                L($"Rohlog: {Path.GetFileName(raw)}");
            }
            Assert.True(w.ScanModelPath is not null, "Kein Modell: " + w.ScanStatusText);
            Assert.Contains("Dreiecke", w.ScanStatusText);

            var model = w.ScanModelPath!;
            var sparse = PhotoScan.SparseModel(work!)!;
            var points3d = File.ReadAllBytes(Path.Combine(sparse, "points3D.bin"));
            var points = BitConverter.ToUInt64(points3d, 0);
            var images = BitConverter.ToUInt64(File.ReadAllBytes(Path.Combine(sparse, "images.bin")), 0);
            var toolMesh = PhotoScan.FindToolMesh(work!)!;
            var (tv, tf) = PlyCounts(toolMesh);
            var view = w.Find<MeshView>("scan-view");
            L($"Kameramodell: {images} Kameras eingepasst, {points} 3D-Punkte");
            L($"COLMAP-Netz {Path.GetFileName(toolMesh)}: {tv} Punkte, {tf} Dreiecke");
            L($"Trendo-Netz modell.obj: {view.Model!.Triangles} Dreiecke (Anzeige scan-view)");
            Assert.True(images >= 3, "zu wenige Kameras");
            Assert.True(points > 100, "zu wenige Punkte");
            Assert.True(view.Model.Triangles > 100, "zu wenige Dreiecke");

            var files = new Dictionary<string, string>();
            foreach (var ext in new[] { "obj", "stl", "glb" })
            {
                w.Find<Button>("scan-export-" + ext).Press();
                var f = Directory.GetFiles(Path.Combine(root), "Scan *." + ext, SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).First();
                var keep = Path.Combine(belegDir, "T-0749-scan." + ext);
                File.Copy(f, keep, true);
                if (ext == "obj" && File.Exists(Path.ChangeExtension(f, ".mtl"))) File.Copy(Path.ChangeExtension(f, ".mtl"), Path.Combine(belegDir, Path.GetFileName(Path.ChangeExtension(f, ".mtl"))), true); // „mtllib“ nennt den Originalnamen
                files[ext] = keep;
                L($"Export {ext}: {new FileInfo(keep).Length} Bytes, SHA-256 {Sha(keep)}");
            }
            var mesh = Mesh3D.ReadMeshFile(files["glb"]);
            Assert.Equal(view.Model.Triangles, mesh.Triangles); // GLB zurückgelesen: gleiche Fläche
            File.WriteAllText(Path.Combine(belegDir, "T-0749-echtlauf.json"), JsonSerializer.Serialize(new
            {
                fotos = w.PhotoReport.Count, kameras = images, punkte3d = points, colmap_netz_punkte = tv, colmap_netz_dreiecke = tf,
                trendo_dreiecke = view.Model.Triangles, laufzeit_s = Math.Round(sw.Elapsed.TotalSeconds, 1), schritte = w.ScanSteps,
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { File.WriteAllText(Path.Combine(belegDir, "T-0749-echtlauf.log"), log.ToString(), new UTF8Encoding(false)); }
    }

    [AvaloniaFact(DisplayName = "T-0749 Echtlauf: „Stopp“ beendet COLMAP sofort, es bleibt kein Prozess und kein halbes Modell")]
    public void Stopp_beendet_colmap()
    {
        var (fotos, colmap, belegDir) = Need();
        var (w, _, _) = Ui.Open();
        var before = Process.GetProcessesByName("colmap").Select(p => p.Id).ToHashSet();
        w.ScanToolOverride = colmap;
        w.Show("scan3d");
        w.Find<TextBox>("scan-photos-folder").Text = fotos;
        w.Find<Button>("scan-run").Press();
        int[] Mine() => Process.GetProcessesByName("colmap").Select(p => p.Id).Where(id => !before.Contains(id)).ToArray();
        Pump(() => Mine().Length > 0, TimeSpan.FromSeconds(30));
        var running = Mine();
        Assert.NotEmpty(running);
        var sw = Stopwatch.StartNew();
        w.Find<Button>("scan-stop").Press();
        Pump(() => !w.ScanBusy, TimeSpan.FromSeconds(30));
        Pump(() => Mine().Length == 0, TimeSpan.FromSeconds(10));
        sw.Stop();
        Assert.False(w.ScanBusy);
        Assert.Contains("Abgebrochen", w.ScanStatusText);
        Assert.Null(w.ScanModelPath);
        Assert.Empty(Mine());
        File.WriteAllText(Path.Combine(belegDir, "T-0749-stopp.log"),
            $"# T-0749 Stopp – {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nCOLMAP-Prozesse beim Klick: {running.Length}\nNach Stopp übrig: {Mine().Length}\nZeit bis Ruhe: {sw.Elapsed.TotalSeconds:0.0} s\nStatus: {w.ScanStatusText}\n", new UTF8Encoding(false));
    }
}
