using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
                if (Click(w, launcher, root, page, b, name, label) is { } f) failures.Add(f);
            }
        }
        return failures;
    }

    /// <summary>Wie <see cref="Require"/>, aber jeder Knopf in einem frischen Fenster: keine Meldung eines vorherigen Knopfs verdeckt die Wirkung.</summary>
    public static IReadOnlyList<string> RequireFresh(Func<(MainWindow W, FakeLauncher L, string Root)> open, params string[] pages)
    {
        var failures = new List<string>();
        foreach (var page in pages)
        {
            var (w0, _, _) = open();
            w0.Show(page); Pump();
            var body0 = Body(w0, page);
            if (body0 is null) { failures.Add(page + ": Seiteninhalt nicht gefunden"); w0.Close(); continue; }
            var keys = body0.GetLogicalDescendants().OfType<Button>().Where(b => b.IsVisible && b.IsEnabled)
                .Select(b => (Name: b.Name ?? "", Label: Label(b))).Distinct().ToList();
            w0.Close();
            if (keys.Count == 0) { failures.Add(page + ": kein sichtbarer aktiver Knopf"); continue; }
            foreach (var (name, label) in keys)
            {
                if (name.EndsWith("-tab-" + page, StringComparison.Ordinal) || Risiko.ContainsKey(name)) continue;
                var (w, l, root) = open();
                try
                {
                    w.Show(page); Pump();
                    var body = Body(w, page);
                    var b = body?.GetLogicalDescendants().OfType<Button>().FirstOrDefault(x => name.Length > 0 ? x.Name == name : string.IsNullOrEmpty(x.Name) && Label(x) == label);
                    if (b is null || !b.IsVisible || !b.IsEnabled) continue;
                    if (Click(w, l, root, page, b, name, label) is { } f) failures.Add(f);
                }
                finally { w.ReopenedAs?.Close(); w.Close(); }
            }
        }
        return failures;
    }

    static string? Click(MainWindow w, FakeLauncher launcher, string root, string page, Button b, string name, string label)
    {
        var body = Body(w, page)!;
        var t0 = Texts(body); var tw0 = Texts(w); var r0 = w.ReopenedAs; var v0 = w.CurrentView; var l0 = launcher.Opened.Count; var f0 = Stamp(root);
        try
        {
            // Wie ein echter Klick: Schalter (Häkchen, Auswahlpunkt) wechseln dabei ihren Zustand
            if (b is ToggleButton tg) tg.IsChecked = tg.IsChecked != true;
            b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            // Wirkung darf auch nach einem await kommen (KI, Netz, Prüfung) – bis zu 2 s nachsehen
            for (var i = 0; i < 40; i++)
            {
                Pump();
                var now = Body(w, page) ?? body;
                if (w.CurrentView != v0 || launcher.Opened.Count > l0 || Stamp(root) != f0 || Texts(now) != t0 || Texts(w) != tw0
                    || w.ReopenedAs != r0) // Einstellung übernommen: Trendo baut das Fenster neu auf
                    return null;
                Thread.Sleep(50);
            }
            return $"{page}	{name}	{label}	ohne Wirkung";
        }
        catch (Exception ex) { return $"{page}	{name}	{label}	Absturz {ex.GetType().Name}: {ex.Message}"; }
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
            if (c is Control { Name: { } n } && Uhr.Any(n.StartsWith)) continue; // laufende Uhren ändern sich ohne Klick
            if (c is TextBlock t) sb.Append(t.Text).Append('|');
            else if (c is SelectableTextBlock s) sb.Append(s.Text).Append('|');
            else if (c is TextBox x) sb.Append(x.Text).Append('|');
            else if (c is Button btn && btn.Content is string label) sb.Append(label).Append('|');
        }
        return sb.ToString();
    }

    static readonly string[] Uhr = ["clock", "sw-time", "timer-text", "desk-clock", "widget-clock"];

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
