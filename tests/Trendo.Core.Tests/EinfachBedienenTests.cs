using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0616 „Einfacher bedienen“: Fehler in Alltagssprache (E7), Alltagswörter (E10), Versionen beim automatischen Speichern (E3).</summary>
public class EinfachBedienenTests
{
    [Fact(DisplayName = "E7: Systemfehler werden zu Alltagssätzen mit passendem Knopf, eigene deutsche Meldungen bleiben, Technik bleibt erhalten")]
    public void Friendly_errors()
    {
        var a = FriendlyError.From(new FileNotFoundException("Could not find file 'C:\\x\\bild.png'.", "C:\\x\\bild.png"));
        Assert.Equal(("fehlt", ErrorFix.PickFolder), (a.Kind, a.Fix)); Assert.Contains("„bild.png“", a.Sentence); Assert.StartsWith("FileNotFoundException: Could not find", a.Technical);
        var own = FriendlyError.From(new FileNotFoundException("Diese Datei gibt es nicht."));
        Assert.Equal("Diese Datei gibt es nicht.", own.Sentence);
        var acc = FriendlyError.From(new UnauthorizedAccessException("Access to the path 'C:\\Windows\\a.txt' is denied."));
        Assert.Equal(("zugriff", ErrorFix.PickFolder), (acc.Kind, acc.Fix)); Assert.DoesNotContain("denied", acc.Sentence);
        Assert.Equal("Anderen Ort wählen", acc.FixLabel);
        var busy = FriendlyError.From(new IOException("The process cannot access the file because it is being used by another process.", unchecked((int)0x80070020)));
        Assert.Equal(("belegt", ErrorFix.Retry), (busy.Kind, busy.Fix));
        var full = FriendlyError.From(new IOException("There is not enough space on the disk.", unchecked((int)0x80070070)));
        Assert.Equal("voll", full.Kind);
        Assert.Equal(("zeit", ErrorFix.Retry), (FriendlyError.From(new TimeoutException("The operation has timed out.")).Kind, FriendlyError.From(new TimeoutException()).Fix));
        Assert.Equal("netz", FriendlyError.From(new HttpRequestException("No such host is known.")).Kind); // nur über den Typnamen (QA-019)
        var prog = FriendlyError.From(new System.ComponentModel.Win32Exception(2, "The system cannot find the file specified."));
        Assert.Equal(("programm", ErrorFix.Setup, "Einrichten"), (prog.Kind, prog.Fix, prog.FixLabel));
        Assert.Equal(("format", ErrorFix.AskClaude), (FriendlyError.From(new InvalidDataException()).Kind, FriendlyError.From(new FormatException("Input string was not in a correct format.")).Fix));
        Assert.Equal(("abbruch", ErrorFix.None), (FriendlyError.From(new OperationCanceledException()).Kind, FriendlyError.From(new OperationCanceledException()).Fix));
        // eigene Meldung: kein Knopf nötig, Satz unverändert
        var mine = FriendlyError.From(new InvalidOperationException("Es gibt nichts rückgängig zu machen."));
        Assert.Equal(("Es gibt nichts rückgängig zu machen.", ErrorFix.None), (mine.Sentence, mine.Fix));
        var unknown = FriendlyError.From(new InvalidOperationException("Sequence contains no elements"));
        Assert.Equal(ErrorFix.AskClaude, unknown.Fix); Assert.DoesNotContain("Sequence", unknown.Sentence); Assert.Contains("Sequence", unknown.Technical);
        // verpackt: die eigentliche Ursache zählt
        Assert.Equal("zugriff", FriendlyError.From(new AggregateException(new UnauthorizedAccessException("Access denied"))).Kind);
        Assert.Equal("", FriendlyError.LabelOf(ErrorFix.None));
        // Befunde der Durchsicht: Dienst lehnt ab ≠ kein Netz; eigenes Abbrechen ≠ Zeitablauf; eigener Satz mit angehängter Systemmeldung bleibt eigen
        Assert.Equal(("dienst", ErrorFix.Setup), (FriendlyError.From(new HttpRequestException("Response status code does not indicate success: 401 (Unauthorized).")).Kind, FriendlyError.From(new HttpRequestException("status code 429")).Fix));
        Assert.Equal("abbruch", FriendlyError.From(new TaskCanceledException()).Kind);
        Assert.Equal("zeit", FriendlyError.From(new TaskCanceledException("x", new TimeoutException())).Kind);
        var mixed = FriendlyError.From(new InvalidDataException("Die Projektdaten sind kein gültiges JSON: 'x' is an invalid start of a value."));
        Assert.Equal("Die Projektdaten sind kein gültiges JSON.", mixed.Sentence); Assert.Contains("invalid start", mixed.Technical);
        Assert.Equal("Bitte einen Namen eingeben.", FriendlyError.From(new ArgumentException("Bitte einen Namen eingeben.", "name")).Sentence); // ohne „(Parameter 'name')“
        Assert.NotEqual("Object reference not set to an instance of an object.", FriendlyError.From(new NullReferenceException()).Sentence);
        Assert.False(FriendlyError.IsOwnMessage("Attempted to divide by zero."));
        Assert.False(FriendlyError.IsOwnMessage("")); Assert.False(FriendlyError.IsOwnMessage("Fehler 0x80004005")); Assert.True(FriendlyError.IsOwnMessage("Bitte einen Speicherort wählen."));
    }

