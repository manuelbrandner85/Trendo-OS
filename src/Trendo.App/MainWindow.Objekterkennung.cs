using Trendo.Core;

namespace Trendo.App;

/// <summary>Objekterkennung YOLOX-S (Apache-2.0) mit onnxruntime in Trendos KI-Laufzeit, auf dem Prozessor, ohne Cloud – genutzt von „Bilder erkennen“.
/// T-0720: aus dem entfernten Studio „Kameras &amp; Roboter“ (CORE-09) unverändert hierher verschoben; die Kamera-Seite des Studios ist gelöscht.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Lauf nachbilden (Argumente nach dem Repo-Platzhalter) – schreibt das markierte Bild selbst.</summary>
    public static Func<IReadOnlyList<string>, Cli.Result>? ObjectsRunOverride { get; set; }
    static readonly KiLibraryModel ObjectsModel = new("objekte-yolox", KiKind.BildVerstehen, KiRuntime.Objekte, "Objekte erkennen (YOLOX)", "", 0.3, 4, "Apache-2.0",
        "Personen, Fahrzeuge, Tiere und Gegenstände im Bild erkennen und zählen", "Im Programm „Bilder erkennen“.", []);
    string ObjectsModelPath => Path.Combine(KiStore.PyPaths(KiRuntime.Objekte).Dir, ObjectDetect.ModelFile);
    bool ObjectsReady => ObjectsRunOverride is not null || KiStore.RuntimeReady(KiRuntime.Objekte) && File.Exists(ObjectsModelPath);
    public double ObjectsThreshold { get; set; } = 0.35;
}
