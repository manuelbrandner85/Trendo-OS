using System.Text.Json.Nodes;
using Trendo.Core.Kie;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0618: Veo 3.1, Suno-Geräusche und die eigene Stimme (Anfrageformen aus docs.kie.ai, Stand 14.11.2026).</summary>
public class KieAusbauTests
{
    [Fact(DisplayName = "kie.ai Veo 3.1: Modell veo-3-1, Stufe Fast, Hoch-/Querformat, Startbild schaltet auf Bild-zu-Video")]
    public void Veo()
    {
        var m = KieModels.Find("veo-3-1")!;
        Assert.Equal(KieKind.Video, m.Kind);
        var t = m.Input(new KieInput(Prompt: "Ein Leuchtturm im Sturm", Aspect: "9:16"));
        Assert.Equal("veo3_fast", (string?)t["model"]); Assert.Equal("9:16", (string?)t["aspect_ratio"]); Assert.Equal("TEXT_2_VIDEO", (string?)t["generation_type"]);
        Assert.Null(t["image_urls"]);
        var b = m.Input(new KieInput(Prompt: "Kamera fährt heran", Aspect: "1:1", ImageUrls: ["https://x/a.png", "https://x/b.png", "https://x/c.png"]));
        Assert.Equal("16:9", (string?)b["aspect_ratio"]); // 1:1 kann Veo nicht
        Assert.Equal("FIRST_AND_LAST_FRAMES_2_VIDEO", (string?)b["generation_type"]);
        Assert.Equal(2, b["image_urls"]!.AsArray().Count); // höchstens erstes und letztes Bild
        Assert.False(KieModels.PriceKnown(m, new KieInput(Prompt: "x"))); // Preis vorab unbekannt → Zusatzbestätigung
        Assert.Equal("Beschreibe, was entstehen soll.", KieModels.Blocked(m, new KieInput(), false));
    }

    [Fact(DisplayName = "kie.ai Geräusche: Suno sounds mit V6, Loop und Tempo, 500 Zeichen, keine fremden Stimmen, Audio-Prüfung")]
    public void Sounds()
    {
        var m = KieModels.Find("ai-music-api/sounds")!;
        Assert.Equal(KieKind.Geraeusch, m.Kind);
        var t = m.Input(new KieInput(Prompt: "Regen auf einem Blechdach", Loop: true, Tempo: 90));
        Assert.Equal("V6", (string?)t["model"]); Assert.True((bool)t["sound_loop"]!); Assert.Equal(90, (int)t["sound_tempo"]!);
        Assert.Null(m.Input(new KieInput(Prompt: "Klick"))["sound_tempo"]); // automatisch
        Assert.Null(m.Input(new KieInput(Prompt: "Klick", Tempo: 999))["sound_tempo"]);
        Assert.Contains("500", KieModels.Blocked(m, new KieInput(Prompt: new string('a', 501)), false));
        Assert.Null(KieModels.Blocked(m, new KieInput(Prompt: "Wind in den Bäumen"), false));
        Assert.NotNull(KieModels.Blocked(m, new KieInput(Prompt: "klingt wie Max Mustermann"), false));
        Assert.Equal("Geräusche & Klänge", KieModels.KindLabel(KieKind.Geraeusch));
        Assert.Equal(".mp3", KieModels.Extension("https://x/y", KieKind.Geraeusch));
        Assert.Contains(KieKind.Geraeusch, KieModels.ForStudio("STUDIO-MUSIK-01"));
        Assert.Contains(KieKind.Geraeusch, KieModels.ForStudio("CORE-01"));
        Assert.True(KieModels.LooksLike([0x49, 0x44, 0x33, 4], KieKind.Geraeusch));
        Assert.False(KieModels.LooksLike("<html>"u8.ToArray(), KieKind.Geraeusch));
    }

    [Fact(DisplayName = "kie.ai eigene Stimme: Prüfsatz-Eingabe mit Grenzen, Stimm-Anlage, Prüfsatz lesen, Lied nutzt persona_id nur mit Gesang")]
    public void Eigene_Stimme()
    {
        var p = KieVoice.PhraseInput("https://up/x.mp3", 2, 22, "de");
        Assert.Equal(2, (int)p["vocal_start_s"]!); Assert.Equal(22, (int)p["vocal_end_s"]!); Assert.Equal("de", (string?)p["language"]);
        Assert.Equal("de", (string?)KieVoice.PhraseInput("https://up/x.mp3", 0, 10, "xx")["language"]);
        Assert.Throws<ArgumentException>(() => KieVoice.PhraseInput("C:\\lokal.mp3", 0, 10, "de"));
        Assert.Throws<ArgumentException>(() => KieVoice.PhraseInput("https://up/x.mp3", 5, 7, "de"));   // zu kurz
        Assert.Throws<ArgumentException>(() => KieVoice.PhraseInput("https://up/x.mp3", 0, 90, "de"));  // zu lang
        Assert.Throws<ArgumentException>(() => KieVoice.PhraseInput("https://up/x.mp3", 10, 10, "de"));
        var c = KieVoice.CreateInput("phrase-1", "https://up/satz.mp3", "", "Pop");
        Assert.Equal("phrase-1", (string?)c["task_id"]); Assert.Equal("Meine Stimme", (string?)c["voice_name"]); Assert.Equal("beginner", (string?)c["singer_skill_level"]);
        Assert.Throws<ArgumentException>(() => KieVoice.CreateInput("", "https://up/satz.mp3", "", ""));
        Assert.Equal("Die Sonne geht auf", KieVoice.Phrase("{\"data\":{\"verify_phrase_text\":\" Die Sonne geht auf \"}}"));
        Assert.Null(KieVoice.Phrase("{\"data\":{}}")); Assert.Null(KieVoice.Phrase("kaputt")); Assert.Null(KieVoice.Phrase(null));

        var song = KieModels.Find("ai-music-api/generate")!;
        var withVoice = song.Input(new KieInput(Title: "T", Style: "Pop", Lyrics: "la", PersonaId: "phrase-1"));
        Assert.Equal("phrase-1", (string?)withVoice["persona_id"]); Assert.Equal("voice_persona", (string?)withVoice["persona_model"]);
        Assert.Null(song.Input(new KieInput(Title: "T", Style: "Pop", Instrumental: true, PersonaId: "phrase-1"))["persona_id"]);
        Assert.Null(song.Input(new KieInput(Title: "T", Style: "Pop"))["persona_id"]);
    }

    [Fact(DisplayName = "kie.ai Modellabgleich kennt Veo 3.1 und Geräusche unter ihren Doku-Adressen")]
    public void Abgleich()
    {
        var all = string.Join(" ", KieModels.All.Select(m => m.Id)) + " kling-3-0 suno ai-avatar multilingual-v2 veo3-api generate-sounds";
        Assert.Empty(KieModels.Missing(all));
        Assert.Contains("veo-3-1", KieModels.Missing("nur suno und kling-3-0"));
    }
}
