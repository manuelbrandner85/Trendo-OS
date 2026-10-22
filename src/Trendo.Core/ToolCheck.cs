using System.Text.Json;

namespace Trendo.Core;

public enum ToolState { Bundled, Working, Broken, Missing, NotNeeded }

public sealed record ToolCheck(Component Component, ToolState State, string? Path, string? Version, string Detail)
{
    public bool Ok => State is ToolState.Bundled or ToolState.Working or ToolState.NotNeeded;
}

/// <summary>Welche Werkzeuge welches Studio braucht, und wie die Funktion geprüft wird.</summary>
public static class StudioTools
{
    public static readonly IReadOnlyDictionary<string, string[]> Required = new Dictionary<string, string[]>
    {
        ["CORE-05"] = ["blender"], ["CORE-06"] = ["unreal"], ["SYS-10"] = ["libreoffice"], ["CORE-02"] = ["ollama", "model"],
        ["CORE-08"] = ["mpv", "ffmpeg"], ["CORE-07"] = ["git", "python"], ["STUDIO-LETTER-01"] = ["tesseract"],
    };

    /// <summary>Exe-Name (für PATH/App-Paths-Suche) und Prüfargumente. null = nicht starten, nur Dateiversion lesen (z. B. Unreal-Editor).</summary>
    public static readonly IReadOnlyDictionary<string, (string Exe, string? VerifyArgs)> Verify = new Dictionary<string, (string, string?)>
    {
        ["blender"] = ("blender.exe", "--version"), ["libreoffice"] = ("soffice.exe", null) /* --version öffnet unter Windows teils ein Fenster und hängt */, ["ollama"] = ("ollama.exe", "--version"),
        ["sumatra"] = ("SumatraPDF.exe", null), ["7zip"] = ("7z.exe", null), ["git"] = ("git.exe", "--version"), ["python"] = ("python.exe", "--version"), ["mpv"] = ("mpv.exe", "--version"), ["ffmpeg"] = ("ffmpeg.exe", "-version"),
        ["tesseract"] = ("tesseract.exe", "--version"), ["unreal"] = ("UnrealEditor.exe", null), ["krita"] = ("krita.exe", null),
        ["vscode"] = ("Code.exe", "--version"), ["obs"] = ("obs64.exe", null),
        ["inkscape"] = ("inkscape.exe", "--version"), ["kdenlive"] = ("kdenlive.exe", null), ["audacity"] = ("Audacity.exe", null),
    };
}


public static class DependencyChecks
{
    /// <summary>Erkennen (bekannte Pfade, gespeicherte Integration, PATH, Registry App Paths) und Funktion prüfen. Installiert nichts.</summary>
    public static async Task<ToolCheck> CheckAsync(this DependencyManager m, Component c, CancellationToken ct = default)
    {
        if (c.Mode == DeliveryMode.Bundled) return new(c, ToolState.Bundled, null, null, "im Setup enthalten");
        if (c.Mode == DeliveryMode.NotNeeded) return new(c, ToolState.NotNeeded, null, null, c.Reason);
        if (c.Id == "model-3b") return m.HasModel(DependencyCatalog.LargeModel) ? new(c, ToolState.Working, null, DependencyCatalog.LargeModel, "Modell vorhanden") : new(c, ToolState.Missing, null, null, "optional, nicht geladen");
        if (c.Id == "model") return m.ModelPresent ? new(c, ToolState.Working, null, DependencyCatalog.LocalModel, "Modell vorhanden") : new(c, ToolState.Missing, null, null, "Modell nicht geladen");
        var path = m.Integrated(c.Id) is { } saved && m.Exists(saved) ? saved : m.ResolvePath(c) ?? m.FindOnSystem(c);
        if (path is null) return new(c, ToolState.Missing, null, null, "nicht gefunden");
        StudioTools.Verify.TryGetValue(c.Id, out var v);
        if (v.VerifyArgs is null)
        {
            m.Integrate(c.Id, path);
            return new(c, ToolState.Working, path, m.FileVersion(path), "gefunden (nicht gestartet, um nichts zu verändern)");
        }
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(TimeSpan.FromSeconds(25));
            var (exit, output) = await m.Runner.RunAsync(path, v.VerifyArgs, null, cts.Token);
            var first = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
            if (exit != 0)
                return path.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase)
                    ? new(c, ToolState.Missing, null, null, "nur Microsoft-Store-Platzhalter gefunden")
                    : new(c, ToolState.Broken, path, null, $"Start fehlgeschlagen (Exit {exit})");
            m.Integrate(c.Id, path);
            return new(c, ToolState.Working, path, first.Length > 80 ? first[..80] : first, "funktioniert");
        }
        catch (Exception ex) { return new(c, ToolState.Broken, path, null, $"Start fehlgeschlagen: {ex.GetType().Name}"); }
    }
}
