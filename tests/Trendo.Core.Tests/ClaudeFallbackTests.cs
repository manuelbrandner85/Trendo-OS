using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// Ersatz-KI und kostenlose KI-Werkzeuge im Kern.
public class ClaudeFallbackTests
{
    [Fact(DisplayName = "Kontingent: Rücksprung-Zeit aus Claudes Meldung (Zeitstempel oder „resets 3pm“), sonst in 30 Minuten; gespeichert je Person")]
    public void Limit_reset()
    {
        var now = new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.FromHours(1));
        var at = now.AddHours(3);
        Assert.Equal(at.ToUnixTimeSeconds(), ClaudeLimit.ResetOf($"Claude AI usage limit reached|{at.ToUnixTimeSeconds()}", now).ToUnixTimeSeconds());
        Assert.Equal(15, ClaudeLimit.ResetOf("5-hour limit reached ∙ resets 3pm", now).Hour);
        Assert.Equal(now.AddDays(1).Day, ClaudeLimit.ResetOf("limit reached, resets 9am", now).Day); // 9 Uhr ist vorbei → morgen
        Assert.Equal(now + ClaudeLimit.Retry, ClaudeLimit.ResetOf("usage limit", now));
        var root = Path.Combine(Path.GetTempPath(), "trendo-limit-" + Guid.NewGuid().ToString("N"));
        var l = new ClaudeLimit(root);
        Assert.False(l.Active(now)); l.Set(at); Assert.True(new ClaudeLimit(root).Active(now)); Assert.False(l.Active(at.AddMinutes(1)));
        l.Clear(); Assert.Null(l.Until);
    }

    [Fact(DisplayName = "KI-Werkzeuge: Bestellzeilen erkennen (Art, Eingabe, Zieldatei im Projekt), unsichere Pfade ersetzen, aus der Anzeige entfernen")]
    public void Tool_markers()
    {
        var t = "Los geht's.\n[[trendo:bild | Ein Drache | bilder/drache.png]]\n[[trendo:musik | fröhliche Spielmusik]]\n[[trendo:bild | böse | ../../windows/x.png]]\n[[trendo:hack | x | y]]\nFertig.";
        var c = ClaudeTools.Parse(t);
        Assert.Equal(3, c.Count);
        Assert.Equal(new ClaudeTools.Call("bild", "Ein Drache", "bilder/drache.png"), c[0]);
        Assert.Equal("musik", c[1].Kind); Assert.Equal("medien/musik-2.mp3", c[1].File);
        Assert.Equal("medien/bild-3.png", c[2].File); // „..“ ersetzt
        Assert.Equal("Los geht's.\n\nFertig.", ClaudeTools.Strip(t));
        Assert.Contains("[[trendo:", ClaudeTools.Rule);
    }

    [Fact(DisplayName = "Ersatz-KI: Auftrag im Agenten-Format mit Werkzeug-Regel; Zusammenfassung aus JSON, sonst der Text")]
    public void Fallback_prompt_and_summary()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-fb-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var p = ClaudeFallback.Prompt("Baue ein Spiel", "Du baust Spiele.", root);
        Assert.Contains("Baue ein Spiel", p); Assert.Contains("\"dateien\"", p); Assert.Contains("[[trendo:", p);
        Assert.Equal("Fertig: „Quiz“", ClaudeFallback.Summary("{\"zusammenfassung\":\"Fertig: „Quiz“\",\"dateien\":[]}"));
        Assert.Equal("Das geht so.", ClaudeFallback.Summary("Das geht so."));
        Directory.Delete(root, true);
    }
}
