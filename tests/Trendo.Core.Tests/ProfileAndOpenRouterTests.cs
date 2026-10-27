using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Wunsch 27.10.2026: Benutzerprofil mit PIN (B2), Personen (B7), Interessen (B3), Lesezeichen (B8), kostenlose OpenRouter-KIs (C1, C4).</summary>
public class ProfileAndOpenRouterTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-prof-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "Anmeldung: PIN prüfen, nur Prüfwert gespeichert, Wiederherstellungs-Code setzt neu, Sperre nach 5 Fehlversuchen")]
    public void Pin_and_recovery()
    {
        var d = Tmp();
        try
        {
            var l = new ProfileLock(d);
            Assert.False(l.Enabled);
            Assert.NotNull(ProfileLock.Problem(ProfileLock.Kind.Pin, "1234"));
            Assert.NotNull(ProfileLock.Problem(ProfileLock.Kind.Pin, "12a4"));
            Assert.Throws<ArgumentException>(() => l.Set(ProfileLock.Kind.Pin, "1111"));
            var code = l.Set(ProfileLock.Kind.Pin, "4711")!;
            Assert.Matches("^[A-Z2-9]{4}-[A-Z2-9]{4}-[A-Z2-9]{4}-[A-Z2-9]{4}$", code);
            Assert.DoesNotContain("4711", File.ReadAllText(Path.Combine(d, "profil", "anmeldung.json")));
            Assert.True(new ProfileLock(d).Verify("4711"));
            Assert.True(new ProfileLock(d).VerifyRecovery(code.ToLowerInvariant().Replace("-", " ")));
            var t0 = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
            for (var i = 0; i < 5; i++) Assert.False(l.Verify("0000", t0));
            Assert.False(l.Verify("4711", t0.AddSeconds(5))); // gesperrt
            Assert.True(l.Verify("4711", t0.AddSeconds(31)));
            l.Set(ProfileLock.Kind.None);
            Assert.False(new ProfileLock(d).Enabled);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Personen: eigener Datenordner, Kind mit einfacher Bedienung; Interessen ergeben Pakete und Symbole; Lesezeichen aus Edge/Chrome ohne Doppelte")]
    public void Persons_interests_bookmarks()
    {
        var d = Tmp();
        try
        {
            var ps = new PersonStore(d);
            var kid = ps.Add("Erika Muster", "emoji:🐼", child: true);
            Assert.Equal(d, PersonStore.BaseOf(ps.RootOf(kid.Id)));
            Assert.Equal(d, PersonStore.BaseOf(d));
            var s = UserSettings.Load(ps.RootOf(kid.Id));
            Assert.True(s.Child && s.SimpleMode && s.EasyLanguage);
            Assert.Throws<ArgumentException>(() => ps.Add("  ", "", false));
            Assert.Contains("office", Interests.PacksFor(["buero"]));
            Assert.Contains("CORE-05", Interests.StudiosFor(["spiele"]));
            const string json = """{"roots":{"bookmark_bar":{"type":"folder","name":"Lesezeichenleiste","children":[{"type":"url","name":"Beispiel","url":"https://example.org/"},{"type":"url","name":"lokal","url":"file:///c:/x"}]},"other":{"type":"folder","name":"Weitere","children":[]}}}""";
            var found = BrowserImport.Parse(json, "Edge");
            var b = Assert.Single(found);
            Assert.Equal("Edge: Lesezeichenleiste", b.Folder);
            Assert.Equal(1, BrowserImport.ImportInto(d, found));
            Assert.Equal(0, BrowserImport.ImportInto(d, found));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "OpenRouter: nur wirklich kostenlose Modelle, Bereiche aus Ein-/Ausgabe, tägliche Aktualisierung, Tageszähler")]
    public void OpenRouter_free()
    {
        var d = Tmp();
        try
        {
            const string json = """
            {"data":[
              {"id":"a/text:free","name":"Text (free)","description":"x","context_length":131072,"pricing":{"prompt":"0","completion":"0"},"architecture":{"input_modalities":["text"],"output_modalities":["text"]}},
              {"id":"b/vision:free","name":"Vision","pricing":{"prompt":"0","completion":"0","image":"0"},"architecture":{"input_modalities":["text","image","audio"],"output_modalities":["text"]}},
              {"id":"c/paid","name":"Teuer","pricing":{"prompt":"0.000001","completion":"0.000002"},"architecture":{"input_modalities":["text"],"output_modalities":["text"]}}
            ]}
            """;
            var list = OpenRouterFree.Parse(json);
            Assert.Equal(["a/text:free", "b/vision:free"], list.Select(m => m.Id));
            Assert.Equal("Text", list[0].Name);
            Assert.Contains(OpenRouterFree.Images, list[1].Areas); Assert.Contains(OpenRouterFree.Audio, list[1].Areas);
            Assert.Equal("b/vision:free", OpenRouterFree.Best(list, OpenRouterFree.Images, null)!.Id);
            Assert.Equal("b/vision:free", OpenRouterFree.Best(list, OpenRouterFree.Text, "b/vision:free")!.Id);
            Assert.True(OpenRouterFree.Stale(d));
            OpenRouterFree.Save(d, list, DateTime.UtcNow);
            Assert.False(OpenRouterFree.Stale(d));
            Assert.True(OpenRouterFree.Stale(d, DateTime.UtcNow.AddHours(25)));
            var c = new RemoteKiCounter(d); var day = new DateTime(2026, 10, 27, 9, 0, 0);
            Assert.Equal(0, c.Today(day)); c.Add(day); Assert.Equal(2, c.Add(day)); Assert.Equal(0, c.Today(day.AddDays(1)));
            var set = new KiSourceSettings();
            Assert.Equal("chat", set.RemoteMode); Assert.False(set.RemoteEverywhere);
        }
        finally { Directory.Delete(d, true); }
    }
}
