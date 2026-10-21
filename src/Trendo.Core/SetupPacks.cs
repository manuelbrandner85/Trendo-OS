namespace Trendo.Core;

public sealed record SetupPack(string Id, string Name, string Description, string[] Components, bool Default);

/// <summary>Basispaket und wählbare Studio-Pakete der Ersteinrichtung. Komponenten stammen aus dem DependencyCatalog.</summary>
public static class SetupPacks
{
    public static readonly SetupPack Base = new("basis", "Basispaket", "PDF-Ansicht, Videos & Musik, Archive (ZIP/7z), Briefvorlagen nach DIN 5008", ["sumatra", "mpv", "7zip"], true);

    public static readonly IReadOnlyList<SetupPack> Studios =
    [
        new("ki", "KI-Assistent (lokal)", "Trendo beantwortet Fragen direkt auf diesem PC – ohne Cloud", ["ollama", "model"], true),
        new("office", "Büro & Briefe", "Texte, Tabellen, Präsentationen", ["libreoffice"], true),
        new("dev", "Programmieren", "Git und Python für eigene Apps", ["git", "python"], false),
        new("media", "Medien", "Videoschnitt", ["kdenlive"], false),
        new("letter-ocr", "Briefe scannen", "Texterkennung für eingescannte Briefe", ["tesseract"], false),
    ];

    public static IEnumerable<Component> ComponentsFor(IEnumerable<string> packIds) =>
        new[] { Base }.Concat(Studios).Where(p => p.Id == "basis" || packIds.Contains(p.Id))
            .SelectMany(p => p.Components).Distinct().Select(DependencyCatalog.Get);
}
