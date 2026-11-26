using System.Security;
using System.Text;

namespace Trendo.Core.Office;

/// <summary>T-0780 (Gegenprüfung M2): gemeinsamer Helfer vor jeder XML-Ausgabe (DOCX, ODT, PPTX, ODP, SVG). Entfernt Zeichen, die in XML 1.0 nicht vorkommen dürfen
/// (Steuerzeichen außer Tab/Zeilenumbruch, einzelne Ersatzzeichen-Hälften, U+FFFE/U+FFFF) – sonst schreibt Trendo Dateien, die Word, LibreOffice oder Inkscape ablehnen.</summary>
public static class XmlSafe
{
    public static bool Allowed(char c) => c is '\t' or '\n' or '\r' || c >= 0x20 && c <= 0xD7FF || c >= 0xE000 && c <= 0xFFFD;

    public static string Clean(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        StringBuilder? sb = null;
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            var ok = Allowed(c);
            if (!ok && char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                sb?.Append(c).Append(s[i + 1]); i++; continue; // gültiges Paar
            }
            if (ok) { sb?.Append(c); continue; }
            sb ??= new StringBuilder(s, 0, i, s.Length);
        }
        return sb?.ToString() ?? s;
    }

    /// <summary>Bereinigen und für XML-Text/Attribute maskieren.</summary>
    public static string Escape(string? s) => SecurityElement.Escape(Clean(s)) ?? "";
}
