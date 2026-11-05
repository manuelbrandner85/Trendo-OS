using System.Security.Cryptography;
using System.Text;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>System-Studios (Vorbild-Ausbau 05.11.2026): Kostenlose KI (X-05), Erweiterungen (X-04), Sauberer Nachbau (X-10), Sicherung (CORE-11), Projektmappe (CORE-01).</summary>
public class SystemPagesTests
{
    const string Models = """
        {"data":[
          {"id":"a/klein:free","name":"Klein (free)","description":"kurz","context_length":8000,"pricing":{"prompt":"0","completion":"0"},"architecture":{"input_modalities":["text"],"output_modalities":["text"]}},
          {"id":"b/gross:free","name":"Groß (free)","description":"%LANG%","context_length":128000,"pricing":{"prompt":"0","completion":"0","image":"0"},"architecture":{"input_modalities":["text","image"],"output_modalities":["text"]}},
          {"id":"c/bild","name":"Bezahlt","context_length":999999,"pricing":{"prompt":"0","completion":"0","image":"0.002"}},
          {"id":"d/teuer","name":"Teuer","context_length":999999,"pricing":{"prompt":"0.00001","completion":"0"}}
        ]}
        """;

    [Fact(DisplayName = "X-05: Kostenlose Internet-KIs – nur Modelle ohne jeden Preis (auch Bild-Preis), „(free)“ aus dem Namen, Beschreibung gekürzt, größter Kontext zuerst; Liste speichern, nach 24 h veraltet")]
    public void OpenRouter_free()
    {
        var list = OpenRouterFree.Parse(Models.Replace("%LANG%", new string('x', 400)));
        Assert.Equal(["b/gross:free", "a/klein:free"], list.Select(m => m.Id));
        Assert.Equal(("Groß", true), (list[0].Name, list[0].Description.EndsWith(" …")));
        Assert.Contains(OpenRouterFree.Images, list[0].Areas); Assert.DoesNotContain(OpenRouterFree.Images, list[1].Areas);
        var root = Path.Combine(Path.GetTempPath(), "trendo-or-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            var t = new DateTime(2026, 11, 5, 8, 0, 0, DateTimeKind.Utc);
            OpenRouterFree.Save(root, list, t);
            Assert.Equal(2, OpenRouterFree.Load(root).Models.Count);
            Assert.False(OpenRouterFree.Stale(root, t.AddHours(23))); Assert.True(OpenRouterFree.Stale(root, t.AddHours(25)));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "X-05: Zähler „ins Internet“ – zählt nur den heutigen Tag, am nächsten Tag wieder bei 0, keine Inhalte gespeichert")]
    public void Remote_counter()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-zaehler-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            var c = new RemoteKiCounter(root); var d1 = new DateTime(2026, 11, 5, 10, 0, 0);
            Assert.Equal(0, c.Today(d1)); Assert.Equal(1, c.Add(d1)); Assert.Equal(2, c.Add(d1.AddHours(3)));
            Assert.Equal(0, c.Today(d1.AddDays(1))); Assert.Equal(1, c.Add(d1.AddDays(1)));
            Assert.DoesNotContain("Frage", File.ReadAllText(Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).Single()));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "X-04: Erweiterungs-Katalog – nur GitHub über https oder lokale ZIP, Prüfsumme mit 64 Hexzeichen Pflicht, unbekannte Art abgelehnt; Weiterleitungen nur zu GitHub auf 443")]
    public void Extension_catalog()
    {
        var sha = new string('a', 64);
        var json = "[" + string.Join(",",
            $$"""{"id":"ok","name":"Gut","version":"1.0","kind":"text","url":"https://github.com/x/y/releases/download/v1/gut.zip","sha256":"{{sha}}"}""",
            $$"""{"id":"fremd","name":"Fremd","version":"1.0","kind":"text","url":"https://example.org/x.zip","sha256":"{{sha}}"}""",
            $$"""{"id":"kurz","name":"Kurz","version":"1.0","kind":"text","url":"https://github.com/x/y/a.zip","sha256":"abc"}""",
            $$"""{"id":"art","name":"Art","version":"1.0","kind":"programm","url":"https://github.com/x/y/a.zip","sha256":"{{sha}}"}""") + "]";
        var (ok, err) = ExtensionCatalog.Parse(json);
        Assert.Equal(["ok"], ok.Select(e => e.Id)); Assert.Equal(3, err.Count);
        Assert.Contains(err, e => e.StartsWith("fremd") && e.Contains("nicht erlaubt")); Assert.Contains(err, e => e.StartsWith("kurz") && e.Contains("Prüfsumme")); Assert.Contains(err, e => e.StartsWith("art") && e.Contains("unbekannte Art"));
        Assert.True(ExtensionCatalog.RedirectAllowed(new Uri("https://objects.githubusercontent.com/x"))); Assert.False(ExtensionCatalog.RedirectAllowed(new Uri("http://github.com/x")));
        Assert.Single(ExtensionCatalog.Parse("kaputt").Errors);
    }

    [Fact(DisplayName = "X-10: Sauberer Nachbau – gleicher Code hat Ähnlichkeit 1, eigener Code mit anderer Struktur niedrig; Vergleich meldet nur Dateien über der Schwelle")]
    public void CleanRoom_similarity()
    {
        const string theirs = "public int Sum(int[] a) { var s = 0; foreach (var x in a) { s += x; } return s; } public int Max(int[] a) { var m = a[0]; foreach (var x in a) { if (x > m) m = x; } return m; }";
        const string mine = "def summe(werte):\n    ergebnis = 0\n    for w in werte:\n        ergebnis = ergebnis + w\n    return ergebnis\n";
        Assert.Equal(1, CleanRoom.Similarity(theirs, theirs), 3);
        Assert.True(CleanRoom.Similarity(theirs, mine) < 0.1);
        var root = Path.Combine(Path.GetTempPath(), "trendo-nachbau-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            var o = Path.Combine(root, "original"); var m = Path.Combine(root, "unser"); Directory.CreateDirectory(o); Directory.CreateDirectory(m);
            File.WriteAllText(Path.Combine(o, "a.cs"), theirs); File.WriteAllText(Path.Combine(m, "kopie.cs"), theirs); File.WriteAllText(Path.Combine(m, "eigen.py"), mine);
            var hits = CleanRoom.Compare(m, o);
            Assert.Single(hits); Assert.Contains("kopie.cs", hits[0].ToString());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "CORE-11: Sicherung – höchstens „keep“ Sicherungen, Vorschau zählt ersetzte und neue Dateien, Wiederherstellen holt den alten Stand zurück")]
    public void Backup_roundtrip()
    {
        using var f = new Fixture(); var data = f.Core.DataRoot; var dir = Path.Combine(data, "backups-test");
        var b = new Backup(data, dir, f.Core.Audit);
        var note = Path.Combine(data, "notiz.txt"); File.WriteAllText(note, "alt");
        var zip = b.Create(new DateTime(2026, 11, 5, 9, 0, 0), keep: 2);
        for (var i = 1; i <= 3; i++) b.Create(new DateTime(2026, 11, 5, 9, i, 0), keep: 2);
        Assert.Equal(2, b.List().Count);
        var keepZip = b.List().Last().FullName;
        File.WriteAllText(note, "neu");
        var plan = b.Preview(keepZip); Assert.True(plan.Replaced >= 1);
        b.Restore(keepZip, new DateTime(2026, 11, 5, 10, 0, 0));
        Assert.Equal("alt", File.ReadAllText(note));
        Assert.Contains(b.Entries(keepZip), e => e.Path.EndsWith("notiz.txt"));
        _ = zip;
    }

    [Fact(DisplayName = "CORE-01: Projekte – lesbarer Name wird Ordnername (Umlaute, belegte Namen mit Nummer), Dateien und Ordner anlegen, Notiz, Umbenennen behält den Ordner, nichts außerhalb des Projekts")]
    public void Project_folder()
    {
        using var f = new Fixture(); var p = f.Core.Projects;
        Assert.Equal("kuechen-umbau", ProjectStore.Slug("Küchen-Umbau"));
        var a = p.CreateNamed("Küchen Umbau"); var b = p.CreateNamed("Küchen Umbau");
        Assert.Equal("kuechen-umbau", a.Id); Assert.Equal("kuechen-umbau-2", b.Id);
        var src = Path.Combine(f.Core.DataRoot, "angebot.txt"); File.WriteAllText(src, "x");
        Assert.Equal("angebot.txt", p.AddFile(a.Id, src)); Assert.Equal("angebot (2).txt", p.AddFile(a.Id, src));
        p.NewFolder(a.Id, "Fotos"); Assert.Throws<InvalidOperationException>(() => p.NewFolder(a.Id, "Fotos"));
        p.SetNote(a.Id, "Fliesen bestellen"); Assert.Equal("Fliesen bestellen", p.Note(a.Id));
        Assert.Contains(p.Files(a.Id), x => x.IsDir && x.Name == "Fotos");
        var r = p.Rename(a.Id, "Neue Küche"); Assert.Equal("kuechen-umbau", r.Id);
        Assert.ThrowsAny<Exception>(() => p.NewFolder(a.Id, "../draussen"));
    }
    [Fact(DisplayName = "CORE-11: Sicherung fällig – ohne Sicherung sofort, sonst erst nach dem Abstand; 0 Stunden heißt nie automatisch")]
    public void Backup_due()
    {
        using var f = new Fixture(); var b = new Backup(f.Core.DataRoot, Path.Combine(f.Core.DataRoot, "backups-due"), f.Core.Audit);
        var t = new DateTime(2026, 11, 5, 9, 0, 0);
        Assert.True(b.Due(t, 24)); Assert.False(b.Due(t, 0));
        var zip = b.Create(t); File.SetLastWriteTime(zip, t);
        Assert.False(b.Due(t.AddHours(23), 24)); Assert.True(b.Due(t.AddHours(25), 24));
    }

    [Fact(DisplayName = "CORE-11: Sicherung enthält keine fremden Ordner – nur der Trendo-Datenordner, eine kaputte ZIP lässt die Daten unverändert")]
    public void Backup_scope()
    {
        using var f = new Fixture(); var data = f.Core.DataRoot; var b = new Backup(data, Path.Combine(data, "backups-scope"), f.Core.Audit);
        File.WriteAllText(Path.Combine(data, "eigen.txt"), "bleibt");
        var zip = b.Create(new DateTime(2026, 11, 5, 9, 0, 0));
        Assert.All(b.Entries(zip), e => Assert.False(Path.IsPathRooted(e.Path) || e.Path.Contains("..")));
        Assert.DoesNotContain(b.Entries(zip), e => e.Path.Contains("backups-scope"));
        var broken = Path.Combine(data, "kaputt.zip"); File.WriteAllText(broken, "keine zip");
        Assert.ThrowsAny<Exception>(() => b.Restore(broken, new DateTime(2026, 11, 5, 10, 0, 0)));
        Assert.Equal("bleibt", File.ReadAllText(Path.Combine(data, "eigen.txt")));
    }
}
