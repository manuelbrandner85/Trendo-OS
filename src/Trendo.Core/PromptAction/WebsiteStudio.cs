namespace Trendo.Core.PromptAction;

/// <summary>
/// Vertrag für Verbesserung 43, Punkte 3–8 (Webseiten-Studio). Dieser Kern setzt ihn nicht um
/// und registriert keine Webseiten-Aktion. Ein anderer Auftrag hängt eine Klasse hier ein,
/// über <see cref="PromptActions.CreateDefault"/>.
/// Veröffentlichen, Pixel-Streaming und jede Wirkung nach außen laufen nur als
/// <see cref="SideEffectClass"/> durch das bestehende Freigabetor, nie aus dem Plan allein.
/// </summary>
public interface IWebsitePageAdapter : IStudioActionAdapter
{
    /// <summary>Arten, die das Studio wirklich bauen kann. Zum Beispiel Startseite, Shop, Portfolio, Blog, Firma.</summary>
    IReadOnlyList<string> PageKinds { get; }

    /// <summary>Stil-Vorlagen mit einem Klick. Danach bleiben Schriften und Farben änderbar.</summary>
    IReadOnlyList<string> StylePresets { get; }

    bool SupportsScrollStory { get; }
    bool SupportsBrowser3D { get; }
    bool SupportsParticles { get; }
    bool SupportsVideoBackground { get; }

    /// <summary>Ohne erreichbaren Server immer false. Nur relevant, wenn der Auftrag Pixel-Streaming ausdrücklich verlangt.</summary>
    bool PixelStreamingAvailable { get; }

    /// <summary>GLB aus dem eigenen Projekt. false, wenn die Datei fehlt oder 3D im Browser nicht geht.</summary>
    bool CanPlaceModel(string relativeGlbPath);

    /// <summary>Schon gerenderte Bilder oder Videos für Scroll-Effekte. Kein Pixel-Streaming.</summary>
    bool CanPlaceRenderedClip(string relativeMediaPath);

    /// <summary>Wendet eine vorhandene Stil-Vorlage an und schreibt die geänderte Datei.</summary>
    AdapterOutcome ApplyStyle(AdapterCall call, string styleId);

    /// <summary>Handy, Ladezeit, Barrierefreiheit, SEO-Grundlagen und Links. Die Note kommt nur aus dieser Prüfung.</summary>
    AdapterOutcome CheckPage(AdapterCall call);
}
