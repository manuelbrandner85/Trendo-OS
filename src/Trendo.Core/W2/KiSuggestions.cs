namespace Trendo.Core;

/// <summary>Vorschlag 36 (freigegeben 02.11.2026): jedes Studio schlägt die freien KIs vor, die dort wirklich helfen.
/// Ein Klick führt in die KI-Bibliothek – geladen wird erst dort nach Ja, aus der offiziellen Quelle.</summary>
public static class KiSuggestions
{
    static readonly Dictionary<string, KiKind[]> Map = new()
    {
        ["CORE-05"] = [KiKind.Modell3D, KiKind.BildErzeugen],
        ["CORE-06"] = [KiKind.Modell3D, KiKind.Geraeusche, KiKind.Musik],
        ["SPEC-04"] = [KiKind.Modell3D, KiKind.Geraeusche],
        ["SPEC-09"] = [KiKind.Modell3D],
        ["SPEC-01"] = [KiKind.Modell3D, KiKind.BildErzeugen],
        ["SPEC-08"] = [KiKind.BildErzeugen, KiKind.Modell3D],
        ["CORE-08"] = [KiKind.Geraeusche, KiKind.SpracheZuText, KiKind.BildVerbessern, KiKind.Musik],
        ["STUDIO-FILM-01"] = [KiKind.Video, KiKind.BildErzeugen, KiKind.TextZuSprache, KiKind.Musik, KiKind.Geraeusche],
        ["SPEC-15"] = [KiKind.Geraeusche, KiKind.SpracheZuText],
        ["X-07"] = [KiKind.Geraeusche, KiKind.BildErzeugen, KiKind.Musik],
        ["SYS-02"] = [KiKind.Musik],
        ["STUDIO-MUSIK-01"] = [KiKind.Musik, KiKind.Geraeusche],
        ["STUDIO-LETTER-01"] = [KiKind.TextZuSprache, KiKind.SpracheZuText],
        ["SYS-10"] = [KiKind.TextZuSprache, KiKind.SpracheZuText, KiKind.Uebersetzen],
        ["SYS-09"] = [KiKind.Uebersetzen],
        ["SPEC-06"] = [KiKind.TextZuSprache, KiKind.BildVerstehen],
        ["CORE-10"] = [KiKind.Wissen, KiKind.SpracheZuText],
        ["CORE-07"] = [KiKind.BildErzeugen, KiKind.Code, KiKind.Modell3D],
        ["X-03"] = [KiKind.Code],
        ["STUDIO-ONLINE-BUSINESS-01"] = [KiKind.BildErzeugen, KiKind.Uebersetzen],
        ["SYS-08"] = [KiKind.Uebersetzen, KiKind.BildVerstehen],
        ["CORE-02"] = [KiKind.Chat, KiKind.Denken],
    };

    public static IReadOnlyList<KiKind> For(string? studioId) => studioId is not null && Map.TryGetValue(studioId, out var k) ? k : [];
    public static IEnumerable<string> Studios => Map.Keys;
}
