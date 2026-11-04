using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Videoschnitt – „Meine Exporte“ (Wunsch 04.11.2026: exportierte Videos ließen sich nicht wiederfinden, „Ordner öffnen“ stand nur kurz in
/// einer Einblendung). Jetzt bleibt eine Liste der fertigen Videos mit Abspielen, Ordner öffnen (Datei im Explorer markiert) und Löschen (Papierkorb).</summary>
public partial class MainWindow
{
    /// <summary>Tests: Löschen ersetzen (Datei → in den Papierkorb).</summary>
    public Action<string>? ExportRecycleOverride { get; set; }

    string ExportFolder => WorkFolder(Programs[VK].Folder);

    /// <summary>Fertige Videos des Videoschnitts, neueste zuerst.</summary>
    public List<FileInfo> EditExports(int max = 8)
    {
        var dir = new DirectoryInfo(ExportFolder);
        if (!dir.Exists) return [];
        return [.. dir.EnumerateFiles("*.mp4").Where(f => f.Length > 0).OrderByDescending(f => f.LastWriteTimeUtc).Take(max)];
    }

    /// <summary>Ordner öffnen und die Datei darin markieren (Windows-Explorer „/select“); sonst nur den Ordner.</summary>
    void RevealInFolder(string file)
    {
        var (exe, args) = DesktopPlatform.RevealCommand(file); // Windows markiert die Datei im Explorer, Linux öffnet den Ordner
        _core.Integrations.LaunchDirect(Principal.LocalOwner, exe, _core.Audit, args);
    }

    static string SizeText(long bytes) => bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):0.0} GB" : $"{Math.Max(1, bytes / (1 << 20))} MB";

    Control EditExportsPanel()
    {
        var sp = new StackPanel { Name = "ve-exports", Spacing = 4 };
        var files = EditExports();
        sp.Children.Add(new DockPanel { Children = {
            Dk(new Button { Name = "ve-exports-folder", Content = "Ordner öffnen", FontSize = 11, Padding = new Thickness(8, 2) }.Also(b => b.Click += (_, _) => Launch(ExportFolder)), Avalonia.Controls.Dock.Right),
            H("Meine Exporte", 13) } });
        if (files.Count == 0) { sp.Children.Add(P("Noch nichts exportiert. Fertige Videos erscheinen hier – mit Abspielen und Ordner öffnen.", Muted, 11.5)); return sp; }
        for (var i = 0; i < files.Count; i++)
        {
            var f = files[i]; var idx = i;
            Button B(string name, string label, Action a) { var b = new Button { Name = $"{name}-{idx}", Content = label, FontSize = 11, Padding = new Thickness(8, 2), Margin = new Thickness(0, 0, 4, 0) }; b.Click += (_, _) => a(); return b; }
            sp.Children.Add(new Border { Background = TileBg, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6), Child = new StackPanel { Spacing = 3, Children = {
                new TextBlock { Name = $"ve-exp-name-{idx}", Text = f.Name, Foreground = Text, FontSize = 12, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis },
                new TextBlock { Text = $"{f.LastWriteTime:dd.MM. HH:mm} · {SizeText(f.Length)}", Foreground = Muted, FontSize = 10.5 },
                new WrapPanel { Children = {
                    B("ve-exp-play", "▶ Abspielen", () => Launch(f.FullName)),
                    B("ve-exp-folder", "Ordner öffnen", () => RevealInFolder(f.FullName)),
                    B("ve-exp-del", "🗑", () =>
                    {
                        if (ExportRecycleOverride is { } o) o(f.FullName); else RecycleBin.Move(f.FullName);
                        _progMsg[VK] = $"„{f.Name}“ liegt im Papierkorb."; _core.Audit.Append(Principal.LocalOwner.Name, "video.export.geloescht", f.Name); Show(VK);
                    }) } } } } });
        }
        return sp;
    }
}
