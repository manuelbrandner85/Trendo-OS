using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 7 – CORE-09: YOLOX-Objekterkennung. Antworten in echter Form; Echtlauf (Modell + onnxruntime) nur nach Freigabe.</summary>
public class ObjectDetectTests
{
    const string Answer = """{"breite": 1280, "hoehe": 720, "ms": 212, "objekte": [{"klasse": "person", "sicherheit": 0.91, "rahmen": [100, 50, 300, 700]}, {"klasse": "car", "sicherheit": 0.82, "rahmen": [600, 300, 1100, 650]}, {"klasse": "person", "sicherheit": 0.66, "rahmen": [400, 80, 520, 690]}, {"klasse": "neu", "sicherheit": 0.5, "rahmen": [1, 2, 3, 4]}], "anzahl": {"person": 2, "car": 1, "neu": 1}}""";

    [Fact(DisplayName = "Objekte +1: Antwort lesen – Größe, Dauer, Rahmen, deutsche Namen; unbekannte Klasse bleibt englisch")]
    public void Parse()
    {
        var r = ObjectDetect.Parse(Answer);
        Assert.Equal((1280, 720, 212, 4), (r.Width, r.Height, r.Ms, r.Hits.Count));
        Assert.Equal(new ObjectDetect.Hit("person", "Person", 0.91, 100, 50, 300, 700), r.Hits[0]);
        Assert.Equal("Auto", r.Hits[1].Label); Assert.Equal("neu", r.Hits[3].Label);
    }

    [Fact(DisplayName = "Objekte +2: Zählung je Klasse mit Einzahl/Mehrzahl – „2 Personen, 1 Auto, 1 neu“; leer → „Nichts erkannt.“")]
    public void Summary()
    {
        Assert.Equal("2 Personen, 1 Auto, 1 neu", ObjectDetect.Parse(Answer).Summary);
        Assert.Equal("Nichts erkannt.", ObjectDetect.Parse("""{"breite":1,"hoehe":1,"ms":1,"objekte":[]}""").Summary);
        Assert.Equal(("Kuh", "Kühe"), ObjectDetect.German["cow"]); Assert.Equal(80, ObjectDetect.German.Count);
    }

    [Fact(DisplayName = "Objekte +3: Argumente – Modell, Bild, Ausgabe oder „-“, Schwelle auf 0,10–0,90 begrenzt und mit Punkt")]
    public void Args()
    {
        Assert.Equal(["m.onnx", "b.jpg", "-", "0.35"], ObjectDetect.Args("m.onnx", "b.jpg", null, 0.35));
        Assert.Equal("0.90", ObjectDetect.Args("m", "b", "o.png", 5)[3]); Assert.Equal("0.10", ObjectDetect.Args("m", "b", "o.png", 0)[3]);
    }

    [Fact(DisplayName = "Objekte +4: Modell wird nur mit passender Prüfsumme übernommen; falsche Datei verworfen, nichts bleibt liegen")]
    public async Task Download()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-yolox-" + Guid.NewGuid().ToString("N"));
        try
        {
            var data = new byte[] { 1, 2, 3 }; var sha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(data));
            var (ok, _) = await ObjectDetect.DownloadModelAsync(_ => Task.FromResult<Stream>(new MemoryStream(data)), dir, "x", sha);
            Assert.True(ok); Assert.Equal(data, File.ReadAllBytes(Path.Combine(dir, ObjectDetect.ModelFile)));
            File.Delete(Path.Combine(dir, ObjectDetect.ModelFile));
            var (bad, msg) = await ObjectDetect.DownloadModelAsync(_ => Task.FromResult<Stream>(new MemoryStream([9, 9])), dir);
            Assert.False(bad); Assert.Contains("Prüfsumme", msg); Assert.Empty(Directory.GetFiles(dir));
            Assert.StartsWith("https://github.com/Megvii-BaseDetection/YOLOX/releases/download/", ObjectDetect.ModelUrl); Assert.Equal(64, ObjectDetect.ModelSha256.Length);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "Objekte +5: KI-Laufzeit „Objekte“ mit festen Fassungen (onnxruntime, numpy, Pillow), Startskript liegt bei; rechnet auf dem Prozessor")]
    public void Runtime()
    {
        var app = KiPyApps.For(KiRuntime.Objekte)!;
        Assert.Equal("trendo_objekte.py", app.Script); Assert.True(app.PackagesOnly); Assert.Empty(app.Torch);
        Assert.All(app.Packages, p => Assert.Contains("==", p));
        var root = new DirectoryInfo(AppContext.BaseDirectory); while (root is not null && !File.Exists(Path.Combine(root.FullName, "Trendo.sln"))) root = root.Parent;
        var script = File.ReadAllText(Path.Combine(root!.FullName, "src", "Trendo.App", "Assets", "ki", "trendo_objekte.py"));
        Assert.Contains("CPUExecutionProvider", script); Assert.DoesNotContain("requests", script); Assert.DoesNotContain("urllib", script);
        Assert.Equal("Apache-2.0", KiLibrary.RuntimeInfo(KiRuntime.Objekte).License);
    }

    [Fact(DisplayName = "Objekte −1: kaputte Antwort (Rahmen unvollständig, kein JSON) wird abgelehnt")]
    public void Broken()
    {
        Assert.Throws<FormatException>(() => ObjectDetect.Parse("""{"objekte":[{"klasse":"car","sicherheit":0.5,"rahmen":[1,2]}]}"""));
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => ObjectDetect.Parse("kein json"));
    }
}

