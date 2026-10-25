using PdfSharp.Fonts;

namespace Trendo.Core.Sys;

public static partial class PdfTools
{
    /// <summary>PDFsharp braucht Schriften für Formularfelder: unter Windows die Systemschriften, unter Linux (USB/Pi) DejaVu oder Liberation aus /usr/share/fonts.</summary>
    internal static class Fonts
    {
        static readonly object Gate = new();
        public static void Ensure()
        {
            lock (Gate)
            {
                if (GlobalFontSettings.FontResolver is not null) return;
                if (OperatingSystem.IsWindows()) GlobalFontSettings.UseWindowsFontsUnderWindows = true;
                else GlobalFontSettings.FontResolver = new LinuxFonts();
            }
        }
    }

    sealed class LinuxFonts : IFontResolver
    {
        static readonly string[] Candidates = ["/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf", "/usr/share/fonts/TTF/DejaVuSans.ttf"];
        public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) => new("trendo-sans");
        public byte[]? GetFont(string faceName) => Candidates.FirstOrDefault(File.Exists) is { } f ? File.ReadAllBytes(f) : null;
    }
}
