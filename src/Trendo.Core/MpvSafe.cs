using System.Text;

namespace Trendo.Core;

/// <summary>T-0764 (8. Prüfrunde): Jeder mpv-Start von Trendo – für alle Profile – lädt nichts aus %APPDATA%\mpv bzw. „portable_config“: keine
/// Einstellungen (mpv.conf, input.conf), keine Skripte (Lua/JavaScript, auch nicht aus dem Benutzerordner) und keine Standard-Tastenbelegung (darüber
/// wäre die eingebaute Konsole mit „run“/„subprocess“ erreichbar). Die Tastenbelegung kommt aus einer festen Trendo-Datei, die vor jedem Start neu
/// geschrieben wird: leer, oder – wo eine Tastatur gebraucht wird (eigenes Player-Fenster) – nur sichere Befehle (Pause, Spulen, Lautstärke, Vollbild,
/// Beenden). Nie: run, subprocess, load-script, script-binding, script-message, keybind, change-list, set/… für Optionen mit Dateipfaden.</summary>
public static class MpvSafe
{
    /// <summary>Feste, sichere Tastenbelegung für ein eigenes Player-Fenster.</summary>
    public const string SafeKeys = """
SPACE cycle pause
p cycle pause
LEFT seek -5
RIGHT seek 5
UP add volume 5
DOWN add volume -5
m cycle mute
f cycle fullscreen
ESC set fullscreen no
q quit
""";

    /// <summary>Erlaubte Befehle einer Trendo-Tastenbelegung (Befehl + Ziel). Alles andere – vor allem run, subprocess, load-script, script-binding,
    /// script-message, keybind, change-list, load-config-file – ist ausgeschlossen.</summary>
    static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "cycle pause", "cycle mute", "cycle fullscreen", "seek -5", "seek 5", "add volume 5", "add volume -5", "set fullscreen no", "quit",
    };

    /// <summary>Die festen Argumente für jeden mpv-Start. <paramref name="keys"/>: sichere Tastenbelegung statt leerer Datei.</summary>
    public static List<string> Args(bool keys = false, string? confDir = null)
    {
        var dir = confDir ?? Path.Combine(Path.GetTempPath(), "trendo-mpv-" + Environment.ProcessId);
        Directory.CreateDirectory(dir);
        var conf = Path.Combine(dir, keys ? "trendo-tasten.conf" : "trendo-leer.conf");
        File.WriteAllText(conf, keys ? SafeKeys : "", new UTF8Encoding(false)); // vor jedem Start neu – ein ausgetauschter Inhalt hält nicht
        return ["--no-config", "--load-scripts=no", "--input-default-bindings=no", "--input-conf=" + conf];
    }

    /// <summary>Ist eine Tastenbelegung sicher? Jede Zeile: Taste + genau ein erlaubter Befehl (keine Befehlsketten mit „;“, keine Präfixe).</summary>
    public static bool IsSafeInputConf(string text) =>
        text.Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#'))
            .All(l => l.IndexOfAny([';', '#', '$', '`']) < 0 && l.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries) is [_, var cmd] && Allowed.Contains(string.Join(' ', cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries))));
}
