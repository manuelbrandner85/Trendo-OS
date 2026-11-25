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
        var sb = new StringBuilder("import hashlib,sys,types,os,tempfile\n");
        // 8. Prüfrunde (M2): Import-Pfade bereinigen – Skriptordner, aktueller Ordner, Temp und %APPDATA% (Blender-Benutzerskripte) fliegen aus sys.path,
        // bevor irgendein Trendo-Skript läuft; ein „import“ kann so keine hineingelegte Datei aus einem beschreibbaren Ordner laden.
        var dirs = new[] { path }.Concat((modules ?? []).Select(m => m.Path)).Select(p => System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(p))!).Distinct();
        // Blenders eigene Ordner (neben dem Programm) bleiben immer; der aktuelle Ordner fliegt nur als genauer Eintrag (nicht seine Unterordner).
        sb.Append("_n=lambda x:os.path.normcase(os.path.abspath(x))\n");
        sb.Append("try:\n import bpy;_own=_n(os.path.dirname(bpy.app.binary_path))\nexcept Exception:\n _own=_n(os.path.dirname(sys.executable))\n");
        sb.Append($"_scr=[_n(x) for x in [{string.Join(",", dirs.Select(Lit))}]]\n");
        sb.Append("_bad=_scr+[_n(tempfile.gettempdir())]+([_n(os.environ['APPDATA'])] if os.environ.get('APPDATA') else [])\n");
        sb.Append("_in=lambda x,b:x==b or x.startswith(b.rstrip(os.sep)+os.sep)\n");
        sb.Append("sys.path[:]=[x for x in sys.path if x and x!='.' and not any(_in(_n(x),s) for s in _scr) and (_in(_n(x),_own) or (_n(x)!=_n(os.getcwd()) and not any(_in(_n(x),b) for b in _bad)))]\n");
        sb.Append("def _t(p,h):\n d=open(p,'rb').read()\n if hashlib.sha256(d).hexdigest()!=h: sys.exit('TRENDO-SKRIPT-VERAENDERT')\n return d\n");
        foreach (var (m, p, s) in modules ?? [])
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(m, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("Modulname ungültig.", nameof(modules));
            sb.Append($"_m=types.ModuleType('{m}');_m.__file__={Lit(p)};exec(compile(_t({Lit(p)},'{Hex(s)}'),{Lit(p)},'exec'),_m.__dict__);sys.modules['{m}']=_m\n");
        }
        sb.Append($"_p={Lit(path)}\nexec(compile(_t(_p,'{Hex(sha256)}'),_p,'exec'),{{'__name__':'__main__','__file__':_p}})\n");
        // 8. Prüfrunde: als eine Zeile ohne Anführungszeichen, Zeilenumbrüche und Leerzeichen (Base64) – so übersteht der Ausdruck jede Kommandozeilen-
        // Zerlegung unter Windows unverändert (auch Startskripte, die Argumente weiterreichen). Lesbar: <see cref="Decode"/>.
        return "import base64;exec(base64.b64decode('" + Convert.ToBase64String(new UTF8Encoding(false).GetBytes(sb.ToString())) + "').decode('utf-8'))";
    }

    /// <summary>Den Python-Code eines Startausdrucks lesbar machen (Prüfung, Tests).</summary>
    public static string Decode(string expr)
    {
        const string head = "import base64;exec(base64.b64decode('", tail = "').decode('utf-8'))";
        if (!expr.StartsWith(head, StringComparison.Ordinal) || !expr.EndsWith(tail, StringComparison.Ordinal)) throw new FormatException("Kein Trendo-Startausdruck.");
        return Encoding.UTF8.GetString(Convert.FromBase64String(expr[head.Length..^tail.Length]));
    }
}