/// <summary>Echter Lauf: YOLOX-S mit onnxruntime auf dem YOLOX-Demobild (Prüfumgebung %TEMP%\trendo-tools-b\yolox, Freigabe 08.11.2026).</summary>
public class ObjectDetectRealTests
{
    static string Dir => Path.Combine(Path.GetTempPath(), "trendo-tools-b", "yolox");
    static string Py => Path.Combine(Dir, "venv", OperatingSystem.IsWindows() ? Path.Combine("Scripts", "python.exe") : Path.Combine("bin", "python"));
    static string Script()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory); while (root is not null && !File.Exists(Path.Combine(root.FullName, "Trendo.sln"))) root = root.Parent;
        return Path.Combine(root!.FullName, "src", "Trendo.App", "Assets", "ki", "trendo_objekte.py");
    }
    public sealed class YoloxFactAttribute : FactAttribute { public YoloxFactAttribute() { if (!File.Exists(Py) || !File.Exists(Path.Combine(Dir, ObjectDetect.ModelFile)) || !File.Exists(Path.Combine(Dir, "dog.jpg"))) Skip = "Übersprungen – YOLOX-Prüfumgebung fehlt."; } }

    [YoloxFact(DisplayName = "Objekte echt: Modell mit gepinnter Prüfsumme; Demobild → Fahrrad und Hund sicher erkannt, Fahrzeug nur einmal gezählt, markiertes Bild geschrieben")]
    public void Real_detect()
    {
        Assert.Equal(ObjectDetect.ModelSha256, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(Dir, ObjectDetect.ModelFile)))));
        var outPng = Path.Combine(Path.GetTempPath(), "trendo-yolox-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            var r = Cli.Run(Py, ["-I", Script(), "-", .. ObjectDetect.Args(Path.Combine(Dir, ObjectDetect.ModelFile), Path.Combine(Dir, "dog.jpg"), outPng, 0.35)], timeoutMs: 120_000);
            Assert.True(r.Ok, r.Err);
            var res = ObjectDetect.Parse(r.Out.Trim().Split('\n')[^1]);
            Assert.Equal((768, 576), (res.Width, res.Height));
            Assert.Contains(res.Hits, h => h.Class == "bicycle" && h.Score > 0.9); Assert.Contains(res.Hits, h => h.Class == "dog" && h.Score > 0.85);
            Assert.Single(res.Hits, h => h.Class is "car" or "truck"); // klassenübergreifend: ein Fahrzeug zählt einmal
            Assert.StartsWith("1 Fahrrad, 1 Hund", res.Summary);
            Assert.True(new FileInfo(outPng).Length > 10_000);
        }
        finally { File.Delete(outPng); }
    }

    [Fact(DisplayName = "Objekte: deutsche Namen im Bild (Python) und in der Zählung (C#) sind gleich – alle 80 Klassen")]
    public void Same_german_names()
    {
        var py = File.ReadAllText(Script());
        foreach (var (en, (one, _)) in ObjectDetect.German) Assert.Contains($"\"{en}\": \"{one}\"", py);
    }
}
