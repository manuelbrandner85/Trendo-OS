# I42-14 Fachprüfung je Studio

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_42.md`, Abschnitt 3, Funktion 14. Wortlaut: „Fachspezifische Lint-, Schema-, Render-, Dokument-, Laufzeit-, Geometrie-, Medien- oder Formatprüfungen.“

**Wörter:** Fachprüfung (QA) = die Kontrolle, die zu diesem Studio gehört. Lint = feste Regeln am Text oder an der Datei, zum Beispiel fehlende Angaben. Schema = ob die Datei die erwartete Form hat. Render = ein echt erzeugtes Bild oder Video, nicht eine Behauptung. Geometrie = Maße und Form eines 3D-Modells. Befund = ein Satz, was an der Datei hakt.

## Ziel und Nutzen

Neben der reinen Rechnung prüft jedes Fach seine eigene Datei: Text, Tabelle, Seite, Geometrie, Bild, Ton oder Lauf. Die Prüfung liest das, was wirklich erzeugt wurde. Ein fehlendes Werkzeug bleibt ein Befund, kein erfundenes „bestanden“.

## Nutzerablauf

1. Nach dem Schreiben der Datei läuft die Prüfung dieses Studios.
2. Die Person sieht die Befunde als Sätze.
3. Was die Prüfung sicher lokal ändern kann, bleibt eine eigene Aktion dieses Studios. Der Auftragskern hat keinen Knopf „Jetzt beheben“.
4. Ein nicht bestandener Befund verhindert „QA bestanden“.
5. Fehlt das Werkzeug (Blender, Unreal, LibreOffice, ffmpeg), nennt der Befund das Werkzeug und erfindet kein Render.

## Anforderungen

- Die Prüfung liefert `QaResult` an `AdapterOutcome.Qa`. `Judge` wertet sie aus (Funktion 13).
- Vorhandene Prüfungen, noch nicht als Auftragsadapter registriert:
  - Brief: `Letters.Validate` und `Proofread.Check` in `src/Trendo.Core/Letters.cs`
  - Office: `Ooxml`, `Csv`, `Pdf` in `src/Trendo.Core/Sys/Sys10.cs`
  - Webseite: `WebsiteQuality.Check` in `src/Trendo.Core/Website/WebsiteQuality.cs` (Handy, Ladezeit, Barrierefreiheit, SEO, Links als lokale Dateiprüfung)
  - 3D: `Studio3D.Inspect` in `src/Trendo.Core/Studio3D.cs`, nur mit vorhandenem Blender
  - Medien: `MediaStudio.Duration` über ffprobe neben ffmpeg in `src/Trendo.Core/MediaStudio.cs`
  - Spiele: `GameStudio.FindEngines` in `src/Trendo.Core/GameStudio.cs`. Ein fehlendes Unreal bleibt der Satz aus `WebsiteUnreal.Describe` in `src/Trendo.Core/Website/WebsiteFiles.cs`, wenn die Webseite diesen Weg nutzt. Der Auftragskern ruft ihn nicht auf.
- Ein Render oder Transcode gilt nur mit Protokoll und vorhandener Datei. `Studio3D.Preview` und `MediaStudio.Run` brauchen das Programm auf dem PC.
- `docs/improvement-42/repository-audit.md` hält fest: auf dem geprüften Linux-Image lagen `ffmpeg` und `ffprobe` im PATH, `blender`, `soffice` und Unreal nicht. Diese Spezifikation übernimmt das nicht als neue Messung.
- Abschnitt 10, Prüffall 11: Blender oder Unreal nur bei vorhandener Werkzeugkette, sonst `blocked`.

## Betroffene Module

- `src/Trendo.Core/PromptAction/StudioWorkflow.cs` – `Judge`, `Accept`
- `src/Trendo.Core/Letters.cs` – `Letters.Validate`, `Proofread`
- `src/Trendo.Core/Sys/Sys10.cs`
- `src/Trendo.Core/Website/WebsiteQuality.cs`, `src/Trendo.App/MainWindow.W4Web.cs`
- `src/Trendo.Core/Studio3D.cs` – `Inspect`, `Preview`
- `src/Trendo.Core/MediaStudio.cs`
- `src/Trendo.Core/GameStudio.cs` – `FindEngines`
- `src/Trendo.Core/Website/WebsiteFiles.cs` – `WebsiteUnreal`

## Sicherheit und Datenschutz

- Lokal zuerst. Die genannten Prüfungen lesen Dateien auf dem PC.
- Cloud nur nach Opt-in. Eine Fachprüfung lädt das Artefakt nicht zur Bewertung hoch.
- Kein kostenpflichtiger Dienst. Ein kostenpflichtiges Audit-API ist keine Voraussetzung.
- Keine Geheimnisse in den Befunden. Sie nennen Datei und Regel. Ablage: `src/Trendo.Core/Secrets.cs`.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs` braucht die lokale Prüfung nicht. Die Prüfung veröffentlicht nichts.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren. `SPEC-12` bleibt ohne Antivirus-Paket.

## Abhängigkeiten

- Funktion 10 ruft die Prüfung im Adapter auf. Ohne Adapter bleibt sie im jeweiligen Studio und nicht in der Auftragskette.
- Funktion 13 ist die gemeinsame Hülle `QaResult`.
- Keine Abhängigkeit zum KI-Laden. Pull Request #252 ist dafür nicht erforderlich.

## Akzeptanzkriterien

- Ein Adapter ohne `Qa` besteht `Judge` nicht.
- Ein Befund nennt die Datei oder die Regel in einem deutschen Satz.
- Ohne Blender liefert der 3D-Weg kein gerendertes Bild und keinen Status „QA bestanden“ für einen Render.
- `WebsiteQuality` bleibt eine Prüfung der lokalen Dateien. Der Auftragskern ruft sie erst auf, wenn ein Web-Adapter registriert ist.

## Offene Fragen

- Die Webseite hat „Qualität prüfen“ und „Jetzt beheben“ in `MainWindow.W4Web.cs`. Soll der Auftrag diese Knöpfe weiter nutzen oder ein eigenes QA-Feld in der Leiste bekommen?
- Welche Prüfungen zählen als Lint, welche als Render, solange kein Adapter die `CheckId` festlegt?

## Ist-Stand

`Judge` ist im Kern. Brief, Office, Webseite, 3D, Medien und Spiele haben eigene Prüfungen in den genannten Dateien. Keine davon ist in `PromptActions.CreateDefault()` eingetragen. Die Webseiten-Prüfung läuft über `WebsiteStudio`, nicht über die Auftragsleiste.
