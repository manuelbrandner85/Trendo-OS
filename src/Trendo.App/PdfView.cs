using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;
using SkiaSharp;

// PDFtoImage nennt nur Windows, Linux, macOS, Android, iOS und Browser – Trendo läuft ausschließlich auf Windows und Linux.
#pragma warning disable CA1416

namespace Trendo.App;

/// <summary>T-0223 Trendo PDF (P1/P2): Seiten zeichnen über PDFtoImage 5.2.1 (MIT, PDFium), Text und Lesezeichen direkt über PDFium.
/// PDFium ist nicht threadsicher – jeder Zugriff läuft über <see cref="Gate"/>.</summary>
public static class PdfView
{
    public static readonly object Gate = new();
    public sealed record Bookmark(string Title, int Page, int Level);
    public sealed record Hit(int Page, string Snippet);

    public static int PageCount(string path, string? password = null) { lock (Gate) return PDFtoImage.Conversion.GetPageCount(File.ReadAllBytes(path), password); }

    /// <summary>Seitengrößen in Punkten.</summary>
    public static IReadOnlyList<(double W, double H)> Sizes(string path, string? password = null)
    {
        lock (Gate) return PDFtoImage.Conversion.GetPageSizes(File.ReadAllBytes(path), password).Select(s => ((double)s.Width, (double)s.Height)).ToList();
    }

    /// <summary>Eine Seite als Bild; dpi 72 = 1 Bildpunkt je PDF-Punkt. Anmerkungen und Formularwerte werden mitgezeichnet.</summary>
    public static Bitmap Render(string path, int page, int dpi, int rotation = 0, string? password = null) => Render(path, page, dpi, rotation, password, out _, out _);

    public static Bitmap Render(string path, int page, int dpi, int rotation, string? password, out int width, out int height)
    {
        using var bmp = RenderSk(path, page, dpi, rotation, password);
        width = bmp.Width; height = bmp.Height;
        using var data = bmp.Encode(SKEncodedImageFormat.Png, 90);
        using var ms = new MemoryStream(data.ToArray());
        return new Bitmap(ms);
    }

    /// <summary>Seite als SkiaSharp-Bild (Bildpunkte), z. B. zum Prüfen, ob wirklich etwas gezeichnet wurde.</summary>
    public static SKBitmap RenderSk(string path, int page, int dpi, int rotation = 0, string? password = null)
    {
        var rot = (((rotation % 360) + 360) % 360) switch { 90 => PDFtoImage.PdfRotation.Rotate90, 180 => PDFtoImage.PdfRotation.Rotate180, 270 => PDFtoImage.PdfRotation.Rotate270, _ => PDFtoImage.PdfRotation.Rotate0 };
        lock (Gate) return PDFtoImage.Conversion.ToImage(File.ReadAllBytes(path), page, password, new PDFtoImage.RenderOptions(Dpi: dpi, WithAnnotations: true, WithFormFill: true, Rotation: rot, BackgroundColor: SKColors.White));
    }

    // ---------- PDFium: Text und Lesezeichen ----------
    const string Lib = "pdfium";
    [DllImport(Lib)] static extern void FPDF_InitLibrary();
    [DllImport(Lib)] static extern IntPtr FPDF_LoadMemDocument(IntPtr data, int size, [MarshalAs(UnmanagedType.LPStr)] string? password);
    [DllImport(Lib)] static extern void FPDF_CloseDocument(IntPtr doc);
    [DllImport(Lib)] static extern int FPDF_GetPageCount(IntPtr doc);
    [DllImport(Lib)] static extern IntPtr FPDF_LoadPage(IntPtr doc, int index);
    [DllImport(Lib)] static extern void FPDF_ClosePage(IntPtr page);
    [DllImport(Lib)] static extern IntPtr FPDFText_LoadPage(IntPtr page);
    [DllImport(Lib)] static extern void FPDFText_ClosePage(IntPtr text);
    [DllImport(Lib)] static extern int FPDFText_CountChars(IntPtr text);
    [DllImport(Lib)] static extern int FPDFText_GetText(IntPtr text, int start, int count, [Out] ushort[] buffer);
    [DllImport(Lib)] static extern IntPtr FPDFBookmark_GetFirstChild(IntPtr doc, IntPtr bookmark);
    [DllImport(Lib)] static extern IntPtr FPDFBookmark_GetNextSibling(IntPtr doc, IntPtr bookmark);
    [DllImport(Lib)] static extern uint FPDFBookmark_GetTitle(IntPtr bookmark, [Out] byte[]? buffer, uint length);
    [DllImport(Lib)] static extern IntPtr FPDFBookmark_GetDest(IntPtr doc, IntPtr bookmark);
    [DllImport(Lib)] static extern int FPDFDest_GetDestPageIndex(IntPtr doc, IntPtr dest);

