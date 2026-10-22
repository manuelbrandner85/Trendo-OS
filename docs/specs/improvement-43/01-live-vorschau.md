# I43-01 Live-Vorschau in jedem Studio

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_43.md`, Punkt 1. Wortlaut: „Live-Vorschau in jedem Studio (echtes Ergebnis neben der Eingabe; wo unmöglich, ehrlicher Hinweis).“

## Ziel und Nutzen

Wer in einem Studio etwas eingibt, sieht daneben das echte Ergebnis. Ein Bild, ein Text oder eine Dateiansicht kommt aus dem, was Trendo gerade erzeugt hat. Geht das an dieser Stelle nicht, steht das in einem Satz da. Eine leere Fläche oder ein erfundenes Beispiel ersetzt das Ergebnis nicht.

## Nutzerablauf

1. Die Person öffnet ein Studio aus `src/Trendo.App/StudioCatalog.cs`.
2. Sie gibt den Auftrag oder die Fachdaten ein.
3. Neben der Eingabe erscheint das Ergebnis, sobald es wirklich vorliegt.
4. Ändert sie die Eingabe und bestätigt die Änderung, erneuert sich diese Ansicht aus dem neuen Ergebnis.
5. Fehlt das Werkzeug, die Datei oder eine Darstellung, bleibt die Fläche ehrlich: kurzer Grund, kein Ersatzbild.

## Anforderungen

- Die Regel gilt für jeden Eintrag in `StudioCatalog.ById`. In der Datei auf `main` (`af4f3a7`) sind das 54 Studios.
- Die Vorschau zeigt das erzeugte Artefakt. Ein Platzhalter, der wie ein fertiges Werk aussieht, erfüllt den Punkt nicht.
- Der Hinweis bei Unmöglichkeit nennt den Grund in Alltagssprache, zum Beispiel fehlendes Programm oder fehlende Datei.
- Die Ansicht hängt am Projektordner dieses Studios. Ein anderes Projekt liefert keine Vorschau hinein.
- Zwei Navigationsebenen bleiben die Obergrenze. Die Vorschau ist Teil der Studiofläche, keine eigene App.

## Betroffene Module

Katalog der Studios:

- `src/Trendo.App/StudioCatalog.cs`

Vorhandene Ergebnis- oder Prüfansichten, Suche nach „Vorschau“ in `src/Trendo.App/MainWindow*.cs`:

- `src/Trendo.App/MainWindow.W1Letter.cs` und `src/Trendo.App/MainWindow.W1Office.cs` (Brief)
- `src/Trendo.App/MainWindow.W4Studio3D.cs` und `src/Trendo.Core/Studio3D.cs` (3D, Methode `Preview`)
- `src/Trendo.App/MainWindow.W4Web.cs` und `src/Trendo.App/WebsitePreview.cs` (Webseite, Chrome-Bild lokal)
- `src/Trendo.App/MainWindow.Live.cs` (Livestream, SPEC-15)
- `src/Trendo.App/MainWindow.Spec14.cs` (Fräsbahnen)
- `src/Trendo.App/MainWindow.Sys02.cs` (Umbenennen, Plan vor dem Ausführen)
- `src/Trendo.App/MainWindow.Sys03.cs` (Downloads sortieren, Plan)
- `src/Trendo.App/MainWindow.Sys10.cs` (Druckvorschau)

Die Auftragsleiste `src/Trendo.App/MainWindow.PromptAction.cs` zeigt Status, Rückfragen und Dateinamen. Sie ist auf der 3D-Seite und auf dem Startbildschirm nicht eingehängt (`WithPromptBar`). Ein Live-Bild des Ergebnisses ist sie nicht.

Für die übrigen Katalogeinträge belegt diese Suche keine Ergebnisvorschau neben der Eingabe. Die Seiten liegen in `src/Trendo.App/MainWindow*.cs`. Eine zweite Studio-Oberfläche entsteht nicht.

## Sicherheit und Datenschutz

- Die Vorschau liest lokale Projektdateien. Sie lädt das Werk nicht zu einem Anbieter hoch.
- Cloud nur nach Opt-in. Eine Vorschau, die ein Netz brauchte, startet erst nach Einwilligung über `src/Trendo.Core/PromptAction/ConsentStore.cs`.
- Kein kostenpflichtiger Dienst. Die Web-Bildvorschau nutzt einen lokalen Chrome oder Chromium (`WebsitePreview.FindBrowser`), kein Abo.
- Keine Geheimnisse in der Vorschau-Datei, im Hinweistext oder im Audit. `src/Trendo.Core/Secrets.cs` bleibt der Ablageort.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs`, sobald die Vorschau eine Wirkung hätte (Versand, Veröffentlichung, Gerät). Das reine Ansehen löst diese Wirkung nicht aus.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Punkt 10: ohne echte Datei gibt es nichts Echtes zu zeigen.
- Punkt 11: der Einfach-Modus nennt die Vorschau als eine seiner drei Flächen.
- Keine Abhängigkeit zum KI-Laden. Pull Request #252 ist dafür nicht erforderlich.
- `src/Trendo.App/WebsitePreview.cs` liegt bewusst im App-Projekt. `docs/build-log/2026-10-22-CORE-07-webseite.md` begründet das mit QA-019 (`System.Net.Http` bleibt aus `Trendo.Core` draußen).

## Akzeptanzkriterien

- In einem Studio mit erzeugter Datei steht diese Datei sichtbar neben der Eingabe, nicht nur ein Statussatz.
- In einem Studio ohne Darstellungsmöglichkeit steht ein ehrlicher Hinweis. Es entsteht keine Ersatzdatei, die wie das Ergebnis aussieht.
- Ein zweites Projekt verändert die angezeigte Vorschau des ersten nicht.
- Die Web-Vorschau schreibt ein Bild aus der lokalen `index.html` oder nennt den Grund (`WebsitePreview.CaptureAsync`). Maße im vorhandenen Aufruf: 1280×800 und 390×844.

## Offene Fragen

- Reicht für Textstudios die vorhandene Briefvorschau, und für die übrigen eine Dateiliste mit Öffnen-Knopf, solange kein Bild möglich ist?
- Die 3D-Seite nimmt die Auftragsleiste heute nicht auf (`key is "start" or "3d"`). Soll die Live-Vorschau dort die bestehende Bildfläche in `MainWindow.W4Studio3D.cs` bleiben?
- Avalonia in diesem Repo hat laut `tasks/T-0430-webseite-profi.bericht.md` kein WebView. Soll die Web-Vorschau beim Chrome-Bild bleiben?

## Ist-Stand

Punkt 1 ist für „jedes Studio“ nicht abgeschlossen. Brief, 3D, Webseite, Live, Fräsen und einzelne Datei-Pläne haben eine Vorschau. Die gemeinsame Leiste aus Verbesserung 42 zeigt den Auftragsstand, nicht das Werk selbst.
