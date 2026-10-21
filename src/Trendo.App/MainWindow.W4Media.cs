using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>CORE-08: Kreativ- & Medien-Studio.</summary>
public partial class MainWindow
{
    private string? _mediaFile;
    public string MediaFolder => WorkFolder("Medien");
    public string? FfmpegPath => ToolPath("ffmpeg") is { } p && File.Exists(p) ? p : Cli.Find("ffmpeg");

    private void InitW4Media() => RegisterTab(KreativTabs, "kreativ-medien", "Medien bearbeiten", "CORE-08", MediaStudioView, "video", "umwandeln", "mp3", "mp4", "gif", "ffmpeg", "schneiden");

    public void MediaPick(string path) { _mediaFile = path; Show("kreativ-medien"); }

    public (bool Ok, string Output) MediaRun(string jobId, double start = 0, double length = 10)
    {
        if (FfmpegPath is not { } ff) { Say("Dafür fehlt FFmpeg. Unter Zusatzprogramme kannst du es installieren.", "kreativ-medien"); return (false, ""); }
        if (_mediaFile is null) return (false, "");
        var (r, o) = MediaStudio.Run(ff, _mediaFile, jobId, start, length);
        _core.Audit.Append(Principal.LocalOwner.Name, "media.job", $"{jobId} exit={r.Exit}");
        Say(r.Ok ? $"Fertig: {Path.GetFileName(o)}" : "Hat nicht geklappt: " + Short(r.Err), "kreativ-medien");
        return (r.Ok, o);
    }

    private Control MediaStudioView()
    {
        var sp = new StackPanel { Spacing = 8, Children = { P("Wähle eine Datei und was du damit machen willst. Das Original bleibt immer erhalten.") } };
        if (FfmpegPath is null) sp.Children.Add(Card(Row(P("FFmpeg fehlt noch. Ohne FFmpeg geht hier nichts.", Brush.Parse("#FACC15")), Nav2("Einrichten", "components"))));
        var files = FileShelf.Recent([MediaFolder, Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)], MediaStudio.Inputs, 15);
        sp.Children.Add(H("1. Datei wählen", 14));
        var path = new TextBox { Name = "media-path", Text = _mediaFile ?? "", PlaceholderText = "Pfad zur Datei", Width = 380 };
        sp.Children.Add(new WrapPanel { Children = { path, Act("media-use", "Übernehmen", () => MediaPick(path.Text?.Trim('"', ' ') ?? "")), Act("media-folder", "Medienordner öffnen", () => Launch(MediaFolder)) } });
        var i = 0;
        foreach (var f in files.Take(8)) { var fp = f.FullName; sp.Children.Add(Act($"media-file-{i++}", (fp == _mediaFile ? "● " : "") + f.Name, () => MediaPick(fp))); }
        if (_mediaFile is { } mf && File.Exists(mf))
        {
            sp.Children.Add(H("2. Was soll passieren?", 14));
            var start = new NumericUpDown { Name = "media-start", Minimum = 0, Value = 0, Increment = 1, Width = 120, FormatString = "0" };
            var len = new NumericUpDown { Name = "media-len", Minimum = 1, Value = 10, Increment = 1, Width = 120, FormatString = "0" };
            var wrap = new WrapPanel();
            foreach (var j in MediaStudio.Jobs)
            {
                var job = j; var b = Act("media-job-" + j.Id, j.Label, () => MediaRun(job.Id, (double)(start.Value ?? 0), (double)(len.Value ?? 10)));
                ToolTip.SetTip(b, j.Help); b.Margin = new Avalonia.Thickness(0, 0, 6, 6); wrap.Children.Add(b);
            }
            sp.Children.Add(Card(new StackPanel { Spacing = 6, Children = { P("Datei: " + Path.GetFileName(mf), Text), wrap,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { P("Ausschnitt ab Sekunde"), start, P("Länge"), len } } } }));
        }
        return KreativPage("Medien-Studio", sp);
    }
}
