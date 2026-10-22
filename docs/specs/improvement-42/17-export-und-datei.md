# I42-17 Export und echte Datei

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_42.md`, Abschnitt 3, Funktion 17. Wortlaut: „Wirklich existierende Dateien, prüfbare Pfade/Checksummen, Formate, Importierbarkeit und Download-/Öffnen-Aktion.“

**Wörter:** Export = eine echte Datei in einem genannten Format auf diesem PC. Prüfsumme (SHA-256, Checksumme) = der Fingerabdruck dieser Datei. Pfad = der Ort der Datei im Projekt. Importierbarkeit = die Datei lässt sich mit dem passenden Programm wieder öffnen. Öffnen und Herunterladen bleiben auf diesem PC und sind kein Hochladen ins Internet.

## Ziel und Nutzen

Am Ende liegt eine Datei, die die Person auf dem PC öffnen kann. Pfad und SHA-256 stammen von dieser Datei. Ein Format wird nur genannt, wenn der vorhandene Export es schreibt. Ein Download oder ein Öffnen ist eine eigene Aktion und kein Upload.

## Nutzerablauf

1. `Accept` prüft `File.Exists` und schreibt die SHA-256 mit `ForensicsLab.Sha256`.
2. Die Leiste zeigt den relativen Pfad und die ersten 12 Zeichen der Prüfsumme.
3. Die Person öffnet die Datei mit dem Programm, das dieses Format schon kennt.
4. Ein Export in ein zweites Format läuft nur über den vorhandenen Export und nur, wenn das Werkzeug da ist.
5. Ohne Datei gibt es keinen Export und keinen Öffnen-Knopf, der eine leere Hülle anbietet.

## Anforderungen

- Die Prüfsumme ist `ForensicsLab.Sha256` in `src/Trendo.Core/W3/Forensics.cs` (SHA-256, klein hex). Sie ändert die 120-Stunden-Regel nicht.
- Der Pfad bleibt relativ und im Projekt (`ResolveInProject`).
- Vorhandene Exporte, noch ohne Auftragsadapter:
  - Brief als DOCX: `Letters.WriteDocx` und `Letters.ReadDocxText` in `src/Trendo.Core/Letters.cs`. PDF: `PdfExport.ConvertAsync` über LibreOffice (`soffice`). Ohne `soffice` gibt es kein PDF.
  - Office: `Ooxml` in `src/Trendo.Core/Sys/Sys10.cs`.
  - E-Mail-Entwurf: `MailCenter.SaveDraft` schreibt eine `.eml` mit `X-Unsent: 1` in `src/Trendo.Core/W3/Mail.cs`. Ein Versand ist das nicht.
  - Webseite: `WebsiteStudio.Build` legt einen Ordner an. `Pack` schreibt eine lokale Zip und `NICHT-HOCHGELADEN.txt` (`src/Trendo.Core/Website/WebsiteStudio.cs`). Das ist kein Upload.
  - 3D: `Studio3D.Formats` nennt `.glb`, `.obj`, `.stl`, `.fbx`, `.blend`. Der Lauf braucht Blender (`Studio3D.Create`, `Convert`).
  - Medien: `MediaStudio.Run` braucht ffmpeg.
- Die Leiste hat keinen Knopf „Öffnen“ und keinen Knopf „Herunterladen“.
- Abschnitt 10, Prüffall 1: Brief erzeugen, speichern, ändern, wieder öffnen, Exportdatei auf Existenz und Inhalt prüfen. Das setzt einen Brief-Adapter voraus. Er ist nicht registriert.
- Eine Meldung „fertige Datei“ ohne `File.Exists` ist ein Fehlschlag.

## Betroffene Module

- `src/Trendo.Core/PromptAction/StudioWorkflow.cs` – `Accept`
- `src/Trendo.Core/W3/Forensics.cs` – `Sha256`
- `src/Trendo.Core/Letters.cs` – `WriteDocx`, `PdfExport`
- `src/Trendo.Core/Sys/Sys10.cs` – `Ooxml`
- `src/Trendo.Core/W3/Mail.cs` – `SaveDraft`, `ToEml`
- `src/Trendo.Core/Website/WebsiteStudio.cs` – `Build`, `Pack`
- `src/Trendo.Core/Studio3D.cs`, `src/Trendo.Core/MediaStudio.cs`
- `src/Trendo.App/MainWindow.PromptAction.cs` – Pfadzeile
- `src/Trendo.Core/ProjectStore.cs`

## Sicherheit und Datenschutz

- Lokal zuerst. Export und Zip bleiben auf diesem PC.
- Cloud nur nach Opt-in. Ein Hoster, ein Formular oder ein Git-Push starten nicht aus dem Export.
- Kein kostenpflichtiger Dienst. LibreOffice, Blender, ffmpeg und Unreal werden nicht durch einen Bezahldienst ersetzt, wenn sie fehlen.
- Keine Geheimnisse in der Zip oder in der `.eml`. `NICHT-HOCHGELADEN.txt` ist ein Hinweis. Ablage der Schlüssel: `src/Trendo.Core/Secrets.cs`.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs`. Eine lokale Datei ist keine Veröffentlichung. `Pack` der Webseite läuft über das vorhandene Tor mit der Aktion `website.pack` und ist kein Auftrag der Leiste.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Funktion 10 und 11 erzeugen die Datei, deren Prüfsumme hier steht.
- Funktion 18 bleibt davor, sobald der Export eine Wirkung nach außen wäre.
- Keine Abhängigkeit zum KI-Laden. Pull Request #252 ist dafür nicht erforderlich.

## Akzeptanzkriterien

- Jede Datei in `Artifacts` existiert im Projekt, und `Sha256` ist die SHA-256 dieser Datei.
- Ein PDF-Export ohne `soffice` liefert kein PDF und meldet nicht „exportiert“.
- `SaveDraft` schreibt eine `.eml` mit `X-Unsent: 1` und sendet keine Nachricht.
- Die Leiste nennt denselben relativen Pfad, den `lauf.json` speichert.

## Offene Fragen

- Die Quelle verlangt eine Öffnen- oder Download-Aktion. Die Leiste zeigt nur den Pfad. Soll „Öffnen“ die vorhandene Studio-Ansicht laden (Brief, Webseite, 3D)?
- PDF hängt an LibreOffice. Reicht DOCX über `Letters.WriteDocx` als erster belegter Export, solange `soffice` fehlt?

## Ist-Stand

Prüfsumme und Existenzcheck sind im Kern. DOCX, PDF, OOXML, EML, Web-Ordner, 3D und Medien haben eigene Schreiber und sind nicht an `CreateDefault()` angeschlossen. Die Leiste öffnet die Datei nicht.
