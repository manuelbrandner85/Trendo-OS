namespace Trendo.Core;

/// <summary>Alle Studios, Runde 1 (CORE-05 „3D-Modelle erstellen“): Vorschaubild jeder 3D-Datei mit f3d (BSD-3-Clause, f3d-app/f3d 4.0.1).
/// f3d läuft als eigener Prozess (Argumente als Liste, keine Shell), nur nach Einrichtung über den Abhängigkeitskatalog („f3d“, offizielle Zip mit fester Prüfsumme).
/// Ergebnis: PNG im Projekt – für Galerie, Asset-Übersicht und Web-Export.</summary>
public static class Model3DPreview
{
    /// <summary>Formate, die f3d 4 liest (Auswahl der gängigen; weitere über Plugins).</summary>
    public static readonly string[] Formats = [".glb", ".gltf", ".obj", ".stl", ".ply", ".fbx", ".3ds", ".dae", ".off", ".3mf", ".usd", ".usda", ".usdc", ".usdz", ".vtk", ".vtp", ".abc", ".step", ".stp", ".iges", ".igs"];
    public const int MinSize = 64, MaxSize = 4096;

    public static bool Supports(string file) => Formats.Contains(Path.GetExtension(file).ToLowerInvariant());

    /// <summary>Prüft Eingabe und Ziel; null = in Ordnung, sonst ein verständlicher Grund.</summary>
    public static string? Check(string input, string output, int size)
    {
        if (string.IsNullOrWhiteSpace(input) || !File.Exists(input)) return "Die 3D-Datei gibt es nicht.";
        if (!Supports(input)) return $"Format {Path.GetExtension(input)} wird nicht unterstützt.";
        if (new FileInfo(input).Length == 0) return "Die 3D-Datei ist leer.";
        if (!output.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return "Das Vorschaubild muss eine PNG-Datei sein.";
        if (size is < MinSize or > MaxSize) return $"Größe {size} liegt außerhalb von {MinSize}–{MaxSize} Pixeln.";
        return null;
    }

    /// <summary>f3d-Argumente (geprüft gegen doc/user/03-OPTIONS.md von v4.0.1): Bild statt Fenster, quadratisch, Kantenglättung (FXAA), Tone-Mapping und
    /// Umgebungsverdeckung für glaubwürdige Kontaktschatten; Dateiname, Gitter und Achsen sind in f3d standardmäßig aus. Dateinamen bleiben eigene Argumente (keine Befehlseinschleusung).</summary>
    public static IReadOnlyList<string> Args(string input, string output, int size) =>
        ["--no-config", "--output=" + output, $"--resolution={size},{size}", "--anti-aliasing", "--tone-mapping", "--ambient-occlusion", "--hdri-ambient", input];
    // --hdri-ambient: ohne Grundeinstellung fehlt das Umgebungslicht – das Modell wirkt dunkel und flach; mit HDRI glaubwürdige Farben und Materialglanz (im echten Lauf verglichen)
    // --no-config: echter Lauf (07.11.2026) zeigte, dass die mitgelieferte f3d-Grundeinstellung Dateiname, Achsenkreuz, Gitter, Spiegelboden und Bildschirm-Skalierung (2304 statt 768 px) einschaltet

    /// <summary>Erzeugt das Vorschaubild. <paramref name="run"/> ist für Tests austauschbar (Standard: <see cref="Cli.Run"/> mit Zeitlimit).</summary>
    public static (bool Ok, string Message) Render(string f3dExe, string input, string output, int size = 512, int timeoutMs = 60000,
        Func<string, IReadOnlyList<string>, int, Cli.Result>? run = null)
    {
        if (Check(input, output, size) is { } why) return (false, why);
        if (string.IsNullOrWhiteSpace(f3dExe) || !File.Exists(f3dExe)) return (false, "f3d ist noch nicht eingerichtet – im Studio „Komponenten“ mit einem Klick (kostenlos, BSD-3).");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        var r = (run ?? ((e, a, t) => Cli.Run(e, a, timeoutMs: t)))(f3dExe, Args(input, output, size), timeoutMs);
        if (!r.Ok) return (false, "f3d meldet einen Fehler: " + (r.Err.Length > 0 ? r.Err : r.Out).Trim().Split('\n')[0]);
        return File.Exists(output) && new FileInfo(output).Length > 0 ? (true, output) : (false, "f3d hat kein Bild geschrieben.");
    }
}
