using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>T-0764 (6./7. Prüfrunde E): Trendos Blender-Skripte liegen für den Lauf in beschreibbaren Ordnern (Temp, Zwischenspeicher). Blender bekommt
/// deshalb keinen Skriptpfad („--python &lt;Datei&gt;“), sondern einen kurzen Startausdruck („--python-expr“): Blender liest die Datei selbst, vergleicht die
/// SHA-256 mit dem Inhalt, den Trendo geschrieben hat, und führt genau die gelesenen Bytes aus. Ein Austausch zwischen Prüfen und Starten ist so nicht
/// möglich; eine veränderte Datei bricht mit „TRENDO-SKRIPT-VERAENDERT“ ab. Mitgeladene Module (z. B. trendo_moebel für trendo_foto) werden ebenso
/// geprüft und vorab in <c>sys.modules</c> eingetragen, damit ein späteres <c>import</c> nicht die Datei vom Datenträger liest.</summary>
public static class BlenderScript
{
    public static string Sha(string content) => Sha(new UTF8Encoding(false).GetBytes(content));
    public static string Sha(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    /// <summary>Skript schreiben (UTF-8 ohne BOM) und die Blender-Argumente dafür liefern: <c>["--python-expr", Ausdruck]</c>.</summary>
    public static List<string> Args(string path, string content)
    {
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return ["--python-expr", Expr(path, Sha(content))];
    }

    /// <summary>Startausdruck: Hauptskript <paramref name="path"/> mit erwarteter <paramref name="sha256"/>; optional Module (Name, Pfad, SHA-256).
    /// Pfade werden als JSON-Zeichenkette eingesetzt (auch ein gültiges Python-Literal, Anführungszeichen maskiert).</summary>
    public static string Expr(string path, string sha256, IEnumerable<(string Module, string Path, string Sha256)>? modules = null)
    {
        static string Hex(string s) => s.Length == 64 && s.All(Uri.IsHexDigit) ? s.ToLowerInvariant() : throw new ArgumentException("SHA-256 erwartet.", nameof(sha256));
        static string Lit(string p) => JsonSerializer.Serialize(System.IO.Path.GetFullPath(p));
        var sb = new StringBuilder("import hashlib,sys,types\n");
        sb.Append("def _t(p,h):\n d=open(p,'rb').read()\n if hashlib.sha256(d).hexdigest()!=h: sys.exit('TRENDO-SKRIPT-VERAENDERT')\n return d\n");
        foreach (var (m, p, s) in modules ?? [])
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(m, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("Modulname ungültig.", nameof(modules));
            sb.Append($"_m=types.ModuleType('{m}');_m.__file__={Lit(p)};exec(compile(_t({Lit(p)},'{Hex(s)}'),{Lit(p)},'exec'),_m.__dict__);sys.modules['{m}']=_m\n");
        }
        sb.Append($"_p={Lit(path)}\nexec(compile(_t(_p,'{Hex(sha256)}'),_p,'exec'),{{'__name__':'__main__','__file__':_p}})\n");
        return sb.ToString();
    }
}
