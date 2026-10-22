using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;

namespace Trendo.App.Tests;

/// <summary>Klickt jeden sichtbaren, aktiven Knopf einer Seite. Wirkung ist Seitenwechsel, gestartetes Programm, geschriebene Datei oder geänderter Text im Seiteninhalt.</summary>
public static class ButtonProbe
{
    /// <summary>Knöpfe, die im Test nicht ausgelöst werden. Der Grund muss hier stehen, sonst gilt der Knopf als ohne Prüfung und der Test schlägt fehl, sobald jemand den Eintrag leert.</summary>
    public static readonly Dictionary<string, string> Risiko = new(StringComparer.Ordinal)
    {
        ["rec-start"] = "Startet eine echte Bildschirmaufnahme mit ffmpeg, nicht über den Fake-Launcher.",
        ["shot-screen"] = "Nimmt den echten Bildschirm mit ffmpeg auf, nicht über den Fake-Launcher.",
        ["trash-yes"] = "Leert den Papierkorb endgültig.",
        ["print-yes"] = "Schickt einen Auftrag an den echten Drucker.",
    };

    public static IReadOnlyList<string> Require(MainWindow w, FakeLauncher launcher, string root, params string[] pages)
    {
        foreach (var kv in Risiko)
            if (string.IsNullOrWhiteSpace(kv.Value))
                return [$"Risiko-Eintrag {kv.Key} ohne Begründung"];
        var failures = new List<string>();
        foreach (var page in pages)
        {
            w.Show(page); Pump();
            var body = Body(w, page);
            if (body is null) { failures.Add(page + ": Seiteninhalt nicht gefunden"); continue; }
            var buttons = body.GetLogicalDescendants().OfType<Button>().Where(b => b.IsVisible && b.IsEnabled).ToList();
            if (buttons.Count == 0) { failures.Add(page + ": kein sichtbarer aktiver Knopf"); continue; }
            var seen = new HashSet<string>();
            foreach (var spec in buttons)
            {
                var name = spec.Name ?? "";
                var label = Label(spec);
                var key = name.Length > 0 ? name : label;
                if (!seen.Add(key)) continue;
                if (name.EndsWith("-tab-" + page, StringComparison.Ordinal)) continue;
                if (Risiko.ContainsKey(name)) continue;
                w.Show(page); Pump();
                body = Body(w, page);
                if (body is null) { failures.Add($"{page}\t{name}\t{label}\tSeite fehlt nach Neuaufbau"); continue; }
                var b = name.Length > 0
                    ? body.GetLogicalDescendants().OfType<Button>().FirstOrDefault(x => x.Name == name && x.IsVisible && x.IsEnabled)
                    : body.GetLogicalDescendants().OfType<Button>().FirstOrDefault(x => string.IsNullOrEmpty(x.Name) && Label(x) == label && x.IsVisible && x.IsEnabled);
                if (b is null) continue;
                var t0 = Texts(body); var v0 = w.CurrentView; var l0 = launcher.Opened.Count; var f0 = Stamp(root);
                try
                {
                    b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
                    var now = Body(w, page) ?? body;
                    var eff = new List<string>();
                    if (w.CurrentView != v0) eff.Add("seite");
                    if (launcher.Opened.Count > l0) eff.Add("programm");
                    if (Stamp(root) != f0) eff.Add("datei");
                    if (Texts(now) != t0) eff.Add("text");
                    if (eff.Count == 0) failures.Add($"{page}\t{name}\t{label}\tohne Wirkung");
                }
                catch (Exception ex) { failures.Add($"{page}\t{name}\t{label}\tAbsturz {ex.GetType().Name}: {ex.Message}"); }
            }
        }
        return failures;
    }

    static ContentControl? Body(MainWindow w, string page) =>
        w.GetLogicalDescendants().OfType<ContentControl>().FirstOrDefault(c => c.Name == "win-body-" + w.WindowGroupOf(page));

    static string Label(Button b) => b.Content as string
        ?? string.Join(" ", b.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

    static string Texts(Control root)
    {
        var sb = new StringBuilder();
        foreach (var c in root.GetLogicalDescendants())
        {
            if (c is TextBlock t) sb.Append(t.Text).Append('|');
            else if (c is SelectableTextBlock s) sb.Append(s.Text).Append('|');
            else if (c is TextBox x) sb.Append(x.Text).Append('|');
            else if (c is Button btn && btn.Content is string label) sb.Append(label).Append('|');
        }
        return sb.ToString();
    }

    static long Stamp(string root)
    {
        if (!Directory.Exists(root)) return 0;
        long n = 0;
        foreach (var f in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(f).Contains("audit", StringComparison.OrdinalIgnoreCase)) continue;
            try { n += File.GetLastWriteTimeUtc(f).Ticks % 1000003 + 1; } catch (IOException) { }
        }
        return n;
    }

    static void Pump() { for (var i = 0; i < 6; i++) Dispatcher.UIThread.RunJobs(); }
}
