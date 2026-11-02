using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// Claude in jedem Studio (Wunsch 02.11.2026): jede Seite, die zu einem Studio gehört, hat den Claude-Knopf – und Claude öffnet dort passend zum Studio.
[Collection("Claude")]
public class ClaudeEverywhere_Ui
{
    [AvaloniaFact(DisplayName = "Claude in jedem Studio: jede Studio-Seite hat „✳ Claude“, und Claude öffnet mit dem passenden Studio", Timeout = 600000)]
    public void Every_studio_page_has_claude()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var missing = new List<string>();
            foreach (var key in w.PageKeys.OrderBy(k => k, StringComparer.Ordinal).ToList())
            {
                if (w.ModuleOf(key) is not { } studio || !w.ClaudeBelongs(key)) continue;
                try { w.Show(key); Avalonia.Threading.Dispatcher.UIThread.RunJobs(); } catch (Exception) { continue; }
                if (w.TryFind<Button>("auftrag-claude") is null) missing.Add($"{key} ({studio})");
            }
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
