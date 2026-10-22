# Verbesserung 43 – Vertrag für das Webseiten-Studio

Punkte 3 bis 8 baut ein eigener Auftrag. Der gemeinsame Kern stellt dafür nur die Schnittstelle bereit:

`src/Trendo.Core/PromptAction/WebsiteStudio.cs` (`IWebsitePageAdapter`).

So hängt die Umsetzung ein:

- Klasse schreiben, die `IWebsitePageAdapter` erfüllt.
- Eine Instanz in `PromptActions.CreateDefault()` aufnehmen.
- Studio-ID im Katalog ist `CORE-07` („Webseite bauen“), solange ihr nichts anderes belegt.
- Dateien nur über `ProjectStore` im Auftrag. `Los` verlangt am Ende eine echte Datei. Ohne Datei bleibt der Lauf fehlgeschlagen.
- Veröffentlichen ist `SideEffectClass.Publish` und läuft durch `ApprovalGate`. Der Plan und `ApplyStyle` veröffentlichen nicht.
- `PixelStreamingAvailable` bleibt false, solange kein Server da ist. Pixel-Streaming nur, wenn der Auftrag es ausdrücklich verlangt.
- `CheckPage` setzt die Note nur aus Handy, Ladezeit, Barrierefreiheit, SEO-Grundlagen und Links. Eine erfundene Note ist keine Prüfung.
- `CanPlaceModel` und `CanPlaceRenderedClip` antworten false, wenn die Datei im Projekt fehlt.

Solange nichts registriert ist, bleibt „Webseite bauen“ auf `not_implemented`. Die Leiste zeigt dann „Nicht unterstützt“ und legt keine Datei an.
