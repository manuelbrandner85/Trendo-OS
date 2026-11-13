using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Paket 01 – Studio 3, S3-17 (07.11.2026): Abspann im Videoschnitt – Zeilen „Rolle: Name, Name“, Länge, Schrift-Lizenzprüfung,
/// als Clip ans Ende der Zeitleiste (rückgängig machbar). Der Text bleibt im Projekt und lässt sich ändern und neu erzeugen.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Schriftdatei statt der Systemschrift.</summary>
    public string? CreditsFontOverride { get; set; }

    public static List<Credits.Section> ParseCredits(string text) =>
        [.. text.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && l.Contains(':'))
            .Select(l => new Credits.Section(l[..l.IndexOf(':')].Trim(), [.. l[(l.IndexOf(':') + 1)..].Split(',').Select(n => n.Trim()).Where(n => n.Length > 0)]))];

    /// <summary>Vorschlag aus dem Projekt: Musik-Spur und Untertitel als Abschnitte.</summary>
    static string CreditsSuggestion(JsonObject p)
    {
        var music = VideoEdit.Clips(p, "music").Select(m => Path.GetFileNameWithoutExtension(m.File)).Distinct().ToList();
        return "Regie: \nKamera: \nSchnitt: \n" + (music.Count > 0 ? "Musik: " + string.Join(", ", music) + "\n" : "") + "Mit Dank an: ";
    }

    public async Task<string?> EditCreditsAdd(ProgramDocument d, string text, double seconds)
    {
        var sections = ParseCredits(text);
        var font = CreditsFontOverride ?? VideoEdit.FontFile();
        if (font is null) { _progMsg[VK] = "Keine Schrift gefunden."; Show(VK); return null; }
        var fc = Credits.CheckFont(font);
        if (fc.Light == "rot") { _progMsg[VK] = "Abspann nicht erzeugt – " + fc.Text; Show(VK); return null; }
        int w = (int)Math.Max(320, d.Number("width", 1280)), h = (int)Math.Max(180, d.Number("height", 720));
        Credits.Template t;
        try { t = Credits.Validate(Credits.Rolling(d.Title.Length > 0 ? d.Title : "Abspann", sections, seconds, font, w, h)); }
        catch (ArgumentException e) { _progMsg[VK] = e.Message + " Schreibweise: „Rolle: Name, Name“ je Zeile."; Show(VK); return null; }
        var dir = Path.Combine(WorkFolder(Programs[VK].Folder), "Abspann"); Directory.CreateDirectory(dir);
        var outFile = Path.Combine(dir, $"{DevStudio.SafeName(d.Title.Length > 0 ? d.Title : "Film")}-abspann-{DateTime.Now:HHmmss}.mp4");
        var txt = Path.ChangeExtension(outFile, ".txt"); File.WriteAllText(txt, Credits.Text(t));
        if (FfmpegRunner(dir, out var why) is not { } run) { _progMsg[VK] = why!; Show(VK); return null; }
        _progMsg[VK] = "Abspann wird erzeugt …"; Show(VK);
        var r = await Task.Run(() => run(Credits.Args(t, txt, outFile)));
        if (!r.Ok || !File.Exists(outFile)) { _progMsg[VK] = "Abspann ging nicht: " + (r.Err.Trim().Split('\n').LastOrDefault() ?? ""); Show(VK); return null; }
        d.Edit("Abspann", x => { x["credits"] = text; VideoEdit.Add(x, VideoEdit.NewClip(outFile, seconds, true)); });
        _progMsg[VK] = $"Abspann ({seconds:0} s) liegt am Ende der Zeitleiste. Schrift: {fc.Text}" + (fc.Light == "gelb" ? " Vor der Veröffentlichung prüfen." : "");
        Show(VK);
        return outFile;
    }

    Control EditCreditsPanel(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "ve-credits", Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };
        sp.Children.Add(H("Abspann", 13));
        var box = new TextBox { Name = "ve-credits-text", Text = d.Text("credits", CreditsSuggestion(d.Data)), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, FontSize = 12 };
        var len = new NumericUpDown { Name = "ve-credits-len", Value = 12, Minimum = 3, Maximum = 120, Increment = 1, FormatString = "0", Width = 100, FontSize = 12 };
        var go = new Button { Name = "ve-credits-add", Content = "Abspann ans Ende", FontSize = 12 };
        go.Click += async (_, _) => await EditCreditsAdd(d, box.Text ?? "", (double)(len.Value ?? 12));
        sp.Children.Add(box);
        sp.Children.Add(new WrapPanel { Children = { new TextBlock { Text = "Sekunden ", Foreground = Muted, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, FontSize = 12 }, len, new Border { Width = 6 }, go } });
        var font = CreditsFontOverride ?? VideoEdit.FontFile();
        if (font is not null) { var fc = Credits.CheckFont(font); sp.Children.Add(P($"Schrift {Path.GetFileName(font)}: {fc.Text}", fc.Light == "gruen" ? Muted : Brush.Parse("#FACC15"), 11)); }
        sp.Children.Add(P("Je Zeile „Rolle: Name, Name“. Rollt von unten nach oben, blendet weich ein und aus.", Muted, 10.5));
        return sp;
    }
}
