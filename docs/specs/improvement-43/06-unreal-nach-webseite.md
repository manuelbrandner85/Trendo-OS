# I43-06 Unreal nach Webseite

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_43.md`, Punkt 6. Wortlaut: „Unreal → Webseite: gerenderte Videos/Bildsequenzen für Scroll-Effekte; Pixel Streaming nur auf ausdrücklichen Wunsch (braucht Server).“

## Ziel und Nutzen

Fertige Bilder aus Unreal werden auf der Seite als Video oder als Bildfolge im Scroll benutzt. Trendo gibt sie nicht als live gerendertes Spiel aus. Pixel Streaming bleibt eine Extra-Entscheidung, weil dafür ein eigener Server läuft.

## Nutzerablauf

1. Die Person sieht, ob Unreal auf diesem PC installiert ist.
2. Sie legt ein fertiges Video (die Fläche nennt mp4 und webm) oder einen Ordner mit Bildern (png, jpg) ab.
3. Die Seite nutzt diese Dateien für Hintergrund oder Scroll.
4. Pixel Streaming schaltet sie nur selbst ein. Trendo startet dann keinen Server von allein.
5. Ohne Mediendatei und ohne Unreal bleibt der Hinweis stehen. Es wird nichts gerendert und nichts verbunden.

## Anforderungen

- Die Seite nimmt gerenderte Videos und Bildsequenzen. Der Scroll-Effekt nutzt diese Dateien (`WebsiteMedia` in `src/Trendo.Core/Website/WebsiteFiles.cs`).
- Eine installierte Engine erlaubt noch keinen Render und keinen Server. `WebsiteUnreal.Describe` formuliert das so.
- Pixel Streaming nur auf ausdrücklichen Wunsch. Die Quelle sagt: es braucht einen Server.
- Der Wunsch allein startet den Server nicht. Die vorhandene Checkbox schreibt `PIXEL-STREAMING.md` (`WebsiteUnreal.WritePixelDoc`) und setzt `PixelStreaming` in `inhalt.json`.
- Fehlt Unreal und fehlt die Mediendatei, entsteht kein Ersatzfilm.

## Betroffene Module

- `src/Trendo.Core/GameStudio.cs` – `FindEngines`, `CreateUnrealProject`, Studio `CORE-06`
- `src/Trendo.Core/Website/WebsiteFiles.cs` – `WebsiteMedia`, `WebsiteUnreal`
- `src/Trendo.Core/Website/WebsiteModels.cs` – `MediaPath`, `PixelStreaming`, `VideoFile`, `Frames`, `UnrealNote`
- `src/Trendo.Core/Website/WebsiteHtml.cs` – Video- und Bildfolge-Auszeichnung
- `src/Trendo.App/MainWindow.W4Web.cs` – Medienfeld, Checkbox „Pixel Streaming nur dokumentieren“
- `src/Trendo.App/MainWindow.W4Game.cs` – Spiele-Studio
- `src/Trendo.App/StudioCatalog.cs` – „Spiele bauen“

## Sicherheit und Datenschutz

- Lokal zuerst. Video und Bilder werden in den Seitenordner kopiert.
- Cloud nur nach Opt-in. Ein Pixel-Streaming-Server ist ein Netzdienst. Verbinden und starten bleiben aus, bis die Person es ausdrücklich verlangt und einwilligt.
- Kein kostenpflichtiger Dienst. Trendo mietet keinen Streaming-Server und setzt kein Unreal-Abo voraus.
- Keine Geheimnisse in `PIXEL-STREAMING.md` oder `UnrealNote`. Die Notiz nennt keine Adresse, die die Person nicht selbst eingesetzt hat. Der vorhandene Text zählt nur, was sie selbst bräuchte, und speichert keine Server-URL.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs`: sobald ein Lauf einen Server starten oder eine Adresse verbinden will, ist das eine Wirkung und braucht eine einmalige Freigabe. Der dokumentierende Pfad heute führt `ApprovalGate` nicht aus, weil er keinen Server anfasst.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren. Sie ist auch nicht das Paket Verbesserung 39 (Remote App Fusion). Die Quelle erlaubt nur die eine Option Pixel Streaming auf Wunsch.

## Abhängigkeiten

- Punkt 4, Technik Video-Hintergrund und Scroll, zeigt die Dateien.
- Keine Abhängigkeit zum KI-Laden (#252).
- Unreal bleibt ein externes Programm (`docs/spec/MASTERPROMPT.md`, Abschnitt Blender und Unreal). Ohne Installation gibt es keinen Render.

## Akzeptanzkriterien

- Ein abgelegtes Video oder ein Bilderordner erscheint in der Seite als lokale Datei (`videoFile` oder `frames`).
- Ohne diese Datei und ohne Engine ist `unrealNote` der Hinweis aus `WebsiteUnreal.Describe`. Es liegt keine erfundene mp4 im Ordner.
- Mit gesetztem `PixelStreaming` liegt `PIXEL-STREAMING.md` im Ordner. Es läuft kein Server, und es wird keine Verbindung geöffnet.
- Ohne das Häkchen entsteht diese Datei nicht, und `pixelStreaming` in `inhalt.json` bleibt falsch.

## Offene Fragen

- Die Quelle erlaubt Pixel Streaming auf ausdrücklichen Wunsch. Der Code dokumentiert nur. Soll der Wunsch vorerst bei dieser Notiz bleiben, bis ein freigegebener Server-Pfad über `ApprovalGate` existiert?
- Wer liefert das Video, wenn Unreal installiert ist: die Person, oder ein späterer, extra freigegebener Render-Auftrag? Die Quelle sagt „gerenderte Videos“, der Code startet keinen Render.

## Ist-Stand

Status, Kopie von Video oder Bildfolge und die Notiz sind in `main`. T-0430 hat keinen Unreal-Render und keine Streaming-Verbindung ausgeführt.
