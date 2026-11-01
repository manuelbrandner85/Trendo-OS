namespace Trendo.Core;

/// <summary>Sicheres Ersetzen einer Datei (erst Zwischendatei schreiben, dann darüber verschieben). Virenscanner, Suchindex oder Sicherungsprogramme
/// halten frisch geschriebene Dateien unter Windows manchmal für einen Augenblick fest – dann meldet Windows „Zugriff verweigert“.
/// Statt den Speichervorgang zu verlieren, wartet Trendo kurz und versucht es erneut (zusammen höchstens etwa 5 Sekunden).</summary>
public static class SafeFile
{
    public const int Attempts = 20;

    public static void Move(string source, string destination)
    {
        for (var i = 0; ; i++)
            try { File.Move(source, destination, true); return; }
            catch (Exception e) when ((e is UnauthorizedAccessException || e is IOException and not FileNotFoundException and not DirectoryNotFoundException) && i < Attempts - 1)
            { Thread.Sleep(25 * (i + 1)); }
    }
}
