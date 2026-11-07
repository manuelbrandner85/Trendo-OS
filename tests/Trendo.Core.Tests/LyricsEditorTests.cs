using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Trendo Musik wie Suno V6 (07.11.2026): Liedtext-Editor und Texter.</summary>
public class LyricsEditorTests
{
    const string Text = "[Strophe]\nDie Straße glänzt im Abendlicht\nwir fahren los und halten nicht\n[Refrain]\nSommer, nimm uns mit";

    [Fact(DisplayName = "Suno V6: Zeile an der Schreibmarke, Abschnitt der Zeile, Zeile ersetzen (Marken geschützt), Etikett einfügen, Statistik")]
    public void Lines()
    {
        Assert.Equal(0, LyricsEditor.LineAt(Text, 0)); Assert.Equal(2, LyricsEditor.LineAt(Text, Text.IndexOf("wir", StringComparison.Ordinal)));
        Assert.Equal("Strophe", LyricsEditor.SectionOf(Text, 2)); Assert.Equal("Refrain", LyricsEditor.SectionOf(Text, 4)); Assert.Null(LyricsEditor.SectionOf("ohne\nMarke", 1));
        Assert.Contains("wir rollen leise durch die Nacht", LyricsEditor.ReplaceLine(Text, 2, "wir rollen leise durch die Nacht\n"));
        Assert.Throws<ArgumentException>(() => LyricsEditor.ReplaceLine(Text, 3, "x"));
        Assert.Throws<ArgumentOutOfRangeException>(() => LyricsEditor.ReplaceLine(Text, 9, "x"));
        var (t, c) = LyricsEditor.InsertSection("Zeile eins", 10, "Bridge"); Assert.Equal("Zeile eins\n[Bridge]\n", t); Assert.Equal(t.Length, c);
        Assert.Equal((3, 15, 2), LyricsEditor.Stats(Text)); // 5 + 6 + 4 Wörter
    }

    [Fact(DisplayName = "Suno V6: Anweisungen an die Text-KI enthalten Zeile, Nachbarn, Silbenzahl und Texter-Beispiele; Antworten werden bereinigt")]
    public void Prompts()
    {
        var who = new Lyricist { Name = "Balladen", Examples = ["Mein eigener Text\nzweite Zeile\ndritte Zeile\nvierte Zeile"] };
        var p = LyricsEditor.LinePrompt(Text, 2, "lustiger", who);
        Assert.Contains("wir fahren los und halten nicht", p); Assert.Contains("Die Straße glänzt", p); Assert.Contains("Silbenzahl", p); Assert.Contains("Mein eigener Text", p);
        Assert.DoesNotContain("Mein eigener Text", LyricsEditor.LinePrompt(Text, 2, "lustiger", null));
        Assert.Contains("drei Varianten", LyricsEditor.VariantsPrompt(Text, 1, null));
        Assert.Contains("Reimschema AABB", LyricsEditor.WholePrompt(Text, "Reimschema AABB", null));
        Assert.Equal(["Erste Zeile", "Zweite Zeile", "Dritte"], LyricsEditor.ParseVariants("1. „Erste Zeile“\n2) Zweite Zeile\n- Dritte\n[Refrain]\nVierte"));
        Assert.Equal("Neue Zeile", LyricsEditor.CleanLine("\"Neue Zeile\"\n"));
        Assert.Throws<InvalidOperationException>(() => LyricsEditor.CleanLine("  "));
    }

    [Fact(DisplayName = "Suno V6: Texter – Beispiele sammeln (mind. 4 Zeilen, höchstens 8), gleicher Name ergänzt, löschen")]
    public void Lyricists_store()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-ly-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            var s = new Lyricists(root);
            Assert.Throws<ArgumentException>(() => s.AddExample("", "a\nb\nc\nd"));
            Assert.Throws<ArgumentException>(() => s.AddExample("Ich", "nur\nzwei"));
            var a = s.AddExample("Meine Balladen", "eins\nzwei\ndrei\nvier");
            var b = s.AddExample("meine balladen", "fünf\nsechs\nsieben\nacht");
            Assert.Equal(a.Id, b.Id); Assert.Equal(2, s.Get(a.Id)!.Examples.Count);
            for (var i = 0; i < 10; i++) s.AddExample("Meine Balladen", $"z{i}\nz\nz\nz");
            Assert.Equal(8, s.Get(a.Id)!.Examples.Count);
            s.Delete(a.Id); Assert.Empty(s.All());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
