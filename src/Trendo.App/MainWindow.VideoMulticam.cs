using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Paket 01 – Studio 3, S3-02 (07.11.2026): Multicam im Videoschnitt – Kameras über den Ton synchronisieren, Wechsel „Sekunde:Kamera“ eingeben,
/// Schnitt in die Zeitleiste (Bild je Kamera, Ton durchgehend von Kamera 1 auf der Musikspur, rückgängig machbar).</summary>
public partial class MainWindow
{
    public List<(string File, double Offset, double Length)>? EditMulticamAngles { get; private set; }
    string _veMcFiles = "", _veMcSwitches = "0:1";

    public async Task<bool> EditMulticamSync(string filesText)
    {
        var files = filesText.Replace("\r\n", "\n").Split('\n').Select(f => f.Trim().Trim('"')).Where(f => f.Length > 0).ToList();
        if (files.Count < 2) { _progMsg[VK] = "Mindestens zwei Kameradateien angeben (eine je Zeile)."; Show(VK); return false; }
        if (files.FirstOrDefault(f => !File.Exists(f)) is { } miss) { _progMsg[VK] = "Datei fehlt: " + Path.GetFileName(miss); Show(VK); return false; }
        var dir = Path.Combine(MediaFolder, ".multicam");
        if (FfmpegRunner(dir, out var why) is not { } run) { _progMsg[VK] = why!; Show(VK); return false; }
        _progMsg[VK] = "Kameras werden über den Ton synchronisiert …"; Show(VK);
        try
        {
            var (res, lens) = await Task.Run(() => (Multicam.Sync(run, files, dir), files.Select(f => VideoProxy.ParseDuration(run(["-hide_banner", "-i", f]).Err) ?? 0).ToList()));
            EditMulticamAngles = [.. files.Select((f, i) => (f, res[i].Offset, lens[i]))];
            _progMsg[VK] = "Synchron: " + string.Join(", ", files.Select((f, i) => $"Kamera {i + 1} {(i == 0 ? "Bezug" : $"{res[i].Offset:+0.000;-0.000} s (Übereinstimmung {res[i].Confidence:0.00})")}")) + ".";
            Show(VK); return true;
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or InvalidDataException) { EditMulticamAngles = null; _progMsg[VK] = e.Message; Show(VK); return false; }
    }

    public void EditMulticamCut(ProgramDocument d, string switchesText)
    {
        if (EditMulticamAngles is not { } angles) throw new InvalidOperationException("Erst „Synchronisieren“.");
        var sw = Multicam.ParseSwitches(switchesText);
        var end = angles.Min(a => a.Offset + a.Length); // nur solange alle Kameras laufen
        end = Math.Min(end, angles[0].Length);
        var clips = Multicam.Cut(angles, sw, end);
        d.Edit("Multicam-Schnitt", x =>
        {
            foreach (var c in clips) VideoEdit.Add(x, c);
            VideoEdit.Add(x, VideoEdit.NewClip(angles[0].File, end, true), "music"); // Ton von Kamera 1 durchgehend
        });
        _progMsg[VK] = $"Multicam-Schnitt: {clips.Count} Abschnitte, {end:0.0} s, Ton durchgehend von Kamera 1.";
    }

    Control EditMulticamPanel(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "ve-multicam", Spacing = 5, Margin = new Thickness(0, 6, 0, 0) };
        sp.Children.Add(H("Multicam", 13));
        var files = new TextBox { Name = "ve-mc-files", Text = _veMcFiles, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, MinHeight = 50, FontSize = 12, Watermark = "Kameradateien, eine je Zeile (Kamera 1 zuerst)" };
        files.TextChanged += (_, _) => _veMcFiles = files.Text ?? "";
        sp.Children.Add(files);
        sp.Children.Add(Act("ve-mc-sync", "Über den Ton synchronisieren", () => _ = EditMulticamSync(files.Text ?? "")));
        if (EditMulticamAngles is { } a)
        {
            sp.Children.Add(P(string.Join("  ·  ", a.Select((x, i) => $"K{i + 1}: {Path.GetFileName(x.File)} {(i == 0 ? "" : $"{x.Offset:+0.000;-0.000} s")}")), Text, 11));
            var sw = new TextBox { Name = "ve-mc-switches", Text = _veMcSwitches, FontSize = 12, Watermark = "Wechsel Sekunde:Kamera, z. B. 0:1, 4:2, 9:1" };
            sw.TextChanged += (_, _) => _veMcSwitches = sw.Text ?? "";
            sp.Children.Add(sw);
            sp.Children.Add(Act("ve-mc-cut", "Multicam-Schnitt in die Zeitleiste", () => ProgramRun(VK, () => EditMulticamCut(d, sw.Text ?? ""))));
        }
        return sp;
    }
}