    sealed class HttpRequestException(string m) : Exception(m); // gleicher Typname wie die Netz-Ausnahme, ohne Netz-Bibliothek

    [Fact(DisplayName = "E7: eigene Meldungen ohne typische Füllwörter und mit kurzem Kopf („Routing-Kreis: …“) bleiben erhalten, englische werden übersetzt")]
    public void Own_messages_survive()
    {
        Assert.Equal("Zum Trainieren braucht es Beispiele aus mindestens zwei Kategorien.", FriendlyError.Say(new InvalidOperationException("Zum Trainieren braucht es Beispiele aus mindestens zwei Kategorien.")));
        var loop = "Routing-Kreis: „Bus A“ würde über „Bus B“ zu sich selbst führen.";
        Assert.Equal(loop, FriendlyError.Say(new InvalidOperationException(loop)));
        Assert.NotEqual("Sequence contains no elements", FriendlyError.Say(new InvalidOperationException("Sequence contains no elements")));
        Assert.NotEqual("Operation is not valid due to the current state of the object.", FriendlyError.Say(new InvalidOperationException("Operation is not valid due to the current state of the object.")));
    }

    [Fact(DisplayName = "E10: Alltagswörter in allen Stufen, Fachwort als Hinweis; Einfach-Wörter der Programme bleiben stufenabhängig")]
    public void Everyday_words()
    {
        Assert.Equal("KI-Zentrale", PlainWords.Plain("Command Center"));
        Assert.Equal("KI-Helfer bei der Arbeit", PlainWords.Plain("Agenten Live"));
        Assert.Equal("Punkte", PlainWords.Plain("Credits"));
        Assert.Equal("Fachwort: KI-Router", PlainWords.Technical("KI-Router"));
        Assert.Null(PlainWords.Technical("Start")); Assert.Equal("Start", PlainWords.Plain("Start"));
        Assert.Equal("Spielbibel", PlainWords.For(UserLevel.Profi, "Spielbibel"));
        Assert.All(PlainWords.Everyday, kv => Assert.NotEqual(kv.Key, kv.Value));
        Assert.Equal(PlainWords.Everyday.Count, PlainWords.Everyday.Values.Distinct().Count()); // jedes Alltagswort steht für genau ein Fachwort
    }

    [Fact(DisplayName = "E3: Automatisches Speichern behält höchstens alle paar Minuten eine Version, normales Speichern immer")]
    public void Versions_while_autosaving()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-einfach-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var f = Path.Combine(dir, "Brief.tdoc");
            Assert.False(ProgramDocument.VersionDue(f, TimeSpan.FromMinutes(10))); // noch keine Datei
            var d = new ProgramDocument("SPEC-08", "Brief"); d.Set("a", 1); d.Save(f);
            Assert.True(ProgramDocument.VersionDue(f, TimeSpan.FromMinutes(10))); // Datei, aber noch keine Version
            d.Set("a", 2); d.Save(keepVersion: false);
            Assert.Empty(ProgramDocument.Versions(f));
            d.Set("a", 3); d.Save();
            Assert.Single(ProgramDocument.Versions(f));
            Assert.False(ProgramDocument.VersionDue(f, TimeSpan.FromMinutes(10)));
            Assert.True(ProgramDocument.VersionDue(f, TimeSpan.FromMinutes(10), DateTime.Now.AddMinutes(11)));
            Assert.Equal(3, ProgramDocument.Load(f).Number("a"));
        }
        finally { Directory.Delete(dir, true); }
    }
}
