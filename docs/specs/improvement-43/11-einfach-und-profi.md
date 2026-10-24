# I43-11 Einfach-Modus und Profi-Modus

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_43.md`, Punkt 11. Wortlaut: „Einfach-Modus (Eingabe, Vorschau, Fertig) und aufklappbarer Profi-Modus (Code, Ebenen, Einstellungen).“

## Ziel und Nutzen

Der Alltag braucht drei Dinge: Eingabe, Vorschau, Fertig. Wer genauer hinsehen will, klappt den Profi-Modus auf und sieht Code, Ebenen und Einstellungen. Beide Modi arbeiten am selben Ordner.

## Nutzerablauf

1. Das Studio öffnet im Einfach-Modus.
2. Die Person gibt den Auftrag ein, sieht die Vorschau und schließt mit „Fertig“ ab. „Fertig“ bedeutet: der Ordner liegt vor (Punkt 10), nicht dass etwas veröffentlicht wurde.
3. Sie klappt „Profi“ auf.
4. Dort sieht sie den Code der Seite, die Ebenen des Aufbaus und die Einstellungen zu Stil, Technik und Medien.
5. Eine Änderung im Profi-Modus erscheint in der Vorschau des Einfach-Modus. Zuklappen verwirft die Änderung nicht.

## Anforderungen

- Zwei Modi, eine Seite. Kein zweites Studio und keine zweite Navigationsebene über der bestehenden Studio-Liste hinaus.
- Einfach-Modus zeigt nur Eingabe, Vorschau und den Abschluss.
- Profi-Modus ist zugeklappt, bis die Person ihn öffnet, und enthält Code, Ebenen und Einstellungen.
- Beschriftungen deutsch und alltagsnah.
- Was der Profi-Modus an Code zeigt, ist der Code aus dem Ordner (`index.html`, `stil.css`, `seite.js`), nicht eine erfundene Ansicht.
- Einstellungen, die es schon gibt (Art, Technik, Stil, Farben, Modell, Medien, Vite), gehören in den Profi-Modus, sobald der Einfach-Modus sie nicht mehr zeigen soll.

## Betroffene Module

- `src/Trendo.App/MainWindow.W4Web.cs` – heute eine Fläche ohne Modus-Schalter: Beschreibung, Stil, Technik, 3D/Film, Qualität und Vorschau gleichzeitig
- `src/Trendo.App/WebsitePreview.cs` – Vorschau des Einfach-Modus
- `src/Trendo.App/MainWindow.PromptAction.cs` – Eingabe der gemeinsamen Leiste (Punkt 2)
- `src/Trendo.Core/Website/WebsiteHtml.cs` – die Dateien, die der Profi-Modus als Code zeigen würde
- `src/Trendo.Core/Website/WebsiteModels.cs` – `WebsiteSection` als naher vorhandener Begriff für Abschnitte der Seite
- `src/Trendo.App/StudioCatalog.cs` – „Webseite bauen“
- `src/Trendo.App/MainWindow.Look.cs` – Hilfetext der Seite `dev`

Ein Schalter „Einfach“ / „Profi“ kommt in `MainWindow.W4Web.cs` nicht vor.

## Sicherheit und Datenschutz

- Lokal zuerst. Der Profi-Modus liest die Dateien des Projektordners.
- Cloud nur nach Opt-in. Code anzeigen sendet den Quelltext nicht.
- Kein kostenpflichtiger Dienst für die beiden Modi.
- Keine Geheimnisse im aufgeklappten Code. Liegt versehentlich ein Schlüssel in einer Datei, zeigt der Profi-Modus die Datei so, wie sie im Ordner steht. Der Modus fügt keine Schlüssel ein und schreibt keine in Logs.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs`. „Fertig“ im Einfach-Modus ist der lokale Ordner. Veröffentlichen bleibt Punkt 10 und erscheint nicht als stiller Abschluss des Einfach-Modus.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Punkt 1 ist die Vorschau des Einfach-Modus.
- Punkt 2 ist die Eingabe des Einfach-Modus. Die Modellwahl dafür: **erst nach Merge von #252.**
- Der aufklappbare Profi-Modus (Code, Ebenen, Einstellungen) braucht den KI-Laden nicht.
- Punkt 10 definiert „Fertig“ als vorhandene Datei oder vorhandenen Ordner.

## Akzeptanzkriterien

- Beim Öffnen von „Webseite bauen“ sind Code, Ebenen und die Detail-Einstellungen zugeklappt. Sichtbar sind Eingabe, Vorschau und der Abschluss.
- Nach dem Aufklappen zeigt der Profi-Modus den Inhalt von `index.html` aus dem aktuellen Ordner. Vor dem ersten Bau sagt er, dass noch kein Code da ist.
- Eine Stiländerung im Profi-Modus ist nach dem Zuklappen in der Vorschau sichtbar.
- „Fertig“ erzeugt oder bestätigt den lokalen Ordner und lädt nichts hoch.

## Offene Fragen

- Was ist eine „Ebene“: die `WebsiteSection`-Liste, die CSS-Ebenen der Scroll-Technik, oder beides? Die Quelle definiert es nicht.
- Gilt der Modus nur für „Webseite bauen“, oder für jedes Studio aus Punkt 1? Der Satz steht in einer Webseiten-Verbesserung und sagt „Eingabe, Vorschau, Fertig“ ohne den Namen eines zweiten Studios.
- Die gemeinsame Auftragsleiste und das Feld `web-prompt` sind heute zwei Eingaben. Welche von beiden ist die Eingabe des Einfach-Modus?

## Ist-Stand

Die Seite `dev` zeigt Beschreibung, Vorlagen, Technik, Medien, Qualität und Vorschau auf einmal. Einen Einfach-Modus und einen aufklappbaren Profi-Modus gibt es nicht. T-0430 lässt Punkt 11 beim gemeinsamen Prompt-Kern offen.
