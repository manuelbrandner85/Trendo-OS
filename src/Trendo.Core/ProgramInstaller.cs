namespace Trendo.Core;

/// <summary>
/// Abgestimmter Entwurf mit BUILD &amp; RELEASE.
/// Diese Datei lädt nichts herunter und startet nichts.
/// Den echten Baustein baut BUILD &amp; RELEASE. Hier steht nur die Schnittstelle.
/// Der Katalog als YAML kommt später. Solange kein Baustein eingetragen ist, gibt es keinen Knopf.
/// </summary>
public interface IProgramInstaller
{
    Task<ProgramOffer?> GetOfferAsync(string catalogId, CancellationToken ct);
    Task<InstallResult> InstallAsync(string catalogId, IProgress<InstallProgress> progress, CancellationToken ct);
    Task<bool> IsInstalledAsync(string catalogId, CancellationToken ct);
}

/// <summary>
/// Angebot für ein Programm. Sha256 und SizeBytes dürfen leer sein, weil winget nicht immer beide Werte liefert.
/// Größe ist der lesbare Text (zum Beispiel „400 MB“). SizeBytes ist die genaue Zahl, wenn sie bekannt ist.
/// </summary>
public sealed record ProgramOffer(
    string Name,
    string Quelle,
    string? Groesse,
    string? Lizenz,
    string? Hinweis,
    bool IsFree,
    bool NeedsUserStep,
    string? InfoLink,
    string? Sha256,
    long? SizeBytes);

public record InstallResult(bool Success, string? UserMessageDe);

/// <summary>Kurzer Stand für den Fortschrittsbalken. Abbrechen läuft über das Abbruch-Token, nicht über diesen Satz.</summary>
public sealed record InstallProgress(int Percent, string Meldung);

public static class ProgramInstall
{
    public const string Aktion = "program.install";
}
