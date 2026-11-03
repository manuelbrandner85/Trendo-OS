using PdfSharp.Fonts;

namespace Trendo.Core.Sys;

public static partial class PdfTools
{
    /// <summary>Schriften für PDFsharp einrichten (auch für andere PDF-Ersteller wie den Brief).</summary>
    public static void EnsureFonts() => Fonts.Ensure();

    /// <summary>PDFsharp braucht Schriften für Formularfelder: unter Windows die Systemschriften, unter Linux (USB/Pi) DejaVu oder Liberation aus /usr/share/fonts.</summary>
    internal static class Fonts
    {
        static readonly object Gate = new();
        public static void Ensure()
        {
            lock (Gate)
            {
                if (GlobalFontSettings.FontResolver is not null) return;
                if (OperatingSystem.IsWindows()) { GlobalFontSettings.UseWindowsFontsUnderWindows = true; GlobalFontSettings.FontResolver = new WindowsFonts(); }
                else GlobalFontSettings.FontResolver = new LinuxFonts();
            }
        }
    }

    /// <summary>Windows-Schriften direkt aus den Schriftdateien (Wunsch 03.11.2026: Brief-Designs mit Calibri, Georgia, Segoe UI …).
    /// PDFsharp selbst kennt unter Windows nur wenige Familien. Unbekannte Familien: Datei nach dem Namen suchen, sonst Arial – nie „keine Schrift“.</summary>
    sealed class WindowsFonts : IFontResolver
    {
        // Familie → Dateien (normal, fett, kursiv, fett-kursiv)
        static readonly Dictionary<string, string[]> Files = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Arial"] = ["arial", "arialbd", "ariali", "arialbi"], ["Calibri"] = ["calibri", "calibrib", "calibrii", "calibriz"],
            ["Candara"] = ["candara", "candarab", "candarai", "candaraz"], ["Constantia"] = ["constan", "constanb", "constani", "constanz"],
            ["Corbel"] = ["corbel", "corbelb", "corbeli", "corbelz"], ["Segoe UI"] = ["segoeui", "segoeuib", "segoeuii", "segoeuiz"],
            ["Georgia"] = ["georgia", "georgiab", "georgiai", "georgiaz"], ["Garamond"] = ["gara", "garabd", "garait", "garabd"],
            ["Verdana"] = ["verdana", "verdanab", "verdanai", "verdanaz"], ["Trebuchet MS"] = ["trebuc", "trebucbd", "trebucit", "trebucbi"],
            ["Tahoma"] = ["tahoma", "tahomabd", "tahoma", "tahomabd"], ["Cambria"] = ["cambria", "cambriab", "cambriai", "cambriaz"],
            ["Courier New"] = ["cour", "courbd", "couri", "courbi"], ["Times New Roman"] = ["times", "timesbd", "timesi", "timesbi"],
            ["Consolas"] = ["consola", "consolab", "consolai", "consolaz"], ["Comic Sans MS"] = ["comic", "comicbd", "comici", "comicz"],
            ["Impact"] = ["impact", "impact", "impact", "impact"], ["Lucida Console"] = ["lucon", "lucon", "lucon", "lucon"],
            ["Segoe Print"] = ["segoepr", "segoeprb", "segoepr", "segoeprb"], ["Segoe Script"] = ["segoesc", "segoescb", "segoesc", "segoescb"],
            ["Sitka"] = ["sitka", "sitkab", "sitkai", "sitkaz"], ["Bahnschrift"] = ["bahnschrift", "bahnschrift", "bahnschrift", "bahnschrift"],
        };
        static IEnumerable<string> Dirs() => [Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts")];
        static string? Find(string stem) => Dirs().Select(d => Path.Combine(d, stem + ".ttf")).FirstOrDefault(File.Exists);

        public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
        {
            var want = (bold ? 1 : 0) + (italic ? 2 : 0);
            if (Files.TryGetValue(familyName.Trim(), out var f))
            {
                if (Find(f[want]) is not null) return new FontResolverInfo(f[want]);
                if (Find(f[0]) is not null) return new FontResolverInfo(f[0], bold, italic); // Schnitt fehlt: nachbilden
            }
            // unbekannte Familie: Datei mit dem Namen (ohne Leerzeichen) – sonst Arial
            var stem = new string(familyName.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            if (stem.Length > 0 && Find(stem) is not null) return new FontResolverInfo(stem, bold, italic);
            var arial = Files["Arial"];
            if (Find(arial[want]) is not null) return new FontResolverInfo(arial[want]);
            return Find(arial[0]) is not null ? new FontResolverInfo(arial[0], bold, italic) : null;
        }
        public byte[]? GetFont(string faceName) => Find(faceName) is { } file ? File.ReadAllBytes(file) : null;
    }

    sealed class LinuxFonts : IFontResolver
    {
        static readonly string[] Candidates = ["/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf", "/usr/share/fonts/TTF/DejaVuSans.ttf"];
        public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) => new("trendo-sans");
        public byte[]? GetFont(string faceName) => Candidates.FirstOrDefault(File.Exists) is { } f ? File.ReadAllBytes(f) : null;
    }
}
