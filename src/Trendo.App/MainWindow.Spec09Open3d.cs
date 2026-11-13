using Avalonia.Controls;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Alle Studios, Runde 2 – SPEC-09 „Gegenstände einscannen“: Scan-Netz glätten (Taubin, ohne Schrumpfen) und vereinfachen (Quadric) mit Open3D (MIT).
/// Läuft als eigener Python-Prozess in eigener Umgebung; Einrichtung nach Klick über die vorhandene uv-Kette. „Rückgängig“ wie bei den anderen Werkzeugen.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Open3D nachbilden (Modus, Eingabe-OBJ, Ausgabe-OBJ, Zahl) – schreibt die Ausgabe selbst.</summary>
    public static Func<string, string, string, int, Cli.Result>? Open3dRunOverride { get; set; }
    static readonly KiLibraryModel Open3dModel = new("scan-open3d", KiKind.Modell3D, KiRuntime.ScanNetz, "Scan-Netze (Open3D)", "", 0.4, 4, "MIT",
        "gescannte Modelle glätten und für Web/Spiele vereinfachen", "Im Scan-Studio unter „Glätten und vereinfachen“.", []);
    bool Open3dReady => Open3dRunOverride is not null || KiStore.RuntimeReady(KiRuntime.ScanNetz);
    public static readonly int[] ScanTargets = [5_000, 20_000, 100_000];

    /// <summary>Glätten („glaetten“, Durchgänge) oder Vereinfachen („vereinfachen“, Ziel-Dreiecke) – öffentlich für Tests.</summary>
    public async Task ScanOpen3d(string mode, int n)
    {
        if (_scanModel is null || !File.Exists(_scanModel)) { _scanStatus = "Noch kein Modell."; Show("scan3d"); return; }
        if (mode is not ("glaetten" or "vereinfachen") || mode == "glaetten" && n is < 1 or > 50 || mode == "vereinfachen" && n is < 100 or > 5_000_000) { _scanStatus = "Ungültige Einstellung."; Show("scan3d"); return; }
        var dir = Path.GetDirectoryName(_scanModel)!; var tmp = Path.Combine(dir, "open3d-ergebnis.obj");
        _scanStatus = mode == "glaetten" ? "Wird geglättet …" : "Wird vereinfacht …"; Show("scan3d");
        Cli.Result r;
        if (Open3dRunOverride is { } o) r = o(mode, _scanModel, tmp, n);
        else r = await KiPyRun(KiRuntime.ScanNetz, [mode, _scanModel, tmp, n.ToString(System.Globalization.CultureInfo.InvariantCulture)], 600_000);
        if (!r.Ok || !File.Exists(tmp)) { _scanStatus = "Open3D: " + Short(r.Err.Length > 0 ? r.Err : r.Out); TryDelete(tmp); Show("scan3d"); return; }
        try { var result = Mesh3D.ReadMeshFile(tmp); ScanEdit(mode == "glaetten" ? $"Geglättet ({n}×, Open3D)" : $"Vereinfacht (Open3D)", _ => result); }
        catch (Exception e) when (e is IOException or InvalidDataException or FormatException) { _scanStatus = "Ergebnis nicht lesbar: " + e.Message; Show("scan3d"); }
        finally { TryDelete(tmp); }
    }

    async Task ScanOpen3dSetup()
    {
        _scanStatus = "Open3D wird eingerichtet (einmalig, ca. 400 MB) …"; Show("scan3d");
        try { await KiPyInstall(Open3dModel, KiPyApps.For(KiRuntime.ScanNetz)!); _scanStatus = "Open3D ist eingerichtet."; }
        catch (Exception e) when (e is InvalidOperationException or IOException or HttpRequestException or OperationCanceledException) { _scanStatus = "Einrichten ging nicht: " + e.Message; }
        Show("scan3d");
    }

    Control ScanOpen3dCard(Mesh mesh)
    {
        var sp = new StackPanel { Name = "scan-o3d", Spacing = 6 };
        sp.Children.Add(H("Glätten und vereinfachen (Open3D)", 12));
        if (!Open3dReady)
        {
            sp.Children.Add(P("Glättet Scan-Rauschen, ohne dass das Modell schrumpft, und macht es leichter für Web und Spiele. Kostenlos, MIT, einmalig ca. 400 MB.", Muted, 11));
            sp.Children.Add(Act("scan-o3d-setup", "Open3D einrichten", () => _ = ScanOpen3dSetup()));
            return sp;
        }
        var passes = new ComboBox { Name = "scan-o3d-passes", ItemsSource = new[] { "leicht (5)", "mittel (10)", "stark (25)" }, SelectedIndex = 1, Width = 120 };
        var targets = ScanTargets.Where(t => t < mesh.Triangles).ToList();
        var target = new ComboBox { Name = "scan-o3d-target", ItemsSource = targets.Select(t => $"{t:N0} Dreiecke").ToList(), SelectedIndex = targets.Count > 0 ? targets.Count - 1 : -1, Width = 150, IsEnabled = targets.Count > 0 };
        sp.Children.Add(new WrapPanel { Children = { passes, new Border { Width = 4 }, Act("scan-o3d-smooth", "Glätten", () => _ = ScanOpen3d("glaetten", passes.SelectedIndex switch { 0 => 5, 2 => 25, _ => 10 })) } });
        var simplify = Act("scan-o3d-simplify", "Vereinfachen", () => { if (target.SelectedIndex >= 0) _ = ScanOpen3d("vereinfachen", targets[target.SelectedIndex]); });
        simplify.IsEnabled = targets.Count > 0;
        sp.Children.Add(new WrapPanel { Children = { target, new Border { Width = 4 }, simplify } });
        if (targets.Count == 0) sp.Children.Add(P($"Schon schlank ({mesh.Triangles:N0} Dreiecke).", Muted, 11));
        return sp;
    }
}
