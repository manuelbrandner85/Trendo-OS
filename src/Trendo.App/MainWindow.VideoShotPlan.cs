using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Paket 01 – Studio 3, S3-01/S3-03 (07.11.2026): Drehbuch → Shotliste (CSV) mit Codes und Prüfung; Storyboard-Tafeln je Einstellung in die Zeitleiste.
/// Was nicht erzeugt werden kann, steht im Fehlprotokoll – der Rest wird trotzdem übernommen.</summary>
public partial class MainWindow
{
    public ShotPlan.Plan? EditShotPlanLast { get; private set; }
    public List<string> EditStoryboardLog { get; } = [];
    string _veScript = "";

    public ShotPlan.Plan EditShotList(ProgramDocument d, string script)
    {
        var p = ShotPlan.Parse(script); EditShotPlanLast = p;
        var dir = Path.Combine(WorkFolder(Programs[VK].Folder), "Drehplan"); Directory.CreateDirectory(dir);
        var csv = Path.Combine(dir, DevStudio.SafeName(d.Title.Length > 0 ? d.Title : "Film") + " – Shotliste.csv");
        File.WriteAllText(csv, ShotPlan.Csv(p), new System.Text.UTF8Encoding(true));
        d.Edit("Drehbuch", x => x["script"] = script);
        _progMsg[VK] = $"Shotliste: {p.Scenes.Count} Szenen, {p.AllShots.Count()} Einstellungen, {p.Seconds:0} s – {Path.GetFileName(csv)}" + (p.Problems.Count > 0 ? $". Bitte prüfen: {string.Join(" ", p.Problems.Take(3))}" : ".");
        Show(VK);
        return p;
    }

    /// <summary>S3-03: je Einstellung eine Tafel (ID, Einstellung, Ort, Beschreibung) in Plan-Länge ans Ende der Zeitleiste – Titel = Shot-ID.</summary>
    public async Task<int> EditStoryboardToTimeline(ProgramDocument d)
    {
        var p = EditShotPlanLast ?? ShotPlan.Parse(d.Text("script"));
        if (!p.AllShots.Any()) { _progMsg[VK] = "Erst eine Shotliste erstellen."; Show(VK); return 0; }
        var font = VideoEdit.FontFile(); var dir = Path.Combine(WorkFolder(Programs[VK].Folder), "Drehplan", "Tafeln"); Directory.CreateDirectory(dir);
        if (FfmpegRunner(dir, out var why) is not { } run) { _progMsg[VK] = why!; Show(VK); return 0; }
        EditStoryboardLog.Clear(); var made = new List<(string File, ShotPlan.Shot Shot)>();
        _progMsg[VK] = "Storyboard-Tafeln entstehen …"; Show(VK);
        foreach (var s in p.Scenes)
            foreach (var x in s.Shots)
            {
                var f = Path.Combine(dir, x.Id + ".mp4");
                if (font is null) { EditStoryboardLog.Add($"{x.Id}: keine Schrift gefunden – Tafel nicht erzeugt."); continue; }
                var r = await Task.Run(() => run(ShotPlan.SlateArgs(s, x, font, f, (int)Math.Max(320, d.Number("width", 1280)), (int)Math.Max(180, d.Number("height", 720)))));
                if (r.Ok && File.Exists(f)) made.Add((f, x));
                else EditStoryboardLog.Add($"{x.Id}: Tafel nicht erzeugt – {(r.Err.Trim().Split('\n').LastOrDefault() ?? "unbekannter Fehler")}");
            }
        if (made.Count > 0)
            d.Edit("Storyboard", x => { foreach (var (f, s) in made) { VideoEdit.Add(x, VideoEdit.NewClip(f, s.Seconds, false)); var l = VideoEdit.Clips(x); VideoEdit.SetClip(x, l.Count - 1, 0, s.Id); } });
        var logFile = Path.Combine(dir, "Fehlprotokoll.txt");
        if (EditStoryboardLog.Count > 0) File.WriteAllLines(logFile, EditStoryboardLog);
        _progMsg[VK] = $"{made.Count} Storyboard-Tafeln in der Zeitleiste" + (EditStoryboardLog.Count > 0 ? $" – {EditStoryboardLog.Count} nicht erzeugt, siehe {Path.GetFileName(logFile)}: {EditStoryboardLog[0]}" : ".");
        Show(VK);
        return made.Count;
    }

    Control EditShotPlanPanel(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "ve-shotplan", Spacing = 5, Margin = new Thickness(0, 6, 0, 0) };
        sp.Children.Add(H("Drehbuch → Shotliste", 13));
        if (_veScript.Length == 0) _veScript = d.Text("script", "1 INNEN. KÜCHE – MORGEN\n- Totale: Erika betritt die Küche (4 s)\n- Nah: Kaffee läuft in die Tasse (3 s)\n\n2 AUSSEN. STRASSE – TAG\n- Halbtotale: Erika geht los (5 s)");
        var box = new TextBox { Name = "ve-script", Text = _veScript, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 110, FontSize = 12, FontFamily = new FontFamily("Consolas, monospace") };
        box.TextChanged += (_, _) => _veScript = box.Text ?? "";
        sp.Children.Add(box);
        var list = Act("ve-shotlist", "Shotliste erstellen", () => EditShotList(d, box.Text ?? ""));
        var board = Act("ve-storyboard", "Storyboard in die Zeitleiste", () => _ = EditStoryboardToTimeline(d));
        sp.Children.Add(new WrapPanel { Children = { list, new Border { Width = 4 }, board } });
        if (EditShotPlanLast is { } p)
        {
            foreach (var s in p.Scenes.Take(12))
                sp.Children.Add(P($"{s.Code} {s.Place}. {s.Location}{(s.Time.Length > 0 ? " – " + s.Time : "")}: " + string.Join(", ", s.Shots.Select(x => $"{x.Id[^3..]} {x.Type} {x.Seconds:0.#} s")), Text, 11));
            foreach (var pr in p.Problems.Take(5)) sp.Children.Add(P("⚠ " + pr, Brush.Parse("#FACC15"), 11));
        }
        sp.Children.Add(P("Szenenkopf „1 INNEN. KÜCHE – TAG“, Einstellungen „- Totale: … (4 s)“.", Muted, 10.5));
        return sp;
    }
}
