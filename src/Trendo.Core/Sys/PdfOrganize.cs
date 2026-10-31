using PdfSharp.Pdf;

namespace Trendo.Core.Sys;

/// <summary>Eine Seite in der neuen Reihenfolge: aus welcher Datei, welche Seite (−1 = leere Seite), zusätzlich gedreht um 0/90/180/270 Grad.</summary>
public sealed record PageOp(string File, int Page, int Rotate = 0);

public static partial class PdfTools
{
    /// <summary>Seiten ordnen (Masterprompt 131): neue PDF aus einer frei geordneten Seitenliste – Seiten aus mehreren Dateien, je Seite gedreht,
    /// leere Seiten in der Größe der Seite davor (sonst A4). Die Vorlagen bleiben unverändert. Gibt die Seitenzahl zurück.</summary>
    public static int Rearrange(IReadOnlyList<PageOp> ops, string output, string? password = null, string? passwordFile = null, AuditLog? audit = null)
    {
        if (ops.Count == 0) throw new ArgumentException("Es muss mindestens eine Seite übrig bleiben.");
        if (ops.Any(o => o.Rotate % 90 != 0)) throw new ArgumentException("Drehen geht nur in Vierteln: 90, 180 oder 270 Grad.");
        var files = ops.Where(o => o.Page >= 0).Select(o => o.File).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        Guard(output, files);
        var src = new Dictionary<string, PdfDocument>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var f in files) src[f] = OpenImport(f, string.Equals(f, passwordFile, StringComparison.OrdinalIgnoreCase) ? password : null);
            using var target = new PdfDocument();
            foreach (var o in ops)
            {
                PdfPage page;
                if (o.Page < 0)
                {
                    page = target.AddPage();
                    if (target.PageCount > 1) { var prev = target.Pages[target.PageCount - 2]; page.Width = prev.Width; page.Height = prev.Height; } else page.Size = PdfSharp.PageSize.A4;
                }
                else
                {
                    var d = src[o.File]; if (o.Page >= d.PageCount) throw new ArgumentException($"Seite {o.Page + 1} gibt es in {Path.GetFileName(o.File)} nicht.");
                    page = target.AddPage(d.Pages[o.Page]);
                }
                page.Rotate = (((page.Rotate + o.Rotate) % 360) + 360) % 360;
            }
            var n = target.PageCount;
            Save(target, output);
            audit?.Append("app", "pdf.organize", $"{string.Join(", ", files.Select(Path.GetFileName))} → {Path.GetFileName(output)} ({n} Seiten)");
            return n;
        }
        finally { foreach (var d in src.Values) d.Dispose(); }
    }

    /// <summary>Gedrehter Winkel einer Seite (0/90/180/270) – für Miniaturen und Tests.</summary>
    public static int PageRotation(string path, int page, string? password = null) { using var d = OpenImport(path, password); return ((d.Pages[page].Rotate % 360) + 360) % 360; }
}
