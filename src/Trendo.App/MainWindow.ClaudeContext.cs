using Trendo.Core.Office;

namespace Trendo.App;

/// <summary>Was du im Studio gerade offen und markiert hast – geht als kurzer Hinweis mit jeder Nachricht an Claude (Wunsch 01.11.2026: „Mach das rot“
/// funktioniert ohne Erklären). Nur Titel, Position und ein kurzer Ausschnitt, keine ganzen Dokumente.</summary>
public partial class MainWindow
{
    /// <summary>Kurze Beschreibung des Studio-Zustands für <paramref name="page"/> (öffentlich für Tests), null wenn es nichts Sinnvolles gibt.</summary>
    public string? ClaudeStudioContext(string page)
    {
        static string Cut(string s, int n = 160) { s = s.Replace('\n', ' ').Trim(); return s.Length > n ? s[..n].TrimEnd() + " …" : s; }
        switch (page)
        {
            case "text":
            {
                var name = TextPath is { } p ? Path.GetFileName(p) : "neues Dokument";
                var blocks = TextDocument.Blocks;
                if (blocks.Count == 0) return $"Trendo Text: {name}, leer.";
                var i = Math.Clamp(TextFocusBlock, 0, blocks.Count - 1);
                var para = blocks[i] as Para;
                var sel = para is not null && _txSelLen > 0 && _txSel >= 0 && _txSel + _txSelLen <= para.Text.Length ? para.Text.Substring(_txSel, _txSelLen) : null;
                return $"Trendo Text: {name}, {blocks.Count} Absätze; Cursor im Absatz {i + 1}" + (para is { Text.Length: > 0 } ? $": „{Cut(para.Text)}“" : "") + (sel is { Length: > 0 } ? $"; markiert: „{Cut(sel, 120)}“" : "") + ".";
            }
            case "tabelle":
            {
                var name = SheetPath is { } p ? Path.GetFileName(p) : "neue Tabelle";
                var (r, c) = _shActive; var (ar, ac) = _shAnchor;
                var cell = ActiveSheet.Get(r, c)?.Raw ?? "";
                var range = (r, c) != (ar, ac) ? $"{Addr.Name(Math.Min(r, ar), Math.Min(c, ac))}:{Addr.Name(Math.Max(r, ar), Math.Max(c, ac))}" : null;
                return $"Trendo Tabelle: {name}, Blatt „{ActiveSheet.Name}“; ausgewählt {Addr.Name(r, c)}" + (cell.Length > 0 ? $" = „{Cut(cell, 80)}“" : " (leer)") + (range is not null ? $", Bereich {range}" : "") + ".";
            }
            case "praesentation":
            {
                if (Deck.Slides.Count == 0) return "Trendo Präsentation: noch keine Folien.";
                var i = Math.Clamp(DeckIndex, 0, Deck.Slides.Count - 1);
                return $"Trendo Präsentation: {(DeckPath is { } p ? Path.GetFileName(p) : "neue Präsentation")}, Folie {i + 1} von {Deck.Slides.Count}" + (Deck.Slides[i].Title is { Length: > 0 } t ? $": „{Cut(t, 80)}“" : "") + ".";
            }
        }
        if (Programs.TryGetValue(page, out var spec) && ProgramActive(page) is { } doc)
            return $"{spec.Title}: Dokument „{doc.Title}“{(doc.Path is { } dp ? $" ({Path.GetFileName(dp)})" : " (noch nicht gespeichert)")}{(doc.Dirty ? ", ungespeicherte Änderungen" : "")}.";
        return null;
    }
}
