using Avalonia.Controls;
using Avalonia;
using Avalonia.LogicalTree;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// Claude in jedem Studio (Wunsch 02.11.2026): jede Seite, die zu einem Studio gehört, hat den Claude-Knopf – und Claude öffnet dort passend zum Studio.
[Collection("Claude")]
public class ClaudeEverywhere_Ui
{
    [AvaloniaFact(DisplayName = "Claude in jedem Studio: jede Studio-Seite hat „Claude“, und Claude öffnet mit dem passenden Studio", Timeout = 600000)]
    public void Every_studio_page_has_claude()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var missing = new List<string>(); var hidden = new List<string>();
            foreach (var key in w.PageKeys.OrderBy(k => k, StringComparer.Ordinal).ToList())
            {
                if (w.ModuleOf(key) is not { } studio || !w.ClaudeBelongs(key)) continue;
                try { w.Show(key); Avalonia.Threading.Dispatcher.UIThread.RunJobs(); } catch (Exception ex) { missing.Add($"{key} ({studio}): Seite lädt nicht – {ex.GetType().Name}"); continue; }
                // E1 (T-0616): Claude steht im Menü „KI ▾“ der Leiste (oder als schlanker Knopf) – sichtbar sein muss der Weg dorthin
                if (w.TryFind<Button>("auftrag-claude") is null) { missing.Add($"{key} ({studio})"); continue; }
                var b = w.GetLogicalDescendants().OfType<Button>().FirstOrDefault(x => x.Name == "auftrag-claude" && x.FindLogicalAncestorOfType<Avalonia.Controls.Primitives.Popup>() is null) // schlanker Knopf (3D, Fokus)
                    ?? w.TryFind<Button>("ki-menu")!;
                // sichtbar ohne Scrollen: der Knopf liegt ganz im Fenster (Wunsch 02.11.2026 – auf „3D-Modelle erstellen“ war er unten in einer Spalte versteckt)
                // gemessen am Studio-Fenster (win-…): Trendo zeichnet auf einer Designfläche und skaliert sie aufs echte Fenster
                var frame = Avalonia.VisualTree.VisualExtensions.GetVisualAncestors(b).OfType<Control>().FirstOrDefault(c => c.Name?.StartsWith("win-", StringComparison.Ordinal) == true && c.GetType().Name == "DeskFrame") ?? (Control)w;
                var at = b.TranslatePoint(new Point(0, 0), frame);
                if (!b.IsEffectivelyVisible || at is not { } p || b.Bounds.Width < 10 || p.X < 0 || p.Y < 0 || p.X + b.Bounds.Width > frame.Bounds.Width + 1 || p.Y + b.Bounds.Height > frame.Bounds.Height + 1)
                    hidden.Add($"{key} ({studio}) bei {at} im Fenster {frame.Bounds.Size}");
            }
            Assert.True(hidden.Count == 0, "Claude nicht sichtbar: " + string.Join(", ", hidden));
            if (Environment.GetEnvironmentVariable("TRENDO_CLAUDE_DIAG") is { Length: > 0 } diag)
                File.WriteAllLines(diag, w.PageKeys.OrderBy(k => k).Select(k => $"{k}	{w.ModuleOf(k) ?? "-"}	{w.ClaudeBelongs(k)}"));
            Assert.True(missing.Count == 0, "Ohne Claude: " + string.Join(", ", missing));
        }
        finally { w.Close(); }
    }

    [Fact(DisplayName = "Claude in jedem Studio: jedes Studio aus dem Studio-Katalog hat ein eigenes Claude-Profil (Rolle, Lieferformat, vier Startideen)")]
    public void Every_studio_has_profile()
    {
        var missing = StudioCatalog.ById.Keys.Where(id => !Trendo.Core.StudioProfiles.Has(id)).ToList();
        Assert.True(missing.Count == 0, "Ohne Profil: " + string.Join(", ", missing));
        foreach (var id in StudioCatalog.ById.Keys)
        {
            var p = Trendo.Core.StudioProfiles.For(id);
            Assert.Equal(4, p.Ideas.Count); Assert.StartsWith("Du ", p.Role); Assert.NotNull(p.Format);
        }
        Assert.Contains("Spieleentwickler", Trendo.Core.StudioProfiles.For("CORE-06").Role);
    }
}
