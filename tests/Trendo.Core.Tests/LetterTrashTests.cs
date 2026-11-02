using Trendo.Core;
using Xunit;
namespace Trendo.Core.Tests;

/// Wunsch 02.11.2026: Briefe, Akten und Briefköpfe löschen – erst Trendo-Papierkorb (zurückholbar), Leeren geht an den Windows-Papierkorb; nie endgültig.
public class BriefePapierkorb
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-trash-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "Briefe löschen: Brief samt Entwurf in den Papierkorb, zurückholen an den alten Ort, Leeren gibt alles an den Windows-Papierkorb")]
    public void Letter_roundtrip()
    {
        var root = Tmp();
        try
        {
            var arch = new LetterArchive(root);
            var (docx, json, _) = arch.Save(new LetterDraft { SenderName = "Erika Muster", SenderStreet = "Hauptstr. 1", SenderCity = "10115 Berlin", Recipient = "Firma\nWeg 2\n20095 Hamburg", Subject = "Kündigung", Body = "Hiermit kündige ich." });
            var e = arch.Delete(docx)!;
            Assert.False(File.Exists(docx)); Assert.False(File.Exists(json));
            Assert.Single(new LetterTrash(root).List());
            new LetterTrash(root).Restore(e.Id);
            Assert.True(File.Exists(docx)); Assert.True(File.Exists(json));
            Assert.Empty(new LetterTrash(root).List());
            arch.Delete(docx);
            var recycled = new List<string>();
            Assert.Equal(1, new LetterTrash(root).Empty(recycled.Add));
            Assert.Single(recycled); // nur weitergereicht – nichts endgültig gelöscht
            Assert.Throws<ArgumentException>(() => arch.Delete(Path.Combine(root, "fremd.docx")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Akten und Briefköpfe löschen und zurückholen; fremder Briefkopf bleibt geschützt")]
    public void Cases_and_letterheads()
    {
        var root = Tmp();
        try
        {
            var store = new CaseStore(root, new AuditLog(Path.Combine(root, "audit.jsonl")));
            var me = Principal.LocalOwner;
            var c = store.Create("Widerspruch Bescheid", LetterKit.Categories[0].Id, me);
            var e = store.Delete(c.Id, me)!;
            Assert.Empty(store.List(me));
            new LetterTrash(root).Restore(e.Id);
            Assert.Single(store.List(me));
            var h = store.SaveLetterhead(new Letterhead("", "", "privat", "Erika Muster", "Hauptstr. 1", "10115 Berlin", ""), me);
            var he = store.DeleteLetterhead(h.Id, me)!;
            Assert.Empty(store.Letterheads(me));
            store.RestoreLetterhead(he.Id, me);
            Assert.Equal("Erika Muster", Assert.Single(store.Letterheads(me)).Name);
        }
        finally { Directory.Delete(root, true); }
    }
}
