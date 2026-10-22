# I42-15 Vorschau aus dem Artefakt

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_42.md`, Abschnitt 3, Funktion 15. Wortlaut: „Echte Vorschau aus erstelltem Artefakt (oder sichtbare Kennzeichnung, wenn Vorschau unmöglich).“

**Wörter:** Vorschau = die Anzeige dessen, was die Datei wirklich hergibt. Artefakt = diese Datei im Projekt. Ist keine Vorschau möglich, steht der Grund daneben. Ein Platzhalter, der wie ein fertiges Werk aussieht, zählt nicht als Vorschau.

## Ziel und Nutzen

Die Person sieht das, was die Datei hergibt, nicht eine erfundene Abbildung. Gibt es keine Vorschau, steht das daneben. Ein Platzhalter, der wie ein fertiges Werk aussieht, gilt nicht als Vorschau.

## Nutzerablauf

1. Der Adapter setzt `AdapterOutcome.Preview` aus der erzeugten Datei.
2. Die Leiste zeigt höchstens 500 Zeichen dieses Textes unter den übrigen Zeilen.
3. Hat das Studio schon eine eigene Vorschau, zeigt diese die echte Datei (Brief, 3D, Webseite, Live).
4. Ist keine Vorschau möglich, bleibt `Preview` leer oder nennt den Grund. Der Status „Datei erstellt“ hängt an der Datei, nicht an der Vorschau.
5. Eine Vorschau ohne Datei gibt es nicht. `Accept` bricht ab, wenn die Datei fehlt.

## Anforderungen

- `RunState.Preview` und `StudioExecutionResult.Preview` tragen den Text. `StudioArtifact.PreviewRef` bleibt im Typ und wird von `Accept` auf null gesetzt.
- Die Leiste kürzt auf 500 Zeichen (`ShowResult` in `MainWindow.PromptAction.cs`).
- Vorhandene Vorschauen, die eine Datei oder ein Modell zeigen und nicht an die Auftragskette gehängt sind:
  - Brief: `LetterPreview` in `src/Trendo.App/MainWindow.W1Office.cs`
  - 3D: `Studio3D.Preview` in `src/Trendo.Core/Studio3D.cs` und das Bild `3d-preview-image` in `src/Trendo.App/MainWindow.W4Studio3D.cs`. Die Auftragsleiste sitzt dort im linken Bereich, damit die Vorschau bei 1366×768 sichtbar bleibt (`Drei_d_leiste_bleibt_in_der_seite`).
  - Webseite: `WebsitePreview` in `src/Trendo.App/WebsitePreview.cs`, aufgerufen aus `src/Trendo.App/MainWindow.W4Web.cs`
  - Live: `StartPreview` in `src/Trendo.App/MainWindow.Live.cs`
- Eine Vorschau, die das Werkzeug nicht erzeugen kann, nennt den Grund. `Studio3D.Preview` braucht Blender. Ohne Blender bleibt die Kennzeichnung, dass kein Render vorliegt.
- Die 3D-Leiste und die 3D-Vorschau sind zwei Dinge. Die Leiste zeigt den Auftragstext, das Bild zeigt das Modell.

## Betroffene Module

- `src/Trendo.Core/PromptAction/Model.cs` – `Preview` am Ergebnis
- `src/Trendo.Core/PromptAction/StudioWorkflow.cs` – `Accept` setzt `state.Preview`
- `src/Trendo.App/MainWindow.PromptAction.cs` – Ausschnitt in `auftrag-detail`
- `src/Trendo.App/MainWindow.W1Office.cs` – `LetterPreview`
- `src/Trendo.App/MainWindow.W4Studio3D.cs`, `src/Trendo.Core/Studio3D.cs`
- `src/Trendo.App/WebsitePreview.cs`, `src/Trendo.App/MainWindow.W4Web.cs`
- `src/Trendo.App/MainWindow.Live.cs`
- `tests/Trendo.App.Tests/PromptBarTests.cs` – `Drei_d_leiste_bleibt_in_der_seite`

## Sicherheit und Datenschutz

- Lokal zuerst. Die Vorschau liest die Datei im Projekt oder das lokale Werkzeug.
- Cloud nur nach Opt-in. Eine Vorschau wird nicht auf einen Vorschaudienst geladen.
- Kein kostenpflichtiger Dienst. Ein Bezahl-Renderer ist keine Voraussetzung.
- Keine Geheimnisse in den 500 Zeichen. Der Adapter liefert den Vorschautext. Schlüssel bleiben in `src/Trendo.Core/Secrets.cs`.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs`. Die Vorschau veröffentlicht und sendet nicht.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Funktion 11 liefert die Datei, aus der die Vorschau kommt.
- Funktion 10 liefert sie nur mit Adapter. Ohne Adapter gibt es keinen Vorschautext aus der Leiste.
- Keine Abhängigkeit zum KI-Laden. Pull Request #252 ist dafür nicht erforderlich.

## Akzeptanzkriterien

- Ein gesetztes `Preview` erscheint in der Leiste, gekürzt bei mehr als 500 Zeichen.
- Ein leeres `Preview` erfindet kein Bild und keinen Beispieltext.
- Die 3D-Seite zeigt die Auftragsleiste und lässt das Vorschaubild mindestens 48 Pixel hoch (`Drei_d_leiste_bleibt_in_der_seite` bei 1366×768).
- Ohne die Datei im Projekt setzt `Accept` keinen Vorschautext als Erfolg.

## Offene Fragen

- Reicht der Textausschnitt in der Leiste, oder soll die vorhandene Brief-, 3D- oder Web-Vorschau das Artefakt des Auftrags zeigen?
- `PreviewRef` am Artefakt bleibt null. Soll die Vorschau eine eigene Datei im Projekt werden?

## Ist-Stand

Die Leiste kann einen Vorschautext zeigen. Die Fachvorschauen für Brief, 3D, Webseite und Live sind eigene Wege. Ein Auftragsadapter, der `Preview` füllt, ist nicht registriert.
