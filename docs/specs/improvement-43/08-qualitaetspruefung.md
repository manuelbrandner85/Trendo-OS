# I43-08 Qualitätsprüfung der Webseite

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_43.md`, Punkt 8. Wortlaut: „Automatische Qualitätsprüfung jeder Webseite (Handy, Ladezeit, Barrierefreiheit, SEO-Grundlagen, Links) mit Note und „Jetzt beheben“.“

## Ziel und Nutzen

Jede erzeugte Seite bekommt eine Note. Die Person sieht, was auf dem Handy, bei der Ladezeit, bei der Barrierefreiheit, bei den SEO-Grundlagen und bei den Links hakt. „Jetzt beheben“ ändert nur das, was die Prüfung sicher lokal ändern kann, und zeigt danach die neue Note.

## Nutzerablauf

1. Nach dem Bau erscheint die Note von allein.
2. Die Person kann „Qualität prüfen“ erneut wählen.
3. Sie wählt „Jetzt beheben“.
4. Die neue Note zeigt, was sich geändert hat und was offen bleibt.
5. Offene Punkte bleiben als Text stehen. Die Note wird nicht auf volle Punktzahl gesetzt, solange Befunde da sind.

## Anforderungen

- Bereiche aus der Quelle: Handy, Ladezeit, Barrierefreiheit, SEO-Grundlagen, Links. Im Code heißen sie Handy, Ladezeit, Barrierefreiheit, SEO und Links (Feld `Areas` in `WebsiteQuality.cs`).
- Die Note ist eine Zahl von 0 bis 100 plus ein alltagsnaher Satz (`WebsiteQuality.Note`, `WebsiteQualityReport.Plain`).
- „Jetzt beheben“ ändert nur Befunde, deren Code in `Fixable` steht (`WebsiteQuality.CanFix`). Dazu gehören im Code unter anderem `viewport`, `description`, `lang`, `alt`, `contrast`, `motion`.
- Ein kaputter eigener Link wird erkannt und nicht durch einen erfundenen Zielanker ersetzt (Anker-Befund `anchor`).
- Die Prüfung liest die Dateien im Ordner. Sie ist kein Lauf gegen ein öffentliches Netz und kein Lighthouse (`Kommentar` in `WebsiteQuality`).
- Die Note liegt als `qualitaet.json` im Seitenordner (`WebsiteQuality.FileName`).

## Betroffene Module

- `src/Trendo.Core/Website/WebsiteQuality.cs` – `Check`, `Fix`, `Save`, `Note`
- `src/Trendo.Core/Website/WebsiteStudio.cs` – ruft `Check` und `Save` nach Bau, Änderung und Stil auf
- `src/Trendo.App/MainWindow.W4Web.cs` – Text `web-score`, Knöpfe „Qualität prüfen“ und „Jetzt beheben“
- `tests/Trendo.Core.Tests/WebsiteStudioTests.cs` – vorhandene Prüfungen dieser Note

## Sicherheit und Datenschutz

- Lokal zuerst. Es wird nichts zur Bewertung hochgeladen.
- Cloud nur nach Opt-in. Externe Skripte und Stile sind ein Befund, kein stiller Nachlade-Versuch.
- Kein kostenpflichtiger Dienst. Kein kostenpflichtiges Audit-API.
- Keine Geheimnisse in `qualitaet.json`. Die Befunde nennen Datei und Regel, keine Schlüssel.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs` braucht die Prüfung nicht. „Jetzt beheben“ schreibt lokale HTML- und CSS-Dateien und veröffentlicht nicht.
- Diese Funktion ist nicht Verbesserung 34 (Antivirus) und nicht die Systemprüfung `SPEC-12`. Sie ist nicht Verbesserung 23 oder 25 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Punkt 3 liefert den Ordner.
- Punkt 7 ändert Farben, danach läuft die Prüfung erneut (Kontrast).
- Keine Abhängigkeit zum KI-Laden (#252). Die Note ist deterministisch aus den Dateien.

## Akzeptanzkriterien

- Nach `WebsiteStudio.Build` liegt `qualitaet.json` im Ordner, mit Zahl, Satz und den fünf Bereichen, soweit dazu etwas zu sagen ist.
- „Jetzt beheben“ ändert einen behebbaren Befund und lässt einen nicht behebbaren Befund stehen.
- Ein fehlendes Sprungziel bleibt ein Link-Fehler. Es wird kein passendes `id` erfunden.
- Die Prüfung öffnet keine Netzwerkverbindung. Externe Skript-URLs in der Seite werden als Befund gezählt.

## Offene Fragen

- Die Quelle sagt „Ladezeit“. Die Prüfung schätzt sie aus der lokalen Dateigröße, sie misst keine echte Ladezeit im Netz. Reicht diese Schätzung?
- Welche Note gilt als bestanden, bevor Punkt 10 ein Paket anbietet? Die Quelle nennt keine Schwelle.

## Ist-Stand

`WebsiteQuality` und die beiden Knöpfe sind in `main`. T-0430 beschreibt die Note als lokale Dateiprüfung. Ein Lighthouse-Lauf ist dort ausdrücklich nicht geschehen.
