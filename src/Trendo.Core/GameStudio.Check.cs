using System.Text;

namespace Trendo.Core;

public static partial class GameStudio
{
    /// <summary>Kurzes Editor-Python: druckt eine Zeile, wenn das Projekt geladen ist. Führt selbst nichts am Spiel aus.</summary>
    public const string ProjectCheckScript = """
import unreal
print("TRENDO_UE ok file=" + str(unreal.Paths.get_project_file_path()))
""";

    public static IReadOnlyList<string> CheckArguments(string uproject, string script) =>
    [
        uproject,
        "-run=pythonscript",
        "-script=" + script,
        "-unattended",
        "-nosplash",
        "-NullRHI",
        "-stdout",
        "-FullStdOutLogOutput",
    ];

    public static bool CheckSucceeded(string output) => output.Contains("TRENDO_UE ok", StringComparison.Ordinal);

    public sealed record ProjectCheck(bool Ran, bool Ok, string Message, string Output);

    /// <summary>Startet UnrealEditor-Cmd.exe mit dem Python-Skript. Ohne Programmdatei wird nichts gestartet.</summary>
    public static ProjectCheck CheckProject(string editor, string uproject, int timeoutMs = 180000)
    {
        if (!File.Exists(editor))
            return new(false, false, "UnrealEditor-Cmd.exe liegt im Ordner der gefundenen Engine nicht.", "");
        if (!File.Exists(uproject))
            return new(false, false, "Die Projektdatei fehlt.", "");
        var script = Path.Combine(Path.GetTempPath(), "trendo-ue-" + Environment.ProcessId + ".py");
        File.WriteAllText(script, ProjectCheckScript, new UTF8Encoding(false));
        var r = Cli.Run(editor, CheckArguments(uproject, script), Path.GetDirectoryName(uproject), timeoutMs);
        var text = r.Out + "\n" + r.Err;
        var ok = CheckSucceeded(text);
        var msg = ok
            ? "Der Editor hat das Projekt per Python geprüft."
            : "Die Editor-Prüfung hat kein ok gemeldet." + (string.IsNullOrWhiteSpace(r.Err) ? "" : " " + OneLine(r.Err));
        return new(true, ok, msg, text);
    }

    static string OneLine(string s)
    {
        var t = s.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return t.Length > 240 ? t[..240] : t;
    }
}
