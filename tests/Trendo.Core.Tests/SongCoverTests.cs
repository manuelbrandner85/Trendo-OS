using System.Text;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Wunsch 03.11.2026: Song-Cover wie echte Album-Cover – Motiv je Genre und Stimmung, ohne Schrift im Bild; Cover in der MP3.</summary>
public class SongCoverTests
{
    [Fact(DisplayName = "Song-Cover: Motiv passt zum Genre (Jazz, Rock, Elektro, Hip-Hop, Klassik, Lo-Fi vor Pop), Stimmung kommt dazu, Bildgestaltung wechselt, nie Schrift oder Logos")]
    public void Prompt_by_genre()
    {
        Assert.Contains("jazz club", SongCover.Prompt("ruhige Jazz-Ballade, Saxophon", "Nachts"));
        Assert.Contains("electric guitar", SongCover.Prompt("Rock, laut", "x"));
        Assert.Contains("neon", SongCover.Prompt("House, tanzbar", "x"));
        Assert.Contains("graffiti", SongCover.Prompt("Hip-Hop", "x"));
        Assert.Contains("grand piano", SongCover.Prompt("Klassik, Streicher", "x"));
        Assert.Contains("rainy window", SongCover.Prompt("Lo-Fi Pop", "x"));
        Assert.Contains("abstract", SongCover.Prompt("etwas ganz Eigenes", "x"));
        var happy = SongCover.Prompt("fröhlicher Pop", "Sommer");
        Assert.Contains("joyful", happy);
        Assert.NotEqual(SongCover.Prompt("Pop", "x", 0), SongCover.Prompt("Pop", "x", 1));
        foreach (var p in new[] { happy, SongCover.Prompt("Metal", "x") }) { Assert.Contains("no text", p); Assert.Contains("no logos", p); }
    }

    [Fact(DisplayName = "MP3 mit Cover: ID3v2.3 mit Titel, Name und Bild „Vorderseite“; alter ID3-Tag ersetzt, Ton unverändert; nur JPEG")]
    public void Embed_in_mp3()
    {
        byte[] audio = [0xFF, 0xFB, 0x90, 0x64, 9, 8, 7];
        byte[] oldTag = [.. "ID3"u8.ToArray(), 4, 0, 0, 0, 0, 0, 3, 1, 2, 3];
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 0xFF, 0xD9];
        var mp3 = SongCover.EmbedInMp3([.. oldTag, .. audio], jpeg, "Nachts im Blue Note", "Erika Muster");
        Assert.Equal("ID3", Encoding.ASCII.GetString(mp3, 0, 3)); Assert.Equal(3, mp3[3]);
        Assert.Equal(jpeg, SongCover.ReadMp3Cover(mp3));
        Assert.Equal(audio, mp3[^audio.Length..]);
        Assert.True(mp3.AsSpan().IndexOf(Encoding.Unicode.GetBytes("Nachts im Blue Note")) > 0); // Titel als UTF-16
        Assert.Equal(jpeg, SongCover.ReadMp3Cover(SongCover.EmbedInMp3(audio, jpeg, "", ""))); // ohne alten Tag
        Assert.Throws<ArgumentException>(() => SongCover.EmbedInMp3(audio, [0x89, 0x50, 0x4E, 0x47], "x", "y"));
        Assert.Null(SongCover.ReadMp3Cover(audio));
    }
}
