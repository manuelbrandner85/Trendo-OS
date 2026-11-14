using Avalonia.Controls;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 7 – CORE-09: „Objekte erkennen“ im Kamera-Bereich – YOLOX-S (Apache-2.0) mit onnxruntime in Trendos KI-Laufzeit,
/// auf dem Prozessor, ohne Cloud. Erkennt im aktuellen Bild Personen, Fahrzeuge, Tiere und Gegenstände, zählt sie und zeigt die Rahmen im Bild.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Lauf nachbilden (Argumente nach dem Repo-Platzhalter) – schreibt das markierte Bild selbst.</summary>
    public static Func<IReadOnlyList<string>, Cli.Result>? ObjectsRunOverride { get; set; }
    static readonly KiLibraryModel ObjectsModel = new("objekte-yolox", KiKind.BildVerstehen, KiRuntime.Objekte, "Objekte erkennen (YOLOX)", "", 0.3, 4, "Apache-2.0",
        "Personen, Fahrzeuge, Tiere und Gegenstände im Kamerabild erkennen und zählen", "Im Studio „Kameras & Roboter“ unter „Objekte erkennen“.", []);
    string ObjectsModelPath => Path.Combine(KiStore.PyPaths(KiRuntime.Objekte).Dir, ObjectDetect.ModelFile);
    bool ObjectsReady => ObjectsRunOverride is not null || KiStore.RuntimeReady(KiRuntime.Objekte) && File.Exists(ObjectsModelPath);
    public ObjectDetect.Result? ObjectsLast { get; private set; }
    public double ObjectsThreshold { get; set; } = 0.35;

    /// <summary>Objekte im aktuellen Kamerabild erkennen (öffentlich für Tests). Das Bild mit Rahmen ersetzt die Vorschau, die Zählung steht im Status.</summary>
    public async Task CamDetectObjects()
    {
        if (Bench.Frame is not { Length: > 0 } frame) { Bench.CamStatus = "Zuerst ein Bild holen (Livebild oder Foto)."; Show("roboter"); return; }
        var dir = Path.Combine(Path.GetTempPath(), "trendo-objekte-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var src = Path.Combine(dir, frame.Length > 2 && frame[0] == 0xFF && frame[1] == 0xD8 ? "bild.jpg" : "bild.png"); var marked = Path.Combine(dir, "erkannt.png");
        try
        {
            await File.WriteAllBytesAsync(src, frame);
            Bench.CamStatus = "Objekte werden erkannt …"; Show("roboter");
            var args = ObjectDetect.Args(ObjectsModelPath, src, marked, ObjectsThreshold);
            var r = ObjectsRunOverride is { } o ? o(args) : await KiPyRun(KiRuntime.Objekte, args, 300_000);
            if (!r.Ok) { Bench.CamStatus = "⚠ Objekterkennung: " + Short(r.Err.Length > 0 ? r.Err : r.Out); Show("roboter"); return; }
            var res = ObjectDetect.Parse(r.Out.Trim().Split('\n')[^1]);
            ObjectsLast = res;
            if (File.Exists(marked)) Bench.Frame = await File.ReadAllBytesAsync(marked);
            Bench.CamStatus = $"Erkannt: {res.Summary} ({res.Ms} ms, YOLOX-S, lokal).";
            _core.Audit.Append(Principal.LocalOwner.Name, "camera.objects", res.Summary);
        }
        catch (Exception e) when (e is IOException or FormatException or System.Text.Json.JsonException or InvalidOperationException) { Bench.CamStatus = "⚠ Ergebnis nicht lesbar: " + Trendo.Core.FriendlyError.Say(e); }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        Show("roboter");
    }

    async Task CamObjectsSetup()
    {
        Bench.CamStatus = "Objekterkennung wird eingerichtet (einmalig, ca. 120 MB) …"; Show("roboter");
        try
        {
            if (!KiStore.RuntimeReady(KiRuntime.Objekte)) await KiPyInstall(ObjectsModel, KiPyApps.For(KiRuntime.Objekte)!);
            var (ok, msg) = File.Exists(ObjectsModelPath) ? (true, "Modell ist schon da.")
                : await ObjectDetect.DownloadModelAsync(new HttpClient { Timeout = TimeSpan.FromMinutes(5) }.GetStreamAsync, Path.GetDirectoryName(ObjectsModelPath)!);
            _core.Audit.Append(Principal.LocalOwner.Name, "deps.model", "yolox_s.onnx " + (ok ? "ok" : "verworfen"));
            Bench.CamStatus = ok ? "✓ Objekterkennung ist eingerichtet." : "⚠ " + msg;
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or HttpRequestException or OperationCanceledException) { Bench.CamStatus = "Einrichten ging nicht: " + Trendo.Core.FriendlyError.Say(e); }
        Show("roboter");
    }

    Control CamObjectsButton() => ObjectsReady
        ? Act("cam-objects", "Objekte erkennen", () => _ = CamDetectObjects())
        : Act("cam-objects-setup", "Objekterkennung einrichten (YOLOX, kostenlos)", () => _ = CamObjectsSetup());
}