    static bool _init;

    static T WithDoc<T>(string path, string? password, Func<IntPtr, T> work)
    {
        lock (Gate)
        {
            if (!_init) { FPDF_InitLibrary(); _init = true; } // zweiter Aufruf durch PDFtoImage ist in PDFium wirkungslos
            var bytes = File.ReadAllBytes(path);
            var h = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var doc = FPDF_LoadMemDocument(h.AddrOfPinnedObject(), bytes.Length, password);
                if (doc == IntPtr.Zero) throw new InvalidOperationException("Die PDF lässt sich nicht öffnen (beschädigt oder Passwort falsch).");
                try { return work(doc); } finally { FPDF_CloseDocument(doc); }
            }
            finally { h.Free(); }
        }
    }

    static string PageText(IntPtr doc, int i)
    {
        var page = FPDF_LoadPage(doc, i); if (page == IntPtr.Zero) return "";
        try
        {
            var tp = FPDFText_LoadPage(page); if (tp == IntPtr.Zero) return "";
            try
            {
                var n = FPDFText_CountChars(tp); if (n <= 0) return "";
                var buf = new ushort[n + 1];
                var got = FPDFText_GetText(tp, 0, n, buf);
                var chars = new char[Math.Max(0, got - 1)];
                for (var k = 0; k < chars.Length; k++) chars[k] = (char)buf[k];
                return new string(chars);
            }
            finally { FPDFText_ClosePage(tp); }
        }
        finally { FPDF_ClosePage(page); }
    }

    /// <summary>Text einer Seite (zum Kopieren).</summary>
    public static string Text(string path, int page, string? password = null) => WithDoc(path, password, d => PageText(d, page));

    /// <summary>Suchen im ganzen Dokument, ohne Groß-/Kleinschreibung; je Fund die Seite und ein kurzer Ausschnitt.</summary>
    public static List<Hit> Search(string path, string query, string? password = null, int max = 200) => WithDoc(path, password, d =>
    {
        var hits = new List<Hit>();
        if (string.IsNullOrWhiteSpace(query)) return hits;
        for (var i = 0; i < FPDF_GetPageCount(d) && hits.Count < max; i++)
        {
            var t = PageText(d, i).Replace("\r", " ").Replace("\n", " ");
            for (var at = t.IndexOf(query, StringComparison.CurrentCultureIgnoreCase); at >= 0 && hits.Count < max; at = t.IndexOf(query, at + query.Length, StringComparison.CurrentCultureIgnoreCase))
            {
                var s = Math.Max(0, at - 30); var e = Math.Min(t.Length, at + query.Length + 30);
                hits.Add(new Hit(i, (s > 0 ? "…" : "") + t[s..e].Trim() + (e < t.Length ? "…" : "")));
            }
        }
        return hits;
    });

    /// <summary>Lesezeichen (Inhaltsverzeichnis) mit Zielseite und Ebene.</summary>
    public static List<Bookmark> Bookmarks(string path, string? password = null) => WithDoc(path, password, d =>
    {
        var list = new List<Bookmark>();
        void Walk(IntPtr parent, int level)
        {
            for (var b = FPDFBookmark_GetFirstChild(d, parent); b != IntPtr.Zero && list.Count < 2000; b = FPDFBookmark_GetNextSibling(d, b))
            {
                var len = FPDFBookmark_GetTitle(b, null, 0);
                var buf = new byte[len];
                if (len > 0) FPDFBookmark_GetTitle(b, buf, len);
                var title = System.Text.Encoding.Unicode.GetString(buf).TrimEnd('\0');
                var dest = FPDFBookmark_GetDest(d, b);
                list.Add(new Bookmark(title, dest == IntPtr.Zero ? -1 : FPDFDest_GetDestPageIndex(d, dest), level));
                if (level < 6) Walk(b, level + 1);
            }
        }
        Walk(IntPtr.Zero, 0);
        return list;
    });
}
